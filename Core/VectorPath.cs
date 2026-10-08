namespace QRGen.Core;

public readonly record struct Pt(double X, double Y);

public abstract record Seg(Pt P);
public sealed record LineSeg(Pt P) : Seg(P);
public sealed record CubicSeg(Pt C1, Pt C2, Pt P) : Seg(P);

public sealed class Figure
{
    public Pt Start;
    public List<Seg> Segs = new();

    public Figure Reversed()
    {
        var pts = new List<Pt> { Start };
        foreach (var s in Segs) pts.Add(s.P);
        var f = new Figure { Start = pts[^1] };
        for (int i = Segs.Count - 1; i >= 0; i--)
        {
            var to = pts[i];
            f.Segs.Add(Segs[i] switch
            {
                CubicSeg c => new CubicSeg(c.C2, c.C1, to),
                _ => new LineSeg(to)
            });
        }
        return f;
    }
}

/// <summary>
/// Tüm çıktı biçimleri (WPF, SVG, PDF, EPS) için ortak vektör yol modeli.
/// Varsayılan dolgu kuralı sıfır olmayan (nonzero); delikler ters yönde çizilir.
/// </summary>
public sealed class VPath
{
    const double K = 0.5522847498;
    public List<Figure> Figures = new();
    public bool EvenOdd;

    public bool IsEmpty => Figures.Count == 0;

    public void Add(Figure f, bool reverse = false) => Figures.Add(reverse ? f.Reversed() : f);

    public void AddRect(double x, double y, double w, double h, bool reverse = false) =>
        AddRoundRect(x, y, w, h, 0, 0, 0, 0, reverse);

    public void AddCircle(double cx, double cy, double r, bool reverse = false) =>
        AddRoundRect(cx - r, cy - r, 2 * r, 2 * r, r, r, r, r, reverse);

    public void AddPolygon(IReadOnlyList<Pt> pts, bool reverse = false)
    {
        var f = new Figure { Start = pts[0] };
        for (int i = 1; i < pts.Count; i++) f.Segs.Add(new LineSeg(pts[i]));
        f.Segs.Add(new LineSeg(pts[0]));
        Add(f, reverse);
    }

    /// <summary>Köşe yarıçapları ayrı ayrı verilebilen yuvarlatılmış dikdörtgen (saat yönünde, ekran koordinatları).</summary>
    public void AddRoundRect(double x, double y, double w, double h, double tl, double tr, double br, double bl, bool reverse = false)
    {
        double m = Math.Min(w, h) / 2;
        tl = Math.Clamp(tl, 0, m); tr = Math.Clamp(tr, 0, m); br = Math.Clamp(br, 0, m); bl = Math.Clamp(bl, 0, m);
        var f = new Figure { Start = new Pt(x + tl, y) };
        void Corner(Pt a, Pt corner, Pt b, double r)
        {
            f.Segs.Add(new LineSeg(a));
            if (r > 0)
                f.Segs.Add(new CubicSeg(
                    new Pt(a.X + K * (corner.X - a.X), a.Y + K * (corner.Y - a.Y)),
                    new Pt(b.X + K * (corner.X - b.X), b.Y + K * (corner.Y - b.Y)), b));
        }
        Corner(new Pt(x + w - tr, y), new Pt(x + w, y), new Pt(x + w, y + tr), tr);
        Corner(new Pt(x + w, y + h - br), new Pt(x + w, y + h), new Pt(x + w - br, y + h), br);
        Corner(new Pt(x + bl, y + h), new Pt(x, y + h), new Pt(x, y + h - bl), bl);
        Corner(new Pt(x, y + tl), new Pt(x, y), new Pt(x + tl, y), tl);
        Add(f, reverse);
    }

    /// <summary>Yolu ölçekleyip öteler (metin geometrisi için).</summary>
    public void Transform(double scale, double dx, double dy)
    {
        Pt T(Pt p) => new(p.X * scale + dx, p.Y * scale + dy);
        foreach (var f in Figures)
        {
            f.Start = T(f.Start);
            for (int i = 0; i < f.Segs.Count; i++)
                f.Segs[i] = f.Segs[i] switch
                {
                    CubicSeg c => new CubicSeg(T(c.C1), T(c.C2), T(c.P)),
                    var s => new LineSeg(T(s.P))
                };
        }
    }
}
