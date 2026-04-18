using Swof.Engine;
using System.Numerics;

var tests = new (string Name, Action Test)[]
{
    ("default match starts with active player in possession", DefaultMatchStartsWithPossession),
    ("active player can dribble the ball", ActivePlayerCanDribble),
    ("action release kicks a possessed ball", ActionReleaseKicksBall),
    ("goal is detected and scored", GoalIsDetectedAndScored),
    ("touchline out is detected", TouchlineOutIsDetected),
    ("accumulator matches direct fixed ticks", AccumulatorMatchesDirectTicks),
    ("movement updates facing azimuth", MovementUpdatesFacingAzimuth),
    ("neutral input keeps facing for dribble", NeutralInputKeepsFacingForDribble),
    ("kick uses stored facing azimuth", KickUsesStoredFacingAzimuth),
    ("quick tap performs pass", QuickTapPerformsPass),
    ("longer hold kicks harder", LongerHoldKicksHarder),
    ("hard kick lifts ball", HardKickLiftsBall),
    ("long pass lifts ball", LongPassLiftsBall),
    ("gravity returns loose ball to ground", GravityReturnsLooseBallToGround)
};

var failed = 0;
foreach (var (name, test) in tests)
{
    try
    {
        test();
        Console.WriteLine($"PASS {name}");
    }
    catch (Exception ex)
    {
        failed++;
        Console.WriteLine($"FAIL {name}");
        Console.WriteLine(ex.Message);
    }
}

if (failed > 0)
{
    Environment.ExitCode = 1;
}

static void DefaultMatchStartsWithPossession()
{
    var engine = MatchEngine.CreateDefault();

    Assert(engine.State.ActivePlayer.HasPossession, "Active player should start with possession.");
    Assert(engine.State.Ball.PossessorId == engine.State.ActivePlayerId, "Ball possessor should be the active player.");
}

static void ActivePlayerCanDribble()
{
    var engine = MatchEngine.CreateDefault();
    var startPlayerX = engine.State.ActivePlayer.Position.X;
    var startBallX = engine.State.Ball.Position.X;

    StepMany(engine, new PlayerInput(Vector2.UnitX, ActionDown: false), 30);

    Assert(engine.State.ActivePlayer.Position.X > startPlayerX, "Player should move right.");
    Assert(engine.State.Ball.Position.X > startBallX, "Possessed ball should move with the player.");
    Assert(engine.State.Ball.PossessorId == engine.State.ActivePlayerId, "Dribbling should keep possession.");
}

static void ActionReleaseKicksBall()
{
    var engine = MatchEngine.CreateDefault();

    StepMany(engine, new PlayerInput(Vector2.UnitX, ActionDown: true), engine.Tuning.TapPassMaxTicks + 3);
    engine.StepTick(new PlayerInput(Vector2.UnitX, ActionDown: false));

    Assert(!engine.State.ActivePlayer.HasPossession, "Player should release possession after kicking.");
    Assert(engine.State.Ball.PossessorId is null, "Kicked ball should be loose.");
    Assert(engine.State.Ball.Velocity.X > 0f, "Ball should travel in the input direction.");
}

static void GoalIsDetectedAndScored()
{
    var engine = MatchEngine.CreateDefault();
    ClearPossession(engine);

    engine.State.Ball.Position = new Vector2(engine.State.Field.Right + 1f, 0f);
    engine.StepTick(PlayerInput.Empty);

    Assert(engine.State.HomeScore == 1, "Home should score when the ball crosses the right goal.");
    Assert(engine.State.IsBallOutOfPlay, "Goal should stop play until restart.");
    Assert(engine.State.LastEvent.Type == MatchEventType.GoalScored, "Last event should be a goal.");
}

static void TouchlineOutIsDetected()
{
    var engine = MatchEngine.CreateDefault();
    ClearPossession(engine);

    engine.State.Ball.Position = new Vector2(0f, engine.State.Field.Top - 1f);
    engine.StepTick(PlayerInput.Empty);

    Assert(engine.State.IsBallOutOfPlay, "Touchline out should stop play.");
    Assert(engine.State.LastEvent.Type == MatchEventType.TouchlineOut, "Last event should be touchline out.");
}

static void AccumulatorMatchesDirectTicks()
{
    var direct = MatchEngine.CreateDefault();
    var accumulated = MatchEngine.CreateDefault();
    var input = new PlayerInput(Vector2.UnitX, ActionDown: false);

    StepMany(direct, input, 60);

    var ticks = 0;
    for (var i = 0; i < 10; i++)
    {
        ticks += accumulated.Step(0.1f, input);
    }

    Assert(ticks == 60, $"Expected 60 accumulated ticks, got {ticks}.");
    Assert(direct.State.Tick == accumulated.State.Tick, "Tick counters should match.");
    Assert(
        Vector2.Distance(direct.State.ActivePlayer.Position, accumulated.State.ActivePlayer.Position) < 0.0001f,
        "Player position should match direct ticks.");
    Assert(
        Vector2.Distance(direct.State.Ball.Position, accumulated.State.Ball.Position) < 0.0001f,
        "Ball position should match direct ticks.");
}

static void MovementUpdatesFacingAzimuth()
{
    var engine = MatchEngine.CreateDefault();

    engine.StepTick(new PlayerInput(Vector2.UnitY, ActionDown: false));

    Assert(Near(engine.State.ActivePlayer.Facing.NormalizedDegrees, 180f), "Moving down should face south.");
}

