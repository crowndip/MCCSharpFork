using System.Collections.Generic;
using Terminal.Gui;

namespace Mc.Ui.Dialogs;

/// <summary>
/// Section-based help viewer.
/// Equivalent to src/help.c in the original MC — shows a full-screen hypertext help
/// window with Contents / Back navigation and a button bar.
/// The original MC uses F1=Help, F2=Index, F3=Back, F10=Quit inside the viewer;
/// we map these to buttons and keyboard shortcuts on the dialog.
/// </summary>
public static class HelpDialog
{
    // -----------------------------------------------------------------------
    // Topic database — each key is a section id, value is the display text.
    // The FIRST topic in the list is the index / table of contents.
    // -----------------------------------------------------------------------
    private static readonly (string Id, string Title, string Body)[] Topics =
    [
        ("index", "Contents",
"""
  Midnight Commander for .NET — Help Index
  =========================================

  Press Tab or click a topic, then Enter to open it.
  Press F3 or Backspace to return here.

  TOPICS
  ------
   1. Keys             Keyboard reference
   2. Panels           Panel operations and navigation
   3. Files            File operations (copy, move, delete …)
   4. Select           Marking / selecting files
   5. CmdLine          Command line usage
   6. Viewer           Internal file viewer
   7. Editor           Internal file editor
   8. Menus            Menu overview
   9. Config           Configuration and settings

  QUICK START
  -----------
  Use arrow keys to move the cursor between files.
  Press Enter to open a file or enter a directory.
  Press Tab to switch between the left and right panels.
  Press F10 or Ctrl+C to quit.
"""),

        ("keys", "Keys — Keyboard Reference",
"""
  FUNCTION KEYS
  -------------
  F1          This help
  F2          User menu (mc.menu)
  F3          View selected file  /  enter directory
  F4          Edit selected file
  F5          Copy marked files to the other panel
  F6          Rename / move marked files
  F7          Create a new directory
  F8          Delete marked files
  F9          Pull down the top menu bar
  F10         Quit Midnight Commander

  PANEL NAVIGATION
  ----------------
  Up / Down         Move cursor
  PgUp / PgDn       Move by one page
  Home / End        Jump to first / last entry
  Enter             Enter directory, or view file
  Backspace         Go to parent directory
  Tab / Shift+Tab   Switch active panel
  Ctrl+R            Refresh both panels
  Ctrl+U            Swap panel directories

  DIRECTORY NAVIGATION
  --------------------
  Alt+Y             Previous directory in history
  Alt+U             Next directory in history
  Ctrl+\            Jump to home directory
  Alt+I             Synchronise panels (go to same dir)
  Ctrl+F            Directory hotlist / bookmarks
  Alt+H             Directory history

  CTRL+X PREFIX COMMANDS
  ----------------------
  Ctrl+X  C         Chmod (change permissions)
  Ctrl+X  O         Chown (change owner / group)
  Ctrl+X  S         Create symbolic link
  Ctrl+X  L         Create hard link
  Ctrl+X  P         Copy current path to command line

  COMMAND LINE
  ------------
  Up / Down         Scroll command history
  Ctrl+Enter        Copy file name to command line
  Ctrl+O            Switch to shell (suspend mc)

  MISCELLANEOUS
  -------------
  Alt+. / Ctrl+H    Toggle hidden (dot) files
  Ctrl+S            Sort order dialog
  Ctrl+L            Refresh screen
  Ins / Space       Mark / unmark file
  +                 Mark by pattern
  -                 Unmark by pattern
  *                 Invert marking
"""),

        ("panels", "Panels — Panel Operations",
"""
  PANEL TYPES
  -----------
  Normal listing    The standard directory listing (default).
  Brief listing     Two-column listing showing more files.
  Long listing      Shows full details like 'ls -l'.
  Info panel        Disk and file information for the selection.
  Tree panel        Directory tree for navigation.
  Quick view        Preview of the selected file's content.

  PANEL NAVIGATION
  ----------------
  Arrow keys        Move the cursor.
  PgUp / PgDn       Scroll the listing.
  Home / End        First / last file.
  Tab               Switch to the other panel.
  Backspace         Go to parent directory (..).
  Enter             Enter a directory, or view a file.
  Alt+Y / Alt+U     Navigate directory history (back/forward).

  SORTING
  -------
  Ctrl+S  or use Left/Right → Sort Order menu.
  Sort fields: Name, Extension, Size, Modification time,
               Access time, Change time, Owner, Group, Inode.
  Checkboxes: Reverse, Directories first, Case sensitive.

  PANEL MENU (F9 → Left / Right)
  --------------------------------
  Listing mode      Choose which fields are displayed.
  Sort Order        Choose how files are sorted.
  Filter            Apply a filename filter to the listing.
  Encoding          Select character encoding for filenames.
"""),

        ("files", "Files — File Operations",
"""
  F5  COPY
  --------
  Copies marked files (or the file under the cursor) to the
  destination path shown in the dialog.  The destination
  defaults to the other panel's current directory.

  Options:
    Preserve attributes   Keep timestamps, permissions, owner.
    Follow symlinks       Dereference links while copying.

  F6  RENAME / MOVE
  -----------------
  When a single file is selected (no marks): the destination
  field is pre-filled with the filename — edit it to rename,
  or provide a full path to move.
  When files are marked: moves them all to the destination.

  F7  MAKE DIRECTORY
  ------------------
  Prompts for a new directory name.  You may use a path
  containing multiple components; intermediate directories
  are created automatically.

  F8  DELETE
  ----------
  Asks for confirmation, then deletes all marked files (or
  the file under the cursor).  Directories are deleted
  recursively if confirmed.

  CTRL+X C  CHMOD
  ---------------
  Opens a permissions dialog for the selected file.
  Checkboxes show owner / group / other r/w/x bits and
  the special bits (setuid, setgid, sticky).

  CTRL+X O  CHOWN
  ---------------
  Opens an owner-and-group dialog.  Enter the user name
  and group name; the 'chown' system command applies the
  change.

  LINK COMMANDS (Ctrl+X prefix)
  ------------------------------
  Ctrl+X S   Create a symbolic link.
  Ctrl+X L   Create a hard link.
"""),

        ("select", "Select — Marking Files",
"""
  HOW MARKS WORK
  --------------
  Most file operations (Copy, Move, Delete) work on ALL
  marked files.  If no files are marked the operation
  applies to the file under the cursor.

  MARKING KEYS
  ------------
  Insert / Space    Toggle mark on current file and move down.
  +                 Mark files matching a shell pattern
                    (e.g. *.txt marks all .txt files).
  -                 Unmark files matching a shell pattern.
  *                 Invert the current selection.

  VISUAL CUE
  ----------
  Marked files are shown highlighted (yellow on black by
  default in the MC color scheme).
"""),

        ("cmdline", "CmdLine — Command Line",
"""
  THE COMMAND LINE
  ----------------
  The command line at the bottom of the screen lets you
  run shell commands without leaving Midnight Commander.

  After you press Enter, the output is shown in the
  terminal area.  The panels are refreshed afterwards.

  HISTORY
  -------
  Up / Down arrow   Scroll through previous commands.
  Ctrl+Enter        Paste the selected filename into the
                    command line.

  SUSPENSION
  ----------
  Ctrl+O            Suspend mc and go to the shell.
                    Type 'exit' (or Ctrl+D) to return.

  USER MENU (F2)
  --------------
  The user menu executes predefined shell commands.
  Commands in the menu run with the TUI suspended so
  interactive programs (pagers, editors) work correctly.
  Edit ~/.config/mc/menu to customise.
"""),

        ("viewer", "Viewer — Internal File Viewer",
"""
  OPENING THE VIEWER
  ------------------
  F3        View the selected file (full screen).
  Enter     View a file (if the internal viewer is enabled
            in Options → Configuration).

  NAVIGATION INSIDE THE VIEWER
  ----------------------------
  Up / Down         Scroll one line.
  PgUp / PgDn       Scroll one screen.
  Home / End        Jump to beginning / end of file.
  F3 or q           Quit the viewer.
  Ctrl+F or /       Search forward.
  Ctrl+B or ?       Search backward.
  n                 Find next occurrence.
  N                 Find previous occurrence.

  MODES
  -----
  F4        Switch between ASCII text and hex modes.
  F5        Go to a specific byte offset (hex mode).
  F7        Find (search dialog).
  F8        Toggle raw / formatted display.
"""),

        ("editor", "Editor — Internal File Editor",
"""
  OPENING THE EDITOR
  ------------------
  F4        Edit the selected file.
  F4        With no file selected: opens the editor for
            a new (unnamed) file.

  EDITOR KEY BINDINGS
  -------------------
  Arrow keys        Move cursor.
  PgUp / PgDn       Scroll by one page.
  Home / End        Start / end of line.
  Ctrl+Home/End     Start / end of file.
  Ctrl+X            Mark beginning of block (selection).
  Ctrl+C            Copy selected block to clipboard.
  Ctrl+V            Paste clipboard.
  Ctrl+K            Cut from cursor to end of line.
  Ctrl+Y            Delete current line.
  Ctrl+U            Undo last change.
  Ctrl+F            Find / search.
  Ctrl+H            Replace.
  F2                Save file.
  F10 or Esc        Close editor (asks to save if modified).
"""),

        ("menus", "Menus — Menu Overview",
"""
  TOP MENU BAR  (F9 or click)
  ---------------------------
  Left / Right  Panel configuration (listing, sort, filter …)
  File          File operations and attributes
  Command       Directory commands, history, user menu
  Options       Application configuration, skins, layout
  Help          Open this help

  FILE MENU
  ---------
  View (F3)         Open in internal viewer.
  Edit (F4)         Open in internal editor.
  Copy (F5)         Copy to other panel.
  Rename/Move (F6)  Rename or move.
  Mkdir (F7)        Create directory.
  Delete (F8)       Delete.
  Chmod (Ctrl+X C)  Change permissions.
  Chown (Ctrl+X O)  Change owner / group.
  Link              Create hard link.
  Symlink           Create symbolic link.
  Properties        Show file information.
  Quit (F10)        Exit Midnight Commander.

  COMMAND MENU
  ------------
  User menu (F2)          Run a command from mc.menu.
  Directory tree          Browse the directory tree.
  Find file               Search for files by name or content.
  Command history         Show shell / session command history.
  Viewed/edited history   Recently viewed or edited files.
  Directory hotlist       Bookmark manager.
  Active VFS list         Show currently mounted VFS paths.
"""),

        ("config", "Config — Configuration",
"""
  OPTIONS MENU
  ------------
  Configuration         General behaviour (viewer, editor …).
  Panel options         How panels display and behave.
  Confirmation          Toggle delete / overwrite confirmations.
  Layout                Horizontal/vertical split, sizes.
  Skins                 Choose a colour scheme.
  Save setup            Persist current settings.

  CONFIGURATION FILES
  -------------------
  ~/.config/mc/ini      Main settings (INI format).
  ~/.config/mc/menu     User menu entries (mc.menu format).
  ~/.config/mc/hotlist  Directory bookmarks.

  HIDDEN FILES
  ------------
  Ctrl+H or Alt+.       Toggle display of dotfiles.
  The setting is saved across sessions.

  SKINS
  -----
  MC supports colour skin files stored in
  ~/.local/share/mc/skins/ or /usr/share/mc/skins/.
  Choose via Options → Skins.
"""),
    ];

