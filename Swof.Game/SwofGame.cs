using System.Diagnostics.CodeAnalysis;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Swof.Engine;
using Swof.Engine.AI;
using NumericsVector2 = System.Numerics.Vector2;

namespace Swof.Game;

internal sealed class SwofGame : Microsoft.Xna.Framework.Game
{
    private const float MaxFrameDeltaSeconds = 0.05f;
    private const float DemoActionStartSeconds = 1f;
    private const float DemoPassReleaseSeconds = 1.08f;
    private const float DemoShotReleaseSeconds = 1.28f;

    private readonly GraphicsDeviceManager _graphics;
    private MatchEngine _engine;
    private ComputerPlayerController _computerPlayers;
    private readonly PlayerInputBuffer _inputBuffer = new();
    private readonly TouchControls _touchControls = new();
    private const int NewMatchItem = 1;
    private readonly PauseMenu _pauseMenu = new(PauseLabels);
    // At full time the same menu shows the result and the way to a new match, so a finger that is
    // still down when the new match starts stays the menu's and does not pass the kickoff.
    private bool _showingResult;
    private double _resultShownAtSeconds;
    // A touch meant for a last shot must not start the next match before the result has been seen.
    private const double ResultHoldSeconds = 1d;
    private readonly MatchDemoAction _demoAction;
    private readonly TeamTactic _opponentTactic;
    private TeamTactic _teamTactic;
    private IReadOnlyList<TouchPoint> _remainingTouches = [];
    private readonly string? _capturePath;
    private readonly bool _opponentCoach;
    private MatchRenderer? _renderer;
    private MatchFeedbackPlayer? _feedbackPlayer;
    private SpriteFont? _font;
    private KeyboardState _previousKeyboard;
    private GamePadState _previousGamePad;
    private double _runtimeSeconds;
    private double _noticeUntilSeconds;
    private string? _notice;
    private double _fpsWindowStarted;
    private int _framesInWindow;
    private float _framesPerSecond;
    private int _feedbackModeIndex;
    private float _demoElapsedSeconds;
    private bool _showDebugHud;
    private bool _cameraEffectsEnabled;

    private readonly double _captureAfterSeconds;
    private readonly IReadOnlyList<ScriptedInput> _script;

    private static readonly TeamTactic[] TeamTactics = [TeamTactic.HighPress, TeamTactic.HalfCourt, TeamTactic.PushUp];
    // Below TeamTactics on purpose: static fields are set in the order they are written.
    private static readonly string[] PauseLabels = ["Resume", .. TeamTactics.Select(tactic => $"Tactic: {tactic.Name}")];

    private static readonly MatchFeedbackMode[] FeedbackModes =
    [
        MatchFeedbackMode.Dry,
        MatchFeedbackMode.Connected,
        MatchFeedbackMode.Arcade
    ];

    [MemberNotNull(nameof(_engine), nameof(_computerPlayers))]
    private void StartMatch()
    {
        // A different seed each match, so the computer players do not replay the same one.
        var tuning = PhysicsTuningStore.LoadOrDefault();
        _showingResult = false;
        _pauseMenu.Labels = PauseLabels;
        _pauseMenu.Close();
        _engine = MatchEngine.CreateDefault(tuning, seed: Environment.TickCount, startWithKickoff: true);
        _computerPlayers = ComputerPlayerController.CreateDefault(_engine.CurrentSnapshot);
        _computerPlayers.SetTactic(TeamSide.Away, _opponentTactic);
        _computerPlayers.SetTactic(TeamSide.Home, _teamTactic);
        if (_opponentCoach)
        {
            // ponytail: the match length is read once here; reloading the tuning with F1 mid-match leaves the rule on the old length.
            _computerPlayers.SetCoach(TeamSide.Away, new Coach(TeamSide.Away, TacticByScore.ChaseAndHold(tuning.HalfLengthSeconds * 2f)));
        }
    }

