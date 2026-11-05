namespace PrintStamp.Core;

public static class BarcodeLayout
{
    public const double WidthFraction = 0.28;
    public const double HeightToWidth = 0.25;
    // barcode height as a fraction of page HEIGHT (depends on the page's width/height ratio)
    public static double HeightFraction(double pageAspect) => WidthFraction * HeightToWidth * pageAspect;

    // keep the whole barcode inside the page. Position = top-left corner, fractions 0..1
    public static BarcodePos Clamp(BarcodePos p, double pageAspect) => new(
        Math.Clamp(p.X, 0, 1 - WidthFraction),
        Math.Clamp(p.Y, 0, Math.Max(0, 1 - HeightFraction(pageAspect))));
}