using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Mc.Ui.Helpers;

/// <summary>
/// Shows the native Windows Explorer shell context menu for a file or folder,
/// identical to what appears when you right-click an item in Explorer.
/// Uses IShellFolder + IContextMenu COM interfaces via P/Invoke.
/// </summary>
[SupportedOSPlatform("windows")]
internal static class WindowsShellContextMenu
{
    // IContextMenu flags
    private const uint CMF_NORMAL      = 0x00000000;
    private const uint CMF_EXPLORE     = 0x00000001;

    // TrackPopupMenuEx flags
    private const uint TPM_LEFTALIGN   = 0x0000;
    private const uint TPM_RIGHTBUTTON = 0x0002;
    private const uint TPM_RETURNCMD   = 0x0100;

    private const int  SW_SHOWNORMAL   = 1;
    private const uint WS_POPUP        = 0x80000000;

    // Keep delegate alive for the lifetime of the process to prevent GC collection.
    private static readonly WndProcDelegate _wndProc = DefWindowProc;

    // ── Public entry point ────────────────────────────────────────────────────

    /// <summary>
    /// Shows the Windows shell context menu for <paramref name="filePath"/> at
    /// the current mouse cursor position. Blocks until the user dismisses the menu
    /// or selects a command, then executes the selected command.
    /// </summary>
    public static void Show(string filePath)
    {
        // Shell context menu handlers are in-process STA COM servers (7-Zip,
        // Notepad++, etc.). .NET console app threads are MTA by default, which
        // causes COM to marshal calls across apartments — most extension handlers
        // then silently fail to load, giving only the generic built-in menu.
        // Fix: run the entire menu interaction on a dedicated STA thread so COM
        // creates and calls the handlers in the correct apartment.
        var thread = new Thread(() => ShowCore(filePath));
        thread.SetApartmentState(ApartmentState.STA);
        thread.IsBackground = true;
        thread.Start();
        thread.Join();
    }

    private static void ShowCore(string filePath)
    {
        IntPtr pidlFull  = IntPtr.Zero;
        IntPtr psfRaw    = IntPtr.Zero;
        IntPtr pCMRaw    = IntPtr.Zero;
        IntPtr hMenu     = IntPtr.Zero;
        IntPtr hwndHost  = IntPtr.Zero;

        try
        {
            // 1. Parse the absolute path into a shell PIDL.
            int hr = SHParseDisplayName(filePath, IntPtr.Zero, out pidlFull, 0, out _);
            if (hr != 0 || pidlFull == IntPtr.Zero) return;

            // 2. Bind to the parent IShellFolder; ppidlChild points WITHIN pidlFull
            //    (must NOT be freed separately).
            var iidSF = typeof(IShellFolder).GUID;
            hr = SHBindToParent(pidlFull, ref iidSF, out psfRaw, out IntPtr pidlChild);
            if (hr != 0 || psfRaw == IntPtr.Zero) return;

            var shellFolder = (IShellFolder)Marshal.GetObjectForIUnknown(psfRaw);

            // 3. Ask the parent folder for an IContextMenu for the child item.
            //    ref IntPtr (not IntPtr[]) is required so the CLR emits a plain
            //    PCUITEMID_CHILD* pointer — managed array marshaling is wrong here.
            var iidCM = typeof(IContextMenu).GUID;
            shellFolder.GetUIObjectOf(IntPtr.Zero, 1, ref pidlChild, ref iidCM, IntPtr.Zero, out pCMRaw);
            if (pCMRaw == IntPtr.Zero) return;

            var contextMenu = (IContextMenu)Marshal.GetObjectForIUnknown(pCMRaw);

            // 4. Populate a Win32 popup menu. idCmdFirst = 1.
            hMenu = CreatePopupMenu();
            contextMenu.QueryContextMenu(hMenu, 0, 1, 0x7FFF, CMF_NORMAL | CMF_EXPLORE);

            // 5. TrackPopupMenuEx requires an HWND owned by the calling thread in
            //    our process. GetConsoleWindow() returns the console host's HWND
            //    (conhost.exe / Windows Terminal) which belongs to a different process
            //    and causes TrackPopupMenuEx to fail silently.
            //    Solution: create a minimal hidden popup window on this STA thread.
            hwndHost = CreateHelperWindow();
            if (hwndHost == IntPtr.Zero) return;

            GetCursorPos(out POINT pt);

            uint cmd = TrackPopupMenuEx(
                hMenu,
                TPM_LEFTALIGN | TPM_RIGHTBUTTON | TPM_RETURNCMD,
                pt.X, pt.Y,
                hwndHost,
                IntPtr.Zero);

            // 6. Invoke the selected command (cmd - 1 = offset from idCmdFirst = 1).
            if (cmd > 0)
            {
                var invoke = new CMINVOKECOMMANDINFO
                {
                    cbSize = Marshal.SizeOf<CMINVOKECOMMANDINFO>(),
                    fMask  = 0,
                    hwnd   = hwndHost,
                    lpVerb = (IntPtr)(int)(cmd - 1),  // MAKEINTRESOURCEA(id)
                    nShow  = SW_SHOWNORMAL,
                };
                contextMenu.InvokeCommand(ref invoke);
            }
        }
        finally
        {
            if (hwndHost != IntPtr.Zero) DestroyWindow(hwndHost);
            if (hMenu    != IntPtr.Zero) DestroyMenu(hMenu);
            if (pCMRaw   != IntPtr.Zero) Marshal.Release(pCMRaw);
            if (psfRaw   != IntPtr.Zero) Marshal.Release(psfRaw);
            if (pidlFull != IntPtr.Zero) CoTaskMemFree(pidlFull);
        }
    }

