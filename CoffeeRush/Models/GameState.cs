namespace CoffeeRush.Models;

public enum GameState
{
    Menu,
    Playing,
    Paused,
    GameOver
}

public class GameStateData
{
    public GameState State { get; set; } = GameState.Menu;
    public Player Player { get; set; } = new();
    public Boss Boss { get; set; } = new();
    public Booth PlayerBooth { get; set; } = new();
    public List<WorkTask> Tasks { get; set; } = new();
    public List<Pickup> Pickups { get; set; } = new();
    public float GameTime { get; set; }
    public int HighScore { get; set; }
    public string GameOverReason { get; set; } = "";

    public void Reset()
    {
        Player.Reset();
        Boss.Reset();
        Tasks.Clear();
        Pickups.Clear();
        GameTime = 0;
        GameOverReason = "";
        State = GameState.Playing;
    }
}
