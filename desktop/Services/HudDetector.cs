using System.Drawing;
using System.Drawing.Imaging;

namespace PubgAutoMapper.Services;

public sealed record HudPoint(string Name, double X, double Y, double Confidence, string Method);

public sealed class HudDetector
{
    public IReadOnlyList<HudPoint> Detect(byte[] png, int width, int height)
    {
        using var input = new MemoryStream(png);
        using var bitmap = new Bitmap(input);
        var points = new List<HudPoint>();

        // Deterministic first pass: detect bright circular/touch controls in known PUBG HUD regions.
        AddIfFound(points, "Joystick", FindBrightCluster(bitmap, 0.00, 0.45, 0.42, 0.95, 18, 180));
        AddIfFound(points, "Fire", FindBrightCluster(bitmap, 0.72, 0.55, 1.00, 0.98, 18, 180));
        AddIfFound(points, "Scope", FindBrightCluster(bitmap, 0.80, 0.30, 1.00, 0.75, 14, 180));
        AddIfFound(points, "Jump", FindBrightCluster(bitmap, 0.82, 0.55, 1.00, 0.80, 12, 180));
        AddIfFound(points, "Crouch", FindBrightCluster(bitmap, 0.70, 0.78, 0.96, 1.00, 12, 180));
        AddIfFound(points, "Prone", FindBrightCluster(bitmap, 0.82, 0.78, 1.00, 1.00, 12, 180));
        AddIfFound(points, "Reload", FindBrightCluster(bitmap, 0.62, 0.82, 0.86, 1.00, 12, 180));
        AddIfFound(points, "Map", FindBrightCluster(bitmap, 0.82, 0.00, 1.00, 0.20, 10, 180));
        return points;
    }

    private static void AddIfFound(List<HudPoint> list, string name, Candidate? c)
    {
        if (c is null) return;
        list.Add(new HudPoint(name, c.X, c.Y, c.Confidence, "bright-cluster"));
    }

    private static Candidate? FindBrightCluster(Bitmap b, double x0, double y0, double x1, double y1, int minPixels, int brightness)
    {
        int left = Math.Clamp((int)(b.Width * x0), 0, b.Width - 1);
        int top = Math.Clamp((int)(b.Height * y0), 0, b.Height - 1);
        int right = Math.Clamp((int)(b.Width * x1), left + 1, b.Width);
        int bottom = Math.Clamp((int)(b.Height * y1), top + 1, b.Height);
        long sx = 0, sy = 0, count = 0;
        int step = Math.Max(1, Math.Min(b.Width, b.Height) / 180);
        for (int y = top; y < bottom; y += step)
        for (int x = left; x < right; x += step)
        {
            var p = b.GetPixel(x, y);
            int v = (p.R + p.G + p.B) / 3;
            int spread = Math.Max(p.R, Math.Max(p.G, p.B)) - Math.Min(p.R, Math.Min(p.G, p.B));
            if (v >= brightness && spread < 90)
            {
                sx += x; sy += y; count++;
            }
        }
        if (count < minPixels) return null;
        double nx = (double)sx / count / b.Width;
        double ny = (double)sy / count / b.Height;
        double confidence = Math.Clamp(count / 800.0, 0.35, 0.95);
        return new Candidate(nx, ny, confidence);
    }

    private sealed record Candidate(double X, double Y, double Confidence);
}