    // -----------------------------------------------------------------------
    // Public entry point
    // -----------------------------------------------------------------------
    public static void Show() => RunViewer("index");

    // -----------------------------------------------------------------------
    // Viewer implementation
    // -----------------------------------------------------------------------
    private static void RunViewer(string startTopicId)
    {
        var history = new Stack<string>();
        string currentId = startTopicId;

        while (true)
        {
            var topic = FindTopic(currentId) ?? Topics[0];

            // Dialog dimensions — match original MC: full width-4, 2/3 or 18 rows min
            int dialogW = Math.Max(76, Application.Driver.Cols - 4);
            int dialogH = Math.Clamp(Application.Driver.Rows * 2 / 3, 18, Application.Driver.Rows - 4);

            string? nextId = null;  // set to navigate away

            var d = new Dialog
            {
                Title = topic.Title,
                Width = dialogW,
                Height = dialogH,
                ColorScheme = McTheme.Dialog,
            };

            // Scrollable body
            var tv = new TextView
            {
                X = 1, Y = 1,
                Width = Dim.Fill(1),
                Height = Dim.Fill(4),
                Text = topic.Body,
                ReadOnly = true,
                ColorScheme = McTheme.Dialog,
            };
            d.Add(tv);

            // Navigation buttons matching original MC help button bar:
            // F3=Back, F2=Index, F10/OK=Close
            bool closed = false;

            var btnBack = new Button
            {
                X = 2,
                Y = Pos.Bottom(tv),
                Text = "[ Back ]",
                Enabled = history.Count > 0,
                ColorScheme = McTheme.Dialog,
            };
            btnBack.Accepting += (_, _) =>
            {
                if (history.TryPop(out var prev)) nextId = prev;
                Application.RequestStop(d);
            };
            d.Add(btnBack);

            var btnContents = new Button
            {
                X = Pos.Center() - 5,
                Y = Pos.Bottom(tv),
                Text = "[ Contents ]",
                ColorScheme = McTheme.Dialog,
            };
            btnContents.Accepting += (_, _) =>
            {
                if (currentId != "index")
                {
                    history.Push(currentId);
                    nextId = "index";
                }
                Application.RequestStop(d);
            };
            d.Add(btnContents);

            var btnOk = new Button
            {
                X = Pos.AnchorEnd(9),
                Y = Pos.Bottom(tv),
                Text = "[ Close ]",
                IsDefault = true,
                ColorScheme = McTheme.Dialog,
            };
            btnOk.Accepting += (_, _) =>
            {
                closed = true;
                Application.RequestStop(d);
            };
            d.Add(btnOk);

            // Keyboard shortcuts inside the help viewer
            d.KeyDown += (_, key) =>
            {
                if (key == Key.F3 || key == Key.Backspace)
                {
                    if (history.TryPop(out var prev)) nextId = prev;
                    Application.RequestStop(d);
                    key.Handled = true;
                }
                else if (key == Key.F2)
                {
                    if (currentId != "index") { history.Push(currentId); nextId = "index"; }
                    Application.RequestStop(d);
                    key.Handled = true;
                }
                else if (key == Key.F10 || key == Key.Esc)
                {
                    closed = true;
                    Application.RequestStop(d);
                    key.Handled = true;
                }
                // Number keys 1-9 jump to the corresponding topic
                else if (key.KeyCode >= KeyCode.D1 && key.KeyCode <= KeyCode.D9)
                {
                    int idx = (int)(key.KeyCode - KeyCode.D1) + 1; // 1-based, topics[0]=index
                    if (idx < Topics.Length)
                    {
                        history.Push(currentId);
                        nextId = Topics[idx].Id;
                        Application.RequestStop(d);
                        key.Handled = true;
                    }
                }
            };

            tv.SetFocus();
            Application.Run(d);
            d.Dispose();

            if (closed || nextId == null) break;
            currentId = nextId;
        }
    }

    private static (string Id, string Title, string Body)? FindTopic(string id)
    {
        foreach (var t in Topics)
            if (t.Id == id) return t;
        return null;
    }
}
