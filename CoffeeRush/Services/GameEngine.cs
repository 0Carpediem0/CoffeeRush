using System.Drawing;
using CoffeeRush.Models;

namespace CoffeeRush.Services;

public class GameEngine
{
    private readonly GameStateData _gameState;
    private readonly Random _random = new();
    private int _nextTaskId = 1;
    private int _nextPickupId = 1;
    private float _taskSpawnTimer;
    private float _pickupSpawnTimer;
    private float _survivalScoreTimer;
    private float _bossHideTimer;
    private float _bossStuckTimer;
    private float _bossEscapeMomentumRemaining;
    private PointF _bossLastPosition;
    private BossEscapeWall _bossEscapeWall = BossEscapeWall.None;
    private BossEscapeWall _lastResolvedWall = BossEscapeWall.None;

    private const float WorldWidth = 1000f;
    private const float WorldHeight = 600f;
    private const float WorldMargin = 20f;
    private const float PlayerCollisionRadius = 18f;
    private const float BossCollisionRadius = 22f;
    private const float SpawnClearance = 28f;
    private const int BossPathCellSize = 40;
    private const float PlayerMoveProbeStep = 6f;
    private const float PlayerMoveProbeLimit = 18f;
    private const float BossHideTime = 4f;
    private const float BossStuckThreshold = 2f;
    private const float BossStuckDistanceEpsilon = 6f;
    private const float BossEscapeExtraDistance = 36f;
    private const float EnergyDecayRate = 0.55f;
    private const int MaxTasks = 4;
    private const int MaxPickups = 4;
    private const float TaskSpawnInterval = 9.5f;
    private const float PickupSpawnInterval = 8f;
    private const float SurvivalScoreInterval = 10f;
    private const float TaskFailEnergyPenalty = 6f;
    private const float TaskCompletionEnergyReward = 5f;

    public GameEngine()
    {
        _gameState = new GameStateData();
    }

    public GameStateData GetState() => _gameState;

    public void StartNewGame()
    {
        _gameState.Reset();

        var landscape = CreateLandscape();
        _gameState.LandscapeName = landscape.Name;
        _gameState.PlayerBooth = landscape.Booth;
        _gameState.Obstacles = landscape.Obstacles;
        _gameState.Player.X = landscape.PlayerSpawn.X;
        _gameState.Player.Y = landscape.PlayerSpawn.Y;
        _gameState.Player.IsInBooth = false;

        _gameState.Boss.AppearanceTimer = 22f;
        _gameState.Boss.NextAppearanceTime = 32f;
        _bossHideTimer = 0;
        _bossStuckTimer = 0;
        _bossEscapeMomentumRemaining = 0;
        _bossLastPosition = PointF.Empty;
        _bossEscapeWall = BossEscapeWall.None;
        _lastResolvedWall = BossEscapeWall.None;

        _nextTaskId = 1;
        _nextPickupId = 1;
        _taskSpawnTimer = 0;
        _pickupSpawnTimer = 0;
        _survivalScoreTimer = 0;
    }

    public void Update(float deltaTime)
    {
        if (_gameState.State != GameState.Playing)
        {
            return;
        }

        _gameState.GameTime += deltaTime;

        UpdateEnergy(deltaTime);
        UpdateTasks(deltaTime);
        UpdatePickups(deltaTime);
        UpdateBoss(deltaTime);
        UpdateSurvivalScore(deltaTime);
        CheckCollisions();
        CheckGameOver();
    }

    public void DeactivateBoss()
    {
        _gameState.Boss.Deactivate();
    }

    private void UpdateEnergy(float deltaTime)
    {
        _gameState.Player.RemoveEnergy(EnergyDecayRate * deltaTime);
    }

    private void UpdateTasks(float deltaTime)
    {
        foreach (var task in _gameState.Tasks)
        {
            task.Update(deltaTime);
        }

        var expiredTasks = _gameState.Tasks.Where(t => t.IsExpired).ToList();
        foreach (var task in expiredTasks)
        {
            _gameState.Player.RemoveEnergy(TaskFailEnergyPenalty);
            _gameState.Player.AddScore(-task.GetPenalty());
            _gameState.Player.TaskStreak = 0;
            _gameState.Tasks.Remove(task);
        }

        _taskSpawnTimer += deltaTime;
        if (_taskSpawnTimer >= TaskSpawnInterval && _gameState.Tasks.Count < MaxTasks)
        {
            SpawnTask();
            _taskSpawnTimer = 0;
        }
    }

