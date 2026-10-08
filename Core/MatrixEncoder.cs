using ZXing;
using ZXing.Common;
using ZXing.QrCode.Internal;

namespace QRGen.Core;

public sealed class CodeMatrix
{
    public required bool[,] Dark;   // [x, y]
    public int Width => Dark.GetLength(0);
    public int Height => Dark.GetLength(1);
    public bool IsQr;
    public bool Is1D;
    public string Info = "";
    public string HumanText = "";
}

public static class MatrixEncoder
{
    public static CodeMatrix Encode(CodeOptions o)
    {
        if (string.IsNullOrEmpty(o.Content))
            throw new InvalidOperationException("İçerik boş.");
        return o.Type switch
        {
            CodeType.QrCode => EncodeQr(o),
            _ => EncodeOther(o)
        };
    }

    static CodeMatrix EncodeQr(CodeOptions o)
    {
        var hints = new Dictionary<EncodeHintType, object>
        {
            [EncodeHintType.CHARACTER_SET] = o.Charset,
        };
        if (!o.QrEci) hints[EncodeHintType.DISABLE_ECI] = true;
        if (o.QrVersion > 0) hints[EncodeHintType.QR_VERSION] = o.QrVersion;
        if (o.QrMask >= 0) hints[EncodeHintType.QR_MASK_PATTERN] = o.QrMask;
        var level = o.Ecc switch
        {
            'L' => ErrorCorrectionLevel.L,
            'Q' => ErrorCorrectionLevel.Q,
            'H' => ErrorCorrectionLevel.H,
            _ => ErrorCorrectionLevel.M
        };
        QRCode qr;
        try { qr = Encoder.encode(o.Content, level, hints); }
        catch (WriterException ex)
        {
            throw new InvalidOperationException(o.QrVersion > 0
                ? $"Veri, sürüm {o.QrVersion} için çok uzun. Sürümü artırın veya 'Otomatik' seçin."
                : "Veri QR kod kapasitesini aşıyor. " + ex.Message);
        }
        var m = qr.Matrix;
        var dark = new bool[m.Width, m.Height];
        for (int y = 0; y < m.Height; y++)
            for (int x = 0; x < m.Width; x++)
                dark[x, y] = m[x, y] == 1;
        return new CodeMatrix
        {
            Dark = dark,
            IsQr = true,
            Info = $"QR · Sürüm {qr.Version.VersionNumber} · {m.Width}×{m.Height} modül · Hata düzeltme {o.Ecc} · Maske {qr.MaskPattern}"
        };
    }

