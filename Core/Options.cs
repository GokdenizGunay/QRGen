using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace QRGen.Core;

public enum CodeType
{
    QrCode, DataMatrix, Aztec, Pdf417,
    Code128, Code39, Code93, Ean13, Ean8, UpcA, UpcE, Itf, Codabar, Msi, Plessey
}

public enum ModuleStyle { Square, Rounded, Dots, Diamond, Smooth, SmallSquares, VerticalBars, HorizontalBars, Star }
public enum EyeFrameStyle { Square, Rounded, ExtraRounded, Circle, Leaf }
public enum EyeBallStyle { Square, Rounded, Circle, Diamond, Leaf }
public enum GradientKind { Horizontal, Vertical, Diagonal, DiagonalReverse, Radial }
public enum LogoBackground { None, Square, Rounded, Circle }

public static class CodeTypeInfo
{
    public static bool Is1D(CodeType t) => t >= CodeType.Code128;

    public static string Display(CodeType t) => t switch
    {
        CodeType.QrCode => "QR Kod",
        CodeType.DataMatrix => "Data Matrix",
        CodeType.Aztec => "Aztec",
        CodeType.Pdf417 => "PDF417",
        CodeType.Code128 => "Code 128",
        CodeType.Code39 => "Code 39",
        CodeType.Code93 => "Code 93",
        CodeType.Ean13 => "EAN-13",
        CodeType.Ean8 => "EAN-8",
        CodeType.UpcA => "UPC-A",
        CodeType.UpcE => "UPC-E",
        CodeType.Itf => "ITF (Interleaved 2 of 5)",
        CodeType.Codabar => "Codabar",
        CodeType.Msi => "MSI",
        CodeType.Plessey => "Plessey",
        _ => t.ToString()
    };

    public static string Sample(CodeType t) => t switch
    {
        CodeType.Ean13 => "869000000001",
        CodeType.Ean8 => "1234567",
        CodeType.UpcA => "03600029145",
        CodeType.UpcE => "0123456",
        CodeType.Itf => "12345678901231",
        CodeType.Codabar => "A123456B",
        CodeType.Msi => "1234567",
        CodeType.Plessey => "12345ABCDEF",
        CodeType.Code39 or CodeType.Code93 => "QRGEN-12345",
        _ => "https://example.com"
    };

    public static string Hint(CodeType t) => t switch
    {
        CodeType.Ean13 => "12 rakam (kontrol hanesi otomatik) veya 13 rakam.",
        CodeType.Ean8 => "7 rakam (kontrol hanesi otomatik) veya 8 rakam.",
        CodeType.UpcA => "11 rakam (kontrol hanesi otomatik) veya 12 rakam.",
        CodeType.UpcE => "0 veya 1 ile başlayan 7 ya da 8 rakam.",
        CodeType.Itf => "Çift sayıda rakam.",
        CodeType.Codabar => "Rakamlar ve - $ : / . + ; başlangıç/bitiş A-D.",
        CodeType.Msi => "Yalnızca rakamlar.",
        CodeType.Plessey => "Rakamlar ve A-F.",
        CodeType.Code39 => "Büyük harf A-Z, rakamlar ve - . $ / + % boşluk.",
        CodeType.Code93 => "Büyük harf, rakam ve bazı semboller.",
        CodeType.Code128 => "Tüm ASCII karakterleri.",
        CodeType.Pdf417 => "Metin veya ikili veri; yüksek kapasite.",
        CodeType.DataMatrix => "Metin; küçük ürün etiketleri için ideal.",
        CodeType.Aztec => "Metin; biletlerde yaygın.",
        _ => ""
    };
}

public sealed class Paint
{
    public Color C1 = Colors.Black;
    public Color C2 = Colors.Black;
    public bool IsGradient;
    public GradientKind Kind;
    /// <summary>Gradyanın uzandığı alan (sahne koordinatları).</summary>
    public double BX, BY, BW, BH;

