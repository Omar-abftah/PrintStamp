using iText.IO.Font.Constants;
using iText.Kernel.Colors;
using iText.Kernel.Font;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas;
using PrintStamp.Core;
using ZXing;
using ZXing.OneD;

namespace PrintStamp.PrintApp.Services;

public static class PdfStamper
{
    public static void Stamp(string input, string output, string code, BarcodePos pos)
    {
        var bits = Code128Bits(code);

        using var pdf = new PdfDocument(new PdfReader(input), new PdfWriter(output));
        var font = PdfFontFactory.CreateFont(StandardFonts.HELVETICA);

        for (int i = 1; i <= pdf.GetNumberOfPages(); i++)
        {
            var page = pdf.GetPage(i);
            var box = page.GetPageSize();                       // unrotated visible box
            float W = box.GetWidth(), H = box.GetHeight();
            int rot = ((page.GetRotation() % 360) + 360) % 360;
            bool swap = rot is 90 or 270;
            double vw = swap ? H : W, vh = swap ? W : H;        // page size as the user SEES it

            // same layout rules as the preview, so what was clicked is what gets printed
            double bw = BarcodeLayout.WidthFraction * vw;
            double bh = bw * BarcodeLayout.HeightToWidth;
            var p = BarcodeLayout.Clamp(pos, vw / vh);
            double x = p.X * vw, y = p.Y * vh;

            // draw in "visual" coordinates (origin top-left, y down); this matrix maps them
            // onto the page's real coordinate space, including /Rotate
            var (a, b, c, d, e, f) = rot switch
            {
                90  => (0f, 1f, 1f, 0f, 0f, 0f),
                180 => (-1f, 0f, 0f, 1f, W, 0f),
                270 => (0f, -1f, -1f, 0f, W, H),
                _   => (1f, 0f, 0f, -1f, 0f, H),
            };

            var canvas = new PdfCanvas(page);
            canvas.SaveState();
            canvas.ConcatMatrix(a, b, c, d, e + box.GetLeft(), f + box.GetBottom());

            // white backing: bars stay scannable even over text or images
            canvas.SetFillColor(ColorConstants.WHITE).Rectangle(x, y, bw, bh).Fill();

            // bars (the matrix already includes the quiet zone on both sides)
            double module = bw / bits.Length, pad = bh * 0.05, barH = bh * 0.68;
            canvas.SetFillColor(ColorConstants.BLACK);
            for (int k = 0; k < bits.Length;)
            {
                if (!bits[k]) { k++; continue; }
                int start = k;
                while (k < bits.Length && bits[k]) k++;
                canvas.Rectangle(x + start * module, y + pad, (k - start) * module, barH);
            }
            canvas.Fill();

            // human-readable code underneath, for typing it in if a scan ever fails
            float fs = (float)(bh * 0.17);
            float tw = font.GetWidth(code, fs);
            canvas.BeginText().SetFontAndSize(font, fs)
                  .SetTextMatrix(1, 0, 0, -1, (float)(x + (bw - tw) / 2), (float)(y + pad + barH + bh * 0.2))
                  .ShowText(code).EndText();   // d = -1 cancels the flip, so the text isn't mirrored

            canvas.RestoreState();
        }
    }

    static bool[] Code128Bits(string code)
    {
        var m = new Code128Writer().encode(code, BarcodeFormat.CODE_128, 1, 1);
        var bits = new bool[m.Width];
        for (int i = 0; i < bits.Length; i++) bits[i] = m[i, 0];
        return bits;
    }
}