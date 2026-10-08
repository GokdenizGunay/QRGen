using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace QRGen.Controls;

/// <summary>Açılır HSV renk seçici: doygunluk/parlaklık alanı, ton çubuğu, hazır renkler ve HEX girişi.</summary>
public sealed class ColorPicker : UserControl
{
    public static readonly DependencyProperty ColorProperty = DependencyProperty.Register(
        nameof(Color), typeof(Color), typeof(ColorPicker),
        new FrameworkPropertyMetadata(Colors.Black, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, (d, _) => ((ColorPicker)d).OnColorChanged()));

    public Color Color { get => (Color)GetValue(ColorProperty); set => SetValue(ColorProperty, value); }
    public event EventHandler? ColorChanged;

    static readonly string[] Palette =
    {
        "#000000", "#1F2937", "#4B5563", "#9CA3AF", "#FFFFFF", "#7F1D1D", "#DC2626", "#F97316",
        "#F59E0B", "#EAB308", "#65A30D", "#16A34A", "#059669", "#0D9488", "#0891B2", "#0284C7",
        "#2563EB", "#4F46E5", "#7C3AED", "#9333EA", "#C026D3", "#DB2777", "#E11D48", "#78350F"
    };

    readonly Border _swatch = new() { Width = 22, Height = 22, CornerRadius = new CornerRadius(5), BorderThickness = new Thickness(1) };
    readonly TextBlock _hexLabel = new() { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0), FontFamily = new FontFamily("Consolas, Segoe UI") };
    readonly Popup _popup;
    readonly Grid _svArea = new() { Width = 220, Height = 150, ClipToBounds = true, Cursor = Cursors.Cross };
    readonly Rectangle _hueLayer = new();
    readonly Ellipse _svThumb = new() { Width = 12, Height = 12, Stroke = Brushes.White, StrokeThickness = 2, IsHitTestVisible = false, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
    readonly Slider _hue = new() { Minimum = 0, Maximum = 360, Margin = new Thickness(0, 10, 0, 0) };
    readonly TextBox _hex = new() { Width = 110, FontFamily = new FontFamily("Consolas, Segoe UI") };
    readonly Border _preview = new() { Width = 40, CornerRadius = new CornerRadius(4), BorderThickness = new Thickness(1), Margin = new Thickness(8, 0, 0, 0) };
    double _h, _s, _v;
    bool _internal;

    public ColorPicker()
    {
        _swatch.SetResourceReference(Border.BorderBrushProperty, "ControlStrokeColorDefaultBrush");
        _preview.SetResourceReference(Border.BorderBrushProperty, "ControlStrokeColorDefaultBrush");

        var btn = new Button
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Left,
            Padding = new Thickness(6, 4, 10, 4),
            Content = new StackPanel { Orientation = Orientation.Horizontal, Children = { _swatch, _hexLabel } }
        };

        // Doygunluk / parlaklık alanı
        var white = new Rectangle { Fill = new LinearGradientBrush(Colors.White, Color.FromArgb(0, 255, 255, 255), 0) };
        var black = new Rectangle { Fill = new LinearGradientBrush(Color.FromArgb(0, 0, 0, 0), Colors.Black, 90) };
        _svArea.Children.Add(_hueLayer);
        _svArea.Children.Add(white);
        _svArea.Children.Add(black);
        _svArea.Children.Add(_svThumb);
        _svThumb.Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 3, ShadowDepth = 0, Opacity = 0.8 };
        var svBorder = new Border { CornerRadius = new CornerRadius(6), ClipToBounds = true, Child = _svArea };
        _svArea.MouseLeftButtonDown += (_, e) => { _svArea.CaptureMouse(); PickSv(e.GetPosition(_svArea)); };
        _svArea.MouseMove += (_, e) => { if (_svArea.IsMouseCaptured) PickSv(e.GetPosition(_svArea)); };
        _svArea.MouseLeftButtonUp += (_, _) => _svArea.ReleaseMouseCapture();

        var hueBrush = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 0) };
        for (int i = 0; i <= 6; i++) hueBrush.GradientStops.Add(new GradientStop(FromHsv(i * 60, 1, 1), i / 6.0));
        var hueBar = new Border { Height = 8, CornerRadius = new CornerRadius(4), Background = hueBrush, Margin = new Thickness(0, 10, 0, -6) };
        _hue.ValueChanged += (_, _) => { if (_internal) return; _h = _hue.Value; ApplyHsv(); };

        var pal = new WrapPanel { Width = 224, Margin = new Thickness(0, 10, 0, 0) };
        foreach (var hex in Palette)
        {
            var c = (Color)ColorConverter.ConvertFromString(hex);
            var sw = new Border
            {
                Width = 22, Height = 22, Margin = new Thickness(2), CornerRadius = new CornerRadius(4),
                Background = new SolidColorBrush(c), Cursor = Cursors.Hand, ToolTip = hex, BorderThickness = new Thickness(1)
            };
            sw.SetResourceReference(Border.BorderBrushProperty, "ControlStrokeColorDefaultBrush");
            sw.MouseLeftButtonUp += (_, _) => Color = c;
            pal.Children.Add(sw);
        }

        _hex.KeyDown += (_, e) => { if (e.Key == Key.Enter) CommitHex(); };
        _hex.LostFocus += (_, _) => CommitHex();
        var hexRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 10, 0, 0), Children = { new TextBlock { Text = "HEX", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) }, _hex, _preview } };

        var card = new Border
        {
            Padding = new Thickness(12), CornerRadius = new CornerRadius(8), BorderThickness = new Thickness(1),
            Child = new StackPanel { Children = { svBorder, hueBar, _hue, pal, hexRow } },
            Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 16, ShadowDepth = 3, Opacity = 0.25 },
            Margin = new Thickness(8)
        };
        card.SetResourceReference(Border.BackgroundProperty, "SolidBackgroundFillColorBaseBrush");
        card.SetResourceReference(Border.BorderBrushProperty, "SurfaceStrokeColorFlyoutBrush");
        card.SetResourceReference(TextElement.ForegroundProperty, "TextFillColorPrimaryBrush");

        _popup = new Popup { Child = card, PlacementTarget = btn, Placement = PlacementMode.Bottom, StaysOpen = false, AllowsTransparency = true, PopupAnimation = PopupAnimation.Fade };
        btn.Click += (_, _) => { SyncFromColor(); _popup.IsOpen = true; };

        Content = new Grid { Children = { btn, _popup } };
        OnColorChanged();
    }

    void PickSv(Point p)
    {
        _s = Math.Clamp(p.X / _svArea.Width, 0, 1);
        _v = 1 - Math.Clamp(p.Y / _svArea.Height, 0, 1);
        ApplyHsv();
    }

    void ApplyHsv()
    {
        _internal = true;
        Color = FromHsv(_h, _s, _v);
        _internal = false;
        UpdateVisuals();
    }

    void CommitHex()
    {
        var t = _hex.Text.Trim();
        if (!t.StartsWith('#')) t = "#" + t;
        try { Color = (Color)ColorConverter.ConvertFromString(t); }
        catch { _hex.Text = ToHex(Color); }
    }

    void OnColorChanged()
    {
        var c = Color;
        _swatch.Background = new SolidColorBrush(c);
        _hexLabel.Text = ToHex(c);
        if (!_internal) SyncFromColor();
        else UpdateVisuals();
        ColorChanged?.Invoke(this, EventArgs.Empty);
    }

    void SyncFromColor()
    {
        ToHsv(Color, out var h, out _s, out _v);
        if (_s > 0.001 && _v > 0.001) _h = h;
        UpdateVisuals();
    }

    void UpdateVisuals()
    {
        _internal = true;
        _hue.Value = _h;
        _internal = false;
        _hueLayer.Fill = new SolidColorBrush(FromHsv(_h, 1, 1));
        _svThumb.Margin = new Thickness(_s * _svArea.Width - 6, (1 - _v) * _svArea.Height - 6, 0, 0);
        _preview.Background = new SolidColorBrush(Color);
        if (!_hex.IsKeyboardFocused) _hex.Text = ToHex(Color);
    }

    public static string ToHex(Color c) => $"#{c.R:X2}{c.G:X2}{c.B:X2}";

    public static Color FromHsv(double h, double s, double v)
    {
        h = ((h % 360) + 360) % 360;
        double c = v * s, x = c * (1 - Math.Abs(h / 60 % 2 - 1)), m = v - c;
        var (r, g, b) = (int)(h / 60) switch
        {
            0 => (c, x, 0.0), 1 => (x, c, 0.0), 2 => (0.0, c, x),
            3 => (0.0, x, c), 4 => (x, 0.0, c), _ => (c, 0.0, x)
        };
        return Color.FromRgb((byte)Math.Round((r + m) * 255), (byte)Math.Round((g + m) * 255), (byte)Math.Round((b + m) * 255));
    }

    static void ToHsv(Color c, out double h, out double s, out double v)
    {
        double r = c.R / 255.0, g = c.G / 255.0, b = c.B / 255.0;
        double max = Math.Max(r, Math.Max(g, b)), min = Math.Min(r, Math.Min(g, b)), d = max - min;
        h = d == 0 ? 0 : max == r ? 60 * (((g - b) / d) % 6) : max == g ? 60 * ((b - r) / d + 2) : 60 * ((r - g) / d + 4);
        if (h < 0) h += 360;
        s = max == 0 ? 0 : d / max;
        v = max;
    }

    public static Color Parse(string hex, Color fallback)
    {
        try { return (Color)ColorConverter.ConvertFromString(hex); } catch { return fallback; }
    }
}

