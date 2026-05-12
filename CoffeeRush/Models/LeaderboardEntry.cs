namespace CoffeeRush.Models;

public class LeaderboardEntry
{
    public string PlayerName { get; set; } = "";
    public int Score { get; set; }
    public DateTime AchievedAtUtc { get; set; }
}
