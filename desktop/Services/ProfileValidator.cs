namespace PubgAutoMapper.Services;

public static class ProfileValidator
{
    public static IReadOnlyList<string> Validate(IReadOnlyList<HudPoint> points)
    {
        var errors = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in points)
        {
            if (string.IsNullOrWhiteSpace(p.Name)) errors.Add("A detected point has no name.");
            if (!seen.Add(p.Name)) errors.Add($"Duplicate point: {p.Name}");
            if (p.X < 0 || p.X > 1 || p.Y < 0 || p.Y > 1) errors.Add($"Out-of-range coordinate: {p.Name}");
            if (p.Confidence < 0 || p.Confidence > 1) errors.Add($"Invalid confidence: {p.Name}");
        }
        return errors;
    }
}
