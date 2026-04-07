using System.Windows.Forms;
using CoffeeRush.Models;
using CoffeeRush.Services;

namespace CoffeeRush.Forms;

public class GameForm : Form
{
    private readonly GameEngine _gameEngine;
    private System.Windows.Forms.Timer _gameTimer;

    private bool _keyW = false, _keyA = false, _keyS = false, _keyD = false;

    private Label _scoreLabel = null!;
    private Label _energyLabel = null!;
    private Label _timeLabel = null!;
    private Label _bossWarningLabel = null!;
    private Label _gameOverLabel = null!;
    private Button _startButton = null!;
    private Button _restartButton = null!;
    private Panel _gamePanel = null!;
    private ProgressBar _energyBar = null!;

    // Эмодзи
    private const string EMOJI_TASK = "\U0001F4CB";   // 📋
    private const string EMOJI_COFFEE = "\U0001F375"; // ☕
    private const string EMOJI_FOOD = "\U0001F354";   // 🍔
    private const string EMOJI_BOSS = "\U0001F454";   // 👔

    public GameForm()
    {
        _gameEngine = new GameEngine();
        InitializeComponents();
        StartTimers();
    }

    private void InitializeComponents()
    {
        Text = "Coffee Rush";
        Size = new Size(1000, 750);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Color.FromArgb(45, 45, 48);
        KeyPreview = true;
        MinimumSize = new Size(900, 650);

        _gamePanel = new Panel { Location = new Point(0, 0), Size = new Size(1000, 600), BackColor = Color.FromArgb(60, 60, 65), BorderStyle = BorderStyle.FixedSingle };
        Controls.Add(_gamePanel);

        var uiPanel = new Panel { Location = new Point(0, 600), Size = new Size(1000, 150), BackColor = Color.FromArgb(35, 35, 40) };
        Controls.Add(uiPanel);

        _scoreLabel = new Label { Text = "Очки: 0", Location = new Point(20, 15), Size = new Size(250, 30), ForeColor = Color.White, Font = new Font("Segoe UI", 14, FontStyle.Bold) };
        uiPanel.Controls.Add(_scoreLabel);

        _energyBar = new ProgressBar { Location = new Point(20, 55), Size = new Size(250, 20), Maximum = 100, Value = 100, Style = ProgressBarStyle.Continuous };
        _energyBar.BackColor = Color.FromArgb(60, 60, 60);
        uiPanel.Controls.Add(_energyBar);

        _energyLabel = new Label { Text = "Энергия: 100%", Location = new Point(280, 55), Size = new Size(150, 20), ForeColor = Color.Lime, Font = new Font("Segoe UI", 11) };
        uiPanel.Controls.Add(_energyLabel);

        _timeLabel = new Label { Text = "Время: 00:00", Location = new Point(600, 15), Size = new Size(180, 30), ForeColor = Color.White, Font = new Font("Segoe UI", 14) };
        uiPanel.Controls.Add(_timeLabel);

        _bossWarningLabel = new Label { Text = "!!! НАЧАЛЬНИК ИДЕТ !!!", Location = new Point(600, 50), Size = new Size(250, 30), ForeColor = Color.Red, Font = new Font("Segoe UI", 14, FontStyle.Bold), Visible = false };
        uiPanel.Controls.Add(_bossWarningLabel);

        var helpLabel = new Label { Text = "WASD-ход | SPACE-спрятаться | E-выйти | J-выполнить задачу", Location = new Point(20, 100), Size = new Size(960, 25), ForeColor = Color.Gray, Font = new Font("Segoe UI", 11) };
        uiPanel.Controls.Add(helpLabel);

        _startButton = new Button { Text = "НАЧАТЬ РАБОТУ", Location = new Point(400, 250), Size = new Size(200, 60), BackColor = Color.FromArgb(0, 122, 204), ForeColor = Color.White, Font = new Font("Segoe UI", 16, FontStyle.Bold), FlatStyle = FlatStyle.Flat };
        _startButton.Click += (s, e) => StartGame();
        _startButton.KeyDown += (s, e) => { if (e.KeyCode == Keys.Space) e.SuppressKeyPress = true; };
        _gamePanel.Controls.Add(_startButton);

        _restartButton = new Button { Text = "Ещё раз", Location = new Point(400, 350), Size = new Size(200, 50), BackColor = Color.FromArgb(0, 122, 204), ForeColor = Color.White, Font = new Font("Segoe UI", 14), FlatStyle = FlatStyle.Flat, Visible = false };
        _restartButton.Click += (s, e) => StartGame();
        _gamePanel.Controls.Add(_restartButton);

        _gameOverLabel = new Label { Text = "", Location = new Point(250, 180), Size = new Size(500, 80), ForeColor = Color.OrangeRed, Font = new Font("Segoe UI", 16), TextAlign = ContentAlignment.MiddleCenter, Visible = false };
        _gamePanel.Controls.Add(_gameOverLabel);

        KeyDown += OnKeyDown;
        KeyUp += OnKeyUp;
    }

