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
        return Type switch
        {
            TaskType.CodeReview => 90,
            TaskType.BugFix => 120,
            TaskType.Documentation => 80,
            TaskType.Meeting => 70,
            TaskType.Deploy => 140,
            _ => 100
        };
    }

    public int GetPenalty()
    {
        return Type switch
        {
            TaskType.CodeReview => 35,
            TaskType.BugFix => 45,
            TaskType.Documentation => 25,
            TaskType.Meeting => 20,
            TaskType.Deploy => 55,
            _ => 30
        };
    }

    public static float GetRecommendedTime(TaskType type)
    {
        return type switch
        {
            TaskType.CodeReview => 15f,
            TaskType.BugFix => 18f,
            TaskType.Documentation => 22f,
            TaskType.Meeting => 20f,
            TaskType.Deploy => 16f,
            _ => 18f
        };
    }
}