    private void SpawnTask()
    {
        var types = Enum.GetValues<TaskType>();
        var type = types[_random.Next(types.Length)];
        var baseTime = WorkTask.GetRecommendedTime(type);
        var maxTime = Math.Max(10f, baseTime + _random.Next(-2, 3));
        var position = GetRandomFreePoint();

        var task = new WorkTask(_nextTaskId++, type, maxTime)
        {
            X = position.X,
            Y = position.Y
        };

        _gameState.Tasks.Add(task);
    }

    private void UpdatePickups(float deltaTime)
    {
        _gameState.Pickups.RemoveAll(p => p.IsCollected);

        _pickupSpawnTimer += deltaTime;
        if (_pickupSpawnTimer >= PickupSpawnInterval && _gameState.Pickups.Count < MaxPickups)
        {
            SpawnPickup();
            _pickupSpawnTimer = 0;
        }
    }

    private void SpawnPickup()
    {
        var type = _random.Next(100) < 60 ? PickupType.Coffee : PickupType.Food;
        var position = GetRandomFreePoint();
        var pickup = new Pickup(_nextPickupId++, type, position.X, position.Y);
        _gameState.Pickups.Add(pickup);
    }

    private void UpdateBoss(float deltaTime)
    {
        var boss = _gameState.Boss;

        if (!boss.IsActive)
        {
            boss.Update(deltaTime);
            if (boss.IsActive && !IsWalkable(boss.X, boss.Y, BossCollisionRadius))
            {
                var entryPoint = GetBossEntryPoint();
                boss.X = entryPoint.X;
                boss.Y = entryPoint.Y;
            }
            _bossLastPosition = new PointF(boss.X, boss.Y);
            _bossStuckTimer = 0;
            _bossEscapeMomentumRemaining = 0;
            _bossEscapeWall = BossEscapeWall.None;
            _lastResolvedWall = BossEscapeWall.None;
            return;
        }

        float currentBossSpeed = boss.State == BossState.Chasing ? boss.ChaseSpeed : boss.Speed;
        if (TryContinueBossEscapeMomentum(currentBossSpeed, deltaTime))
        {
            _bossLastPosition = new PointF(boss.X, boss.Y);
            return;
        }

        if (TryContinueBossWallEscape(currentBossSpeed, deltaTime))
        {
            _bossLastPosition = new PointF(boss.X, boss.Y);
            return;
        }

        if (_gameState.Player.IsInBooth)
        {
            _bossHideTimer += deltaTime;
            if (_bossHideTimer >= BossHideTime)
            {
                boss.Deactivate();
                _bossHideTimer = 0;
                _bossStuckTimer = 0;
                _bossEscapeMomentumRemaining = 0;
                _bossEscapeWall = BossEscapeWall.None;
                _lastResolvedWall = BossEscapeWall.None;
            }
            else if (boss.State == BossState.Chasing)
            {
                boss.StartReturning();
            }
        }
        else if (boss.State == BossState.Chasing)
        {
            _bossHideTimer = 0;
            boss.BeginChasing(_random);
            MoveBossTowards(_gameState.Player.X, _gameState.Player.Y, boss.ChaseSpeed, deltaTime);
            UpdateBossStuckState(_gameState.Player.X, _gameState.Player.Y, boss.ChaseSpeed, deltaTime);
            return;
        }

        if (!_gameState.Player.IsInBooth && boss.State != BossState.Chasing)
        {
            boss.BeginChasing(_random);
            MoveBossTowards(_gameState.Player.X, _gameState.Player.Y, boss.ChaseSpeed, deltaTime);
            UpdateBossStuckState(_gameState.Player.X, _gameState.Player.Y, boss.ChaseSpeed, deltaTime);
            return;
        }

        var patrolTarget = boss.GetCurrentPatrolTarget();
        MoveBossTowards(patrolTarget.X, patrolTarget.Y, boss.Speed, deltaTime);

        if (DistanceTo(boss.X, boss.Y, patrolTarget.X, patrolTarget.Y) < 10f)
        {
            if (boss.State == BossState.Returning)
            {
                boss.ResumePatrol();
            }
            else
            {
                boss.AdvancePatrolPoint();
            }
        }

        UpdateBossStuckState(targetX: boss.State == BossState.Chasing ? _gameState.Player.X : patrolTarget.X,
            targetY: boss.State == BossState.Chasing ? _gameState.Player.Y : patrolTarget.Y,
            speed: boss.State == BossState.Chasing ? boss.ChaseSpeed : boss.Speed,
            deltaTime: deltaTime);
    }