    private void StartTimers()
    {
        _gameTimer = new System.Windows.Forms.Timer { Interval = 50 };
        _gameTimer.Tick += GameTimer_Tick;
        _gameTimer.Start();
    }

    private void GameTimer_Tick(object? sender, EventArgs e)
    {
        var state = _gameEngine.GetState();
        
        if (state.State == GameState.Playing)
        {
            HandleMovement();
            _gameEngine.Update(0.05f);
            
            state = _gameEngine.GetState();
            _scoreLabel.Text = $"Очки: {state.Player.Score} | Задач: {state.Tasks.Count}";
            UpdateUI(state);
            
            using (var g = _gamePanel.CreateGraphics()) DrawGame(g, state);
            
            if (state.State == GameState.GameOver) ShowGameOver(state);
        }
    }

    private void HandleMovement()
    {
        var state = _gameEngine.GetState();
        if (state.Player.IsInBooth) return;

        float speed = 5f;
        float x = state.Player.X;
        float y = state.Player.Y;

        if (_keyW) y -= speed;
        if (_keyS) y += speed;
        if (_keyA) x -= speed;
        if (_keyD) x += speed;

        x = Math.Max(20, Math.Min(980, x));
        y = Math.Max(20, Math.Min(580, y));

        _gameEngine.MovePlayerTo(x, y);
    }

    private void StartGame()
    {
        _gameEngine.StartNewGame();
        _gameOverLabel.Visible = false;
        _restartButton.Visible = false;
        _startButton.Visible = false;
    }

