using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Swof.Engine;
using NumericsVector2 = System.Numerics.Vector2;

namespace Swof.Game;

internal sealed class SwofGame : Microsoft.Xna.Framework.Game
{
    private const float MaxFrameDeltaSeconds = 0.05f;

    private readonly GraphicsDeviceManager _graphics;
    private readonly MatchEngine _engine;
    private MatchRenderer? _renderer;
    private SpriteFont? _font;
    private KeyboardState _previousKeyboard;
    private GamePadState _previousGamePad;
    private double _runtimeSeconds;
    private double _noticeUntilSeconds;
    private string? _notice;
    private double _fpsWindowStarted;
    private int _framesInWindow;
    private float _framesPerSecond;

    public SwofGame()
    {
        var tuning = PhysicsTuningStore.LoadOrDefault();
        _engine = MatchEngine.CreateDefault(tuning);
        _graphics = new GraphicsDeviceManager(this)
        {
            PreferredBackBufferWidth = 1280,
            PreferredBackBufferHeight = 720,
            SynchronizeWithVerticalRetrace = true,
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
        SetNotice("MonoGame host active", 2.5d);
        base.Initialize();
    }

    protected override void LoadContent()
    {
        _font = Content.Load<SpriteFont>("Default");
        var playerAtlas = Content.Load<Texture2D>("PlayerAtlasV2");
        var playerRunAtlas = Content.Load<Texture2D>("PlayerRunAtlas");
        _renderer = new MatchRenderer(GraphicsDevice, playerAtlas, playerRunAtlas);
    }

    protected override void Update(GameTime gameTime)
    {
        _runtimeSeconds += gameTime.ElapsedGameTime.TotalSeconds;
        var keyboard = Keyboard.GetState();
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

        if (IsNewKeyPress(Keys.R, keyboard) || IsNewButtonPress(Buttons.Start, gamePad))
        {
            _engine.ResumeFromRestart();
        }

        var input = IsActive ? ReadInput(keyboard, gamePad) : PlayerInput.Empty;
        var deltaSeconds = Math.Min((float)gameTime.ElapsedGameTime.TotalSeconds, MaxFrameDeltaSeconds);
        _engine.Step(deltaSeconds, input);
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
        var snapshot = _engine.GetRenderSnapshot();
        var active = _engine.State.ActivePlayer;
        var ballOwner = snapshot.Ball.PossessorId is int possessorId ? $"P{possessorId}" : "loose";
        var primaryStatus =
            $"Home {snapshot.HomeScore} - {snapshot.AwayScore} Away   " +
            $"{snapshot.ClockSeconds / 60f:00}:{snapshot.ClockSeconds % 60f:00.0}   " +
            $"Tick {snapshot.Tick}   FPS {_framesPerSecond:0}   Ball {ballOwner}";
        var secondaryStatus =
            $"{active.Name}  C:{active.Attributes.EffectiveBallControl} S:{active.Attributes.EffectiveSpeed}   " +
            "Move: Arrows/WASD or left stick   Action: Space/A   R/Start: resume   F1: reload tuning   F11: fullscreen";
        var notice = _runtimeSeconds <= _noticeUntilSeconds ? _notice : null;

        _renderer.Draw(
            snapshot,
            _font,
            primaryStatus,
            secondaryStatus,
            notice,
            (float)gameTime.ElapsedGameTime.TotalSeconds);
        base.Draw(gameTime);
    }

    protected override void UnloadContent()
    {
        _renderer?.Dispose();
        _renderer = null;
        base.UnloadContent();
    }

    protected override void OnActivated(object sender, EventArgs args)
    {
        _previousKeyboard = Keyboard.GetState();
        _previousGamePad = GamePad.GetState(PlayerIndex.One);
        base.OnActivated(sender, args);
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
