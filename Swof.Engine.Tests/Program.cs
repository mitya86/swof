using Swof.Engine;
using Swof.Engine.AI;
using System.Numerics;

var tests = new (string Name, Action Test)[]
{
    ("default match starts with active player in possession", DefaultMatchStartsWithPossession),
    ("default match is five-a-side with all futsal roles", DefaultMatchIsFiveASideWithFutsalRoles),
    ("default court uses international futsal dimensions", DefaultCourtUsesFutsalDimensions),
    ("default tuning uses a readable futsal tempo", DefaultTuningUsesReadableFutsalTempo),
    ("legacy tuning profile falls back to current defaults", LegacyTuningProfileFallsBackToCurrentDefaults),
    ("futsal penalty area uses curved post-centered boundaries", FutsalPenaltyAreaUsesCurvedBoundaries),
    ("default match has one goalkeeper per team", DefaultMatchHasOneGoalkeeperPerTeam),
    ("officials stay outside player possession state", OfficialsStayOutsidePlayerPossessionState),
    ("goalkeeper cannot be selected as a normal pass target", GoalkeeperCannotBeNormalPassTarget),
    ("off ball goalkeeper ignores direct movement input", OffBallGoalkeeperIgnoresDirectMovementInput),
    ("goalkeeper AI remains inside its keeper zone", GoalkeeperAiRemainsInsideKeeperZone),
    ("goalkeeper catches a low shot across a simulation tick", GoalkeeperCatchesSweptLowShot),
    ("goalkeeper does not catch a ball above reachable height", GoalkeeperDoesNotCatchHighBall),
    ("user aims but cannot move goalkeeper during distribution", UserAimsButCannotMoveGoalkeeperDuringDistribution),
    ("computer goalkeeper selects a defender and distributes without stealing team control", ComputerGoalkeeperSelectsDefenderAndDistributes),
    ("match with no user team treats both goalkeepers alike", MatchWithNoUserTeamTreatsBothGoalkeepersAlike),
    ("pressed carrier in its own half does not carry toward its own goal", PressedCarrierInOwnHalfDoesNotCarryTowardOwnGoal),
    ("computer carrier turns a quarter at a time", ComputerCarrierTurnsAQuarterAtATime),
    ("wide players offer ahead of the ball", WidePlayersOfferAheadOfBall),
    ("carrier does not pass within an opponent's reach", CarrierDoesNotPassWithinOpponentsReach),
    ("pressed carrier in its own third passes at once", PressedCarrierInOwnThirdPassesAtOnce),
    ("pressed carrier can pass away from the presser but not through it", PressedCarrierCanPassAwayFromPresserButNotThroughIt),
    ("active player can dribble the ball", ActivePlayerCanDribble),
    ("holding beyond the tap window commits a shot before release", HeldActionCommitsShotBeforeRelease),
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
    ("pass outside the intended direction stays untargeted", PassOutsideIntendedDirectionStaysUntargeted),
    ("pass inside the intended direction targets teammate", PassInsideIntendedDirectionTargetsTeammate),
    ("pass beyond assistance range stays untargeted", PassBeyondAssistanceRangeStaysUntargeted),
    ("diagonal goal shot selects the intended side", DiagonalGoalShotSelectsIntendedSide),
    ("away diagonal goal shot mirrors home aiming", AwayDiagonalGoalShotMirrorsHomeAiming),
    ("shot aimed away from goal stays unassisted", ShotAimedAwayFromGoalStaysUnassisted),
    ("side input after contact curves the shot", SideInputAfterContactCurvesShot),
    ("forward and backward aftertouch drive and lift the shot", ForwardAndBackwardAftertouchChangeHeight),
    ("late height input cannot relaunch the ball", LateHeightInputCannotRelaunchBall),
    ("ground spin damps without curling the rolling ball", GroundSpinDampsWithoutCurlingRollingBall),
    ("held shot passes through friendly players", HeldShotPassesThroughFriendlyPlayers),
    ("pass stops passer immediately", PassStopsPasserImmediately),
    ("targeted pass switches active player", TargetedPassSwitchesActivePlayer),
    ("untargeted pass keeps active player", UntargetedPassKeepsActivePlayer),
    ("opponent can intercept loose pass", OpponentCanInterceptLoosePass),
    ("non active possessor controls ball", NonActivePossessorControlsBall),
    ("home AI collector becomes the human-controlled player", HomeAiCollectorBecomesHumanControlledPlayer),
    ("passive challenger can win controlled ball", PassiveChallengerCanWinControlledBall),
    ("high control player resists passive challenge", HighControlPlayerResistsPassiveChallenge),
    ("equal carrier keeps the ball from a challenger behind but not in front", EqualCarrierKeepsBallFromBehindButNotInFront),
    ("sliding tackle has travel and recovery phases", SlidingTackleHasTravelAndRecoveryPhases),
    ("sliding tackle can knock ball loose", SlidingTackleCanKnockBallLoose),
    ("holding after contact cannot accelerate the shot", HoldingAfterContactCannotAccelerateShot),
    ("shot lift is applied entirely at contact", ShotLiftIsAppliedEntirelyAtContact),
    ("long pass lifts ball", LongPassLiftsBall),
    ("gravity returns loose ball to ground", GravityReturnsLooseBallToGround),
    ("render snapshot interpolates between ticks", RenderSnapshotInterpolatesBetweenTicks),
    ("input buffer preserves a quick tap until fixed ticks consume it", InputBufferPreservesQuickTap),
    ("action feedback events are ordered and emitted exactly once", ActionFeedbackEventsAreExactlyOnce),
    ("feedback routes only to the local team", FeedbackRoutesOnlyToLocalTeam),
    ("feedback stereo pan follows event direction", FeedbackStereoPanFollowsEventDirection),
    ("render snapshot presents current action intent without interpolation delay", RenderSnapshotPresentsCurrentActionIntent),
    ("render snapshot presents contact pose on the authoritative tick", RenderSnapshotPresentsContactPose),
    ("computer player skips unavailable instructions and breaks ties deterministically", ComputerPlayerSkipsUnavailableInstructionsAndBreaksTies),
    ("tactical priorities change computer player instruction selection", TacticalPrioritiesChangeInstructionSelection),
    ("closest teammate recognizes and pursues a loose ball", ClosestTeammateRecognizesAndPursuesLooseBall),
    ("ball carrier selects safe space and a clear passing lane", BallCarrierSelectsSafeSpaceAndClearPassingLane),
    ("live away AI carries and circulates possession", LiveAwayAiCarriesAndCirculatesPossession),
    ("off-ball teammate chooses an open passing position", OffBallTeammateChoosesOpenPassingPosition),
    ("live home AI teammates move to passing options", LiveHomeAiTeammatesMoveToPassingOptions),
    ("defenders mark distinct opponents from compact goal-side positions", DefendersMarkDistinctOpponentsFromCompactGoalSidePositions),
    ("live two-team AI combines pass support and defensive marking", LiveTwoTeamAiCombinesPassSupportAndDefensiveMarking),
    ("opponent possession selects an active defender with AI cover", OpponentPossessionSelectsActiveDefenderWithAiCover),
    ("team shape follows the active teammate while preserving roles", TeamShapeFollowsActiveTeammate),
    ("control frame moves multiple outfielders in one tick", ControlFrameMovesMultipleOutfielders),
    ("control frame tracks action edges independently per player", ControlFrameTracksIndependentActionEdges),
    ("live computer controller preserves human input and moves eligible AI", LiveComputerControllerPreservesHumanInputAndMovesAi),
    ("team-scoped AI moves teammates while opponents remain still", TeamScopedAiMovesTeammatesWhileOpponentsRemainStill),
    ("scripted match with live AI replays to an identical state", ScriptedMatchWithLiveAiReplaysIdentically),
    ("kick-in is awarded against the team that last touched the ball", KickInIsAwardedAgainstLastTouch),
    ("goal-line exit is a corner or a goal clearance by last touch", GoalLineExitIsCornerOrGoalClearance),
    ("stopped play resumes on its own with the clock held", StoppedPlayResumesOnItsOwnWithClockHeld),
    ("opponents are moved back from a kick-in", OpponentsAreMovedBackFromKickIn),
    ("button held through a stoppage does not kick on resume", ButtonHeldThroughStoppageDoesNotKickOnResume),
    ("unattended match keeps resuming after forced outs", UnattendedMatchKeepsResumingAfterForcedOuts),
    ("sliding player takes a restart on their feet", SlidingPlayerTakesRestartOnTheirFeet),
    ("goal resets every player to kickoff positions", GoalResetsEveryPlayerToKickoffPositions),
    ("match plays two halves and then stays finished", MatchPlaysTwoHalvesAndThenStaysFinished),
    ("half-time interrupts live play cleanly", HalfTimeInterruptsLivePlayCleanly),
    ("computer carrier shoots from range and leaves the shot alone", ComputerCarrierShootsFromRangeAndLeavesTheShotAlone),
    ("computer carrier does not shoot from distance or through a defender", ComputerCarrierDoesNotShootFromDistanceOrThroughDefender),
    ("computer presser slides at a carrier within reach", ComputerPresserSlidesAtCarrierWithinReach),
    ("releasing the button without the ball does not steal it", ReleasingButtonWithoutBallDoesNotStealIt),
    ("touches give a stick and one button", TouchesGiveStickAndOneButton),
    ("a team told to wait at halfway does not follow the carrier past it", HalfCourtTeamWaitsAtHalfway),
    ("a team told to push up marks further from its own goal", PushUpTeamMarksFurtherFromOwnGoal),
    ("a short touch passes and a held touch shoots", ShortTouchPassesAndHeldTouchShoots),
    ("goalkeeper guards from inside its own area", GoalkeeperGuardsFromInsideOwnArea),
    ("set goalkeeper is beaten from range but not from distance", SetGoalkeeperIsBeatenFromRangeButNotFromDistance),
    ("kickoff taker stands over the ball and keeps it", KickoffTakerStandsOverTheBallAndKeepsIt),
    ("ball carried over the touchline is out against the carrier", BallCarriedOverTouchlineIsOutAgainstCarrier),
    ("computer corner taker carries the ball into the court", ComputerCornerTakerCarriesBallIntoCourt),
    ("goalkeeper smothers a ball dribbled straight at it", GoalkeeperSmothersBallDribbledStraightAtIt),
    ("adjacent opponents do not swap the ball every tick", AdjacentOpponentsDoNotSwapBallEveryTick),
    ("a pass goes past an opponent standing beside its line", PassGoesPastOpponentBesideItsLine),
    ("unattended match repeats for a seed and differs between seeds", UnattendedMatchRepeatsForSeedAndDiffersBetweenSeeds)
};

// "matches [count] [firstSeed] [equal] [home=half|up] [away=half|up] [home-up=metres] [away-up=metres]" plays unattended matches instead of the scenarios.
if (args.FirstOrDefault() == "matches")
{
    var count = args.Length > 1 ? int.Parse(args[1]) : 100;
    var firstSeed = args.Length > 2 ? int.Parse(args[2]) : 1;
    Environment.ExitCode = MatchRunner.Run(
        count,
        firstSeed,
        equalSquads: args.Contains("equal"),
        homeTactic: TacticFor("home"),
        awayTactic: TacticFor("away")) == 0 ? 0 : 1;

    // "home=half" or "home=up" picks a preset; "home-up=12" sets how far up Home's markers stand, for measuring.
    TeamTactic TacticFor(string side)
    {
        var tactic = args.Contains($"{side}=half") ? TeamTactic.HalfCourt
            : args.Contains($"{side}=up") ? TeamTactic.PushUp
            : TeamTactic.HighPress;
        var up = args.FirstOrDefault(argument => argument.StartsWith($"{side}-up=", StringComparison.Ordinal));
        return up is null ? tactic : tactic with { MarkersUpMetres = float.Parse(up[(side.Length + 4)..]) };
    }
    return;
}

// Optional first argument: run only scenarios whose name contains it.
var filter = args.FirstOrDefault();
var selected = tests
    .Where(test => filter is null || test.Name.Contains(filter, StringComparison.OrdinalIgnoreCase))
    .ToArray();
if (selected.Length == 0)
{
    // A filter that matches nothing must not look like a green run.
    Console.WriteLine($"FAIL no scenario name contains '{filter}'");
    Environment.ExitCode = 1;
}

var failed = 0;
foreach (var (name, test) in selected)
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

static void DefaultMatchIsFiveASideWithFutsalRoles()
{
    var engine = MatchEngine.CreateDefault();
    var expectedRoles = new HashSet<FutsalOutfieldRole>
    {
        FutsalOutfieldRole.Fixo,
        FutsalOutfieldRole.LeftAla,
        FutsalOutfieldRole.RightAla,
        FutsalOutfieldRole.Pivo
    };

    foreach (var team in Enum.GetValues<TeamSide>())
    {
        var teamPlayers = engine.State.Players.Where(player => player.Team == team).ToArray();
        var outfieldRoles = teamPlayers
            .Where(player => player.Role == PlayerRole.Outfield)
            .Select(player => player.FutsalRole)
            .ToHashSet();

        Assert(teamPlayers.Length == 5, $"{team} should start with five players on the court.");
        Assert(outfieldRoles.SetEquals(expectedRoles), $"{team} should assign fixo, both alas, and pivo exactly once.");
        Assert(
            teamPlayers.Single(player => player.Role == PlayerRole.Goalkeeper).FutsalRole == FutsalOutfieldRole.None,
            $"{team} goalkeeper should not occupy an outfield role.");
    }
}

static void DefaultCourtUsesFutsalDimensions()
{
    var field = FieldDimensions.Default;

    Assert(Near(field.Length, 40f), "The default futsal court should be 40m long.");
    Assert(Near(field.Width, 20f), "The default futsal court should be 20m wide.");
    Assert(Near(field.GoalWidth, 3f), "Futsal goals should be 3m wide.");
    Assert(Near(field.CenterCircleRadius, 3f), "The futsal centre circle should have a 3m radius.");
    Assert(Near(field.PenaltyAreaRadius, 6f), "The futsal penalty area should use 6m arcs.");
}

static void DefaultTuningUsesReadableFutsalTempo()
{
    var tuning = PhysicsTuning.Default;
    var fullCourtRunSeconds = FieldDimensions.Default.Length
        / (tuning.PlayerMaxSpeedPerTick * tuning.TickRate);

    Assert(
        Near(tuning.PlayerMaxSpeedPerTick, 14f / 60f),
        "Default outfield pace should use the slower readable futsal baseline.");
    Assert(
        Near(tuning.PassSpeedPerTick, 20f / 60f),
        "Default ground passes should remain quicker than players without flashing through combinations.");
    Assert(
        fullCourtRunSeconds >= 2.8f,
        "Even an unopposed maximum-speed court traversal should take long enough to read.");
}

static void LegacyTuningProfileFallsBackToCurrentDefaults()
{
    var path = Path.Combine(Path.GetTempPath(), $"swof-legacy-tuning-{Guid.NewGuid():N}.json");
    try
    {
        File.WriteAllText(
            path,
            """
            {
              "PlayerMaxSpeedPerTick": 0.3,
              "PassSpeedPerTick": 0.4,
              "SlideTravelTicks": 26,
              "TackleDistance": 2.2,
              "SlideRecoveryTicks": 60
            }
            """);

        var loaded = PhysicsTuningStore.LoadOrDefault(path);

        Assert(
            loaded.ProfileVersion == PhysicsTuning.CurrentProfileVersion,
            "An unversioned profile should be replaced by the current tuning contract.");
        Assert(
            Near(loaded.PlayerMaxSpeedPerTick, PhysicsTuning.Default.PlayerMaxSpeedPerTick),
            "Legacy player speed must not silently override the new readable default.");
        Assert(
            loaded.SlideTravelTicks == PhysicsTuning.Default.SlideTravelTicks
                && loaded.SlideRecoveryTicks == PhysicsTuning.Default.SlideRecoveryTicks,
            "Legacy tackle timings must not restore the obsolete long slide.");
    }
    finally
    {
        File.Delete(path);
    }
}

static void FutsalPenaltyAreaUsesCurvedBoundaries()
{
    var field = FieldDimensions.Default;

    Assert(
        field.ContainsInPenaltyArea(TeamSide.Home, new Vector2(field.Left + 5.9f, 0f)),
        "The central portion should extend almost 6m from the goal line.");
    Assert(
        field.ContainsInPenaltyArea(TeamSide.Home, new Vector2(field.Left + 3f, field.GoalBottom + 5f)),
        "A point inside the lower 6m post-centred arc should be in the area.");
    Assert(
        !field.ContainsInPenaltyArea(TeamSide.Home, new Vector2(field.Left + 5.9f, field.GoalBottom + 5f)),
        "The former rectangular corner must be excluded by the futsal arc.");
    Assert(
        field.ContainsInPenaltyArea(TeamSide.Away, new Vector2(field.Right - 3f, field.GoalTop - 5f)),
        "The away penalty area should mirror the home arc.");
}

static void DefaultMatchHasOneGoalkeeperPerTeam()
{
    var engine = MatchEngine.CreateDefault();

    foreach (var team in Enum.GetValues<TeamSide>())
    {
        var goalkeepers = engine.State.Players.Count(player =>
            player.Team == team && player.Role == PlayerRole.Goalkeeper);
        Assert(goalkeepers == 1, $"{team} should have exactly one goalkeeper.");
    }
}

static void OfficialsStayOutsidePlayerPossessionState()
{
    var player = new PlayerState(1, "Player", TeamSide.Home, Vector2.Zero);
    var official = new OfficialState(100, "Referee", MatchOfficialRole.Referee, new Vector2(2f, 3f));
    var state = new MatchState(
        FieldDimensions.Default,
        new[] { player },
        new BallState(),
        activePlayerId: player.Id,
        officials: new[] { official });
    var snapshot = MatchSnapshot.FromState(state);

    Assert(state.Players.Count == 1, "Officials must not be stored in the selectable player collection.");
    Assert(state.Officials.Count == 1, "Official state should have its own collection.");
    Assert(snapshot.Officials.Single().Role == MatchOfficialRole.Referee, "Official role should reach render snapshots.");
}

static void GoalkeeperCannotBeNormalPassTarget()
{
    var engine = MatchEngine.CreateDefault();
    var passer = engine.State.ActivePlayer;
    var goalkeeper = engine.State.Players.Single(player =>
        player.Team == TeamSide.Home && player.Role == PlayerRole.Goalkeeper);
    var outfieldTarget = engine.State.Players.Single(player => player.Id == 2);
    goalkeeper.Position = passer.Position + new Vector2(4f, 0f);
    outfieldTarget.Position = passer.Position + new Vector2(14f, 0f);
    MoveOtherHomeOutfieldersBehindPasser(engine, outfieldTarget.Id);

    StepMany(engine, new PlayerInput(Vector2.Zero, ActionDown: true), engine.Tuning.TapPassMaxTicks);
    engine.StepTick(PlayerInput.Empty);

    Assert(engine.State.ActivePlayerId == outfieldTarget.Id, "A normal pass should select an outfield receiver, not the closer keeper.");
}

