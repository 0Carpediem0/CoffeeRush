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

    // Настройки
    private const float EnergyDecayRate = 2f; // % в секунду
    private const int MaxTasks = 3;
    private const float TaskSpawnInterval = 8f;
    private const float PickupSpawnInterval = 6f;
    private const float SurvivalScoreInterval = 10f;

    public GameEngine()
    {
        _gameState = new GameStateData();
    }

    public GameStateData GetState() => _gameState;

    public void StartNewGame()
    {
        _gameState.Reset();
        _gameState.PlayerBooth = new Booth { X = 50, Y = 300 };
        _gameState.Player.X = _gameState.PlayerBooth.X + 30;
        _gameState.Player.Y = _gameState.PlayerBooth.Y + 30;
        _gameState.Player.IsInBooth = true;

        _nextTaskId = 1;
        _nextPickupId = 1;
        _taskSpawnTimer = 0;
        _pickupSpawnTimer = 0;
        _survivalScoreTimer = 0;
    }

    public void Update(float deltaTime)
    {
        if (_gameState.State != GameState.Playing)
            return;

        _gameState.GameTime += deltaTime;

        UpdateEnergy(deltaTime);
        UpdateTasks(deltaTime);
        UpdatePickups();
        UpdateBoss(deltaTime);
        UpdateSurvivalScore(deltaTime);
        CheckCollisions();
        CheckGameOver();
    }

    private void UpdateEnergy(float deltaTime)
    {
        _gameState.Player.RemoveEnergy(EnergyDecayRate * deltaTime);
    }

    private void UpdateTasks(float deltaTime)
    {
        // Обновление таймеров задач
        foreach (var task in _gameState.Tasks)
        {
            task.Update(deltaTime);
        }

        // Удаление просроченных задач
        var expiredTasks = _gameState.Tasks.Where(t => t.IsExpired).ToList();
        foreach (var task in expiredTasks)
        {
            _gameState.Player.RemoveEnergy(10f);
            _gameState.Player.AddScore(-task.GetPenalty());
            _gameState.Player.TaskStreak = 0;
            _gameState.Tasks.Remove(task);
        }

        // Спавн новых задач
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
        var maxTime = 10f + _random.Next(10); // 10-20 секунд

        var task = new WorkTask(_nextTaskId++, type, maxTime)
        {
            X = 200 + _random.Next(400),
            Y = 50 + _random.Next(300)
        };

        _gameState.Tasks.Add(task);
    }

    private void UpdatePickups()
    {
        // Удаление собранных
        _gameState.Pickups.RemoveAll(p => p.IsCollected);

        // Спавн
        _pickupSpawnTimer += 0.016f; // примерно 1 кадр
        if (_pickupSpawnTimer >= PickupSpawnInterval)
        {
            SpawnPickup();
            _pickupSpawnTimer = 0;
        }
    }

    private void SpawnPickup()
    {
        var type = _random.Next(100) < 70 ? PickupType.Coffee : PickupType.Food;
        var pickup = new Pickup(_nextPickupId++, type, 200 + _random.Next(400), 50 + _random.Next(300));
        _gameState.Pickups.Add(pickup);
    }

    private void UpdateBoss(float deltaTime)
    {
        var boss = _gameState.Boss;

        if (!boss.IsActive)
        {
            boss.Update(deltaTime);
            return;
        }

        // Если игрок не в кабинке - преследовать
        if (!_gameState.Player.IsInBooth)
        {
            boss.Chase(_gameState.Player.X, _gameState.Player.Y, deltaTime);
        }
        else
        {
            // Игрок в кабинке - вернуться к патрулированию
            boss.StartReturning();
            boss.Update(deltaTime);
        }
    }

    private void UpdateSurvivalScore(float deltaTime)
    {
        _survivalScoreTimer += deltaTime;
        if (_survivalScoreTimer >= SurvivalScoreInterval)
        {
            _gameState.Player.AddScore(10);
            _survivalScoreTimer = 0;
        }
    }

    private void CheckCollisions()
    {
        var player = _gameState.Player;

        // Проверка кабинки
        if (_gameState.PlayerBooth.ContainsPoint(player.X, player.Y))
        {
            player.IsInBooth = true;
        }
        else
        {
            player.IsInBooth = false;
        }

        // Проверка pickup'ов
        foreach (var pickup in _gameState.Pickups)
        {
            if (pickup.IsCollected) continue;

            float dx = player.X - pickup.X;
            float dy = player.Y - pickup.Y;
            float dist = MathF.Sqrt(dx * dx + dy * dy);

            if (dist < 30) // радиус сбора
            {
                pickup.IsCollected = true;
                player.AddEnergy(pickup.EnergyRestore);
            }
        }
    }

    private void CheckGameOver()
    {
        var player = _gameState.Player;

        // Уволили за низкую энергию
        if (player.Energy <= 0)
        {
            _gameState.State = GameState.GameOver;
            _gameState.GameOverReason = "Вас уволили за низкую продуктивность!";
            return;
        }

        // Поймал начальник
        if (_gameState.Boss.IsActive && !player.IsInBooth)
        {
            float dx = player.X - _gameState.Boss.X;
            float dy = player.Y - _gameState.Boss.Y;
            float dist = MathF.Sqrt(dx * dx + dy * dy);

            if (dist < 25) // радиус поимки
            {
                _gameState.State = GameState.GameOver;
                _gameState.GameOverReason = "Вас поймал начальник без дела!";
            }
        }
    }

    public void MovePlayer(float targetX, float targetY)
    {
        var player = _gameState.Player;
        float speed = 150f; // скорость игрока

        float dx = targetX - player.X;
        float dy = targetY - player.Y;
        float dist = MathF.Sqrt(dx * dx + dy * dy);

        if (dist > 5)
        {
            player.X += (dx / dist) * speed * 0.016f; // примерно 1 кадр
            player.Y += (dy / dist) * speed * 0.016f;
        }
    }

    public void CompleteTask(int taskId)
    {
        var task = _gameState.Tasks.FirstOrDefault(t => t.Id == taskId);
        if (task == null || task.IsCompleted) return;

        task.IsCompleted = true;

        var player = _gameState.Player;
        player.TaskStreak++;

        int points = task.GetPoints();
        if (player.TaskStreak >= 3)
        {
            points = (int)(points * 1.5f); // бонус за серию
        }

        player.AddScore(points);
        _gameState.Tasks.Remove(task);
    }

    public void HideInBooth()
    {
        var booth = _gameState.PlayerBooth;
        _gameState.Player.X = booth.X + booth.Width / 2;
        _gameState.Player.Y = booth.Y + booth.Height / 2;
        _gameState.Player.IsInBooth = true;
    }
}
