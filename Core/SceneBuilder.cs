using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace QRGen.Core;

public sealed class Layer
{
    public required VPath Path;
    public required Paint Paint;
}

/// <summary>Biçimden bağımsız çizim sahnesi. Birim: 1 modül.</summary>
public sealed class Scene
{
    public double Width, Height;
    public Color Background;
    public bool Transparent;
    public List<Layer> Layers = new();
    public BitmapSource? Logo;
    public Rect LogoRect;
    public CodeMatrix Matrix = null!;
}

public static class SceneBuilder
{
    public static Scene Build(CodeOptions o, CodeMatrix m)
    {
        var sc = new Scene { Background = o.Background, Transparent = o.Transparent, Matrix = m };
        double q = Math.Max(0, o.Margin);
        double cw = m.Width;
        double ch = m.Is1D ? Math.Max(4, Math.Round(cw * o.BarHeightRatio)) : m.Height;
        double x0 = q, y0 = q;

        var fg = new Paint
        {
            C1 = o.Fg1, C2 = o.UseGradient ? o.Fg2 : o.Fg1, IsGradient = o.UseGradient, Kind = o.Gradient,
            BX = x0, BY = y0, BW = cw, BH = ch
        };

        // --- Logo alanı ---
        bool useLogo = o.Logo != null && !m.Is1D;
        Rect imgRect = Rect.Empty, bgRect = Rect.Empty;
        double circleR = 0;
        if (useLogo)
        {
            double box = Math.Min(cw, ch) * Math.Clamp(o.LogoPercent, 5, 40) / 100.0;
            double ar = o.Logo!.PixelWidth / (double)Math.Max(1, o.Logo.PixelHeight);
            double iw = ar >= 1 ? box : box * ar, ih = ar >= 1 ? box / ar : box;
            imgRect = new Rect(x0 + (cw - iw) / 2, y0 + (ch - ih) / 2, iw, ih);
            bgRect = imgRect;
            bgRect.Inflate(o.LogoPadding, o.LogoPadding);
            if (o.LogoBg == LogoBackground.Circle)
            {
                circleR = Math.Max(iw, ih) / 2 + o.LogoPadding;
                bgRect = new Rect(x0 + cw / 2 - circleR, y0 + ch / 2 - circleR, 2 * circleR, 2 * circleR);
            }
        }

        // --- Hangi modüller çizilecek ---
        int n = m.Width, mh = m.Height;
        var eye = new bool[n, mh];
        var eyes = new List<(int x, int y)>();
        if (m.IsQr)
        {
            eyes.Add((0, 0)); eyes.Add((n - 7, 0)); eyes.Add((0, mh - 7));
            foreach (var (ex, ey) in eyes)
                for (int y = 0; y < 7; y++)
                    for (int x = 0; x < 7; x++) eye[ex + x, ey + y] = true;
        }

        var on = new bool[n, mh];
        for (int y = 0; y < mh; y++)
            for (int x = 0; x < n; x++)
            {
                if (!m.Dark[x, y] || eye[x, y]) continue;
                if (useLogo && o.LogoClear && Covered(x0 + x, y0 + y, imgRect, bgRect, o.LogoBg, circleR)) continue;
                on[x, y] = true;
            }

        // --- Veri modülleri ---
        var data = new VPath();
        if (m.Is1D)
        {
            for (int x = 0; x < n; x++)
            {
                if (!on[x, 0]) continue;
                int s = x;
                while (x + 1 < n && on[x + 1, 0]) x++;
                data.AddRect(x0 + s, y0, x - s + 1, ch);
            }
        }
        else AddModules(data, on, x0, y0, o.ModuleStyle, Math.Clamp(o.ModuleScale, 0.3, 1.0));
        sc.Layers.Add(new Layer { Path = data, Paint = fg });

        // --- Göz (konum belirleme desenleri) ---
        if (m.IsQr)
        {
            var frame = new VPath();
            var ball = new VPath();
            for (int i = 0; i < eyes.Count; i++)
            {
                double ex = x0 + eyes[i].x, ey = y0 + eyes[i].y;
                bool mirror = i != 0;
                var (fo, fi) = o.EyeFrame switch
                {
                    EyeFrameStyle.Rounded => (2.0, 1.2),
                    EyeFrameStyle.ExtraRounded => (3.0, 2.0),
                    EyeFrameStyle.Circle => (3.5, 2.5),
                    EyeFrameStyle.Leaf => (3.0, 2.0),
                    _ => (0.0, 0.0)
                };
                if (o.EyeFrame == EyeFrameStyle.Leaf)
                {
                    AddLeaf(frame, ex, ey, 7, fo, mirror, false);
                    AddLeaf(frame, ex + 1, ey + 1, 5, fi, mirror, true);
                }
                else
                {
                    frame.AddRoundRect(ex, ey, 7, 7, fo, fo, fo, fo);
                    frame.AddRoundRect(ex + 1, ey + 1, 5, 5, fi, fi, fi, fi, reverse: true);
                }

                double bx = ex + 2, by = ey + 2;
                switch (o.EyeBall)
                {
                    case EyeBallStyle.Rounded: ball.AddRoundRect(bx, by, 3, 3, 0.9, 0.9, 0.9, 0.9); break;
                    case EyeBallStyle.Circle: ball.AddCircle(bx + 1.5, by + 1.5, 1.5); break;
                    case EyeBallStyle.Diamond:
                        ball.AddPolygon(new[] { new Pt(bx + 1.5, by - 0.1), new Pt(bx + 3.1, by + 1.5), new Pt(bx + 1.5, by + 3.1), new Pt(bx - 0.1, by + 1.5) });
                        break;
                    case EyeBallStyle.Leaf: AddLeaf(ball, bx, by, 3, 1.3, mirror, false); break;
                    default: ball.AddRect(bx, by, 3, 3); break;
                }
            }
            sc.Layers.Add(new Layer { Path = frame, Paint = o.CustomEyes ? Paint.Solid(o.EyeFrameColor) : fg });
            sc.Layers.Add(new Layer { Path = ball, Paint = o.CustomEyes ? Paint.Solid(o.EyeBallColor) : fg });
        }

        // --- Metin ---
        double ty = y0 + ch;
        double textW = cw;
        var texts = new List<string>();
        if (m.Is1D && o.ShowText && !string.IsNullOrEmpty(m.HumanText)) texts.Add(m.HumanText);
        if (!string.IsNullOrWhiteSpace(o.Caption)) texts.AddRange(o.Caption.Replace("\r", "").Split('\n'));
        if (texts.Count > 0)
        {
            var textPath = new VPath();
            double size = Math.Max(0.5, cw * o.FontPercent / 100.0);
            ty += size * 0.25;
            foreach (var line in texts)
            {
                if (line.Length == 0) { ty += size * 1.2; continue; }
                ty += AddText(textPath, line, o.FontFamily, o.FontBold, size, x0, ty, textW);
            }
            sc.Layers.Add(new Layer { Path = textPath, Paint = Paint.Solid(o.TextColor) });
        }

        // --- Logo arka planı ---
        if (useLogo)
        {
            if (o.LogoBg != LogoBackground.None)
            {
                var lp = new VPath();
                switch (o.LogoBg)
                {
                    case LogoBackground.Circle: lp.AddCircle(x0 + cw / 2, y0 + ch / 2, circleR); break;
                    case LogoBackground.Rounded:
                        double r = Math.Min(bgRect.Width, bgRect.Height) * 0.22;
                        lp.AddRoundRect(bgRect.X, bgRect.Y, bgRect.Width, bgRect.Height, r, r, r, r);
                        break;
                    default: lp.AddRect(bgRect.X, bgRect.Y, bgRect.Width, bgRect.Height); break;
                }
                sc.Layers.Add(new Layer { Path = lp, Paint = Paint.Solid(o.Background) });
            }
            sc.Logo = o.Logo;
            sc.LogoRect = imgRect;
        }

        sc.Width = cw + 2 * q;
        sc.Height = ty + q;
        return sc;
    }

