using System.Text.Json;
using CoffeeRush.Models;

namespace CoffeeRush.Services;

public class LeaderboardService
{
    private const int MaxEntries = 10;
    private readonly string _filePath;

    public LeaderboardService(string? dataDirectory = null)
    {
        var dataDir = dataDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "CoffeeRush");
        Directory.CreateDirectory(dataDir);
        _filePath = Path.Combine(dataDir, "leaderboard.json");
    }

    public List<LeaderboardEntry> LoadEntries()
    {
        if (!File.Exists(_filePath))
        {
            return [];
        }

        try
        {
            var json = File.ReadAllText(_filePath);
            var entries = JsonSerializer.Deserialize<List<LeaderboardEntry>>(json);
            return NormalizeEntries(entries ?? []);
        }
        catch
        {
            return [];
        }
    }

    public List<LeaderboardEntry> AddEntry(string playerName, int score)
    {
        var trimmedName = playerName.Trim();
        if (string.IsNullOrWhiteSpace(trimmedName) || score <= 0)
        {
            return LoadEntries();
        }

        var entries = LoadEntries();
        var existingEntry = entries.FirstOrDefault(entry =>
            string.Equals(entry.PlayerName, trimmedName, StringComparison.OrdinalIgnoreCase));

        if (existingEntry is not null)
        {
            if (score <= existingEntry.Score)
            {
                return entries;
            }

            existingEntry.PlayerName = trimmedName;
            existingEntry.Score = score;
            existingEntry.AchievedAtUtc = DateTime.UtcNow;
        }
        else
        {
            entries.Add(new LeaderboardEntry
            {
                PlayerName = trimmedName,
                Score = score,
                AchievedAtUtc = DateTime.UtcNow
            });
        }

        entries = NormalizeEntries(entries);

        SaveEntries(entries);
        return entries;
    }

    private static List<LeaderboardEntry> NormalizeEntries(List<LeaderboardEntry> entries)
    {
        return entries
            .Where(entry => !string.IsNullOrWhiteSpace(entry.PlayerName) && entry.Score > 0)
            .GroupBy(entry => entry.PlayerName.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(group => group
                .OrderByDescending(entry => entry.Score)
                .ThenBy(entry => entry.AchievedAtUtc)
                .First())
            .OrderByDescending(entry => entry.Score)
            .ThenBy(entry => entry.AchievedAtUtc)
            .Take(MaxEntries)
            .ToList();
    }

    private void SaveEntries(List<LeaderboardEntry> entries)
    {
        var json = JsonSerializer.Serialize(entries, new JsonSerializerOptions
        {
            WriteIndented = true
        });
        File.WriteAllText(_filePath, json);
    }
}