    private void UpdateSurvivalScore(float deltaTime)
    {
        _survivalScoreTimer += deltaTime;
        if (_survivalScoreTimer >= SurvivalScoreInterval)
        {
            int survivalTicks = Math.Max(1, (int)(_gameState.GameTime / SurvivalScoreInterval));
            int timeBonus = 8 + survivalTicks * 2;
            _gameState.Player.AddScore(timeBonus);
            _survivalScoreTimer = 0;
        }
    }

    private void CheckCollisions()
    {
        var player = _gameState.Player;
        _gameState.Player.IsInBooth = _gameState.PlayerBooth.ContainsPoint(player.X, player.Y);

        foreach (var pickup in _gameState.Pickups)
        {
            if (pickup.IsCollected)
            {
                continue;
            }

            float dx = player.X - pickup.X;
            float dy = player.Y - pickup.Y;
            float dist = MathF.Sqrt(dx * dx + dy * dy);

            if (dist < 30)
            {
                pickup.IsCollected = true;
                player.AddEnergy(pickup.EnergyRestore);
            }
        }
    }

    private void CheckGameOver()
    {
        var player = _gameState.Player;

        if (player.Energy <= 0)
        {
            _gameState.State = GameState.GameOver;
            _gameState.GameOverReason = "Вас уволили за низкую продуктивность!";
            return;
        }

        if (_gameState.Boss.IsActive && !player.IsInBooth)
        {
            float dx = player.X - _gameState.Boss.X;
            float dy = player.Y - _gameState.Boss.Y;
            float dist = MathF.Sqrt(dx * dx + dy * dy);

            if (dist < 25)
            {
                _gameState.State = GameState.GameOver;
                _gameState.GameOverReason = "Вас поймал начальник без дела!";
            }
        }
    }

    public void MovePlayer(float targetX, float targetY, float deltaTime)
    {
        var player = _gameState.Player;
        float speed = 150f;

        float dx = targetX - player.X;
        float dy = targetY - player.Y;
        float dist = MathF.Sqrt(dx * dx + dy * dy);

        if (dist > 5)
        {
            TryMovePlayerTo(
                player.X + (dx / dist) * speed * deltaTime,
                player.Y + (dy / dist) * speed * deltaTime);
        }
    }

    public void CompleteTask(int taskId)
    {
        var task = _gameState.Tasks.FirstOrDefault(t => t.Id == taskId);
        if (task == null || task.IsCompleted)
        {
            return;
        }

        task.IsCompleted = true;

        var player = _gameState.Player;
        player.TaskStreak++;

        int points = task.GetPoints();
        if (player.TaskStreak >= 3)
        {
            points = (int)(points * 1.5f);
        }

        player.AddScore(points);
        player.AddEnergy(TaskCompletionEnergyReward);
        _gameState.Tasks.Remove(task);
    }

    public void HideInBooth()
    {
        var booth = _gameState.PlayerBooth;
        _gameState.Player.X = booth.X + booth.Width / 2;
        _gameState.Player.Y = booth.Y + booth.Height / 2;
        _gameState.Player.IsInBooth = true;
    }

    public void LeaveBooth()
    {
        if (!_gameState.Player.IsInBooth)
        {
            return;
        }

        var booth = _gameState.PlayerBooth;
        float targetX = booth.X + booth.Width + 30;
        float targetY = booth.Y + booth.Height / 2;

        if (IsWalkable(targetX, targetY, PlayerCollisionRadius))
        {
            _gameState.Player.X = targetX;
            _gameState.Player.Y = targetY;
            _gameState.Player.IsInBooth = false;
        }
    }

    public void MovePlayerTo(float x, float y)
    {
        TryMovePlayerTo(x, y);
    }

    private void TryMovePlayerTo(float x, float y)
    {
        var player = _gameState.Player;
        float totalDx = x - player.X;
        float totalDy = y - player.Y;
        float distance = MathF.Sqrt(totalDx * totalDx + totalDy * totalDy);
        int steps = Math.Max(1, (int)MathF.Ceiling(distance / PlayerMoveProbeStep));

        for (int i = 1; i <= steps; i++)
        {
            float nextX = player.X + totalDx / (steps - i + 1);
            float nextY = player.Y + totalDy / (steps - i + 1);

            if (!TryMovePlayerStep(nextX, nextY))
            {
                break;
            }
        }
    }

