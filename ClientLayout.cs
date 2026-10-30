// Build from this folder:
// %WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe /nologo /target:winexe /win32manifest:app.manifest /out:ClientLayout.exe /reference:System.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll /reference:System.Runtime.Serialization.dll ClientLayout.cs

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading;
using System.Windows.Forms;

static class NativeMethods
{
    public const int GWL_STYLE = -16;
    public const int GWL_EXSTYLE = -20;

    public const int WS_CAPTION = 0x00C00000;
    public const int WS_THICKFRAME = 0x00040000;
    public const int WS_MINIMIZEBOX = 0x00020000;
    public const int WS_MAXIMIZEBOX = 0x00010000;
    public const int WS_OVERLAPPEDWINDOW = 0x00CF0000;

    public const int WS_EX_DLGMODALFRAME = 0x00000001;
    public const int WS_EX_WINDOWEDGE = 0x00000100;
    public const int WS_EX_CLIENTEDGE = 0x00000200;
    public const int WS_EX_STATICEDGE = 0x00020000;
    public const int WS_EX_TOPMOST = 0x00000008;

    public const int SW_RESTORE = 9;

    public const int SWP_NOSIZE = 0x0001;
    public const int SWP_NOMOVE = 0x0002;
    public const int SWP_NOZORDER = 0x0004;
    public const int SWP_NOACTIVATE = 0x0010;
    public const int SWP_FRAMECHANGED = 0x0020;
    public const int SWP_SHOWWINDOW = 0x0040;
    public const int SWP_NOOWNERZORDER = 0x0200;

    public static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
    public static readonly IntPtr HWND_NOTOPMOST = new IntPtr(-2);

    public const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
    public const int DWMWA_BORDER_COLOR = 34;
    public const int DWMWCP_DEFAULT = 0;
    public const int DWMWCP_DONOTROUND = 1;
    public const int DWMWA_COLOR_DEFAULT = unchecked((int)0xFFFFFFFF);
    public const int DWMWA_COLOR_NONE = unchecked((int)0xFFFFFFFE);

    public const uint WM_CLOSE = 0x0010;
    public const int PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;

    public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll")]
    public static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern int GetWindowTextLength(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern bool IsWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern bool IsIconic(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out int processId);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool GetWindowRect(IntPtr hWnd, out Rect rect);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
    public static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    public static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool PostMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("dwmapi.dll")]
    public static extern int DwmSetWindowAttribute(IntPtr hwnd, int dwAttribute, ref int pvAttribute, int cbAttribute);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern IntPtr OpenProcess(int dwDesiredAccess, bool bInheritHandle, int dwProcessId);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern bool QueryFullProcessImageName(IntPtr hProcess, int dwFlags, StringBuilder lpExeName, ref int lpdwSize);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool CloseHandle(IntPtr hObject);

    [StructLayout(LayoutKind.Sequential)]
    public struct Rect
    {
        public int Left, Top, Right, Bottom;
    }
}

[DataContract]
sealed class Slot
{
    [DataMember] public int X { get; set; }
    [DataMember] public int Y { get; set; }
    [DataMember] public int Width { get; set; }
    [DataMember] public int Height { get; set; }
    [DataMember] public bool Borderless { get; set; }
    [DataMember] public bool TopMost { get; set; }
}

[DataContract]
sealed class GameProfile
{
    [DataMember] public string GameExe { get; set; }
    [DataMember] public int ClientCount { get; set; }
    [DataMember] public List<Slot> Slots { get; set; }
}

[DataContract]
sealed class StoreFile
{
    [DataMember] public string LastExe { get; set; }
    [DataMember] public List<GameProfile> Games { get; set; }
}

sealed class WindowInfo
{
    public IntPtr Hwnd;
    public int X, Y, Width, Height;
    public bool Borderless;
    public bool TopMost;
    public string ExePath = "";
    public string Folder = "";

    public Slot ToSlot()
    {
        return new Slot
        {
            X = X,
            Y = Y,
            Width = Width,
            Height = Height,
            Borderless = Borderless,
            TopMost = TopMost
        };
    }
}

static class LayoutIo
{
    public static string StorePath()
    {
        return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "layout.json");
    }

    public static StoreFile Load(string path)
    {
        if (!File.Exists(path)) return Empty();
        using (var stream = File.OpenRead(path))
        {
            var serial = new DataContractJsonSerializer(typeof(StoreFile));
            var store = (StoreFile)serial.ReadObject(stream);
            return Normalize(store);
        }
    }

    public static void Save(string path, StoreFile store)
    {
        store = Normalize(store);
        using (var stream = File.Create(path))
        {
            var serial = new DataContractJsonSerializer(typeof(StoreFile));
            serial.WriteObject(stream, store);
        }
    }

    public static StoreFile Empty()
    {
        return new StoreFile { LastExe = "", Games = new List<GameProfile>() };
    }

    public static StoreFile Normalize(StoreFile store)
    {
        if (store == null) store = Empty();
        if (store.Games == null) store.Games = new List<GameProfile>();
        if (store.LastExe == null) store.LastExe = "";
        foreach (var game in store.Games)
        {
            if (game.Slots == null) game.Slots = new List<Slot>();
            game.Slots = game.Slots.Where(s => s != null && s.Width >= 50 && s.Height >= 50).ToList();
            if (game.GameExe == null) game.GameExe = "";
            if (game.ClientCount < 1) game.ClientCount = Math.Max(1, game.Slots.Count);
        }
        return store;
    }
}

