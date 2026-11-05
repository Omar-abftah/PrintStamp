using System.IO;
using Avalonia.Media.Imaging;
using PDFtoImage;
using SkiaSharp;

namespace PrintStamp.PrintApp.Services;

public static class PdfPreview
{
    // renders page 1 as it appears on screen (rotation already applied by PDFium)
    public static Bitmap RenderFirstPage(string path)
    {
        using var fs = File.OpenRead(path);
        using var sk = Conversion.ToImage(fs, 0);
        using var data = sk.Encode(SKEncodedImageFormat.Png, 90);
        using var ms = new MemoryStream();
        data.SaveTo(ms);
        ms.Position = 0;
        return new Bitmap(ms);
    }
}