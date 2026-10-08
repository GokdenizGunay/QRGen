using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Win32;
using QRGen.Controls;
using QRGen.Core;
using QRGen.Export;

namespace QRGen;

public partial class MainWindow : Window
{
    enum ContentKind { Text, Url, Wifi, VCard, Email, Sms, Phone, WhatsApp, Geo, Event }

    readonly AppSettings _settings = AppSettings.Load();
    readonly DispatcherTimer _timer;
    Scene? _scene;
    BitmapSource? _logo;
    bool _loading = true;
    int _verifyToken;
    CodeType _prevType = CodeType.QrCode;

    static readonly (string Name, ModuleStyle Style)[] ModuleStyles =
    {
        ("Kare", ModuleStyle.Square), ("Yuvarlatılmış", ModuleStyle.Rounded), ("Nokta", ModuleStyle.Dots),
        ("Elmas", ModuleStyle.Diamond), ("Akışkan (birleşik)", ModuleStyle.Smooth), ("Küçük kare", ModuleStyle.SmallSquares),
        ("Dikey çizgiler", ModuleStyle.VerticalBars), ("Yatay çizgiler", ModuleStyle.HorizontalBars), ("Yıldız", ModuleStyle.Star)
    };
    static readonly (string Name, EyeFrameStyle Style)[] FrameStyles =
    {
        ("Kare", EyeFrameStyle.Square), ("Yuvarlatılmış", EyeFrameStyle.Rounded), ("Çok yuvarlak", EyeFrameStyle.ExtraRounded),
        ("Daire", EyeFrameStyle.Circle), ("Yaprak", EyeFrameStyle.Leaf)
    };
    static readonly (string Name, EyeBallStyle Style)[] BallStyles =
    {
        ("Kare", EyeBallStyle.Square), ("Yuvarlatılmış", EyeBallStyle.Rounded), ("Daire", EyeBallStyle.Circle),
        ("Elmas", EyeBallStyle.Diamond), ("Yaprak", EyeBallStyle.Leaf)
    };
    static readonly (string Name, GradientKind Kind)[] Gradients =
    {
        ("Yatay →", GradientKind.Horizontal), ("Dikey ↓", GradientKind.Vertical), ("Çapraz ↘", GradientKind.Diagonal),
        ("Çapraz ↗", GradientKind.DiagonalReverse), ("Radyal ◎", GradientKind.Radial)
    };
    static readonly (string Name, ContentKind Kind)[] ContentKinds =
    {
        ("Düz metin", ContentKind.Text), ("Web adresi (URL)", ContentKind.Url), ("Wi-Fi ağı", ContentKind.Wifi),
        ("Kartvizit (vCard)", ContentKind.VCard), ("E-posta", ContentKind.Email), ("SMS", ContentKind.Sms),
        ("Telefon", ContentKind.Phone), ("WhatsApp", ContentKind.WhatsApp), ("Konum (coğrafi)", ContentKind.Geo),
        ("Takvim etkinliği", ContentKind.Event)
    };

    sealed record Preset(string Name, string Fg1, string? Fg2, string Bg, GradientKind Grad, ModuleStyle M, EyeFrameStyle F, EyeBallStyle B,
                         string? EyeFrame = null, string? EyeBall = null);

    static readonly Preset[] Presets =
    {
        new("Klasik", "#000000", null, "#FFFFFF", GradientKind.Diagonal, ModuleStyle.Square, EyeFrameStyle.Square, EyeBallStyle.Square),
        new("Okyanus", "#0EA5E9", "#1E3A8A", "#FFFFFF", GradientKind.Diagonal, ModuleStyle.Rounded, EyeFrameStyle.Rounded, EyeBallStyle.Rounded),
        new("Gün batımı", "#F97316", "#DB2777", "#FFFFFF", GradientKind.Horizontal, ModuleStyle.Dots, EyeFrameStyle.Circle, EyeBallStyle.Circle),
        new("Orman", "#065F46", null, "#F0FDF4", GradientKind.Diagonal, ModuleStyle.Smooth, EyeFrameStyle.ExtraRounded, EyeBallStyle.Rounded, "#047857", "#10B981"),
        new("Neon", "#22D3EE", "#A855F7", "#0F172A", GradientKind.Diagonal, ModuleStyle.Dots, EyeFrameStyle.Rounded, EyeBallStyle.Circle),
        new("Lavanta", "#7C3AED", "#4F46E5", "#FFFFFF", GradientKind.Radial, ModuleStyle.Diamond, EyeFrameStyle.Leaf, EyeBallStyle.Leaf),
        new("Kiraz", "#9F1239", null, "#FFF1F2", GradientKind.Diagonal, ModuleStyle.Smooth, EyeFrameStyle.Circle, EyeBallStyle.Circle, "#9F1239", "#E11D48"),
        new("Kehribar", "#92400E", "#D97706", "#FFFBEB", GradientKind.Vertical, ModuleStyle.VerticalBars, EyeFrameStyle.Rounded, EyeBallStyle.Rounded),
        new("Grafit", "#E5E7EB", null, "#111827", GradientKind.Diagonal, ModuleStyle.SmallSquares, EyeFrameStyle.Square, EyeBallStyle.Square),
    };

