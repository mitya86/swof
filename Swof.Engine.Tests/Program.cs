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
    ("player acceleration is quick but progressive", PlayerAccelerationIsQuickButProgressive),
    ("player stops promptly after input release", PlayerStopsPromptlyAfterInputRelease),
    ("player turns across momentum responsively", PlayerTurnsAcrossMomentumResponsively),
    ("player reversal takes effect promptly", PlayerReversalTakesEffectPromptly),
    ("diagonal movement preserves the speed cap", DiagonalMovementPreservesSpeedCap),
    ("boundary contact cancels only outward momentum", BoundaryContactCancelsOnlyOutwardMomentum),
    ("controlled ball is not glued to carry point", ControlledBallIsNotGluedToCarryPoint),
    ("dribble touches accelerate ball", DribbleTouchesAccelerateBall),
    ("dribble touch redirects ball along new facing", DribbleTouchRedirectsBallAlongNewFacing),
    ("turning keeps controlled ball in front", TurningKeepsControlledBallInFront),
    ("collecting loose ball settles into control", CollectingLooseBallSettlesIntoControl),
    ("straight dribble keeps ball under control", StraightDribbleKeepsBallUnderControl),
    ("stopped player keeps ball trapped in control radius", StoppedPlayerKeepsBallTrappedInControlRadius),
    ("stationary lead depends on ball control skill", StationaryLeadDependsOnBallControlSkill),
    ("low control player can lose ball on abrupt turn", LowControlPlayerCanLoseBallOnAbruptTurn),
    ("kick uses stored facing azimuth", KickUsesStoredFacingAzimuth),
    ("quick tap performs pass", QuickTapPerformsPass),
    ("pass stops passer immediately", PassStopsPasserImmediately),
    ("targeted pass switches active player", TargetedPassSwitchesActivePlayer),
    ("untargeted pass keeps active player", UntargetedPassKeepsActivePlayer),
    ("opponent can intercept loose pass", OpponentCanInterceptLoosePass),
    ("non active possessor controls ball", NonActivePossessorControlsBall),
    ("passive challenger can win controlled ball", PassiveChallengerCanWinControlledBall),
    ("high control player resists passive challenge", HighControlPlayerResistsPassiveChallenge),
    ("sliding tackle has travel and recovery phases", SlidingTackleHasTravelAndRecoveryPhases),
    ("sliding tackle can knock ball loose", SlidingTackleCanKnockBallLoose),
    ("longer hold kicks harder", LongerHoldKicksHarder),
    ("hard kick lifts ball", HardKickLiftsBall),
    ("long pass lifts ball", LongPassLiftsBall),
    ("gravity returns loose ball to ground", GravityReturnsLooseBallToGround),
    ("render snapshot interpolates between ticks", RenderSnapshotInterpolatesBetweenTicks)
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
    Assert(engine.State.Ball.PossessorId == engine.State.ActivePlayerId, "Ball should remain under player control.");
}

static void PlayerAccelerationIsQuickButProgressive()
{
    var engine = CreateMovementEngine();
    var player = engine.State.ActivePlayer;

    engine.StepTick(new PlayerInput(Vector2.UnitX, ActionDown: false));
    var firstTickSpeed = player.Velocity.Length();
    StepMany(engine, new PlayerInput(Vector2.UnitX, ActionDown: false), 7);

    Assert(firstTickSpeed > 0f, "Movement input should have an immediate effect.");
    Assert(
        firstTickSpeed < player.MaxSpeedPerTick * 0.35f,
        "The first movement tick should not jump straight to running speed.");
    Assert(
        player.Velocity.Length() >= player.MaxSpeedPerTick * 0.9f,
        "A runner should reach responsive arcade pace within eight ticks.");
}

