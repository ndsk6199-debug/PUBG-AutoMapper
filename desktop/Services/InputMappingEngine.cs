namespace PubgAutoMapper.Services;

public enum ControlAction { MoveUp, MoveLeft, MoveDown, MoveRight, Fire, Aim, Jump, Crouch, Prone, Reload, Inventory, Map, Weapon1, Weapon2, Sprint }

public sealed record MappingBinding(string Input, ControlAction Action);

public static class DefaultMappings
{
    public static IReadOnlyList<MappingBinding> Create() => new[]
    {
        new MappingBinding("W", ControlAction.MoveUp), new MappingBinding("A", ControlAction.MoveLeft),
        new MappingBinding("S", ControlAction.MoveDown), new MappingBinding("D", ControlAction.MoveRight),
        new MappingBinding("MouseLeft", ControlAction.Fire), new MappingBinding("MouseRight", ControlAction.Aim),
        new MappingBinding("Space", ControlAction.Jump), new MappingBinding("C", ControlAction.Crouch),
        new MappingBinding("Z", ControlAction.Prone), new MappingBinding("R", ControlAction.Reload),
        new MappingBinding("Tab", ControlAction.Inventory), new MappingBinding("M", ControlAction.Map),
        new MappingBinding("1", ControlAction.Weapon1), new MappingBinding("2", ControlAction.Weapon2),
        new MappingBinding("Shift", ControlAction.Sprint)
    };
}

public static class CoordinateMath
{
    public static double Normalize(double pixel, double length) => length <= 0 ? 0 : Math.Clamp(pixel / length, 0, 1);
    public static (double X, double Y) Normalize(double x, double y, double width, double height)
        => (Normalize(x, width), Normalize(y, height));
}