    private bool TryMovePlayerStep(float x, float y)
    {
        var player = _gameState.Player;

        if (IsWalkable(x, y, PlayerCollisionRadius))
        {
            player.X = x;
            player.Y = y;
            return true;
        }

        bool moved = false;
        if (TrySlidePlayerHorizontally(x))
        {
            moved = true;
        }

        if (TrySlidePlayerVertically(y))
        {
            moved = true;
        }

        return moved;
    }

    private bool TrySlidePlayerHorizontally(float targetX)
    {
        var player = _gameState.Player;
        foreach (float offset in GetPlayerProbeOffsets())
        {
            float candidateY = player.Y + offset;
            if (!IsWalkable(targetX, candidateY, PlayerCollisionRadius))
            {
                continue;
            }

            player.X = targetX;
            player.Y = candidateY;
            return true;
        }

        return false;
    }

    private bool TrySlidePlayerVertically(float targetY)
    {
        var player = _gameState.Player;
        foreach (float offset in GetPlayerProbeOffsets())
        {
            float candidateX = player.X + offset;
            if (!IsWalkable(candidateX, targetY, PlayerCollisionRadius))
            {
                continue;
            }

            player.X = candidateX;
            player.Y = targetY;
            return true;
        }

        return false;
    }

    private static IEnumerable<float> GetPlayerProbeOffsets()
    {
        yield return 0f;

        for (float offset = PlayerMoveProbeStep; offset <= PlayerMoveProbeLimit; offset += PlayerMoveProbeStep)
        {
            yield return offset;
            yield return -offset;
        }
    }

    private void MoveBossTowards(float targetX, float targetY, float speed, float deltaTime)
    {
        var boss = _gameState.Boss;
        float dx = targetX - boss.X;
        float dy = targetY - boss.Y;
        float dist = MathF.Sqrt(dx * dx + dy * dy);

        if (dist <= 1f)
        {
            return;
        }

        float nextX = boss.X + (dx / dist) * speed * deltaTime;
        float nextY = boss.Y + (dy / dist) * speed * deltaTime;

        if (IsWalkable(nextX, nextY, BossCollisionRadius))
        {
            boss.X = nextX;
            boss.Y = nextY;
            return;
        }

        if (IsWalkable(nextX, boss.Y, BossCollisionRadius))
        {
            boss.X = nextX;
        }

        if (IsWalkable(boss.X, nextY, BossCollisionRadius))
        {
            boss.Y = nextY;
            return;
        }

        float step = speed * deltaTime;
        float baseAngle = MathF.Atan2(dy, dx);
        float[] angleOffsets =
        {
            0.45f, -0.45f,
            0.9f, -0.9f,
            1.35f, -1.35f,
            MathF.PI / 2f, -MathF.PI / 2f
        };

        float bestDistance = float.MaxValue;
        PointF? bestPoint = null;

        foreach (float angleOffset in angleOffsets)
        {
            float angle = baseAngle + angleOffset;
            float candidateX = boss.X + MathF.Cos(angle) * step;
            float candidateY = boss.Y + MathF.Sin(angle) * step;

            if (!IsWalkable(candidateX, candidateY, BossCollisionRadius))
            {
                continue;
            }

            float candidateDistance = DistanceTo(candidateX, candidateY, targetX, targetY);
            if (candidateDistance < bestDistance)
            {
                bestDistance = candidateDistance;
                bestPoint = new PointF(candidateX, candidateY);
            }
        }

        if (bestPoint is PointF point)
        {
            boss.X = point.X;
            boss.Y = point.Y;
            return;
        }

        var waypoint = FindBossWaypoint(targetX, targetY);
        if (waypoint is PointF nextWaypoint)
        {
            TryMoveBossStep(nextWaypoint.X, nextWaypoint.Y, speed, deltaTime);
        }
    }

