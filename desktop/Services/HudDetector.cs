using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PubgAutoMapper.Services;

public sealed record HudPoint(string Name, double X, double Y, double Confidence, string Method);

public sealed class HudDetector
{
    public IReadOnlyList<HudPoint> Detect(byte[] png, int width, int height)
    {
        using var input = new MemoryStream(png);
        var decoder = new PngBitmapDecoder(input, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
        BitmapSource source = decoder.Frames[0];
        if (source.Format != PixelFormats.Bgra32)
            source = new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);

        int w = source.PixelWidth, h = source.PixelHeight, stride = w * 4;
        var pixels = new byte[stride * h];
        source.CopyPixels(pixels, stride, 0);
        var points = new List<HudPoint>();

        DetectRegion(points, "Joystick", pixels, w, h, 0.00, 0.45, 0.42, 0.98, 185);
        DetectRegion(points, "Fire", pixels, w, h, 0.72, 0.55, 1.00, 0.98, 185);
        DetectRegion(points, "Scope", pixels, w, h, 0.80, 0.30, 1.00, 0.75, 175);
        DetectRegion(points, "Jump", pixels, w, h, 0.82, 0.55, 1.00, 0.82, 175);
        DetectRegion(points, "Crouch", pixels, w, h, 0.70, 0.78, 0.98, 1.00, 175);
        DetectRegion(points, "Prone", pixels, w, h, 0.80, 0.78, 1.00, 1.00, 175);
        DetectRegion(points, "Reload", pixels, w, h, 0.62, 0.82, 0.88, 1.00, 175);
        DetectRegion(points, "Map", pixels, w, h, 0.82, 0.00, 1.00, 0.20, 175);
        return points;
    }

    private static void DetectRegion(List<HudPoint> points, string name, byte[] px, int w, int h,
        double x0, double y0, double x1, double y1, int threshold)
    {
        int left = Math.Clamp((int)(w * x0), 0, w - 1);
        int top = Math.Clamp((int)(h * y0), 0, h - 1);
        int right = Math.Clamp((int)(w * x1), left + 1, w);
        int bottom = Math.Clamp((int)(h * y1), top + 1, h);
        int step = Math.Max(1, Math.Min(w, h) / 220);
        var candidates = new List<(int x, int y, int brightness)>();

        for (int y = top; y < bottom; y += step)
        for (int x = left; x < right; x += step)
        {
            int i = y * w * 4 + x * 4;
            int b = px[i], g = px[i + 1], r = px[i + 2];
            int brightness = (r + g + b) / 3;
            int spread = Math.Max(r, Math.Max(g, b)) - Math.Min(r, Math.Min(g, b));
            if (brightness >= threshold && spread <= 110)
                candidates.Add((x, y, brightness));
        }

        if (candidates.Count < 6) return;

        // Weighted centroid is more stable than a raw pixel centroid for translucent HUD icons.
        double sx = 0, sy = 0, weight = 0;
        foreach (var c in candidates)
        {
            double wt = Math.Max(1, c.brightness - threshold + 1);
            sx += c.x * wt;
            sy += c.y * wt;
            weight += wt;
        }

        double nx = Math.Clamp(sx / weight / w, x0, Math.Min(1, x1));
        double ny = Math.Clamp(sy / weight / h, y0, Math.Min(1, y1));
        double confidence = Math.Clamp(0.40 + candidates.Count / 250.0, 0.40, 0.96);
        points.Add(new HudPoint(name, nx, ny, confidence, "weighted-bright-cluster"));
    }
}
