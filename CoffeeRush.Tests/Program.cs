using System.Text.Json;
using CoffeeRush.Models;
using CoffeeRush.Services;

namespace CoffeeRush.Tests;

internal static class Program
{
    private static readonly List<(string Name, Action Test)> Tests =
    [
        ("Player.AddEnergy clamps to 100", PlayerAddEnergyClampsTo100),
        ("Player.RemoveEnergy clamps to 0", PlayerRemoveEnergyClampsToZero),
        ("WorkTask update expires incomplete task", WorkTaskUpdateExpiresIncompleteTask),
        ("WorkTask returns configured rewards", WorkTaskReturnsConfiguredRewards),
        ("LeaderboardService keeps only best score per nickname", LeaderboardServiceKeepsBestScorePerNickname),
        ("LeaderboardService normalizes duplicate stored entries", LeaderboardServiceNormalizesStoredDuplicates),
        ("GameEngine.StartNewGame resets playable state", GameEngineStartNewGameResetsPlayableState),
        ("GameEngine.CompleteTask rewards score, streak and energy", GameEngineCompleteTaskRewardsPlayer),
        ("GameEngine survival bonus grows over time", GameEngineAwardsSurvivalScore),
        ("GameEngine booth enter and leave flow works", GameEngineBoothEnterAndLeaveWorks),
        ("GameEngine movement does not place player inside obstacle", GameEngineMovementBlocksObstacleOverlap)
    ];

    private static int Main()
    {
        int passed = 0;

        foreach (var (name, test) in Tests)
        {
            try
            {
                test();
                Console.WriteLine($"[PASS] {name}");
                passed++;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[FAIL] {name}");
                Console.WriteLine($"       {ex.Message}");
            }
        }

        Console.WriteLine();
        Console.WriteLine($"Passed {passed}/{Tests.Count} tests.");
        return passed == Tests.Count ? 0 : 1;
    }

    private static void PlayerAddEnergyClampsTo100()
    {
        var player = new Player { Energy = 95f };
        player.AddEnergy(20f);
        AssertEqual(100f, player.Energy, "Energy should be capped at 100.");
    }

    private static void PlayerRemoveEnergyClampsToZero()
    {
        var player = new Player { Energy = 4f };
        player.RemoveEnergy(10f);
        AssertEqual(0f, player.Energy, "Energy should not become negative.");
    }

    private static void WorkTaskUpdateExpiresIncompleteTask()
    {
        var task = new WorkTask(1, TaskType.Documentation, 5f);
        task.Update(6f);
        AssertTrue(task.IsExpired, "Task should expire after time runs out.");
    }

    private static void WorkTaskReturnsConfiguredRewards()
    {
        var task = new WorkTask(7, TaskType.Deploy, 16f);
        AssertEqual(140, task.GetPoints(), "Deploy score changed unexpectedly.");
        AssertEqual(55, task.GetPenalty(), "Deploy penalty changed unexpectedly.");
        AssertEqual(16f, WorkTask.GetRecommendedTime(TaskType.Deploy), "Deploy recommended time changed unexpectedly.");
    }

    private static void LeaderboardServiceKeepsBestScorePerNickname()
    {
        using var tempDir = new TempDirectory();
        var service = new LeaderboardService(tempDir.Path);

        service.AddEntry("Kirill", 100);
        var afterWorse = service.AddEntry("kirill", 80);
        AssertEqual(1, afterWorse.Count, "A worse duplicate score should not create another entry.");
        AssertEqual(100, afterWorse[0].Score, "A worse duplicate score should not replace the best score.");

        var afterBetter = service.AddEntry("KIRILL", 160);
        AssertEqual(1, afterBetter.Count, "A better duplicate score should still keep only one entry.");
        AssertEqual("KIRILL", afterBetter[0].PlayerName, "Stored nickname should reflect the latest best-scoring name variant.");
        AssertEqual(160, afterBetter[0].Score, "A better duplicate score should replace the previous best score.");
    }

