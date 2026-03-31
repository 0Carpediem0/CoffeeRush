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
    public float PatrolTargetX { get; set; }
    public float PatrolTargetY { get; set; }
    public bool IsActive { get; set; }
    public float AppearanceTimer { get; set; }
    public float NextAppearanceTime { get; set; } = 35f;

    private readonly float[] _patrolPointsX = { 100, 300, 500, 300 };
    private readonly float[] _patrolPointsY = { 100, 50, 100, 150 };
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
            return;
        }

        switch (State)
        {
            case BossState.Patrolling:
                Patrol(deltaTime);
                break;
            case BossState.Chasing:
                break;
            case BossState.Returning:
                ReturnToPatrol(deltaTime);
                break;
        }
    }

    private void Patrol(float deltaTime)
    {
        float targetX = _patrolPointsX[_currentPatrolIndex];
        float targetY = _patrolPointsY[_currentPatrolIndex];

        float dx = targetX - X;
        float dy = targetY - Y;
        float dist = MathF.Sqrt(dx * dx + dy * dy);

        if (dist < 5f)
        {
            _currentPatrolIndex = (_currentPatrolIndex + 1) % _patrolPointsX.Length;
        }
        else
        {
            X += (dx / dist) * Speed * deltaTime;
            Y += (dy / dist) * Speed * deltaTime;
        }
    }

    private void ReturnToPatrol(float deltaTime)
    {
        float targetX = _patrolPointsX[_currentPatrolIndex];
        float targetY = _patrolPointsY[_currentPatrolIndex];

        float dx = targetX - X;
        float dy = targetY - Y;
        float dist = MathF.Sqrt(dx * dx + dy * dy);

        if (dist < 5f)
        {
            State = BossState.Patrolling;
        }
        else
        {
            X += (dx / dist) * Speed * deltaTime;
            Y += (dy / dist) * Speed * deltaTime;
        }
    }

    public void Chase(float targetX, float targetY, float deltaTime)
    {
        State = BossState.Chasing;
        float dx = targetX - X;
        float dy = targetY - Y;
        float dist = MathF.Sqrt(dx * dx + dy * dy);

        if (dist > 1f)
        {
            X += (dx / dist) * ChaseSpeed * deltaTime;
            Y += (dy / dist) * ChaseSpeed * deltaTime;
        }
    }

    public void StartReturning()
    {
        State = BossState.Returning;
    }

    private void Activate()
    {
        IsActive = true;
        AppearanceTimer = NextAppearanceTime;
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
