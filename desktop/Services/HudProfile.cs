namespace PubgAutoMapper.Services;

public sealed record HudProfile(string Game, int Width, int Height, IReadOnlyList<HudPoint> Points);

public static class HudProfileGenerator
{
    public static string ToJson(HudProfile profile)
    {
        var options = new System.Text.Json.JsonSerializerOptions { WriteIndented = true };
        return System.Text.Json.JsonSerializer.Serialize(profile, options);
    }
}