    private void UpdateBossStuckState(float targetX, float targetY, float speed, float deltaTime)
    {
        var boss = _gameState.Boss;
        float movedDistance = DistanceTo(boss.X, boss.Y, _bossLastPosition.X, _bossLastPosition.Y);

        if (movedDistance < BossStuckDistanceEpsilon)
        {
            _bossStuckTimer += deltaTime;
        }
        else
        {
            _bossStuckTimer = 0;
            _bossEscapeWall = BossEscapeWall.None;
        }

        _bossLastPosition = new PointF(boss.X, boss.Y);

        if (_bossStuckTimer < BossStuckThreshold)
        {
            return;
        }

        _bossEscapeWall = DetectBossAdjacentWall();
        if (_bossEscapeWall == BossEscapeWall.None)
        {
            return;
        }

        _lastResolvedWall = _bossEscapeWall;

        if (TryContinueBossWallEscape(speed, deltaTime))
        {
            _bossStuckTimer = 0;
            _bossLastPosition = new PointF(_gameState.Boss.X, _gameState.Boss.Y);
        }
    }

    private bool TryContinueBossWallEscape(float speed, float deltaTime)
    {
        if (_bossEscapeWall == BossEscapeWall.None)
        {
            return false;
        }

        var currentWall = DetectBossAdjacentWall();
        if (currentWall != _bossEscapeWall)
        {
            _bossEscapeMomentumRemaining = BossEscapeExtraDistance;
            _bossEscapeWall = BossEscapeWall.None;
            return false;
        }

        var boss = _gameState.Boss;
        float moveDistance = speed * deltaTime;
        float targetX = boss.X;
        float targetY = boss.Y;

        switch (_bossEscapeWall)
        {
            case BossEscapeWall.Left:
            case BossEscapeWall.Right:
                targetY -= moveDistance;
                break;
            case BossEscapeWall.Top:
            case BossEscapeWall.Bottom:
                targetX += moveDistance;
                break;
        }

        if (!IsWalkable(targetX, targetY, BossCollisionRadius))
        {
            return false;
        }

        boss.X = targetX;
        boss.Y = targetY;
        return true;
    }

    private bool TryContinueBossEscapeMomentum(float speed, float deltaTime)
    {
        if (_bossEscapeMomentumRemaining <= 0 || _bossEscapeWall != BossEscapeWall.None)
        {
            return false;
        }

        var boss = _gameState.Boss;
        float moveDistance = Math.Min(speed * deltaTime, _bossEscapeMomentumRemaining);
        float targetX = boss.X;
        float targetY = boss.Y;

        switch (GetLastEscapeDirection())
        {
            case BossEscapeDirection.Up:
                targetY -= moveDistance;
                break;
            case BossEscapeDirection.Right:
                targetX += moveDistance;
                break;
            default:
                _bossEscapeMomentumRemaining = 0;
                return false;
        }

        if (!IsWalkable(targetX, targetY, BossCollisionRadius))
        {
            _bossEscapeMomentumRemaining = 0;
            return false;
        }

        boss.X = targetX;
        boss.Y = targetY;
        _bossEscapeMomentumRemaining -= moveDistance;
        return true;
    }

    private BossEscapeDirection GetLastEscapeDirection()
    {
        return _lastResolvedWall switch
        {
            BossEscapeWall.Left or BossEscapeWall.Right => BossEscapeDirection.Up,
            BossEscapeWall.Top or BossEscapeWall.Bottom => BossEscapeDirection.Right,
            _ => BossEscapeDirection.None
        };
    }

    private BossEscapeWall DetectBossAdjacentWall()
    {
        var boss = _gameState.Boss;
        const float probeDistance = 10f;

        if (!IsWalkable(boss.X + probeDistance, boss.Y, BossCollisionRadius))
        {
            return BossEscapeWall.Right;
        }

        if (!IsWalkable(boss.X - probeDistance, boss.Y, BossCollisionRadius))
        {
            return BossEscapeWall.Left;
        }

        if (!IsWalkable(boss.X, boss.Y + probeDistance, BossCollisionRadius))
        {
            return BossEscapeWall.Bottom;
        }

        if (!IsWalkable(boss.X, boss.Y - probeDistance, BossCollisionRadius))
        {
            return BossEscapeWall.Top;
        }

        return BossEscapeWall.None;
    }

    private void TryMoveBossStep(float targetX, float targetY, float speed, float deltaTime)
    {
        var boss = _gameState.Boss;
        float dx = targetX - boss.X;
        float dy = targetY - boss.Y;
        float dist = MathF.Sqrt(dx * dx + dy * dy);

        if (dist <= 1f)
        {
            return;
        }

        float moveDistance = Math.Min(speed * deltaTime, dist);
        float nextX = boss.X + (dx / dist) * moveDistance;
        float nextY = boss.Y + (dy / dist) * moveDistance;

        if (IsWalkable(nextX, nextY, BossCollisionRadius))
        {
            boss.X = nextX;
            boss.Y = nextY;
            return;
        }

        if (IsWalkable(nextX, boss.Y, BossCollisionRadius))
        {
            boss.X = nextX;
        }

        if (IsWalkable(boss.X, nextY, BossCollisionRadius))
        {
            boss.Y = nextY;
        }
    }

