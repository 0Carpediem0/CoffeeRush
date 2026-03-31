using System.Windows.Forms;
using CoffeeRush.Models;
using CoffeeRush.Services;

namespace CoffeeRush.Forms;

public class GameForm : Form
{
    private readonly GameEngine _gameEngine;
    private System.Windows.Forms.Timer _gameTimer;
    private System.Windows.Forms.Timer _renderTimer;

    // Элементы UI
    private Label _scoreLabel = null!;
    private Label _energyLabel = null!;
    private Label _timeLabel = null!;
    private Label _bossAlertLabel = null!;
    private Label _gameOverLabel = null!;
    private Button _startButton = null!;
    private Button _restartButton = null!;
    private Panel _gamePanel = null!;

    public GameForm()
    {
        _gameEngine = new GameEngine();
        InitializeComponents();
        StartTimers();
    }

    private void InitializeComponents()
    {
        Text = "Coffee Rush ☕";
        Size = new Size(800, 600);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Color.FromArgb(45, 45, 48);

        // Панель игры
        _gamePanel = new Panel
        {
            Location = new Point(0, 0),
            Size = new Size(800, 500),
            BackColor = Color.FromArgb(60, 60, 65),
            BorderStyle = BorderStyle.FixedSingle
        };
        Controls.Add(_gamePanel);

        // UI панель
        var uiPanel = new Panel
        {
            Location = new Point(0, 500),
            Size = new Size(800, 100),
            BackColor = Color.FromArgb(35, 35, 40)
        };
        Controls.Add(uiPanel);

        _scoreLabel = new Label
        {
            Text = "Очки: 0",
            Location = new Point(20, 20),
            Size = new Size(150, 30),
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 14, FontStyle.Bold)
        };
        uiPanel.Controls.Add(_scoreLabel);

        _energyLabel = new Label
        {
            Text = "Энергия: 100%",
            Location = new Point(200, 20),
            Size = new Size(200, 30),
            ForeColor = Color.Lime,
            Font = new Font("Segoe UI", 14, FontStyle.Bold)
        };
        uiPanel.Controls.Add(_energyLabel);