static void PlayerStopsPromptlyAfterInputRelease()
{
    var engine = CreateMovementEngine();
    var player = engine.State.ActivePlayer;
    StepMany(engine, new PlayerInput(Vector2.UnitX, ActionDown: false), 20);
    var runningSpeed = player.Velocity.Length();

    engine.StepTick(PlayerInput.Empty);
    var brakingSpeed = player.Velocity.Length();
    StepMany(engine, PlayerInput.Empty, 3);

    Assert(brakingSpeed < runningSpeed, "Releasing input should brake on the next tick.");
    Assert(player.Velocity.Length() < 0.0001f, "A player should stop within four neutral ticks.");
}

static void PlayerTurnsAcrossMomentumResponsively()
{
    var engine = CreateMovementEngine();
    var player = engine.State.ActivePlayer;
    StepMany(engine, new PlayerInput(Vector2.UnitX, ActionDown: false), 20);
    var runningSpeed = player.Velocity.Length();

    StepMany(engine, new PlayerInput(Vector2.UnitY, ActionDown: false), 6);

    var movementDirection = Vector2.Normalize(player.Velocity);
    Assert(
        Vector2.Dot(movementDirection, Vector2.UnitY) > 0.9f,
        "A 90-degree input should redirect most movement within six ticks.");
    Assert(
        player.Velocity.Length() > runningSpeed * 0.7f,
        "Turning should round the path without making the player feel stuck.");
}

static void PlayerReversalTakesEffectPromptly()
{
    var engine = CreateMovementEngine();
    var player = engine.State.ActivePlayer;
    StepMany(engine, new PlayerInput(Vector2.UnitX, ActionDown: false), 20);

    StepMany(engine, new PlayerInput(-Vector2.UnitX, ActionDown: false), 4);

    Assert(player.Velocity.X < 0f, "A full reversal should change travel direction within four ticks.");
    Assert(Near(player.Facing.NormalizedDegrees, Azimuth.West.NormalizedDegrees), "Facing should track reversal intent immediately.");
}

static void DiagonalMovementPreservesSpeedCap()
{
    var engine = CreateMovementEngine();
    var player = engine.State.ActivePlayer;
    var diagonal = Vector2.Normalize(Vector2.One);

    StepMany(engine, new PlayerInput(Vector2.One, ActionDown: false), 30);

    Assert(
        player.Velocity.Length() <= player.MaxSpeedPerTick + 0.0001f,
        "Diagonal input must not exceed the cardinal movement speed.");
    Assert(
        Vector2.Dot(Vector2.Normalize(player.Velocity), diagonal) > 0.999f,
        "Steady diagonal movement should follow the requested eight-way direction.");
}

static void BoundaryContactCancelsOnlyOutwardMomentum()
{
    var engine = CreateMovementEngine();
    var player = engine.State.ActivePlayer;
    var boundaryX = engine.State.Field.Right - player.Radius;
    player.Position = new Vector2(boundaryX - 0.01f, 0f);
    player.Velocity = Vector2.UnitX * player.MaxSpeedPerTick;

    engine.StepTick(new PlayerInput(Vector2.UnitX, ActionDown: false));

    Assert(Near(player.Position.X, boundaryX), "Player position should clamp to the playable boundary.");
    Assert(Near(player.Velocity.X, 0f), "Outward velocity should be cancelled at the boundary.");

    engine.StepTick(new PlayerInput(Vector2.UnitY, ActionDown: false));

    Assert(Near(player.Position.X, boundaryX), "Moving along an edge should not push the player back into the field.");
    Assert(player.Position.Y > 0f, "Tangential movement should remain available at the boundary.");
}

static void ControlledBallIsNotGluedToCarryPoint()
{
    var engine = MatchEngine.CreateDefault();

    engine.StepTick(new PlayerInput(Vector2.UnitX, ActionDown: false));

    var gluedCarryPoint = engine.State.ActivePlayer.Position
        + engine.State.ActivePlayer.Facing.ToUnitVector() * engine.Tuning.DribbleLeadDistance;
    Assert(
        Vector2.Distance(engine.State.Ball.Position, gluedCarryPoint) > 0.001f,
        "Controlled ball should not be pinned to a fixed carry point.");
}