static void NeutralInputKeepsFacingForDribble()
{
    var engine = MatchEngine.CreateDefault();

    engine.StepTick(new PlayerInput(Vector2.UnitY, ActionDown: false));
    engine.StepTick(PlayerInput.Empty);

    Assert(Near(engine.State.ActivePlayer.Facing.NormalizedDegrees, 180f), "Neutral input should retain facing.");
    Assert(
        engine.State.Ball.Position.Y > engine.State.ActivePlayer.Position.Y,
        "Possessed ball should stay in front of the player's facing.");
}

static void KickUsesStoredFacingAzimuth()
{
    var engine = MatchEngine.CreateDefault();

    engine.StepTick(new PlayerInput(Vector2.UnitY, ActionDown: false));
    StepMany(engine, new PlayerInput(Vector2.Zero, ActionDown: true), engine.Tuning.TapPassMaxTicks + 3);
    engine.StepTick(new PlayerInput(Vector2.Zero, ActionDown: false));

    Assert(engine.State.Ball.Velocity.Y > 0f, "Kick should travel south from stored facing.");
    Assert(MathF.Abs(engine.State.Ball.Velocity.X) < 0.0001f, "Stored south-facing kick should not drift sideways.");
}

static void QuickTapPerformsPass()
{
    var engine = MatchEngine.CreateDefault();
    var teammate = engine.State.Players.Single(player => player.Id == 2);
    teammate.Position = engine.State.ActivePlayer.Position + new Vector2(20f, 0f);

    StepMany(engine, new PlayerInput(Vector2.Zero, ActionDown: true), engine.Tuning.TapPassMaxTicks);
    engine.StepTick(new PlayerInput(Vector2.Zero, ActionDown: false));

    Assert(engine.State.Ball.PossessorId is null, "Pass should release the ball.");
    Assert(
        Near(engine.State.Ball.Velocity.Length(), engine.Tuning.PassSpeedPerTick),
        "Quick tap should use pass speed.");
    Assert(engine.State.Ball.Velocity.X > 0f, "Pass should travel toward teammate in the facing cone.");
}

static void LongerHoldKicksHarder()
{
    var shortKick = MatchEngine.CreateDefault();
    StepMany(shortKick, new PlayerInput(Vector2.Zero, ActionDown: true), shortKick.Tuning.TapPassMaxTicks + 1);
    shortKick.StepTick(new PlayerInput(Vector2.Zero, ActionDown: false));

    var longKick = MatchEngine.CreateDefault();
    StepMany(longKick, new PlayerInput(Vector2.Zero, ActionDown: true), longKick.Tuning.MaxKickHoldTicks);
    longKick.StepTick(new PlayerInput(Vector2.Zero, ActionDown: false));

    Assert(
        longKick.State.Ball.Velocity.Length() > shortKick.State.Ball.Velocity.Length(),
        "Longer held kick should be faster.");
}

static void HardKickLiftsBall()
{
    var engine = MatchEngine.CreateDefault();

    StepMany(engine, new PlayerInput(Vector2.Zero, ActionDown: true), engine.Tuning.MaxKickHoldTicks);
    engine.StepTick(new PlayerInput(Vector2.Zero, ActionDown: false));
    engine.StepTick(PlayerInput.Empty);

    Assert(engine.State.Ball.Height > 0f, "Hard kick should lift the ball.");
    Assert(engine.State.Ball.VerticalVelocity > 0f, "Hard kick should start with upward velocity.");
}

static void LongPassLiftsBall()
{
    var engine = MatchEngine.CreateDefault();
    var teammate = engine.State.Players.Single(player => player.Id == 2);
    teammate.Position = engine.State.ActivePlayer.Position + new Vector2(engine.Tuning.LongPassDistance + 2f, 0f);

    StepMany(engine, new PlayerInput(Vector2.Zero, ActionDown: true), engine.Tuning.TapPassMaxTicks);
    engine.StepTick(new PlayerInput(Vector2.Zero, ActionDown: false));
    engine.StepTick(PlayerInput.Empty);

    Assert(engine.State.Ball.Height > 0f, "Long pass should lift the ball.");
}

static void GravityReturnsLooseBallToGround()
{
    var engine = MatchEngine.CreateDefault();

    StepMany(engine, new PlayerInput(Vector2.Zero, ActionDown: true), engine.Tuning.MaxKickHoldTicks);
    engine.StepTick(new PlayerInput(Vector2.Zero, ActionDown: false));
    StepMany(engine, PlayerInput.Empty, 240);

    Assert(engine.State.Ball.Height >= 0f, "Ball height should never go below ground.");
    Assert(engine.State.Ball.Height < 0.05f, "Gravity and bounce decay should return the ball near ground.");
}

static void StepMany(MatchEngine engine, PlayerInput input, int count)
{
    for (var i = 0; i < count; i++)
    {
        engine.StepTick(input);
    }
}

static void ClearPossession(MatchEngine engine)
{
    foreach (var player in engine.State.Players)
    {
        player.HasPossession = false;
    }

    engine.State.Ball.PossessorId = null;
    engine.State.Ball.Velocity = Vector2.Zero;
}

static void Assert(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}

static bool Near(float actual, float expected)
{
    return MathF.Abs(actual - expected) < 0.0001f;
}