    public static Paint Solid(Color c) => new() { C1 = c, C2 = c };

    /// <summary>Doğrusal gradyanın başlangıç/bitişi veya radyal için merkez + yarıçap.</summary>
    public (Pt a, Pt b) Line() => Kind switch
    {
        GradientKind.Horizontal => (new(BX, BY + BH / 2), new(BX + BW, BY + BH / 2)),
        GradientKind.Vertical => (new(BX + BW / 2, BY), new(BX + BW / 2, BY + BH)),
        GradientKind.Diagonal => (new(BX, BY), new(BX + BW, BY + BH)),
        GradientKind.DiagonalReverse => (new(BX, BY + BH), new(BX + BW, BY)),
        _ => (new(BX + BW / 2, BY + BH / 2), new(BX + BW / 2 + Math.Max(BW, BH) * 0.7071, BY + BH / 2))
    };
}

public sealed class CodeOptions
{
    public CodeType Type = CodeType.QrCode;
    public string Content = "";

    // QR
    public char Ecc = 'M';
    public int QrVersion;          // 0 = otomatik
    public int QrMask = -1;        // -1 = otomatik
    public string Charset = "UTF-8";
    public bool QrEci = true;
    public int Margin = 4;

    // Diğer 2B
    public int DataMatrixShape;    // 0 otomatik, 1 kare, 2 dikdörtgen
    public int Pdf417Ecc = 2;
    public bool Pdf417Compact;
    public int AztecEcc = 33;
    public int AztecLayers;        // 0 = otomatik

    // 1B
    public double BarHeightRatio = 0.35;
    public bool ShowText = true;

    // Stil
    public ModuleStyle ModuleStyle = ModuleStyle.Square;
    public EyeFrameStyle EyeFrame = EyeFrameStyle.Square;
    public EyeBallStyle EyeBall = EyeBallStyle.Square;
    public double ModuleScale = 0.9;
    public Color Fg1 = Colors.Black, Fg2 = Color.FromRgb(0x25, 0x63, 0xEB);
    public bool UseGradient;
    public GradientKind Gradient = GradientKind.Diagonal;
    public Color Background = Colors.White;
    public bool Transparent;
    public bool CustomEyes;
    public Color EyeFrameColor = Colors.Black, EyeBallColor = Colors.Black;

    // Alt yazı
    public string Caption = "";
    public string FontFamily = "Segoe UI";
    public double FontPercent = 8;
    public bool FontBold = true;
    public Color TextColor = Colors.Black;

    // Logo
    public BitmapSource? Logo;
    public double LogoPercent = 22;
    public double LogoPadding = 1;
    public LogoBackground LogoBg = LogoBackground.Rounded;
    public bool LogoClear = true;
}

/// <summary>Kaydedilebilir stil ön ayarı (JSON).</summary>
public sealed class StylePreset
{
    public string ModuleStyle { get; set; } = "Square";
    public string EyeFrame { get; set; } = "Square";
    public string EyeBall { get; set; } = "Square";
    public double ModuleScale { get; set; } = 0.9;
    public string Fg1 { get; set; } = "#000000";
    public string Fg2 { get; set; } = "#2563EB";
    public bool UseGradient { get; set; }
    public string Gradient { get; set; } = "Diagonal";
    public string Background { get; set; } = "#FFFFFF";
    public bool Transparent { get; set; }
    public bool CustomEyes { get; set; }
    public string EyeFrameColor { get; set; } = "#000000";
    public string EyeBallColor { get; set; } = "#000000";
    public string TextColor { get; set; } = "#000000";
    public string FontFamily { get; set; } = "Segoe UI";
    public double FontPercent { get; set; } = 8;
    public bool FontBold { get; set; } = true;
    public double LogoPercent { get; set; } = 22;
    public double LogoPadding { get; set; } = 1;
    public string LogoBg { get; set; } = "Rounded";
    public bool LogoClear { get; set; } = true;
}
