using System.Drawing.Drawing2D;
using System.Media;
using System.Windows.Forms;
using CoffeeRush.Models;
using CoffeeRush.Services;

namespace CoffeeRush.Forms;

public class GameForm : Form
{
    private const int WorldWidth = 1000;
    private const int WorldHeight = 600;
    private const float WorldMargin = 20f;
    private const float PlayerSpeed = 125f;
    private const float TaskInteractionRadius = 64f;

    private readonly LeaderboardService _leaderboardService;
    private readonly GameEngine _gameEngine;
    private readonly System.Windows.Forms.Timer _gameTimer;
    private readonly Font _hudFont = new("Segoe UI", 12, FontStyle.Bold);
    private readonly Font _hudValueFont = new("Segoe UI", 11, FontStyle.Regular);
    private readonly Font _helpFont = new("Segoe UI", 10, FontStyle.Regular);
    private readonly Font _overlayBodyFont = new("Segoe UI", 12, FontStyle.Regular);
    private readonly Font _titleFont = new("Segoe UI", 24, FontStyle.Bold);
    private readonly Font _menuBodyFont = new("Segoe UI", 11, FontStyle.Regular);
    private readonly Font _taskButtonFont = new("Segoe UI", 10, FontStyle.Bold);
    private readonly string? _bgmPath;
    private readonly SoundPlayer? _coffeePickupSound;
    private readonly SoundPlayer? _foodPickupSound;
    private readonly SoundPlayer? _taskCompleteSound;
    private readonly SoundPlayer? _bossWarningSound;
    private readonly SoundPlayer? _menuSelectSound;

    private bool _keyW;
    private bool _keyA;
    private bool _keyS;
    private bool _keyD;
    private bool _isFullscreen;
    private bool _isPaused;
    private bool _isMusicPlaying;
    private bool _musicEnabled = true;
    private bool _soundEnabled = true;
    private bool _currentRunScoreSaved;
    private bool _lastBossActive;

    private string _currentPlayerName = "";
    private List<LeaderboardEntry> _leaderboardEntries;

    private FormBorderStyle _restoreBorderStyle;
    private Rectangle _restoreBounds;
    private FormWindowState _restoreWindowState;
    private dynamic? _bgmComPlayer;

    private Label _scoreLabel = null!;
    private Label _energyLabel = null!;
    private Label _timeLabel = null!;
    private Label _landscapeLabel = null!;
    private Label _bossWarningLabel = null!;
    private Label _hintLabel = null!;
    private Label _gameOverLabel = null!;
    private Label _nicknameLabel = null!;
    private Label _leaderboardLabel = null!;
    private Button _startButton = null!;
    private Button _restartButton = null!;
    private Button _resumeButton = null!;
    private Button _exitButton = null!;
    private TextBox _nicknameTextBox = null!;
    private GameCanvas _gamePanel = null!;
    private TableLayoutPanel _hudPanel = null!;
    private ProgressBar _energyBar = null!;
    private Panel _taskOverlay = null!;
    private Label _taskTitleLabel = null!;
    private Label _taskInstructionLabel = null!;
    private ProgressBar _taskProgressBar = null!;
    private Button _taskPrimaryButton = null!;
    private Button _taskSecondaryButton = null!;
    private Button _taskTertiaryButton = null!;
    private Button _taskCancelButton = null!;
    private TableLayoutPanel _rootLayout = null!;
    private Button _musicToggleButton = null!;
    private Button _soundToggleButton = null!;
    private Button _helpButton = null!;
    private Panel _helpOverlay = null!;
    private Label _helpOverlayTitleLabel = null!;
    private Label _helpOverlayBodyLabel = null!;
    private Button _helpOverlayCloseButton = null!;

    private Image _playerSprite = null!;
    private Image _bossSprite = null!;
    private Image _coffeeSprite = null!;
    private Image _foodSprite = null!;
    private Image _jobSprite = null!;

    private WorkTask? _activeTask;
    private TaskMiniGameKind _activeTaskMiniGame;
    private Keys[] _taskSequenceKeys = Array.Empty<Keys>();
    private string[] _taskSequenceLabels = Array.Empty<string>();
    private int _taskSequenceProgress;
    private int _taskTapTarget;
    private int _taskTapProgress;
    private string _taskChoiceAnswer = string.Empty;
    private string[] _taskChoices = Array.Empty<string>();

    public GameForm()
    {
        _leaderboardService = new LeaderboardService();
        _leaderboardEntries = _leaderboardService.LoadEntries();
        _gameEngine = new GameEngine();
        _gameTimer = new System.Windows.Forms.Timer { Interval = 50 };
        _gameTimer.Tick += GameTimer_Tick;

        LoadSprites();
        (_bgmPath, _coffeePickupSound, _foodPickupSound, _taskCompleteSound, _bossWarningSound, _menuSelectSound) = LoadAudio();
        InitializeComponents();
        ShowMenu();
        StartBackgroundMusic();

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

    private static (string? bgmPath, SoundPlayer? coffeePickup, SoundPlayer? foodPickup, SoundPlayer? taskComplete, SoundPlayer? bossWarning, SoundPlayer? menuSelect) LoadAudio()
    {
        var audioPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "audio");
        string? bgmPath = Path.Combine(audioPath, "bgm.wav");
        if (!File.Exists(bgmPath))
        {
            bgmPath = null;
        }

        return (
            bgmPath,
            TryCreateSoundPlayer(Path.Combine(audioPath, "coffee-pickup.wav")),
            TryCreateSoundPlayer(Path.Combine(audioPath, "food-pickup.wav")),
            TryCreateSoundPlayer(Path.Combine(audioPath, "task-complete.wav")),
            TryCreateSoundPlayer(Path.Combine(audioPath, "boss-warning.wav")),
            TryCreateSoundPlayer(Path.Combine(audioPath, "menu-select.wav")));
    }