static void DribbleTouchesAccelerateBall()
{
    var engine = MatchEngine.CreateDefault();

    StepMany(engine, new PlayerInput(Vector2.UnitX, ActionDown: false), 12);

    Assert(engine.State.Ball.Velocity.Length() > 0.02f, "Stride touches should add visible ball velocity.");
    Assert(engine.State.Ball.PossessorId == engine.State.ActivePlayerId, "Stride touches should keep controlled possession.");
}

static void DribbleTouchRedirectsBallAlongNewFacing()
{
    var engine = MatchEngine.CreateDefault();

    StepMany(engine, new PlayerInput(Vector2.UnitX, ActionDown: false), 30);

    var redirected = false;
    for (var i = 0; i < 45; i++)
    {
        engine.StepTick(new PlayerInput(Vector2.UnitY, ActionDown: false));
        var ballVelocity = engine.State.Ball.Velocity;
        if (ballVelocity.LengthSquared() <= 0.0001f)
        {
            continue;
        }

        var alignment = Vector2.Dot(Vector2.Normalize(ballVelocity), engine.State.ActivePlayer.Facing.ToUnitVector());
        if (alignment > 0.98f)
        {
            redirected = true;
            break;
        }
    }

    Assert(redirected, "A dribble touch after turning should redirect the ball along the new facing.");
    Assert(engine.State.Ball.PossessorId == engine.State.ActivePlayerId, "High-control player should keep the redirected dribble.");
}

static void TurningKeepsControlledBallInFront()
{
    var engine = MatchEngine.CreateDefault();
    var player = engine.State.ActivePlayer;

    StepMany(engine, new PlayerInput(Vector2.UnitX, ActionDown: false), 20);
    StepMany(engine, new PlayerInput(Vector2.UnitY, ActionDown: false), 25);

    var facing = player.Facing.ToUnitVector();
    var right = new Vector2(-facing.Y, facing.X);
    var offset = engine.State.Ball.Position - player.Position;
    var forward = Vector2.Dot(offset, facing);
    var side = MathF.Abs(Vector2.Dot(offset, right));

    Assert(player.HasPossession, "Turning run should keep possession in normal control conditions.");
    Assert(forward > 0.55f, "After turning, the ball should remain in front of the player.");
    Assert(side < forward, "After turning, lateral ball offset should be smaller than forward lead.");
}

static void CollectingLooseBallSettlesIntoControl()
{
    var engine = MatchEngine.CreateDefault();
    ClearPossession(engine);

    var player = engine.State.ActivePlayer;
    player.Position = Vector2.Zero;
    player.Velocity = Vector2.Zero;
    player.Facing = Azimuth.East;
    engine.State.Ball.Position = new Vector2(player.Radius + engine.State.Ball.Radius + 0.25f, 0f);
    engine.State.Ball.Velocity = Vector2.Zero;

    engine.StepTick(new PlayerInput(Vector2.UnitX, ActionDown: false));

    Assert(player.HasPossession, "Approaching a loose ball should collect it.");
    Assert(engine.State.Ball.PossessorId == player.Id, "Collected ball should become controlled.");
    var expectedContactPoint = player.Position + player.Facing.ToUnitVector() * engine.Tuning.DribbleLeadDistance;
    Assert(
        Vector2.Distance(engine.State.Ball.Position, expectedContactPoint) < 0.001f,
        "Collected ball should settle at the front-foot contact point.");

    StepMany(engine, new PlayerInput(Vector2.UnitX, ActionDown: false), 12);

    Assert(player.HasPossession, "Collected ball should not be knocked away immediately.");
    Assert(
        Vector2.Distance(player.Position, engine.State.Ball.Position) < player.Radius + engine.State.Ball.Radius + 2.5f,
        "Collected ball should remain inside the control envelope.");
}

