namespace PubgAutoMapper.Models;

public readonly record struct CoordinatePoint(double X, double Y)
{
    public CoordinatePoint Normalize(int width, int height) => new(X / width, Y / height);
    public override string ToString() => $"px ({X:0}, {Y:0})";
}
