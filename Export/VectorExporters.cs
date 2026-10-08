using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Windows.Media;
using QRGen.Core;

namespace QRGen.Export;

static class Num
{
    public static string F(double v) => Math.Round(v, 4).ToString("0.####", CultureInfo.InvariantCulture);
    public static string Hex(Color c) => $"#{c.R:X2}{c.G:X2}{c.B:X2}";
    public static string Rgb(Color c) => $"{F(c.R / 255.0)} {F(c.G / 255.0)} {F(c.B / 255.0)}";
}

public static class SvgExporter
{
    public static string Build(Scene s, int pxWidth, int pxHeight)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
        sb.AppendLine($"<svg xmlns=\"http://www.w3.org/2000/svg\" xmlns:xlink=\"http://www.w3.org/1999/xlink\" version=\"1.1\" " +
                      $"width=\"{pxWidth}\" height=\"{pxHeight}\" viewBox=\"0 0 {Num.F(s.Width)} {Num.F(s.Height)}\">");
        sb.AppendLine("  <!-- QRGen ile oluşturuldu -->");

        var defs = new StringBuilder();
        var body = new StringBuilder();
        if (!s.Transparent)
            body.AppendLine($"  <rect width=\"{Num.F(s.Width)}\" height=\"{Num.F(s.Height)}\" fill=\"{Num.Hex(s.Background)}\"/>");

        int gi = 0;
        foreach (var l in s.Layers)
        {
            if (l.Path.IsEmpty) continue;
            string fill;
            if (l.Paint.IsGradient)
            {
                string id = $"g{gi++}";
                var (a, b) = l.Paint.Line();
                if (l.Paint.Kind == GradientKind.Radial)
                    defs.AppendLine($"    <radialGradient id=\"{id}\" gradientUnits=\"userSpaceOnUse\" cx=\"{Num.F(a.X)}\" cy=\"{Num.F(a.Y)}\" r=\"{Num.F(b.X - a.X)}\">");
                else
                    defs.AppendLine($"    <linearGradient id=\"{id}\" gradientUnits=\"userSpaceOnUse\" x1=\"{Num.F(a.X)}\" y1=\"{Num.F(a.Y)}\" x2=\"{Num.F(b.X)}\" y2=\"{Num.F(b.Y)}\">");
                defs.AppendLine($"      <stop offset=\"0\" stop-color=\"{Num.Hex(l.Paint.C1)}\"/>");
                defs.AppendLine($"      <stop offset=\"1\" stop-color=\"{Num.Hex(l.Paint.C2)}\"/>");
                defs.AppendLine(l.Paint.Kind == GradientKind.Radial ? "    </radialGradient>" : "    </linearGradient>");
                fill = $"url(#{id})";
            }
            else fill = Num.Hex(l.Paint.C1);
            body.AppendLine($"  <path fill=\"{fill}\" fill-rule=\"{(l.Path.EvenOdd ? "evenodd" : "nonzero")}\" d=\"{PathData(l.Path)}\"/>");
        }

        if (s.Logo != null)
        {
            var png = Convert.ToBase64String(WpfRenderer.EncodePng(s.Logo));
            var r = s.LogoRect;
            body.AppendLine($"  <image x=\"{Num.F(r.X)}\" y=\"{Num.F(r.Y)}\" width=\"{Num.F(r.Width)}\" height=\"{Num.F(r.Height)}\" " +
                            $"preserveAspectRatio=\"none\" xlink:href=\"data:image/png;base64,{png}\" href=\"data:image/png;base64,{png}\"/>");
        }

        if (defs.Length > 0) sb.Append("  <defs>\n").Append(defs).Append("  </defs>\n");
        sb.Append(body);
        sb.AppendLine("</svg>");
        return sb.ToString();
    }

    static string PathData(VPath p)
    {
        var sb = new StringBuilder();
        foreach (var f in p.Figures)
        {
            sb.Append('M').Append(Num.F(f.Start.X)).Append(' ').Append(Num.F(f.Start.Y));
            foreach (var s in f.Segs)
            {
                if (s is CubicSeg c)
                    sb.Append('C').Append(Num.F(c.C1.X)).Append(' ').Append(Num.F(c.C1.Y)).Append(' ')
                      .Append(Num.F(c.C2.X)).Append(' ').Append(Num.F(c.C2.Y)).Append(' ')
                      .Append(Num.F(c.P.X)).Append(' ').Append(Num.F(c.P.Y));
                else
                    sb.Append('L').Append(Num.F(s.P.X)).Append(' ').Append(Num.F(s.P.Y));
            }
            sb.Append('Z');
        }
        return sb.ToString();
    }
}

/// <summary>Sahne koordinatlarını (y aşağı, modül) sayfa noktalarına (y yukarı, pt) çevirir.</summary>
sealed class PageXf(double scale, double pageH)
{
    public Pt T(Pt p) => new(p.X * scale, pageH - p.Y * scale);
    public double S(double v) => v * scale;

