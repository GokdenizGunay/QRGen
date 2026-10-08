using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using QRGen.Core;

namespace QRGen.Export;

public enum RasterFormat { Png, Jpeg, Bmp, Tiff, Gif }

public static class WpfRenderer
{
    public static Geometry ToGeometry(VPath p)
    {
        var g = new StreamGeometry { FillRule = p.EvenOdd ? FillRule.EvenOdd : FillRule.Nonzero };
        using (var c = g.Open())
        {
            foreach (var f in p.Figures)
            {
                c.BeginFigure(new Point(f.Start.X, f.Start.Y), true, true);
                foreach (var s in f.Segs)
                {
                    if (s is CubicSeg cs)
                        c.BezierTo(new Point(cs.C1.X, cs.C1.Y), new Point(cs.C2.X, cs.C2.Y), new Point(cs.P.X, cs.P.Y), false, false);
                    else
                        c.LineTo(new Point(s.P.X, s.P.Y), false, false);
                }
            }
        }
        g.Freeze();
        return g;
    }

    public static Brush ToBrush(Paint p)
    {
        Brush b;
        if (!p.IsGradient) b = new SolidColorBrush(p.C1);
        else
        {
            var (a, e) = p.Line();
            if (p.Kind == GradientKind.Radial)
            {
                double r = e.X - a.X;
                b = new RadialGradientBrush(p.C1, p.C2)
                {
                    MappingMode = BrushMappingMode.Absolute,
                    Center = new Point(a.X, a.Y), GradientOrigin = new Point(a.X, a.Y),
                    RadiusX = r, RadiusY = r
                };
            }
            else
            {
                b = new LinearGradientBrush(p.C1, p.C2, new Point(a.X, a.Y), new Point(e.X, e.Y))
                { MappingMode = BrushMappingMode.Absolute };
            }
        }
        b.Freeze();
        return b;
    }

    /// <summary>Sahneyi vektörel WPF çizimine dönüştürür (önizleme ve raster çıktı için).</summary>
    public static DrawingGroup ToDrawing(Scene s, Color? forceBackground = null)
    {
        var g = new DrawingGroup();
        using (var dc = g.Open())
        {
            var bounds = new Rect(0, 0, s.Width, s.Height);
            if (forceBackground is Color fb) dc.DrawRectangle(new SolidColorBrush(fb), null, bounds);
            if (!s.Transparent) dc.DrawRectangle(new SolidColorBrush(s.Background), null, bounds);
            else dc.DrawRectangle(Brushes.Transparent, null, bounds);
            foreach (var l in s.Layers)
                if (!l.Path.IsEmpty) dc.DrawGeometry(ToBrush(l.Paint), null, ToGeometry(l.Path));
            if (s.Logo != null) dc.DrawImage(s.Logo, s.LogoRect);
        }
        g.ClipGeometry = new RectangleGeometry(new Rect(0, 0, s.Width, s.Height));
        g.Freeze();
        return g;
    }

    /// <summary>Çıktı piksel boyutunu hesaplar. snap: modül başına tam sayı piksel.</summary>
    public static (int w, int h, double scale) PixelSize(Scene s, int targetWidth, bool snap)
    {
        double scale = targetWidth / s.Width;
        if (snap) scale = Math.Max(1, Math.Round(scale));
        int w = Math.Max(1, (int)Math.Round(s.Width * scale));
        int h = Math.Max(1, (int)Math.Round(s.Height * scale));
        return (w, h, scale);
    }

    public static BitmapSource Render(Scene s, int targetWidth, bool snap, double dpi, Color? opaqueOn = null)
    {
        var (w, h, scale) = PixelSize(s, targetWidth, snap);
        var drawing = ToDrawing(s, opaqueOn);
        var dv = new DrawingVisual();
        RenderOptions.SetBitmapScalingMode(dv, BitmapScalingMode.HighQuality);
        using (var dc = dv.RenderOpen())
        {
            dc.PushTransform(new ScaleTransform(scale, scale));
            dc.DrawDrawing(drawing);
        }
        var rtb = new RenderTargetBitmap(w, h, 96, 96, PixelFormats.Pbgra32);
        rtb.Render(dv);
        if (Math.Abs(dpi - 96) < 0.01) { rtb.Freeze(); return rtb; }
        int stride = w * 4;
        var px = new byte[stride * h];
        rtb.CopyPixels(px, stride, 0);
        var bs = BitmapSource.Create(w, h, dpi, dpi, PixelFormats.Pbgra32, null, px, stride);
        bs.Freeze();
        return bs;
    }

    public static void Save(Scene s, string path, RasterFormat fmt, int targetWidth, bool snap, double dpi, int jpegQuality)
    {
        bool needsOpaque = fmt is RasterFormat.Jpeg or RasterFormat.Bmp;
        Color? under = needsOpaque && s.Transparent ? Colors.White : null;
        var bmp = Render(s, targetWidth, snap, dpi, under);
        BitmapEncoder enc = fmt switch
        {
            RasterFormat.Jpeg => new JpegBitmapEncoder { QualityLevel = Math.Clamp(jpegQuality, 1, 100) },
            RasterFormat.Bmp => new BmpBitmapEncoder(),
            RasterFormat.Tiff => new TiffBitmapEncoder { Compression = TiffCompressOption.Lzw },
            RasterFormat.Gif => new GifBitmapEncoder(),
            _ => new PngBitmapEncoder()
        };
        BitmapSource frame = bmp;
        if (needsOpaque) frame = new FormatConvertedBitmap(bmp, PixelFormats.Bgr24, null, 0);
        enc.Frames.Add(BitmapFrame.Create(frame));
        using var fs = File.Create(path);
        enc.Save(fs);
    }

    public static byte[] EncodePng(BitmapSource src)
    {
        var enc = new PngBitmapEncoder();
        enc.Frames.Add(BitmapFrame.Create(src));
        using var ms = new MemoryStream();
        enc.Save(ms);
        return ms.ToArray();
    }

    /// <summary>Logoyu sınırlı boyutta BGRA piksellere çevirir.</summary>
    public static (byte[] bgra, int w, int h) LogoPixels(BitmapSource src, int maxSide)
    {
        BitmapSource b = src;
        double k = Math.Min(1.0, maxSide / (double)Math.Max(src.PixelWidth, src.PixelHeight));
        if (k < 1) b = new TransformedBitmap(src, new ScaleTransform(k, k));
        var conv = new FormatConvertedBitmap(b, PixelFormats.Bgra32, null, 0);
        int w = conv.PixelWidth, h = conv.PixelHeight;
        var px = new byte[w * h * 4];
        conv.CopyPixels(px, w * 4, 0);
        return (px, w, h);
    }
}
