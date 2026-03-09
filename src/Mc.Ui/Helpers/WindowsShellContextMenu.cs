using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Mc.Ui.Helpers;

/// <summary>
/// Shows the native Windows Explorer shell context menu for a file or folder,
/// identical to what appears when you right-click an item in Explorer.
/// </summary>
[SupportedOSPlatform("windows")]
internal static class WindowsShellContextMenu
{
    private const uint CMF_NORMAL       = 0x00000000;
    private const uint CMF_EXPLORE      = 0x00000001;
    private const uint TPM_LEFTALIGN    = 0x0000;
    private const uint TPM_RIGHTBUTTON  = 0x0002;
    private const uint TPM_RETURNCMD    = 0x0100;
    private const int  SW_SHOWNORMAL    = 1;
    private const uint WS_POPUP         = 0x80000000;
    private const uint WM_NULL          = 0x0000;
    private const uint WM_INITMENUPOPUP = 0x0117;
    private const uint WM_MEASUREITEM   = 0x002C;
    private const uint WM_DRAWITEM      = 0x002B;
    private const uint WM_MENUCHAR      = 0x0120;

    // [ThreadStatic] lets the static WndProc delegate read the current IContextMenu2/3
    // without a lock — the WndProc callback always fires on the same STA thread that
    // created the window, so these values are always correct for the current invocation.
    [ThreadStatic] private static IContextMenu2? _cm2;
    [ThreadStatic] private static IContextMenu3? _cm3;

    // The delegate must be a non-ThreadStatic static field so that
    // Marshal.GetFunctionPointerForDelegate produces a stable function pointer.
    private static readonly WndProcDelegate _wndProc = ShellMenuWndProc;

    // ── Public entry point ────────────────────────────────────────────────────

