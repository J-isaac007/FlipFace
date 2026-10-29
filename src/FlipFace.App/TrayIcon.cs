using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace FlipFace.App;

/// <summary>The notification-area icon and its menu. Its picture is grey while remapping is off.</summary>
public sealed class TrayIcon : IDisposable
{
    private static readonly Icon OnIcon = DrawIcon(Color.FromArgb(0x2F, 0x6F, 0xEB));
    private static readonly Icon OffIcon = DrawIcon(Color.FromArgb(0x8A, 0x90, 0x9A));

    private readonly FlipFaceController _controller;
    private readonly NotifyIcon _icon;
    private readonly ToolStripMenuItem _remapping;
    private readonly ToolStripMenuItem _profiles;
    private readonly ToolStripMenuItem _startWithWindows;

    public TrayIcon(FlipFaceController controller, Action open, Action exit)
    {
        _controller = controller;
        _remapping = new ToolStripMenuItem("Remapping on", null, (_, _) => controller.Enabled = !controller.Enabled);
        _profiles = new ToolStripMenuItem("Profile");
        _startWithWindows = new ToolStripMenuItem("Start with Windows", null,
            (_, _) => StartWithWindows.IsEnabled = !StartWithWindows.IsEnabled);

        var menu = new ContextMenuStrip();
        menu.Items.Add(new ToolStripMenuItem("Open FlipFace", null, (_, _) => open()) { Font = new Font(menu.Font, System.Drawing.FontStyle.Bold) });
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(_remapping);
        menu.Items.Add(_profiles);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(_startWithWindows);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(new ToolStripMenuItem("Exit", null, (_, _) => exit()));
        menu.Opening += (_, _) => RefreshMenu();

        _icon = new NotifyIcon { ContextMenuStrip = menu, Visible = true };
        _icon.DoubleClick += (_, _) => open();
        controller.StateChanged += Refresh;
        Refresh();
    }

    /// <summary>The same picture as the tray icon, for the window's title bar.</summary>
    public static Icon AppIcon => OnIcon;

    public void ShowTip(string title, string text) => _icon.ShowBalloonTip(4000, title, text, ToolTipIcon.Info);

    private void Refresh()
    {
        var remapping = _controller.Snapshot.Remapping;
        _icon.Icon = remapping ? OnIcon : OffIcon;
        var status = _controller.StartError is not null ? "not running"
            : remapping ? $"remapping with \"{_controller.Profile.Name}\""
            : _controller.Enabled ? "waiting for a controller"
            : "remapping off";
        // Tray tooltips are limited to 63 characters.
        var text = $"FlipFace: {status}";
        _icon.Text = text.Length > 63 ? text[..63] : text;
    }

    private void RefreshMenu()
    {
        _remapping.Checked = _controller.Enabled;
        _startWithWindows.Checked = StartWithWindows.IsEnabled;
        _profiles.DropDownItems.Clear();
        foreach (var profile in _controller.Profiles())
        {
            var name = profile.Name;
            _profiles.DropDownItems.Add(new ToolStripMenuItem(name, null, (_, _) => _controller.SelectProfile(name))
            {
                Checked = string.Equals(name, _controller.Profile.Name, StringComparison.OrdinalIgnoreCase),
            });
        }
    }

    // Four face buttons in a diamond on a rounded tile: FlipFace's whole job in one picture.
    private static Icon DrawIcon(Color tile)
    {
        using var bitmap = new Bitmap(32, 32);
        using (var g = Graphics.FromImage(bitmap))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using var path = new GraphicsPath();
            const int r = 8;
            path.AddArc(1, 1, r * 2, r * 2, 180, 90);
            path.AddArc(31 - r * 2, 1, r * 2, r * 2, 270, 90);
            path.AddArc(31 - r * 2, 31 - r * 2, r * 2, r * 2, 0, 90);
            path.AddArc(1, 31 - r * 2, r * 2, r * 2, 90, 90);
            path.CloseFigure();
            using var fill = new SolidBrush(tile);
            g.FillPath(fill, path);
            foreach (var (x, y) in new[] { (16, 7), (25, 16), (16, 25), (7, 16) })
                g.FillEllipse(Brushes.White, x - 4.5f, y - 4.5f, 9, 9);
        }
        return Icon.FromHandle(bitmap.GetHicon());
    }

    public void Dispose()
    {
        _controller.StateChanged -= Refresh;
        _icon.Visible = false;
        _icon.Dispose();
    }
}