    public SwofGame(SwofGameLaunchOptions? launchOptions = null)
    {
        launchOptions ??= SwofGameLaunchOptions.Default;
        _opponentTactic = launchOptions.OpponentTactic;
        _teamTactic = launchOptions.TeamTactic;
        _opponentCoach = launchOptions.OpponentCoach;
        StartMatch();
        _demoAction = launchOptions.DemoAction;
        _capturePath = launchOptions.CapturePath;
        _captureAfterSeconds = launchOptions.CaptureAfterSeconds;
        _script = launchOptions.Script;
        if (launchOptions.StartPaused && _demoAction == MatchDemoAction.None)
        {
            _pauseMenu.Open();
        }

        _feedbackModeIndex = Array.IndexOf(FeedbackModes, launchOptions.FeedbackMode);
        if (_feedbackModeIndex < 0)
        {
            _feedbackModeIndex = 1;
        }

        _graphics = new GraphicsDeviceManager(this)
        {
            PreferredBackBufferWidth = 1280,
            PreferredBackBufferHeight = 720,
            // A window nobody can see never gets its vertical sync, and the game then stops after one frame.
            SynchronizeWithVerticalRetrace = _capturePath is null,
            HardwareModeSwitch = false
        };

        Content.RootDirectory = "Content";
        IsMouseVisible = true;
        // Let VSync present at the display refresh rate. The engine keeps its own fixed-step
        // accumulator, so presentation can interpolate smoothly without changing simulation timing.
        IsFixedTimeStep = false;
        InactiveSleepTime = TimeSpan.FromMilliseconds(20);
    }

    protected override void Initialize()
    {
        Window.Title = "SWOF";
        Window.AllowUserResizing = true;
        if (_demoAction == MatchDemoAction.None)
        {
            SetNotice("Both teams active: support runs and structured marking", 4.5d);
        }

        base.Initialize();
    }

    protected override void LoadContent()
    {
        _font = Content.Load<SpriteFont>("Default");
        var playerAtlas = Content.Load<Texture2D>("PlayerAtlasV2");
        var playerRunAtlas = Content.Load<Texture2D>("PlayerRunAtlas");
        _renderer = new MatchRenderer(GraphicsDevice, playerAtlas, playerRunAtlas);
        try
        {
            _feedbackPlayer = new MatchFeedbackPlayer();
        }
        catch (Exception)
        {
            SetNotice("Audio feedback unavailable", 2.5d);
        }
    }