    /// <summary>
    /// Shows the Windows shell context menu for <paramref name="filePath"/> at the
    /// current mouse cursor position, then executes the chosen command.
    /// </summary>
    public static void Show(string filePath)
    {
        // Shell extension handlers (7-Zip, Notepad++, etc.) are in-process STA COM
        // servers. .NET console threads are MTA by default — spin a dedicated STA
        // thread so COM creates all handlers in the correct apartment.
        var thread = new Thread(() =>
        {
            OleInitialize(IntPtr.Zero);
            try   { ShowCore(filePath); }
            finally { OleUninitialize(); }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.IsBackground = true;
        thread.Start();
        thread.Join();
    }

    // ── Core logic ────────────────────────────────────────────────────────────

    private static void ShowCore(string filePath)
    {
        IntPtr pidlFull = IntPtr.Zero;
        IntPtr psfRaw   = IntPtr.Zero;
        IntPtr pCMRaw   = IntPtr.Zero;
        IntPtr pCM2Raw  = IntPtr.Zero;
        IntPtr pCM3Raw  = IntPtr.Zero;
        IntPtr hMenu    = IntPtr.Zero;
        IntPtr hwndHost = IntPtr.Zero;

        try
        {
            // 1. Create the helper window before anything else.
            //    • TrackPopupMenuEx needs an HWND owned by the calling thread in our
            //      process; GetConsoleWindow() returns conhost.exe's HWND (different
            //      process) which silently fails.
            //    • Some extension handlers inspect hwndOwner during creation and skip
            //      registering their items when it is null or foreign.
            hwndHost = CreateHelperWindow();
            if (hwndHost == IntPtr.Zero) return;

            // 2. Parse the file path into a shell PIDL.
            int hr = SHParseDisplayName(filePath, IntPtr.Zero, out pidlFull, 0, out _);
            if (hr != 0 || pidlFull == IntPtr.Zero) return;

            // 3. Bind to the parent IShellFolder.
            //    pidlChild points WITHIN pidlFull — must NOT be freed separately.
            var iidSF = typeof(IShellFolder).GUID;
            hr = SHBindToParent(pidlFull, ref iidSF, out psfRaw, out IntPtr pidlChild);
            if (hr != 0 || psfRaw == IntPtr.Zero) return;

            var shellFolder = (IShellFolder)Marshal.GetObjectForIUnknown(psfRaw);

            // 4. Get IContextMenu for the file.
            //    ref IntPtr (not IntPtr[]) is required — managed array marshaling
            //    corrupts the PCUITEMID_CHILD* call for InterfaceIsIUnknown methods.
            var iidCM = typeof(IContextMenu).GUID;
            shellFolder.GetUIObjectOf(hwndHost, 1, ref pidlChild, ref iidCM,
                                      IntPtr.Zero, out pCMRaw);
            if (pCMRaw == IntPtr.Zero) return;

            var contextMenu = (IContextMenu)Marshal.GetObjectForIUnknown(pCMRaw);

            // 5. Query for IContextMenu2 / IContextMenu3.
            //    Required for owner-drawn items: 7-Zip's submenu icon and other
            //    extensions use WM_INITMENUPOPUP / WM_DRAWITEM / WM_MEASUREITEM.
            //    Without forwarding those messages the items are added to the menu
            //    but render as blank/invisible entries.
            var iidCM2 = typeof(IContextMenu2).GUID;
            if (Marshal.QueryInterface(pCMRaw, ref iidCM2, out pCM2Raw) == 0 && pCM2Raw != IntPtr.Zero)
                _cm2 = (IContextMenu2)Marshal.GetObjectForIUnknown(pCM2Raw);

            var iidCM3 = typeof(IContextMenu3).GUID;
            if (Marshal.QueryInterface(pCMRaw, ref iidCM3, out pCM3Raw) == 0 && pCM3Raw != IntPtr.Zero)
                _cm3 = (IContextMenu3)Marshal.GetObjectForIUnknown(pCM3Raw);

            // 6. Populate the Win32 popup menu. idCmdFirst = 1.
            hMenu = CreatePopupMenu();
            contextMenu.QueryContextMenu(hMenu, 0, 1, 0x7FFF, CMF_NORMAL | CMF_EXPLORE);

            // 7. SetForegroundWindow routes keyboard events (Escape, arrows, Enter)
            //    to the menu's internal message loop.
            SetForegroundWindow(hwndHost);
            GetCursorPos(out POINT pt);

            uint cmd = TrackPopupMenuEx(
                hMenu,
                TPM_LEFTALIGN | TPM_RIGHTBUTTON | TPM_RETURNCMD,
                pt.X, pt.Y, hwndHost, IntPtr.Zero);

            PostMessage(hwndHost, WM_NULL, IntPtr.Zero, IntPtr.Zero);

            // 8. Execute the chosen command (cmd − 1 = offset from idCmdFirst = 1).
            if (cmd > 0)
            {
                var invoke = new CMINVOKECOMMANDINFO
                {
                    cbSize = Marshal.SizeOf<CMINVOKECOMMANDINFO>(),
                    fMask  = 0,
                    hwnd   = hwndHost,
                    lpVerb = (IntPtr)(int)(cmd - 1),
                    nShow  = SW_SHOWNORMAL,
                };
                contextMenu.InvokeCommand(ref invoke);
            }
        }
        finally
        {
            _cm2 = null;
            _cm3 = null;
            if (hwndHost != IntPtr.Zero) DestroyWindow(hwndHost);
            if (hMenu    != IntPtr.Zero) DestroyMenu(hMenu);
            if (pCM3Raw  != IntPtr.Zero) Marshal.Release(pCM3Raw);
            if (pCM2Raw  != IntPtr.Zero) Marshal.Release(pCM2Raw);
            if (pCMRaw   != IntPtr.Zero) Marshal.Release(pCMRaw);
            if (psfRaw   != IntPtr.Zero) Marshal.Release(psfRaw);
            if (pidlFull != IntPtr.Zero) CoTaskMemFree(pidlFull);
        }
    }

    // ── Custom WndProc ────────────────────────────────────────────────────────

    /// <summary>
    /// Forwards the owner-draw and menu-popup messages that shell extensions
    /// depend on to IContextMenu2/3::HandleMenuMsg so their items render correctly.
    /// </summary>
    private static IntPtr ShellMenuWndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        switch (msg)
        {
            case WM_INITMENUPOPUP:
            case WM_DRAWITEM:
            case WM_MEASUREITEM:
                try
                {
                    if (_cm3 != null)
                    {
                        _cm3.HandleMenuMsg2(msg, wParam, lParam, out _);
                        return IntPtr.Zero;
                    }
                    _cm2?.HandleMenuMsg(msg, wParam, lParam);
                }
                catch { /* never let a bad extension crash the WndProc */ }
                return IntPtr.Zero;

            case WM_MENUCHAR:
                if (_cm3 != null)
                {
                    try
                    {
                        _cm3.HandleMenuMsg2(msg, wParam, lParam, out IntPtr res);
                        return res;
                    }
                    catch { }
                }
                break;
        }
        return DefWindowProc(hWnd, msg, wParam, lParam);
    }

    // ── Helper window ─────────────────────────────────────────────────────────

    private const string HelperClassName = "Mc_ShellMenuHost";

    private static IntPtr CreateHelperWindow()
    {
        IntPtr hInstance = GetModuleHandle(null);
        var wc = new WNDCLASSEX
        {
            cbSize        = (uint)Marshal.SizeOf<WNDCLASSEX>(),
            lpfnWndProc   = Marshal.GetFunctionPointerForDelegate(_wndProc),
            hInstance     = hInstance,
            lpszClassName = HelperClassName,
        };
        RegisterClassEx(ref wc);  // ignore failure — class may already be registered
        return CreateWindowEx(0, HelperClassName, null, WS_POPUP,
            0, 0, 1, 1, IntPtr.Zero, IntPtr.Zero, hInstance, IntPtr.Zero);
    }

    // ── COM interfaces ────────────────────────────────────────────────────────

    [ComImport, Guid("000214E6-0000-0000-C000-000000000046"),
     InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellFolder
    {
        void ParseDisplayName(IntPtr hwnd, IntPtr pbc,
            [MarshalAs(UnmanagedType.LPWStr)] string pszDisplayName,
            out uint pchEaten, out IntPtr ppidl, ref uint pdwAttributes);
        void EnumObjects(IntPtr hwnd, uint grfFlags, out IntPtr ppenumIDList);
        void BindToObject(IntPtr pidl, IntPtr pbc, ref Guid riid, out IntPtr ppv);
        void BindToStorage(IntPtr pidl, IntPtr pbc, ref Guid riid, out IntPtr ppv);
        void CompareIDs(IntPtr lParam, IntPtr pidl1, IntPtr pidl2);
        void CreateViewObject(IntPtr hwndOwner, ref Guid riid, out IntPtr ppv);
        void GetAttributesOf(uint cidl, ref IntPtr apidl, ref uint rgfInOut);
        void GetUIObjectOf(IntPtr hwndOwner, uint cidl, ref IntPtr apidl,
            ref Guid riid, IntPtr rgfReserved, out IntPtr ppv);
        void GetDisplayNameOf(IntPtr pidl, uint uFlags, out IntPtr pName);
        void SetNameOf(IntPtr hwnd, IntPtr pidl,
            [MarshalAs(UnmanagedType.LPWStr)] string pszName,
            uint uFlags, out IntPtr ppidlOut);
    }

    [ComImport, Guid("000214e4-0000-0000-c000-000000000046"),
     InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IContextMenu
    {
        void QueryContextMenu(IntPtr hmenu, uint indexMenu,
            uint idCmdFirst, uint idCmdLast, uint uFlags);
        void InvokeCommand(ref CMINVOKECOMMANDINFO pici);
        void GetCommandString(IntPtr idCmd, uint uType,
            IntPtr pReserved, IntPtr pszName, uint cchMax);
    }

    /// <summary>Extends IContextMenu with owner-draw message handling.</summary>
    [ComImport, Guid("000214f4-0000-0000-c000-000000000046"),
     InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IContextMenu2
    {
        void QueryContextMenu(IntPtr hmenu, uint indexMenu,
            uint idCmdFirst, uint idCmdLast, uint uFlags);
        void InvokeCommand(ref CMINVOKECOMMANDINFO pici);
        void GetCommandString(IntPtr idCmd, uint uType,
            IntPtr pReserved, IntPtr pszName, uint cchMax);
        void HandleMenuMsg(uint uMsg, IntPtr wParam, IntPtr lParam);
    }

    /// <summary>Extends IContextMenu2 with HandleMenuMsg2 (returns a result value).</summary>
    [ComImport, Guid("bcfce0a0-ec17-11d0-8d10-00a0c90f2719"),
     InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IContextMenu3
    {
        void QueryContextMenu(IntPtr hmenu, uint indexMenu,
            uint idCmdFirst, uint idCmdLast, uint uFlags);
        void InvokeCommand(ref CMINVOKECOMMANDINFO pici);
        void GetCommandString(IntPtr idCmd, uint uType,
            IntPtr pReserved, IntPtr pszName, uint cchMax);
        void HandleMenuMsg(uint uMsg, IntPtr wParam, IntPtr lParam);
        void HandleMenuMsg2(uint uMsg, IntPtr wParam, IntPtr lParam, out IntPtr plResult);
    }

    // ── Structs ───────────────────────────────────────────────────────────────

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int X, Y; }

    [StructLayout(LayoutKind.Sequential)]
    private struct CMINVOKECOMMANDINFO
    {
        public int    cbSize;
        public uint   fMask;
        public IntPtr hwnd;
        public IntPtr lpVerb;
        public IntPtr lpParameters;
        public IntPtr lpDirectory;
        public int    nShow;
        public uint   dwHotKey;
        public IntPtr hIcon;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WNDCLASSEX
    {
        public uint    cbSize;
        public uint    style;
        public IntPtr  lpfnWndProc;
        public int     cbClsExtra;
        public int     cbWndExtra;
        public IntPtr  hInstance;
        public IntPtr  hIcon;
        public IntPtr  hCursor;
        public IntPtr  hbrBackground;
        public string? lpszMenuName;
        public string? lpszClassName;
        public IntPtr  hIconSm;
    }

    // ── Delegate ──────────────────────────────────────────────────────────────

    private delegate IntPtr WndProcDelegate(
        IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    // ── P/Invoke ──────────────────────────────────────────────────────────────

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHParseDisplayName(
        string pszName, IntPtr pbc, out IntPtr ppidl,
        uint sfgaoIn, out uint psfgaoOut);

    [DllImport("shell32.dll")]
    private static extern int SHBindToParent(
        IntPtr pidl, ref Guid riid, out IntPtr ppv, out IntPtr ppidlLast);

    [DllImport("user32.dll")]
    private static extern IntPtr CreatePopupMenu();

    [DllImport("user32.dll")]
    private static extern bool DestroyMenu(IntPtr hMenu);

    [DllImport("user32.dll")]
    private static extern uint TrackPopupMenuEx(
        IntPtr hmenu, uint fuFlags, int x, int y, IntPtr hwnd, IntPtr lptpm);

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT lpPoint);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool PostMessage(
        IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern ushort RegisterClassEx(ref WNDCLASSEX lpWndClass);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr CreateWindowEx(
        uint dwExStyle, string lpClassName, string? lpWindowName,
        uint dwStyle, int x, int y, int nWidth, int nHeight,
        IntPtr hWndParent, IntPtr hMenu, IntPtr hInstance, IntPtr lpParam);

    [DllImport("user32.dll")]
    private static extern bool DestroyWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern IntPtr DefWindowProc(
        IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr GetModuleHandle(string? lpModuleName);

    [DllImport("ole32.dll")]
    private static extern void CoTaskMemFree(IntPtr pv);

    [DllImport("ole32.dll")]
    private static extern int OleInitialize(IntPtr pvReserved);

    [DllImport("ole32.dll")]
    private static extern void OleUninitialize();
}
