namespace CoffeeRush.Models;

public class Obstacle
{
    public float X { get; set; }
    public float Y { get; set; }
    public float Width { get; set; }
    public float Height { get; set; }

    public bool IntersectsCircle(float centerX, float centerY, float radius)
    {
        float closestX = Math.Clamp(centerX, X, X + Width);
        float closestY = Math.Clamp(centerY, Y, Y + Height);
        float dx = centerX - closestX;
        float dy = centerY - closestY;
        return dx * dx + dy * dy < radius * radius;
    }
}