    protected override void Update(GameTime gameTime)
    {
        _runtimeSeconds += gameTime.ElapsedGameTime.TotalSeconds;
        var keyboard = _script.Count == 0
            ? Keyboard.GetState()
            : new KeyboardState(_script.Where(item => item.Key is not null && item.IsHeldAt(_runtimeSeconds)).Select(item => item.Key!.Value).ToArray());
        var gamePad = GamePad.GetState(PlayerIndex.One, GamePadDeadZone.Circular);

        if (IsNewKeyPress(Keys.Escape, keyboard) || IsNewButtonPress(Buttons.Back, gamePad))
        {
            Exit();
            return;
        }

        if (IsNewKeyPress(Keys.F11, keyboard))
        {
            _graphics.IsFullScreen = !_graphics.IsFullScreen;
            _graphics.ApplyChanges();
            SetNotice(_graphics.IsFullScreen ? "Fullscreen enabled" : "Windowed mode", 1.5d);
        }

        if (IsNewKeyPress(Keys.F1, keyboard))
        {
            ReloadTuning();
        }

        if (IsNewKeyPress(Keys.F2, keyboard))
        {
            _feedbackModeIndex = (_feedbackModeIndex + 1) % FeedbackModes.Length;
            var label = FeedbackModes[_feedbackModeIndex].ToString().ToLowerInvariant();
            SetNotice($"Game feel: {label}", 1.5d);
        }

        if (IsNewKeyPress(Keys.F3, keyboard))
        {
            _showDebugHud = !_showDebugHud;
            SetNotice(_showDebugHud ? "Debug HUD enabled" : "Match HUD enabled", 1.5d);
        }

        if (IsNewKeyPress(Keys.F4, keyboard))
        {
            _cameraEffectsEnabled = !_cameraEffectsEnabled;
            SetNotice(_cameraEffectsEnabled ? "Camera feedback enabled" : "Camera feedback disabled", 1.5d);
        }

        if (IsNewKeyPress(Keys.T, keyboard) || IsNewButtonPress(Buttons.Y, gamePad))
        {
            SetTeamTactic(TeamTactics[(Array.IndexOf(TeamTactics, _teamTactic) + 1) % TeamTactics.Length]);
        }

        var wasPaused = _pauseMenu.IsOpen;
        if (_demoAction == MatchDemoAction.None)
        {
            var result = _engine.CurrentSnapshot;
            if (result.IsMatchOver && !_showingResult)
            {
                _showingResult = true;
                _resultShownAtSeconds = _runtimeSeconds;
                _pauseMenu.Labels = [$"Full time   Home {result.HomeScore} - {result.AwayScore} Away", "New match   (R or Start)"];
                _pauseMenu.Open();
            }

            if (IsNewKeyPress(Keys.P, keyboard) && !_showingResult)
            {
                if (wasPaused)
                {
                    _pauseMenu.Close();
                }
                else
                {
                    _pauseMenu.Open();
                }
            }

            var chosen = _pauseMenu.Read(ReadTouches(), ScreenSize(), out var touches);
            if (_showingResult)
            {
                if (chosen == NewMatchItem && _runtimeSeconds - _resultShownAtSeconds >= ResultHoldSeconds)
                {
                    StartMatch();
                }
            }
            else if (chosen == 0)
            {
                _pauseMenu.Close();
            }
            else if (chosen is int item)
            {
                SetTeamTactic(TeamTactics[item - 1]);
            }

            _remainingTouches = touches;
        }

        if (wasPaused && !_pauseMenu.IsOpen)
        {
            // Nothing pressed while the menu was open may reach the match as a pass or a shot.
            _inputBuffer.Reset(ReadInput(keyboard, gamePad));
            _touchControls.Reset();
        }

        if (IsNewKeyPress(Keys.R, keyboard) || IsNewButtonPress(Buttons.Start, gamePad))
        {
            if (_engine.CurrentSnapshot.IsMatchOver)
            {
                StartMatch();
            }
            else
            {
                _engine.ResumeFromRestart();
            }
        }

        var deltaSeconds = Math.Min((float)gameTime.ElapsedGameTime.TotalSeconds, MaxFrameDeltaSeconds);
        var input = _demoAction == MatchDemoAction.None
            ? IsActive || _script.Count > 0 ? ReadInput(keyboard, gamePad) : PlayerInput.Empty
            : ReadDemoInput(deltaSeconds);
        if (_demoAction == MatchDemoAction.None)
        {
            input = AddTouchInput(input);
        }

        // The view is turned in the second half, so the stick is turned with it.
        input = _engine.CurrentSnapshot.FromScreen(input);

        if (!_pauseMenu.IsOpen)
        {
            _inputBuffer.Capture(input);
            _engine.Step(deltaSeconds, _inputBuffer, _computerPlayers);
        }

        _feedbackPlayer?.Update(
            _engine.CurrentSnapshot,
            _engine.UserControlledTeam ?? TeamSide.Home,
            FeedbackModes[_feedbackModeIndex] == MatchFeedbackMode.Dry ? 0f : 1f,
            deltaSeconds,
            gamePad.IsConnected);
        _previousKeyboard = keyboard;
        _previousGamePad = gamePad;
        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        if (_renderer is null || _font is null)
        {
            GraphicsDevice.Clear(Color.Black);
            base.Draw(gameTime);
            return;
        }

        UpdateFramesPerSecond(gameTime);
        var snapshot = _engine.GetRenderSnapshot().AsSeen();

        var active = _engine.State.ActivePlayer;
        var ballOwner = snapshot.Ball.PossessorId is int possessorId ? $"P{possessorId}" : "loose";
        var primaryStatus =
            $"Home {snapshot.HomeScore} - {snapshot.AwayScore} Away   " +
            $"{snapshot.ClockSeconds / 60f:00}:{snapshot.ClockSeconds % 60f:00.0}   " +
            $"Tick {snapshot.Tick}   FPS {_framesPerSecond:0}   Ball {ballOwner}";
        var secondaryStatus =
            $"{active.Name}  C:{active.Attributes.EffectiveBallControl} S:{active.Attributes.EffectiveSpeed}   " +
            "Move/aftertouch: Arrows/WASD, left stick or drag on the left half   Tap Space/A or the right half: pass   Hold: shoot/tackle   P or the top right corner: pause and tactic   R/Start: resume";
        var notice = _runtimeSeconds <= _noticeUntilSeconds ? _notice : null;

        _renderer.Draw(
            snapshot,
            _font,
            primaryStatus,
            secondaryStatus,
            notice,
            (float)gameTime.ElapsedGameTime.TotalSeconds,
            new MatchPresentationSettings(
                FeedbackModes[_feedbackModeIndex],
                _cameraEffectsEnabled,
                _showDebugHud),
            _touchControls,
            _demoAction == MatchDemoAction.None ? _pauseMenu : null,
            _showingResult ? NewMatchItem : 1 + Array.IndexOf(TeamTactics, _teamTactic));
        base.Draw(gameTime);
        if (_capturePath is not null && _runtimeSeconds >= _captureAfterSeconds)
        {
            SaveFrameAndExit(_capturePath);
        }
    }

