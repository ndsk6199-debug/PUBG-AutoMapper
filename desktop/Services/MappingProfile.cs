using System.Text.Json;

namespace PubgAutoMapper.Services;

public sealed record TouchPoint(double X, double Y);

public sealed class MappingProfile
{
    public string Name { get; init; } = "PUBG S10 Lite";
    public TouchPoint Joystick { get; init; } = new(0.193265, 0.674267);
    public double JoystickRadius { get; init; } = 0.10;
    public TouchPoint Fire { get; init; } = new(0.847731, 0.762215);
    public TouchPoint Aim { get; init; } = new(0.923865, 0.521173);
    public TouchPoint Jump { get; init; } = new(0.925329, 0.664495);
    public TouchPoint Crouch { get; init; } = new(0.838946, 0.928339);
    public TouchPoint Prone { get; init; } = new(0.901903, 0.908795);
    public TouchPoint Reload { get; init; } = new(0.768668, 0.934853);

    public static MappingProfile LoadOrDefault(string path)
    {
        if (!File.Exists(path)) return new MappingProfile();
        try { return JsonSerializer.Deserialize<MappingProfile>(File.ReadAllText(path)) ?? new MappingProfile(); }
        catch { return new MappingProfile(); }
    }
}