static void OffBallGoalkeeperIgnoresDirectMovementInput()
{
    var engine = MatchEngine.CreateDefault();
    var goalkeeper = engine.State.Players.Single(player =>
        player.Team == TeamSide.Home && player.Role == PlayerRole.Goalkeeper);
    ClearPossession(engine);
    engine.State.ActivePlayerId = goalkeeper.Id;
    engine.State.Ball.Position = new Vector2(engine.State.Field.Right - 1f, 0f);
    goalkeeper.Position = new Vector2(
        engine.State.Field.Left + engine.Tuning.GoalkeeperGoalLineOffset,
        0f);
    var positionBefore = goalkeeper.Position;

    engine.StepTick(new PlayerInput(Vector2.UnitY, ActionDown: false));

    // The goalkeeper still repositions on its own (here straight out, the ball being on the centre line),
    // so only the sideways stick direction is checked.
    Assert(
        MathF.Abs(goalkeeper.Position.Y - positionBefore.Y) < 0.0001f,
        "Directional input must not move an off-ball goalkeeper.");
    Assert(goalkeeper.Facing != Azimuth.South, "Off-ball input must not aim the goalkeeper either.");
}

static void GoalkeeperAiRemainsInsideKeeperZone()
{
    var engine = MatchEngine.CreateDefault();
    var goalkeeper = engine.State.Players.Single(player =>
        player.Team == TeamSide.Home && player.Role == PlayerRole.Goalkeeper);
    ClearPossession(engine);
    engine.State.Ball.Position = new Vector2(engine.State.Field.Left + 4f, engine.State.Field.Bottom - 1f);

    StepMany(engine, PlayerInput.Empty, 180);

    Assert(
        goalkeeper.Position.X <= engine.State.Field.Left + engine.Tuning.GoalkeeperMaxAdvanceDistance + 0.0001f,
        "Keeper AI must not advance beyond its keeper zone.");
    Assert(
        goalkeeper.Position.Y <= engine.State.Field.GoalBottom + engine.Tuning.GoalkeeperLateralPadding + 0.0001f,
        "Keeper AI must not chase laterally beyond its keeper zone.");
}

static void GoalkeeperCatchesSweptLowShot()
{
    var engine = MatchEngine.CreateDefault();
    var goalkeeper = engine.State.Players.Single(player =>
        player.Team == TeamSide.Home && player.Role == PlayerRole.Goalkeeper);
    ClearPossession(engine);
    engine.State.Ball.Position = goalkeeper.Position + new Vector2(6f, 0f);
    engine.State.Ball.Velocity = new Vector2(-6f, 0f);
    engine.State.Ball.Height = 0.7f;

    engine.StepTick(PlayerInput.Empty);

    Assert(engine.State.Ball.PossessorId == goalkeeper.Id, "A fast low shot crossing the keeper should be caught.");
    Assert(engine.State.Ball.ControlKind == BallControlKind.GoalkeeperHands, "A save should create held-ball possession.");
    Assert(engine.State.ActivePlayerId == goalkeeper.Id, "The user's keeper should become active only for distribution.");
}

static void GoalkeeperDoesNotCatchHighBall()
{
    var engine = MatchEngine.CreateDefault();
    var goalkeeper = engine.State.Players.Single(player =>
        player.Team == TeamSide.Home && player.Role == PlayerRole.Goalkeeper);
    ClearPossession(engine);
    engine.State.Ball.Position = goalkeeper.Position + new Vector2(6f, 0f);
    engine.State.Ball.Velocity = new Vector2(-6f, 0f);
    engine.State.Ball.Height = engine.Tuning.GoalkeeperCatchMaxHeight + 1f;

    engine.StepTick(PlayerInput.Empty);

    Assert(engine.State.Ball.PossessorId is null, "A ball above the keeper's reach must remain loose.");
    Assert(engine.State.Ball.ControlKind == BallControlKind.None, "An unreachable ball must not become held possession.");
}

static void UserAimsButCannotMoveGoalkeeperDuringDistribution()
{
    var engine = MatchEngine.CreateDefault();
    var goalkeeper = CatchBallWithHomeGoalkeeper(engine);
    var positionBefore = goalkeeper.Position;

    engine.StepTick(new PlayerInput(Vector2.UnitY, ActionDown: true));
    engine.StepTick(new PlayerInput(Vector2.UnitY, ActionDown: false));

    Assert(
        Vector2.Distance(goalkeeper.Position, positionBefore) < 0.0001f,
        "A keeper holding the ball should aim without walking under direct control.");
    Assert(engine.State.Ball.PossessorId is null, "A quick action tap should distribute the held ball.");
    Assert(engine.State.Ball.Velocity.Y > 0f, "Distribution should follow the direction selected while holding the ball.");
    Assert(engine.State.ActivePlayer.Role == PlayerRole.Outfield, "Control should return to an outfield player after distribution.");
}

static void ComputerGoalkeeperSelectsDefenderAndDistributes()
{
    var engine = MatchEngine.CreateDefault();
    var goalkeeper = engine.State.Players.Single(player =>
        player.Team == TeamSide.Away && player.Role == PlayerRole.Goalkeeper);
    ClearPossession(engine);
    engine.State.Ball.Position = goalkeeper.Position + new Vector2(-6f, 0f);
    engine.State.Ball.Velocity = new Vector2(6f, 0f);
    engine.State.Ball.Height = 0.5f;
    engine.StepTick(PlayerInput.Empty);

    Assert(engine.State.Ball.PossessorId == goalkeeper.Id, "The computer keeper should catch an eligible shot.");
    Assert(
        engine.State.ActivePlayer.Team == TeamSide.Home
            && engine.State.ActivePlayer.Role == PlayerRole.Outfield,
        "An opponent save should select an eligible user-team defender.");
    var selectedDefenderId = engine.State.ActivePlayerId;

    StepMany(engine, PlayerInput.Empty, engine.Tuning.GoalkeeperAiDistributionDelayTicks);

    Assert(engine.State.Ball.PossessorId is null, "The computer keeper should distribute after its deterministic delay.");
    Assert(
        engine.State.ActivePlayerId == selectedDefenderId,
        "Computer distribution should retain the selected user-team defender while the ball travels.");
}

static void PressedCarrierCanPassAwayFromPresserButNotThroughIt()
{
    Assert(PassTarget(teammateOffset: new Vector2(0f, 8f)) == 3, "A teammate square of a pressed carrier should be a pass.");
    Assert(PassTarget(teammateOffset: new Vector2(8f, 0f)) != 3, "A teammate behind the presser should not be the pass.");

    // The presser shadows the carrier from 1.9 m, straight ahead of it.
    static int? PassTarget(Vector2 teammateOffset)
    {
        var engine = MatchEngine.CreateDefault();
        var state = engine.State;
        var carrier = state.Players.Single(player => player.Id == 2);
        carrier.Position = new Vector2(0f, -4f);
        foreach (var other in state.Players.Where(player => player.Id != carrier.Id))
        {
            other.Position = new Vector2(other.Team == TeamSide.Home ? -17f : 17f, other.Id - 5f);
        }

        state.Players.Single(player => player.Id == 10).Position = carrier.Position + new Vector2(1.9f, 0f);
        state.Players.Single(player => player.Id == 3).Position = carrier.Position + teammateOffset;
        GivePossession(engine, carrier);
        Assert(
            new SafePossessionPattern().TryRecognize(
                new PlayerDecisionContext(MatchSnapshot.FromState(state), carrier.Id),
                out var opportunity),
            "The carrier should be weighing where to go.");
        return opportunity.PassTargetId;
    }
}

static void PressedCarrierInOwnHalfDoesNotCarryTowardOwnGoal()
{
    Assert(CarryX(carrierX: -6f) >= 0f, "In its own half a pressed carrier should go sideways or forward.");
    Assert(CarryX(carrierX: 6f) < 0f, "In the opposing half a pressed carrier may still turn back.");

    // Home attacks toward positive X; one presser stands straight ahead of the carrier.
    static float CarryX(float carrierX)
    {
        var engine = MatchEngine.CreateDefault();
        var state = engine.State;
        var carrier = state.Players.Single(player => player.Id == 2);
        carrier.Position = new Vector2(carrierX, 0f);
        // Side on, so that forward and back are both a quarter turn away.
        carrier.Facing = Azimuth.South;
        foreach (var other in state.Players.Where(player => player.Id != carrier.Id))
        {
            other.Position = new Vector2(other.Team == TeamSide.Home ? -17f : 17f, other.Id - 5f);
        }

        state.Players.Single(player => player.Id == 10).Position = carrier.Position + new Vector2(2.5f, 0f);
        GivePossession(engine, carrier);
        Assert(
            new SafePossessionPattern().TryRecognize(
                new PlayerDecisionContext(MatchSnapshot.FromState(state), carrier.Id),
                out var opportunity),
            "The carrier should be weighing where to go.");
        return opportunity.CarryDirection.X;
    }
}

static void WidePlayersOfferAheadOfBall()
{
    // The fixo has the ball in its own half and nobody is near. Level with it, the alas gave it
    // only square and backward passes: over 100 matches passes took the ball back as far as carries took it up.
    var engine = MatchEngine.CreateDefault(userControlledTeam: null);
    var state = engine.State;
    var home = state.Players.Where(player => player.Team == TeamSide.Home && !player.IsGoalkeeper).ToArray();
    var fixo = home.Single(player => player.FutsalRole == FutsalOutfieldRole.Fixo);
    foreach (var other in state.Players.Where(player => !player.IsGoalkeeper))
    {
        other.Position = new Vector2(other.Team == TeamSide.Home ? -12f : 17f, other.Id - 5f);
    }

    fixo.Position = new Vector2(-8f, 0f);
    GivePossession(engine, fixo);
    var snapshot = MatchSnapshot.FromState(state);
    foreach (var ala in home.Where(player => player.FutsalRole is FutsalOutfieldRole.LeftAla or FutsalOutfieldRole.RightAla))
    {
        Assert(
            new OpenPassingOptionPattern().TryRecognize(new PlayerDecisionContext(snapshot, ala.Id), out var offer),
            "An ala should look for somewhere to offer a pass.");
        Assert(
            offer.Target.X - fixo.Position.X >= 2f,
            $"An ala should offer ahead of the ball, not level with it (offered {offer.Target.X - fixo.Position.X:0.0} m ahead).");
    }
}

static void CarrierDoesNotPassWithinOpponentsReach()
{
    Assert(PassTarget(opponentOffLane: 2.2f) != 3, "An opponent 2.2 m off the lane, two metres from the passer, reaches the pass.");
    Assert(PassTarget(opponentOffLane: 3.4f) == 3, "With the opponent 3.4 m off the lane the pass is on; if not, the scenario proves nothing.");

    static int? PassTarget(float opponentOffLane)
    {
        var engine = MatchEngine.CreateDefault(userControlledTeam: null);
        var state = engine.State;
        var carrier = state.Players.Single(player => player.Id == 2);
        var teammate = state.Players.Single(player => player.Id == 3);
        foreach (var other in state.Players.Where(player => !player.IsGoalkeeper))
        {
            other.Position = new Vector2(other.Team == TeamSide.Home ? -17f : 17f, other.Id - 5f);
        }

        carrier.Position = new Vector2(-6f, 0f);
        teammate.Position = carrier.Position + new Vector2(8f, 0f);
        state.Players.Single(player => player.Id == 10).Position = carrier.Position + new Vector2(2f, opponentOffLane);
        GivePossession(engine, carrier);
        Assert(
            new SafePossessionPattern().TryRecognize(
                new PlayerDecisionContext(MatchSnapshot.FromState(state), carrier.Id),
                out var opportunity),
            "The carrier should be weighing where to go.");
        return opportunity.PassTargetId;
    }
}

static void PressedCarrierInOwnThirdPassesAtOnce()
{
    Assert(
        TicksUntilPass(carrierX: -10f) == RetainPossessionInstruction.OwnThirdPassDelayTicks,
        "Pressed in its own third, a carrier with a safe pass should play it after a sixth of a second.");
    Assert(
        TicksUntilPass(carrierX: 0f) == RetainPossessionInstruction.MinimumPassDelayTicks,
        "Pressed further up the court, a carrier still holds the ball for half a second first.");

    // Home attacks toward positive X. A presser stands beside the carrier; a teammate is free ahead.
    static int TicksUntilPass(float carrierX)
    {
        var engine = MatchEngine.CreateDefault(userControlledTeam: null);
        var state = engine.State;
        var carrier = state.Players.Single(player => player.Id == 2);
        foreach (var other in state.Players.Where(player => !player.IsGoalkeeper))
        {
            other.Position = new Vector2(other.Team == TeamSide.Home ? -17f : 17f, other.Id - 5f);
        }

        carrier.Position = new Vector2(carrierX, 0f);
        state.Players.Single(player => player.Id == 3).Position = carrier.Position + new Vector2(8f, 0f);
        state.Players.Single(player => player.Id == 10).Position = carrier.Position + new Vector2(0f, 3f);
        GivePossession(engine, carrier);
        var context = new PlayerDecisionContext(MatchSnapshot.FromState(state), carrier.Id);
        var instruction = new RetainPossessionInstruction(new SafePossessionPattern());
        for (var tick = 1; tick <= 60; tick++)
        {
            if (instruction.Evaluate(context)?.Input.ActionDown == true)
            {
                return tick;
            }
        }

        return int.MaxValue;
    }
}

static void ComputerCarrierTurnsAQuarterAtATime()
{
    // Away's left ala has just run back toward its own goal to collect the ball, with the whole
    // court behind it. Turning right round at speed swings the ball round the body, and over
    // 100 matches that cost computer carriers the ball 198 times a match.
    var engine = MatchEngine.CreateDefault(userControlledTeam: null);
    var state = engine.State;
    var carrier = state.Players.Single(player => player.Id == 5);
    foreach (var other in state.Players.Where(player => player.Id != carrier.Id && !player.IsGoalkeeper))
    {
        other.Position = new Vector2(other.Team == TeamSide.Home ? -17f : 17f, other.Id - 5f);
    }

    carrier.Position = new Vector2(3f, 5f);
    carrier.Facing = Azimuth.East;
    GivePossession(engine, carrier);
    Assert(
        new SafePossessionPattern().TryRecognize(
            new PlayerDecisionContext(MatchSnapshot.FromState(state), carrier.Id),
            out var opportunity),
        "The carrier should be weighing where to go.");
    Assert(
        Vector2.Dot(opportunity.CarryDirection, carrier.Facing.ToUnitVector()) >= -0.01f,
        $"The carrier should not turn right round in one go, but chose {opportunity.CarryDirection}.");
}

static void MatchWithNoUserTeamTreatsBothGoalkeepersAlike()
{
    // Home's keeper would otherwise wait four seconds for a person who is not there.
    foreach (var team in new[] { TeamSide.Home, TeamSide.Away })
    {
        var engine = MatchEngine.CreateDefault(userControlledTeam: null);
        var goalkeeper = engine.State.Players.Single(player =>
            player.Team == team && player.Role == PlayerRole.Goalkeeper);
        var towardGoal = team == TeamSide.Home ? -1f : 1f;
        ClearPossession(engine);
        engine.State.Ball.Position = goalkeeper.Position - new Vector2(6f * towardGoal, 0f);
        engine.State.Ball.Velocity = new Vector2(6f * towardGoal, 0f);
        engine.State.Ball.Height = 0.5f;
        engine.StepTick(PlayerControlFrame.Empty);
        Assert(engine.State.Ball.PossessorId == goalkeeper.Id, $"The {team} keeper should catch the shot.");

        for (var tick = 0; tick < engine.Tuning.GoalkeeperAiDistributionDelayTicks; tick++)
        {
            engine.StepTick(PlayerControlFrame.Empty);
        }

        Assert(engine.State.Ball.PossessorId is null, $"The {team} keeper should distribute after the computer delay.");
        Assert(engine.State.ActivePlayerId == 0, "Nobody should be the active player.");
    }
}