static class WowWindows
{
    public static List<WindowInfo> List()
    {
        var found = new List<WindowInfo>();
        NativeMethods.EnumWindows((hwnd, l) =>
        {
            try
            {
                WindowInfo info;
                if (TryRead(hwnd, out info)) found.Add(info);
            }
            catch (Exception)
            {
            }
            return true;
        }, IntPtr.Zero);
        return found;
    }

    public static bool TryRead(IntPtr hwnd, out WindowInfo info)
    {
        info = null;
        if (!NativeMethods.IsWindow(hwnd) || !NativeMethods.IsWindowVisible(hwnd)) return false;

        int length = NativeMethods.GetWindowTextLength(hwnd);
        if (length <= 0) return false;
        var title = new StringBuilder(length + 1);
        NativeMethods.GetWindowText(hwnd, title, title.Capacity);
        if (title.ToString().IndexOf("Warcraft", StringComparison.OrdinalIgnoreCase) < 0) return false;

        int pid;
        NativeMethods.GetWindowThreadProcessId(hwnd, out pid);
        string path;
        if (!IsWowProcess(pid, out path)) return false;

        NativeMethods.Rect rect;
        if (!NativeMethods.GetWindowRect(hwnd, out rect)) return false;
        int width = rect.Right - rect.Left;
        int height = rect.Bottom - rect.Top;
        if (width < 200 || height < 200) return false;

        int style = WindowOps.GetStyle(hwnd);
        int ex = WindowOps.GetExStyle(hwnd);
        info = new WindowInfo
        {
            Hwnd = hwnd,
            X = rect.Left,
            Y = rect.Top,
            Width = width,
            Height = height,
            Borderless = (style & NativeMethods.WS_CAPTION) == 0,
            TopMost = (ex & NativeMethods.WS_EX_TOPMOST) != 0,
            ExePath = path,
            Folder = FolderOf(path)
        };
        return true;
    }