    private static SoundPlayer? TryCreateSoundPlayer(string path)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            var player = new SoundPlayer(path);
            player.Load();
            return player;
        }
        catch
        {
            return null;
        }
    }

    private void InitializeComponents()
    {
        Text = "Coffee Rush";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(1100, 760);
        ClientSize = new Size(1280, 860);
        BackColor = Color.FromArgb(28, 30, 36);

        _rootLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(28, 30, 36),
            ColumnCount = 1,
            RowCount = 2,
            Padding = new Padding(16, 16, 16, 20)
        };
        _rootLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        _rootLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 190f));
        Controls.Add(_rootLayout);

        _gamePanel = new GameCanvas
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 0, 0, 16),
            BackColor = Color.FromArgb(42, 45, 54)
        };
        _gamePanel.Paint += GamePanel_Paint;
        _rootLayout.Controls.Add(_gamePanel, 0, 0);

        _hudPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(34, 37, 44),
            ColumnCount = 3,
            RowCount = 3,
            Padding = new Padding(18, 16, 18, 16),
            Margin = new Padding(0)
        };
        _hudPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33f));
        _hudPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33f));
        _hudPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 34f));
        _hudPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 42f));
        _hudPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 34f));
        _hudPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        _rootLayout.Controls.Add(_hudPanel, 0, 1);

        _scoreLabel = CreateHudLabel("Очки: 0", ContentAlignment.MiddleLeft);
        _hudPanel.Controls.Add(_scoreLabel, 0, 0);

        _timeLabel = CreateHudLabel("Время: 00:00", ContentAlignment.MiddleCenter);
        _hudPanel.Controls.Add(_timeLabel, 1, 0);

        _bossWarningLabel = CreateHudLabel("Начальник рядом", ContentAlignment.MiddleRight);
        _bossWarningLabel.ForeColor = Color.FromArgb(255, 110, 110);
        _bossWarningLabel.Visible = false;
        _hudPanel.Controls.Add(_bossWarningLabel, 2, 0);

        var energyPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            Margin = new Padding(0, 2, 18, 0)
        };
        energyPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        energyPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        _hudPanel.Controls.Add(energyPanel, 0, 1);

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
        _hudPanel.Controls.Add(_landscapeLabel, 1, 1);

        _hintLabel = new Label
        {
            Dock = DockStyle.Fill,
            AutoEllipsis = true,
            Text = "WASD - движение   J - начать задание рядом   Space - спрятаться   E - выйти из кабинки   Esc - пауза   F11 - полноэкранный режим",
            ForeColor = Color.FromArgb(187, 192, 201),
            Font = _helpFont,
            TextAlign = ContentAlignment.TopLeft,
            Margin = new Padding(0, 12, 0, 0)
        };
        _hudPanel.SetColumnSpan(_hintLabel, 3);
        _hudPanel.Controls.Add(_hintLabel, 0, 2);

        _startButton = CreateOverlayButton("НАЧАТЬ");
        _startButton.Size = new Size(260, 64);
        _startButton.Click += (_, _) => StartGame();
        _gamePanel.Controls.Add(_startButton);

        _restartButton = CreateOverlayButton("ЕЩЁ РАЗ");
        _restartButton.Visible = false;
        _restartButton.Click += (_, _) => StartGame();
        _gamePanel.Controls.Add(_restartButton);

        _resumeButton = CreateOverlayButton("ПРОДОЛЖИТЬ");
        _resumeButton.Visible = false;
        _resumeButton.Click += (_, _) => ResumeGame();
        _gamePanel.Controls.Add(_resumeButton);

        _exitButton = CreateOverlayButton("ВЫХОД");
        _exitButton.Size = new Size(220, 52);
        _exitButton.Click += (_, _) => HandleExitButton();
        _gamePanel.Controls.Add(_exitButton);

        _musicToggleButton = CreateOverlayButton("МУЗЫКА: ВКЛ");
        _musicToggleButton.Size = new Size(180, 44);
        _musicToggleButton.Font = _helpFont;
        _musicToggleButton.Click += (_, _) => ToggleMusic();
        _gamePanel.Controls.Add(_musicToggleButton);

        _soundToggleButton = CreateOverlayButton("ЗВУКИ: ВКЛ");
        _soundToggleButton.Size = new Size(180, 44);
        _soundToggleButton.Font = _helpFont;
        _soundToggleButton.Click += (_, _) => ToggleSound();
        _gamePanel.Controls.Add(_soundToggleButton);

        _helpButton = CreateOverlayButton("?");
        _helpButton.Size = new Size(44, 44);
        _helpButton.Font = _hudFont;
        _helpButton.Click += (_, _) => ShowHelpOverlay();
        _gamePanel.Controls.Add(_helpButton);

        _nicknameLabel = new Label
        {
            AutoSize = false,
            Size = new Size(130, 28),
            ForeColor = Color.White,
            BackColor = Color.Transparent,
            Font = _helpFont,
            Text = "Ник игрока:",
            TextAlign = ContentAlignment.MiddleLeft
        };
        _gamePanel.Controls.Add(_nicknameLabel);

        _nicknameTextBox = new TextBox
        {
            Size = new Size(250, 32),
            Font = _overlayBodyFont,
            MaxLength = 18,
            Text = "Игрок"
        };
        _nicknameTextBox.KeyDown += MenuNicknameTextBox_KeyDown;
        _gamePanel.Controls.Add(_nicknameTextBox);

        _leaderboardLabel = new Label
        {
            AutoSize = false,
            Size = new Size(270, 260),
            ForeColor = Color.White,
            BackColor = Color.Transparent,
            Font = _helpFont,
            TextAlign = ContentAlignment.TopLeft
        };
        _gamePanel.Controls.Add(_leaderboardLabel);

        _gameOverLabel = new Label
        {
            AutoSize = false,
            Size = new Size(560, 140),
            Visible = false,
            ForeColor = Color.White,
            BackColor = Color.FromArgb(190, 24, 26, 32),
            Font = _overlayBodyFont,
            TextAlign = ContentAlignment.MiddleCenter,
            Padding = new Padding(20)
        };
        _gamePanel.Controls.Add(_gameOverLabel);

        _taskOverlay = new Panel
        {
            Size = new Size(560, 320),
            Visible = false,
            BackColor = Color.FromArgb(236, 20, 22, 28)
        };
        _gamePanel.Controls.Add(_taskOverlay);

        _taskTitleLabel = new Label
        {
            Location = new Point(24, 20),
            Size = new Size(350, 36),
            Font = _taskButtonFont,
            ForeColor = Color.White,
            TextAlign = ContentAlignment.MiddleLeft
        };
        _taskOverlay.Controls.Add(_taskTitleLabel);

        _taskInstructionLabel = new Label
        {
            Location = new Point(24, 68),
            Size = new Size(512, 108),
            Font = _overlayBodyFont,
            ForeColor = Color.FromArgb(220, 225, 235),
            TextAlign = ContentAlignment.TopLeft,
            AutoEllipsis = true
        };
        _taskOverlay.Controls.Add(_taskInstructionLabel);

        _taskProgressBar = new ProgressBar
        {
            Location = new Point(24, 182),
            Size = new Size(512, 24),
            Maximum = 100,
            Value = 0,
            Style = ProgressBarStyle.Continuous
        };
        _taskOverlay.Controls.Add(_taskProgressBar);

        _taskPrimaryButton = CreateOverlayButton("1");
        _taskPrimaryButton.Location = new Point(24, 232);
        _taskPrimaryButton.Size = new Size(156, 52);
        _taskPrimaryButton.Click += (_, _) => HandleTaskButton(_taskPrimaryButton.Text);
        _taskOverlay.Controls.Add(_taskPrimaryButton);

        _taskSecondaryButton = CreateOverlayButton("2");
        _taskSecondaryButton.Location = new Point(202, 232);
        _taskSecondaryButton.Size = new Size(156, 52);
        _taskSecondaryButton.Click += (_, _) => HandleTaskButton(_taskSecondaryButton.Text);
        _taskOverlay.Controls.Add(_taskSecondaryButton);

        _taskTertiaryButton = CreateOverlayButton("3");
        _taskTertiaryButton.Location = new Point(380, 232);
        _taskTertiaryButton.Size = new Size(156, 52);
        _taskTertiaryButton.Click += (_, _) => HandleTaskButton(_taskTertiaryButton.Text);
        _taskOverlay.Controls.Add(_taskTertiaryButton);

        _taskCancelButton = CreateOverlayButton("ОТМЕНА");
        _taskCancelButton.Location = new Point(396, 20);
        _taskCancelButton.Size = new Size(140, 38);
        _taskCancelButton.Font = _helpFont;
        _taskCancelButton.Click += (_, _) => CancelTaskInteraction();
        _taskOverlay.Controls.Add(_taskCancelButton);

        _helpOverlay = new Panel
        {
            Size = new Size(620, 380),
            Visible = false,
            BackColor = Color.FromArgb(236, 20, 22, 28)
        };
        _gamePanel.Controls.Add(_helpOverlay);

        _helpOverlayTitleLabel = new Label
        {
            Location = new Point(24, 18),
            Size = new Size(420, 38),
            Font = _hudFont,
            ForeColor = Color.White,
            Text = "О игре и управлении",
            TextAlign = ContentAlignment.MiddleLeft
        };
        _helpOverlay.Controls.Add(_helpOverlayTitleLabel);

        _helpOverlayCloseButton = CreateOverlayButton("ЗАКРЫТЬ");
        _helpOverlayCloseButton.Location = new Point(456, 18);
        _helpOverlayCloseButton.Size = new Size(140, 38);
        _helpOverlayCloseButton.Font = _helpFont;
        _helpOverlayCloseButton.Click += (_, _) => HideHelpOverlay();
        _helpOverlay.Controls.Add(_helpOverlayCloseButton);

        _helpOverlayBodyLabel = new Label
        {
            Location = new Point(24, 72),
            Size = new Size(572, 280),
            Font = _overlayBodyFont,
            ForeColor = Color.FromArgb(220, 225, 235),
            TextAlign = ContentAlignment.TopLeft
        };
        _helpOverlay.Controls.Add(_helpOverlayBodyLabel);

        Resize += (_, _) => RepositionOverlayControls();
        KeyDown += OnKeyDown;
        KeyUp += OnKeyUp;

        RepositionOverlayControls();
        UpdateAudioToggleButtons();
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

        EnsureBackgroundMusic();

        const float deltaTime = 0.05f;
        _gameEngine.Update(deltaTime);
        HandleMovement(deltaTime);

        state = _gameEngine.GetState();
        PlayStateDrivenSounds(state);
        _scoreLabel.Text = $"Очки: {state.Player.Score} | Задачи: {state.Tasks.Count}";
        UpdateUI(state);
        _gamePanel.Invalidate();

        if (state.State == GameState.GameOver)
        {
            CancelTaskInteraction(resumeGame: false);
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
        var nickname = _nicknameTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(nickname))
        {
            _nicknameLabel.ForeColor = Color.FromArgb(255, 110, 110);
            _nicknameLabel.Text = "Введите ник:";
            _nicknameTextBox.Focus();
            return;
        }

        _nicknameLabel.ForeColor = Color.White;
        _nicknameLabel.Text = "Ник игрока:";
        RestartBackgroundMusic();
        CancelTaskInteraction(resumeGame: false);
        _isPaused = false;
        _currentPlayerName = nickname;
        _currentRunScoreSaved = false;
        _gameEngine.StartNewGame();
        _gameEngine.GetState().State = GameState.Playing;
        _lastBossActive = _gameEngine.GetState().Boss.IsActive;
        _gameOverLabel.Visible = false;
        SetGameLayout();
        _hudPanel.Visible = true;
        _nicknameLabel.Visible = false;
        _nicknameTextBox.Visible = false;
        _leaderboardLabel.Visible = false;
        _restartButton.Visible = false;
        _resumeButton.Visible = false;
        _startButton.Visible = false;
        _exitButton.Visible = false;
        _musicToggleButton.Visible = false;
        _soundToggleButton.Visible = false;
        _helpButton.Visible = false;
        _helpOverlay.Visible = false;
        UpdateUI(_gameEngine.GetState());
        _gamePanel.Focus();
        _gameTimer.Start();
        _gamePanel.Invalidate();
    }

    private void PauseGame()
    {
        var state = _gameEngine.GetState();
        if (state.State != GameState.Playing || _taskOverlay.Visible)
        {
            return;
        }

        _isPaused = true;
        state.State = GameState.Paused;
        _gameTimer.Stop();
        _resumeButton.Visible = true;
        _exitButton.Visible = true;
        _exitButton.Text = "В МЕНЮ";
        _musicToggleButton.Visible = true;
        _soundToggleButton.Visible = true;
        _helpButton.Visible = true;
        RepositionOverlayControls();
        _gamePanel.Invalidate();
    }

    private void ResumeGame()
    {
        var state = _gameEngine.GetState();
        if (!_isPaused || state.State == GameState.GameOver)
        {
            return;
        }

        PlayEffect(_menuSelectSound);
        _isPaused = false;
        state.State = GameState.Playing;
        _resumeButton.Visible = false;
        _exitButton.Visible = false;
        _gameTimer.Start();
        _gamePanel.Focus();
        _gamePanel.Invalidate();
    }

    private void ShowMenu()
    {
        SaveCurrentRunIfNeeded();
        EnsureBackgroundMusic();
        _isPaused = false;
        CancelTaskInteraction(resumeGame: false);
        _gameTimer.Stop();
        var state = _gameEngine.GetState();
        state.State = GameState.Menu;
        _gameOverLabel.Visible = false;
        SetMenuLayout();
        _hudPanel.Visible = false;
        _nicknameLabel.Visible = true;
        _nicknameTextBox.Visible = true;
        _leaderboardLabel.Visible = true;
        _leaderboardLabel.Text = GetLeaderboardDisplayText();
        _restartButton.Visible = false;
        _resumeButton.Visible = false;
        _startButton.Visible = true;
        _exitButton.Visible = true;
        _exitButton.Text = "ВЫХОД";
        _musicToggleButton.Visible = true;
        _soundToggleButton.Visible = true;
        _helpButton.Visible = true;
        _helpOverlay.Visible = false;
        _lastBossActive = state.Boss.IsActive;
        UpdateUI(state);
        RepositionOverlayControls();
        _nicknameTextBox.Focus();
        _nicknameTextBox.SelectionStart = _nicknameTextBox.TextLength;
        _gamePanel.Invalidate();
    }

    private void HandleExitButton()
    {
        PlayEffect(_menuSelectSound);
        if (_exitButton.Text == "В МЕНЮ")
        {
            ShowMenu();
            return;
        }

        Close();
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
            DrawMenuScreen(graphics);
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

        DrawDangerFeedback(graphics, state, offsetX, offsetY, viewportWidth, viewportHeight);

        using var borderPen = new Pen(Color.FromArgb(100, 108, 120), 2);
        graphics.DrawRectangle(borderPen, offsetX, offsetY, viewportWidth, viewportHeight);

        if (_isPaused && !_taskOverlay.Visible)
        {
            DrawPauseOverlay(graphics);
        }
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

    private void DrawMenuScreen(Graphics graphics)
    {
        var leaderboardBounds = GetMenuLeaderboardBounds();

        using var backgroundBrush = new LinearGradientBrush(
            ClientRectangle,
            Color.FromArgb(36, 41, 52),
            Color.FromArgb(23, 27, 35),
            90f);
        graphics.FillRectangle(backgroundBrush, ClientRectangle);

        using var accentBrush = new SolidBrush(Color.FromArgb(36, 67, 128, 255));
        graphics.FillEllipse(accentBrush, -80, -40, 360, 220);
        graphics.FillEllipse(accentBrush, _gamePanel.ClientSize.Width - 280, _gamePanel.ClientSize.Height - 180, 320, 220);

        var titleRect = new Rectangle(80, 70, _gamePanel.ClientSize.Width - 160, 60);
        TextRenderer.DrawText(graphics, "Coffee Rush", _titleFont, titleRect, Color.White, TextFormatFlags.HorizontalCenter);

        var subtitleRect = new Rectangle(140, 138, _gamePanel.ClientSize.Width - 280, 78);
        TextRenderer.DrawText(
            graphics,
            "Вы офисный выживальщик: собирайте задачи, держите энергию и прячьтесь от начальника.\nВсе главные механики должны раскрыться за один короткий забег.",
            _menuBodyFont,
            subtitleRect,
            Color.FromArgb(225, 230, 240),
            TextFormatFlags.HorizontalCenter | TextFormatFlags.WordBreak);

        int controlsWidth = Math.Min(560, Math.Max(420, _gamePanel.ClientSize.Width - 520));
        int controlsX = (_gamePanel.ClientSize.Width - controlsWidth) / 2;
        var controlsCardRect = new Rectangle(controlsX - 24, 248, controlsWidth + 48, 220);
        using var controlsCardBrush = new SolidBrush(Color.FromArgb(96, 20, 24, 32));
        using var controlsCardPen = new Pen(Color.FromArgb(120, 78, 90, 110), 1.5f);
        graphics.FillRectangle(controlsCardBrush, controlsCardRect);
        graphics.DrawRectangle(controlsCardPen, controlsCardRect);

        var controlsRect = new Rectangle(controlsX, 270, controlsWidth, 176);
        TextRenderer.DrawText(
            graphics,
            "Управление\nWASD - движение\nJ - начать задание рядом\nSpace - спрятаться в кабинке\nE - выйти из кабинки\nEsc - пауза или назад\nEnter - начать игру",
            _menuBodyFont,
            controlsRect,
            Color.FromArgb(204, 210, 220),
            TextFormatFlags.HorizontalCenter | TextFormatFlags.WordBreak);

        var leaderboardTitleRect = new Rectangle(leaderboardBounds.X, 132, leaderboardBounds.Width, 34);
        TextRenderer.DrawText(graphics, "Лидерборд", _hudFont, leaderboardTitleRect, Color.White, TextFormatFlags.HorizontalCenter);
    }

    private void DrawPauseOverlay(Graphics graphics)
    {
        using var veilBrush = new SolidBrush(Color.FromArgb(170, 12, 14, 18));
        graphics.FillRectangle(veilBrush, _gamePanel.ClientRectangle);

        var titleRect = new Rectangle(0, 120, _gamePanel.ClientSize.Width, 44);
        TextRenderer.DrawText(graphics, "Пауза", _titleFont, titleRect, Color.White, TextFormatFlags.HorizontalCenter);

        var bodyRect = new Rectangle(160, 180, _gamePanel.ClientSize.Width - 320, 70);
        TextRenderer.DrawText(
            graphics,
            "Esc - продолжить\nEnter - продолжить\nQ - выйти в меню\nКнопки ниже: музыка, звуки и справка",
            _menuBodyFont,
            bodyRect,
            Color.FromArgb(220, 225, 235),
            TextFormatFlags.HorizontalCenter | TextFormatFlags.WordBreak);
    }

    private void DrawDangerFeedback(Graphics graphics, GameStateData state, float offsetX, float offsetY, float viewportWidth, float viewportHeight)
    {
        int alpha = 0;
        if (state.Player.Energy < 30)
        {
            alpha = Math.Max(alpha, 70);
        }

        if (state.Boss.IsActive && !state.Player.IsInBooth)
        {
            alpha = Math.Max(alpha, 90);
        }

        if (alpha <= 0)
        {
            return;
        }

        using var dangerPen = new Pen(Color.FromArgb(alpha, 180, 42, 42), 12f);
        graphics.DrawRectangle(dangerPen, offsetX - 8f, offsetY - 8f, viewportWidth + 16f, viewportHeight + 16f);
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
        _bossWarningLabel.Text = state.Boss.IsActive ? "Начальник рядом" : string.Empty;
    }

    private void ShowGameOver(GameStateData state)
    {
        SaveCurrentRunIfNeeded();
        _gameTimer.Stop();
        _isPaused = false;
        SetGameLayout();
        _hudPanel.Visible = true;
        _gameOverLabel.Text = $"{state.GameOverReason}\r\nИтоговый счёт: {state.Player.Score}\r\nEnter - начать заново";
        _gameOverLabel.Visible = true;
        _restartButton.Visible = true;
        _resumeButton.Visible = false;
        _exitButton.Visible = true;
        _exitButton.Text = "В МЕНЮ";
        _musicToggleButton.Visible = false;
        _soundToggleButton.Visible = false;
        _helpButton.Visible = false;
        _helpOverlay.Visible = false;
        RepositionOverlayControls();
    }

    private void RepositionOverlayControls()
    {
        if (_gamePanel is null)
        {
            return;
        }

        var leaderboardBounds = GetMenuLeaderboardBounds();
        int centerX = _gamePanel.ClientSize.Width / 2;
        int centerY = _gamePanel.ClientSize.Height / 2;

        _gameOverLabel.Location = new Point(
            Math.Max(16, centerX - _gameOverLabel.Width / 2),
            Math.Max(24, centerY - 160));

        int settingsY = Math.Max(24, _gamePanel.ClientSize.Height - 290);
        int settingsSpacing = 18;
        int settingsRowWidth = _musicToggleButton.Width + _soundToggleButton.Width + _helpButton.Width + settingsSpacing * 2;
        int settingsStartX = centerX - settingsRowWidth / 2;

        _musicToggleButton.Location = new Point(settingsStartX, settingsY);
        _soundToggleButton.Location = new Point(_musicToggleButton.Right + settingsSpacing, settingsY);
        _helpButton.Location = new Point(_soundToggleButton.Right + settingsSpacing, settingsY);

        int nicknameY = settingsY + _musicToggleButton.Height + 22;
        _nicknameTextBox.Location = new Point(centerX - (_nicknameTextBox.Width / 2) + 42, nicknameY);
        _nicknameLabel.Location = new Point(_nicknameTextBox.Left - 138, nicknameY + 2);
        _leaderboardLabel.Location = new Point(leaderboardBounds.X, leaderboardBounds.Y);
        _leaderboardLabel.Size = leaderboardBounds.Size;

        int menuButtonX = Math.Max(16, centerX - _startButton.Width / 2);
        int startY = nicknameY + _nicknameTextBox.Height + 28;
        _startButton.Location = new Point(menuButtonX, startY);

        _restartButton.Location = new Point(
            Math.Max(16, centerX - _restartButton.Width / 2),
            Math.Max(24, centerY + 94));

        _resumeButton.Location = new Point(
            Math.Max(16, centerX - _resumeButton.Width / 2),
            Math.Max(24, centerY + 24));

        int exitX = Math.Max(16, centerX - _exitButton.Width / 2);
        int exitY = Math.Max(24, startY + _startButton.Height + 16);
        _exitButton.Location = new Point(exitX, exitY);

        _taskOverlay.Location = new Point(
            Math.Max(16, centerX - _taskOverlay.Width / 2),
            Math.Max(24, centerY - _taskOverlay.Height / 2));

        _helpOverlay.Location = new Point(
            Math.Max(16, centerX - _helpOverlay.Width / 2),
            Math.Max(24, centerY - _helpOverlay.Height / 2));
    }

    private Rectangle GetMenuLeaderboardBounds()
    {
        int boardWidth = Math.Min(240, Math.Max(200, _gamePanel.ClientSize.Width / 5));
        int boardX = _gamePanel.ClientSize.Width - boardWidth - 56;
        int boardY = 176;
        int boardHeight = Math.Max(120, _gamePanel.ClientSize.Height - boardY - 56);
        return new Rectangle(boardX, boardY, boardWidth, boardHeight);
    }

    private void SetMenuLayout()
    {
        _gamePanel.Margin = new Padding(0);
        _rootLayout.RowStyles[1].Height = 0f;
    }

    private void SetGameLayout()
    {
        _gamePanel.Margin = new Padding(0, 0, 0, 16);
        _rootLayout.RowStyles[1].Height = 190f;
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        var state = _gameEngine.GetState();

        if (_helpOverlay.Visible)
        {
            if (e.KeyCode == Keys.Escape || e.KeyCode == Keys.Enter)
            {
                HideHelpOverlay();
                e.SuppressKeyPress = true;
            }

            return;
        }

        if (_taskOverlay.Visible)
        {
            HandleTaskOverlayKeyDown(e);
            return;
        }

        if (state.State == GameState.Menu)
        {
            if (_nicknameTextBox.Focused && e.KeyCode != Keys.Enter && e.KeyCode != Keys.Escape && e.KeyCode != Keys.Q)
            {
                return;
            }

            if (e.KeyCode == Keys.Enter)
            {
                StartGame();
            }
            else if (e.KeyCode == Keys.Escape || e.KeyCode == Keys.Q)
            {
                Close();
            }

            e.SuppressKeyPress = true;
            return;
        }

        if (state.State == GameState.GameOver)
        {
            if (e.KeyCode == Keys.Enter)
            {
                StartGame();
            }
            else if (e.KeyCode == Keys.Escape || e.KeyCode == Keys.Q)
            {
                ShowMenu();
            }

            e.SuppressKeyPress = true;
            return;
        }

        if (_isPaused)
        {
            if (e.KeyCode == Keys.Escape || e.KeyCode == Keys.Enter)
            {
                ResumeGame();
            }
            else if (e.KeyCode == Keys.Q)
            {
                ShowMenu();
            }

            e.SuppressKeyPress = true;
            return;
        }

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
                e.SuppressKeyPress = true;
                break;
            case Keys.J:
                CompleteNearestTask();
                e.SuppressKeyPress = true;
                break;
            case Keys.Escape:
                PauseGame();
                e.SuppressKeyPress = true;
                break;
            case Keys.F11:
                ToggleFullscreen();
                e.SuppressKeyPress = true;
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
        float minDist = TaskInteractionRadius;

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
            BeginTaskInteraction(nearest);
        }
    }

    private void BeginTaskInteraction(WorkTask task)
    {
        _activeTask = task;
        _taskSequenceProgress = 0;
        _taskTapProgress = 0;
        _taskTapTarget = 0;
        _taskChoiceAnswer = string.Empty;
        _taskChoices = Array.Empty<string>();
        _keyW = false;
        _keyA = false;
        _keyS = false;
        _keyD = false;

        switch (task.Type)
        {
            case TaskType.CodeReview:
            case TaskType.Deploy:
                ConfigureSequenceTask(task);
                break;
            case TaskType.BugFix:
            case TaskType.Documentation:
                ConfigureTapTask(task);
                break;
            case TaskType.Meeting:
                ConfigureChoiceTask();
                break;
            default:
                ConfigureTapTask(task);
                break;
        }

        _taskOverlay.Visible = true;
        _taskOverlay.BringToFront();
        _gamePanel.Focus();
        _gamePanel.Invalidate();
    }

    private void ConfigureSequenceTask(WorkTask task)
    {
        _activeTaskMiniGame = TaskMiniGameKind.Sequence;
        _taskSequenceKeys = task.Type == TaskType.Deploy
            ? [Keys.D, Keys.E, Keys.P, Keys.L, Keys.O, Keys.Y]
            : [Keys.A, Keys.S, Keys.D];
        _taskSequenceLabels = _taskSequenceKeys.Select(MapKeyLabel).ToArray();

        _taskTitleLabel.Text = task.Type == TaskType.Deploy ? "Развёртывание" : "Проверка кода";
        _taskInstructionLabel.Text =
            $"Наберите последовательность клавиш, чтобы завершить задачу.{Environment.NewLine}" +
            $"Последовательность: {string.Join("  ", _taskSequenceLabels)}";
        _taskInstructionLabel.ForeColor = Color.FromArgb(220, 225, 235);
        _taskProgressBar.Visible = true;
        _taskProgressBar.Value = 0;
        SetTaskButtons(primaryVisible: false, secondaryVisible: false, tertiaryVisible: false);
    }

    private void ConfigureTapTask(WorkTask task)
    {
        _activeTaskMiniGame = TaskMiniGameKind.Tap;
        _taskTapTarget = task.Type == TaskType.BugFix ? 6 : 5;
        _taskTapProgress = 0;

        _taskTitleLabel.Text = task.Type == TaskType.BugFix ? "Исправление бага" : "Оформление документации";
        _taskInstructionLabel.Text = task.Type == TaskType.BugFix
            ? "Быстро жмите Enter или Пробел, чтобы закрыть баг."
            : "Жмите Enter или Пробел, чтобы дописать документацию.";
        _taskInstructionLabel.ForeColor = Color.FromArgb(220, 225, 235);
        _taskProgressBar.Visible = true;
        _taskProgressBar.Value = 0;
        SetTaskButtons(primaryVisible: true, secondaryVisible: false, tertiaryVisible: false);
        _taskPrimaryButton.Text = task.Type == TaskType.BugFix ? "ЧИНИТЬ" : "ПИСАТЬ";
    }

    private void ConfigureChoiceTask()
    {
        _activeTaskMiniGame = TaskMiniGameKind.Choice;
        _taskChoices = new[] { "Планёрка", "Курилка", "Кофе" }
            .OrderBy(_ => Guid.NewGuid())
            .ToArray();

        _taskChoiceAnswer = "Планёрка";
        _taskTitleLabel.Text = "Созвон";
        _taskInstructionLabel.Text =
            "Выберите нужную встречу кнопками 1, 2 или 3. Нужный вариант: рабочая планёрка.";
        _taskInstructionLabel.ForeColor = Color.FromArgb(220, 225, 235);
        _taskProgressBar.Visible = false;
        SetTaskButtons(primaryVisible: true, secondaryVisible: true, tertiaryVisible: true);
        _taskPrimaryButton.Text = $"1. {_taskChoices[0]}";
        _taskSecondaryButton.Text = $"2. {_taskChoices[1]}";
        _taskTertiaryButton.Text = $"3. {_taskChoices[2]}";
    }

    private void HandleTaskOverlayKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Escape)
        {
            CancelTaskInteraction();
            e.SuppressKeyPress = true;
            return;
        }

        switch (_activeTaskMiniGame)
        {
            case TaskMiniGameKind.Sequence:
                HandleSequenceTaskKey(e);
                break;
            case TaskMiniGameKind.Tap:
                if (e.KeyCode == Keys.Enter || e.KeyCode == Keys.Space)
                {
                    HandleTaskButton(_taskPrimaryButton.Text);
                    e.SuppressKeyPress = true;
                }
                break;
            case TaskMiniGameKind.Choice:
                if (e.KeyCode == Keys.D1 || e.KeyCode == Keys.NumPad1)
                {
                    HandleTaskButton(_taskChoices[0]);
                    e.SuppressKeyPress = true;
                }
                else if (e.KeyCode == Keys.D2 || e.KeyCode == Keys.NumPad2)
                {
                    HandleTaskButton(_taskChoices[1]);
                    e.SuppressKeyPress = true;
                }
                else if (e.KeyCode == Keys.D3 || e.KeyCode == Keys.NumPad3)
                {
                    HandleTaskButton(_taskChoices[2]);
                    e.SuppressKeyPress = true;
                }
                break;
        }
    }

    private void HandleSequenceTaskKey(KeyEventArgs e)
    {
        if (_taskSequenceProgress >= _taskSequenceKeys.Length)
        {
            return;
        }

        if (e.KeyCode == _taskSequenceKeys[_taskSequenceProgress])
        {
            _taskSequenceProgress++;
            _taskProgressBar.Value = Math.Clamp(_taskSequenceProgress * 100 / _taskSequenceKeys.Length, 0, 100);

            if (_taskSequenceProgress >= _taskSequenceKeys.Length)
            {
                FinishTaskInteraction(success: true);
            }
            else
            {
                _taskInstructionLabel.Text =
                    $"Продолжайте: {string.Join("  ", _taskSequenceLabels)}{Environment.NewLine}" +
                    $"Осталось символов: {_taskSequenceKeys.Length - _taskSequenceProgress}";
                _taskInstructionLabel.ForeColor = Color.FromArgb(220, 225, 235);
            }
        }
        else
        {
            _taskSequenceProgress = 0;
            _taskProgressBar.Value = 0;
            _taskInstructionLabel.Text =
                $"Неверная клавиша. Начните заново.{Environment.NewLine}" +
                $"Последовательность: {string.Join("  ", _taskSequenceLabels)}";
            _taskInstructionLabel.ForeColor = Color.FromArgb(255, 170, 170);
        }

        e.SuppressKeyPress = true;
    }

    private void HandleTaskButton(string action)
    {
        switch (_activeTaskMiniGame)
        {
            case TaskMiniGameKind.Tap:
                _taskTapProgress++;
                _taskProgressBar.Value = Math.Clamp(_taskTapProgress * 100 / Math.Max(1, _taskTapTarget), 0, 100);
                if (_taskTapProgress >= _taskTapTarget)
                {
                    FinishTaskInteraction(success: true);
                }
                break;
            case TaskMiniGameKind.Choice:
                string normalizedAction = action.Contains(". ")
                    ? action[(action.IndexOf(". ", StringComparison.Ordinal) + 2)..]
                    : action;

                if (normalizedAction == _taskChoiceAnswer)
                {
                    FinishTaskInteraction(success: true);
                }
                else
                {
                    _taskInstructionLabel.Text =
                        "Это не та встреча. Нужная задача связана с рабочей планёркой. Попробуйте ещё раз.";
                    _taskInstructionLabel.ForeColor = Color.FromArgb(255, 170, 170);
                }
                break;
        }
    }

    private void FinishTaskInteraction(bool success)
    {
        int? taskId = _activeTask?.Id;

        _taskOverlay.Visible = false;
        _activeTask = null;
        _activeTaskMiniGame = TaskMiniGameKind.None;

        if (success && taskId.HasValue)
        {
            PlayEffect(_taskCompleteSound);
            _gameEngine.CompleteTask(taskId.Value);
        }

        var state = _gameEngine.GetState();
        if (state.State == GameState.Playing)
        {
            UpdateUI(state);
        }

        _gamePanel.Focus();
        _gamePanel.Invalidate();
    }

    private void CancelTaskInteraction(bool resumeGame = true)
    {
        if (_taskOverlay is null)
        {
            return;
        }

        _taskOverlay.Visible = false;
        _activeTask = null;
        _activeTaskMiniGame = TaskMiniGameKind.None;
        _taskSequenceKeys = Array.Empty<Keys>();
        _taskSequenceLabels = Array.Empty<string>();
        _taskSequenceProgress = 0;
        _taskTapTarget = 0;
        _taskTapProgress = 0;
        _taskChoiceAnswer = string.Empty;
        _taskChoices = Array.Empty<string>();

        var state = _gameEngine.GetState();
        if (resumeGame && state.State == GameState.Playing)
        {
            _gamePanel.Focus();
            _gamePanel.Invalidate();
        }
    }

    private void PlayStateDrivenSounds(GameStateData state)
    {
        foreach (var pickup in state.Pickups.Where(p => p.IsCollected))
        {
            PlayEffect(pickup.Type == PickupType.Food ? _foodPickupSound : _coffeePickupSound);
        }

        if (!_lastBossActive && state.Boss.IsActive)
        {
            PlayEffect(_bossWarningSound);
        }

        _lastBossActive = state.Boss.IsActive;
    }

    private void StartBackgroundMusic()
    {
        if (!_soundEnabled || !_musicEnabled || _isMusicPlaying || string.IsNullOrWhiteSpace(_bgmPath))
        {
            return;
        }

        try
        {
            if (_bgmComPlayer is null)
            {
                var playerType = Type.GetTypeFromProgID("WMPlayer.OCX");
                if (playerType is null)
                {
                    return;
                }

                var createdPlayer = Activator.CreateInstance(playerType);
                if (createdPlayer is null)
                {
                    return;
                }

                _bgmComPlayer = createdPlayer;
                _bgmComPlayer.settings.setMode("loop", true);
                _bgmComPlayer.settings.volume = 12;
            }

            _bgmComPlayer.URL = _bgmPath;
            _bgmComPlayer.controls.play();
            _isMusicPlaying = true;
        }
        catch
        {
            _isMusicPlaying = false;
        }
    }

    private void StopBackgroundMusic()
    {
        if (!_isMusicPlaying || _bgmComPlayer is null)
        {
            return;
        }

        try
        {
            _bgmComPlayer.controls.stop();
        }
        catch
        {
        }

        _isMusicPlaying = false;
    }

    private void RestartBackgroundMusic()
    {
        StopBackgroundMusic();
        StartBackgroundMusic();
    }

    private void EnsureBackgroundMusic()
    {
        if (_soundEnabled && _musicEnabled && !_isMusicPlaying)
        {
            StartBackgroundMusic();
        }
    }

    private void PlayEffect(SoundPlayer? player)
    {
        if (!_soundEnabled || player is null)
        {
            return;
        }

        try
        {
            player.Play();
        }
        catch
        {
        }
    }

    private void ToggleMusic()
    {
        _musicEnabled = !_musicEnabled;
        UpdateAudioToggleButtons();

        if (_musicEnabled)
        {
            StartBackgroundMusic();
        }
        else
        {
            StopBackgroundMusic();
        }

        _gamePanel.Focus();
    }

    private void ToggleSound()
    {
        _soundEnabled = !_soundEnabled;
        if (!_soundEnabled)
        {
            StopAllEffects();
            StopBackgroundMusic();
        }
        else if (_musicEnabled)
        {
            StartBackgroundMusic();
        }

        UpdateAudioToggleButtons();
        _gamePanel.Focus();
    }

    private void UpdateAudioToggleButtons()
    {
        _musicToggleButton.Text = _musicEnabled ? "МУЗЫКА: ВКЛ" : "МУЗЫКА: ВЫКЛ";
        _soundToggleButton.Text = _soundEnabled ? "ЗВУКИ: ВКЛ" : "ЗВУКИ: ВЫКЛ";
    }

    private void ShowHelpOverlay()
    {
        _helpOverlayBodyLabel.Text =
            "О чём игра:\r\n" +
            "Вы офисный выживальщик. Нужно бегать по карте, закрывать рабочие задачи, держать энергию и прятаться от начальника.\r\n\r\n" +
            "Как играть:\r\n" +
            "WASD - движение\r\n" +
            "J - начать задачу рядом\r\n" +
            "Space - спрятаться в кабинке\r\n" +
            "E - выйти из кабинки\r\n" +
            "Esc - пауза или назад\r\n" +
            "F11 - полноэкранный режим\r\n\r\n" +
            "Как зарабатывать очки:\r\n" +
            "Очки даются за выполнение задач.\r\n" +
            "Чем дольше держитесь в забеге, тем больше бонус за выживание.\r\n" +
            "Серия успешных задач даёт дополнительную выгоду.";
        _helpOverlay.Visible = true;
        _helpOverlay.BringToFront();
        _gamePanel.Invalidate();
    }

    private void HideHelpOverlay()
    {
        _helpOverlay.Visible = false;
        _gamePanel.Focus();
        _gamePanel.Invalidate();
    }

    private void StopAllEffects()
    {
        StopEffect(_coffeePickupSound);
        StopEffect(_foodPickupSound);
        StopEffect(_taskCompleteSound);
        StopEffect(_bossWarningSound);
        StopEffect(_menuSelectSound);
    }

    private static void StopEffect(SoundPlayer? player)
    {
        if (player is null)
        {
            return;
        }

        try
        {
            player.Stop();
        }
        catch
        {
        }
    }

    private void SetTaskButtons(bool primaryVisible, bool secondaryVisible, bool tertiaryVisible)
    {
        _taskPrimaryButton.Visible = primaryVisible;
        _taskSecondaryButton.Visible = secondaryVisible;
        _taskTertiaryButton.Visible = tertiaryVisible;
    }

    private static string MapKeyLabel(Keys key)
    {
        return key switch
        {
            Keys.Space => "Пробел",
            Keys.Enter => "Enter",
            _ => key.ToString().ToUpperInvariant()
        };
    }

    private void MenuNicknameTextBox_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Enter)
        {
            StartGame();
            e.SuppressKeyPress = true;
        }
    }

    private string GetLeaderboardDisplayText()
    {
        if (_leaderboardEntries.Count == 0)
        {
            return "Пока рекордов нет.\r\nСыграйте первый забег и задайте планку.";
        }

        return string.Join(
            Environment.NewLine,
            _leaderboardEntries.Select((entry, index) => $"{index + 1}. {entry.PlayerName} - {entry.Score}"));
    }

    private void SaveCurrentRunIfNeeded()
    {
        var state = _gameEngine.GetState();
        if (_currentRunScoreSaved || string.IsNullOrWhiteSpace(_currentPlayerName) || state.Player.Score <= 0)
        {
            return;
        }

        _leaderboardEntries = _leaderboardService.AddEntry(_currentPlayerName, state.Player.Score);
        _leaderboardLabel.Text = GetLeaderboardDisplayText();
        _currentRunScoreSaved = true;
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        SaveCurrentRunIfNeeded();
        StopBackgroundMusic();
        _gameTimer.Stop();
        _gameTimer.Dispose();

        _hudFont.Dispose();
        _hudValueFont.Dispose();
        _helpFont.Dispose();
        _overlayBodyFont.Dispose();
        _titleFont.Dispose();
        _menuBodyFont.Dispose();
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

internal enum TaskMiniGameKind
{
    None,
    Sequence,
    Tap,
    Choice
}