    private PointF? FindBossWaypoint(float targetX, float targetY)
    {
        int cols = (int)(WorldWidth / BossPathCellSize);
        int rows = (int)(WorldHeight / BossPathCellSize);

        var startCell = FindNearestWalkableCell(_gameState.Boss.X, _gameState.Boss.Y, cols, rows);
        var goalCell = FindNearestWalkableCell(targetX, targetY, cols, rows);
        if (startCell is null || goalCell is null)
        {
            return null;
        }

        if (startCell.Value == goalCell.Value)
        {
            return GetCellCenter(goalCell.Value.x, goalCell.Value.y);
        }

        var queue = new Queue<(int x, int y)>();
        var visited = new bool[cols, rows];
        var previous = new (int x, int y)[cols, rows];

        queue.Enqueue(startCell.Value);
        visited[startCell.Value.x, startCell.Value.y] = true;
        previous[startCell.Value.x, startCell.Value.y] = (-1, -1);

        int[] offsetX = { 1, -1, 0, 0 };
        int[] offsetY = { 0, 0, 1, -1 };

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (current == goalCell.Value)
            {
                break;
            }

            for (int i = 0; i < 4; i++)
            {
                int nx = current.x + offsetX[i];
                int ny = current.y + offsetY[i];

                if (nx < 0 || nx >= cols || ny < 0 || ny >= rows || visited[nx, ny])
                {
                    continue;
                }

                var center = GetCellCenter(nx, ny);
                if (!IsWalkable(center.X, center.Y, BossCollisionRadius))
                {
                    continue;
                }

                visited[nx, ny] = true;
                previous[nx, ny] = current;
                queue.Enqueue((nx, ny));
            }
        }

        if (!visited[goalCell.Value.x, goalCell.Value.y])
        {
            return null;
        }

        var step = goalCell.Value;
        while (previous[step.x, step.y] != startCell.Value)
        {
            step = previous[step.x, step.y];
            if (step == (-1, -1))
            {
                return null;
            }
        }