    static bool IsWowProcess(int pid, out string path)
    {
        path = ExePath(pid);
        if (path.Length > 0)
            return string.Equals(Path.GetFileName(path), "Wow.exe", StringComparison.OrdinalIgnoreCase);
        try
        {
            using (var process = Process.GetProcessById(pid))
                return string.Equals(process.ProcessName, "Wow", StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception)
        {
            return false;
        }
    }

    public static string ExePath(int pid)
    {
        IntPtr handle = NativeMethods.OpenProcess(NativeMethods.PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
        if (handle == IntPtr.Zero) return "";
        try
        {
            var buffer = new StringBuilder(1024);
            int size = buffer.Capacity;
            if (!NativeMethods.QueryFullProcessImageName(handle, 0, buffer, ref size)) return "";
            return buffer.ToString();
        }
        finally
        {
            NativeMethods.CloseHandle(handle);
        }
    }

    public static string FolderOf(string path)
    {
        if (string.IsNullOrEmpty(path)) return "?";
        string dir = Path.GetDirectoryName(path);
        if (string.IsNullOrEmpty(dir)) return "?";
        return new DirectoryInfo(dir).Name;
    }

    public static string DiscoverExe()
    {
        string dir = AppDomain.CurrentDomain.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        string beside = Path.Combine(dir, "Wow.exe");
        if (File.Exists(beside)) return Path.GetFullPath(beside);
        string parent = Path.GetFullPath(Path.Combine(dir, "..", "Wow.exe"));
        if (File.Exists(parent)) return parent;
        return "";
    }
}

static class WindowOps
{
    public static int GetStyle(IntPtr hwnd)
    {
        return unchecked((int)NativeMethods.GetWindowLongPtr(hwnd, NativeMethods.GWL_STYLE).ToInt64());
    }

    public static int GetExStyle(IntPtr hwnd)
    {
        return unchecked((int)NativeMethods.GetWindowLongPtr(hwnd, NativeMethods.GWL_EXSTYLE).ToInt64());
    }

    public static void SetBorderless(IntPtr hwnd, bool borderless)
    {
        int style = GetStyle(hwnd);
        int ex = GetExStyle(hwnd);
        if (borderless)
        {
            style &= ~(NativeMethods.WS_CAPTION | NativeMethods.WS_THICKFRAME | NativeMethods.WS_MINIMIZEBOX | NativeMethods.WS_MAXIMIZEBOX);
            ex &= ~(NativeMethods.WS_EX_DLGMODALFRAME | NativeMethods.WS_EX_CLIENTEDGE | NativeMethods.WS_EX_STATICEDGE | NativeMethods.WS_EX_WINDOWEDGE);
        }
        else
        {
            style |= NativeMethods.WS_OVERLAPPEDWINDOW;
        }

        NativeMethods.SetWindowLongPtr(hwnd, NativeMethods.GWL_STYLE, new IntPtr(style));
        NativeMethods.SetWindowLongPtr(hwnd, NativeMethods.GWL_EXSTYLE, new IntPtr(ex));
        ApplyCorner(hwnd, borderless);
    }

    public static void SetTopMost(IntPtr hwnd, bool topMost)
    {
        bool ok = NativeMethods.SetWindowPos(
            hwnd,
            topMost ? NativeMethods.HWND_TOPMOST : NativeMethods.HWND_NOTOPMOST,
            0, 0, 0, 0,
            NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE);
        if (!ok) throw new Win32Exception();
    }

    public static void Place(IntPtr hwnd, Slot slot)
    {
        if (!NativeMethods.IsWindow(hwnd)) return;
        if (NativeMethods.IsIconic(hwnd)) NativeMethods.ShowWindow(hwnd, NativeMethods.SW_RESTORE);
        SetBorderless(hwnd, slot.Borderless);
        bool ok = NativeMethods.SetWindowPos(
            hwnd,
            slot.TopMost ? NativeMethods.HWND_TOPMOST : NativeMethods.HWND_NOTOPMOST,
            slot.X, slot.Y, slot.Width, slot.Height,
            NativeMethods.SWP_FRAMECHANGED | NativeMethods.SWP_NOACTIVATE | NativeMethods.SWP_SHOWWINDOW);
        if (!ok) throw new Win32Exception();
    }

    public static void RefreshFrame(IntPtr hwnd)
    {
        NativeMethods.Rect rect;
        if (!NativeMethods.GetWindowRect(hwnd, out rect)) return;
        NativeMethods.SetWindowPos(
            hwnd, IntPtr.Zero,
            rect.Left, rect.Top, rect.Right - rect.Left, rect.Bottom - rect.Top,
            NativeMethods.SWP_FRAMECHANGED | NativeMethods.SWP_NOZORDER | NativeMethods.SWP_NOACTIVATE | NativeMethods.SWP_NOOWNERZORDER);
    }

    public static int NearestSlot(WindowInfo win, IList<Slot> slots, HashSet<int> claimed)
    {
        int best = -1;
        long bestCost = long.MaxValue;
        long cx = win.X + win.Width / 2;
        long cy = win.Y + win.Height / 2;
        for (int i = 0; i < slots.Count; i++)
        {
            if (claimed.Contains(i)) continue;
            Slot slot = slots[i];
            long dx = cx - (slot.X + slot.Width / 2);
            long dy = cy - (slot.Y + slot.Height / 2);
            long dw = win.Width - slot.Width;
            long dh = win.Height - slot.Height;
            long cost = dx * dx + dy * dy + dw * dw + dh * dh;
            if (cost < bestCost)
            {
                bestCost = cost;
                best = i;
            }
        }
        return best;
    }

    static void ApplyCorner(IntPtr hwnd, bool borderless)
    {
        int corner = borderless ? NativeMethods.DWMWCP_DONOTROUND : NativeMethods.DWMWCP_DEFAULT;
        int color = borderless ? NativeMethods.DWMWA_COLOR_NONE : NativeMethods.DWMWA_COLOR_DEFAULT;
        NativeMethods.DwmSetWindowAttribute(hwnd, NativeMethods.DWMWA_WINDOW_CORNER_PREFERENCE, ref corner, 4);
        NativeMethods.DwmSetWindowAttribute(hwnd, NativeMethods.DWMWA_BORDER_COLOR, ref color, 4);
    }
}

sealed class MainForm : Form
{
    readonly string storePath;
    StoreFile store;
    TextBox exeBox;
    NumericUpDown countBox;
    ListView list;
    Label savedLabel;
    Label statusLabel;
    Button launchButton;
    Button applyButton;
    Button saveButton;
    Button refreshButton;
    Button removeButton;
    Button restoreButton;
    Button topButton;
    Button notTopButton;
    Button upButton;
    Button downButton;
    System.Windows.Forms.Timer timer;
    volatile bool closing;
    bool busy;

    public MainForm()
    {
        storePath = LayoutIo.StorePath();
        Text = "Client layout";
        Font = new Font("Segoe UI", 9f);
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(760, 520);
        MinimumSize = new Size(720, 460);
        Padding = new Padding(10);

        exeBox = new TextBox();
        countBox = new NumericUpDown { Minimum = 1, Maximum = 20, Value = 2, Width = 48 };
        list = new ListView
        {
            View = View.Details,
            FullRowSelect = true,
            GridLines = true,
            HideSelection = false,
            MultiSelect = false,
            HeaderStyle = ColumnHeaderStyle.Nonclickable,
            Dock = DockStyle.Fill
        };
        list.Columns.Add("#", 36);
        list.Columns.Add("Folder", 180);
        list.Columns.Add("Position", 120);
        list.Columns.Add("Size", 110);
        list.Columns.Add("Border", 90);
        list.Columns.Add("On top", 60);

        savedLabel = new Label { Dock = DockStyle.Top, Height = 22, TextAlign = ContentAlignment.MiddleLeft };
        statusLabel = new Label { Dock = DockStyle.Bottom, Height = 22, TextAlign = ContentAlignment.MiddleLeft, AutoEllipsis = true };
        var hint = new Label
        {
            Dock = DockStyle.Bottom,
            Height = 78,
            ForeColor = SystemColors.GrayText,
            Text = "Top row is the first client. A new window is added under the bigger ones. Take the border off the small one and turn on Keep on top, then Save layout. Launch starts the exe above until that many clients are open. For Battle.net, press Play on each account, then Apply layout. A window with no border moves with Alt+Space, then M."
        };

        var exeRow = new Panel { Dock = DockStyle.Top, Height = 32 };
        var gameLabel = new Label { Text = "Game", AutoSize = false, TextAlign = ContentAlignment.MiddleLeft };
        var browseButton = new Button { Text = "Browse..." };
        browseButton.Click += BrowseClick;
        exeRow.Controls.Add(gameLabel);
        exeRow.Controls.Add(exeBox);
        exeRow.Controls.Add(browseButton);
        exeRow.Resize += (s, e) =>
        {
            browseButton.SetBounds(exeRow.ClientSize.Width - 84, 2, 80, 26);
            gameLabel.SetBounds(0, 4, 40, 22);
            exeBox.SetBounds(44, 4, Math.Max(40, exeRow.ClientSize.Width - 44 - 90), 23);
        };

        launchButton = new Button { Text = "Launch", AutoSize = true };
        applyButton = new Button { Text = "Apply layout", AutoSize = true };
        saveButton = new Button { Text = "Save layout", AutoSize = true };
        refreshButton = new Button { Text = "Refresh", AutoSize = true };
        launchButton.Click += (s, e) => LaunchClick();
        applyButton.Click += (s, e) => ApplyClick();
        saveButton.Click += (s, e) => SaveClick();
        refreshButton.Click += (s, e) => RefreshList();

        var launchRow = Row(
            new Label { Text = "Clients", AutoSize = true, Padding = new Padding(0, 6, 0, 0) },
            countBox,
            launchButton, applyButton, saveButton, refreshButton);

        removeButton = new Button { Text = "Remove border", AutoSize = true };
        restoreButton = new Button { Text = "Restore border", AutoSize = true };
        topButton = new Button { Text = "Keep on top", AutoSize = true };
        notTopButton = new Button { Text = "Not on top", AutoSize = true };
        upButton = new Button { Text = "Move up", AutoSize = true };
        downButton = new Button { Text = "Move down", AutoSize = true };
        removeButton.Click += (s, e) => BorderClick(true);
        restoreButton.Click += (s, e) => BorderClick(false);
        topButton.Click += (s, e) => TopClick(true);
        notTopButton.Click += (s, e) => TopClick(false);
        upButton.Click += (s, e) => MoveSelected(-1);
        downButton.Click += (s, e) => MoveSelected(1);
        var actionRow = Row(removeButton, restoreButton, topButton, notTopButton, upButton, downButton);
        actionRow.Dock = DockStyle.Bottom;

        Controls.Add(list);
        Controls.Add(actionRow);
        Controls.Add(statusLabel);
        Controls.Add(hint);
        Controls.Add(savedLabel);
        Controls.Add(launchRow);
        Controls.Add(exeRow);

        try
        {
            store = LayoutIo.Load(storePath);
        }
        catch (Exception)
        {
            store = LayoutIo.Empty();
            Status("Could not read the saved layout. Starting fresh.");
        }

        string exe = FirstExisting(store.LastExe);
        if (exe.Length == 0) exe = WowWindows.DiscoverExe();
        exeBox.Text = exe;
        LoadProfileIntoForm();
        if (statusLabel.Text.Length == 0)
            Status(exe.Length == 0 ? "Browse to Wow.exe." : "Arrange the open clients, then Save layout.");

        timer = new System.Windows.Forms.Timer { Interval = 1000 };
        timer.Tick += (s, e) => { if (!busy) RefreshList(); };
        timer.Start();
        Shown += (s, e) => RefreshList();
    }

    static string FirstExisting(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return "";
        try
        {
            string full = Path.GetFullPath(path);
            return File.Exists(full) ? full : "";
        }
        catch (Exception)
        {
            return "";
        }
    }

    static FlowLayoutPanel Row(params Control[] controls)
    {
        var row = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            WrapContents = true,
            FlowDirection = FlowDirection.LeftToRight,
            Padding = new Padding(0, 4, 0, 4)
        };
        foreach (var control in controls)
        {
            control.Margin = new Padding(0, 0, 8, 4);
            row.Controls.Add(control);
        }
        return row;
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        closing = true;
        if (timer != null) timer.Stop();
        base.OnFormClosing(e);
    }

    void Status(string text)
    {
        statusLabel.Text = text;
    }

    void SetBusy(bool value)
    {
        busy = value;
        UseWaitCursor = value;
        launchButton.Enabled = !value;
        applyButton.Enabled = !value;
        saveButton.Enabled = !value;
        refreshButton.Enabled = !value;
        removeButton.Enabled = !value;
        restoreButton.Enabled = !value;
        topButton.Enabled = !value;
        notTopButton.Enabled = !value;
        upButton.Enabled = !value;
        downButton.Enabled = !value;
        Cursor = value ? Cursors.WaitCursor : Cursors.Default;
    }

    GameProfile FindProfile(string exe)
    {
        if (store.Games == null) return null;
        foreach (var game in store.Games)
        {
            if (SamePath(game.GameExe, exe)) return game;
        }
        return null;
    }

    static bool SamePath(string a, string b)
    {
        if (string.IsNullOrWhiteSpace(a) || string.IsNullOrWhiteSpace(b)) return false;
        try
        {
            return string.Equals(Path.GetFullPath(a), Path.GetFullPath(b), StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception)
        {
            return string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
        }
    }

    void LoadProfileIntoForm()
    {
        var profile = FindProfile(exeBox.Text.Trim());
        int count = profile != null && profile.ClientCount > 0 ? profile.ClientCount : 2;
        if (profile != null && profile.Slots != null && profile.Slots.Count > count) count = profile.Slots.Count;
        countBox.Value = Math.Max(countBox.Minimum, Math.Min(countBox.Maximum, count));
        if (profile == null || profile.Slots == null || profile.Slots.Count == 0)
            savedLabel.Text = "No saved layout" + ForGame(exeBox.Text) + ".";
        else
            savedLabel.Text = "Saved layout: " + profile.Slots.Count + " client" + (profile.Slots.Count == 1 ? "" : "s") + ForGame(exeBox.Text) + ".";
    }

    static string ForGame(string exe)
    {
        exe = exe == null ? "" : exe.Trim();
        if (exe.Length == 0) return "";
        return " for " + WowWindows.FolderOf(exe);
    }

    void BrowseClick(object sender, EventArgs e)
    {
        using (var dialog = new OpenFileDialog())
        {
            dialog.Filter = "Wow.exe|Wow.exe|Programs|*.exe";
            dialog.FileName = "Wow.exe";
            dialog.Title = "Choose Wow.exe";
            string current = exeBox.Text.Trim();
            if (File.Exists(current)) dialog.InitialDirectory = Path.GetDirectoryName(current);
            if (dialog.ShowDialog(this) != DialogResult.OK) return;
            exeBox.Text = dialog.FileName;
            LoadProfileIntoForm();
            Status("Using " + dialog.FileName);
        }
    }

    void RefreshList()
    {
        if (list.IsDisposed) return;
        var found = WowWindows.List();
        var byId = new Dictionary<long, WindowInfo>();
        foreach (var win in found) byId[win.Hwnd.ToInt64()] = win;

        long selected = list.SelectedItems.Count == 0 ? 0 : (long)list.SelectedItems[0].Tag;
        for (int i = list.Items.Count - 1; i >= 0; i--)
        {
            if (!byId.ContainsKey((long)list.Items[i].Tag)) list.Items.RemoveAt(i);
        }

        var seen = new HashSet<long>();
        foreach (ListViewItem item in list.Items) seen.Add((long)item.Tag);

        foreach (ListViewItem item in list.Items) Fill(item, byId[(long)item.Tag]);

        var newcomers = found
            .Where(w => !seen.Contains(w.Hwnd.ToInt64()))
            .OrderByDescending(w => (long)w.Width * w.Height)
            .ThenBy(w => w.Y)
            .ThenBy(w => w.X);
        foreach (var win in newcomers) list.Items.Add(MakeItem(win));

        Renumber();
        SizeColumns();
        if (selected != 0)
        {
            foreach (ListViewItem item in list.Items)
            {
                if ((long)item.Tag == selected) item.Selected = true;
            }
        }
    }

    static ListViewItem MakeItem(WindowInfo win)
    {
        var item = new ListViewItem("");
        item.SubItems.Add("");
        item.SubItems.Add("");
        item.SubItems.Add("");
        item.SubItems.Add("");
        item.SubItems.Add("");
        item.Tag = win.Hwnd.ToInt64();
        Fill(item, win);
        return item;
    }

    static void Fill(ListViewItem item, WindowInfo win)
    {
        item.SubItems[1].Text = win.Folder;
        item.SubItems[2].Text = win.X + ", " + win.Y;
        item.SubItems[3].Text = win.Width + " x " + win.Height;
        item.SubItems[4].Text = win.Borderless ? "No border" : "Border";
        item.SubItems[5].Text = win.TopMost ? "Yes" : "No";
    }

    void Renumber()
    {
        for (int i = 0; i < list.Items.Count; i++) list.Items[i].Text = (i + 1).ToString();
    }

    void SizeColumns()
    {
        if (list.ClientSize.Width < 80 || list.Columns.Count < 6) return;
        int others = list.Columns[0].Width + list.Columns[2].Width + list.Columns[3].Width + list.Columns[4].Width + list.Columns[5].Width;
        int folder = list.ClientSize.Width - others - SystemInformation.VerticalScrollBarWidth - 4;
        list.Columns[1].Width = Math.Max(80, folder);
    }

    bool TrySelected(out IntPtr hwnd)
    {
        hwnd = IntPtr.Zero;
        if (list.SelectedItems.Count == 0)
        {
            Status("Select a client in the list first.");
            return false;
        }
        hwnd = new IntPtr((long)list.SelectedItems[0].Tag);
        if (!NativeMethods.IsWindow(hwnd))
        {
            Status("That client has closed.");
            RefreshList();
            return false;
        }
        return true;
    }

    void BorderClick(bool borderless)
    {
        IntPtr hwnd;
        if (!TrySelected(out hwnd)) return;
        try
        {
            WindowOps.SetBorderless(hwnd, borderless);
            WindowOps.RefreshFrame(hwnd);
            RefreshList();
            Status(borderless
                ? "Border removed. Move that window with Alt+Space, then M."
                : "Border restored.");
        }
        catch (Win32Exception ex)
        {
            Status(Blocked(ex));
        }
    }

    void TopClick(bool topMost)
    {
        IntPtr hwnd;
        if (!TrySelected(out hwnd)) return;
        try
        {
            WindowOps.SetTopMost(hwnd, topMost);
            RefreshList();
            Status(topMost ? "That client stays on top." : "That client no longer stays on top.");
        }
        catch (Win32Exception ex)
        {
            Status(Blocked(ex));
        }
    }

    static string Blocked(Win32Exception ex)
    {
        if (ex.NativeErrorCode == 5)
            return "Windows blocked the change. Start this tool the same way the game is started, without an extra Run as administrator.";
        return ex.Message;
    }

    void MoveSelected(int delta)
    {
        if (list.SelectedItems.Count == 0)
        {
            Status("Select a client in the list first.");
            return;
        }
        int index = list.SelectedItems[0].Index;
        int target = index + delta;
        if (target < 0 || target >= list.Items.Count) return;
        timer.Stop();
        var item = list.SelectedItems[0];
        list.Items.RemoveAt(index);
        list.Items.Insert(target, item);
        item.Selected = true;
        item.Focused = true;
        Renumber();
        if (!busy) timer.Start();
    }

    List<WindowInfo> ListedWindows()
    {
        var windows = new List<WindowInfo>();
        foreach (ListViewItem item in list.Items)
        {
            var hwnd = new IntPtr((long)item.Tag);
            WindowInfo info;
            if (WowWindows.TryRead(hwnd, out info)) windows.Add(info);
        }
        return windows;
    }

    void SaveClick()
    {
        RefreshList();
        var windows = ListedWindows();
        if (windows.Count == 0)
        {
            Status("No World of Warcraft windows are open, so the saved layout was left as it is.");
            return;
        }

        var paths = windows.Select(w => w.ExePath).Where(p => !string.IsNullOrEmpty(p)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (paths.Count > 1)
        {
            Status("Clients from more than one Wow.exe are open. Close one game, then save.");
            return;
        }

        string exe = paths.Count == 1 ? paths[0] : exeBox.Text.Trim();
        if (!File.Exists(exe))
        {
            Status("Browse to Wow.exe, then save.");
            return;
        }

        exe = Path.GetFullPath(exe);
        exeBox.Text = exe;
        var profile = FindProfile(exe);
        if (profile == null)
        {
            profile = new GameProfile { GameExe = exe, Slots = new List<Slot>() };
            store.Games.Add(profile);
        }
        profile.GameExe = exe;
        profile.Slots = windows.Select(w => w.ToSlot()).ToList();
        profile.ClientCount = windows.Count;
        countBox.Value = windows.Count;
        store.LastExe = exe;
        try
        {
            LayoutIo.Save(storePath, store);
        }
        catch (Exception ex)
        {
            Status("Could not save the layout. " + ex.Message);
            return;
        }
        LoadProfileIntoForm();
        Status("Saved " + windows.Count + " client" + (windows.Count == 1 ? "" : "s") + ".");
    }

    void ApplyClick()
    {
        var windows = WowWindows.List();
        if (windows.Count == 0)
        {
            Status("No World of Warcraft windows are open.");
            return;
        }
        var paths = windows.Select(w => w.ExePath).Where(p => !string.IsNullOrEmpty(p)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (paths.Count > 1)
        {
            Status("Clients from more than one Wow.exe are open. Apply moves one game at a time.");
            return;
        }

        string exe = paths.Count == 1 ? paths[0] : exeBox.Text.Trim();
        var profile = FindProfile(exe);
        if (profile == null || profile.Slots == null || profile.Slots.Count == 0)
        {
            Status("No saved layout for this game. Arrange the windows and Save layout.");
            return;
        }

        var claimed = new HashSet<int>();
        int placed = 0;
        string problem = null;
        foreach (var win in windows.OrderByDescending(w => (long)w.Width * w.Height))
        {
            int slot = WindowOps.NearestSlot(win, profile.Slots, claimed);
            if (slot < 0) break;
            claimed.Add(slot);
            try
            {
                WindowOps.Place(win.Hwnd, profile.Slots[slot]);
                placed++;
            }
            catch (Win32Exception ex)
            {
                problem = Blocked(ex);
            }
        }
        RefreshList();
        Status(problem ?? ("Applied the layout to " + placed + " client" + (placed == 1 ? "" : "s") + "."));
    }

    void LaunchClick()
    {
        if (busy) return;
        string exe = exeBox.Text.Trim();
        if (!File.Exists(exe))
        {
            Status("Browse to Wow.exe first. For the Battle.net client, press Play on each account, then Apply layout.");
            return;
        }
        exe = Path.GetFullPath(exe);
        exeBox.Text = exe;

        int target = (int)countBox.Value;
        var profile = FindProfile(exe);
        var slots = profile != null && profile.Slots != null ? profile.Slots : new List<Slot>();
        SetBusy(true);
        try
        {
            store.LastExe = exe;
            if (profile != null) profile.ClientCount = target;
            try { LayoutIo.Save(storePath, store); } catch (Exception) { }

            var mine = WowWindows.List().Where(w => w.ExePath.Length == 0 || SamePath(w.ExePath, exe)).ToList();
            var known = new HashSet<long>(WowWindows.List().Select(w => w.Hwnd.ToInt64()));
            var claimed = new HashSet<int>();
            foreach (var win in mine.OrderByDescending(w => (long)w.Width * w.Height))
            {
                if (slots.Count == 0) break;
                int slot = WindowOps.NearestSlot(win, slots, claimed);
                if (slot < 0) break;
                claimed.Add(slot);
                WindowOps.Place(win.Hwnd, slots[slot]);
                ReapplyLater(win.Hwnd, slots[slot]);
            }

            int need = target - mine.Count;
            if (need <= 0)
            {
                Status("Already have " + mine.Count + " client" + (mine.Count == 1 ? "" : "s") + " open." + (slots.Count > 0 ? " Applied the saved layout." : ""));
                return;
            }

            string dir = Path.GetDirectoryName(exe);
            int started = 0;
            for (int n = 0; n < need; n++)
            {
                if (closing) return;
                Status("Starting client " + (mine.Count + n + 1) + " of " + target + "...");
                Process.Start(new ProcessStartInfo
                {
                    FileName = exe,
                    WorkingDirectory = dir,
                    UseShellExecute = true
                });
                IntPtr hwnd = WaitForNew(known, 40000);
                if (hwnd == IntPtr.Zero)
                {
                    Status("Client " + (mine.Count + n + 1) + " did not open. For Battle.net, press Play on each account, then Apply layout.");
                    return;
                }
                known.Add(hwnd.ToInt64());
                started++;
                int slot = FirstFree(claimed, slots.Count);
                if (slot >= 0)
                {
                    claimed.Add(slot);
                    WindowOps.Place(hwnd, slots[slot]);
                    ReapplyLater(hwnd, slots[slot]);
                }
            }
            string extra = "";
            if (slots.Count == 0) extra = " Arrange them and Save layout.";
            else if (mine.Count + started > slots.Count) extra = " The extra client has no saved slot.";
            Status("Started " + started + " client" + (started == 1 ? "" : "s") + "." + extra);
        }
        catch (Win32Exception ex)
        {
            Status(Blocked(ex));
        }
        catch (Exception ex)
        {
            Status(ex.Message);
        }
        finally
        {
            SetBusy(false);
            RefreshList();
            LoadProfileIntoForm();
        }
    }

    static int FirstFree(HashSet<int> claimed, int count)
    {
        for (int i = 0; i < count; i++)
        {
            if (!claimed.Contains(i)) return i;
        }
        return -1;
    }

    IntPtr WaitForNew(HashSet<long> known, int timeoutMs)
    {
        int start = Environment.TickCount;
        while (unchecked(Environment.TickCount - start) < timeoutMs)
        {
            if (closing) return IntPtr.Zero;
            foreach (var win in WowWindows.List())
            {
                if (!known.Contains(win.Hwnd.ToInt64()) && win.Width >= 320 && win.Height >= 240)
                    return win.Hwnd;
            }
            Application.DoEvents();
            Thread.Sleep(150);
        }
        return IntPtr.Zero;
    }

    void ReapplyLater(IntPtr hwnd, Slot slot)
    {
        ThreadPool.QueueUserWorkItem(delegate
        {
            foreach (int wait in new[] { 2000, 3000 })
            {
                Thread.Sleep(wait);
                if (closing || !NativeMethods.IsWindow(hwnd)) return;
                try { WindowOps.Place(hwnd, slot); }
                catch (Win32Exception) { return; }
            }
            if (IsHandleCreated && !IsDisposed)
            {
                try { BeginInvoke(new Action(RefreshList)); }
                catch (InvalidOperationException) { }
            }
        });
    }
}

static class SelfTest
{
    public static int Run()
    {
        var errors = new List<string>();
        TestStore(errors);
        TestNearest(errors);
        TestWindow(errors);
        string result = errors.Count == 0 ? "ok" : string.Join(Environment.NewLine, errors.ToArray());
        File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "selftest-result.txt"), result + Environment.NewLine);
        return errors.Count == 0 ? 0 : 1;
    }

    static void TestStore(List<string> errors)
    {
        string dir = Path.Combine(Path.GetTempPath(), "client-layout-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            string path = Path.Combine(dir, "layout.json");
            var store = LayoutIo.Empty();
            store.LastExe = @"D:\Games\A\Wow.exe";
            store.Games.Add(new GameProfile
            {
                GameExe = @"D:\Games\A\Wow.exe",
                ClientCount = 2,
                Slots = new List<Slot>
                {
                    new Slot { X = 0, Y = 0, Width = 1280, Height = 720, Borderless = false, TopMost = false },
                    new Slot { X = 1400, Y = 800, Width = 640, Height = 360, Borderless = true, TopMost = true }
                }
            });
            LayoutIo.Save(path, store);
            var loaded = LayoutIo.Load(path);
            if (loaded.Games.Count != 1) errors.Add("store count " + loaded.Games.Count);
            else if (!loaded.Games[0].Slots[1].Borderless || !loaded.Games[0].Slots[1].TopMost)
                errors.Add("store lost border or topmost");
            else if (loaded.Games[0].Slots[1].X != 1400)
                errors.Add("store lost position");
        }
        finally
        {
            try { Directory.Delete(dir, true); } catch (Exception) { }
        }
    }

    static void TestNearest(List<string> errors)
    {
        var slots = new List<Slot>
        {
            new Slot { X = 0, Y = 0, Width = 800, Height = 600 },
            new Slot { X = 2000, Y = 0, Width = 400, Height = 300 }
        };
        var win = new WindowInfo { X = 1900, Y = 10, Width = 400, Height = 300 };
        int index = WindowOps.NearestSlot(win, slots, new HashSet<int>());
        if (index != 1) errors.Add("nearest slot was " + index);
    }

    static void TestWindow(List<string> errors)
    {
        var ready = new ManualResetEvent(false);
        IntPtr hwnd = IntPtr.Zero;
        var thread = new Thread(() =>
        {
            try
            {
                var form = new Form();
                form.Text = "ClientLayoutSelfTest";
                form.FormBorderStyle = FormBorderStyle.Sizable;
                form.StartPosition = FormStartPosition.Manual;
                form.Location = new Point(40, 40);
                form.ClientSize = new Size(400, 300);
                form.MinimumSize = new Size(100, 100);
                form.ShowInTaskbar = false;
                form.Opacity = 0;
                form.Shown += (s, e) =>
                {
                    hwnd = form.Handle;
                    ready.Set();
                };
                Application.Run(form);
            }
            catch (Exception)
            {
                ready.Set();
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.IsBackground = true;
        thread.Start();
        if (!ready.WaitOne(5000) || hwnd == IntPtr.Zero)
        {
            errors.Add("test window did not open");
            return;
        }

        try
        {
            var slot = new Slot { X = 80, Y = 60, Width = 640, Height = 400, Borderless = true, TopMost = true };
            WindowOps.Place(hwnd, slot);
            int style = WindowOps.GetStyle(hwnd);
            int ex = WindowOps.GetExStyle(hwnd);
            if ((style & NativeMethods.WS_CAPTION) != 0) errors.Add("caption still set: " + style.ToString("X"));
            if ((style & NativeMethods.WS_THICKFRAME) != 0) errors.Add("thick frame still set: " + style.ToString("X"));
            if ((ex & NativeMethods.WS_EX_TOPMOST) == 0) errors.Add("topmost not set: " + ex.ToString("X"));
            NativeMethods.Rect rect;
            if (!NativeMethods.GetWindowRect(hwnd, out rect)) errors.Add("no rect");
            else
            {
                int w = rect.Right - rect.Left;
                int h = rect.Bottom - rect.Top;
                if (Math.Abs(rect.Left - 80) > 8 || Math.Abs(rect.Top - 60) > 8 || Math.Abs(w - 640) > 8 || Math.Abs(h - 400) > 8)
                    errors.Add("rect " + rect.Left + "," + rect.Top + " " + w + "x" + h);
            }

            WindowOps.SetBorderless(hwnd, false);
            WindowOps.SetTopMost(hwnd, false);
            WindowOps.RefreshFrame(hwnd);
            style = WindowOps.GetStyle(hwnd);
            ex = WindowOps.GetExStyle(hwnd);
            if ((style & NativeMethods.WS_CAPTION) == 0) errors.Add("caption was not restored: " + style.ToString("X"));
            if ((ex & NativeMethods.WS_EX_TOPMOST) != 0) errors.Add("topmost was not cleared: " + ex.ToString("X"));
        }
        catch (Exception ex)
        {
            errors.Add(ex.ToString());
        }
        finally
        {
            NativeMethods.PostMessage(hwnd, NativeMethods.WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
            thread.Join(3000);
        }
    }
}

static class Program
{
    [STAThread]
    static int Main(string[] args)
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        if (args.Length == 1 && args[0] == "--selftest") return SelfTest.Run();
        Application.Run(new MainForm());
        return 0;
    }
}
