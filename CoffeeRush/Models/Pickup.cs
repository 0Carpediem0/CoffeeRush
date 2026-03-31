namespace CoffeeRush.Models;

public enum PickupType
{
    Coffee,
    Food
}

public class Pickup
{
    public int Id { get; set; }
    public PickupType Type { get; set; }
    public float X { get; set; }
    public float Y { get; set; }
    public float EnergyRestore { get; set; }
    public bool IsCollected { get; set; }

    public Pickup(int id, PickupType type, float x, float y)
    {
        Id = id;
        Type = type;
        X = x;
        Y = y;
        IsCollected = false;

        EnergyRestore = type switch
        {
            PickupType.Coffee => 15f,
            PickupType.Food => 25f,
            _ => 0f
        };
    }
}