    protected override void UnloadContent()
    {
        _renderer?.Dispose();
        _renderer = null;
        _feedbackPlayer?.Dispose();
        _feedbackPlayer = null;
        base.UnloadContent();
    }

    protected override void OnActivated(object sender, EventArgs args)
    {
        _previousKeyboard = Keyboard.GetState();
        _previousGamePad = GamePad.GetState(PlayerIndex.One);
        _inputBuffer.Reset(ReadInput(_previousKeyboard, _previousGamePad));
        _touchControls.Reset();
        base.OnActivated(sender, args);
    }

    // ponytail: the left mouse button stands in for one finger, so stick and button cannot be held together
    // with the mouse alone (use it with the keyboard). Read TouchPanel here once there is a touch device to try.
    /// <summary>Writes what was just drawn to a PNG, so a layout can be looked at without anyone at the screen.</summary>
    private void SaveFrameAndExit(string path)
    {
        var width = GraphicsDevice.PresentationParameters.BackBufferWidth;
        var height = GraphicsDevice.PresentationParameters.BackBufferHeight;
        var pixels = new Color[width * height];
        GraphicsDevice.GetBackBufferData(pixels);
        using var texture = new Texture2D(GraphicsDevice, width, height);
        texture.SetData(pixels);
        using var stream = File.Create(path);
        texture.SaveAsPng(stream, width, height);
        Exit();
    }

    private TouchPoint[] ReadTouches()
    {
        if (_script.Count > 0)
        {
            return _script
                .Where(item => item.Touch is not null && item.IsHeldAt(_runtimeSeconds))
                .Select(item => new TouchPoint(item.Finger, item.Touch!.Value * ScreenSize()))
                .ToArray();
        }

        var mouse = Mouse.GetState();
        return IsActive
            && mouse.LeftButton == ButtonState.Pressed
            && GraphicsDevice.Viewport.Bounds.Contains(mouse.Position)
                ? [new TouchPoint(0, new NumericsVector2(mouse.X, mouse.Y))]
                : [];
    }

    private NumericsVector2 ScreenSize()
    {
        var bounds = GraphicsDevice.Viewport.Bounds;
        return new NumericsVector2(bounds.Width, bounds.Height);
    }