        return GetCellCenter(step.x, step.y);
    }

    private (int x, int y)? FindNearestWalkableCell(float x, float y, int cols, int rows)
    {
        int cellX = Math.Clamp((int)(x / BossPathCellSize), 0, cols - 1);
        int cellY = Math.Clamp((int)(y / BossPathCellSize), 0, rows - 1);

        for (int radius = 0; radius <= Math.Max(cols, rows); radius++)
        {
            for (int nx = Math.Max(0, cellX - radius); nx <= Math.Min(cols - 1, cellX + radius); nx++)
            {
                for (int ny = Math.Max(0, cellY - radius); ny <= Math.Min(rows - 1, cellY + radius); ny++)
                {
                    var center = GetCellCenter(nx, ny);
                    if (IsWalkable(center.X, center.Y, BossCollisionRadius))
                    {
                        return (nx, ny);
                    }
                }
            }
        }

        return null;
    }

    private static PointF GetCellCenter(int cellX, int cellY)
    {
        return new PointF(
            cellX * BossPathCellSize + BossPathCellSize / 2f,
            cellY * BossPathCellSize + BossPathCellSize / 2f);
    }

    private bool IsWalkable(float x, float y, float radius)
    {
        if (x < WorldMargin || x > WorldWidth - WorldMargin || y < WorldMargin || y > WorldHeight - WorldMargin)
        {
            return false;
        }

        foreach (var obstacle in _gameState.Obstacles)
        {
            if (obstacle.IntersectsCircle(x, y, radius))
            {
                return false;
            }
        }

        return true;
    }

    private PointF GetRandomFreePoint()
    {
        for (int attempt = 0; attempt < 100; attempt++)
        {
            float x = 140 + _random.Next(700);
            float y = 60 + _random.Next(440);

            if (!IsWalkable(x, y, SpawnClearance))
            {
                continue;
            }

            if (DistanceTo(_gameState.PlayerBooth.X + _gameState.PlayerBooth.Width / 2, _gameState.PlayerBooth.Y + _gameState.PlayerBooth.Height / 2, x, y) < 110)
            {
                continue;
            }

            bool nearTask = _gameState.Tasks.Any(task => DistanceTo(task.X, task.Y, x, y) < 70);
            bool nearPickup = _gameState.Pickups.Any(pickup => !pickup.IsCollected && DistanceTo(pickup.X, pickup.Y, x, y) < 60);
            if (nearTask || nearPickup)
            {
                continue;
            }

            return new PointF(x, y);
        }

        return new PointF(650, 200);
    }

    private PointF GetBossEntryPoint()
    {
        for (float y = 70f; y <= WorldHeight - 70f; y += 30f)
        {
            float x = WorldMargin + BossCollisionRadius + 4f;
            if (IsWalkable(x, y, BossCollisionRadius))
            {
                return new PointF(x, y);
            }
        }

        for (float x = 120f; x <= WorldWidth - 120f; x += 40f)
        {
            float y = WorldMargin + BossCollisionRadius + 4f;
            if (IsWalkable(x, y, BossCollisionRadius))
            {
                return new PointF(x, y);
            }
        }

        return new PointF(60f, 100f);
    }

    private static float DistanceTo(float x1, float y1, float x2, float y2)
    {
        float dx = x1 - x2;
        float dy = y1 - y2;
        return MathF.Sqrt(dx * dx + dy * dy);
    }

    private LandscapePreset CreateLandscape()
    {
        return _random.Next(3) switch
        {
            0 => CreateOpenOfficeLandscape(),
            1 => CreateWarehouseLandscape(),
            _ => CreateMeetingMazeLandscape()
        };
    }

    private static LandscapePreset CreateOpenOfficeLandscape()
    {
        return new LandscapePreset(
            "Опен-спейс",
            new Booth { X = 60, Y = 460, Width = 70, Height = 70 },
            new List<Obstacle>
            {
                new() { X = 240, Y = 90, Width = 70, Height = 220 },
                new() { X = 380, Y = 0, Width = 70, Height = 160 },
                new() { X = 380, Y = 250, Width = 70, Height = 220 },
                new() { X = 560, Y = 120, Width = 170, Height = 60 },
                new() { X = 560, Y = 330, Width = 170, Height = 60 },
                new() { X = 820, Y = 80, Width = 60, Height = 180 }
            },
            new PointF(150, 500));
    }

    private static LandscapePreset CreateWarehouseLandscape()
    {
        return new LandscapePreset(
            "Складской этаж",
            new Booth { X = 70, Y = 70, Width = 70, Height = 70 },
            new List<Obstacle>
            {
                new() { X = 200, Y = 70, Width = 70, Height = 150 },
                new() { X = 200, Y = 290, Width = 70, Height = 220 },
                new() { X = 380, Y = 70, Width = 70, Height = 210 },
                new() { X = 380, Y = 350, Width = 70, Height = 160 },
                new() { X = 560, Y = 70, Width = 70, Height = 130 },
                new() { X = 560, Y = 270, Width = 70, Height = 240 },
                new() { X = 740, Y = 70, Width = 70, Height = 210 },
                new() { X = 740, Y = 350, Width = 70, Height = 160 }
            },
            new PointF(160, 150));
    }

    private static LandscapePreset CreateMeetingMazeLandscape()
    {
        return new LandscapePreset(
            "Переговорки",
            new Booth { X = 70, Y = 470, Width = 70, Height = 70 },
            new List<Obstacle>
            {
                new() { X = 180, Y = 120, Width = 180, Height = 50 },
                new() { X = 180, Y = 260, Width = 180, Height = 50 },
                new() { X = 180, Y = 400, Width = 180, Height = 50 },
                new() { X = 470, Y = 50, Width = 55, Height = 160 },
                new() { X = 470, Y = 280, Width = 55, Height = 220 },
                new() { X = 620, Y = 120, Width = 220, Height = 50 },
                new() { X = 620, Y = 330, Width = 220, Height = 50 }
            },
            new PointF(170, 510));
    }

    private sealed record LandscapePreset(string Name, Booth Booth, List<Obstacle> Obstacles, PointF PlayerSpawn);

    private enum BossEscapeWall
    {
        None,
        Left,
        Right,
        Top,
        Bottom
    }

    private enum BossEscapeDirection
    {
        None,
        Up,
        Right
    }
}