static void StraightDribbleKeepsBallUnderControl()
{
    var engine = MatchEngine.CreateDefault();
    foreach (var opponent in engine.State.Players.Where(player => player.Team != engine.State.ActivePlayer.Team))
    {
        opponent.Position = new Vector2(opponent.Position.X, engine.State.Field.Top + opponent.Radius);
    }

    var maxDistance = 0f;

    for (var i = 0; i < 240; i++)
    {
        engine.StepTick(new PlayerInput(Vector2.UnitX, ActionDown: false));
        maxDistance = MathF.Max(
            maxDistance,
            Vector2.Distance(engine.State.ActivePlayer.Position, engine.State.Ball.Position));
        Assert(
            engine.State.ActivePlayer.HasPossession,
            $"Straight dribbling should not lose the ball at tick {i}; distance={Vector2.Distance(engine.State.ActivePlayer.Position, engine.State.Ball.Position):0.000}, offset={engine.State.Ball.Position - engine.State.ActivePlayer.Position}, ballVelocity={engine.State.Ball.Velocity}, playerVelocity={engine.State.ActivePlayer.Velocity}.");
    }

    Assert(engine.State.ActivePlayer.HasPossession, "Straight dribbling should not lose the ball.");
    Assert(engine.State.Ball.PossessorId == engine.State.ActivePlayerId, "Ball should remain controlled during a straight run.");
    Assert(
        engine.State.Ball.Position.X > engine.State.ActivePlayer.Position.X,
        "Ball should stay ahead of the player during a straight dribble.");
    Assert(
        maxDistance < 2.7f,
        "Straight dribbling should keep the ball close.");
}

static void StoppedPlayerKeepsBallTrappedInControlRadius()
{
    var engine = MatchEngine.CreateDefault();
    var player = engine.State.ActivePlayer;

    StepMany(engine, new PlayerInput(Vector2.UnitX, ActionDown: false), 25);
    StepMany(engine, PlayerInput.Empty, 30);

    var distance = Vector2.Distance(player.Position, engine.State.Ball.Position);
    var possessionRadius = player.Radius + engine.State.Ball.Radius + engine.Tuning.PossessionDistance;

    Assert(player.HasPossession, "Stopping should not automatically lose possession.");
    Assert(engine.State.Ball.PossessorId == player.Id, "Stopped player should still be the possessor.");
    Assert(distance <= possessionRadius, "Stopped player's ball should remain inside possession radius.");
    Assert(engine.State.Ball.Velocity.Length() < 0.04f, "Stopped player's ball should settle to near-zero velocity.");
}

static void StationaryLeadDependsOnBallControlSkill()
{
    var tuning = PhysicsTuning.Default;
    var highControl = new PlayerState(1, "High Control", TeamSide.Home, Vector2.Zero)
    {
        Facing = Azimuth.East,
        HasPossession = true,
        Attributes = new PlayerAttributes(8, 8, 8, 8, 15, 10, 8),
        Radius = tuning.PlayerRadius,
        MaxSpeedPerTick = tuning.PlayerMaxSpeedPerTick,
        AccelerationPerTick = tuning.PlayerAccelerationPerTick,
        FrictionPerTick = tuning.PlayerFrictionPerTick
    };
    var highBall = new BallState
    {
        Radius = tuning.BallRadius,
        Position = highControl.Position + highControl.Facing.ToUnitVector() * (tuning.DribbleLeadDistance + 0.2f),
        Velocity = new Vector2(0.08f, 0f),
        PossessorId = highControl.Id
    };
    var highEngine = new MatchEngine(new MatchState(FieldDimensions.Default, new[] { highControl }, highBall, highControl.Id), tuning);
    StepMany(highEngine, PlayerInput.Empty, 25);

    var lowControl = new PlayerState(2, "Low Control", TeamSide.Home, Vector2.Zero)
    {
        Facing = Azimuth.East,
        HasPossession = true,
        Attributes = new PlayerAttributes(8, 8, 8, 8, 8, 10, 8),
        Radius = tuning.PlayerRadius,
        MaxSpeedPerTick = tuning.PlayerMaxSpeedPerTick,
        AccelerationPerTick = tuning.PlayerAccelerationPerTick,
        FrictionPerTick = tuning.PlayerFrictionPerTick
    };
    var lowBall = new BallState
    {
        Radius = tuning.BallRadius,
        Position = lowControl.Position + lowControl.Facing.ToUnitVector() * (tuning.DribbleLeadDistance + 0.2f),
        Velocity = new Vector2(0.08f, 0f),
        PossessorId = lowControl.Id
    };
    var lowEngine = new MatchEngine(new MatchState(FieldDimensions.Default, new[] { lowControl }, lowBall, lowControl.Id), tuning);
    StepMany(lowEngine, PlayerInput.Empty, 25);

    var highLead = Vector2.Distance(highControl.Position, highEngine.State.Ball.Position);
    var lowLead = Vector2.Distance(lowControl.Position, lowEngine.State.Ball.Position);

    Assert(highControl.HasPossession && lowControl.HasPossession, "Both players should retain possession while stationary.");
    Assert(lowLead > highLead, "Lower ball-control player should leave slightly more resting ball lead.");
}