    // ── Helper window ─────────────────────────────────────────────────────────

    private const string HelperClassName = "Mc_ShellMenuHost";

    /// <summary>
    /// Creates a 1×1 hidden popup window owned by the calling thread.
    /// This gives TrackPopupMenuEx a valid in-process HWND to work with.
    /// </summary>
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

        // Ignore the return value — the class may already be registered.
        RegisterClassEx(ref wc);

        return CreateWindowEx(
            0, HelperClassName, null,
            WS_POPUP,
            0, 0, 1, 1,
            IntPtr.Zero, IntPtr.Zero, hInstance, IntPtr.Zero);
    }

    // ── COM interfaces ────────────────────────────────────────────────────────

    [ComImport]
    [Guid("000214E6-0000-0000-C000-000000000046")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
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

    [ComImport]
    [Guid("000214e4-0000-0000-c000-000000000046")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IContextMenu
    {
        void QueryContextMenu(IntPtr hmenu, uint indexMenu,
            uint idCmdFirst, uint idCmdLast, uint uFlags);
        void InvokeCommand(ref CMINVOKECOMMANDINFO pici);
        void GetCommandString(IntPtr idCmd, uint uType,
            IntPtr pReserved, IntPtr pszName, uint cchMax);
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
        public IntPtr lpVerb;       // MAKEINTRESOURCEA(id) for numeric invocation
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

    // ── Delegates ─────────────────────────────────────────────────────────────

    private delegate IntPtr WndProcDelegate(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    // ── P/Invoke declarations ─────────────────────────────────────────────────

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHParseDisplayName(
        string pszName, IntPtr pbc,
        out IntPtr ppidl, uint sfgaoIn, out uint psfgaoOut);

    [DllImport("shell32.dll")]
    private static extern int SHBindToParent(
        IntPtr pidl, ref Guid riid,
        out IntPtr ppv, out IntPtr ppidlLast);

    [DllImport("user32.dll")]
    private static extern IntPtr CreatePopupMenu();

    [DllImport("user32.dll")]
    private static extern bool DestroyMenu(IntPtr hMenu);

    [DllImport("user32.dll")]
    private static extern uint TrackPopupMenuEx(
        IntPtr hmenu, uint fuFlags,
        int x, int y, IntPtr hwnd, IntPtr lptpm);

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT lpPoint);

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
    private static extern IntPtr DefWindowProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr GetModuleHandle(string? lpModuleName);

    [DllImport("ole32.dll")]
    private static extern void CoTaskMemFree(IntPtr pv);
}
