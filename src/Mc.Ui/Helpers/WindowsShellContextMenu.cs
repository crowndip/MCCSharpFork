using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Mc.Ui.Helpers;

/// <summary>
/// Shows the native Windows Explorer shell context menu for a file or folder,
/// identical to what appears when you right-click an item in Explorer.
/// Uses SHCreateDefaultContextMenu (the same API Explorer uses internally)
/// so that all dynamic shell extension handlers (7-Zip, Notepad++, etc.)
/// are loaded correctly.
/// </summary>
[SupportedOSPlatform("windows")]
internal static class WindowsShellContextMenu
{
    // IContextMenu / QueryContextMenu flags
    private const uint CMF_NORMAL      = 0x00000000;
    private const uint CMF_EXPLORE     = 0x00000001;

    // TrackPopupMenuEx flags
    private const uint TPM_LEFTALIGN   = 0x0000;
    private const uint TPM_RIGHTBUTTON = 0x0002;
    private const uint TPM_RETURNCMD   = 0x0100;

    private const int  SW_SHOWNORMAL   = 1;
    private const uint WS_POPUP        = 0x80000000;
    private const uint WM_NULL         = 0x0000;

    // Registry
    private static readonly IntPtr HKEY_CLASSES_ROOT =
        (IntPtr)unchecked((int)0x80000000);
    private const uint KEY_READ = 0x20019;

    // Keep delegate alive to prevent GC collection.
    private static readonly WndProcDelegate _wndProc = DefWindowProc;

    // ── Public entry point ────────────────────────────────────────────────────