    public void WritePath(StringBuilder sb, VPath p, string m, string l, string c, string h)
    {
        foreach (var f in p.Figures)
        {
            var st = T(f.Start);
            sb.Append($"{Num.F(st.X)} {Num.F(st.Y)} {m}\n");
            foreach (var s in f.Segs)
            {
                if (s is CubicSeg cs)
                {
                    Pt a = T(cs.C1), b = T(cs.C2), e = T(cs.P);
                    sb.Append($"{Num.F(a.X)} {Num.F(a.Y)} {Num.F(b.X)} {Num.F(b.Y)} {Num.F(e.X)} {Num.F(e.Y)} {c}\n");
                }
                else
                {
                    var e = T(s.P);
                    sb.Append($"{Num.F(e.X)} {Num.F(e.Y)} {l}\n");
                }
            }
            sb.Append(h).Append('\n');
        }
    }

    public string ShadingDict(Paint p, string extra = "")
    {
        var (a, b) = p.Line();
        Pt ta = T(a), tb = T(b);
        string fn = $"<< /FunctionType 2 /Domain [0 1] /C0 [{Num.Rgb(p.C1)}] /C1 [{Num.Rgb(p.C2)}] /N 1 >>";
        string coords = p.Kind == GradientKind.Radial
            ? $"/ShadingType 3 /Coords [{Num.F(ta.X)} {Num.F(ta.Y)} 0 {Num.F(ta.X)} {Num.F(ta.Y)} {Num.F(S(b.X - a.X))}]"
            : $"/ShadingType 2 /Coords [{Num.F(ta.X)} {Num.F(ta.Y)} {Num.F(tb.X)} {Num.F(tb.Y)}]";
        return $"<< {coords} /ColorSpace /DeviceRGB /Function {fn} /Extend [true true]{extra} >>";
    }
}

public static class PdfExporter
{
    public static void Save(Scene s, string path, int pxW, int pxH, double dpi)
    {
        double pw = pxW / dpi * 72, ph = pxH / dpi * 72;
        var xf = new PageXf(pw / s.Width, ph);
        var objs = new List<byte[]>();
        int Reserve() { objs.Add(Array.Empty<byte>()); return objs.Count; }
        void Set(int id, string str) => objs[id - 1] = Encoding.ASCII.GetBytes(str);
        void SetStream(int id, string dict, byte[] data)
        {
            var head = Encoding.ASCII.GetBytes($"<< {dict} /Length {data.Length} >>\nstream\n");
            var tail = Encoding.ASCII.GetBytes("\nendstream");
            objs[id - 1] = [.. head, .. data, .. tail];
        }

        int catalog = Reserve(), pages = Reserve(), page = Reserve(), content = Reserve();
        var patterns = new StringBuilder();
        var cs = new StringBuilder();
        if (!s.Transparent)
            cs.Append($"{Num.Rgb(s.Background)} rg 0 0 {Num.F(pw)} {Num.F(ph)} re f\n");

        int pi = 0;
        foreach (var l in s.Layers)
        {
            if (l.Path.IsEmpty) continue;
            if (l.Paint.IsGradient)
            {
                int pid = Reserve();
                Set(pid, $"<< /Type /Pattern /PatternType 2 /Shading {xf.ShadingDict(l.Paint)} >>");
                patterns.Append($"/P{pi} {pid} 0 R ");
                cs.Append($"/Pattern cs /P{pi} scn\n");
                pi++;
            }
            else cs.Append($"{Num.Rgb(l.Paint.C1)} rg\n");
            xf.WritePath(cs, l.Path, "m", "l", "c", "h");
            cs.Append(l.Path.EvenOdd ? "f*\n" : "f\n");
        }

        string xobj = "";
        if (s.Logo != null)
        {
            var (px, w, h) = WpfRenderer.LogoPixels(s.Logo, 1024);
            var rgb = new byte[w * h * 3];
            var alpha = new byte[w * h];
            for (int i = 0; i < w * h; i++)
            {
                rgb[i * 3] = px[i * 4 + 2]; rgb[i * 3 + 1] = px[i * 4 + 1]; rgb[i * 3 + 2] = px[i * 4];
                alpha[i] = px[i * 4 + 3];
            }
            int smask = Reserve(), img = Reserve();
            SetStream(smask, $"/Type /XObject /Subtype /Image /Width {w} /Height {h} /ColorSpace /DeviceGray /BitsPerComponent 8 /Filter /FlateDecode", Deflate(alpha));
            SetStream(img, $"/Type /XObject /Subtype /Image /Width {w} /Height {h} /ColorSpace /DeviceRGB /BitsPerComponent 8 /Filter /FlateDecode /SMask {smask} 0 R", Deflate(rgb));
            var r = s.LogoRect;
            var bl = xf.T(new Pt(r.X, r.Bottom));
            cs.Append($"q {Num.F(xf.S(r.Width))} 0 0 {Num.F(xf.S(r.Height))} {Num.F(bl.X)} {Num.F(bl.Y)} cm /Im0 Do Q\n");
            xobj = $"/XObject << /Im0 {img} 0 R >>";
        }

        SetStream(content, "/Filter /FlateDecode", Deflate(Encoding.ASCII.GetBytes(cs.ToString())));
        Set(catalog, $"<< /Type /Catalog /Pages {pages} 0 R >>");
        Set(pages, $"<< /Type /Pages /Kids [{page} 0 R] /Count 1 >>");
        string res = $"<< {(patterns.Length > 0 ? $"/Pattern << {patterns}>> " : "")}{xobj} >>";
        Set(page, $"<< /Type /Page /Parent {pages} 0 R /MediaBox [0 0 {Num.F(pw)} {Num.F(ph)}] /Resources {res} /Contents {content} 0 R >>");
        int info = Reserve();
        Set(info, $"<< /Producer (QRGen) /Creator (QRGen) /CreationDate (D:{DateTime.Now:yyyyMMddHHmmss}) >>");

        using var fs = File.Create(path);
        void W(string t) { var b = Encoding.ASCII.GetBytes(t); fs.Write(b); }
        W("%PDF-1.4\n%");
        fs.Write([0xE2, 0xE3, 0xCF, 0xD3, (byte)'\n']);
        var offsets = new long[objs.Count];
        for (int i = 0; i < objs.Count; i++)
        {
            offsets[i] = fs.Position;
            W($"{i + 1} 0 obj\n");
            fs.Write(objs[i]);
            W("\nendobj\n");
        }
        long xref = fs.Position;
        W($"xref\n0 {objs.Count + 1}\n0000000000 65535 f \n");
        foreach (var o in offsets) W($"{o:D10} 00000 n \n");
        W($"trailer\n<< /Size {objs.Count + 1} /Root {catalog} 0 R /Info {info} 0 R >>\nstartxref\n{xref}\n%%EOF\n");
    }