    private static void LeaderboardServiceNormalizesStoredDuplicates()
    {
        using var tempDir = new TempDirectory();
        string leaderboardPath = System.IO.Path.Combine(tempDir.Path, "leaderboard.json");
        var rawEntries = new List<LeaderboardEntry>
        {
            new() { PlayerName = "Alpha", Score = 90, AchievedAtUtc = new DateTime(2026, 1, 1, 0, 0, 3, DateTimeKind.Utc) },
            new() { PlayerName = "alpha", Score = 120, AchievedAtUtc = new DateTime(2026, 1, 1, 0, 0, 2, DateTimeKind.Utc) },
            new() { PlayerName = "Bravo", Score = 110, AchievedAtUtc = new DateTime(2026, 1, 1, 0, 0, 1, DateTimeKind.Utc) }
        };
        File.WriteAllText(leaderboardPath, JsonSerializer.Serialize(rawEntries));

        var service = new LeaderboardService(tempDir.Path);
        var loaded = service.LoadEntries();

        AssertEqual(2, loaded.Count, "Stored duplicate nicknames should be collapsed into one best-score entry.");
        AssertEqual("alpha", loaded[0].PlayerName, "Highest score should stay on top after normalization.");
        AssertEqual(120, loaded[0].Score, "Highest duplicate score should be kept.");
        AssertEqual("Bravo", loaded[1].PlayerName, "Remaining entries should stay sorted after normalization.");
    }

    private static void GameEngineStartNewGameResetsPlayableState()
    {
        var engine = new GameEngine();
        engine.StartNewGame();
        var state = engine.GetState();

        AssertEqual(GameState.Playing, state.State, "New game should switch state to Playing.");
        AssertTrue(!string.IsNullOrWhiteSpace(state.LandscapeName), "Landscape name should be assigned.");
        AssertTrue(state.Obstacles.Count > 0, "Landscape should create obstacles.");
        AssertEqual(100f, state.Player.Energy, "Player energy should reset to full.");
        AssertTrue(!state.Player.IsInBooth, "Player should not start inside booth.");
        AssertEqual(0, state.Tasks.Count, "No tasks should exist immediately after reset.");
        AssertEqual(0, state.Pickups.Count, "No pickups should exist immediately after reset.");
    }

    private static void GameEngineCompleteTaskRewardsPlayer()
    {
        var engine = new GameEngine();
        engine.StartNewGame();
        var state = engine.GetState();
        state.Player.Energy = 90f;
        state.Player.TaskStreak = 2;
        state.Tasks.Clear();
        state.Tasks.Add(new WorkTask(1, TaskType.CodeReview, 15f));

        engine.CompleteTask(1);

        AssertEqual(0, state.Tasks.Count, "Completed task should be removed from the active list.");
        AssertEqual(3, state.Player.TaskStreak, "Task streak should increment after completion.");
        AssertEqual(95f, state.Player.Energy, "Completing a task should restore a bit of energy.");
        AssertEqual(135, state.Player.Score, "Third task in a streak should apply the score multiplier.");
    }

    private static void GameEngineAwardsSurvivalScore()
    {
        var engine = new GameEngine();
        engine.StartNewGame();
        var state = engine.GetState();

        engine.Update(10f);
        AssertEqual(10, state.Player.Score, "First survival interval should grant the base time bonus.");

        engine.Update(10f);
        AssertEqual(22, state.Player.Score, "Second survival interval should grant a larger time-based bonus.");
    }

    private static void GameEngineBoothEnterAndLeaveWorks()
    {
        var engine = new GameEngine();
        engine.StartNewGame();
        var state = engine.GetState();
        var booth = state.PlayerBooth;

        engine.HideInBooth();
        AssertTrue(state.Player.IsInBooth, "HideInBooth should mark the player as hidden.");
        AssertTrue(booth.ContainsPoint(state.Player.X, state.Player.Y), "Player should move inside the booth.");

        engine.LeaveBooth();
        AssertTrue(!state.Player.IsInBooth, "LeaveBooth should mark the player as outside of the booth.");
        AssertTrue(state.Player.X > booth.X + booth.Width, "Player should be moved outside the booth exit.");
    }

    private static void GameEngineMovementBlocksObstacleOverlap()
    {
        var engine = new GameEngine();
        engine.StartNewGame();
        var state = engine.GetState();
        state.Obstacles =
        [
            new Obstacle { X = 100f, Y = 100f, Width = 80f, Height = 80f }
        ];
        state.Player.X = 70f;
        state.Player.Y = 140f;

        engine.MovePlayerTo(130f, 140f);

        AssertTrue(!state.Obstacles[0].IntersectsCircle(state.Player.X, state.Player.Y, 18f),
            "Player should not end up intersecting an obstacle after movement.");
    }

    private static void AssertTrue(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private static void AssertEqual<T>(T expected, T actual, string message)
        where T : notnull
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new InvalidOperationException($"{message} Expected: {expected}. Actual: {actual}.");
        }
    }

    private sealed class TempDirectory : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "CoffeeRushTests", Guid.NewGuid().ToString("N"));

        public TempDirectory()
        {
            Directory.CreateDirectory(Path);
        }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
