using System.Drawing.Drawing2D;
using System.Windows.Forms;
using CoffeeRush.Models;
using CoffeeRush.Services;

namespace CoffeeRush.Forms;

public class GameForm : Form
{
    private const int WorldWidth = 1000;
    private const int WorldHeight = 600;
    private const float WorldMargin = 20f;
    private const float PlayerSpeed = 150f;

    private readonly GameEngine _gameEngine;
    private readonly System.Windows.Forms.Timer _gameTimer;
    private readonly Font _hudFont = new("Segoe UI", 12, FontStyle.Bold);
    private readonly Font _hudValueFont = new("Segoe UI", 11, FontStyle.Regular);
    private readonly Font _helpFont = new("Segoe UI", 10, FontStyle.Regular);
    private readonly Font _overlayBodyFont = new("Segoe UI", 12, FontStyle.Regular);

    private bool _keyW;
    private bool _keyA;
    private bool _keyS;
    private bool _keyD;
    private bool _isFullscreen;

    private FormBorderStyle _restoreBorderStyle;
    private Rectangle _restoreBounds;
    private FormWindowState _restoreWindowState;

    private Label _scoreLabel = null!;
    private Label _energyLabel = null!;
    private Label _timeLabel = null!;
    private Label _landscapeLabel = null!;
    private Label _bossWarningLabel = null!;
    private Label _hintLabel = null!;
    private Label _gameOverLabel = null!;
    private Button _startButton = null!;
    private Button _restartButton = null!;
    private GameCanvas _gamePanel = null!;
    private ProgressBar _energyBar = null!;

    private Image _playerSprite = null!;
    private Image _bossSprite = null!;
    private Image _coffeeSprite = null!;
    private Image _foodSprite = null!;
    private Image _jobSprite = null!;

    public GameForm()
    {
        _gameEngine = new GameEngine();
        _gameTimer = new System.Windows.Forms.Timer { Interval = 50 };
        _gameTimer.Tick += GameTimer_Tick;

        LoadSprites();
        InitializeComponents();
        UpdateMenuOverlay();

        DoubleBuffered = true;
        KeyPreview = true;
    }