        _timeLabel = new Label
        {
            Text = "Время: 00:00",
            Location = new Point(450, 20),
            Size = new Size(150, 30),
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 14)
        };
        uiPanel.Controls.Add(_timeLabel);

        _bossAlertLabel = new Label
        {
            Text = "⚠️ НАЧАЛЬНИК!",
            Location = new Point(300, 200),
            Size = new Size(200, 50),
            ForeColor = Color.Red,
            Font = new Font("Segoe UI", 24, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleCenter,
            Visible = false
        };
        _gamePanel.Controls.Add(_bossAlertLabel);

        // Кнопки
        _startButton = new Button
        {
            Text = "НАЧАТЬ РАБОТУ",
            Location = new Point(300, 200),
            Size = new Size(200, 60),
            BackColor = Color.FromArgb(0, 122, 204),
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 16, FontStyle.Bold),
            FlatStyle = FlatStyle.Flat
        };
        _startButton.Click += (s, e) => StartGame();
        _gamePanel.Controls.Add(_startButton);

        _restartButton = new Button
        {
            Text = "Ещё раз",
            Location = new Point(300, 300),
            Size = new Size(200, 50),
            BackColor = Color.FromArgb(0, 122, 204),
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 14),
            FlatStyle = FlatStyle.Flat,
            Visible = false
        };
        _restartButton.Click += (s, e) => StartGame();
        _gamePanel.Controls.Add(_restartButton);

        _gameOverLabel = new Label
        {
            Text = "",
            Location = new Point(150, 150),
            Size = new Size(500, 60),
            ForeColor = Color.OrangeRed,
            Font = new Font("Segoe UI", 18),
            TextAlign = ContentAlignment.MiddleCenter,
            Visible = false
        };
        _gamePanel.Controls.Add(_gameOverLabel);

        // Обработка мыши
        MouseMove += OnMouseMove;
        MouseClick += OnMouseClick;
        KeyDown += OnKeyDown;
    }

    private void StartTimers()
    {
        _gameTimer = new System.Windows.Forms.Timer { Interval = 16 }; // ~60 FPS
        _gameTimer.Tick += (s, e) => GameLoop();
        _gameTimer.Start();

        _renderTimer = new System.Windows.Forms.Timer { Interval = 16 };
        _renderTimer.Tick += (s, e) => Render();
    }

    private void GameLoop()
    {
        _gameEngine.Update(0.016f);
    }

    private void Render()
    {
        var state = _gameEngine.GetState();

        if (state.State == GameState.Menu)
        {
            _startButton.Visible = true;
            return;
        }

        _startButton.Visible = false;

        // Перерисовать панель
        _gamePanel.Invalidate();

        using (var g = _gamePanel.CreateGraphics())
        {
            DrawGame(g, state);
        }

        // Обновить UI
        UpdateUI(state);

        if (state.State == GameState.GameOver)
        {
            ShowGameOver(state);
        }
    }

    private void DrawGame(Graphics g, GameStateData state)
    {
        // Очистка
        g.Clear(Color.FromArgb(60, 60, 65));

        // Кабинка игрока
        var booth = state.PlayerBooth;
        g.FillRectangle(new SolidBrush(Color.FromArgb(80, 80, 90)), booth.X, booth.Y, booth.Width, booth.Height);
        g.DrawRectangle(new Pen(Color.FromArgb(100, 100, 110), 2), booth.X, booth.Y, booth.Width, booth.Height);

        // Задачи
        foreach (var task in state.Tasks)
        {
            Color taskColor = task.Type switch
            {
                TaskType.CodeReview => Color.Cyan,
                TaskType.BugFix => Color.Red,
                TaskType.Documentation => Color.Yellow,
                TaskType.Meeting => Color.Magenta,
                TaskType.Deploy => Color.Lime,
                _ => Color.White
            };

            // Таймер задачи (полоска)
            float timerPercent = task.TimeRemaining / task.MaxTime;
            g.FillRectangle(new SolidBrush(Color.Gray), task.X - 25, task.Y - 35, 50, 8);
            g.FillRectangle(new SolidBrush(timerPercent > 0.3f ? Color.Lime : Color.Red),
                task.X - 25, task.Y - 35, 50 * timerPercent, 8);

            g.FillEllipse(new SolidBrush(taskColor), task.X - 15, task.Y - 15, 30, 30);
            g.DrawString("📋", new Font("Segoe UI", 16), Brushes.White, task.X - 12, task.Y - 12);
        }

        // Пикапы (кофе, еда)
        foreach (var pickup in state.Pickups)
        {
            string emoji = pickup.Type == PickupType.Coffee ? "☕" : "🍔";
            Color color = pickup.Type == PickupType.Coffee ? Color.FromArgb(139, 69, 19) : Color.Orange;

            g.FillEllipse(new SolidBrush(color), pickup.X - 12, pickup.Y - 12, 24, 24);
            g.DrawString(emoji, new Font("Segoe UI", 14), Brushes.White, pickup.X - 10, pickup.Y - 10);
        }

        // Игрок
        var player = state.Player;
        Color playerColor = player.IsInBooth ? Color.FromArgb(100, 200, 100) : Color.FromArgb(100, 150, 255);
        g.FillEllipse(new SolidBrush(playerColor), player.X - 15, player.Y - 15, 30, 30);

        // Начальник
        if (state.Boss.IsActive)
        {
            g.FillEllipse(new SolidBrush(Color.Red), state.Boss.X - 20, state.Boss.Y - 20, 40, 40);
            g.DrawString("👔", new Font("Segoe UI", 20), Brushes.White, state.Boss.X - 12, state.Boss.Y - 12);
        }
    }

    private void UpdateUI(GameStateData state)
    {
        var player = state.Player;

        _scoreLabel.Text = $"Очки: {player.Score}";
        _energyLabel.Text = $"Энергия: {(int)player.Energy}%";

        if (player.Energy > 50)
            _energyLabel.ForeColor = Color.Lime;
        else if (player.Energy > 25)
            _energyLabel.ForeColor = Color.Orange;
        else
            _energyLabel.ForeColor = Color.Red;

        int minutes = (int)(state.GameTime / 60);
        int seconds = (int)(state.GameTime % 60);
        _timeLabel.Text = $"Время: {minutes:D2}:{seconds:D2}";

        _bossAlertLabel.Visible = state.Boss.IsActive && !state.Player.IsInBooth;
    }

    private void ShowGameOver(GameStateData state)
    {
        _gameOverLabel.Text = $"{state.GameOverReason}\nИтоговый счёт: {state.Player.Score}";
        _gameOverLabel.Visible = true;
        _restartButton.Visible = true;
    }

    private void StartGame()
    {
        _gameEngine.StartNewGame();
        _gameOverLabel.Visible = false;
        _restartButton.Visible = false;
    }

    private void OnMouseMove(object? sender, MouseEventArgs e)
    {
        if (_gameEngine.GetState().State != GameState.Playing)
            return;

        var state = _gameEngine.GetState();
        if (state.Player.IsInBooth)
            return;

        // Преобразование координат мыши в координаты панели
        Point panelPos = _gamePanel.PointToClient(Cursor.Position);
        _gameEngine.MovePlayer(panelPos.X, panelPos.Y);
    }

    private void OnMouseClick(object? sender, MouseEventArgs e)
    {
        if (_gameEngine.GetState().State != GameState.Playing)
            return;

        Point panelPos = _gamePanel.PointToClient(Cursor.Position);
        var state = _gameEngine.GetState();

        // Проверка клика на задачи
        foreach (var task in state.Tasks.ToList())
        {
            float dx = panelPos.X - task.X;
            float dy = panelPos.Y - task.Y;
            if (MathF.Sqrt(dx * dx + dy * dy) < 25)
            {
                _gameEngine.CompleteTask(task.Id);
                return;
            }
        }
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Space)
        {
            _gameEngine.HideInBooth();
        }
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        _gameTimer?.Stop();
        _renderTimer?.Stop();
        base.OnFormClosing(e);
    }
}