static PlayerState CatchBallWithHomeGoalkeeper(MatchEngine engine)
{
    var goalkeeper = engine.State.Players.Single(player =>
        player.Team == TeamSide.Home && player.Role == PlayerRole.Goalkeeper);
    ClearPossession(engine);
    engine.State.Ball.Position = goalkeeper.Position + new Vector2(0.5f, 0f);
    engine.State.Ball.Height = 0f;
    engine.StepTick(PlayerInput.Empty);
    Assert(engine.State.Ball.PossessorId == goalkeeper.Id, "Test setup should result in a home goalkeeper catch.");
    return goalkeeper;
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

static void HeldActionCommitsShotBeforeRelease()
{
    var engine = MatchEngine.CreateDefault();

    StepMany(engine, new PlayerInput(Vector2.UnitX, ActionDown: true), engine.Tuning.TapPassMaxTicks);
    Assert(engine.State.ActivePlayer.HasPossession, "The tap window must remain available for a pass.");

    engine.StepTick(new PlayerInput(Vector2.UnitX, ActionDown: true));

    Assert(!engine.State.ActivePlayer.HasPossession, "Crossing the tap window should commit a shot without waiting for release.");
    Assert(engine.State.Ball.PossessorId is null, "Kicked ball should be loose.");
    Assert(engine.State.Ball.Velocity.X > 0f, "Ball should travel in the input direction.");
    Assert(engine.State.ActivePlayer.KickAnimation01 > 0f, "Shot contact should start visible follow-through immediately.");
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
    foreach (var otherPlayer in engine.State.Players.Where(candidate => candidate.Id != player.Id))
    {
        otherPlayer.Position = new Vector2(15f, 8f);
    }

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

    // 120 ticks stops short of the far goal, where carrying the ball over the line ends the run.
    for (var i = 0; i < 120; i++)
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

static void PassGoesPastOpponentBesideItsLine()
{
    const int Teammate = 2;
    const int Nobody = 0;
    // Beside the ball as it is struck, as a presser stands: within possession distance, off the line.
    var beside = new Vector2(2f, 2.3f);
    Assert(
        FirstToTakePass(PhysicsTuning.Default, beside, out _) == Teammate,
        "A pass should go past an opponent standing 2.3 m to the side of its line.");
    // With a reach wide enough to cover that opponent, the ball has still gone past it when it may first be taken.
    Assert(
        FirstToTakePass(PhysicsTuning.Default with { FastBallCollectionReach = 2f }, beside, out _) == Teammate,
        "A pass that has gone past an opponent should not be taken by it.");
    var taker = FirstToTakePass(PhysicsTuning.Default, new Vector2(6f, 0.5f), out var opponentId);
    Assert(taker == opponentId, "An opponent standing on the line should still take the pass.");
    // Walked 2.5 m off the line, the teammate the pass was played to still takes it.
    Assert(
        FirstToTakePass(PhysicsTuning.Default, new Vector2(-10f, 5f), out _, receiverStep: new Vector2(0f, 2.5f)) == Teammate,
        "The teammate a pass was played to should take it from further off its line than anyone else.");
    Assert(
        FirstToTakePass(PhysicsTuning.Default, new Vector2(-10f, 5f), out _, receiverStep: new Vector2(0f, 3.5f)) != Teammate,
        "A teammate further away than possession distance should not take the pass; if it does, the last check proves nothing.");

    static int FirstToTakePass(PhysicsTuning tuning, Vector2 opponentOffset, out int opponentId, Vector2 receiverStep = default)
    {
        var engine = MatchEngine.CreateDefault(tuning);
        var passer = engine.State.ActivePlayer;
        var teammate = engine.State.Players.Single(player => player.Id == Teammate);
        var opponent = engine.State.Players.First(player => player.Team == TeamSide.Away && player.Role == PlayerRole.Outfield);
        opponentId = opponent.Id;
        foreach (var other in engine.State.Players.Where(player => player.Role == PlayerRole.Outfield))
        {
            other.Position = new Vector2(-15f, -8f);
        }

        passer.Position = new Vector2(-8f, 0f);
        teammate.Position = passer.Position + new Vector2(14f, 0f);
        opponent.Position = passer.Position + opponentOffset;
        engine.State.Ball.Position = passer.Position + passer.Facing.ToUnitVector() * engine.Tuning.DribbleLeadDistance;

        StepMany(engine, new PlayerInput(Vector2.Zero, ActionDown: true), engine.Tuning.TapPassMaxTicks);
        engine.StepTick(PlayerInput.Empty);
        teammate.Position += receiverStep;
        for (var tick = 0; tick < 120 && engine.State.Ball.PossessorId is null && !engine.State.IsBallOutOfPlay; tick++)
        {
            engine.StepTick(PlayerInput.Empty);
        }

        return engine.State.Ball.PossessorId ?? Nobody;
    }
}

static void PassOutsideIntendedDirectionStaysUntargeted()
{
    var engine = MatchEngine.CreateDefault();
    var passer = engine.State.ActivePlayer;
    var nearSoutheastTeammate = engine.State.Players.Single(player => player.Id == 2);
    var otherTeammate = engine.State.Players.Single(player => player.Id == 3);
    nearSoutheastTeammate.Position = passer.Position + new Vector2(18f, 10.5f);
    otherTeammate.Position = passer.Position - new Vector2(18f, 0f);
    MoveOtherHomeOutfieldersBehindPasser(engine, nearSoutheastTeammate.Id);

    StepMany(engine, new PlayerInput(Vector2.Zero, ActionDown: true), engine.Tuning.TapPassMaxTicks);
    engine.StepTick(PlayerInput.Empty);

    Assert(engine.State.ActivePlayerId == passer.Id, "An east pass should not snap to a teammate in the southeast sector.");
    Assert(MathF.Abs(engine.State.Ball.Velocity.Y) < 0.0001f, "An untargeted pass should retain the passer's east facing.");
}

static void PassInsideIntendedDirectionTargetsTeammate()
{
    var engine = MatchEngine.CreateDefault();
    var passer = engine.State.ActivePlayer;
    var intendedTeammate = engine.State.Players.Single(player => player.Id == 2);
    var otherTeammate = engine.State.Players.Single(player => player.Id == 3);
    intendedTeammate.Position = passer.Position + new Vector2(20f, 7f);
    otherTeammate.Position = passer.Position - new Vector2(18f, 0f);
    MoveOtherHomeOutfieldersBehindPasser(engine, intendedTeammate.Id);

    StepMany(engine, new PlayerInput(Vector2.Zero, ActionDown: true), engine.Tuning.TapPassMaxTicks);
    engine.StepTick(PlayerInput.Empty);

    Assert(engine.State.ActivePlayerId == intendedTeammate.Id, "A teammate inside the east intent sector should receive assistance.");
    Assert(engine.State.Ball.Velocity.Y > 0f, "The assisted pass should lead toward the intended teammate.");
}

static void PassBeyondAssistanceRangeStaysUntargeted()
{
    var engine = MatchEngine.CreateDefault();
    var passer = engine.State.ActivePlayer;
    var distantTeammate = engine.State.Players.Single(player => player.Id == 2);
    var otherTeammate = engine.State.Players.Single(player => player.Id == 3);
    distantTeammate.Position = passer.Position + new Vector2(40f, 0f);
    otherTeammate.Position = passer.Position - new Vector2(18f, 0f);
    MoveOtherHomeOutfieldersBehindPasser(engine, distantTeammate.Id);

    StepMany(engine, new PlayerInput(Vector2.Zero, ActionDown: true), engine.Tuning.TapPassMaxTicks);
    engine.StepTick(PlayerInput.Empty);

    Assert(engine.State.ActivePlayerId == passer.Id, "A teammate beyond the practical pass-assistance range should not capture the pass.");
}

static void DiagonalGoalShotSelectsIntendedSide()
{
    var tuning = PhysicsTuning.Default with { PlayerDecelerationPerTick = 0f };
    var engine = MatchEngine.CreateDefault(tuning);
    var shooter = engine.State.ActivePlayer;
    shooter.Position = new Vector2(12f, 0f);
    shooter.Facing = new Azimuth(135f);
    shooter.Velocity = Vector2.Zero;
    engine.State.Ball.Position = shooter.Position + shooter.Facing.ToUnitVector() * tuning.DribbleLeadDistance;

    StepMany(engine, new PlayerInput(Vector2.Zero, ActionDown: true), tuning.TapPassMaxTicks + 1);

    var goalLineY = ProjectedYAtX(engine.State.Ball, engine.State.Field.Right);
    Assert(goalLineY > engine.State.Field.GoalBottom * 0.55f, "Southeast should express a clear lower-side goal intent.");
    Assert(goalLineY < engine.State.Field.GoalBottom, "A southeast shot in range should be shaped inside the lower post.");
}

static void AwayDiagonalGoalShotMirrorsHomeAiming()
{
    var engine = MatchEngine.CreateDefault();
    var shooter = engine.State.Players.Single(player => player.Id == 4);
    ClearPossession(engine);
    engine.State.ActivePlayerId = shooter.Id;
    shooter.Position = new Vector2(-12f, 0f);
    shooter.Facing = new Azimuth(315f);
    shooter.HasPossession = true;
    engine.State.Ball.PossessorId = shooter.Id;
    engine.State.Ball.ControlKind = BallControlKind.Feet;
    engine.State.Ball.Position = shooter.Position + shooter.Facing.ToUnitVector() * engine.Tuning.DribbleLeadDistance;

    StepMany(engine, new PlayerInput(Vector2.Zero, ActionDown: true), engine.Tuning.TapPassMaxTicks + 1);

    var goalLineY = ProjectedYAtX(engine.State.Ball, engine.State.Field.Left);
    Assert(goalLineY < engine.State.Field.GoalTop * 0.55f, "Northwest should express a clear upper-side goal intent.");
    Assert(goalLineY > engine.State.Field.GoalTop, "An away northwest shot in range should be shaped inside the upper post.");
}

static void ShotAimedAwayFromGoalStaysUnassisted()
{
    var engine = MatchEngine.CreateDefault();
    var shooter = engine.State.ActivePlayer;
    shooter.Position = new Vector2(12f, 4f);
    shooter.Facing = new Azimuth(135f);
    engine.State.Ball.Position = shooter.Position + shooter.Facing.ToUnitVector() * engine.Tuning.DribbleLeadDistance;

    StepMany(engine, new PlayerInput(Vector2.Zero, ActionDown: true), engine.Tuning.TapPassMaxTicks + 1);

    var goalLineY = ProjectedYAtX(engine.State.Ball, engine.State.Field.Right);
    Assert(goalLineY > engine.State.Field.GoalBottom, "An outward-facing diagonal must not be rescued into the goal.");
}

static void SideInputAfterContactCurvesShot()
{
    var engine = MatchEngine.CreateDefault();

    StepMany(engine, new PlayerInput(Vector2.Zero, ActionDown: true), engine.Tuning.TapPassMaxTicks + 1);
    engine.StepTick(new PlayerInput(Vector2.UnitY, ActionDown: false));

    Assert(engine.State.Ball.Velocity.Y > 0.001f, "Side input immediately after contact should bend the shot.");
}

static void ForwardAndBackwardAftertouchChangeHeight()
{
    var neutral = MatchEngine.CreateDefault();
    var driven = MatchEngine.CreateDefault();
    var lofted = MatchEngine.CreateDefault();

    StepMany(neutral, new PlayerInput(Vector2.Zero, ActionDown: true), neutral.Tuning.TapPassMaxTicks + 1);
    StepMany(driven, new PlayerInput(Vector2.Zero, ActionDown: true), driven.Tuning.TapPassMaxTicks + 1);
    StepMany(lofted, new PlayerInput(Vector2.Zero, ActionDown: true), lofted.Tuning.TapPassMaxTicks + 1);

    neutral.StepTick(PlayerInput.Empty);
    driven.StepTick(new PlayerInput(Vector2.UnitX, ActionDown: false));
    lofted.StepTick(new PlayerInput(-Vector2.UnitX, ActionDown: false));

    Assert(
        driven.State.Ball.VerticalVelocity < neutral.State.Ball.VerticalVelocity,
        "Forward aftertouch should drive the shot lower than neutral input.");
    Assert(
        lofted.State.Ball.VerticalVelocity > neutral.State.Ball.VerticalVelocity,
        "Backward aftertouch should lift the shot higher than neutral input.");
}

static void LateHeightInputCannotRelaunchBall()
{
    var engine = MatchEngine.CreateDefault();

    StepMany(engine, new PlayerInput(Vector2.Zero, ActionDown: true), engine.Tuning.TapPassMaxTicks + 1);
    StepMany(engine, PlayerInput.Empty, engine.Tuning.AftertouchHeightInputTicks);
    var verticalVelocityBeforeLateInput = engine.State.Ball.VerticalVelocity;

    engine.StepTick(new PlayerInput(-Vector2.UnitX, ActionDown: false));

    Assert(
        engine.State.Ball.VerticalVelocity <= verticalVelocityBeforeLateInput,
        "Height input outside the immediate contact window must not make the ball ascend again.");
}

static void GroundSpinDampsWithoutCurlingRollingBall()
{
    var tuning = PhysicsTuning.Default with
    {
        BallCurveMode = BallCurveMode.SwosLikeLut,
        SpinGroundDampingPerTick = 0.94f,
        MinKickSpeedPerTick = 18f / 60f,
        MaxKickSpeedPerTick = 18f / 60f
    };
    var engine = MatchEngine.CreateDefault(tuning);
    var shooter = engine.State.ActivePlayer;
    shooter.Position = Vector2.Zero;
    shooter.Facing = Azimuth.East;
    shooter.Velocity = Vector2.Zero;
    engine.State.Ball.Position = shooter.Position
        + shooter.Facing.ToUnitVector() * engine.Tuning.DribbleLeadDistance;
    foreach (var player in engine.State.Players.Where(player => player.Id != shooter.Id))
    {
        player.Position = new Vector2(player.Team == TeamSide.Home ? -15f : 15f, player.Position.Y < 0f ? -8f : 8f);
    }

    StepMany(engine, new PlayerInput(Vector2.Zero, ActionDown: true), engine.Tuning.TapPassMaxTicks + 1);
    engine.StepTick(new PlayerInput(Vector2.UnitY, ActionDown: false));

    var ticksToGround = 0;
    while (!engine.State.Ball.IsOnGround && ticksToGround < 120)
    {
        engine.StepTick(PlayerInput.Empty);
        ticksToGround++;
    }

    Assert(ticksToGround < 120, "Curved shot should return to the grass.");
    Assert(engine.State.Ball.Velocity.LengthSquared() > 0.0001f, "Ball should still be rolling when it lands.");

    var spinAtLanding = MathF.Abs(engine.State.Ball.CurveSpinZ);
    var directionAtLanding = Vector2.Normalize(engine.State.Ball.Velocity);
    StepMany(engine, PlayerInput.Empty, 8);
    var rollingSpeedSquared = engine.State.Ball.Velocity.LengthSquared();
    var directionAfterRolling = rollingSpeedSquared > 0.0001f
        ? Vector2.Normalize(engine.State.Ball.Velocity)
        : directionAtLanding;

    Assert(
        spinAtLanding < 0.0001f
        || MathF.Abs(engine.State.Ball.CurveSpinZ) < spinAtLanding * 0.15f,
        "Court contact should rapidly remove sidespin.");
    Assert(
        Vector2.Dot(directionAtLanding, directionAfterRolling) > 0.999f,
        "A rolling ball should retain its travel direction instead of curling across the court.");
}

static void HeldShotPassesThroughFriendlyPlayers()
{
    var tuning = PhysicsTuning.Default with
    {
        KickCollectionCooldownTicks = 0,
        MinKickSpeedPerTick = 0f,
        MaxKickSpeedPerTick = 0f
    };
    var kicker = new PlayerState(1, "Kicker", TeamSide.Home, Vector2.Zero)
    {
        Facing = Azimuth.East,
        HasPossession = true,
        Radius = tuning.PlayerRadius
    };
    var teammate = new PlayerState(2, "Teammate", TeamSide.Home, new Vector2(2.67f, 0f))
    {
        Facing = Azimuth.East,
        Radius = tuning.PlayerRadius
    };
    var ball = new BallState
    {
        Radius = tuning.BallRadius,
        Position = kicker.Position + kicker.Facing.ToUnitVector() * tuning.DribbleLeadDistance,
        PossessorId = kicker.Id
    };
    var engine = new MatchEngine(
        new MatchState(FieldDimensions.Default, new[] { kicker, teammate }, ball, activePlayerId: kicker.Id),
        tuning);

    StepMany(engine, new PlayerInput(Vector2.Zero, ActionDown: true), tuning.TapPassMaxTicks + 2);
    Assert(engine.State.Ball.PossessorId is null, "A teammate should not collect a shot while action remains held.");

    engine.StepTick(PlayerInput.Empty);
    Assert(engine.State.Ball.PossessorId == teammate.Id, "The teammate should be able to collect after action is released.");
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
    MoveOtherHomeOutfieldersBehindPasser(engine, teammate.Id);

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
    MoveOtherHomeOutfieldersBehindPasser(engine, receiver.Id);

    StepMany(engine, new PlayerInput(Vector2.Zero, ActionDown: true), engine.Tuning.TapPassMaxTicks);
    engine.StepTick(new PlayerInput(Vector2.Zero, ActionDown: false));
    StepMany(engine, PlayerInput.Empty, 30);

    Assert(engine.State.Ball.PossessorId == opponent.Id, "Opponent near the passing lane should intercept the loose ball.");
    Assert(opponent.HasPossession, "Intercepting opponent should own possession.");
    Assert(
        engine.State.ActivePlayer.Team == TeamSide.Home
            && engine.State.ActivePlayer.Role == PlayerRole.Outfield,
        "An intercepted pass should hand control to a user-team defender.");
    Assert(
        Vector2.Distance(engine.State.ActivePlayer.Position, opponent.Position)
            < Vector2.Distance(receiver.Position, opponent.Position),
        "The defensive handoff should select a player closer to the interceptor than the stranded receiver.");
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

static void HomeAiCollectorBecomesHumanControlledPlayer()
{
    var setupEngine = MatchEngine.CreateDefault(opponentMovementEnabled: false);
    ClearPossession(setupEngine);
    var previousActive = setupEngine.State.ActivePlayer;
    var collector = setupEngine.State.Players.Single(player => player.Id == 2);
    previousActive.Position = new Vector2(-15f, 0f);
    collector.Position = new Vector2(-4f, 0f);
    collector.Velocity = Vector2.Zero;
    foreach (var player in setupEngine.State.Players.Where(player =>
        player.Role == PlayerRole.Outfield
        && player.Id != previousActive.Id
        && player.Id != collector.Id))
    {
        player.Position = new Vector2(player.Team == TeamSide.Home ? -17f : 17f, player.Id % 2 == 0 ? -8f : 8f);
        player.Velocity = Vector2.Zero;
    }

    setupEngine.State.Ball.Position = collector.Position;
    var engine = new MatchEngine(
        setupEngine.State,
        setupEngine.Tuning,
        opponentMovementEnabled: false);
    var computerPlayers = ComputerPlayerController.CreateForTeam(
        engine.CurrentSnapshot,
        engine.UserControlledTeam!.Value);

    engine.StepTick(PlayerInput.Empty, computerPlayers);

    Assert(
        computerPlayers.LastDecisions.ContainsKey(collector.Id),
        "The collecting teammate should have been AI-controlled before gaining possession.");
    Assert(engine.State.Ball.PossessorId == collector.Id, "The home teammate should collect the loose ball.");
    Assert(engine.State.ActivePlayerId == collector.Id, "Human control should follow home-team possession.");

    var collectorPositionBeforeInput = collector.Position;
    engine.StepTick(new PlayerInput(Vector2.UnitY, ActionDown: false), computerPlayers);

    Assert(
        collector.Position.Y > collectorPositionBeforeInput.Y,
        "The next human input should move the new ball carrier.");
    Assert(
        !computerPlayers.LastDecisions.ContainsKey(collector.Id),
        "The newly active player must no longer receive an AI decision.");
    Assert(
        computerPlayers.LastDecisions.ContainsKey(previousActive.Id),
        "The former active player should immediately return to AI control.");

    var awaySetup = MatchEngine.CreateDefault(opponentMovementEnabled: false);
    ClearPossession(awaySetup);
    var awayCollector = awaySetup.State.Players.Single(player => player.Id == 4);
    awayCollector.Position = Vector2.Zero;
    foreach (var player in awaySetup.State.Players.Where(player =>
        player.Role == PlayerRole.Outfield && player.Id != awayCollector.Id))
    {
        player.Position = new Vector2(player.Team == TeamSide.Home ? -17f : 17f, player.Id % 2 == 0 ? -8f : 8f);
        player.Velocity = Vector2.Zero;
    }

    awaySetup.State.Ball.Position = awayCollector.Position;
    var awayEngine = new MatchEngine(
        awaySetup.State,
        awaySetup.Tuning,
        opponentMovementEnabled: false);
    var homeComputerPlayers = ComputerPlayerController.CreateForTeam(
        awayEngine.CurrentSnapshot,
        awayEngine.UserControlledTeam!.Value);

    awayEngine.StepTick(PlayerInput.Empty, homeComputerPlayers);

    Assert(awayEngine.State.Ball.PossessorId == awayCollector.Id, "The stationary opponent should collect the nearby ball.");
    Assert(
        awayEngine.State.ActivePlayer.Team == TeamSide.Home
            && awayEngine.State.ActivePlayer.Role == PlayerRole.Outfield,
        "Away possession should select a defender while keeping control on the user's team.");
    var selectedDefender = awayEngine.State.ActivePlayer;
    var selectedPosition = selectedDefender.Position;
    awayEngine.StepTick(new PlayerInput(Vector2.UnitY, ActionDown: false), homeComputerPlayers);
    Assert(
        !homeComputerPlayers.LastDecisions.ContainsKey(awayEngine.State.ActivePlayerId),
        "The selected defender must be removed from AI control on the next control frame.");
    Assert(
        selectedDefender.Position.Y > selectedPosition.Y,
        "The selected defender should respond to the user's next movement input.");
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

static void EqualCarrierKeepsBallFromBehindButNotInFront()
{
    var behind = new Vector2(-0.7f, 0f);
    var inFront = new Vector2(2.9f, 0f);

    Assert(CarrierKeepsBall(behind, PhysicsTuning.Default), "A carrier should shield the ball from an equal challenger behind it.");
    Assert(!CarrierKeepsBall(inFront, PhysicsTuning.Default), "A carrier should lose the ball to an equal challenger it runs into.");
    Assert(
        !CarrierKeepsBall(behind, PhysicsTuning.Default with { PassiveContestPossessorAdvantage = 0f }),
        "Without the carrier's advantage standing near the ball is enough to take it.");

    static bool CarrierKeepsBall(Vector2 challengerPosition, PhysicsTuning tuning)
    {
        PlayerState Player(int id, TeamSide team, Vector2 position, Azimuth facing) => new(id, "Player", team, position)
        {
            Facing = facing,
            Attributes = PlayerAttributes.DefaultOutfield,
            Radius = tuning.PlayerRadius,
            MaxSpeedPerTick = tuning.PlayerMaxSpeedPerTick,
            AccelerationPerTick = tuning.PlayerAccelerationPerTick,
            FrictionPerTick = tuning.PlayerFrictionPerTick
        };
        var carrier = Player(1, TeamSide.Home, Vector2.Zero, Azimuth.East);
        carrier.HasPossession = true;
        var challenger = Player(2, TeamSide.Away, challengerPosition, Azimuth.West);
        var ball = new BallState
        {
            Radius = tuning.BallRadius,
            Position = carrier.Position + carrier.Facing.ToUnitVector() * tuning.DribbleLeadDistance,
            PossessorId = carrier.Id
        };
        var engine = new MatchEngine(
            new MatchState(FieldDimensions.Default, new[] { carrier, challenger }, ball, activePlayerId: carrier.Id),
            tuning);

        engine.StepTick(PlayerInput.Empty);
        return carrier.HasPossession;
    }
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
    engine.State.Ball.Position = new Vector2(15f, 8f);

    engine.StepTick(new PlayerInput(Vector2.Zero, ActionDown: true));
    Assert(active.IsSliding, "Pressing action without possession should start a slide.");

    var travelTicks = 0;
    while (active.IsSliding && travelTicks < 120)
    {
        engine.StepTick(PlayerInput.Empty);
        travelTicks++;
    }

    Assert(travelTicks == engine.Tuning.SlideTravelTicks, "Slide travel duration should match tuning.");
    Assert(
        travelTicks <= engine.Tuning.TickRate * 0.15f,
        "A default futsal tackle should finish its travel in about 150 ms instead of producing a long slide.");
    Assert(
        engine.Tuning.SlideSpeedPerTick * travelTicks <= 3.5f,
        "A default futsal tackle should cover only a compact challenge distance.");
    Assert(active.IsRecoveringFromSlide, "Slide should enter recovery after travel.");

    var recoveryTicks = 0;
    while (active.IsRecoveringFromSlide && recoveryTicks < 240)
    {
        engine.StepTick(PlayerInput.Empty);
        recoveryTicks++;
    }

    Assert(recoveryTicks == engine.Tuning.SlideRecoveryTicks, "Slide recovery duration should match tuning.");
    Assert(
        recoveryTicks <= engine.Tuning.TickRate * 0.25f,
        "A default futsal tackler should recover in about 250 ms and rejoin play promptly.");
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

    StepMany(engine, PlayerInput.Empty, 6);
    Assert(!carrier.HasPossession, "A tackled player should not pick the ball straight back up.");
}

static void HoldingAfterContactCannotAccelerateShot()
{
    var engine = MatchEngine.CreateDefault();
    StepMany(engine, new PlayerInput(Vector2.Zero, ActionDown: true), engine.Tuning.TapPassMaxTicks + 1);

    var previousSpeed = engine.State.Ball.Velocity.Length();
    var previousVerticalVelocity = engine.State.Ball.VerticalVelocity;
    for (var i = 0; i < 10; i++)
    {
        engine.StepTick(new PlayerInput(Vector2.Zero, ActionDown: true));

        Assert(
            engine.State.Ball.Velocity.Length() <= previousSpeed + 0.0001f,
            "Holding after contact must not add forward speed to the ball.");
        Assert(
            engine.State.Ball.VerticalVelocity <= previousVerticalVelocity + 0.0001f,
            "Holding after contact must not inject additional lift.");
        previousSpeed = engine.State.Ball.Velocity.Length();
        previousVerticalVelocity = engine.State.Ball.VerticalVelocity;
    }
}

static void ShotLiftIsAppliedEntirelyAtContact()
{
    var engine = MatchEngine.CreateDefault();

    StepMany(engine, new PlayerInput(Vector2.Zero, ActionDown: true), engine.Tuning.TapPassMaxTicks + 1);
    var launchVerticalVelocity = engine.State.Ball.VerticalVelocity;

    Assert(launchVerticalVelocity > 0f, "Shot contact should establish upward velocity immediately.");
    Assert(Near(engine.State.Ball.Height, 0f), "Ball should still be at foot height on the contact tick.");

    engine.StepTick(PlayerInput.Empty);

    Assert(engine.State.Ball.Height > 0f, "Shot should rise on the first post-contact tick.");
    Assert(
        engine.State.Ball.VerticalVelocity < launchVerticalVelocity,
        "Gravity should reduce vertical velocity immediately after contact.");
}

static void LongPassLiftsBall()
{
    var engine = MatchEngine.CreateDefault();
    var teammate = engine.State.Players.Single(player => player.Id == 2);
    teammate.Position = engine.State.ActivePlayer.Position + new Vector2(engine.Tuning.LongPassDistance + 2f, 0f);
    MoveOtherHomeOutfieldersBehindPasser(engine, teammate.Id);

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

static void InputBufferPreservesQuickTap()
{
    var engine = MatchEngine.CreateDefault();
    var inputBuffer = new PlayerInputBuffer();

    inputBuffer.Capture(new PlayerInput(Vector2.Zero, ActionDown: true));
    var ticks = engine.Step(engine.Tuning.TickSeconds * 0.5f, inputBuffer);
    Assert(ticks == 0, "The press should remain queued when no fixed tick runs.");

    inputBuffer.Capture(PlayerInput.Empty);
    ticks = engine.Step(engine.Tuning.TickSeconds * 1.5f, inputBuffer);

    Assert(ticks == 2, "The accumulated time should consume the buffered press and release on separate ticks.");
    Assert(engine.State.Ball.PossessorId is null, "The preserved quick tap should execute a pass.");
    Assert(inputBuffer.PendingActionTransitions == 0, "Both buffered transitions should be consumed.");
}

static void ActionFeedbackEventsAreExactlyOnce()
{
    var engine = MatchEngine.CreateDefault();

    engine.StepTick(new PlayerInput(Vector2.Zero, ActionDown: true));
    StepMany(engine, new PlayerInput(Vector2.Zero, ActionDown: true), engine.Tuning.TapPassMaxTicks - 1);
    engine.StepTick(PlayerInput.Empty);
    StepMany(engine, PlayerInput.Empty, 4);

    var actionEvents = engine.State.FeedbackEvents
        .Where(feedbackEvent => feedbackEvent.Type == GameplayFeedbackType.ActionAccepted)
        .ToArray();
    var contactEvents = engine.State.FeedbackEvents
        .Where(feedbackEvent => feedbackEvent.Type == GameplayFeedbackType.PassContact)
        .ToArray();

    Assert(actionEvents.Length == 1, "One physical press should emit one accepted-intent event.");
    Assert(contactEvents.Length == 1, "One pass should emit one authoritative contact event.");
    Assert(actionEvents[0].Sequence < contactEvents[0].Sequence, "Intent acknowledgement should precede contact.");
    Assert(contactEvents[0].Tick > actionEvents[0].Tick, "A tap-pass contact should occur on the later release tick.");
}

static void FeedbackRoutesOnlyToLocalTeam()
{
    var localTeamEvent = new GameplayFeedbackEvent(
        Sequence: 1,
        Tick: 1,
        Type: GameplayFeedbackType.PassContact,
        PlayerId: 1,
        Team: TeamSide.Home,
        Position: Vector2.Zero,
        Direction: Vector2.UnitX,
        Intensity: 1f);
    var opponentEvent = localTeamEvent with { Sequence = 2, Team = TeamSide.Away };
    var globalEvent = localTeamEvent with { Sequence = 3, Team = null };

    Assert(
        GameplayFeedbackRouting.IsRelevantToTeam(localTeamEvent, TeamSide.Home),
        "A local-team event should reach local feedback.");
    Assert(
        !GameplayFeedbackRouting.IsRelevantToTeam(opponentEvent, TeamSide.Home),
        "An opponent event should not reach local feedback.");
    Assert(
        GameplayFeedbackRouting.IsRelevantToTeam(globalEvent, TeamSide.Home),
        "A teamless match event should reach local feedback.");
}

static void FeedbackStereoPanFollowsEventDirection()
{
    var rightEvent = new GameplayFeedbackEvent(
        Sequence: 1,
        Tick: 1,
        Type: GameplayFeedbackType.PassContact,
        PlayerId: 1,
        Team: TeamSide.Home,
        Position: Vector2.Zero,
        Direction: Vector2.UnitX,
        Intensity: 1f);
    var leftEvent = rightEvent with { Sequence = 2, Direction = -Vector2.UnitX };
    var centeredEvent = rightEvent with { Sequence = 3, Direction = Vector2.Zero };

    Assert(Near(GameplayFeedbackRouting.GetStereoPan(rightEvent), 1f), "Rightward feedback should pan right.");
    Assert(Near(GameplayFeedbackRouting.GetStereoPan(leftEvent), -1f), "Leftward feedback should pan left.");
    Assert(Near(GameplayFeedbackRouting.GetStereoPan(centeredEvent), 0f), "Directionless feedback should stay centered.");
}

static void RenderSnapshotPresentsCurrentActionIntent()
{
    var engine = MatchEngine.CreateDefault();

    engine.StepTick(new PlayerInput(Vector2.Zero, ActionDown: true));
    var renderPlayer = engine.GetRenderSnapshot().ActivePlayer;

    Assert(engine.RenderAlpha == 0f, "A direct fixed tick should leave render alpha at zero.");
    Assert(renderPlayer.ActionCharge01 > 0f, "Current action intent should be visible without waiting for interpolation alpha.");
}

static void RenderSnapshotPresentsContactPose()
{
    var engine = MatchEngine.CreateDefault();

    engine.StepTick(new PlayerInput(Vector2.Zero, ActionDown: true));
    engine.StepTick(PlayerInput.Empty);
    var renderPlayer = engine.GetRenderSnapshot().Players.Single(player => player.Id == 1);

    Assert(engine.RenderAlpha == 0f, "A direct fixed tick should leave render alpha at zero.");
    Assert(renderPlayer.KickAnimation01 == 1f, "The contact pose should appear on the same tick as ball launch.");
    Assert(
        engine.CurrentSnapshot.FeedbackEvents.Any(feedbackEvent => feedbackEvent.Type == GameplayFeedbackType.PassContact),
        "The contact snapshot should carry the matching pass feedback event.");
}

static void ComputerPlayerSkipsUnavailableInstructionsAndBreaksTies()
{
    var context = CreatePlayerDecisionContext();
    var firstTiedCandidate = new PlayerInstructionCandidate(
        PlayerInstructionKind.MaintainPosition,
        PlayerInstructionPriorityBand.TacticalOpportunity,
        0.6f,
        PlayerInput.Empty);
    var secondTiedCandidate = new PlayerInstructionCandidate(
        PlayerInstructionKind.RecoverLooseBall,
        PlayerInstructionPriorityBand.TacticalOpportunity,
        0.6f,
        new PlayerInput(Vector2.UnitX, ActionDown: false));
    var lowerBandCandidate = new PlayerInstructionCandidate(
        PlayerInstructionKind.RecoverLooseBall,
        PlayerInstructionPriorityBand.Shape,
        100f,
        new PlayerInput(Vector2.UnitY, ActionDown: false));
    var brain = new ComputerPlayerBrain(new IPlayerInstruction[]
    {
        new StubPlayerInstruction(candidate: null),
        new StubPlayerInstruction(firstTiedCandidate),
        new StubPlayerInstruction(secondTiedCandidate),
        new StubPlayerInstruction(lowerBandCandidate)
    });

    var decision = brain.Decide(context);

    Assert(
        decision.Kind == PlayerInstructionKind.MaintainPosition,
        "Unavailable instructions should be skipped, priority bands should outrank scores, and exact ties should keep registration order.");
    Assert(decision.Input == PlayerInput.Empty, "The selected instruction should emit its normal PlayerInput.");
}

static void TacticalPrioritiesChangeInstructionSelection()
{
    var context = CreatePlayerDecisionContext();
    var maintain = new PlayerInstructionCandidate(
        PlayerInstructionKind.MaintainPosition,
        PlayerInstructionPriorityBand.TacticalOpportunity,
        0.7f,
        PlayerInput.Empty);
    var recover = new PlayerInstructionCandidate(
        PlayerInstructionKind.RecoverLooseBall,
        PlayerInstructionPriorityBand.TacticalOpportunity,
        0.5f,
        new PlayerInput(Vector2.UnitX, ActionDown: false));
    var brain = new ComputerPlayerBrain(new IPlayerInstruction[]
    {
        new StubPlayerInstruction(maintain),
        new StubPlayerInstruction(recover)
    });

    var neutralDecision = brain.Decide(context);
    var aggressivePriorities = new TacticalPriorities(
        new Dictionary<PlayerInstructionKind, float>
        {
            [PlayerInstructionKind.RecoverLooseBall] = 0.4f
        });
    var aggressiveDecision = brain.Decide(context, aggressivePriorities);

    Assert(
        neutralDecision.Kind == PlayerInstructionKind.MaintainPosition,
        "Neutral tactics should retain the instruction with the strongest situational score.");
    Assert(
        aggressiveDecision.Kind == PlayerInstructionKind.RecoverLooseBall,
        "A tactical modifier should change selection between instructions in the same priority band.");
    Assert(
        aggressiveDecision.Input.Move == Vector2.UnitX,
        "Changing tactical priorities must select a different normal input without changing player physics.");
}

static void ClosestTeammateRecognizesAndPursuesLooseBall()
{
    var engine = MatchEngine.CreateDefault();
    ClearPossession(engine);
    var outfielders = engine.State.Players
        .Where(player => player.Team == TeamSide.Home && player.Role == PlayerRole.Outfield)
        .OrderBy(player => player.Id)
        .ToArray();
    outfielders[0].Position = Vector2.Zero;
    outfielders[1].Position = new Vector2(12f, 4f);
    outfielders[2].Position = new Vector2(14f, -4f);
    engine.State.Ball.Position = new Vector2(3f, 0f);
    var snapshot = MatchSnapshot.FromState(engine.State);
    var brain = ComputerPlayerBrain.CreateDefault();

    var closestDecision = brain.Decide(new PlayerDecisionContext(snapshot, outfielders[0].Id));
    var fartherDecision = brain.Decide(new PlayerDecisionContext(snapshot, outfielders[1].Id));

    Assert(
        closestDecision.Kind == PlayerInstructionKind.RecoverLooseBall,
        "The closest eligible teammate should recognize the loose-ball recovery pattern.");
    Assert(
        Vector2.Distance(closestDecision.Input.Move, Vector2.UnitX) < 0.0001f,
        "The recovery instruction should emit normalized movement toward the ball.");
    Assert(!closestDecision.Input.ActionDown, "Recovering a loose ball should not press the action button.");
    Assert(
        fartherDecision.Kind != PlayerInstructionKind.RecoverLooseBall,
        "A farther teammate should fall back to shape instead of duplicating loose-ball recovery.");
}

static void BallCarrierSelectsSafeSpaceAndClearPassingLane()
{
    var engine = MatchEngine.CreateDefault();
    ClearPossession(engine);
    var possessor = engine.State.Players.Single(player => player.Id == 4);
    var blockedReceiver = engine.State.Players.Single(player => player.Id == 6);
    possessor.Position = new Vector2(8f, 0f);
    possessor.Facing = Azimuth.West;
    blockedReceiver.Position = Vector2.Zero;
    engine.State.Players.Single(player => player.Id == 5).Position = new Vector2(1f, -6f);
    engine.State.Players.Single(player => player.Id == 10).Position = new Vector2(3f, 7f);
    var blocker = engine.State.Players.Single(player => player.Id == 1);
    blocker.Position = new Vector2(4f, 0f);
    foreach (var opponent in engine.State.Players.Where(player =>
        player.Team == TeamSide.Home && player.Id != blocker.Id))
    {
        opponent.Position = new Vector2(-18f, opponent.Id % 2 == 0 ? -8f : 8f);
    }

    GivePossession(engine, possessor);
    var snapshot = MatchSnapshot.FromState(engine.State);
    var context = new PlayerDecisionContext(snapshot, possessor.Id);
    var pattern = new SafePossessionPattern();

    Assert(
        pattern.TryRecognize(context, out var opportunity),
        "An outfield ball carrier should recognize its possession options.");
    Assert(
        opportunity.CarryDirection.LengthSquared() > 0.99f,
        "The carrier should select a normalized direction into safer nearby space.");
    Assert(opportunity.PassTargetId is not null, "A clear teammate should be recognized as a passing outlet.");
    Assert(
        opportunity.PassTargetId != blockedReceiver.Id,
        "A teammate behind an opponent on the passing lane must not be selected.");

    var instruction = new RetainPossessionInstruction(pattern);
    PlayerInstructionCandidate? press = null;
    for (var tick = 0; tick < 60 && press?.Input.ActionDown != true; tick++)
    {
        press = instruction.Evaluate(context);
    }

    var pressedCandidate = press
        ?? throw new InvalidOperationException("The possession instruction did not select a safe pass in time.");
    Assert(pressedCandidate.Kind == PlayerInstructionKind.RetainPossession, "The possessor should use its dedicated possession instruction.");
    Assert(pressedCandidate.Input.ActionDown, "A safe pass should begin with a normal action-button press.");
    var releaseCandidate = instruction.Evaluate(context)
        ?? throw new InvalidOperationException("The possession instruction did not finish its pass input.");
    Assert(!releaseCandidate.Input.ActionDown, "The following tick should release the button and commit a tap pass.");
    Assert(
        Vector2.Distance(pressedCandidate.Input.Move, releaseCandidate.Input.Move) < 0.0001f,
        "The press and release should keep the same intended passing direction.");

    var markers = engine.State.Players
        .Where(player => player.Team == TeamSide.Home)
        .OrderBy(player => player.Id)
        .ToArray();
    var receivers = engine.State.Players
        .Where(player => player.Team == TeamSide.Away && player.Role == PlayerRole.Outfield && player.Id != possessor.Id)
        .OrderBy(player => player.Id)
        .ToArray();
    for (var index = 0; index < receivers.Length; index++)
    {
        markers[index].Position = receivers[index].Position;
    }

    Assert(
        pattern.TryRecognize(
            new PlayerDecisionContext(MatchSnapshot.FromState(engine.State), possessor.Id),
            out var closedOpportunity),
        "The carrier should still recognize carry space when passing outlets close.");
    Assert(closedOpportunity.PassTargetId is null, "The carrier should keep the ball rather than force a marked pass.");
}

static void LiveAwayAiCarriesAndCirculatesPossession()
{
    var setup = MatchEngine.CreateDefault();
    ClearPossession(setup);
    var carrier = setup.State.Players.Single(player => player.Id == 4);
    carrier.Position = new Vector2(8f, 0f);
    carrier.Facing = Azimuth.West;
    setup.State.Players.Single(player => player.Id == 5).Position = new Vector2(1f, -5f);
    setup.State.Players.Single(player => player.Id == 6).Position = new Vector2(1f, 5f);
    setup.State.Players.Single(player => player.Id == 10).Position = new Vector2(0f, 0f);
    foreach (var opponent in setup.State.Players.Where(player => player.Team == TeamSide.Home))
    {
        opponent.Position = new Vector2(-18f, Math.Clamp(opponent.Position.Y, -8f, 8f));
    }

    GivePossession(setup, carrier);
    var engine = new MatchEngine(setup.State, setup.Tuning);
    var computerPlayers = ComputerPlayerController.CreateDefault(engine.CurrentSnapshot);
    var carrierStart = carrier.Position;
    var passOccurred = false;
    var teammateReceived = false;

    for (var tick = 0; tick < 120 && !teammateReceived; tick++)
    {
        engine.StepTick(PlayerInput.Empty, computerPlayers);
        if (tick == 0)
        {
            Assert(
                computerPlayers.LastDecisions[carrier.Id].Kind == PlayerInstructionKind.RetainPossession,
                "The live opposing ball carrier should select possession play instead of standing still.");
        }

        passOccurred |= engine.State.FeedbackEvents.Any(feedbackEvent =>
            feedbackEvent.Type == GameplayFeedbackType.PassContact
            && feedbackEvent.Team == TeamSide.Away);
        teammateReceived |= passOccurred && engine.State.Players.Any(player =>
            player.Team == TeamSide.Away
            && player.Id != carrier.Id
            && player.HasPossession);
    }

    Assert(
        Vector2.Distance(carrier.Position, carrierStart) > 1f,
        "The AI carrier should advance into safer space before releasing the ball.");
    Assert(passOccurred, "The AI carrier should execute a real pass through the match action system.");
    Assert(teammateReceived, "A teammate should collect the pass so Away retains possession.");
    engine.StepTick(PlayerInput.Empty, computerPlayers);
    Assert(
        engine.State.ActivePlayer.Team == TeamSide.Home
            && engine.State.ActivePlayer.Role == PlayerRole.Outfield,
        "Opponent circulation should keep control on a selected user-team defender.");
    Assert(
        !computerPlayers.LastDecisions.ContainsKey(engine.State.ActivePlayerId),
        "The currently selected defender must remain reserved for human input.");
}

static void TeamShapeFollowsActiveTeammate()
{
    var engine = MatchEngine.CreateDefault();
    var activePlayer = engine.State.ActivePlayer;
    var leftAla = engine.State.Players.Single(player =>
        player.Team == TeamSide.Home && player.FutsalRole == FutsalOutfieldRole.LeftAla);
    var rightAla = engine.State.Players.Single(player =>
        player.Team == TeamSide.Home && player.FutsalRole == FutsalOutfieldRole.RightAla);
    activePlayer.Position += new Vector2(4f, 4f);
    var snapshot = MatchSnapshot.FromState(engine.State);
    var pattern = new FutsalTeamShapePattern();

    Assert(
        pattern.TryRecognize(new PlayerDecisionContext(snapshot, leftAla.Id), out var leftTarget),
        "The left ala should recognize its shifted shape target.");
    Assert(
        pattern.TryRecognize(new PlayerDecisionContext(snapshot, rightAla.Id), out var rightTarget),
        "The right ala should recognize its shifted shape target.");
    Assert(leftTarget.Target.X > leftAla.Position.X, "The left ala should advance when the fixo advances.");
    Assert(leftTarget.Target.Y > leftAla.Position.Y, "The left ala should slide laterally with the active teammate.");
    Assert(rightTarget.Target.Y > leftTarget.Target.Y, "The two alas should preserve distinct sides of the shape.");

    var shapeDecision = new MaintainTeamShapeInstruction(new FutsalTeamShapePattern()).Evaluate(
        new PlayerDecisionContext(snapshot, leftAla.Id));
    Assert(
        shapeDecision?.Kind == PlayerInstructionKind.MaintainTeamShape,
        "Shape should provide normal fallback movement while the ball is possessed.");

    ClearPossession(engine);
    engine.State.Ball.Position = leftAla.Position + Vector2.UnitX;
    var looseBallDecision = ComputerPlayerBrain.CreateDefault().Decide(
        new PlayerDecisionContext(MatchSnapshot.FromState(engine.State), leftAla.Id));
    Assert(
        looseBallDecision.Kind == PlayerInstructionKind.RecoverLooseBall,
        "Urgent loose-ball recovery should override the lower shape band.");
}

static void OffBallTeammateChoosesOpenPassingPosition()
{
    var engine = MatchEngine.CreateDefault();
    var possessor = engine.State.ActivePlayer;
    var leftAla = engine.State.Players.Single(player =>
        player.Team == TeamSide.Home && player.FutsalRole == FutsalOutfieldRole.LeftAla);
    var rightAla = engine.State.Players.Single(player =>
        player.Team == TeamSide.Home && player.FutsalRole == FutsalOutfieldRole.RightAla);
    possessor.Position = Vector2.Zero;
    leftAla.Position = new Vector2(-8f, -8f);
    rightAla.Position = new Vector2(-8f, 8f);
    var blockedTarget = new Vector2(1f, -5f);
    var blocker = engine.State.Players.Single(player => player.Id == 4);
    blocker.Position = blockedTarget;
    foreach (var opponent in engine.State.Players.Where(player =>
        player.Team == TeamSide.Away && player.Id != blocker.Id))
    {
        opponent.Position = new Vector2(16f, opponent.Id % 2 == 0 ? -8f : 8f);
    }

    var snapshot = MatchSnapshot.FromState(engine.State);
    var pattern = new OpenPassingOptionPattern();

    Assert(
        pattern.TryRecognize(new PlayerDecisionContext(snapshot, leftAla.Id), out var leftOpportunity),
        "A non-possessing teammate should recognize a passing-support opportunity.");
    Assert(
        Vector2.Distance(leftOpportunity.Target, blockedTarget) > 0.5f,
        "A marked role target should lose to a clearer nearby support point.");
    Assert(
        leftOpportunity.OpponentClearance > 0.5f,
        "The selected support point should provide meaningful separation from the marker.");
    Assert(
        pattern.TryRecognize(new PlayerDecisionContext(snapshot, rightAla.Id), out var rightOpportunity),
        "The second ala should independently recognize its own support point.");
    Assert(
        leftOpportunity.Target.Y < rightOpportunity.Target.Y,
        "Role-relative support targets should keep the two alas on distinct sides.");
    Assert(
        !pattern.TryRecognize(new PlayerDecisionContext(snapshot, possessor.Id), out _),
        "The player in possession must not execute an off-ball support instruction.");

    var decision = ComputerPlayerBrain.CreateDefault().Decide(
        new PlayerDecisionContext(snapshot, leftAla.Id));
    Assert(decision.Kind == PlayerInstructionKind.OfferSafePass, "Pass support should outrank passive shape maintenance.");
    Assert(decision.Input.Move.LengthSquared() > 0.9f, "The instruction should emit normal movement toward the open point.");

    ClearPossession(engine);
    engine.State.Ball.Position = leftAla.Position + Vector2.UnitX;
    var recoveryDecision = ComputerPlayerBrain.CreateDefault().Decide(
        new PlayerDecisionContext(MatchSnapshot.FromState(engine.State), leftAla.Id));
    Assert(
        recoveryDecision.Kind == PlayerInstructionKind.RecoverLooseBall,
        "Urgent loose-ball recovery should continue to outrank pass support.");
}

static void LiveHomeAiTeammatesMoveToPassingOptions()
{
    var engine = MatchEngine.CreateDefault(opponentMovementEnabled: false);
    var computerPlayers = ComputerPlayerController.CreateForTeam(
        engine.CurrentSnapshot,
        engine.UserControlledTeam!.Value);
    var teammates = engine.State.Players
        .Where(player => player.Team == TeamSide.Home
            && player.Role == PlayerRole.Outfield
            && player.Id != engine.State.ActivePlayerId)
        .OrderBy(player => player.Id)
        .ToArray();
    var positionsBefore = teammates.ToDictionary(player => player.Id, player => player.Position);

    engine.StepTick(PlayerInput.Empty, computerPlayers);

    Assert(
        teammates.All(player => computerPlayers.LastDecisions[player.Id].Kind == PlayerInstructionKind.OfferSafePass),
        "Every available home outfielder should independently select pass-support movement.");
    Assert(
        teammates.All(player => Vector2.Distance(player.Position, positionsBefore[player.Id]) > 0f),
        "The live control frame should move every off-ball teammate toward its selected support point.");
}

static void DefendersMarkDistinctOpponentsFromCompactGoalSidePositions()
{
    var engine = MatchEngine.CreateDefault();
    var snapshot = MatchSnapshot.FromState(engine.State);
    var defenders = snapshot.Players
        .Where(player => player.Team == TeamSide.Away && player.Role == PlayerRole.Outfield)
        .OrderBy(player => player.Id)
        .ToArray();
    var decisions = defenders.ToDictionary(
        defender => defender.Id,
        defender => ComputerPlayerBrain.CreateDefault().Decide(
            new PlayerDecisionContext(snapshot, defender.Id)));
    Assert(
        decisions.Values.Count(decision => decision.Kind == PlayerInstructionKind.PressureBallCarrier) == 1,
        "One away defender should apply direct pressure to the home carrier.");
    Assert(
        decisions.Values.Count(decision => decision.Kind == PlayerInstructionKind.CoverBallCarrier) == 1,
        "One away defender should protect the space behind the presser.");
    Assert(
        decisions.Values.Count(decision => decision.Kind == PlayerInstructionKind.MarkOpponent) == 2,
        "The two remaining away defenders should retain distinct marks.");

    var pattern = new StructuredMarkingPattern();
    var opportunities = defenders
        .Where(defender => decisions[defender.Id].Kind == PlayerInstructionKind.MarkOpponent)
        .Select(defender =>
        {
            Assert(
                pattern.TryRecognize(new PlayerDecisionContext(snapshot, defender.Id), out var opportunity),
                $"Away defender {defender.Id} should recognize a marking assignment.");
            return (Defender: defender, Opportunity: opportunity);
        })
        .ToArray();

    Assert(
        opportunities.Select(item => item.Opportunity.MarkedOpponentId).Distinct().Count() == opportunities.Length,
        "Every dedicated marker should receive a distinct opposing outfielder.");
    foreach (var (defender, opportunity) in opportunities)
    {
        var markedOpponent = snapshot.Players.Single(player => player.Id == opportunity.MarkedOpponentId);
        Assert(
            opportunity.Target.X > markedOpponent.Position.X,
            $"Away defender {defender.Id} should remain goal-side of marked opponent {markedOpponent.Id}.");
        Assert(
            Vector2.Distance(opportunity.Target, opportunity.StructuralAnchor)
                < Vector2.Distance(opportunity.GoalSidePoint, opportunity.StructuralAnchor),
            "The final marking target should retain measurable influence from the compact role anchor.");
        Assert(
            Vector2.Distance(opportunity.Target, markedOpponent.Position) > 0.5f,
            "Structured marking should not collapse directly onto the opponent's position.");
    }

    var homeDefender = snapshot.Players.Single(player =>
        player.Team == TeamSide.Home && player.FutsalRole == FutsalOutfieldRole.LeftAla);
    Assert(
        !pattern.TryRecognize(new PlayerDecisionContext(snapshot, homeDefender.Id), out _),
        "A team in possession should offer passing options rather than run its defensive marking instruction.");

    ClearPossession(engine);
    var awayPossessor = engine.State.Players.Single(player => player.Id == 4);
    GivePossession(engine, awayPossessor);
    var reverseSnapshot = MatchSnapshot.FromState(engine.State);
    var homeDefenders = reverseSnapshot.Players
        .Where(player => player.Team == TeamSide.Home && player.Role == PlayerRole.Outfield)
        .OrderBy(player => player.Id)
        .ToArray();
    var homeDecisions = homeDefenders.ToDictionary(
        defender => defender.Id,
        defender => ComputerPlayerBrain.CreateDefault().Decide(
            new PlayerDecisionContext(reverseSnapshot, defender.Id)));
    Assert(
        homeDecisions[reverseSnapshot.ActivePlayerId].Kind == PlayerInstructionKind.PressureBallCarrier,
        "The selected home defender should recognize that the user is responsible for pressure.");
    Assert(
        homeDecisions.Values.Count(decision => decision.Kind == PlayerInstructionKind.CoverBallCarrier) == 1,
        "A home teammate should cover behind the user-controlled presser.");
    var homeMarker = homeDefenders.First(defender =>
        homeDecisions[defender.Id].Kind == PlayerInstructionKind.MarkOpponent);
    Assert(
        pattern.TryRecognize(new PlayerDecisionContext(reverseSnapshot, homeMarker.Id), out var homeOpportunity),
        "Home markers should use the same goal-side structure when Away possesses the ball.");
    var homeMarkedOpponent = reverseSnapshot.Players.Single(player =>
        player.Id == homeOpportunity.MarkedOpponentId);
    Assert(
        homeOpportunity.Target.X < homeMarkedOpponent.Position.X,
        "A home defender should remain goal-side toward the left goal.");
}

static void LiveTwoTeamAiCombinesPassSupportAndDefensiveMarking()
{
    var engine = MatchEngine.CreateDefault();
    var computerPlayers = ComputerPlayerController.CreateDefault(engine.CurrentSnapshot);
    var homeAiPlayers = engine.State.Players
        .Where(player => player.Team == TeamSide.Home
            && player.Role == PlayerRole.Outfield
            && player.Id != engine.State.ActivePlayerId)
        .OrderBy(player => player.Id)
        .ToArray();
    var awayPlayers = engine.State.Players
        .Where(player => player.Team == TeamSide.Away && player.Role == PlayerRole.Outfield)
        .OrderBy(player => player.Id)
        .ToArray();
    var awayPositionsBefore = awayPlayers.ToDictionary(player => player.Id, player => player.Position);

    engine.StepTick(PlayerInput.Empty, computerPlayers);

    Assert(
        !computerPlayers.LastDecisions.ContainsKey(engine.State.ActivePlayerId),
        "The active human player must remain excluded from AI evaluation.");
    Assert(
        homeAiPlayers.All(player => computerPlayers.LastDecisions[player.Id].Kind == PlayerInstructionKind.OfferSafePass),
        "Home off-ball teammates should continue to make themselves available for a pass.");
    Assert(
        awayPlayers.Count(player =>
            computerPlayers.LastDecisions[player.Id].Kind == PlayerInstructionKind.PressureBallCarrier) == 1,
        "One away outfielder should pressure the home ball carrier.");
    Assert(
        awayPlayers.Count(player =>
            computerPlayers.LastDecisions[player.Id].Kind == PlayerInstructionKind.CoverBallCarrier) == 1,
        "One away outfielder should cover the direct presser.");
    Assert(
        awayPlayers.Count(player =>
            computerPlayers.LastDecisions[player.Id].Kind == PlayerInstructionKind.MarkOpponent) == 2,
        "The remaining away outfielders should preserve distinct structured marks.");
    Assert(
        awayPlayers.All(player => Vector2.Distance(player.Position, awayPositionsBefore[player.Id]) > 0f),
        "Reactivated away players should move toward their individual marking targets.");
}

static void OpponentPossessionSelectsActiveDefenderWithAiCover()
{
    var setup = MatchEngine.CreateDefault();
    ClearPossession(setup);
    var previousActive = setup.State.ActivePlayer;
    var expectedDefender = setup.State.Players.Single(player => player.Id == 9);
    var carrier = setup.State.Players.Single(player => player.Id == 4);
    previousActive.Position = new Vector2(-16f, 0f);
    setup.State.Players.Single(player => player.Id == 2).Position = new Vector2(4f, -5f);
    setup.State.Players.Single(player => player.Id == 3).Position = new Vector2(0f, 6f);
    expectedDefender.Position = new Vector2(5f, 0f);
    carrier.Position = new Vector2(10f, 0f);
    carrier.Facing = Azimuth.West;
    GivePossession(setup, carrier);

    var engine = new MatchEngine(setup.State, setup.Tuning);
    Assert(
        engine.State.ActivePlayerId == expectedDefender.Id,
        "Loading an opponent-possession state should select the best goal-side home defender.");

    var computerPlayers = ComputerPlayerController.CreateDefault(engine.CurrentSnapshot);
    var homeAiPlayers = engine.State.Players
        .Where(player => player.Team == TeamSide.Home
            && player.Role == PlayerRole.Outfield
            && player.Id != engine.State.ActivePlayerId)
        .OrderBy(player => player.Id)
        .ToArray();
    var positionsBefore = homeAiPlayers.ToDictionary(player => player.Id, player => player.Position);

    engine.StepTick(PlayerInput.Empty, computerPlayers);

    Assert(
        !computerPlayers.LastDecisions.ContainsKey(expectedDefender.Id),
        "The selected defender should remain reserved for the human controller.");
    Assert(
        homeAiPlayers.Count(player =>
            computerPlayers.LastDecisions[player.Id].Kind == PlayerInstructionKind.CoverBallCarrier) == 1,
        "Exactly one home AI teammate should cover behind the selected presser.");
    Assert(
        homeAiPlayers.Count(player =>
            computerPlayers.LastDecisions[player.Id].Kind == PlayerInstructionKind.MarkOpponent) == 2,
        "The other two home AI teammates should retain distinct marks.");
    Assert(
        homeAiPlayers.All(player => Vector2.Distance(player.Position, positionsBefore[player.Id]) > 0f),
        "Every non-active home outfielder should visibly move in its defensive duty.");

    var selectedPosition = expectedDefender.Position;
    engine.StepTick(new PlayerInput(Vector2.UnitY, ActionDown: false), computerPlayers);
    Assert(
        expectedDefender.Position.Y > selectedPosition.Y,
        "The automatically selected defender should respond to human movement immediately.");
}

static void ControlFrameMovesMultipleOutfielders()
{
    var engine = MatchEngine.CreateDefault();
    var homePlayer = engine.State.Players.Single(player => player.Id == 1);
    var awayPlayer = engine.State.Players.Single(player => player.Id == 4);
    var stationaryPlayer = engine.State.Players.Single(player => player.Id == 2);
    var homePositionBefore = homePlayer.Position;
    var awayPositionBefore = awayPlayer.Position;
    var stationaryPositionBefore = stationaryPlayer.Position;
    var frame = new PlayerControlFrame(new Dictionary<int, PlayerInput>
    {
        [homePlayer.Id] = new PlayerInput(Vector2.UnitX, ActionDown: false),
        [awayPlayer.Id] = new PlayerInput(-Vector2.UnitX, ActionDown: false)
    });

    engine.StepTick(frame);

    Assert(homePlayer.Position.X > homePositionBefore.X, "The home player should consume its frame input.");
    Assert(awayPlayer.Position.X < awayPositionBefore.X, "The away player should move during the same tick.");
    Assert(
        Vector2.Distance(stationaryPlayer.Position, stationaryPositionBefore) < 0.0001f,
        "A player omitted from the frame should not be moved by the compatibility layer.");
}

static void ControlFrameTracksIndependentActionEdges()
{
    var engine = MatchEngine.CreateDefault();
    ClearPossession(engine);
    var firstPlayer = engine.State.Players.Single(player => player.Id == 1);
    var secondPlayer = engine.State.Players.Single(player => player.Id == 4);

    engine.StepTick(new PlayerControlFrame(new Dictionary<int, PlayerInput>
    {
        [firstPlayer.Id] = new PlayerInput(Vector2.UnitX, ActionDown: true),
        [secondPlayer.Id] = PlayerInput.Empty
    }));

    Assert(firstPlayer.IsSliding, "The first player's independent press edge should start a slide.");
    Assert(!secondPlayer.IsSliding, "Neutral input must not start the second player's slide.");

    engine.StepTick(new PlayerControlFrame(new Dictionary<int, PlayerInput>
    {
        [firstPlayer.Id] = new PlayerInput(Vector2.UnitX, ActionDown: true),
        [secondPlayer.Id] = new PlayerInput(-Vector2.UnitX, ActionDown: true)
    }));

    Assert(
        secondPlayer.IsSliding,
        "The second player's later press must remain an edge even while the first player holds action.");
}

static void LiveComputerControllerPreservesHumanInputAndMovesAi()
{
    var setupEngine = MatchEngine.CreateDefault();
    ClearPossession(setupEngine);
    var humanPlayer = setupEngine.State.Players.Single(player => player.Id == 1);
    var homeAiPlayer = setupEngine.State.Players.Single(player => player.Id == 2);
    var otherHomePlayer = setupEngine.State.Players.Single(player => player.Id == 3);
    var awayAiPlayer = setupEngine.State.Players.Single(player => player.Id == 4);
    var otherAwayPlayers = setupEngine.State.Players
        .Where(player => player.Team == TeamSide.Away && player.Id != awayAiPlayer.Id && player.Role == PlayerRole.Outfield)
        .ToArray();
    humanPlayer.Position = new Vector2(-15f, 0f);
    homeAiPlayer.Position = new Vector2(-8f, 0f);
    otherHomePlayer.Position = new Vector2(-16f, 0f);
    setupEngine.State.Players.Single(player => player.Id == 9).Position = new Vector2(-17f, 0f);
    awayAiPlayer.Position = new Vector2(8f, 0f);
    foreach (var player in otherAwayPlayers)
    {
        player.Position = new Vector2(16f + player.Id * 0.1f, player.Position.Y);
    }

    setupEngine.State.Ball.Position = Vector2.Zero;
    var engine = new MatchEngine(setupEngine.State, setupEngine.Tuning);
    var computerPlayers = ComputerPlayerController.CreateDefault(engine.CurrentSnapshot);
    var humanPositionBefore = humanPlayer.Position;
    var homeAiPositionBefore = homeAiPlayer.Position;
    var awayAiPositionBefore = awayAiPlayer.Position;

    engine.StepTick(new PlayerInput(Vector2.UnitY, ActionDown: false), computerPlayers);

    Assert(humanPlayer.Position.Y > humanPositionBefore.Y, "The assigned human input should be preserved.");
    Assert(
        MathF.Abs(humanPlayer.Position.X - humanPositionBefore.X) < 0.0001f,
        "AI must not overwrite the assigned human player's direction.");
    Assert(homeAiPlayer.Position.X > homeAiPositionBefore.X, "The closest home AI should pursue the loose ball.");
    Assert(awayAiPlayer.Position.X < awayAiPositionBefore.X, "The closest away AI should decide independently.");
    Assert(
        !computerPlayers.LastDecisions.ContainsKey(humanPlayer.Id),
        "An assigned human slot must not be evaluated as a computer player.");
    Assert(
        computerPlayers.LastDecisions[homeAiPlayer.Id].Kind == PlayerInstructionKind.RecoverLooseBall,
        "The live controller should expose the AI instruction selected for inspection.");
}

static void TeamScopedAiMovesTeammatesWhileOpponentsRemainStill()
{
    var setupEngine = MatchEngine.CreateDefault(opponentMovementEnabled: false);
    ClearPossession(setupEngine);
    var humanPlayer = setupEngine.State.Players.Single(player => player.Id == 1);
    var homeAiPlayer = setupEngine.State.Players.Single(player => player.Id == 2);
    var awayOutfielder = setupEngine.State.Players.Single(player => player.Id == 4);
    var awayGoalkeeper = setupEngine.State.Players.Single(player =>
        player.Team == TeamSide.Away && player.Role == PlayerRole.Goalkeeper);
    humanPlayer.Position = new Vector2(-15f, 0f);
    homeAiPlayer.Position = new Vector2(-8f, 0f);
    setupEngine.State.Players.Single(player => player.Id == 3).Position = new Vector2(-16f, 4f);
    setupEngine.State.Players.Single(player => player.Id == 9).Position = new Vector2(-16f, -4f);
    setupEngine.State.Ball.Position = Vector2.Zero;
    var engine = new MatchEngine(
        setupEngine.State,
        setupEngine.Tuning,
        opponentMovementEnabled: false);
    var computerPlayers = ComputerPlayerController.CreateForTeam(
        engine.CurrentSnapshot,
        engine.UserControlledTeam!.Value);
    var homeAiPositionBefore = homeAiPlayer.Position;
    var awayOutfielderPositionBefore = awayOutfielder.Position;
    var awayGoalkeeperPositionBefore = awayGoalkeeper.Position;

    var frame = computerPlayers.CreateControlFrame(
        engine.CurrentSnapshot,
        new Dictionary<int, PlayerInput>
        {
            [humanPlayer.Id] = new PlayerInput(Vector2.UnitY, ActionDown: false),
            [awayOutfielder.Id] = new PlayerInput(-Vector2.UnitX, ActionDown: false)
        });
    engine.StepTick(frame);

    Assert(homeAiPlayer.Position.X > homeAiPositionBefore.X, "The closest home AI should pursue the loose ball.");
    Assert(
        awayOutfielder.Position == awayOutfielderPositionBefore,
        "Disabled opponent movement must ignore even an accidentally assigned away input.");
    Assert(
        awayGoalkeeper.Position == awayGoalkeeperPositionBefore,
        "Disabling opponent movement must also freeze the away goalkeeper.");
    Assert(
        computerPlayers.LastDecisions.Keys.All(playerId =>
            engine.State.Players.Single(player => player.Id == playerId).Team == TeamSide.Home),
        "Only home-team brains should be evaluated in the temporary teammate test mode.");
}

static PlayerDecisionContext CreatePlayerDecisionContext()
{
    var engine = MatchEngine.CreateDefault();
    return new PlayerDecisionContext(engine.CurrentSnapshot, engine.State.ActivePlayerId);
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
        Position = new Vector2(15f, 8f),
        Radius = tuning.BallRadius
    };

    return new MatchEngine(
        new MatchState(FieldDimensions.Default, new[] { player }, ball, activePlayerId: player.Id),
        tuning);
}

static void MoveOtherHomeOutfieldersBehindPasser(MatchEngine engine, int intendedTargetId)
{
    var passer = engine.State.ActivePlayer;
    var index = 0;
    foreach (var teammate in engine.State.Players
                 .Where(player => player.Team == passer.Team
                     && player.Role == PlayerRole.Outfield
                     && player.Id != passer.Id
                     && player.Id != intendedTargetId)
                 .OrderBy(player => player.Id))
    {
        teammate.Position = passer.Position + new Vector2(-8f - index * 2f, index - 1f);
        index++;
    }
}

static void ScriptedMatchWithLiveAiReplaysIdentically()
{
    var first = RunScriptedMatch(ticks: 1200);
    var second = RunScriptedMatch(ticks: 1200);

    Assert(first == second, $"Same inputs should replay to the same state.\nfirst:  {first}\nsecond: {second}");
}

// ponytail: compares two runs in one process, so it catches hidden state and
// randomness but not cross-CPU float drift. Pin a golden fingerprint once CI
// logs show Linux, Windows and macOS agree.
static string RunScriptedMatch(int ticks)
{
    var engine = MatchEngine.CreateDefault();
    var computerPlayers = ComputerPlayerController.CreateDefault(engine.CurrentSnapshot);
    for (var tick = 0; tick < ticks; tick++)
    {
        // Circle the stick and alternate taps with long holds. With default
        // tuning this changes possession about 70 times in 1200 ticks; the ball
        // does not leave play, so restarts and goals are not covered yet.
        var angle = tick * 0.05f;
        var move = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
        var actionDown = tick % 90 < (tick % 180 < 90 ? 3 : 20);
        engine.StepTick(new PlayerInput(move, actionDown), computerPlayers);
    }

    var state = engine.State;
    var players = string.Join(
        ";",
        state.Players.Select(player => $"{player.Id}:{player.Position:R}/{player.Velocity:R}"));
    return $"t{state.Tick} {state.HomeScore}-{state.AwayScore} "
        + $"ball {state.Ball.Position:R}/{state.Ball.Velocity:R}/{state.Ball.Height:R} {players}";
}

static MatchEngine PutBallOut(TeamSide lastTouch, Vector2 ballPosition)
{
    var engine = MatchEngine.CreateDefault();
    ClearPossession(engine);
    engine.State.LastTouchTeam = lastTouch;
    engine.State.Ball.Position = ballPosition;
    engine.StepTick(PlayerInput.Empty);
    return engine;
}

static void KickInIsAwardedAgainstLastTouch()
{
    foreach (var lastTouch in new[] { TeamSide.Home, TeamSide.Away })
    {
        var awarded = lastTouch == TeamSide.Home ? TeamSide.Away : TeamSide.Home;
        var engine = PutBallOut(lastTouch, new Vector2(3f, FieldDimensions.Default.Top - 1f));
        var state = engine.State;
        var taker = state.Players.Single(player => player.HasPossession);

        Assert(state.IsBallOutOfPlay, "Touchline exit should stop play.");
        Assert(state.LastEvent.Restart == RestartKind.KickIn, $"Expected a kick-in, got {state.LastEvent.Restart}.");
        Assert(state.LastEvent.Team == awarded, $"Kick-in after a {lastTouch} touch should go to {awarded}.");
        Assert(taker.Team == awarded && taker.Role == PlayerRole.Outfield, "An awarded outfielder should take it.");
        Assert(state.Ball.PossessorId == taker.Id, "Ball possession should match the taker.");
        Assert(Near(taker.Position.Y, state.Field.Top) && Near(taker.Position.X, 3f), "Taker should stand where the ball left.");
        Assert(state.Ball.Position.Y > state.Field.Top, "The ball should be placed inside the court.");
        if (awarded == TeamSide.Home)
        {
            Assert(state.ActivePlayerId == taker.Id, "The user should control their own team's taker.");
        }
    }
}

static void GoalLineExitIsCornerOrGoalClearance()
{
    var field = FieldDimensions.Default;
    foreach (var defending in new[] { TeamSide.Home, TeamSide.Away })
    {
        var attacking = defending == TeamSide.Home ? TeamSide.Away : TeamSide.Home;
        var lineX = defending == TeamSide.Home ? field.Left : field.Right;
        var beyond = new Vector2(lineX + MathF.Sign(lineX), 6f);

        var corner = PutBallOut(lastTouch: defending, beyond).State;
        var cornerTaker = corner.Players.Single(player => player.HasPossession);
        Assert(corner.LastEvent.Restart == RestartKind.Corner, $"Defender's touch should give a corner, got {corner.LastEvent.Restart}.");
        Assert(corner.LastEvent.Team == attacking && cornerTaker.Team == attacking, "The corner should go to the attackers.");
        Assert(
            Near(cornerTaker.Position.X, lineX) && Near(cornerTaker.Position.Y, field.Bottom),
            "The corner should be taken from the nearer corner.");

        var clearance = PutBallOut(lastTouch: attacking, beyond).State;
        var keeper = clearance.Players.Single(player => player.HasPossession);
        Assert(clearance.LastEvent.Restart == RestartKind.GoalClearance, $"Attacker's touch should give a goal clearance, got {clearance.LastEvent.Restart}.");
        Assert(clearance.LastEvent.Team == defending, "The goal clearance should go to the defending team.");
        Assert(keeper.IsGoalkeeper && keeper.Team == defending, "The defending goalkeeper should hold the ball.");
        Assert(clearance.Ball.ControlKind == BallControlKind.GoalkeeperHands, "The goalkeeper should hold it in hands.");
    }
}

static void StoppedPlayResumesOnItsOwnWithClockHeld()
{
    var engine = PutBallOut(TeamSide.Away, new Vector2(0f, FieldDimensions.Default.Bottom + 1f));
    var state = engine.State;
    var taker = state.Players.Single(player => player.HasPossession);
    var clockAtStop = state.ClockSeconds;

    StepMany(engine, PlayerInput.Empty, engine.Tuning.RestartDelayTicks - 1);
    Assert(state.IsBallOutOfPlay, "Play should stay stopped for the whole restart delay.");
    Assert(state.ClockSeconds == clockAtStop, "The match clock should not run while play is stopped.");

    engine.StepTick(PlayerInput.Empty);
    Assert(!state.IsBallOutOfPlay, "Play should resume without a host call.");

    engine.StepTick(PlayerInput.Empty);
    Assert(!state.IsBallOutOfPlay, "A taker standing on the line should not put the ball out again.");
    Assert(taker.HasPossession, "The taker should still have the ball after the restart.");
    Assert(state.ClockSeconds > clockAtStop, "The clock should run again once play is live.");
}

static void OpponentsAreMovedBackFromKickIn()
{
    var engine = MatchEngine.CreateDefault();
    ClearPossession(engine);
    var spot = new Vector2(3f, engine.State.Field.Top);
    var crowding = engine.State.Players.Single(player => player.Id == 4);
    crowding.Position = spot + new Vector2(0.5f, 0.8f);
    engine.State.LastTouchTeam = TeamSide.Away;
    // Far enough out that the crowding opponent cannot collect it before the rules check.
    engine.State.Ball.Position = spot + new Vector2(0f, -3f);
    engine.StepTick(PlayerInput.Empty);

    var taker = engine.State.Players.Single(player => player.HasPossession);
    Assert(taker.Team == TeamSide.Home, "Home should be awarded the kick-in.");
    foreach (var opponent in engine.State.Players.Where(player => player.Team == TeamSide.Away && !player.IsGoalkeeper))
    {
        Assert(
            Vector2.Distance(opponent.Position, spot) >= engine.Tuning.RestartOpponentDistance - 0.001f,
            $"Opponent {opponent.Id} should be at least the restart distance away.");
    }

    StepMany(engine, PlayerInput.Empty, engine.Tuning.RestartDelayTicks + 1);
    Assert(taker.HasPossession, "A crowding opponent should not win the ball as play resumes.");
}

static void ButtonHeldThroughStoppageDoesNotKickOnResume()
{
    var engine = PutBallOut(TeamSide.Away, new Vector2(3f, FieldDimensions.Default.Top - 1f));
    var taker = engine.State.Players.Single(player => player.HasPossession);
    var held = new PlayerInput(Vector2.Zero, ActionDown: true);
    Assert(engine.State.ActivePlayerId == taker.Id, "The user should control the taker.");

    StepMany(engine, held, engine.Tuning.RestartDelayTicks + engine.Tuning.TapPassMaxTicks + 10);
    Assert(!engine.State.IsBallOutOfPlay, "Play should have resumed.");
    Assert(taker.HasPossession, "A button held since before the restart should not kick the ball.");

    engine.StepTick(PlayerInput.Empty);
    Assert(taker.HasPossession, "Releasing that stale hold should not pass either.");

    StepMany(engine, held, engine.Tuning.TapPassMaxTicks + 2);
    Assert(!taker.HasPossession, "A fresh press after the restart should kick normally.");
}

static string RunUnattendedMatchWithForcedOuts()
{
    var engine = MatchEngine.CreateDefault();
    var computerPlayers = ComputerPlayerController.CreateDefault(engine.CurrentSnapshot);
    var field = engine.State.Field;
    var forcedExits = new[]
    {
        new Vector2(4f, field.Top - 1f),
        new Vector2(field.Left - 1f, 6f),
        new Vector2(-4f, field.Bottom + 1f),
        new Vector2(field.Right + 1f, -6f)
    };
    var stoppages = 0;
    var consecutiveStoppedTicks = 0;
    for (var tick = 0; tick < 6000; tick++)
    {
        if (tick % 400 == 399 && !engine.State.IsBallOutOfPlay)
        {
            ClearPossession(engine);
            engine.State.Ball.Position = forcedExits[tick / 400 % forcedExits.Length];
        }

        var wasOut = engine.State.IsBallOutOfPlay;
        engine.StepTick(PlayerInput.Empty, computerPlayers);
        if (engine.State.IsBallOutOfPlay)
        {
            stoppages += wasOut ? 0 : 1;
            consecutiveStoppedTicks++;
            Assert(
                consecutiveStoppedTicks <= engine.Tuning.RestartDelayTicks,
                $"Play stayed stopped past the restart delay at tick {tick} ({engine.State.LastEvent.Description}).");
        }
        else
        {
            consecutiveStoppedTicks = 0;
        }
    }

    // 15 exits are forced. A few more can happen in open play, but a taker who keeps putting the
    // ball straight back out would stop play far more often than this.
    Assert(stoppages is >= 14 and <= 30, $"Expected roughly one stoppage per forced exit, got {stoppages}.");
    return $"{stoppages} {engine.State.Ball.Position:R} "
        + string.Join(";", engine.State.Players.Select(player => $"{player.Position:R}"));
}

static void UnattendedMatchKeepsResumingAfterForcedOuts()
{
    var first = RunUnattendedMatchWithForcedOuts();
    var second = RunUnattendedMatchWithForcedOuts();

    Assert(first == second, "Restarts should replay to the same state.");
}

static void SlidingPlayerTakesRestartOnTheirFeet()
{
    var engine = MatchEngine.CreateDefault();
    ClearPossession(engine);
    var spot = new Vector2(3f, engine.State.Field.Top);
    var slider = engine.State.Players.Single(player => player.Id == 2);
    slider.Position = spot + new Vector2(0f, 4f);
    slider.SlideTicksRemaining = 20;
    slider.SlideRecoveryTicksRemaining = 20;
    engine.State.LastTouchTeam = TeamSide.Away;
    engine.State.Ball.Position = spot + new Vector2(0f, -3f);
    engine.StepTick(PlayerInput.Empty);

    Assert(slider.HasPossession, "The nearest home outfielder should take the kick-in.");
    Assert(!slider.IsSliding && !slider.IsRecoveringFromSlide, "The taker should not resume mid-slide.");
}

static void GoalResetsEveryPlayerToKickoffPositions()
{
    var engine = MatchEngine.CreateDefault();
    ClearPossession(engine);
    foreach (var player in engine.State.Players)
    {
        player.Position += new Vector2(1.5f, -2f);
        player.SlideRecoveryTicksRemaining = 10;
    }

    engine.State.Ball.Position = new Vector2(engine.State.Field.Right + 1f, 0f);
    engine.StepTick(PlayerInput.Empty);

    var state = engine.State;
    var taker = state.Players.Single(player => player.HasPossession);
    Assert(state.HomeScore == 1 && state.LastEvent.Type == MatchEventType.GoalScored, "Home should have scored.");
    Assert(taker.Team == TeamSide.Away, "The conceding team should kick off.");
    foreach (var player in state.Players.Where(player => player.Id != taker.Id))
    {
        Assert(player.Position == player.KickoffPosition, $"Player {player.Id} should be back at their kickoff position.");
        Assert(!player.IsRecoveringFromSlide, $"Player {player.Id} should be on their feet for the kickoff.");
    }

    StepMany(engine, PlayerInput.Empty, engine.Tuning.RestartDelayTicks);
    Assert(!state.IsBallOutOfPlay, "Play should resume from the kickoff on its own.");
}

static void KickoffTakerStandsOverTheBallAndKeepsIt()
{
    var engine = MatchEngine.CreateDefault();
    ClearPossession(engine);
    engine.State.Ball.Position = new Vector2(engine.State.Field.Right + 1f, 0f);
    engine.StepTick(PlayerInput.Empty);

    var state = engine.State;
    var taker = state.Players.Single(player => player.HasPossession);
    Assert(taker.FutsalRole == FutsalOutfieldRole.Pivo, "The forward-most outfielder should take the kickoff.");
    Assert(state.Ball.Position == Vector2.Zero, "The kickoff is taken from the centre spot.");
    Assert(
        Vector2.Distance(taker.Position, state.Ball.Position) <= engine.Tuning.DribbleLeadDistance + 0.001f,
        "The taker should stand over the ball, not metres behind it.");

    StepMany(engine, PlayerInput.Empty, engine.Tuning.RestartDelayTicks + 10);
    Assert(taker.HasPossession, "The taker should still have the ball once play resumes.");
}

static void BallCarriedOverTouchlineIsOutAgainstCarrier()
{
    var engine = MatchEngine.CreateDefault();
    var state = engine.State;
    var carrier = state.ActivePlayer;
    Assert(carrier.HasPossession && carrier.Team == TeamSide.Home, "The Home active player should start with the ball.");
    foreach (var other in state.Players.Where(player => player.Id != carrier.Id && player.Role == PlayerRole.Outfield))
    {
        other.Position = new Vector2(other.Position.X, state.Field.Bottom - other.Radius);
    }

    for (var tick = 0; tick < 120 && !state.IsBallOutOfPlay; tick++)
    {
        engine.StepTick(new PlayerInput(-Vector2.UnitY, ActionDown: false));
    }

    Assert(state.LastEvent.Type == MatchEventType.TouchlineOut, "Dribbling the ball over the touchline should stop play.");
    Assert(state.LastEvent.Team == TeamSide.Away, "The kick-in goes against the player who carried it out.");
}

static void ComputerCornerTakerCarriesBallIntoCourt()
{
    var engine = MatchEngine.CreateDefault();
    var state = engine.State;
    ClearPossession(engine);
    state.LastTouchTeam = TeamSide.Home;
    state.Ball.Position = new Vector2(state.Field.Left - 1f, -8f);
    engine.StepTick(PlayerInput.Empty);
    Assert(state.LastEvent.Restart == RestartKind.Corner && state.LastEvent.Team == TeamSide.Away, "Away should have a corner.");

    engine.ResumeFromRestart();
    // Every opponent stands inside the court, so the emptiest space is beyond the lines.
    var taker = state.Players.Single(player => player.HasPossession);
    foreach (var opponent in state.Players.Where(player => player.Team == TeamSide.Home && player.Role == PlayerRole.Outfield))
    {
        opponent.Position = taker.Position + new Vector2(3.5f, 3.5f);
    }

    var recognized = new SafePossessionPattern().TryRecognize(
        new PlayerDecisionContext(MatchSnapshot.FromState(state), taker.Id),
        out var opportunity);
    Assert(recognized, "The corner taker should be weighing where to carry the ball.");
    Assert(
        opportunity.CarryDirection.X >= 0f && opportunity.CarryDirection.Y >= 0f,
        $"The corner taker should carry into the court, not {opportunity.CarryDirection}.");
}

static void GoalkeeperSmothersBallDribbledStraightAtIt()
{
    var engine = MatchEngine.CreateDefault();
    var state = engine.State;
    var carrier = state.ActivePlayer;
    foreach (var other in state.Players.Where(player => player.Role == PlayerRole.Outfield && player.Id != carrier.Id))
    {
        other.Position = new Vector2(other.Position.X, state.Field.Bottom - other.Radius);
    }

    carrier.Position = new Vector2(8f, 1f);
    state.Ball.Position = new Vector2(9f, 1f);
    var goalkeeper = state.Players.Single(player => player.IsGoalkeeper && player.Team == TeamSide.Away);

    // The button is never pressed: the carrier just runs at the goal.
    for (var tick = 0; tick < 80 && !goalkeeper.HasPossession && state.LastEvent.Type == MatchEventType.None; tick++)
    {
        engine.StepTick(new PlayerInput(Vector2.UnitX, ActionDown: false));
    }

    Assert(state.HomeScore == 0, "Walking the ball at a set goalkeeper should not score.");
    Assert(goalkeeper.HasPossession, "The goalkeeper should take a ball dribbled into it.");
}

static void AdjacentOpponentsDoNotSwapBallEveryTick()
{
    Assert(CountPossessionSwaps(PhysicsTuning.Default) <= 2, "Two players standing together should not trade the ball back and forth.");
    // The same pair with the protection switched off shows what it prevents.
    Assert(
        CountPossessionSwaps(PhysicsTuning.Default with { NewPossessionProtectionTicks = 0 }) > 10,
        "Without protection this pair should trade the ball constantly; if not, the scenario proves nothing.");

    static int CountPossessionSwaps(PhysicsTuning tuning)
    {
        // Strong tacklers with weak control: each wins the ball cleanly from the other.
        var attributes = new PlayerAttributes(8, 8, 8, Tackling: 15, BallControl: 4, 8, 8);
        PlayerState Create(int id, TeamSide team, Vector2 position, Azimuth facing) => new(id, "Player", team, position)
        {
            Facing = facing,
            Attributes = attributes,
            Radius = tuning.PlayerRadius,
            MaxSpeedPerTick = tuning.PlayerMaxSpeedPerTick,
            AccelerationPerTick = tuning.PlayerAccelerationPerTick,
            FrictionPerTick = tuning.PlayerFrictionPerTick
        };
        var home = Create(1, TeamSide.Home, Vector2.Zero, Azimuth.East);
        var away = Create(2, TeamSide.Away, new Vector2(2f, 0f), Azimuth.West);
        away.HasPossession = true;
        var ball = new BallState
        {
            Radius = tuning.BallRadius,
            Position = away.Position + away.Facing.ToUnitVector() * tuning.DribbleLeadDistance,
            PossessorId = away.Id
        };
        var engine = new MatchEngine(new MatchState(FieldDimensions.Default, new[] { home, away }, ball, activePlayerId: home.Id), tuning);

        var swaps = 0;
        var holder = engine.State.Ball.PossessorId;
        for (var tick = 0; tick < 60; tick++)
        {
            engine.StepTick(PlayerInput.Empty);
            if (engine.State.Ball.PossessorId is int current && current != holder)
            {
                swaps++;
                holder = current;
            }
        }

        return swaps;
    }
}

static void UnattendedMatchRepeatsForSeedAndDiffersBetweenSeeds()
{
    var first = MatchRunner.Play(seed: 3, halfLengthSeconds: 10f);
    Assert(first.Fault is null, $"A short unattended match should reach full time: {first.Fault}");
    Assert(first == MatchRunner.Play(seed: 3, halfLengthSeconds: 10f), "The same seed should play the same match.");
    Assert(first != MatchRunner.Play(seed: 4, halfLengthSeconds: 10f), "Another seed should play a different match.");

    // With identical squads only the computer players' decisions can differ.
    var equalFirst = MatchRunner.Play(seed: 3, halfLengthSeconds: 10f, equalSquads: true);
    Assert(equalFirst == MatchRunner.Play(seed: 3, halfLengthSeconds: 10f, equalSquads: true), "Equal squads with the same seed should play the same match.");
    Assert(equalFirst != MatchRunner.Play(seed: 4, halfLengthSeconds: 10f, equalSquads: true), "Equal squads with another seed should play a different match.");
}

static void MatchPlaysTwoHalvesAndThenStaysFinished()
{
    var tuning = PhysicsTuning.Default with { HalfLengthSeconds = 1f };
    var engine = MatchEngine.CreateDefault(tuning);
    var state = engine.State;
    var halfTicks = (int)MathF.Round(tuning.HalfLengthSeconds / tuning.TickSeconds);

    StepMany(engine, PlayerInput.Empty, halfTicks - 1);
    Assert(!state.IsBallOutOfPlay && state.Period == 1, "The first half should still be live just before time.");

    StepMany(engine, PlayerInput.Empty, 2);
    Assert(state.LastEvent.Type == MatchEventType.HalfTime, $"Expected half-time, got {state.LastEvent.Type}.");
    Assert(state.IsBallOutOfPlay && state.Period == 2 && !state.IsMatchOver, "Half-time should stop play and start period 2.");
    Assert(
        state.Players.Single(player => player.HasPossession).Team == TeamSide.Away,
        "Away should kick off the second half.");

    var guard = 0;
    while (state.IsBallOutOfPlay && guard++ < 1000)
    {
        engine.StepTick(PlayerInput.Empty);
    }

    Assert(!state.IsBallOutOfPlay, "The second half should start on its own.");

    StepMany(engine, PlayerInput.Empty, halfTicks + 2);
    Assert(state.IsMatchOver, "The match should be over after the second half.");
    Assert(state.LastEvent.Type == MatchEventType.FullTime, $"Expected full time, got {state.LastEvent.Type}.");

    var clockAtFullTime = state.ClockSeconds;
    engine.ResumeFromRestart();
    StepMany(engine, new PlayerInput(Vector2.UnitX, ActionDown: true), 600);
    Assert(state.IsMatchOver && state.IsBallOutOfPlay, "A finished match should stay finished.");
    Assert(state.ClockSeconds == clockAtFullTime, "The clock should not run after full time.");
    Assert(engine.CurrentSnapshot.IsMatchOver && engine.CurrentSnapshot.Period == 2, "The snapshot should report the result state.");
}

static void HalfTimeInterruptsLivePlayCleanly()
{
    var first = RunShortMatchWithLiveAi();
    Assert(first == RunShortMatchWithLiveAi(), "A full short match should replay identically.");
}

static string RunShortMatchWithLiveAi()
{
    // Five-second halves end in the middle of passes, dribbles and held shots.
    var engine = MatchEngine.CreateDefault(PhysicsTuning.Default with { HalfLengthSeconds = 5f });
    var computerPlayers = ComputerPlayerController.CreateDefault(engine.CurrentSnapshot);
    var state = engine.State;
    var sawHalfTime = false;
    for (var tick = 0; tick < 2000 && !state.IsMatchOver; tick++)
    {
        var angle = tick * 0.05f;
        var move = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
        engine.StepTick(new PlayerInput(move, tick % 90 < (tick % 180 < 90 ? 3 : 20)), computerPlayers);
        if (!sawHalfTime && state.LastEvent.Type == MatchEventType.HalfTime)
        {
            sawHalfTime = true;
            var taker = state.Players.Single(player => player.HasPossession);
            Assert(taker.Team == TeamSide.Away && state.Ball.PossessorId == taker.Id, "Away should hold the ball at half-time.");
            Assert(state.Ball.Position == Vector2.Zero && state.Ball.Height == 0f, "The ball should be on the centre spot.");
            Assert(
                state.Players.Where(player => player.Id != taker.Id).All(player => player.Position == player.KickoffPosition),
                "Everyone else should be back at their kickoff position.");
        }
    }

    Assert(sawHalfTime, "The match should reach half-time.");
    Assert(state.IsMatchOver && state.Period == 2, "The match should reach full time.");
    Assert(state.Players.Count(player => player.HasPossession) <= 1, "At most one player may hold the ball at full time.");
    return $"t{state.Tick} {state.HomeScore}-{state.AwayScore} {state.ClockSeconds:R} "
        + string.Join(";", state.Players.Select(player => $"{player.Id}:{player.Position:R}"));
}

static (MatchEngine Engine, ComputerPlayerController Computers, PlayerState Shooter) CreateShootingChance(
    float distanceFromGoalLine,
    Vector2? blockerOffset = null,
    float lateralOffset = 2f)
{
    var setup = MatchEngine.CreateDefault();
    var state = setup.State;
    var shooter = state.Players.Single(player => player.Id == 10);
    shooter.Position = new Vector2(state.Field.Left + distanceFromGoalLine, lateralOffset);
    foreach (var defender in state.Players.Where(player => player.Team == TeamSide.Home && player.Role == PlayerRole.Outfield))
    {
        defender.Position = new Vector2(state.Field.Right - 3f, defender.Id - 5f);
    }

    if (blockerOffset is Vector2 offset)
    {
        state.Players.Single(player => player.Id == 1).Position = shooter.Position + offset;
    }

    GivePossession(setup, shooter);

    // A new engine over the edited state starts from a fresh snapshot, which is what the brains read.
    var engine = new MatchEngine(setup.State, setup.Tuning);
    return (engine, ComputerPlayerController.CreateDefault(engine.CurrentSnapshot), shooter);
}

static void ComputerCarrierShootsFromRangeAndLeavesTheShotAlone()
{
    var (engine, computers, shooter) = CreateShootingChance(distanceFromGoalLine: 8f);
    var state = engine.State;

    engine.StepTick(PlayerInput.Empty, computers);
    Assert(
        computers.LastDecisions[shooter.Id].Kind == PlayerInstructionKind.ShootAtGoal,
        $"Expected a shot, got {computers.LastDecisions[shooter.Id].Kind}.");

    var guard = 0;
    while (shooter.HasPossession && guard++ < engine.Tuning.TapPassMaxTicks + 4)
    {
        engine.StepTick(PlayerInput.Empty, computers);
        Assert(
            !shooter.HasPossession || computers.LastDecisions[shooter.Id].Input.ActionDown,
            "The button should stay down until the shot leaves.");
    }

    Assert(state.Ball.PossessorId is null, "The held button should release a shot.");
    Assert(
        state.Ball.Velocity.Length() > engine.Tuning.PassSpeedPerTick * 1.3f && state.Ball.Velocity.X < 0f,
        "The ball should leave at shot pace toward the Home goal.");
    Assert(
        MathF.Abs(ProjectedYAtX(state.Ball, state.Field.Left)) < state.Field.GoalWidth * 0.5f,
        "The shot should be on target.");

    for (var tick = 0; tick < ShootAtGoalInstruction.FollowThroughTicks; tick++)
    {
        engine.StepTick(PlayerInput.Empty, computers);
        if (state.Ball.IsPossessed || state.IsBallOutOfPlay)
        {
            break;
        }

        var decision = computers.LastDecisions[shooter.Id];
        Assert(
            decision.Kind == PlayerInstructionKind.ShootAtGoal && decision.Input.Move == Vector2.Zero && !decision.Input.ActionDown,
            "The shooter should keep the stick neutral while the shot can still be bent.");
        Assert(state.Ball.CurveSpinZ == 0f, "A computer shot should not pick up accidental curve.");
    }
}

static void ComputerCarrierDoesNotShootFromDistanceOrThroughDefender()
{
    var (farEngine, farComputers, farShooter) = CreateShootingChance(distanceFromGoalLine: 16f);
    farEngine.StepTick(PlayerInput.Empty, farComputers);
    Assert(
        farComputers.LastDecisions[farShooter.Id].Kind != PlayerInstructionKind.ShootAtGoal,
        "A carrier outside shooting range should not shoot.");

    var (blockedEngine, blockedComputers, blockedShooter) = CreateShootingChance(
        distanceFromGoalLine: 8f,
        blockerOffset: new Vector2(-2.5f, -0.6f));
    blockedEngine.StepTick(PlayerInput.Empty, blockedComputers);
    Assert(
        blockedComputers.LastDecisions[blockedShooter.Id].Kind != PlayerInstructionKind.ShootAtGoal,
        "A carrier with a defender in the shooting lane should not shoot.");

    var (wideEngine, wideComputers, wideShooter) = CreateShootingChance(distanceFromGoalLine: 1.2f, lateralOffset: 8.5f);
    wideEngine.StepTick(PlayerInput.Empty, wideComputers);
    Assert(
        wideComputers.LastDecisions[wideShooter.Id].Kind != PlayerInstructionKind.ShootAtGoal,
        "A carrier near the corner should not shoot across the face of goal.");
}

static void ComputerPresserSlidesAtCarrierWithinReach()
{
    var (farEngine, farComputers, farPresser) = CreatePressingDuel(presserDistance: 5f);
    farEngine.StepTick(PlayerInput.Empty, farComputers);
    Assert(
        farComputers.LastDecisions[farPresser.Id].Kind == PlayerInstructionKind.PressureBallCarrier
            && !farComputers.LastDecisions[farPresser.Id].Input.ActionDown,
        "A presser out of reach should close down without sliding.");
    Assert(!farPresser.IsSliding, "A presser out of reach should stay on their feet.");

    var (engine, computers, presser) = CreatePressingDuel(presserDistance: PressureBallCarrierInstruction.TackleRange - 0.2f);
    for (var tick = 0; tick < PressureBallCarrierInstruction.JockeyTicksBeforeTackle; tick++)
    {
        engine.StepTick(PlayerInput.Empty, computers);
        Assert(!presser.IsSliding, "The presser should shadow the carrier before committing to a slide.");
    }

    engine.StepTick(PlayerInput.Empty, computers);
    Assert(
        computers.LastDecisions[presser.Id].Kind == PlayerInstructionKind.PressureBallCarrier
            && computers.LastDecisions[presser.Id].Input.ActionDown,
        "A presser who has shadowed a carrier within reach should press the button.");
    Assert(presser.IsSliding, "The press should start a slide, the same tackle a human has.");

    // The stick-and-button grammar has no other tackle, and one press must not become a string of slides.
    for (var tick = 0; tick < 60; tick++)
    {
        engine.StepTick(PlayerInput.Empty, computers);
        Assert(
            !computers.LastDecisions[presser.Id].Input.ActionDown
                || computers.LastDecisions[presser.Id].Kind != PlayerInstructionKind.PressureBallCarrier,
            "The presser should not slide again straight away.");
    }
}

static (MatchEngine Engine, ComputerPlayerController Computers, PlayerState Presser) CreatePressingDuel(float presserDistance)
{
    var setup = MatchEngine.CreateDefault();
    var state = setup.State;
    var carrier = state.Players.Single(player => player.Id == state.ActivePlayerId);
    var presser = state.Players.Single(player => player.Id == 10);
    foreach (var other in state.Players.Where(player => player.Team == TeamSide.Away && player.Role == PlayerRole.Outfield && player.Id != presser.Id))
    {
        other.Position = new Vector2(state.Field.Right - 3f, other.Id - 5f);
    }

    carrier.Position = Vector2.Zero;
    carrier.Facing = Azimuth.West;
    GivePossession(setup, carrier);
    presser.Position = new Vector2(presserDistance, 0f);
    presser.Facing = Azimuth.West;
    var engine = new MatchEngine(setup.State, setup.Tuning);
    return (engine, ComputerPlayerController.CreateDefault(engine.CurrentSnapshot), presser);
}

static void ReleasingButtonWithoutBallDoesNotStealIt()
{
    var engine = MatchEngine.CreateDefault();
    var state = engine.State;
    var holder = state.Players.Single(player => player.HasPossession);
    var thief = state.Players.Single(player => player.Id == 10);

    StepMany(engine, new PlayerInput(Vector2.Zero, ActionDown: true), 3);
    Assert(holder.HasPossession, "The holder should still be winding up.");

    thief.Position = holder.Position + new Vector2(1.5f, 0f);
    GivePossession(engine, thief);
    engine.StepTick(PlayerInput.Empty);

    Assert(thief.HasPossession && !holder.HasPossession, "Letting go of the button must not win the ball back.");
    Assert(!holder.IsSliding, "Letting go of the button must not start a slide.");
}

static void GoalkeeperGuardsFromInsideOwnArea()
{
    foreach (var ballOffset in new[] { new Vector2(7f, 6f), new Vector2(3.5f, -5.7f), new Vector2(1f, 8.9f), new Vector2(18f, 0f), new Vector2(30f, -7f) })
    {
        var engine = MatchEngine.CreateDefault();
        var state = engine.State;
        var goalkeeper = state.Players.Single(player => player.Team == TeamSide.Home && player.IsGoalkeeper);
        var carrier = state.Players.Single(player => player.Id == 10);
        carrier.Position = new Vector2(state.Field.Left + ballOffset.X, ballOffset.Y);
        GivePossession(engine, carrier);

        StepMany(engine, PlayerInput.Empty, 240);

        Assert(
            state.Field.ContainsInPenaltyArea(TeamSide.Home, goalkeeper.Position),
            $"With the ball at {ballOffset} the goalkeeper should stay where it can use its hands, not at {goalkeeper.Position}.");
        Assert(
            MathF.Abs(goalkeeper.Position.Y) <= MathF.Abs(carrier.Position.Y) + 0.01f
                && MathF.Sign(goalkeeper.Position.Y) * MathF.Sign(carrier.Position.Y) >= 0f,
            $"With the ball at {ballOffset} the goalkeeper should stand between the ball and the goal centre.");
    }
}

static void SetGoalkeeperIsBeatenFromRangeButNotFromDistance()
{
    // Unopposed standing shots at a goalkeeper who has had time to set, from a grid of positions.
    // The bounds are a balance contract: goals must be possible, and never free from long range.
    var goals = 0;
    var shots = 0;
    foreach (var distance in new[] { 5f, 7f, 9f, 11f, 14f, 18f })
    {
        foreach (var lateral in new[] { 0f, 2f, 4f, 6f })
        {
            var scored = StandingShotScores(distance, lateral);
            shots++;
            goals += scored ? 1 : 0;
            Assert(!(scored && distance >= 18f), $"A shot from {distance} m should not beat a set goalkeeper.");
            Assert(!(scored && lateral == 0f && distance >= 7f), $"A shot straight at a set goalkeeper from {distance} m should be saved.");
        }
    }

    // Measured at 8 of 24 when written.
    Assert(
        goals >= 4 && goals <= 12,
        $"Expected a set goalkeeper to concede 4 to 12 of {shots} unopposed shots, got {goals}.");
}

static bool StandingShotScores(float distanceFromGoalLine, float lateralOffset)
{
    var engine = MatchEngine.CreateDefault();
    var state = engine.State;
    foreach (var other in state.Players.Where(player => player.Role == PlayerRole.Outfield && player.Id != 10))
    {
        other.Position = new Vector2(state.Field.Right - 2f, other.Id - 5f);
    }

    var shooter = state.Players.Single(player => player.Id == 10);
    shooter.Position = new Vector2(state.Field.Left + distanceFromGoalLine, lateralOffset);
    shooter.Facing = Azimuth.West;
    GivePossession(engine, shooter);
    for (var tick = 0; tick < 90; tick++)
    {
        engine.StepTick(PlayerControlFrame.Empty);
    }

    Assert(shooter.HasPossession, "The shooter should still have the ball once the goalkeeper has set.");
    var goalkeeper = state.Players.Single(player => player.Team == TeamSide.Home && player.IsGoalkeeper);
    var aim = new Vector2(state.Field.Left, goalkeeper.Position.Y > 0f ? -1.4f : 1.4f);
    var move = Vector2.Normalize(aim - shooter.Position);
    for (var tick = 0; tick < 200; tick++)
    {
        var input = tick < engine.Tuning.TapPassMaxTicks + 3 ? new PlayerInput(move, ActionDown: true) : PlayerInput.Empty;
        engine.StepTick(PlayerControlFrame.ForPlayer(shooter.Id, input));
        if (state.AwayScore > 0)
        {
            return true;
        }

        if (state.IsBallOutOfPlay || state.Ball.PossessorId is int possessorId && possessorId != shooter.Id)
        {
            return false;
        }
    }

    return false;
}

static void HalfCourtTeamWaitsAtHalfway()
{
    var engine = MatchEngine.CreateDefault(userControlledTeam: null);
    var state = engine.State;
    var carrier = state.Players.Single(player => player.Id == 4);
    var homeOutfielders = state.Players
        .Where(player => player.Team == TeamSide.Home && player.Role == PlayerRole.Outfield)
        .Select(player => player.Id)
        .ToArray();
    var pressure = new BallCarrierPressurePattern();
    var cover = new BallCarrierCoverPattern();

    Vector2 Target<T>(IPlayerPattern<T> pattern, Func<T, Vector2> pick, TeamTactic? tactic)
        where T : struct
    {
        var snapshot = MatchSnapshot.FromState(state);
        foreach (var id in homeOutfielders)
        {
            if (pattern.TryRecognize(new PlayerDecisionContext(snapshot, id, tactic), out var match))
            {
                return pick(match);
            }
        }

        throw new InvalidOperationException("Nobody took the job.");
    }

    // Home defends the left goal, so its halfway press line is x = 0. The carrier is deep in its own half.
    carrier.Position = new Vector2(12f, 3f);
    GivePossession(engine, carrier);
    Assert(Target(pressure, match => match.Target, null).X > 9f, "By default the presser should go to the carrier.");
    Assert(
        Target(pressure, match => match.Target, TeamTactic.HalfCourt).X <= 0f,
        "Told to wait at halfway, the presser should not cross it.");
    Assert(
        Target(cover, match => match.Target, TeamTactic.HalfCourt).X < Target(pressure, match => match.Target, TeamTactic.HalfCourt).X,
        "The cover should stay behind its presser.");

    var defaultFrame = ComputerPlayerController.CreateDefault(engine.CurrentSnapshot);
    var halfCourtFrame = ComputerPlayerController.CreateDefault(engine.CurrentSnapshot);
    halfCourtFrame.SetTactic(TeamSide.Home, TeamTactic.HalfCourt);
    var nobody = new Dictionary<int, PlayerInput>();
    var snapshotDeep = MatchSnapshot.FromState(state);
    defaultFrame.CreateControlFrame(snapshotDeep, nobody);
    halfCourtFrame.CreateControlFrame(snapshotDeep, nobody);
    Assert(
        homeOutfielders.Any(id => defaultFrame.LastDecisions[id].Input != halfCourtFrame.LastDecisions[id].Input),
        "The tactic should change what the team that was given it does.");

    // Now Home has the ball deep in its own half: only a tactic given to Away may change what Away does.
    var homeCarrier = state.Players.Single(player => player.Id == 1);
    homeCarrier.Position = new Vector2(-12f, 3f);
    GivePossession(engine, homeCarrier);
    var snapshotHomeBall = MatchSnapshot.FromState(state);
    int[] awayOutfielders = [4, 5, 6, 10];
    var plain = ComputerPlayerController.CreateDefault(snapshotHomeBall);
    var homeHalfCourt = ComputerPlayerController.CreateDefault(snapshotHomeBall);
    homeHalfCourt.SetTactic(TeamSide.Home, TeamTactic.HalfCourt);
    var awayHalfCourt = ComputerPlayerController.CreateDefault(snapshotHomeBall);
    awayHalfCourt.SetTactic(TeamSide.Away, TeamTactic.HalfCourt);
    plain.CreateControlFrame(snapshotHomeBall, nobody);
    homeHalfCourt.CreateControlFrame(snapshotHomeBall, nobody);
    awayHalfCourt.CreateControlFrame(snapshotHomeBall, nobody);
    Assert(
        awayOutfielders.All(id => plain.LastDecisions[id] == homeHalfCourt.LastDecisions[id]),
        "A tactic given to one team should not change the other team.");
    Assert(
        awayOutfielders.Any(id => plain.LastDecisions[id] != awayHalfCourt.LastDecisions[id]),
        "The same tactic given to the defending team should change it.");

    // Once the carrier is inside the line, the tactic changes nothing.
    carrier.Position = new Vector2(-5f, 3f);
    GivePossession(engine, carrier);
    Assert(
        Target(pressure, match => match.Target, TeamTactic.HalfCourt) == Target(pressure, match => match.Target, null)
            && Target(cover, match => match.Target, TeamTactic.HalfCourt) == Target(cover, match => match.Target, null),
        "Inside the press line the team should press as it does by default.");

    // A loose ball is an emergency, wherever it is.
    ClearPossession(engine);
    state.Ball.Position = new Vector2(12f, 3f);
    state.Ball.Velocity = Vector2.Zero;
    var looseSnapshot = MatchSnapshot.FromState(state);
    Assert(
        homeOutfielders.Any(id => ComputerPlayerBrain.CreateDefault()
            .Decide(new PlayerDecisionContext(looseSnapshot, id, TeamTactic.HalfCourt)).Kind == PlayerInstructionKind.RecoverLooseBall),
        "A loose ball beyond the press line should still be chased.");
}

static void PushUpTeamMarksFurtherFromOwnGoal()
{
    var engine = MatchEngine.CreateDefault(userControlledTeam: null);
    var state = engine.State;
    var marking = new StructuredMarkingPattern();
    int[] homeOutfielders = [1, 2, 3, 9];
    int[] awayOutfielders = [4, 5, 6, 10];

    // Away's fixo has the ball deep in its own half and its team-mates are all in that half too.
    var carrier = state.Players.Single(player => player.Id == 4);
    carrier.Position = new Vector2(14f, 0f);
    GivePossession(engine, carrier);
    var snapshot = MatchSnapshot.FromState(state);

    Dictionary<int, float> MarkTargets(TeamTactic? tactic) => homeOutfielders
        .Where(id => marking.TryRecognize(new PlayerDecisionContext(snapshot, id, tactic), out _))
        .ToDictionary(
            id => id,
            id =>
            {
                marking.TryRecognize(new PlayerDecisionContext(snapshot, id, tactic), out var match);
                return match.Target.X;
            });

    var deep = MarkTargets(null);
    var up = MarkTargets(TeamTactic.PushUp);
    Assert(deep.Count == 2, $"Two players should mark while the other two press and cover (got {deep.Count}).");
    Assert(up.Keys.Order().SequenceEqual(deep.Keys.Order()), "Pushing up should not change who marks.");
    foreach (var (id, x) in deep)
    {
        Assert(
            MathF.Abs(up[id] - x - TeamTactic.PushUp.MarkersUpMetres) < 0.01f,
            $"Told to push up, a marker held back by its depth limit should stand that much further up ({x:0.0} m, pushed up {up[id]:0.0} m).");
    }

    // A marker whose opponent is deep in Home's half stands inside its limit: there only its anchor moves, so it
    // goes up by part of the distance.
    foreach (var attacker in state.Players.Where(player => player.Id is 5 or 6 or 10))
    {
        attacker.Position = new Vector2(-17f, attacker.Position.Y);
    }

    snapshot = MatchSnapshot.FromState(state);
    var deepInside = MarkTargets(null);
    var upInside = MarkTargets(TeamTactic.PushUp);
    Assert(deepInside.Count > 0, "Somebody should mark the attackers near Home's goal.");
    foreach (var (id, x) in deepInside)
    {
        var moved = upInside[id] - x;
        Assert(
            moved > 0.5f && moved < TeamTactic.PushUp.MarkersUpMetres - 0.5f,
            $"Inside its limit a marker told to push up should stand part of the way further up (moved {moved:0.0} m).");
    }

    // The tactic belongs to the team that was given it. Home has the ball, so Away is the team that marks.
    var homeCarrier = state.Players.Single(player => player.Id == 1);
    homeCarrier.Position = new Vector2(-14f, 0f);
    GivePossession(engine, homeCarrier);
    snapshot = MatchSnapshot.FromState(state);
    var nobody = new Dictionary<int, PlayerInput>();
    var plain = ComputerPlayerController.CreateDefault(snapshot);
    var homeUp = ComputerPlayerController.CreateDefault(snapshot);
    homeUp.SetTactic(TeamSide.Home, TeamTactic.PushUp);
    var awayUp = ComputerPlayerController.CreateDefault(snapshot);
    awayUp.SetTactic(TeamSide.Away, TeamTactic.PushUp);
    plain.CreateControlFrame(snapshot, nobody);
    homeUp.CreateControlFrame(snapshot, nobody);
    awayUp.CreateControlFrame(snapshot, nobody);
    Assert(
        awayOutfielders.All(id => plain.LastDecisions[id] == homeUp.LastDecisions[id]),
        "Home pushing up should not change what Away does.");
    Assert(
        awayOutfielders.Any(id => plain.LastDecisions[id].Input != awayUp.LastDecisions[id].Input),
        "Away pushing up should move one of Away's markers differently.");
}

static void TouchesGiveStickAndOneButton()
{
    var screen = new Vector2(1000f, 500f);
    var radius = 500f * TouchControls.StickRadiusFraction;
    var start = new Vector2(200f, 300f);
    var controls = new TouchControls();

    var input = controls.Read([new TouchPoint(1, start)], screen);
    Assert(input == PlayerInput.Empty, "A touch that has not moved should give no input.");
    Assert(controls.StickOrigin == start, "The stick should be centred where the touch started.");

    input = controls.Read([new TouchPoint(1, start + new Vector2(5f, 0f))], screen);
    Assert(input.Move == Vector2.Zero, "A drag inside the dead zone should not move the player.");

    input = controls.Read([new TouchPoint(1, start + new Vector2(0f, -30f))], screen);
    Assert(input.Move == new Vector2(0f, -1f) && !input.ActionDown, "A drag up should push the stick up only.");

    input = controls.Read([new TouchPoint(1, start + new Vector2(400f, 0f))], screen);
    Assert(input.Move == Vector2.UnitX, "A stick touch dragged across the middle should still be the stick.");
    Assert(!input.ActionDown, "A stick touch dragged onto the button side should not press the button.");
    Assert(controls.StickKnob == start + new Vector2(radius, 0f), "The knob should stop at the stick radius.");

    input = controls.Read([new TouchPoint(1, start + new Vector2(400f, 0f)), new TouchPoint(2, new Vector2(800f, 300f))], screen);
    Assert(input.Move == Vector2.UnitX && input.ActionDown, "Stick and button should work together.");

    input = controls.Read([new TouchPoint(1, start + new Vector2(400f, 0f)), new TouchPoint(2, new Vector2(100f, 300f))], screen);
    Assert(input.ActionDown, "A button touch dragged onto the stick side should still hold the button.");

    input = controls.Read([new TouchPoint(1, start + new Vector2(400f, 0f)), new TouchPoint(3, new Vector2(50f, 50f))], screen);
    Assert(input.Move == Vector2.UnitX && !input.ActionDown, "Releasing the button should keep the stick's push.");
    Assert(controls.StickOrigin == start, "A second touch on the stick side should not move the stick.");

    input = controls.Read([], screen);
    Assert(input == PlayerInput.Empty && controls.StickOrigin is null, "Lifting every touch should release everything.");

    input = controls.Read([new TouchPoint(4, new Vector2(900f, 100f))], screen);
    Assert(input.Move == Vector2.Zero && input.ActionDown, "A touch on the right half should press the button only.");
    input = controls.Read([new TouchPoint(4, new Vector2(100f, 100f))], screen);
    input = controls.Read([new TouchPoint(4, new Vector2(100f, 300f))], screen);
    Assert(input.Move == Vector2.Zero && input.ActionDown && controls.StickOrigin is null,
        "A button touch dragged onto the left half should stay the button and not become the stick.");
    controls.Reset();
    input = controls.Read([], screen);
    Assert(!input.ActionDown, "A reset should release the button.");
}

static void ShortTouchPassesAndHeldTouchShoots()
{
    var screen = new Vector2(1000f, 500f);
    TouchPoint[] button = [new TouchPoint(1, new Vector2(800f, 300f))];

    foreach (var (heldTicks, expected) in new[] { (3, GameplayFeedbackType.PassContact), (40, GameplayFeedbackType.ShotContact) })
    {
        var engine = MatchEngine.CreateDefault();
        var controls = new TouchControls();
        var buffer = new PlayerInputBuffer();
        var kinds = new List<GameplayFeedbackType>();
        for (var tick = 0; tick < heldTicks + 30; tick++)
        {
            buffer.Capture(controls.Read(tick < heldTicks ? button : [], screen));
            engine.StepTick(buffer.ConsumeForTick());
            kinds.AddRange(engine.CurrentSnapshot.FeedbackEvents.Select(feedback => feedback.Type));
        }

        var other = expected == GameplayFeedbackType.PassContact ? GameplayFeedbackType.ShotContact : GameplayFeedbackType.PassContact;
        Assert(kinds.Contains(expected) && !kinds.Contains(other), $"A touch held {heldTicks} ticks should give a {expected} only.");
    }
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
    engine.State.Ball.ControlKind = BallControlKind.None;
    engine.State.Ball.Velocity = Vector2.Zero;
}

static void GivePossession(MatchEngine engine, PlayerState player)
{
    ClearPossession(engine);
    player.HasPossession = true;
    engine.State.Ball.PossessorId = player.Id;
    engine.State.Ball.ControlKind = BallControlKind.Feet;
    engine.State.Ball.Position = player.Position
        + player.Facing.ToUnitVector() * engine.Tuning.DribbleLeadDistance;
}

static float ProjectedYAtX(BallState ball, float targetX)
{
    Assert(MathF.Abs(ball.Velocity.X) > 0.0001f, "The test shot must travel toward a goal line.");
    var ticksToTarget = (targetX - ball.Position.X) / ball.Velocity.X;
    return ball.Position.Y + ball.Velocity.Y * ticksToTarget;
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

sealed class StubPlayerInstruction(PlayerInstructionCandidate? candidate) : IPlayerInstruction
{
    public PlayerInstructionCandidate? Evaluate(PlayerDecisionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return candidate;
    }
}
