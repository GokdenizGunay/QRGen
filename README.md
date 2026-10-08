# QRGen - Easiest way to make 2D codes

A modern, portable QR code and barcode generator for Windows, built with C# and WPF (.NET 10).

QRGen runs as a single `.exe` with no installer and no .NET runtime required. You can style your codes, add a logo in the middle, and export them to raster or vector formats. Every code is checked by reading it back, so you know it scans before you save it.

## Features

### Supported symbologies
- **2D:** QR Code, Data Matrix, Aztec, PDF417
- **1D:** Code 128, Code 39, Code 93, EAN-13, EAN-8, UPC-A, UPC-E, ITF (Interleaved 2 of 5), Codabar, MSI, Plessey

### Content types (2D codes)
Plain text, URL, Wi-Fi network, vCard contact, email, SMS, phone, WhatsApp, geo location, and calendar event.

### Encoding options
- **QR:** error correction level (L / M / Q / H), version (auto or 1–40), mask pattern (auto or 0–7), character set (UTF-8, ISO-8859-1, ISO-8859-9, Shift_JIS), optional ECI header
- **PDF417:** error correction level (0–8) and compact mode
- **Aztec:** error correction percentage
- **Data Matrix:** symbol shape (auto, square, or rectangle)
- **1D barcodes:** bar height and human-readable text (EAN/UPC check digits are calculated automatically)
- Adjustable quiet zone (margin) for every code type

### Styling
- 9 module styles: square, rounded, dots, diamond, smooth (connected), small squares, vertical bars, horizontal bars, star
- 5 finder pattern (eye) frame styles and 5 eye center styles
- Solid colors or gradients (horizontal, vertical, two diagonal directions, radial)
- Custom eye colors and transparent background
- Optional caption text below the code, with your choice of font, size, color, and weight
- Built-in theme presets
- Save and load styles as JSON

### Logo
- Place any image (PNG, JPG, BMP, GIF, TIFF, ICO) in the center of the code. You can also drag and drop it onto the window.
- Set the logo size and padding
- Choose a background shape: none, square, rounded, or circle
- Optionally clear the modules behind the logo
- Error correction switches to H automatically when you add a logo

### Export
- **Raster:** PNG, JPG, BMP, TIFF, GIF
- **Vector:** SVG, PDF, EPS. These scale without losing quality and are suitable for print.
- Set the output size in pixels, the DPI, and the JPEG quality
- Optional pixel snapping for sharp module edges
- Copy the image or the SVG markup to the clipboard
- **Batch mode:** each line of input becomes a separate file in the folder you choose

### Scan verification
Each generated code is decoded again with ZXing. An indicator under the preview tells you whether it scans and whether the decoded content matches. This helps when you try low-contrast colors or large logos.

### Interface
- Clean Windows 11 (Fluent) look
- System, Light, and Dark themes
- Live preview

## Download

Get `QRGen.exe` from the [Releases](../../releases) page and run it. Nothing to install.

Requirements: Windows 10 or 11 (x64).

## Building from source

Requirements: [.NET 10 SDK](https://dotnet.microsoft.com/download)

```bash
git clone https://github.com/GokdenizGunay/QRGen.git
cd QRGen
build.cmd
```

The output is a single self-contained executable at `publish\QRGen.exe`.

To run it from source for development:

```bash
dotnet run -c Release
```

## Keyboard shortcuts

| Shortcut | Action |
|---|---|
| `Ctrl+S` | Save as PNG |
| `Ctrl+Shift+S` | Save as (any format) |
| `Ctrl+C` | Copy image to clipboard |

## Settings

Settings (theme, export size, DPI, last used style) are stored in `QRGen.settings.json` next to the executable, so the app stays portable. If that folder is not writable, `%APPDATA%\QRGen` is used instead.

## Known limitations

- Plessey barcodes cannot be scan-verified because ZXing.Net has no Plessey reader.
- EPS does not support transparency, so transparent parts of a logo are filled with the background color. PNG, SVG, and PDF keep full transparency.

## Project structure

```
Core/       Encoding, scene building, payloads, verification, settings
Export/     WPF raster renderer and SVG / PDF / EPS exporters
Controls/   Custom HSV color picker
MainWindow.xaml(.cs)   User interface
```

All output formats are drawn from one shared vector model. That is why raster and vector exports look the same.

## Third-party components

- [ZXing.Net](https://github.com/micjahn/ZXing.Net): barcode encoding and decoding (Apache License 2.0)
- [.NET](https://github.com/dotnet/runtime) and [WPF](https://github.com/dotnet/wpf) (MIT License)

## License

This project is licensed under the [MIT License](LICENSE).
This project is made with Claude.
