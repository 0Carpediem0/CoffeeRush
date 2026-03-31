namespace CoffeeRush.Models;

public enum TaskType
{
    CodeReview,
    BugFix,
    Documentation,
    Meeting,
    Deploy
}

public class WorkTask
{
    public int Id { get; set; }
    public TaskType Type { get; set; }
    public float X { get; set; }
    public float Y { get; set; }
    public float TimeRemaining { get; set; }
    public float MaxTime { get; set; }
    public bool IsCompleted { get; set; }
    public bool IsExpired => TimeRemaining <= 0 && !IsCompleted;

    public WorkTask(int id, TaskType type, float maxTime)
    {
        Id = id;
        Type = type;
        MaxTime = maxTime;
        TimeRemaining = maxTime;
        IsCompleted = false;
    }

    public void Update(float deltaTime)
    {
        if (!IsCompleted)
        {
            TimeRemaining -= deltaTime;
        }
    }

    public int GetPoints()
    {
        return 100;
    }

    public int GetPenalty()
    {
        return 50;
    }
}
