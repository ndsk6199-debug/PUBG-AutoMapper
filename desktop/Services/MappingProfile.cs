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
    public TouchPoint Inventory { get; init; } = new(0.0893119, 0.899023);
    public TouchPoint Map { get; init; } = new(0.920937, 0.117264);
    public TouchPoint Weapon1 { get; init; } = new(0.443631, 0.915309);
    public TouchPoint Weapon2 { get; init; } = new(0.547584, 0.912052);
    public TouchPoint Sprint { get; init; } = new(0.689605, 0.781759);
    public TouchPoint LookCenter { get; init; } = new(0.63, 0.47);
    public double MouseScale { get; init; } = 2.2;

    public static MappingProfile LoadOrDefault(string path)
    {
        if (!File.Exists(path)) return new MappingProfile();
        try { return JsonSerializer.Deserialize<MappingProfile>(File.ReadAllText(path)) ?? new MappingProfile(); }
        catch { return new MappingProfile(); }
    }

    public static void Validate(MappingProfile profile)
    {
        var points = new[] { profile.Joystick, profile.Fire, profile.Aim, profile.Jump, profile.Crouch, profile.Prone,
            profile.Reload, profile.Inventory, profile.Map, profile.Weapon1, profile.Weapon2, profile.Sprint, profile.LookCenter };
        if (points.Any(p => p.X < 0 || p.X > 1 || p.Y < 0 || p.Y > 1)) throw new InvalidDataException("Profile contains out-of-range coordinates.");
        if (profile.JoystickRadius <= 0 || profile.JoystickRadius >= 1) throw new InvalidDataException("JoystickRadius must be between 0 and 1.");
        if (profile.MouseScale <= 0) throw new InvalidDataException("MouseScale must be positive.");
    }
}
