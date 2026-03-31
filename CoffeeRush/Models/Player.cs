namespace CoffeeRush.Models;

public class Player
{
    public float X { get; set; }
    public float Y { get; set; }
    public float Energy { get; set; } = 100f;
    public int Score { get; set; }
    public bool IsInBooth { get; set; }
    public bool IsHidden => IsInBooth;
    public int TaskStreak { get; set; }

    public void MoveTo(float x, float y)
    {
        X = x;
        Y = y;
    }

    public void AddEnergy(float amount)
    {
        Energy = Math.Min(100f, Energy + amount);
    }

    public void RemoveEnergy(float amount)
    {
        Energy = Math.Max(0f, Energy - amount);
    }

    public void AddScore(int points)
    {
        Score += points;
    }

    public void Reset()
    {
        Energy = 100f;
        Score = 0;
        IsInBooth = false;
        TaskStreak = 0;
    }
}