    static byte[] Deflate(byte[] data)
    {
        using var ms = new MemoryStream();
        using (var z = new ZLibStream(ms, CompressionLevel.Optimal, true)) z.Write(data);
        return ms.ToArray();
    }
}

public static class EpsExporter
{
    public static void Save(Scene s, string path, int pxW, int pxH, double dpi)
    {
        double pw = pxW / dpi * 72, ph = pxH / dpi * 72;
        var xf = new PageXf(pw / s.Width, ph);
        var sb = new StringBuilder();
        sb.Append("%!PS-Adobe-3.0 EPSF-3.0\n");
        sb.Append($"%%BoundingBox: 0 0 {(int)Math.Ceiling(pw)} {(int)Math.Ceiling(ph)}\n");
        sb.Append($"%%HiResBoundingBox: 0 0 {Num.F(pw)} {Num.F(ph)}\n");
        sb.Append("%%Creator: QRGen\n%%LanguageLevel: 3\n%%Pages: 1\n%%EndComments\n");
        sb.Append("save\n/m {moveto} bind def /l {lineto} bind def /c {curveto} bind def /h {closepath} bind def\n");
        if (!s.Transparent)
            sb.Append($"{Num.Rgb(s.Background)} setrgbcolor 0 0 {Num.F(pw)} {Num.F(ph)} rectfill\n");

        foreach (var l in s.Layers)
        {
            if (l.Path.IsEmpty) continue;
            sb.Append("newpath\n");
            xf.WritePath(sb, l.Path, "m", "l", "c", "h");
            if (l.Paint.IsGradient)
                sb.Append($"gsave {(l.Path.EvenOdd ? "eoclip" : "clip")} newpath {xf.ShadingDict(l.Paint)} shfill grestore\n");
            else
                sb.Append($"{Num.Rgb(l.Paint.C1)} setrgbcolor {(l.Path.EvenOdd ? "eofill" : "fill")}\n");
        }

        if (s.Logo != null)
        {
            // EPS alfa kanalını desteklemez: logo arka plan rengi üzerine birleştirilir.
            var (px, w, h) = WpfRenderer.LogoPixels(s.Logo, 512);
            var bg = s.Background;
            var r = s.LogoRect;
            var bl = xf.T(new Pt(r.X, r.Bottom));
            sb.Append($"gsave {Num.F(bl.X)} {Num.F(bl.Y)} translate {Num.F(xf.S(r.Width))} {Num.F(xf.S(r.Height))} scale\n");
            sb.Append($"{w} {h} 8 [{w} 0 0 -{h} 0 {h}] currentfile /ASCIIHexDecode filter false 3 colorimage\n");
            for (int i = 0; i < w * h; i++)
            {
                double a = px[i * 4 + 3] / 255.0;
                int R = (int)Math.Round(px[i * 4 + 2] * a + bg.R * (1 - a));
                int G = (int)Math.Round(px[i * 4 + 1] * a + bg.G * (1 - a));
                int B = (int)Math.Round(px[i * 4] * a + bg.B * (1 - a));
                sb.Append($"{R:X2}{G:X2}{B:X2}");
                if ((i + 1) % 24 == 0) sb.Append('\n');
            }
            sb.Append(">\ngrestore\n");
        }
        sb.Append("restore\nshowpage\n%%EOF\n");
        File.WriteAllText(path, sb.ToString(), Encoding.ASCII);
    }
}