    public MainWindow()
    {
        InitializeComponent();
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(120) };
        _timer.Tick += (_, _) => { _timer.Stop(); Render(); };

        PopulateLists();
        ApplyAppSettings();
        HookChanges(this);
        WireButtons();

        _loading = false;
        UpdateVisibility();
        Render();
    }

    // ------------------------------------------------------------------ Kurulum

    void PopulateLists()
    {
        foreach (CodeType t in Enum.GetValues<CodeType>())
            cmbType.Items.Add(new ComboBoxItem { Content = CodeTypeInfo.Display(t), Tag = t });
        cmbType.SelectedIndex = 0;

        foreach (var (n, k) in ContentKinds) cmbContentType.Items.Add(new ComboBoxItem { Content = n, Tag = k });
        cmbContentType.SelectedIndex = 1;

        cmbVersion.Items.Add(new ComboBoxItem { Content = "Otomatik", Tag = 0 });
        for (int v = 1; v <= 40; v++) cmbVersion.Items.Add(new ComboBoxItem { Content = $"{v}  ({17 + 4 * v}×{17 + 4 * v})", Tag = v });
        cmbVersion.SelectedIndex = 0;

        cmbMask.Items.Add(new ComboBoxItem { Content = "Otomatik", Tag = -1 });
        for (int i = 0; i < 8; i++) cmbMask.Items.Add(new ComboBoxItem { Content = i.ToString(), Tag = i });
        cmbMask.SelectedIndex = 0;

        for (int i = 0; i <= 8; i++) cmbPdfEcc.Items.Add(new ComboBoxItem { Content = $"Seviye {i}", Tag = i });
        cmbPdfEcc.SelectedIndex = 2;

        foreach (var (n, s) in ModuleStyles) cmbModule.Items.Add(new ComboBoxItem { Content = n, Tag = s });
        foreach (var (n, s) in FrameStyles) cmbEyeFrame.Items.Add(new ComboBoxItem { Content = n, Tag = s });
        foreach (var (n, s) in BallStyles) cmbEyeBall.Items.Add(new ComboBoxItem { Content = n, Tag = s });
        foreach (var (n, k) in Gradients) cmbGradient.Items.Add(new ComboBoxItem { Content = n, Tag = k });
        cmbModule.SelectedIndex = cmbEyeFrame.SelectedIndex = cmbEyeBall.SelectedIndex = 0;
        cmbGradient.SelectedIndex = 2;

        var fonts = Fonts.SystemFontFamilies.Select(f => f.Source).Distinct().OrderBy(s => s, StringComparer.CurrentCultureIgnoreCase).ToList();
        foreach (var f in fonts) cmbFont.Items.Add(new ComboBoxItem { Content = f, Tag = f, FontFamily = new FontFamily(f) });
        SelectByTag(cmbFont, "Segoe UI");
        if (cmbFont.SelectedIndex < 0 && cmbFont.Items.Count > 0) cmbFont.SelectedIndex = 0;

        foreach (var d in new[] { 72, 96, 150, 300, 600 }) cmbDpi.Items.Add(new ComboBoxItem { Content = $"{d} DPI", Tag = d });

        dpStart.SelectedDate = DateTime.Today;
        dpEnd.SelectedDate = DateTime.Today;

        foreach (var p in Presets) pnlPresets.Children.Add(MakePresetButton(p));
    }

    Button MakePresetButton(Preset p)
    {
        var fg1 = ColorPicker.Parse(p.Fg1, Colors.Black);
        Brush fill = p.Fg2 == null ? new SolidColorBrush(fg1) : new LinearGradientBrush(fg1, ColorPicker.Parse(p.Fg2, fg1), 45);
        var swatch = new Border
        {
            Width = 34, Height = 34, CornerRadius = new CornerRadius(7),
            Background = new SolidColorBrush(ColorPicker.Parse(p.Bg, Colors.White)),
            BorderThickness = new Thickness(1), Padding = new Thickness(7),
            Child = new Border { CornerRadius = new CornerRadius(p.M is ModuleStyle.Dots or ModuleStyle.Rounded or ModuleStyle.Smooth ? 10 : 2), Background = fill }
        };
        swatch.SetResourceReference(Border.BorderBrushProperty, "ControlStrokeColorDefaultBrush");
        var b = new Button
        {
            Margin = new Thickness(0, 0, 5, 5), Padding = new Thickness(4, 6, 4, 6), Width = 72,
            Content = new StackPanel { Children = { swatch, new TextBlock { Text = p.Name, FontSize = 11, Margin = new Thickness(0, 5, 0, 0), HorizontalAlignment = HorizontalAlignment.Center } } }
        };
        b.Click += (_, _) => ApplyPreset(p);
        return b;
    }

    void ApplyPreset(Preset p)
    {
        _loading = true;
        cpFg1.Color = ColorPicker.Parse(p.Fg1, Colors.Black);
        chkGradient.IsChecked = p.Fg2 != null;
        if (p.Fg2 != null) cpFg2.Color = ColorPicker.Parse(p.Fg2, Colors.Black);
        cpBg.Color = ColorPicker.Parse(p.Bg, Colors.White);
        chkTransparent.IsChecked = false;
        SelectByTag(cmbGradient, p.Grad);
        SelectByTag(cmbModule, p.M);
        SelectByTag(cmbEyeFrame, p.F);
        SelectByTag(cmbEyeBall, p.B);
        chkCustomEyes.IsChecked = p.EyeFrame != null;
        if (p.EyeFrame != null) cpEyeFrame.Color = ColorPicker.Parse(p.EyeFrame, Colors.Black);
        if (p.EyeBall != null) cpEyeBall.Color = ColorPicker.Parse(p.EyeBall, Colors.Black);
        cpText.Color = p.Fg2 != null ? ColorPicker.Parse(p.Fg2, Colors.Black) : cpFg1.Color;
        _loading = false;
        Changed();
    }

    void ApplyAppSettings()
    {
        SelectByTag(cmbTheme, _settings.Theme);
        if (cmbTheme.SelectedIndex < 0) cmbTheme.SelectedIndex = 0;
        ApplyTheme(_settings.Theme);
        txtSize.Text = Math.Clamp(_settings.ExportSize, 16, 10000).ToString();
        SelectByTag(cmbDpi, _settings.Dpi);
        if (cmbDpi.SelectedIndex < 0) SelectByTag(cmbDpi, 300);
        if (_settings.LastStyle != null) ApplyStyle(_settings.LastStyle);
    }

    void ApplyTheme(string theme)
    {
        Application.Current.ThemeMode = theme switch
        {
            "Light" => ThemeMode.Light,
            "Dark" => ThemeMode.Dark,
            _ => ThemeMode.System
        };
    }

    void HookChanges(DependencyObject root)
    {
        foreach (var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())
        {
            switch (child)
            {
                case ColorPicker cp: cp.ColorChanged += (_, _) => Changed(); continue;
                case TextBox tb when tb == txtBatch: continue;
                case TextBox tb when tb == txtSize: tb.TextChanged += (_, _) => UpdateExportInfo(); continue;
                case TextBox tb: tb.TextChanged += (_, _) => Changed(); break;
                case ComboBox cb when cb == cmbTheme || cb == cmbBatchFormat: break;
                case ComboBox cb when cb == cmbDpi: cb.SelectionChanged += (_, _) => UpdateExportInfo(); break;
                case ComboBox cb when cb == cmbType: cb.SelectionChanged += (_, _) => OnTypeChanged(); break;
                case ComboBox cb: cb.SelectionChanged += (_, _) => Changed(); break;
                case CheckBox ch when ch == chkLogoAutoH || ch == chkBatchNames: break;
                case CheckBox ch when ch == chkSnap: ch.Click += (_, _) => UpdateExportInfo(); break;
                case CheckBox ch: ch.Click += (_, _) => Changed(); break;
                case Slider s when s == sldJpeg: break;
                case Slider s: s.ValueChanged += (_, _) => Changed(); break;
                case DatePicker dp: dp.SelectedDateChanged += (_, _) => Changed(); break;
            }
            HookChanges(child);
        }
    }

    void WireButtons()
    {
        cmbTheme.SelectionChanged += (_, _) =>
        {
            if (cmbTheme.SelectedItem is ComboBoxItem { Tag: string t })
            {
                ApplyTheme(t);
                _settings.Theme = t;
            }
        };
        btnSwap.Click += (_, _) =>
        {
            _loading = true;
            (cpFg1.Color, cpBg.Color) = (cpBg.Color, cpFg1.Color);
            _loading = false;
            Changed();
        };
        btnLogo.Click += (_, _) => PickLogo();
        btnLogoRemove.Click += (_, _) => SetLogo(null);
        btnSaveStyle.Click += (_, _) => SaveStyle();
        btnLoadStyle.Click += (_, _) => LoadStyle();
        btnResetStyle.Click += (_, _) => { ApplyStyle(new StylePreset()); Changed(); };
        btnSavePng.Click += (_, _) => SaveAs("png");
        btnSaveAs.Click += (_, _) => SaveAs(null);
        btnCopyQuick.Click += (_, _) => CopyImage();
        btnCopyImage.Click += (_, _) => CopyImage();
        btnCopySvg.Click += (_, _) => CopySvg();
        btnBatch.Click += async (_, _) => await BatchExport();
        Drop += OnDrop;
        DragOver += (_, e) => { e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None; e.Handled = true; };
        PreviewKeyDown += OnKey;
        Closing += (_, _) =>
        {
            _settings.ExportSize = ExportSize();
            _settings.Dpi = Dpi();
            _settings.LastStyle = CaptureStyle();
            _settings.Save();
        };
    }

    // ------------------------------------------------------------------ Durum

    void Changed()
    {
        if (_loading) return;
        UpdateVisibility();
        _timer.Stop();
        _timer.Start();
    }

    CodeType SelType => cmbType.SelectedItem is ComboBoxItem { Tag: CodeType t } ? t : CodeType.QrCode;
    ContentKind SelKind => cmbContentType.SelectedItem is ComboBoxItem { Tag: ContentKind k } ? k : ContentKind.Text;

    void OnTypeChanged()
    {
        if (_loading) return;
        var t = SelType;
        bool was1D = CodeTypeInfo.Is1D(_prevType), is1D = CodeTypeInfo.Is1D(t);
        _loading = true;
        if (is1D != was1D) sldMargin.Value = is1D ? 10 : 4;
        if (is1D)
        {
            var test = new CodeOptions { Type = t, Content = txtText.Text };
            try { MatrixEncoder.Encode(test); }
            catch { txtText.Text = CodeTypeInfo.Sample(t); }
        }
        else if (was1D && SelKind == ContentKind.Text && txtText.Text == CodeTypeInfo.Sample(_prevType))
            txtText.Text = "https://example.com";
        _loading = false;
        _prevType = t;
        Changed();
    }

    void UpdateVisibility()
    {
        var t = SelType;
        bool is1D = CodeTypeInfo.Is1D(t), isQr = t == CodeType.QrCode;
        var k = is1D ? ContentKind.Text : SelKind;

        static Visibility V(bool b) => b ? Visibility.Visible : Visibility.Collapsed;
        pnlContentType.Visibility = V(!is1D);
        pnlText.Visibility = V(k == ContentKind.Text);
        pnlUrl.Visibility = V(k == ContentKind.Url);
        pnlWifi.Visibility = V(k == ContentKind.Wifi);
        pnlVCard.Visibility = V(k == ContentKind.VCard);
        pnlEmail.Visibility = V(k == ContentKind.Email);
        pnlSms.Visibility = V(k is ContentKind.Sms or ContentKind.WhatsApp);
        pnlPhone.Visibility = V(k == ContentKind.Phone);
        pnlGeo.Visibility = V(k == ContentKind.Geo);
        pnlEvent.Visibility = V(k == ContentKind.Event);
        txtTypeHint.Text = CodeTypeInfo.Hint(t);
        txtTypeHint.Visibility = V(txtTypeHint.Text.Length > 0 && t != CodeType.QrCode);

        pnlQrSettings.Visibility = V(isQr);
        pnlDmSettings.Visibility = V(t == CodeType.DataMatrix);
        pnlPdfSettings.Visibility = V(t == CodeType.Pdf417);
        pnlAztecSettings.Visibility = V(t == CodeType.Aztec);
        pnl1DSettings.Visibility = V(is1D);

        pnlEyeStyles.Visibility = V(isQr);
        pnlEyeColors.Visibility = V(isQr);
        pnlGradient.Visibility = V(chkGradient.IsChecked == true);
        pnlEyeColorPickers.Visibility = V(chkCustomEyes.IsChecked == true);
        txtLogoNot2D.Visibility = V(is1D);
        previewFrame.Background = chkTransparent.IsChecked == true ? CheckerBrush() : null;
    }

    static Brush CheckerBrush()
    {
        var g = new DrawingGroup();
        var dark = new SolidColorBrush(Color.FromArgb(0x38, 0x80, 0x80, 0x80));
        g.Children.Add(new GeometryDrawing(Brushes.Transparent, null, new RectangleGeometry(new Rect(0, 0, 16, 16))));
        g.Children.Add(new GeometryDrawing(dark, null, new RectangleGeometry(new Rect(0, 0, 8, 8))));
        g.Children.Add(new GeometryDrawing(dark, null, new RectangleGeometry(new Rect(8, 8, 8, 8))));
        return new DrawingBrush(g) { TileMode = TileMode.Tile, Viewport = new Rect(0, 0, 16, 16), ViewportUnits = BrushMappingMode.Absolute };
    }

    // ------------------------------------------------------------------ Seçenekler

    string BuildContent()
    {
        if (CodeTypeInfo.Is1D(SelType)) return txtText.Text.Trim();
        switch (SelKind)
        {
            case ContentKind.Url: return Payloads.Url(txtUrl.Text);
            case ContentKind.Wifi:
                return Payloads.Wifi(txtSsid.Text, txtWifiPass.Text, (cmbWifiSec.SelectedItem as ComboBoxItem)?.Tag as string ?? "WPA", chkWifiHidden.IsChecked == true);
            case ContentKind.VCard:
                return Payloads.VCard(txtFirst.Text.Trim(), txtLast.Text.Trim(), txtOrg.Text.Trim(), txtTitle.Text.Trim(), txtPhone.Text.Trim(),
                    txtMobile.Text.Trim(), txtEmail.Text.Trim(), txtWeb.Text.Trim(), txtStreet.Text.Trim(), txtCity.Text.Trim(),
                    txtZip.Text.Trim(), txtCountry.Text.Trim(), txtNote.Text.Trim());
            case ContentKind.Email: return Payloads.Email(txtMailTo.Text, txtMailSubject.Text, txtMailBody.Text);
            case ContentKind.Sms: return Payloads.Sms(txtSmsNumber.Text, txtSmsMsg.Text);
            case ContentKind.WhatsApp: return Payloads.WhatsApp(txtSmsNumber.Text, txtSmsMsg.Text);
            case ContentKind.Phone: return Payloads.Phone(txtPhoneNum.Text);
            case ContentKind.Geo: return Payloads.Geo(txtLat.Text, txtLon.Text);
            case ContentKind.Event:
                bool allDay = chkAllDay.IsChecked == true;
                var s = (dpStart.SelectedDate ?? DateTime.Today).Date;
                var e = (dpEnd.SelectedDate ?? s).Date;
                if (!allDay)
                {
                    if (TimeSpan.TryParse(txtStartTime.Text, CultureInfo.InvariantCulture, out var st)) s += st;
                    if (TimeSpan.TryParse(txtEndTime.Text, CultureInfo.InvariantCulture, out var et)) e += et;
                }
                else e = e.AddDays(1);
                return Payloads.Event(txtEvTitle.Text, txtEvLoc.Text, txtEvDesc.Text, s, e, allDay);
            default: return txtText.Text;
        }
    }

    static T TagOf<T>(ComboBox cb, T fallback) => cb.SelectedItem is ComboBoxItem { Tag: T v } ? v : fallback;

    CodeOptions ReadOptions()
    {
        var o = new CodeOptions
        {
            Type = SelType,
            Content = BuildContent(),
            Ecc = TagOf(cmbEcc, "M")[0],
            QrVersion = TagOf(cmbVersion, 0),
            QrMask = TagOf(cmbMask, -1),
            Charset = TagOf(cmbCharset, "UTF-8"),
            QrEci = chkEci.IsChecked == true,
            Margin = (int)sldMargin.Value,
            DataMatrixShape = cmbDmShape.SelectedIndex,
            Pdf417Ecc = TagOf(cmbPdfEcc, 2),
            Pdf417Compact = chkPdfCompact.IsChecked == true,
            AztecEcc = (int)sldAztecEcc.Value,
            BarHeightRatio = sldBarHeight.Value / 100.0,
            ShowText = chkShowText.IsChecked == true,
            Logo = _logo,
            LogoPercent = sldLogoSize.Value,
            LogoPadding = sldLogoPad.Value,
            LogoBg = (LogoBackground)Math.Max(0, cmbLogoBg.SelectedIndex),
            LogoClear = chkLogoClear.IsChecked == true,
            Caption = txtCaption.Text,
        };
        ApplyStyleTo(o, CaptureStyle());
        return o;
    }

    StylePreset CaptureStyle() => new()
    {
        ModuleStyle = TagOf(cmbModule, ModuleStyle.Square).ToString(),
        EyeFrame = TagOf(cmbEyeFrame, EyeFrameStyle.Square).ToString(),
        EyeBall = TagOf(cmbEyeBall, EyeBallStyle.Square).ToString(),
        ModuleScale = sldModuleScale.Value / 100.0,
        Fg1 = ColorPicker.ToHex(cpFg1.Color),
        Fg2 = ColorPicker.ToHex(cpFg2.Color),
        UseGradient = chkGradient.IsChecked == true,
        Gradient = TagOf(cmbGradient, GradientKind.Diagonal).ToString(),
        Background = ColorPicker.ToHex(cpBg.Color),
        Transparent = chkTransparent.IsChecked == true,
        CustomEyes = chkCustomEyes.IsChecked == true,
        EyeFrameColor = ColorPicker.ToHex(cpEyeFrame.Color),
        EyeBallColor = ColorPicker.ToHex(cpEyeBall.Color),
        TextColor = ColorPicker.ToHex(cpText.Color),
        FontFamily = TagOf(cmbFont, "Segoe UI"),
        FontPercent = sldFont.Value,
        FontBold = chkBold.IsChecked == true,
        LogoPercent = sldLogoSize.Value,
        LogoPadding = sldLogoPad.Value,
        LogoBg = ((LogoBackground)Math.Max(0, cmbLogoBg.SelectedIndex)).ToString(),
        LogoClear = chkLogoClear.IsChecked == true,
    };

    static void ApplyStyleTo(CodeOptions o, StylePreset p)
    {
        o.ModuleStyle = Enum.TryParse<ModuleStyle>(p.ModuleStyle, out var ms) ? ms : ModuleStyle.Square;
        o.EyeFrame = Enum.TryParse<EyeFrameStyle>(p.EyeFrame, out var ef) ? ef : EyeFrameStyle.Square;
        o.EyeBall = Enum.TryParse<EyeBallStyle>(p.EyeBall, out var eb) ? eb : EyeBallStyle.Square;
        o.ModuleScale = p.ModuleScale;
        o.Fg1 = ColorPicker.Parse(p.Fg1, Colors.Black);
        o.Fg2 = ColorPicker.Parse(p.Fg2, Colors.Black);
        o.UseGradient = p.UseGradient;
        o.Gradient = Enum.TryParse<GradientKind>(p.Gradient, out var g) ? g : GradientKind.Diagonal;
        o.Background = ColorPicker.Parse(p.Background, Colors.White);
        o.Transparent = p.Transparent;
        o.CustomEyes = p.CustomEyes;
        o.EyeFrameColor = ColorPicker.Parse(p.EyeFrameColor, Colors.Black);
        o.EyeBallColor = ColorPicker.Parse(p.EyeBallColor, Colors.Black);
        o.TextColor = ColorPicker.Parse(p.TextColor, Colors.Black);
        o.FontFamily = p.FontFamily;
        o.FontPercent = p.FontPercent;
        o.FontBold = p.FontBold;
    }

    void ApplyStyle(StylePreset p)
    {
        bool prev = _loading;
        _loading = true;
        var o = new CodeOptions();
        ApplyStyleTo(o, p);
        SelectByTag(cmbModule, o.ModuleStyle);
        SelectByTag(cmbEyeFrame, o.EyeFrame);
        SelectByTag(cmbEyeBall, o.EyeBall);
        sldModuleScale.Value = Math.Clamp(o.ModuleScale * 100, 40, 100);
        cpFg1.Color = o.Fg1; cpFg2.Color = o.Fg2;
        chkGradient.IsChecked = o.UseGradient;
        SelectByTag(cmbGradient, o.Gradient);
        cpBg.Color = o.Background;
        chkTransparent.IsChecked = o.Transparent;
        chkCustomEyes.IsChecked = o.CustomEyes;
        cpEyeFrame.Color = o.EyeFrameColor; cpEyeBall.Color = o.EyeBallColor;
        cpText.Color = o.TextColor;
        SelectByTag(cmbFont, o.FontFamily);
        sldFont.Value = Math.Clamp(o.FontPercent, 3, 20);
        chkBold.IsChecked = o.FontBold;
        sldLogoSize.Value = Math.Clamp(p.LogoPercent, 8, 35);
        sldLogoPad.Value = Math.Clamp(p.LogoPadding, 0, 4);
        cmbLogoBg.SelectedIndex = Enum.TryParse<LogoBackground>(p.LogoBg, out var lb) ? (int)lb : 2;
        chkLogoClear.IsChecked = p.LogoClear;
        _loading = prev;
    }

    static void SelectByTag(ComboBox cb, object value)
    {
        foreach (var item in cb.Items)
            if (item is ComboBoxItem ci && Equals(ci.Tag, value)) { cb.SelectedItem = ci; return; }
    }

    // ------------------------------------------------------------------ Çizim

    void Render()
    {
        try
        {
            var o = ReadOptions();
            var m = MatrixEncoder.Encode(o);
            _scene = SceneBuilder.Build(o, m);
            imgPreview.Source = new DrawingImage(WpfRenderer.ToDrawing(_scene));
            imgPreview.Opacity = 1;
            errorBanner.Visibility = Visibility.Collapsed;
            txtInfo.Text = m.Info + $" · {o.Content.Length} karakter";
            UpdateExportInfo();
            StartVerify(_scene, o);
        }
        catch (Exception ex)
        {
            _scene = null;
            imgPreview.Opacity = 0.25;
            txtError.Text = ex.Message;
            errorBanner.Visibility = Visibility.Visible;
            txtInfo.Text = "";
            SetVerify(null, "Kod oluşturulamadı");
            UpdateExportInfo();
        }
    }

    void StartVerify(Scene scene, CodeOptions o)
    {
        int token = ++_verifyToken;
        if (!Verifier.Supported(o.Type)) { SetVerify(null, "Bu barkod türü için otomatik doğrulama yok"); return; }
        SetVerify(null, "Doğrulanıyor…");
        var (px, w, h) = Verifier.Snapshot(scene);
        string expected = o.Content;
        bool is1D = CodeTypeInfo.Is1D(o.Type);
        var type = o.Type;
        Task.Run(() =>
        {
            string? text = null;
            try { text = Verifier.Decode(px, w, h, type); } catch { }
            return text;
        }).ContinueWith(t =>
        {
            if (token != _verifyToken) return;
            var text = t.Result;
            if (text == null) SetVerify(false, "Okunamadı — kontrastı artırın, logoyu küçültün veya hata düzeltmeyi yükseltin");
            else if (text == expected || (is1D && text.StartsWith(expected, StringComparison.Ordinal)) || text.Replace("\r", "") == expected.Replace("\r", ""))
                SetVerify(true, "Taranabilir — içerik doğrulandı");
            else SetVerify(false, "Okundu ancak içerik farklı (karakter kodlamasını kontrol edin)");
        }, TaskScheduler.FromCurrentSynchronizationContext());
    }

    void SetVerify(bool? ok, string text)
    {
        txtVerify.Text = text;
        dotVerify.SetResourceReference(System.Windows.Shapes.Shape.FillProperty, ok switch
        {
            true => "SystemFillColorSuccessBrush",
            false => "SystemFillColorCautionBrush",
            _ => "TextFillColorTertiaryBrush"
        });
    }

    int ExportSize() => int.TryParse(txtSize.Text, out var v) ? Math.Clamp(v, 16, 10000) : 1024;
    int Dpi() => TagOf(cmbDpi, 300);

    void UpdateExportInfo()
    {
        if (_scene == null) { txtExportInfo.Text = ""; return; }
        var (w, h, _) = WpfRenderer.PixelSize(_scene, ExportSize(), chkSnap.IsChecked == true);
        double dpi = Dpi();
        txtExportInfo.Text = $"Gerçek çıktı: {w} × {h} piksel · Baskı boyutu {w / dpi * 2.54:0.##} × {h / dpi * 2.54:0.##} cm ({dpi} DPI)";
    }

    // ------------------------------------------------------------------ Dışa aktarma

    static readonly string[] Exts = { "png", "jpg", "bmp", "tif", "gif", "svg", "pdf", "eps" };
    const string AllFilter = "PNG görüntü|*.png|JPEG görüntü|*.jpg|BMP görüntü|*.bmp|TIFF görüntü|*.tif|GIF görüntü|*.gif|SVG vektör|*.svg|PDF belge|*.pdf|EPS vektör|*.eps";

    void SizePreset_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string s }) txtSize.Text = s;
    }

    void ExportFormat_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string ext }) SaveAs(ext);
    }

    void SaveAs(string? ext)
    {
        if (_scene == null) { ShowError("Kaydedilecek geçerli bir kod yok."); return; }
        var dlg = new SaveFileDialog
        {
            Title = "Kodu kaydet",
            FileName = $"qrgen_{DateTime.Now:yyyyMMdd_HHmmss}",
            Filter = AllFilter,
            FilterIndex = ext == null ? 1 : Array.IndexOf(Exts, ext) + 1,
            AddExtension = true,
            OverwritePrompt = true,
        };
        if (_settings.LastFolder != null && Directory.Exists(_settings.LastFolder)) dlg.InitialDirectory = _settings.LastFolder;
        if (dlg.ShowDialog(this) != true) return;
        var chosen = Path.GetExtension(dlg.FileName).TrimStart('.').ToLowerInvariant();
        if (chosen == "jpeg") chosen = "jpg";
        if (chosen == "tiff") chosen = "tif";
        if (!Exts.Contains(chosen)) chosen = Exts[Math.Clamp(dlg.FilterIndex - 1, 0, Exts.Length - 1)];
        try
        {
            ExportScene(_scene, dlg.FileName, chosen);
            _settings.LastFolder = Path.GetDirectoryName(dlg.FileName);
            SetVerify(true, $"Kaydedildi: {Path.GetFileName(dlg.FileName)}");
        }
        catch (Exception ex) { ShowError("Kaydetme başarısız: " + ex.Message); }
    }

    void ExportScene(Scene s, string path, string ext)
    {
        int size = ExportSize();
        bool snap = chkSnap.IsChecked == true;
        int dpi = Dpi();
        var (w, h, _) = WpfRenderer.PixelSize(s, size, snap);
        switch (ext)
        {
            case "svg": File.WriteAllText(path, SvgExporter.Build(s, w, h)); break;
            case "pdf": PdfExporter.Save(s, path, w, h, dpi); break;
            case "eps": EpsExporter.Save(s, path, w, h, dpi); break;
            default:
                var fmt = ext switch
                {
                    "jpg" => RasterFormat.Jpeg, "bmp" => RasterFormat.Bmp, "tif" => RasterFormat.Tiff,
                    "gif" => RasterFormat.Gif, _ => RasterFormat.Png
                };
                WpfRenderer.Save(s, path, fmt, size, snap, dpi, (int)sldJpeg.Value);
                break;
        }
    }

    void CopyImage()
    {
        if (_scene == null) return;
        try
        {
            var bmp = WpfRenderer.Render(_scene, Math.Min(ExportSize(), 4096), chkSnap.IsChecked == true, 96);
            var data = new DataObject();
            data.SetImage(_scene.Transparent ? bmp : new FormatConvertedBitmap(bmp, PixelFormats.Bgr24, null, 0));
            data.SetData("PNG", new MemoryStream(WpfRenderer.EncodePng(bmp)));
            Clipboard.SetDataObject(data, true);
            SetVerify(true, "Görüntü panoya kopyalandı");
        }
        catch (Exception ex) { ShowError("Kopyalanamadı: " + ex.Message); }
    }

    void CopySvg()
    {
        if (_scene == null) return;
        var (w, h, _) = WpfRenderer.PixelSize(_scene, ExportSize(), chkSnap.IsChecked == true);
        try
        {
            Clipboard.SetText(SvgExporter.Build(_scene, w, h));
            SetVerify(true, "SVG kodu panoya kopyalandı");
        }
        catch (Exception ex) { ShowError("Kopyalanamadı: " + ex.Message); }
    }

    async Task BatchExport()
    {
        var lines = txtBatch.Text.Replace("\r", "").Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0).ToList();
        if (lines.Count == 0) { txtBatchStatus.Text = "Önce her satıra bir içerik yazın."; return; }
        var dlg = new OpenFolderDialog { Title = "Kayıt klasörünü seçin" };
        if (_settings.LastFolder != null && Directory.Exists(_settings.LastFolder)) dlg.InitialDirectory = _settings.LastFolder;
        if (dlg.ShowDialog(this) != true) return;
        string folder = dlg.FolderName;
        _settings.LastFolder = folder;
        string ext = TagOf(cmbBatchFormat, "png");
        var baseOpts = ReadOptions();
        int ok = 0, fail = 0;
        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        btnBatch.IsEnabled = false;
        for (int i = 0; i < lines.Count; i++)
        {
            try
            {
                baseOpts.Content = lines[i];
                var m = MatrixEncoder.Encode(baseOpts);
                var scene = SceneBuilder.Build(baseOpts, m);
                string name = chkBatchNames.IsChecked == true ? SafeName(lines[i]) : $"kod_{i + 1:D3}";
                string unique = name;
                for (int n = 2; !used.Add(unique) || File.Exists(Path.Combine(folder, unique + "." + ext)); n++) unique = $"{name}_{n}";
                ExportScene(scene, Path.Combine(folder, unique + "." + ext), ext);
                ok++;
            }
            catch { fail++; }
            txtBatchStatus.Text = $"{i + 1}/{lines.Count} işlendi…";
            await Dispatcher.Yield(DispatcherPriority.Background);
        }
        btnBatch.IsEnabled = true;
        txtBatchStatus.Text = $"Tamamlandı: {ok} dosya kaydedildi" + (fail > 0 ? $", {fail} satır hatalı (geçersiz içerik)." : ".");
    }

    static string SafeName(string s)
    {
        var bad = Path.GetInvalidFileNameChars();
        var chars = s.Select(c => bad.Contains(c) || char.IsWhiteSpace(c) ? '_' : c).ToArray();
        var r = new string(chars).Trim('_', '.');
        if (r.Length > 50) r = r[..50];
        return r.Length == 0 ? "kod" : r;
    }

    // ------------------------------------------------------------------ Logo ve stil dosyaları

    void PickLogo()
    {
        var dlg = new OpenFileDialog
        {
            Title = "Logo seç",
            Filter = "Görseller|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.tif;*.tiff;*.ico|Tüm dosyalar|*.*"
        };
        if (dlg.ShowDialog(this) == true) LoadLogo(dlg.FileName);
    }

    void LoadLogo(string path)
    {
        try
        {
            var bi = new BitmapImage();
            bi.BeginInit();
            bi.CacheOption = BitmapCacheOption.OnLoad;
            bi.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
            bi.UriSource = new Uri(path);
            bi.EndInit();
            bi.Freeze();
            SetLogo(bi);
        }
        catch (Exception ex) { ShowError("Görsel yüklenemedi: " + ex.Message); }
    }

    void SetLogo(BitmapSource? logo)
    {
        _logo = logo;
        imgLogoThumb.Source = logo;
        txtNoLogo.Visibility = logo == null ? Visibility.Visible : Visibility.Collapsed;
        if (logo != null && chkLogoAutoH.IsChecked == true && SelType == CodeType.QrCode)
        {
            _loading = true;
            SelectByTag(cmbEcc, "H");
            _loading = false;
        }
        Changed();
    }

    void SaveStyle()
    {
        var dlg = new SaveFileDialog { Title = "Stili kaydet", Filter = "QRGen stili|*.qrstyle.json|JSON|*.json", FileName = "stil.qrstyle.json" };
        if (dlg.ShowDialog(this) != true) return;
        try { File.WriteAllText(dlg.FileName, JsonSerializer.Serialize(CaptureStyle(), new JsonSerializerOptions { WriteIndented = true })); }
        catch (Exception ex) { ShowError("Stil kaydedilemedi: " + ex.Message); }
    }

    void LoadStyle()
    {
        var dlg = new OpenFileDialog { Title = "Stil yükle", Filter = "QRGen stili|*.json|Tüm dosyalar|*.*" };
        if (dlg.ShowDialog(this) == true) LoadStyleFile(dlg.FileName);
    }

    void LoadStyleFile(string path)
    {
        try
        {
            var p = JsonSerializer.Deserialize<StylePreset>(File.ReadAllText(path)) ?? throw new InvalidDataException();
            ApplyStyle(p);
            Changed();
        }
        catch { ShowError("Stil dosyası okunamadı."); }
    }

    void OnDrop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is not string[] { Length: > 0 } files) return;
        var f = files[0];
        var ext = Path.GetExtension(f).ToLowerInvariant();
        if (ext == ".json") LoadStyleFile(f);
        else if (new[] { ".png", ".jpg", ".jpeg", ".bmp", ".gif", ".tif", ".tiff", ".ico" }.Contains(ext))
        {
            LoadLogo(f);
            tabs.SelectedIndex = 2;
        }
    }

    void OnKey(object sender, KeyEventArgs e)
    {
        bool ctrl = Keyboard.Modifiers.HasFlag(ModifierKeys.Control), shift = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);
        if (ctrl && e.Key == Key.S) { SaveAs(shift ? null : "png"); e.Handled = true; }
        else if (ctrl && e.Key == Key.C && Keyboard.FocusedElement is not TextBox) { CopyImage(); e.Handled = true; }
    }

    void ShowError(string msg) => MessageBox.Show(this, msg, "QRGen", MessageBoxButton.OK, MessageBoxImage.Warning);
}