    static CodeMatrix EncodeOther(CodeOptions o)
    {
        var hints = new Dictionary<EncodeHintType, object> { [EncodeHintType.MARGIN] = 0 };
        Writer writer;
        BarcodeFormat fmt;
        string content = o.Content;
        switch (o.Type)
        {
            case CodeType.DataMatrix:
                writer = new ZXing.Datamatrix.DataMatrixWriter(); fmt = BarcodeFormat.DATA_MATRIX;
                hints[EncodeHintType.DATA_MATRIX_SHAPE] = o.DataMatrixShape switch
                {
                    1 => ZXing.Datamatrix.Encoder.SymbolShapeHint.FORCE_SQUARE,
                    2 => ZXing.Datamatrix.Encoder.SymbolShapeHint.FORCE_RECTANGLE,
                    _ => ZXing.Datamatrix.Encoder.SymbolShapeHint.FORCE_NONE
                };
                hints[EncodeHintType.CHARACTER_SET] = "UTF-8";
                break;
            case CodeType.Aztec:
                writer = new ZXing.Aztec.AztecWriter(); fmt = BarcodeFormat.AZTEC;
                hints[EncodeHintType.ERROR_CORRECTION] = o.AztecEcc;
                if (o.AztecLayers != 0) hints[EncodeHintType.AZTEC_LAYERS] = o.AztecLayers;
                hints[EncodeHintType.CHARACTER_SET] = "UTF-8";
                break;
            case CodeType.Pdf417:
                writer = new ZXing.PDF417.PDF417Writer(); fmt = BarcodeFormat.PDF_417;
                hints[EncodeHintType.ERROR_CORRECTION] = (ZXing.PDF417.Internal.PDF417ErrorCorrectionLevel)o.Pdf417Ecc;
                hints[EncodeHintType.PDF417_COMPACT] = o.Pdf417Compact;
                hints[EncodeHintType.CHARACTER_SET] = "UTF-8";
                break;
            case CodeType.Code128: writer = new ZXing.OneD.Code128Writer(); fmt = BarcodeFormat.CODE_128; break;
            case CodeType.Code39: writer = new ZXing.OneD.Code39Writer(); fmt = BarcodeFormat.CODE_39; break;
            case CodeType.Code93: writer = new ZXing.OneD.Code93Writer(); fmt = BarcodeFormat.CODE_93; break;
            case CodeType.Ean13: writer = new ZXing.OneD.EAN13Writer(); fmt = BarcodeFormat.EAN_13; break;
            case CodeType.Ean8: writer = new ZXing.OneD.EAN8Writer(); fmt = BarcodeFormat.EAN_8; break;
            case CodeType.UpcA: writer = new ZXing.OneD.UPCAWriter(); fmt = BarcodeFormat.UPC_A; break;
            case CodeType.UpcE: writer = new ZXing.OneD.UPCEWriter(); fmt = BarcodeFormat.UPC_E; break;
            case CodeType.Itf: writer = new ZXing.OneD.ITFWriter(); fmt = BarcodeFormat.ITF; break;
            case CodeType.Codabar: writer = new ZXing.OneD.CodaBarWriter(); fmt = BarcodeFormat.CODABAR; break;
            case CodeType.Msi: writer = new ZXing.OneD.MSIWriter(); fmt = BarcodeFormat.MSI; break;
            case CodeType.Plessey: writer = new ZXing.OneD.PlesseyWriter(); fmt = BarcodeFormat.PLESSEY; break;
            default: throw new NotSupportedException();
        }

        BitMatrix bm;
        try { bm = writer.encode(content, fmt, 1, 1, hints); }
        catch (Exception)
        {
            throw new InvalidOperationException($"{CodeTypeInfo.Display(o.Type)} için geçersiz içerik. {CodeTypeInfo.Hint(o.Type)}");
        }

        bool is1D = CodeTypeInfo.Is1D(o.Type);
        bool[,] dark;
        if (is1D) dark = Normalize1D(bm, o.Type == CodeType.Plessey);
        else
        {
            dark = new bool[bm.Width, bm.Height];
            for (int y = 0; y < bm.Height; y++)
                for (int x = 0; x < bm.Width; x++)
                    dark[x, y] = bm[x, y];
        }

        return new CodeMatrix
        {
            Dark = dark,
            Is1D = is1D,
            HumanText = is1D ? HumanReadable(o.Type, content) : "",
            Info = is1D
                ? $"{CodeTypeInfo.Display(o.Type)} · {dark.GetLength(0)} modül genişlik"
                : $"{CodeTypeInfo.Display(o.Type)} · {bm.Width}×{bm.Height} modül"
        };
    }

    static string HumanReadable(CodeType t, string s)
    {
        int need = t switch { CodeType.Ean13 => 12, CodeType.Ean8 => 7, CodeType.UpcA => 11, _ => -1 };
        if (need > 0 && s.Length == need && s.All(char.IsAsciiDigit))
            return s + CheckDigit(s);
        return s;
    }

    static int Gcd(int a, int b) => b == 0 ? a : Gcd(b, a % b);

    /// <summary>
    /// 1B çıktıyı kenar boşluklarından arındırır ve en dar çubuğu 1 modüle indirir.
    /// ZXing'in Plessey yazıcısı 5/11/14/20 birimlik çubuklar ürettiği için 5'e bölünüp yuvarlanır.
    /// </summary>
    static bool[,] Normalize1D(BitMatrix bm, bool plessey)
    {
        int l = 0, r = bm.Width - 1;
        while (l < r && !bm[l, 0]) l++;
        while (r > l && !bm[r, 0]) r--;
        var runs = new List<(bool dark, int len)>();
        for (int x = l; x <= r; x++)
        {
            if (runs.Count > 0 && runs[^1].dark == bm[x, 0]) runs[^1] = (runs[^1].dark, runs[^1].len + 1);
            else runs.Add((bm[x, 0], 1));
        }
        int unit = runs.Aggregate(0, (g, rn) => Gcd(g, rn.len));
        var lens = runs.Select(rn => plessey ? Math.Max(1, (int)Math.Round(rn.len / 5.0)) : rn.len / Math.Max(1, unit)).ToList();
        var dark = new bool[lens.Sum(), 1];
        int pos = 0;
        for (int i = 0; i < runs.Count; i++)
            for (int k = 0; k < lens[i]; k++) dark[pos++, 0] = runs[i].dark;
        return dark;
    }

    static int CheckDigit(string digits)
    {
        int sum = 0;
        for (int i = 0; i < digits.Length; i++)
        {
            int d = digits[digits.Length - 1 - i] - '0';
            sum += i % 2 == 0 ? d * 3 : d;
        }
        return (10 - sum % 10) % 10;
    }
}