static void LowControlPlayerCanLoseBallOnAbruptTurn()
{
    var tuning = PhysicsTuning.Default;
    var player = new PlayerState(1, "Low Control Runner", TeamSide.Home, Vector2.Zero)
    {
        Facing = Azimuth.East,
        HasPossession = true,
        Attributes = new PlayerAttributes(8, 8, 8, 8, 8, 13, 8),
        Radius = tuning.PlayerRadius,
        MaxSpeedPerTick = tuning.PlayerMaxSpeedPerTick,
        AccelerationPerTick = tuning.PlayerAccelerationPerTick,
        FrictionPerTick = tuning.PlayerFrictionPerTick
    };
    var ball = new BallState
    {
        Radius = tuning.BallRadius,
        Position = player.Position + player.Facing.ToUnitVector() * tuning.DribbleLeadDistance,
        PossessorId = player.Id
    };
    var engine = new MatchEngine(new MatchState(FieldDimensions.Default, new[] { player }, ball, activePlayerId: player.Id), tuning);

    StepMany(engine, new PlayerInput(Vector2.UnitX, ActionDown: false), 60);
    var lost = false;
    var pattern = new[] { Vector2.UnitY, -Vector2.UnitX, -Vector2.UnitY, Vector2.UnitX };
    for (var i = 0; i < 120; i++)
    {
        var move = pattern[i % pattern.Length];
        engine.StepTick(new PlayerInput(move, ActionDown: false));
        if (!engine.State.ActivePlayer.HasPossession)
        {
            lost = true;
            break;
        }
    }

    Assert(lost, "Low-control player should be able to overrun the ball on a hard turn.");
    Assert(engine.State.Ball.PossessorId is null, "Lost dribble should leave the ball loose.");
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

static void PassStopsPasserImmediately()
{
    var engine = MatchEngine.CreateDefault();
    var passer = engine.State.ActivePlayer;
    var teammate = engine.State.Players.Single(player => player.Id == 2);
    teammate.Position = passer.Position + new Vector2(20f, 0f);

    StepMany(engine, new PlayerInput(Vector2.UnitX, ActionDown: false), 20);
    Assert(passer.Velocity.Length() > 0.05f, "Passer should be running before pass.");

    StepMany(engine, new PlayerInput(Vector2.UnitX, ActionDown: true), engine.Tuning.TapPassMaxTicks);
    engine.StepTick(new PlayerInput(Vector2.UnitX, ActionDown: false));

    Assert(passer.Velocity.LengthSquared() < 0.00001f, "Pass release should stop the passer immediately.");
}

static void TargetedPassSwitchesActivePlayer()
{
    var engine = MatchEngine.CreateDefault();
    var passerId = engine.State.ActivePlayerId;
    var teammate = engine.State.Players.Single(player => player.Id == 2);
    teammate.Position = engine.State.ActivePlayer.Position + new Vector2(20f, 0f);

    StepMany(engine, new PlayerInput(Vector2.Zero, ActionDown: true), engine.Tuning.TapPassMaxTicks);
    engine.StepTick(new PlayerInput(Vector2.Zero, ActionDown: false));

    Assert(engine.State.ActivePlayerId == teammate.Id, "Targeted pass should switch control to the receiver.");
    Assert(!engine.State.Players.Single(player => player.Id == passerId).HasPossession, "Passer should release possession.");
    Assert(engine.State.Ball.PossessorId is null, "Ball should travel loose after a pass.");
}

static void UntargetedPassKeepsActivePlayer()
{
    var engine = MatchEngine.CreateDefault();
    var passerId = engine.State.ActivePlayerId;

    foreach (var teammate in engine.State.Players.Where(player => player.Team == TeamSide.Home && player.Id != passerId))
    {
        teammate.Position = engine.State.ActivePlayer.Position + new Vector2(-20f, 0f);
    }

    StepMany(engine, new PlayerInput(Vector2.Zero, ActionDown: true), engine.Tuning.TapPassMaxTicks);
    engine.StepTick(new PlayerInput(Vector2.Zero, ActionDown: false));

    Assert(engine.State.ActivePlayerId == passerId, "Untargeted pass should keep control on the passer.");
    Assert(engine.State.Ball.PossessorId is null, "Untargeted pass should still release the ball.");
}

static void OpponentCanInterceptLoosePass()
{
    var engine = MatchEngine.CreateDefault();
    var passerId = engine.State.ActivePlayerId;
    var receiver = engine.State.Players.Single(player => player.Id == 2);
    var opponent = engine.State.Players.Single(player => player.Id == 4);

    receiver.Position = engine.State.ActivePlayer.Position + new Vector2(26f, 0f);
    opponent.Position = engine.State.ActivePlayer.Position + new Vector2(8f, 0f);
    opponent.Facing = Azimuth.West;

    StepMany(engine, new PlayerInput(Vector2.Zero, ActionDown: true), engine.Tuning.TapPassMaxTicks);
    engine.StepTick(new PlayerInput(Vector2.Zero, ActionDown: false));
    StepMany(engine, PlayerInput.Empty, 30);

    Assert(engine.State.Ball.PossessorId == opponent.Id, "Opponent near the passing lane should intercept the loose ball.");
    Assert(opponent.HasPossession, "Intercepting opponent should own possession.");
    Assert(engine.State.ActivePlayerId == receiver.Id, "User control should remain on the intended receiver after an intercepted pass.");
    Assert(!engine.State.Players.Single(player => player.Id == passerId).HasPossession, "Passer should not keep possession.");
}

static void NonActivePossessorControlsBall()
{
    var engine = MatchEngine.CreateDefault();
    var opponent = engine.State.Players.Single(player => player.Id == 4);

    ClearPossession(engine);
    engine.State.ActivePlayerId = 1;
    opponent.Position = new Vector2(5f, 0f);
    opponent.Facing = Azimuth.West;
    opponent.HasPossession = true;
    engine.State.Ball.PossessorId = opponent.Id;
    engine.State.Ball.Position = opponent.Position + opponent.Facing.ToUnitVector() * engine.Tuning.DribbleLeadDistance;
    engine.State.Ball.Velocity = Vector2.Zero;

    StepMany(engine, PlayerInput.Empty, 10);

    Assert(engine.State.ActivePlayerId == 1, "Active player should not change just because opponent has possession.");
    Assert(engine.State.Ball.PossessorId == opponent.Id, "Non-active possessor should keep possession.");
    Assert(
        Vector2.Distance(opponent.Position, engine.State.Ball.Position) < opponent.Radius + engine.State.Ball.Radius + 1f,
        "Non-active possessor should keep the ball controlled nearby.");
}

static void PassiveChallengerCanWinControlledBall()
{
    var tuning = PhysicsTuning.Default;
    var attacker = new PlayerState(1, "Attacker", TeamSide.Home, Vector2.Zero)
    {
        Facing = Azimuth.East,
        HasPossession = true,
        Attributes = new PlayerAttributes(8, 8, 8, 8, 8, 10, 8),
        Radius = tuning.PlayerRadius,
        MaxSpeedPerTick = tuning.PlayerMaxSpeedPerTick,
        AccelerationPerTick = tuning.PlayerAccelerationPerTick,
        FrictionPerTick = tuning.PlayerFrictionPerTick
    };
    var defender = new PlayerState(2, "Defender", TeamSide.Away, new Vector2(1.15f, 0f))
    {
        Facing = Azimuth.West,
        Attributes = new PlayerAttributes(8, 8, 8, 15, 8, 10, 8),
        Radius = tuning.PlayerRadius,
        MaxSpeedPerTick = tuning.PlayerMaxSpeedPerTick,
        AccelerationPerTick = tuning.PlayerAccelerationPerTick,
        FrictionPerTick = tuning.PlayerFrictionPerTick
    };
    var ball = new BallState
    {
        Radius = tuning.BallRadius,
        Position = attacker.Position + attacker.Facing.ToUnitVector() * tuning.DribbleLeadDistance,
        PossessorId = attacker.Id
    };
    var engine = new MatchEngine(new MatchState(FieldDimensions.Default, new[] { attacker, defender }, ball, activePlayerId: attacker.Id), tuning);

    engine.StepTick(PlayerInput.Empty);

    Assert(!attacker.HasPossession, "Low-control attacker should lose possession to a strong nearby tackler.");
    Assert(defender.HasPossession, "Strong nearby tackler should win passive possession.");
    Assert(engine.State.Ball.PossessorId == defender.Id, "Ball possessor should become the defender.");
}

static void HighControlPlayerResistsPassiveChallenge()
{
    var tuning = PhysicsTuning.Default;
    var attacker = new PlayerState(1, "Attacker", TeamSide.Home, Vector2.Zero)
    {
        Facing = Azimuth.East,
        HasPossession = true,
        Attributes = new PlayerAttributes(8, 8, 8, 8, 15, 10, 8),
        Radius = tuning.PlayerRadius,
        MaxSpeedPerTick = tuning.PlayerMaxSpeedPerTick,
        AccelerationPerTick = tuning.PlayerAccelerationPerTick,
        FrictionPerTick = tuning.PlayerFrictionPerTick
    };
    var defender = new PlayerState(2, "Defender", TeamSide.Away, new Vector2(1.15f, 0f))
    {
        Facing = Azimuth.West,
        Attributes = new PlayerAttributes(8, 8, 8, 8, 8, 10, 8),
        Radius = tuning.PlayerRadius,
        MaxSpeedPerTick = tuning.PlayerMaxSpeedPerTick,
        AccelerationPerTick = tuning.PlayerAccelerationPerTick,
        FrictionPerTick = tuning.PlayerFrictionPerTick
    };
    var ball = new BallState
    {
        Radius = tuning.BallRadius,
        Position = attacker.Position + attacker.Facing.ToUnitVector() * tuning.DribbleLeadDistance,
        PossessorId = attacker.Id
    };
    var engine = new MatchEngine(new MatchState(FieldDimensions.Default, new[] { attacker, defender }, ball, activePlayerId: attacker.Id), tuning);

    engine.StepTick(PlayerInput.Empty);

    Assert(attacker.HasPossession, "High-control attacker should resist weak passive contact.");
    Assert(!defender.HasPossession, "Weak passive challenger should not win possession.");
    Assert(engine.State.Ball.PossessorId == attacker.Id, "Ball possessor should remain the high-control attacker.");
}

static void SlidingTackleHasTravelAndRecoveryPhases()
{
    var engine = MatchEngine.CreateDefault();
    var active = engine.State.ActivePlayer;
    ClearPossession(engine);
    active.HasPossession = false;

    engine.StepTick(new PlayerInput(Vector2.Zero, ActionDown: true));
    Assert(active.IsSliding, "Pressing action without possession should start a slide.");

    var travelTicks = 0;
    while (active.IsSliding && travelTicks < 120)
    {
        engine.StepTick(PlayerInput.Empty);
        travelTicks++;
    }

    Assert(travelTicks == engine.Tuning.SlideTravelTicks, "Slide travel duration should match tuning.");
    Assert(active.IsRecoveringFromSlide, "Slide should enter recovery after travel.");

    var recoveryTicks = 0;
    while (active.IsRecoveringFromSlide && recoveryTicks < 240)
    {
        engine.StepTick(PlayerInput.Empty);
        recoveryTicks++;
    }

    Assert(recoveryTicks == engine.Tuning.SlideRecoveryTicks, "Slide recovery duration should match tuning.");
}

static void SlidingTackleCanKnockBallLoose()
{
    var tuning = PhysicsTuning.Default;
    var slider = new PlayerState(1, "Slider", TeamSide.Home, Vector2.Zero)
    {
        Facing = Azimuth.East,
        Radius = tuning.PlayerRadius,
        MaxSpeedPerTick = tuning.PlayerMaxSpeedPerTick,
        AccelerationPerTick = tuning.PlayerAccelerationPerTick,
        FrictionPerTick = tuning.PlayerFrictionPerTick
    };
    var carrier = new PlayerState(2, "Carrier", TeamSide.Away, new Vector2(2f, 0f))
    {
        Facing = Azimuth.West,
        HasPossession = true,
        Attributes = new PlayerAttributes(8, 8, 8, 8, 8, 8, 8),
        Radius = tuning.PlayerRadius,
        MaxSpeedPerTick = tuning.PlayerMaxSpeedPerTick,
        AccelerationPerTick = tuning.PlayerAccelerationPerTick,
        FrictionPerTick = tuning.PlayerFrictionPerTick
    };
    var ball = new BallState
    {
        Radius = tuning.BallRadius,
        Position = carrier.Position + carrier.Facing.ToUnitVector() * tuning.DribbleLeadDistance,
        PossessorId = carrier.Id
    };
    var engine = new MatchEngine(new MatchState(FieldDimensions.Default, new[] { slider, carrier }, ball, activePlayerId: slider.Id), tuning);

    engine.StepTick(new PlayerInput(Vector2.Zero, ActionDown: true));
    StepMany(engine, PlayerInput.Empty, 8);

    Assert(!carrier.HasPossession, "Sliding contact should dispossess the ball carrier.");
    Assert(engine.State.Ball.PossessorId is null, "Sliding tackle should knock the ball loose.");
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

static void RenderSnapshotInterpolatesBetweenTicks()
{
    var engine = MatchEngine.CreateDefault();

    var initialX = engine.State.ActivePlayer.Position.X;
    engine.StepTick(new PlayerInput(Vector2.UnitX, ActionDown: false));
    var tickOneX = engine.State.ActivePlayer.Position.X;

    var ticks = engine.Step(engine.Tuning.TickSeconds / 2f, new PlayerInput(Vector2.UnitX, ActionDown: false));
    var renderX = engine.GetRenderSnapshot().ActivePlayer.Position.X;

    Assert(ticks == 0, "Half a tick should not advance simulation.");
    Assert(engine.State.Tick == 1, "Half a tick should not mutate authoritative tick.");
    Assert(renderX > initialX, "Interpolated render player should move past previous tick position.");
    Assert(renderX < tickOneX, "Interpolated render player should remain before current tick position.");
}

static MatchEngine CreateMovementEngine()
{
    var tuning = PhysicsTuning.Default;
    var player = new PlayerState(1, "Movement Test Runner", TeamSide.Home, Vector2.Zero)
    {
        Facing = Azimuth.East,
        Attributes = PlayerAttributes.DefaultOutfield,
        Radius = tuning.PlayerRadius,
        MaxSpeedPerTick = tuning.PlayerMaxSpeedPerTick,
        AccelerationPerTick = tuning.PlayerAccelerationPerTick,
        FrictionPerTick = tuning.PlayerFrictionPerTick
    };
    var ball = new BallState
    {
        Position = new Vector2(40f, 20f),
        Radius = tuning.BallRadius
    };

    return new MatchEngine(
        new MatchState(FieldDimensions.Default, new[] { player }, ball, activePlayerId: player.Id),
        tuning);
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