    /// <summary>Adds the stick and button from the touches the pause menu did not keep.</summary>
    private PlayerInput AddTouchInput(PlayerInput input)
    {
        var touch = _touchControls.Read(_remainingTouches, ScreenSize());
        return new PlayerInput(input.Move + touch.Move, input.ActionDown || touch.ActionDown);
    }

    // ponytail: changed outside the per-tick control frame, so a replay or a network peer would not see it;
    // put the tactic in the frame when either exists.
    private void SetTeamTactic(TeamTactic tactic)
    {
        _teamTactic = tactic;
        _computerPlayers.SetTactic(TeamSide.Home, _teamTactic);
        SetNotice($"Team tactic: {_teamTactic.Name}", 2d);
    }

    private PlayerInput ReadInput(KeyboardState keyboard, GamePadState gamePad)
    {
        var move = NumericsVector2.Zero;
        if (keyboard.IsKeyDown(Keys.Left) || keyboard.IsKeyDown(Keys.A))
        {
            move.X -= 1f;
        }

        if (keyboard.IsKeyDown(Keys.Right) || keyboard.IsKeyDown(Keys.D))
        {
            move.X += 1f;
        }

        if (keyboard.IsKeyDown(Keys.Up) || keyboard.IsKeyDown(Keys.W))
        {
            move.Y -= 1f;
        }

        if (keyboard.IsKeyDown(Keys.Down) || keyboard.IsKeyDown(Keys.S))
        {
            move.Y += 1f;
        }

        if (gamePad.IsConnected)
        {
            var stick = gamePad.ThumbSticks.Left;
            move += new NumericsVector2(stick.X, -stick.Y);
            if (gamePad.DPad.Left == ButtonState.Pressed)
            {
                move.X -= 1f;
            }

            if (gamePad.DPad.Right == ButtonState.Pressed)
            {
                move.X += 1f;
            }

            if (gamePad.DPad.Up == ButtonState.Pressed)
            {
                move.Y -= 1f;
            }

            if (gamePad.DPad.Down == ButtonState.Pressed)
            {
                move.Y += 1f;
            }
        }

        var actionDown = keyboard.IsKeyDown(Keys.Space)
            || gamePad.Buttons.A == ButtonState.Pressed;
        return new PlayerInput(move, actionDown);
    }

    private PlayerInput ReadDemoInput(float deltaSeconds)
    {
        _demoElapsedSeconds += deltaSeconds;
        var actionDown = _demoAction switch
        {
            MatchDemoAction.Pass => _demoElapsedSeconds is >= DemoActionStartSeconds and < DemoPassReleaseSeconds,
            MatchDemoAction.Shot => _demoElapsedSeconds is >= DemoActionStartSeconds and < DemoShotReleaseSeconds,
            _ => false
        };
        return new PlayerInput(NumericsVector2.Zero, actionDown);
    }

    private void ReloadTuning()
    {
        var tuning = PhysicsTuningStore.LoadOrDefault();
        _engine.ApplyTuning(tuning);
        SetNotice("Tuning profile reloaded", 2d);
    }

    private void UpdateFramesPerSecond(GameTime gameTime)
    {
        _framesInWindow++;
        var now = gameTime.TotalGameTime.TotalSeconds;
        var elapsed = now - _fpsWindowStarted;
        if (elapsed < 1d)
        {
            return;
        }

        _framesPerSecond = (float)(_framesInWindow / elapsed);
        _framesInWindow = 0;
        _fpsWindowStarted = now;
    }

    private void SetNotice(string notice, double durationSeconds)
    {
        _notice = notice;
        _noticeUntilSeconds = _runtimeSeconds + durationSeconds;
    }

    private bool IsNewKeyPress(Keys key, KeyboardState current)
    {
        return current.IsKeyDown(key) && _previousKeyboard.IsKeyUp(key);
    }

    private bool IsNewButtonPress(Buttons button, GamePadState current)
    {
        return current.IsButtonDown(button) && _previousGamePad.IsButtonUp(button);
    }
}
