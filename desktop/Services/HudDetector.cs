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
        var source = decoder.Frames[0];
        var bitmap = new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
        int stride = bitmap.PixelWidth * 4;
        var pixels = new byte[stride * bitmap.PixelHeight];
        bitmap.CopyPixels(pixels, stride, 0);

        var points = new List<HudPoint>();
        AddIfFound(points, "Joystick", FindBrightCluster(pixels, bitmap.PixelWidth, bitmap.PixelHeight, 0.00, 0.45, 0.42, 0.95, 18, 180));
        AddIfFound(points, "Fire", FindBrightCluster(pixels, bitmap.PixelWidth, bitmap.PixelHeight, 0.72, 0.55, 1.00, 0.98, 18, 180));
        AddIfFound(points, "Scope", FindBrightCluster(pixels, bitmap.PixelWidth, bitmap.PixelHeight, 0.80, 0.30, 1.00, 0.75, 14, 180));
        AddIfFound(points, "Jump", FindBrightCluster(pixels, bitmap.PixelWidth, bitmap.PixelHeight, 0.82, 0.55, 1.00, 0.80, 12, 180));
        AddIfFound(points, "Crouch", FindBrightCluster(pixels, bitmap.PixelWidth, bitmap.PixelHeight, 0.70, 0.78, 0.96, 1.00, 12, 180));
        AddIfFound(points, "Prone", FindBrightCluster(pixels, bitmap.PixelWidth, bitmap.PixelHeight, 0.82, 0.78, 1.00, 1.00, 12, 180));
        AddIfFound(points, "Reload", FindBrightCluster(pixels, bitmap.PixelWidth, bitmap.PixelHeight, 0.62, 0.82, 0.86, 1.00, 12, 180));
        AddIfFound(points, "Map", FindBrightCluster(pixels, bitmap.PixelWidth, bitmap.PixelHeight, 0.82, 0.00, 1.00, 0.20, 10, 180));
        return points;
    }

    private static void AddIfFound(List<HudPoint> list, string name, Candidate? c)
    {
        if (c is null) return;
        list.Add(new HudPoint(name, c.X, c.Y, c.Confidence, "bright-cluster"));
    }

    private static Candidate? FindBrightCluster(byte[] pixels, int width, int height, double x0, double y0, double x1, double y1, int minPixels, int brightness)
    {
        int left = Math.Clamp((int)(width * x0), 0, width - 1);
        int top = Math.Clamp((int)(height * y0), 0, height - 1);
        int right = Math.Clamp((int)(width * x1), left + 1, width);
        int bottom = Math.Clamp((int)(height * y1), top + 1, height);
        long sx = 0, sy = 0, count = 0;
        int step = Math.Max(1, Math.Min(width, height) / 180);

        for (int y = top; y < bottom; y += step)
        for (int x = left; x < right; x += step)
        {
            int i = y * width * 4 + x * 4;
            int b = pixels[i], g = pixels[i + 1], r = pixels[i + 2];
            int v = (r + g + b) / 3;
            int spread = Math.Max(r, Math.Max(g, b)) - Math.Min(r, Math.Min(g, b));
            if (v >= brightness && spread < 90) { sx += x; sy += y; count++; }
        }
        if (count < minPixels) return null;
        return new Candidate((double)sx / count / width, (double)sy / count / height, Math.Clamp(count / 800.0, 0.35, 0.95));
    }

    private sealed record Candidate(double X, double Y, double Confidence);
}