    private void LoadSprites()
    {
        var spritePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "sprites");
        _playerSprite = LoadTransparentSprite(Path.Combine(spritePath, "player.png"));
        _bossSprite = LoadTransparentSprite(Path.Combine(spritePath, "boss.png"));
        _coffeeSprite = LoadTransparentSprite(Path.Combine(spritePath, "coffee.png"));
        _foodSprite = LoadTransparentSprite(Path.Combine(spritePath, "food.png"));
        _jobSprite = LoadTransparentSprite(Path.Combine(spritePath, "job.png"));
    }

    private static Bitmap LoadTransparentSprite(string path)
    {
        using var source = new Bitmap(path);
        var sprite = new Bitmap(source.Width, source.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);

        using (var graphics = Graphics.FromImage(sprite))
        {
            graphics.DrawImage(source, 0, 0, source.Width, source.Height);
        }

        sprite.MakeTransparent(Color.White);
        return sprite;
    }

    private void InitializeComponents()
    {
        Text = "Coffee Rush";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(1100, 760);
        ClientSize = new Size(1280, 860);
        BackColor = Color.FromArgb(28, 30, 36);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(28, 30, 36),
            ColumnCount = 1,
            RowCount = 2,
            Padding = new Padding(16, 16, 16, 20)
        };
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 190f));
        Controls.Add(root);

        _gamePanel = new GameCanvas
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 0, 0, 16),
            BackColor = Color.FromArgb(42, 45, 54)
        };
        _gamePanel.Paint += GamePanel_Paint;
        root.Controls.Add(_gamePanel, 0, 0);

        var uiCard = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(34, 37, 44),
            ColumnCount = 3,
            RowCount = 3,
            Padding = new Padding(18, 16, 18, 16),
            Margin = new Padding(0)
        };
        uiCard.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33f));
        uiCard.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33f));
        uiCard.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 34f));
        uiCard.RowStyles.Add(new RowStyle(SizeType.Absolute, 42f));
        uiCard.RowStyles.Add(new RowStyle(SizeType.Absolute, 34f));
        uiCard.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        root.Controls.Add(uiCard, 0, 1);

        _scoreLabel = CreateHudLabel("Очки: 0", ContentAlignment.MiddleLeft);
        uiCard.Controls.Add(_scoreLabel, 0, 0);

        _timeLabel = CreateHudLabel("Время: 00:00", ContentAlignment.MiddleCenter);
        uiCard.Controls.Add(_timeLabel, 1, 0);

        _bossWarningLabel = CreateHudLabel("Начальник рядом", ContentAlignment.MiddleRight);
        _bossWarningLabel.ForeColor = Color.FromArgb(255, 110, 110);
        _bossWarningLabel.Visible = false;
        uiCard.Controls.Add(_bossWarningLabel, 2, 0);

        var energyPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            Margin = new Padding(0, 2, 18, 0)
        };
        energyPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        energyPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        uiCard.Controls.Add(energyPanel, 0, 1);

        _energyBar = new ProgressBar
        {
            Dock = DockStyle.Fill,
            Maximum = 100,
            Value = 100,
            Style = ProgressBarStyle.Continuous,
            Margin = new Padding(0, 4, 8, 4)
        };
        energyPanel.Controls.Add(_energyBar, 0, 0);

        _energyLabel = new Label
        {
            AutoSize = true,
            Dock = DockStyle.Fill,
            Text = "Энергия: 100%",
            ForeColor = Color.FromArgb(88, 214, 141),
            Font = _hudValueFont,
            TextAlign = ContentAlignment.MiddleRight
        };
        energyPanel.Controls.Add(_energyLabel, 1, 0);

        _landscapeLabel = CreateHudLabel("Карта: -", ContentAlignment.MiddleCenter);
        _landscapeLabel.Font = _hudValueFont;
        uiCard.Controls.Add(_landscapeLabel, 1, 1);

        _hintLabel = new Label
        {
            Dock = DockStyle.Fill,
            AutoEllipsis = true,
            Text = "WASD - движение   J - выполнить задачу рядом   Space - спрятаться в кабинке   E - выйти   F11 - полноэкранный режим",
            ForeColor = Color.FromArgb(187, 192, 201),
            Font = _helpFont,
            TextAlign = ContentAlignment.TopLeft,
            Margin = new Padding(0, 12, 0, 0)
        };
        uiCard.SetColumnSpan(_hintLabel, 3);
        uiCard.Controls.Add(_hintLabel, 0, 2);

        _startButton = CreateOverlayButton("НАЧАТЬ");
        _startButton.Size = new Size(260, 64);
        _startButton.Click += (_, _) => StartGame();
        _gamePanel.Controls.Add(_startButton);

        _restartButton = CreateOverlayButton("ЕЩЁ РАЗ");
        _restartButton.Visible = false;
        _restartButton.Click += (_, _) => StartGame();
        _gamePanel.Controls.Add(_restartButton);

        _gameOverLabel = new Label
        {
            AutoSize = false,
            Size = new Size(520, 120),
            Visible = false,
            ForeColor = Color.White,
            BackColor = Color.FromArgb(190, 24, 26, 32),
            Font = _overlayBodyFont,
            TextAlign = ContentAlignment.MiddleCenter,
            Padding = new Padding(20)
        };
        _gamePanel.Controls.Add(_gameOverLabel);

        Resize += (_, _) => RepositionOverlayControls();
        KeyDown += OnKeyDown;
        KeyUp += OnKeyUp;

        RepositionOverlayControls();
    }

    private Label CreateHudLabel(string text, ContentAlignment alignment)
    {
        return new Label
        {
            Dock = DockStyle.Fill,
            Text = text,
            ForeColor = Color.White,
            Font = _hudFont,
            TextAlign = alignment,
            Margin = new Padding(0)
        };
    }

    private Button CreateOverlayButton(string text)
    {
        var button = new Button
        {
            Text = text,
            Size = new Size(240, 56),
            BackColor = Color.FromArgb(67, 128, 255),
            FlatStyle = FlatStyle.Flat,
            ForeColor = Color.White,
            Font = _hudFont,
            TabStop = false
        };
        button.FlatAppearance.BorderSize = 0;
        return button;
    }

    private void GameTimer_Tick(object? sender, EventArgs e)
    {
        var state = _gameEngine.GetState();
        if (state.State != GameState.Playing)
        {
            _gamePanel.Invalidate();
            return;
        }

        const float deltaTime = 0.05f;
        _gameEngine.Update(deltaTime);
        HandleMovement(deltaTime);

        state = _gameEngine.GetState();
        _scoreLabel.Text = $"Очки: {state.Player.Score} | Задачи: {state.Tasks.Count}";
        UpdateUI(state);
        _gamePanel.Invalidate();

        if (state.State == GameState.GameOver)
        {
            ShowGameOver(state);
        }
    }

    private void HandleMovement(float deltaTime)
    {
        var state = _gameEngine.GetState();
        if (state.Player.IsInBooth)
        {
            return;
        }

        float x = state.Player.X;
        float y = state.Player.Y;

        if (_keyW) y -= PlayerSpeed * deltaTime;
        if (_keyS) y += PlayerSpeed * deltaTime;
        if (_keyA) x -= PlayerSpeed * deltaTime;
        if (_keyD) x += PlayerSpeed * deltaTime;

        x = Math.Clamp(x, WorldMargin, WorldWidth - WorldMargin);
        y = Math.Clamp(y, WorldMargin, WorldHeight - WorldMargin);

        _gameEngine.MovePlayerTo(x, y);
    }

    private void StartGame()
    {
        _gameEngine.StartNewGame();
        _gameOverLabel.Visible = false;
        _restartButton.Visible = false;
        _startButton.Visible = false;
        UpdateUI(_gameEngine.GetState());
        _gamePanel.Focus();
        _gameTimer.Start();
        _gamePanel.Invalidate();
    }

    private void GamePanel_Paint(object? sender, PaintEventArgs e)
    {
        var graphics = e.Graphics;
        graphics.Clear(_gamePanel.BackColor);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.InterpolationMode = InterpolationMode.NearestNeighbor;
        graphics.PixelOffsetMode = PixelOffsetMode.Half;

        var state = _gameEngine.GetState();
        if (state.State == GameState.Menu)
        {
            using var menuBrush = new SolidBrush(Color.FromArgb(52, 58, 70));
            graphics.FillRectangle(menuBrush, _gamePanel.ClientRectangle);
            return;
        }

        float scale = Math.Min(_gamePanel.ClientSize.Width / (float)WorldWidth, _gamePanel.ClientSize.Height / (float)WorldHeight);
        if (scale <= 0)
        {
            return;
        }

        float viewportWidth = WorldWidth * scale;
        float viewportHeight = WorldHeight * scale;
        float offsetX = (_gamePanel.ClientSize.Width - viewportWidth) / 2f;
        float offsetY = (_gamePanel.ClientSize.Height - viewportHeight) / 2f;

        using var viewportBrush = new SolidBrush(Color.FromArgb(60, 60, 65));
        graphics.FillRectangle(viewportBrush, offsetX, offsetY, viewportWidth, viewportHeight);

        var previousState = graphics.Save();
        graphics.TranslateTransform(offsetX, offsetY);
        graphics.ScaleTransform(scale, scale);

        DrawGame(graphics, state);

        graphics.Restore(previousState);

        using var borderPen = new Pen(Color.FromArgb(100, 108, 120), 2);
        graphics.DrawRectangle(borderPen, offsetX, offsetY, viewportWidth, viewportHeight);
    }

    private void DrawGame(Graphics g, GameStateData state)
    {
        g.Clear(Color.FromArgb(60, 60, 65));

        using var obstacleBrush = new SolidBrush(Color.FromArgb(88, 96, 108));
        using var obstacleAccentBrush = new SolidBrush(Color.FromArgb(62, 68, 78));
        using var obstaclePen = new Pen(Color.FromArgb(124, 132, 145), 2);

        foreach (var obstacle in state.Obstacles)
        {
            g.FillRectangle(obstacleBrush, obstacle.X, obstacle.Y, obstacle.Width, obstacle.Height);
            g.DrawRectangle(obstaclePen, obstacle.X, obstacle.Y, obstacle.Width, obstacle.Height);
            g.FillRectangle(obstacleAccentBrush, obstacle.X + 6, obstacle.Y + 6, Math.Max(8, obstacle.Width - 12), 10);
        }

        using var boothBrush = new SolidBrush(Color.FromArgb(80, 80, 90));
        using var boothPen = new Pen(Color.FromArgb(100, 100, 110), 2);
        var booth = state.PlayerBooth;
        g.FillRectangle(boothBrush, booth.X, booth.Y, booth.Width, booth.Height);
        g.DrawRectangle(boothPen, booth.X, booth.Y, booth.Width, booth.Height);

        foreach (var task in state.Tasks)
        {
            using var trackBrush = new SolidBrush(Color.FromArgb(95, 95, 100));
            using var fillBrush = new SolidBrush(task.TimeRemaining / task.MaxTime > 0.3f ? Color.Lime : Color.Red);
            float timerPercent = Math.Clamp(task.TimeRemaining / task.MaxTime, 0f, 1f);
            g.FillRectangle(trackBrush, task.X - 25, task.Y - 35, 50, 8);
            g.FillRectangle(fillBrush, task.X - 25, task.Y - 35, 50 * timerPercent, 8);
            g.DrawImage(_jobSprite, task.X - 22, task.Y - 22, 44, 44);
        }

        foreach (var pickup in state.Pickups)
        {
            var sprite = pickup.Type == PickupType.Coffee ? _coffeeSprite : _foodSprite;
            g.DrawImage(sprite, pickup.X - 16, pickup.Y - 16, 32, 32);
        }

        var player = state.Player;
        g.DrawImage(_playerSprite, player.X - 20, player.Y - 20, 40, 40);

        if (state.Boss.IsActive)
        {
            g.DrawImage(_bossSprite, state.Boss.X - 24, state.Boss.Y - 24, 48, 48);
        }
    }

    private void UpdateUI(GameStateData state)
    {
        var player = state.Player;
        int energy = Math.Clamp((int)player.Energy, 0, 100);
        _energyBar.Value = energy;
        _energyLabel.Text = $"Энергия: {energy}%";

        if (energy > 50)
        {
            _energyLabel.ForeColor = Color.FromArgb(88, 214, 141);
        }
        else if (energy > 25)
        {
            _energyLabel.ForeColor = Color.FromArgb(255, 196, 107);
        }
        else
        {
            _energyLabel.ForeColor = Color.FromArgb(255, 110, 110);
        }

        int totalSeconds = (int)state.GameTime;
        _timeLabel.Text = $"Время: {totalSeconds / 60:D2}:{totalSeconds % 60:D2}";
        _landscapeLabel.Text = $"Карта: {state.LandscapeName}";
        _bossWarningLabel.Visible = state.Boss.IsActive && !state.Player.IsInBooth;
    }

    private void ShowGameOver(GameStateData state)
    {
        _gameTimer.Stop();
        _gameOverLabel.Text = $"{state.GameOverReason}\r\nИтоговый счёт: {state.Player.Score}";
        _gameOverLabel.Visible = true;
        _restartButton.Visible = true;
        RepositionOverlayControls();
    }

    private void UpdateMenuOverlay()
    {
        _gameOverLabel.Visible = false;
        _startButton.Visible = true;
        _restartButton.Visible = false;
        _startButton.Size = new Size(260, 64);
        _startButton.Text = "НАЧАТЬ";
        RepositionOverlayControls();
    }

    private void RepositionOverlayControls()
    {
        if (_gamePanel is null)
        {
            return;
        }

        int centerX = _gamePanel.ClientSize.Width / 2;
        int centerY = _gamePanel.ClientSize.Height / 2;

        _gameOverLabel.Location = new Point(
            Math.Max(16, centerX - _gameOverLabel.Width / 2),
            Math.Max(24, centerY - 140));

        _startButton.Location = new Point(
            Math.Max(16, centerX - _startButton.Width / 2),
            Math.Max(24, centerY - _startButton.Height / 2));

        _restartButton.Location = new Point(
            Math.Max(16, centerX - _restartButton.Width / 2),
            Math.Max(24, centerY + 78));
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        switch (e.KeyCode)
        {
            case Keys.W:
                _keyW = true;
                break;
            case Keys.A:
                _keyA = true;
                break;
            case Keys.S:
                _keyS = true;
                break;
            case Keys.D:
                _keyD = true;
                break;
            case Keys.Space:
                _gameEngine.HideInBooth();
                e.SuppressKeyPress = true;
                break;
            case Keys.E:
                _gameEngine.LeaveBooth();
                break;
            case Keys.J:
                CompleteNearestTask();
                break;
            case Keys.F11:
                ToggleFullscreen();
                break;
        }
    }

    private void OnKeyUp(object? sender, KeyEventArgs e)
    {
        switch (e.KeyCode)
        {
            case Keys.W:
                _keyW = false;
                break;
            case Keys.A:
                _keyA = false;
                break;
            case Keys.S:
                _keyS = false;
                break;
            case Keys.D:
                _keyD = false;
                break;
        }
    }

    private void ToggleFullscreen()
    {
        if (!_isFullscreen)
        {
            _restoreBorderStyle = FormBorderStyle;
            _restoreBounds = Bounds;
            _restoreWindowState = WindowState;

            FormBorderStyle = FormBorderStyle.None;
            WindowState = FormWindowState.Normal;
            Bounds = Screen.FromControl(this).Bounds;
            TopMost = true;
            _isFullscreen = true;
        }
        else
        {
            TopMost = false;
            FormBorderStyle = _restoreBorderStyle;
            WindowState = FormWindowState.Normal;
            Bounds = _restoreBounds;
            WindowState = _restoreWindowState;
            _isFullscreen = false;
        }
    }

    private void CompleteNearestTask()
    {
        var state = _gameEngine.GetState();
        if (state.Player.IsInBooth)
        {
            return;
        }

        var player = state.Player;
        WorkTask? nearest = null;
        float minDist = 50f;

        foreach (var task in state.Tasks)
        {
            float dx = player.X - task.X;
            float dy = player.Y - task.Y;
            float dist = MathF.Sqrt(dx * dx + dy * dy);
            if (dist < minDist)
            {
                minDist = dist;
                nearest = task;
            }
        }

        if (nearest is not null)
        {
            _gameEngine.CompleteTask(nearest.Id);
        }
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        _gameTimer.Stop();
        _gameTimer.Dispose();

        _hudFont.Dispose();
        _hudValueFont.Dispose();
        _helpFont.Dispose();
        _overlayBodyFont.Dispose();
        _playerSprite.Dispose();
        _bossSprite.Dispose();
        _coffeeSprite.Dispose();
        _foodSprite.Dispose();
        _jobSprite.Dispose();

        base.OnFormClosing(e);
    }
}

internal sealed class GameCanvas : Panel
{
    public GameCanvas()
    {
        DoubleBuffered = true;
        ResizeRedraw = true;
    }
}
