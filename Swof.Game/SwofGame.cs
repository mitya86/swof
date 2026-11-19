using System.Diagnostics.CodeAnalysis;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Input.Touch;
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
    // The start screen, the pause menu and the result all live here; this class only draws them and obeys.
    private readonly MatchFlow _flow;
    private readonly MatchDemoAction _demoAction;
    // Null leaves the opponent's tactic to the chosen difficulty.
    private readonly TeamTactic? _opponentTactic;
    private IReadOnlyList<TouchPoint> _remainingTouches = [];
    private readonly string? _capturePath;
    private readonly bool _opponentCoach;
    private readonly string? _careerPath;
    private readonly Career? _career;
    private SpellLedger? _careerLedger;
    private TeamTactic? _careerTactic;
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
        var level = _flow.Difficulty;
        var outfield = level.Squad(MatchEngine.DefaultSquad, TeamSide.Away).ToArray();
        var lineup = _career?.Lineup();
        for (var slot = 0; slot < (lineup?.Count ?? 0); slot++)
        {
            outfield[slot] = lineup![slot].Attributes;
        }

        _engine = MatchEngine.CreateDefault(
            tuning,
            outfieldAttributes: outfield,
            seed: Environment.TickCount,
            startWithKickoff: true);
        _computerPlayers = ComputerPlayerController.CreateDefault(_engine.CurrentSnapshot);
        // ponytail: the match length is read once here; reloading the tuning with F1 mid-match leaves the coach on the old length.
        var matchSeconds = tuning.HalfLengthSeconds * 2f;
        level.Brief(_computerPlayers, TeamSide.Away, matchSeconds);
        // What was asked for at launch wins over the level.
        if (_opponentTactic is not null)
        {
            _computerPlayers.SetTactic(TeamSide.Away, _opponentTactic);
        }

        if (_opponentCoach && !level.OpponentCoach)
        {
            _computerPlayers.SetCoach(TeamSide.Away, new Coach(TeamSide.Away, TacticByScore.ChaseAndHold(matchSeconds)));
        }

        if (_career is not null && lineup is not null)
        {
            // The coach sets out his squad and picks the tactic; the person can still change it during the match.
            foreach (var (id, player) in HomeOutfielders().Zip(lineup))
            {
                _computerPlayers.SetHabits(id, player.Habits);
                _computerPlayers.SetTraits(id, player.Traits);
            }

            _flow.SetTactic(_career.Coach.UsualTactic);
            _careerTactic = _flow.Tactic;
            _careerLedger = new SpellLedger();
        }

        _computerPlayers.SetTactic(TeamSide.Home, _flow.Tactic);
    }

    private int[] HomeOutfielders() =>
        [.. _engine.State.Players.Where(player => player.Team == TeamSide.Home && !player.IsGoalkeeper).Select(player => player.Id)];

    /// <summary>At full time the career develops from the match, trains and is saved, once per match.</summary>
    private void FinishCareerMatch()
    {
        if (_career is null || _careerLedger is null || _careerTactic is null || !_engine.CurrentSnapshot.IsMatchOver)
        {
            return;
        }

        var match = _engine.CurrentSnapshot;
        var court = _careerLedger.Counts.Values.Aggregate(default(PlayerSpells), (sum, spells) => sum + spells);
        // ponytail: the training session runs here on the game thread, a short hitch at full time; move it off the thread if it shows.
        _career.AfterMatch([.. HomeOutfielders().Select(_careerLedger.Of)], court, _careerTactic, match.HomeScore - match.AwayScore);
        _career.Save(_careerPath!);
        _careerLedger = null;
    }

    public SwofGame(SwofGameLaunchOptions? launchOptions = null)
    {
        launchOptions ??= SwofGameLaunchOptions.Default;
        _opponentTactic = launchOptions.OpponentTactic;
        _opponentCoach = launchOptions.OpponentCoach;
        _demoAction = launchOptions.DemoAction;
        _careerPath = launchOptions.CareerPath;
        _career = _careerPath is null ? null : Career.LoadOrNew(_careerPath, Environment.TickCount);
        // Demos, captured frames and --start-paused open in a match, as they did before there was a start screen.
        var startScreen = _demoAction == MatchDemoAction.None
            && (launchOptions.StartScreen || (launchOptions.CapturePath is null && !launchOptions.StartPaused));
        _flow = new MatchFlow(startScreen, launchOptions.TeamTactic);
        StartMatch();
        _capturePath = launchOptions.CapturePath;
        _captureAfterSeconds = launchOptions.CaptureAfterSeconds;
        _script = launchOptions.Script;
        if (launchOptions.StartPaused && _demoAction == MatchDemoAction.None)
        {
            _flow.Pause();
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

        if (OperatingSystem.IsAndroid())
        {
            // A phone has no window: the game fills the screen, on its side.
            _graphics.IsFullScreen = true;
            _graphics.SupportedOrientations = DisplayOrientation.LandscapeLeft | DisplayOrientation.LandscapeRight;
        }

        Content.RootDirectory = "Content";
        IsMouseVisible = true;
        // Let VSync present at the display refresh rate. The engine keeps its own fixed-step
        // accumulator, so presentation can interpolate smoothly without changing simulation timing.
        IsFixedTimeStep = false;
        InactiveSleepTime = TimeSpan.FromMilliseconds(20);
    }

    protected override void Initialize()
    {
        if (!OperatingSystem.IsAndroid())
        {
            Window.Title = Product.Name;
            Window.AllowUserResizing = true;
        }

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

        if ((IsNewKeyPress(Keys.T, keyboard) || IsNewButtonPress(Buttons.Y, gamePad)) && _flow.Screen != MatchScreen.Start)
        {
            var tactics = MatchFlow.Tactics;
            _flow.SetTactic(tactics[(tactics.ToList().IndexOf(_flow.Tactic) + 1) % tactics.Count]);
            ApplyTeamTactic();
        }

        var wasPaused = _flow.IsMenuOpen;
        // The stick and button as they are on the screen: the menus read them before any turn for changed ends.
        var screenInput = _demoAction == MatchDemoAction.None && (IsActive || _script.Count > 0)
            ? ReadInput(keyboard, gamePad)
            : PlayerInput.Empty;
        if (_demoAction == MatchDemoAction.None)
        {
            var flowEvent = _flow.Update(
                _engine.CurrentSnapshot,
                screenInput,
                IsNewKeyPress(Keys.P, keyboard),
                ReadTouches(),
                ScreenSize(),
                (float)_runtimeSeconds,
                out var touches);
            if (flowEvent is FlowEvent.StartMatch or FlowEvent.LeaveMatch)
            {
                // Leaving also makes a fresh match, which waits behind the start screen.
                StartMatch();
            }
            else if (flowEvent == FlowEvent.TacticChanged)
            {
                ApplyTeamTactic();
            }

            _remainingTouches = touches;
        }

        screenInput = _flow.ForMatch(screenInput);
        if (wasPaused && !_flow.IsMenuOpen)
        {
            // Nothing pressed while the menu was open may reach the match as a pass or a shot.
            _inputBuffer.Reset(screenInput);
            _touchControls.Reset();
        }

        if (IsNewKeyPress(Keys.R, keyboard) || IsNewButtonPress(Buttons.Start, gamePad))
        {
            if (!_engine.CurrentSnapshot.IsMatchOver)
            {
                _engine.ResumeFromRestart();
            }
        }

        var deltaSeconds = Math.Min((float)gameTime.ElapsedGameTime.TotalSeconds, MaxFrameDeltaSeconds);
        var input = _demoAction == MatchDemoAction.None ? screenInput : ReadDemoInput(deltaSeconds);
        if (_demoAction == MatchDemoAction.None)
        {
            input = AddTouchInput(input);
        }

        // The view is turned in the second half, so the stick is turned with it.
        input = _engine.CurrentSnapshot.FromScreen(input);

        if (!_flow.IsMenuOpen)
        {
            _inputBuffer.Capture(input);
            _engine.Step(deltaSeconds, _inputBuffer, _computerPlayers, _careerLedger is null ? null : _careerLedger.Watch);
            FinishCareerMatch();
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
            _demoAction == MatchDemoAction.None ? _flow.Menu : null,
            _flow.Cursor,
            _flow.Heading);
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

    // No touchscreen gives a finger this id.
    private const int MouseTouchId = -1;

    private TouchPoint[] ReadTouches()
    {
        if (_script.Count > 0)
        {
            return _script
                .Where(item => item.Touch is not null && item.IsHeldAt(_runtimeSeconds))
                .Select(item => new TouchPoint(item.Finger, item.Touch!.Value * ScreenSize()))
                .ToArray();
        }

        // Fingers on a touchscreen, each with the id the screen gave it for as long as it stays down.
        var touches = TouchPanel.GetState()
            .Where(touch => touch.State is TouchLocationState.Pressed or TouchLocationState.Moved)
            .Select(touch => new TouchPoint(touch.Id, new NumericsVector2(touch.Position.X, touch.Position.Y)))
            .ToList();

        // The left mouse button is one more finger, for trying the touch controls at a desk.
        var mouse = Mouse.GetState();
        if (IsActive && mouse.LeftButton == ButtonState.Pressed && GraphicsDevice.Viewport.Bounds.Contains(mouse.Position))
        {
            touches.Add(new TouchPoint(MouseTouchId, new NumericsVector2(mouse.X, mouse.Y)));
        }

        return [.. touches];
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
    private void ApplyTeamTactic()
    {
        _computerPlayers.SetTactic(TeamSide.Home, _flow.Tactic);
        SetNotice($"Team tactic: {_flow.Tactic.Name}", 2d);
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
