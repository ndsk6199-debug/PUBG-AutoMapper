using System.Text.Json;
using System.Text.Json.Serialization;

namespace PubgAutoMapper.Services;

public sealed record ExportButton(string Name, string Key, double X, double Y);

public static class ProfileExportService
{
    public static string BuildJson(string game, string deviceModel, int width, int height, IReadOnlyList<HudPoint> points)
    {
        var buttons = points.Select(p => new ExportButton(p.Name, DefaultKey(p.Name), p.X, p.Y)).ToList();
        var payload = new
        {
            schemaVersion = 1,
            game,
            device = new { model = deviceModel, orientation = width >= height ? "landscape" : "portrait" },
            coordinateSystem = new { type = "normalized", origin = "top-left", xRange = new[] { 0.0, 1.0 }, yRange = new[] { 0.0, 1.0 } },
            buttons,
            detection = new { generatedAtUtc = DateTime.UtcNow, pointsDetected = points.Count }
        };
        return JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull });
    }

    private static string DefaultKey(string name) => name switch
    {
        "Fire" => "MouseLeft",
        "Aim Down Sights" => "MouseRight",
        "Jump" => "Space",
        "Crouch" => "C",
        "Prone" => "Z",
        "Reload" => "R",
        "Map" => "M",
        "Weapon 1" => "1",
        "Weapon 2" => "2",
        "Inventory" => "Tab",
        "Sprint" => "Shift",
        _ => ""
    };
}