    private void DrawGame(Graphics g, GameStateData state)
    {
        g.Clear(Color.FromArgb(60, 60, 65));

        var booth = state.PlayerBooth;
        g.FillRectangle(new SolidBrush(Color.FromArgb(80, 80, 90)), booth.X, booth.Y, booth.Width, booth.Height);
        g.DrawRectangle(new Pen(Color.FromArgb(100, 100, 110), 2), booth.X, booth.Y, booth.Width, booth.Height);

        foreach (var task in state.Tasks)
        {
            Color taskColor = task.Type switch { TaskType.CodeReview => Color.Cyan, TaskType.BugFix => Color.Red, TaskType.Documentation => Color.Yellow, TaskType.Meeting => Color.Magenta, TaskType.Deploy => Color.Lime, _ => Color.White };
            float timerPercent = task.TimeRemaining / task.MaxTime;
            g.FillRectangle(new SolidBrush(Color.Gray), task.X - 25, task.Y - 35, 50, 8);
            g.FillRectangle(new SolidBrush(timerPercent > 0.3f ? Color.Lime : Color.Red), task.X - 25, task.Y - 35, 50 * timerPercent, 8);
            g.FillEllipse(new SolidBrush(taskColor), task.X - 15, task.Y - 15, 30, 30);
            g.DrawString(EMOJI_TASK, new Font("Segoe UI", 16), Brushes.White, task.X - 12, task.Y - 12);
        }

        foreach (var pickup in state.Pickups)
        {
            string emoji = pickup.Type == PickupType.Coffee ? EMOJI_COFFEE : EMOJI_FOOD;
            Color color = pickup.Type == PickupType.Coffee ? Color.FromArgb(139, 69, 19) : Color.Orange;
            g.FillEllipse(new SolidBrush(color), pickup.X - 12, pickup.Y - 12, 24, 24);
            g.DrawString(emoji, new Font("Segoe UI", 14), Brushes.White, pickup.X - 10, pickup.Y - 10);
        }

        var player = state.Player;
        Color playerColor = player.IsInBooth ? Color.FromArgb(100, 200, 100) : Color.FromArgb(100, 150, 255);
        g.FillEllipse(new SolidBrush(playerColor), player.X - 15, player.Y - 15, 30, 30);

        if (state.Boss.IsActive)
        {
            g.FillEllipse(new SolidBrush(Color.Red), state.Boss.X - 20, state.Boss.Y - 20, 40, 40);
            g.DrawString(EMOJI_BOSS, new Font("Segoe UI", 20), Brushes.White, state.Boss.X - 12, state.Boss.Y - 12);
        }
    }

    private void UpdateUI(GameStateData state)
    {
        var player = state.Player;
        int energy = (int)player.Energy;
        _energyBar.Value = Math.Max(0, energy);
        _energyLabel.Text = $"Энергия: {energy}%";
        
        if (energy > 50) _energyLabel.ForeColor = Color.Lime;
        else if (energy > 25) _energyLabel.ForeColor = Color.Orange;
        else _energyLabel.ForeColor = Color.Red;

        int totalSeconds = (int)state.GameTime;
        int minutes = totalSeconds / 60;
        int seconds = totalSeconds % 60;
        _timeLabel.Text = $"Время: {minutes:D2}:{seconds:D2}";

        _bossWarningLabel.Visible = state.Boss.IsActive && !state.Player.IsInBooth;
    }

    private void ShowGameOver(GameStateData state)
    {
        _gameOverLabel.Text = state.GameOverReason + "\nИтоговый счёт: " + state.Player.Score;
        _gameOverLabel.Visible = true;
        _restartButton.Visible = true;
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        switch (e.KeyCode)
        {
            case Keys.W: _keyW = true; break;
            case Keys.A: _keyA = true; break;
            case Keys.S: _keyS = true; break;
            case Keys.D: _keyD = true; break;
            case Keys.Space: _gameEngine.HideInBooth(); e.SuppressKeyPress = true; break;
            case Keys.E: _gameEngine.LeaveBooth(); break;
            case Keys.J: CompleteNearestTask(); break;
        }
    }

    private void OnKeyUp(object? sender, KeyEventArgs e)
    {
        switch (e.KeyCode)
        {
            case Keys.W: _keyW = false; break;
            case Keys.A: _keyA = false; break;
            case Keys.S: _keyS = false; break;
            case Keys.D: _keyD = false; break;
        }
    }

    private void CompleteNearestTask()
    {
        var state = _gameEngine.GetState();
        if (state.Player.IsInBooth) return;

        var player = state.Player;
        WorkTask? nearest = null;
        float minDist = 50;

        foreach (var task in state.Tasks)
        {
            float dx = player.X - task.X;
            float dy = player.Y - task.Y;
            float dist = MathF.Sqrt(dx * dx + dy * dy);
            if (dist < minDist) { minDist = dist; nearest = task; }
        }

        if (nearest != null) _gameEngine.CompleteTask(nearest.Id);
    }

    protected override void OnFormClosing(FormClosingEventArgs e) { _gameTimer?.Stop(); base.OnFormClosing(e); }
}