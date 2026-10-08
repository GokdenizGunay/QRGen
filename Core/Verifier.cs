using System.Windows.Media;
using System.Windows.Media.Imaging;
using QRGen.Export;
using ZXing;
using ZXing.Common;

namespace QRGen.Core;

/// <summary>Oluşturulan görüntüyü ZXing ile geri okuyarak taranabilirliği doğrular.</summary>
public static class Verifier
{
    /// <summary>ZXing'de Plessey okuyucu bulunmaz.</summary>
    public static bool Supported(CodeType t) => t != CodeType.Plessey;

    public static (byte[] px, int w, int h) Snapshot(Scene s)
    {
        int target = (int)Math.Clamp(s.Width * 4, 300, 1400);
        var bmp = WpfRenderer.Render(s, target, true, 96, s.Transparent ? Colors.White : null);
        int w = bmp.PixelWidth, h = bmp.PixelHeight;
        var px = new byte[w * h * 4];
        bmp.CopyPixels(px, w * 4, 0);
        return (px, w, h);
    }

    public static string? Decode(byte[] px, int w, int h, CodeType type)
    {
        var fmt = type switch
        {
            CodeType.QrCode => BarcodeFormat.QR_CODE,
            CodeType.DataMatrix => BarcodeFormat.DATA_MATRIX,
            CodeType.Aztec => BarcodeFormat.AZTEC,
            CodeType.Pdf417 => BarcodeFormat.PDF_417,
            CodeType.Code128 => BarcodeFormat.CODE_128,
            CodeType.Code39 => BarcodeFormat.CODE_39,
            CodeType.Code93 => BarcodeFormat.CODE_93,
            CodeType.Ean13 => BarcodeFormat.EAN_13,
            CodeType.Ean8 => BarcodeFormat.EAN_8,
            CodeType.UpcA => BarcodeFormat.UPC_A,
            CodeType.UpcE => BarcodeFormat.UPC_E,
            CodeType.Itf => BarcodeFormat.ITF,
            CodeType.Codabar => BarcodeFormat.CODABAR,
            CodeType.Msi => BarcodeFormat.MSI,
            _ => BarcodeFormat.PLESSEY
        };
        var reader = new BarcodeReaderGeneric
        {
            AutoRotate = true,
            Options = new DecodingOptions
            {
                TryHarder = true,
                TryInverted = true,
                PossibleFormats = new[] { fmt },
                CharacterSet = "UTF-8",
                ReturnCodabarStartEnd = true
            }
        };
        var src = new RGBLuminanceSource(px, w, h, RGBLuminanceSource.BitmapFormat.BGRA32);
        return reader.Decode(src)?.Text;
    }
}