    /// <summary>
    /// Shows the Windows shell context menu for <paramref name="filePath"/> at
    /// the current mouse cursor position. Blocks until the user dismisses the
    /// menu or selects a command, then executes the selected command.
    /// </summary>
    public static void Show(string filePath)
    {
        // Shell extension handlers are in-process STA COM servers. .NET console
        // threads are MTA by default — spin up a dedicated STA thread so COM
        // creates all handlers in the correct apartment.
        var thread = new Thread(() =>
        {
            // OleInitialize is required by some shell handlers (OLE clipboard,
            // drag-and-drop internals). Pair with OleUninitialize in finally.
            OleInitialize(IntPtr.Zero);
            try   { ShowCore(filePath); }
            finally { OleUninitialize(); }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.IsBackground = true;
        thread.Start();
        thread.Join();
    }

    // ── Core implementation ───────────────────────────────────────────────────

    private static void ShowCore(string filePath)
    {
        IntPtr pidlFull    = IntPtr.Zero;  // full PIDL of file
        IntPtr pidlFolder  = IntPtr.Zero;  // full PIDL of parent folder
        IntPtr psfRaw      = IntPtr.Zero;  // IShellFolder* of parent
        IntPtr pCMRaw      = IntPtr.Zero;  // IContextMenu*
        IntPtr hMenu       = IntPtr.Zero;
        IntPtr hwndHost    = IntPtr.Zero;
        IntPtr apidlNative = IntPtr.Zero;  // native PCUITEMID_CHILD array (1 entry)
        IntPtr hkeysNative = IntPtr.Zero;  // native HKEY array
        var    hkeys       = new List<IntPtr>();

        try
        {
            // 1. Create the helper window first.
            //    TrackPopupMenuEx requires an HWND owned by the calling thread in
            //    our process. GetConsoleWindow() returns conhost.exe's HWND (a
            //    different process) which causes silent failure.
            //    The window must exist before GetUIObjectOf so extension handlers
            //    that inspect hwndOwner during init have a valid window to use.
            hwndHost = CreateHelperWindow();
            if (hwndHost == IntPtr.Zero) return;

            // 2. Parse the file path into a full shell PIDL.
            int hr = SHParseDisplayName(filePath, IntPtr.Zero, out pidlFull, 0, out _);
            if (hr != 0 || pidlFull == IntPtr.Zero) return;

            // 3. Bind to the parent IShellFolder. pidlChild points WITHIN pidlFull
            //    — must NOT be freed separately.
            var iidSF = typeof(IShellFolder).GUID;
            hr = SHBindToParent(pidlFull, ref iidSF, out psfRaw, out IntPtr pidlChild);
            if (hr != 0 || psfRaw == IntPtr.Zero) return;

            // 4. Obtain the parent folder's absolute PIDL directly from psfRaw.
            //    Re-parsing the directory path via SHParseDisplayName can produce a
            //    subtly different PIDL (e.g. different case or junction resolution)
            //    than the one psfRaw actually represents. The Properties verb combines
            //    pidlFolder + pidlChild to resolve the file path; a mismatch causes
            //    "The properties for this item are not available". Asking the folder
            //    object for its own PIDL guarantees consistency.
            SHGetIDListFromObject(psfRaw, out pidlFolder);

            // 5. Open the registry keys that define which shell extension handlers
            //    to load. Explorer passes these to SHCreateDefaultContextMenu so
            //    that dynamic handlers like 7-Zip (registered under HKCR\*\shellex
            //    \ContextMenuHandlers) are discovered. Without them, only static
            //    verbs (e.g. "Run as administrator" from HKCR\exefile\shell\runas)
            //    appear because those come directly from the IShellFolder and do not
            //    require the dynamic handler enumeration path.
            hkeys = OpenAssocKeys(filePath);

            // 6. Marshal the child PIDL and HKEY arrays into native memory for
            //    DEFCONTEXTMENU (the struct holds raw pointers, not managed arrays).
            apidlNative = Marshal.AllocHGlobal(IntPtr.Size);
            Marshal.WriteIntPtr(apidlNative, pidlChild);

            if (hkeys.Count > 0)
            {
                hkeysNative = Marshal.AllocHGlobal(IntPtr.Size * hkeys.Count);
                for (int i = 0; i < hkeys.Count; i++)
                    Marshal.WriteIntPtr(hkeysNative, i * IntPtr.Size, hkeys[i]);
            }

            // 7. Build the full default context menu via SHCreateDefaultContextMenu.
            //    This is the same function Explorer uses — it aggregates static verbs
            //    AND all dynamic shell extension handlers for the given file.
            var dcm = new DEFCONTEXTMENU
            {
                hwnd       = hwndHost,
                pidlFolder = pidlFolder,
                psf        = psfRaw,
                cidl       = 1,
                apidl      = apidlNative,
                cKeys      = (uint)hkeys.Count,
                aKeys      = hkeysNative,
            };
            var iidCM = typeof(IContextMenu).GUID;
            hr = SHCreateDefaultContextMenu(ref dcm, ref iidCM, out pCMRaw);
            if (hr != 0 || pCMRaw == IntPtr.Zero) return;

            var contextMenu = (IContextMenu)Marshal.GetObjectForIUnknown(pCMRaw);

            // 8. Populate the Win32 popup menu. idCmdFirst = 1.
            hMenu = CreatePopupMenu();
            contextMenu.QueryContextMenu(hMenu, 0, 1, 0x7FFF, CMF_NORMAL | CMF_EXPLORE);

            // 9. SetForegroundWindow before TrackPopupMenuEx routes keyboard events
            //    (Escape, arrow keys, Enter) to the menu's internal message loop.
            SetForegroundWindow(hwndHost);
            GetCursorPos(out POINT pt);

            uint cmd = TrackPopupMenuEx(
                hMenu,
                TPM_LEFTALIGN | TPM_RIGHTBUTTON | TPM_RETURNCMD,
                pt.X, pt.Y,
                hwndHost,
                IntPtr.Zero);

            // Flush any messages left in the queue after the modal loop exits.
            PostMessage(hwndHost, WM_NULL, IntPtr.Zero, IntPtr.Zero);

            // 10. Execute the chosen command.
            if (cmd > 0)
            {
                var invoke = new CMINVOKECOMMANDINFO
                {
                    cbSize = Marshal.SizeOf<CMINVOKECOMMANDINFO>(),
                    fMask  = 0,
                    hwnd   = hwndHost,
                    lpVerb = (IntPtr)(int)(cmd - 1),  // MAKEINTRESOURCEA(offset)
                    nShow  = SW_SHOWNORMAL,
                };
                contextMenu.InvokeCommand(ref invoke);
            }
        }
        finally
        {
            if (hwndHost    != IntPtr.Zero) DestroyWindow(hwndHost);
            if (hMenu       != IntPtr.Zero) DestroyMenu(hMenu);
            if (pCMRaw      != IntPtr.Zero) Marshal.Release(pCMRaw);
            if (psfRaw      != IntPtr.Zero) Marshal.Release(psfRaw);
            if (apidlNative != IntPtr.Zero) Marshal.FreeHGlobal(apidlNative);
            if (hkeysNative != IntPtr.Zero) Marshal.FreeHGlobal(hkeysNative);
            foreach (var hk in hkeys) RegCloseKey(hk);
            if (pidlFolder  != IntPtr.Zero) CoTaskMemFree(pidlFolder);
            if (pidlFull    != IntPtr.Zero) CoTaskMemFree(pidlFull);
        }
    }

    // ── Registry key helpers ──────────────────────────────────────────────────

    /// <summary>
    /// Opens the registry keys that list which shell extension handlers apply to
    /// <paramref name="filePath"/> and returns their native HKEY handles.
    /// Caller is responsible for closing them with RegCloseKey.
    /// </summary>
    private static List<IntPtr> OpenAssocKeys(string filePath)
    {
        var keys = new List<IntPtr>();

        TryOpenKey("*",                  keys);  // handlers for ALL file types
        TryOpenKey("AllFilesystemObjects", keys); // handlers for any fs object

        string ext = Path.GetExtension(filePath).ToLowerInvariant();
        if (string.IsNullOrEmpty(ext)) return keys;

        TryOpenKey(ext, keys);  // e.g. ".txt", ".zip"

        // Resolve the ProgID (e.g. "txtfile", "7-Zip.7z") and open that key too.
        // The ProgID is the default value of HKCR\<ext>.
        string? progId = ReadDefaultValue(ext);
        if (!string.IsNullOrEmpty(progId))
            TryOpenKey(progId, keys);

        return keys;
    }

    private static void TryOpenKey(string subKey, List<IntPtr> list)
    {
        if (RegOpenKeyEx(HKEY_CLASSES_ROOT, subKey, 0, KEY_READ, out IntPtr hk) == 0)
            list.Add(hk);
    }

    /// <summary>Reads the default (unnamed) string value of HKCR\<paramref name="subKey"/>.</summary>
    private static string? ReadDefaultValue(string subKey)
    {
        try
        {
            return Microsoft.Win32.Registry.GetValue(
                @"HKEY_CLASSES_ROOT\" + subKey, "", null) as string;
        }
        catch { return null; }
    }

    // ── Helper window ─────────────────────────────────────────────────────────

    private const string HelperClassName = "Mc_ShellMenuHost";

    /// <summary>
    /// Creates a minimal hidden WS_POPUP window owned by the calling thread,
    /// giving TrackPopupMenuEx and shell extension handlers a valid in-process HWND.
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
        RegisterClassEx(ref wc);  // Ignore failure — class may already be registered.

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
        public IntPtr lpVerb;
        public IntPtr lpParameters;
        public IntPtr lpDirectory;
        public int    nShow;
        public uint   dwHotKey;
        public IntPtr hIcon;
    }

    /// <summary>
    /// Input structure for SHCreateDefaultContextMenu.
    /// Mirrors the Windows SDK DEFCONTEXTMENU typedef exactly.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct DEFCONTEXTMENU
    {
        public IntPtr hwnd;         // owner HWND
        public IntPtr pcmcb;        // IContextMenuCB* (null = none)
        public IntPtr pidlFolder;   // full PIDL of parent folder
        public IntPtr psf;          // IShellFolder* of parent folder
        public uint   cidl;         // number of selected items
        public IntPtr apidl;        // PCUITEMID_CHILD* array (native alloc)
        public IntPtr punkAssocInfo; // IUnknown* (null = use file assoc)
        public uint   cKeys;        // number of registry keys
        public IntPtr aKeys;        // HKEY array (native alloc, null if cKeys=0)
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

    private delegate IntPtr WndProcDelegate(
        IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    // ── P/Invoke ──────────────────────────────────────────────────────────────

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHParseDisplayName(
        string pszName, IntPtr pbc,
        out IntPtr ppidl, uint sfgaoIn, out uint psfgaoOut);

    [DllImport("shell32.dll")]
    private static extern int SHBindToParent(
        IntPtr pidl, ref Guid riid,
        out IntPtr ppv, out IntPtr ppidlLast);

    [DllImport("shell32.dll")]
    private static extern int SHCreateDefaultContextMenu(
        ref DEFCONTEXTMENU pdcm, ref Guid riid, out IntPtr ppv);

    [DllImport("shell32.dll")]
    private static extern int SHGetIDListFromObject(IntPtr punk, out IntPtr ppidl);

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

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode)]
    private static extern int RegOpenKeyEx(
        IntPtr hKey, string lpSubKey, uint ulOptions,
        uint samDesired, out IntPtr phkResult);

    [DllImport("advapi32.dll")]
    private static extern int RegCloseKey(IntPtr hKey);
}
