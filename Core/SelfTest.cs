using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using QRGen.Export;

namespace QRGen.Core;

/// <summary>Komut satırı öz testi: QRGen.exe --selftest &lt;klasör&gt;</summary>
public static class SelfTest
{
    public static int Run(string dir)
    {
        Directory.CreateDirectory(dir);
        var log = new StringBuilder();
        int fail = 0;

        void Check(string name, CodeOptions o, bool export = false)
        {
            try
            {
                var m = MatrixEncoder.Encode(o);
                var s = SceneBuilder.Build(o, m);
                var (px, w, h) = Verifier.Snapshot(s);
                var text = Verifier.Decode(px, w, h, o.Type);
                bool ok = !Verifier.Supported(o.Type) || text != null &&(text == o.Content || (CodeTypeInfo.Is1D(o.Type) && text.StartsWith(o.Content)));
                if (!ok) fail++;
                log.AppendLine($"{(ok ? "OK  " : "FAIL")} {name}: {m.Info} -> {text}");
                WpfRenderer.Save(s, Path.Combine(dir, name + ".png"), RasterFormat.Png, 512, true, 96, 90);
                if (export)
                {
                    var (pw, ph, _) = WpfRenderer.PixelSize(s, 512, true);
                    File.WriteAllText(Path.Combine(dir, name + ".svg"), SvgExporter.Build(s, pw, ph));
                    PdfExporter.Save(s, Path.Combine(dir, name + ".pdf"), pw, ph, 300);
                    EpsExporter.Save(s, Path.Combine(dir, name + ".eps"), pw, ph, 300);
                    foreach (var f in new[] { RasterFormat.Jpeg, RasterFormat.Bmp, RasterFormat.Tiff, RasterFormat.Gif })
                        WpfRenderer.Save(s, Path.Combine(dir, $"{name}.{f.ToString().ToLower()}"), f, 512, true, 300, 90);
                }
            }
            catch (Exception ex) { fail++; log.AppendLine($"ERR  {name}: {ex.Message}"); }
        }

        foreach (CodeType t in Enum.GetValues<CodeType>())
            Check("type_" + t, new CodeOptions { Type = t, Content = CodeTypeInfo.Is1D(t) ? CodeTypeInfo.Sample(t) : "QRGen test ğüşıöç 123", Margin = CodeTypeInfo.Is1D(t) ? 10 : 4 });

        foreach (ModuleStyle ms in Enum.GetValues<ModuleStyle>())
            Check("module_" + ms, new CodeOptions { Content = "https://example.com/" + ms, ModuleStyle = ms, Ecc = 'Q' });
        foreach (EyeFrameStyle ef in Enum.GetValues<EyeFrameStyle>())
            Check("eye_" + ef, new CodeOptions { Content = "eye", EyeFrame = ef, EyeBall = (EyeBallStyle)((int)ef % 5), CustomEyes = true, EyeFrameColor = Colors.DarkRed, EyeBallColor = Colors.OrangeRed });
        foreach (GradientKind g in Enum.GetValues<GradientKind>())
            Check("grad_" + g, new CodeOptions { Content = "gradient", UseGradient = true, Gradient = g, Fg1 = Color.FromRgb(0x0E, 0x3A, 0x8A), Fg2 = Color.FromRgb(0x7C, 0x3A, 0xED) }, export: g == GradientKind.Radial);

        foreach (var ecc in "LMQH")
            Check("ecc_" + ecc, new CodeOptions { Content = "ECC " + ecc, Ecc = ecc });
        Check("version10_mask3", new CodeOptions { Content = "v10", QrVersion = 10, QrMask = 3 });
        Check("caption", new CodeOptions { Content = "caption", Caption = "Beni tara · Şık QR", ModuleStyle = ModuleStyle.Smooth }, export: true);
        Check("transparent", new CodeOptions { Content = "transparent", Transparent = true }, export: true);
        Check("ean13_text", new CodeOptions { Type = CodeType.Ean13, Content = "869000000001", Margin = 10, Caption = "Ürün" }, export: true);

        // Logo testi
        var logo = MakeLogo();
        foreach (LogoBackground lb in Enum.GetValues<LogoBackground>())
            Check("logo_" + lb, new CodeOptions { Content = "https://example.com/logo", Ecc = 'H', Logo = logo, LogoBg = lb, ModuleStyle = ModuleStyle.Dots, EyeFrame = EyeFrameStyle.Rounded, EyeBall = EyeBallStyle.Circle }, export: lb == LogoBackground.Circle);

        log.AppendLine($"Toplam hata: {fail}");
        File.WriteAllText(Path.Combine(dir, "selftest.log"), log.ToString());
        return fail;
    }

    /// <summary>Arayüz ekran görüntüsü: QRGen.exe --shot dosya.png [Light|Dark] [sekme]</summary>
    public static async void Screenshot(Window win, string path, string? theme, int tab)
    {
        if (theme != null) Application.Current.ThemeMode = theme == "Dark" ? ThemeMode.Dark : ThemeMode.Light;
        if (win.FindName("tabs") is System.Windows.Controls.TabControl tc) tc.SelectedIndex = tab;
        await Task.Delay(1500);
        var content = (FrameworkElement)win.Content;
        var rtb = new RenderTargetBitmap((int)content.ActualWidth, (int)content.ActualHeight, 96, 96, PixelFormats.Pbgra32);
        var dv = new DrawingVisual();
        using (var dc = dv.RenderOpen())
        {
            var bg = win.Background is SolidColorBrush { Color.A: > 0 } b ? b : (theme == "Dark" ? new SolidColorBrush(Color.FromRgb(32, 32, 32)) : new SolidColorBrush(Color.FromRgb(243, 243, 243)));
            dc.DrawRectangle(bg, null, new Rect(0, 0, content.ActualWidth, content.ActualHeight));
            dc.DrawRectangle(new VisualBrush(content), null, new Rect(0, 0, content.ActualWidth, content.ActualHeight));
        }
        rtb.Render(dv);
        File.WriteAllBytes(path, WpfRenderer.EncodePng(rtb));
        win.Close();
    }

    static BitmapSource MakeLogo()
    {
        var dv = new DrawingVisual();
        using (var dc = dv.RenderOpen())
        {
            dc.DrawEllipse(new SolidColorBrush(Color.FromRgb(0xE1, 0x1D, 0x48)), null, new Point(64, 64), 60, 60);
            dc.DrawRectangle(Brushes.White, null, new Rect(40, 40, 48, 48));
        }
        var rtb = new RenderTargetBitmap(128, 128, 96, 96, PixelFormats.Pbgra32);
        rtb.Render(dv);
        rtb.Freeze();
        return rtb;
    }
}