    static bool Covered(double cx, double cy, Rect img, Rect bg, LogoBackground shape, double r)
    {
        var cell = new Rect(cx, cy, 1, 1);
        const double eps = 0.05;
        bool Overlap(Rect a) => a.Left < cell.Right - eps && a.Right > cell.Left + eps && a.Top < cell.Bottom - eps && a.Bottom > cell.Top + eps;
        if (Overlap(img)) return true;
        if (shape == LogoBackground.Circle)
        {
            double ccx = bg.X + r, ccy = bg.Y + r;
            double nx = Math.Clamp(ccx, cell.Left, cell.Right), ny = Math.Clamp(ccy, cell.Top, cell.Bottom);
            return (nx - ccx) * (nx - ccx) + (ny - ccy) * (ny - ccy) < (r - eps) * (r - eps);
        }
        return Overlap(bg);
    }

    static void AddLeaf(VPath p, double x, double y, double size, double r, bool mirror, bool reverse)
    {
        if (mirror) p.AddRoundRect(x, y, size, size, 0, r, 0, r, reverse);
        else p.AddRoundRect(x, y, size, size, r, 0, r, 0, reverse);
    }

    static void AddModules(VPath p, bool[,] on, double x0, double y0, ModuleStyle style, double s)
    {
        int n = on.GetLength(0), h = on.GetLength(1);
        bool On(int x, int y) => x >= 0 && y >= 0 && x < n && y < h && on[x, y];
        double inset = (1 - s) / 2;

        switch (style)
        {
            case ModuleStyle.Square:
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < n; x++)
                    {
                        if (!on[x, y]) continue;
                        int st = x;
                        while (x + 1 < n && on[x + 1, y]) x++;
                        p.AddRect(x0 + st, y0 + y, x - st + 1, 1);
                    }
                return;
            case ModuleStyle.VerticalBars:
                for (int x = 0; x < n; x++)
                    for (int y = 0; y < h; y++)
                    {
                        if (!on[x, y]) continue;
                        int st = y;
                        while (y + 1 < h && on[x, y + 1]) y++;
                        double r = s / 2;
                        p.AddRoundRect(x0 + x + inset, y0 + st + inset, s, y - st + s, r, r, r, r);
                    }
                return;
            case ModuleStyle.HorizontalBars:
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < n; x++)
                    {
                        if (!on[x, y]) continue;
                        int st = x;
                        while (x + 1 < n && on[x + 1, y]) x++;
                        double r = s / 2;
                        p.AddRoundRect(x0 + st + inset, y0 + y + inset, x - st + s, s, r, r, r, r);
                    }
                return;
        }

        for (int y = 0; y < h; y++)
            for (int x = 0; x < n; x++)
            {
                if (!on[x, y]) continue;
                double X = x0 + x, Y = y0 + y;
                switch (style)
                {
                    case ModuleStyle.Rounded:
                        double rr = s * 0.35;
                        p.AddRoundRect(X + inset, Y + inset, s, s, rr, rr, rr, rr);
                        break;
                    case ModuleStyle.Dots:
                        p.AddCircle(X + 0.5, Y + 0.5, s / 2);
                        break;
                    case ModuleStyle.Diamond:
                        double d = s / 2 * 1.08;
                        p.AddPolygon(new[] { new Pt(X + 0.5, Y + 0.5 - d), new Pt(X + 0.5 + d, Y + 0.5), new Pt(X + 0.5, Y + 0.5 + d), new Pt(X + 0.5 - d, Y + 0.5) });
                        break;
                    case ModuleStyle.SmallSquares:
                        p.AddRect(X + inset, Y + inset, s, s);
                        break;
                    case ModuleStyle.Star:
                        var pts = new Pt[10];
                        double ro = s / 2 * 1.12, ri = ro * 0.48;
                        for (int i = 0; i < 10; i++)
                        {
                            double a = -Math.PI / 2 + i * Math.PI / 5, rad = i % 2 == 0 ? ro : ri;
                            pts[i] = new Pt(X + 0.5 + rad * Math.Cos(a), Y + 0.55 + rad * Math.Sin(a));
                        }
                        p.AddPolygon(pts);
                        break;
                    case ModuleStyle.Smooth:
                        bool u = On(x, y - 1), dn = On(x, y + 1), l = On(x - 1, y), r = On(x + 1, y);
                        const double R = 0.5;
                        p.AddRoundRect(X, Y, 1, 1, !u && !l ? R : 0, !u && !r ? R : 0, !dn && !r ? R : 0, !dn && !l ? R : 0);
                        break;
                }
            }
    }

    /// <summary>Metni vektör yola dönüştürür, satır yüksekliğini döndürür.</summary>
    static double AddText(VPath p, string text, string family, bool bold, double size, double x, double y, double maxW)
    {
        const double em = 100;
        var tf = new Typeface(new FontFamily(family), FontStyles.Normal, bold ? FontWeights.SemiBold : FontWeights.Normal, FontStretches.Normal);
        var ft = new FormattedText(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, tf, em, Brushes.Black, 1.0);
        double scale = size / em;
        if (ft.WidthIncludingTrailingWhitespace * scale > maxW) scale = maxW / ft.WidthIncludingTrailingWhitespace;
        var geo = ft.BuildGeometry(new Point(0, 0)).GetFlattenedPathGeometry(0.05, ToleranceType.Absolute);
        var tp = new VPath();
        foreach (var fig in geo.Figures)
        {
            var f = new Figure { Start = new Pt(fig.StartPoint.X, fig.StartPoint.Y) };
            foreach (var seg in fig.Segments)
            {
                if (seg is PolyLineSegment pl) foreach (var pt in pl.Points) f.Segs.Add(new LineSeg(new Pt(pt.X, pt.Y)));
                else if (seg is LineSegment ls) f.Segs.Add(new LineSeg(new Pt(ls.Point.X, ls.Point.Y)));
            }
            if (f.Segs.Count > 0) tp.Figures.Add(f);
        }
        double w = ft.WidthIncludingTrailingWhitespace * scale;
        tp.Transform(scale, x + (maxW - w) / 2, y);
        p.Figures.AddRange(tp.Figures);
        return ft.Height * scale;
    }
}

