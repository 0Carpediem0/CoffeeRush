namespace CoffeeRush.Models;

public class Booth
{
    public float X { get; set; }
    public float Y { get; set; }
    public float Width { get; set; } = 60f;
    public float Height { get; set; } = 60f;

    public bool ContainsPoint(float px, float py)
    {
        return px >= X && px <= X + Width &&
               py >= Y && py <= Y + Height;
    }
}
