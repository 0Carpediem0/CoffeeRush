using System.Drawing;

namespace CoffeeRush.Models;

public enum BossState
{
    Patrolling,
    Chasing,
    Returning
}

public class Boss
{
    public float X { get; set; }
    public float Y { get; set; }
    public float Speed { get; set; } = 80f;
    public float ChaseSpeed { get; set; } = 120f;
    public BossState State { get; set; } = BossState.Patrolling;
    public bool IsActive { get; set; }
    public float AppearanceTimer { get; set; }
    public float NextAppearanceTime { get; set; } = 35f;

    private readonly PointF[] _patrolPoints =
    {
        new(100, 100),
        new(300, 50),
        new(500, 100),
        new(300, 150)
    };

    private int _currentPatrolIndex;

    public void Update(float deltaTime)
    {
        if (!IsActive)
        {
            AppearanceTimer -= deltaTime;
            if (AppearanceTimer <= 0)
            {
                Activate();
            }
        }
    }

    public PointF GetCurrentPatrolTarget()
    {
        return _patrolPoints[_currentPatrolIndex];
    }

    public void AdvancePatrolPoint()
    {
        _currentPatrolIndex = (_currentPatrolIndex + 1) % _patrolPoints.Length;
    }

    public void BeginChasing(Random random)
    {
        if (State == BossState.Chasing)
        {
            return;
        }

        State = BossState.Chasing;
        NextAppearanceTime = 35f + random.Next(20);
    }

    public void StartReturning()
    {
        State = BossState.Returning;
    }

    public void ResumePatrol()
    {
        State = BossState.Patrolling;
    }

    private void Activate()
    {
        IsActive = true;
        AppearanceTimer = NextAppearanceTime;
        State = BossState.Patrolling;
        X = -50;
        Y = 100;
    }

    public void Deactivate()
    {
        IsActive = false;
        AppearanceTimer = NextAppearanceTime;
        State = BossState.Patrolling;
    }

    public void Reset()
    {
        IsActive = false;
        AppearanceTimer = NextAppearanceTime;
        State = BossState.Patrolling;
        _currentPatrolIndex = 0;
    }
}
