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

    private const int SW_SHOWNORMAL = 1;

    // ── Public entry point ────────────────────────────────────────────────────

    /// <summary>
    /// Shows the Windows shell context menu for <paramref name="filePath"/> at
    /// the current mouse cursor position. Blocks until the user dismisses the menu
    /// or selects a command, then executes the selected command.
    /// </summary>
    public static void Show(string filePath)
    {
        IntPtr pidlFull  = IntPtr.Zero;
        IntPtr psfRaw    = IntPtr.Zero;
        IntPtr pCMRaw    = IntPtr.Zero;
        IntPtr hMenu     = IntPtr.Zero;

        try
        {
            // 1. Parse the absolute path into a shell PIDL.
            int hr = SHParseDisplayName(filePath, IntPtr.Zero, out pidlFull, 0, out _);
            if (hr != 0 || pidlFull == IntPtr.Zero) return;

            // 2. Bind to the parent IShellFolder; ppidlChild points WITHIN pidlFull
            //    (do not free it separately).
            var iidSF = typeof(IShellFolder).GUID;
            hr = SHBindToParent(pidlFull, ref iidSF, out psfRaw, out IntPtr pidlChild);
            if (hr != 0 || psfRaw == IntPtr.Zero) return;

            var shellFolder = (IShellFolder)Marshal.GetObjectForIUnknown(psfRaw);

            // 3. Ask the parent folder for an IContextMenu for the child item.
            IntPtr[] apidl = [pidlChild];
            var iidCM = typeof(IContextMenu).GUID;
            shellFolder.GetUIObjectOf(IntPtr.Zero, 1, apidl, ref iidCM, IntPtr.Zero, out pCMRaw);
            if (pCMRaw == IntPtr.Zero) return;

            var contextMenu = (IContextMenu)Marshal.GetObjectForIUnknown(pCMRaw);

            // 4. Populate a Win32 popup menu. idCmdFirst = 1.
            hMenu = CreatePopupMenu();
            contextMenu.QueryContextMenu(hMenu, 0, 1, 0x7FFF, CMF_NORMAL | CMF_EXPLORE);

            // 5. Show the menu at the current mouse position.
            //    TrackPopupMenuEx runs its own modal message loop — safe in console apps.
            GetCursorPos(out POINT pt);
            IntPtr hwnd = GetConsoleWindow();

            uint cmd = TrackPopupMenuEx(
                hMenu,
                TPM_LEFTALIGN | TPM_RIGHTBUTTON | TPM_RETURNCMD,
                pt.X, pt.Y,
                hwnd,
                IntPtr.Zero);

            // 6. Invoke the selected command (cmd - 1 = offset from idCmdFirst).
            if (cmd > 0)
            {
                var invoke = new CMINVOKECOMMANDINFO
                {
                    cbSize = Marshal.SizeOf<CMINVOKECOMMANDINFO>(),
                    fMask  = 0,
                    hwnd   = hwnd,
                    lpVerb = (IntPtr)(int)(cmd - 1),  // MAKEINTRESOURCEA(id)
                    nShow  = SW_SHOWNORMAL,
                };
                contextMenu.InvokeCommand(ref invoke);
            }
        }
        finally
        {
            if (hMenu   != IntPtr.Zero) DestroyMenu(hMenu);
            if (pCMRaw  != IntPtr.Zero) Marshal.Release(pCMRaw);
            if (psfRaw  != IntPtr.Zero) Marshal.Release(psfRaw);
            if (pidlFull != IntPtr.Zero) CoTaskMemFree(pidlFull);
            // pidlChild is a pointer into pidlFull — NOT freed separately.
        }
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
        void GetAttributesOf(uint cidl, [In] IntPtr[] apidl, ref uint rgfInOut);
        void GetUIObjectOf(IntPtr hwndOwner, uint cidl, [In] IntPtr[] apidl,
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
        public IntPtr lpVerb;       // MAKEINTRESOURCEA(id) for numeric, or string ptr
        public IntPtr lpParameters; // null
        public IntPtr lpDirectory;  // null
        public int    nShow;
        public uint   dwHotKey;
        public IntPtr hIcon;
    }

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

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetConsoleWindow();

    [DllImport("ole32.dll")]
    private static extern void CoTaskMemFree(IntPtr pv);
}
