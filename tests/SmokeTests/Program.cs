using System.Text.Json;
using PubgAutoMapper.Services;

var points = new[]
{
    new HudPoint("Fire", 0.847731, 0.762215, 0.99, "test"),
    new HudPoint("Scope", 0.923865, 0.521173, 0.95, "test")
};

var errors = ProfileValidator.Validate(points);
if (errors.Count != 0) throw new Exception(string.Join("; ", errors));

var json = ProfileExportService.BuildJson("PUBG Mobile", "Samsung Galaxy S10 Lite", 3040, 1440, points);
using var doc = JsonDocument.Parse(json);
if (!doc.RootElement.TryGetProperty("buttons", out var buttons) || buttons.GetArrayLength() != 2)
    throw new Exception("Generated profile did not contain the expected buttons.");

Console.WriteLine("Smoke tests passed: validation + JSON export + JSON parse.");
