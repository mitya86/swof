using System.Numerics;
using Swof.Engine.AI;

namespace Swof.Engine;

public sealed class MatchEngine
{
    private const float DefensiveControlSwitchHysteresis = 2.25f;
    private const float DefensiveWrongSidePenalty = 0.75f;

    private readonly Dictionary<int, PlayerActionInputState> _playerActionStates = new();
    private int _ballCollectionCooldownTicks;
    private int? _releasedByPlayerId;
    private int _releasedByCollectionBlockTicks;
    private int _aftertouchTicksRemaining;
    private int? _aftertouchPlayerId;
    private int _dribbleSecureTicksRemaining;
    private int _newPossessionProtectionTicks;
    private float _dribbleRisk;
    private float _dribbleSustain01;
    private Vector2 _previousDribbleMoveDirection = Vector2.Zero;
    private float _previousDribbleSideDistance;
    private float _lastDribbleTurn01;
    private Vector2 _aftertouchBasisForward = Vector2.UnitX;
    private Vector2 _aftertouchBasisRight = Vector2.UnitY;
    private bool _aftertouchCurveApplied;
    private bool _aftertouchHeightApplied;
    private TeamSide? _shotTeamWhileActionHeld;
    private int? _shotActionPlayerId;
    private int _goalkeeperPossessionTicks;
    private int _ticksSinceBallReleased;
    private TeamSide? _lastPossessionTeam;
    private float _accumulatedSeconds;

    public MatchEngine(
        MatchState state,
        PhysicsTuning? tuning = null,
        TeamSide? userControlledTeam = TeamSide.Home,
        bool opponentMovementEnabled = true)
    {
        State = state;
        Tuning = tuning ?? PhysicsTuning.Default;
        UserControlledTeam = userControlledTeam;
        OpponentMovementEnabled = opponentMovementEnabled;
        var initialPossessor = State.Ball.PossessorId is int possessorId
            ? State.Players.FirstOrDefault(player => player.Id == possessorId)
            : null;
        _lastPossessionTeam = initialPossessor?.Team;
        State.LastTouchTeam ??= initialPossessor?.Team;
        if (initialPossessor is not null && initialPossessor.Team != UserControlledTeam)
        {
            SelectUserDefenderForOpponentPossession(initialPossessor.Position, forceSwitch: true);
        }

        CurrentSnapshot = MatchSnapshot.FromState(State);
        PreviousSnapshot = CurrentSnapshot;
    }

    public MatchState State { get; }
    public PhysicsTuning Tuning { get; private set; }
    /// <summary>The team a person plays, or null when computers play both and neither is treated differently.</summary>
    public TeamSide? UserControlledTeam { get; }
    public bool OpponentMovementEnabled { get; }
    public MatchSnapshot PreviousSnapshot { get; private set; }
    public MatchSnapshot CurrentSnapshot { get; private set; }
    public float RenderAlpha => Math.Clamp(_accumulatedSeconds / Tuning.TickSeconds, 0f, 1f);

    public static MatchEngine CreateDefault(
        PhysicsTuning? tuning = null,
        bool opponentMovementEnabled = true,
        IReadOnlyList<PlayerAttributes>? outfieldAttributes = null,
        int seed = 0,
        TeamSide? userControlledTeam = TeamSide.Home)
    {
        tuning ??= PhysicsTuning.Default;
        var field = FieldDimensions.Default;
        var poor = new PlayerAttributes(4, 4, 4, 4, 4, 4, 4);
        var moderate = new PlayerAttributes(8, 8, 8, 8, 8, 8, 8);
        var elite = new PlayerAttributes(15, 15, 15, 15, 15, 15, 15);
        // Eight outfielders in the order created below: Home fixo, left ala, right ala,
        // pivo, then Away the same. Squads are not data yet, so this is the only way in.
        var squad = outfieldAttributes ?? [poor, moderate, elite, moderate, poor, moderate, elite, moderate];
        if (squad.Count != 8)
        {
            throw new ArgumentException("Eight outfield attribute sets are required.", nameof(outfieldAttributes));
        }

        var players = new List<PlayerState>
        {
            CreatePlayer(1, "Mara", 10, TeamSide.Home, FutsalOutfieldRole.Fixo, new Vector2(-12f, 0f), tuning, Azimuth.East, squad[0]),
            CreatePlayer(2, "Rook", 6, TeamSide.Home, FutsalOutfieldRole.LeftAla, new Vector2(-7f, -5f), tuning, Azimuth.East, squad[1]),
            CreatePlayer(3, "Vale", 8, TeamSide.Home, FutsalOutfieldRole.RightAla, new Vector2(-7f, 5f), tuning, Azimuth.East, squad[2]),
            CreatePlayer(9, "Tavi", 9, TeamSide.Home, FutsalOutfieldRole.Pivo, new Vector2(-2.5f, 0f), tuning, Azimuth.East, squad[3]),
            CreatePlayer(4, "Kade", 9, TeamSide.Away, FutsalOutfieldRole.Fixo, new Vector2(12f, 0f), tuning, Azimuth.West, squad[4]),
            CreatePlayer(5, "Neri", 7, TeamSide.Away, FutsalOutfieldRole.LeftAla, new Vector2(7f, 5f), tuning, Azimuth.West, squad[5]),
            CreatePlayer(6, "Sol", 11, TeamSide.Away, FutsalOutfieldRole.RightAla, new Vector2(7f, -5f), tuning, Azimuth.West, squad[6]),
            CreatePlayer(10, "Bram", 10, TeamSide.Away, FutsalOutfieldRole.Pivo, new Vector2(2.5f, 0f), tuning, Azimuth.West, squad[7]),
            CreateGoalkeeper(
                7,
                "Ivo",
                1,
                TeamSide.Home,
                new Vector2(field.Left + tuning.GoalkeeperGoalLineOffset, 0f),
                tuning),
            CreateGoalkeeper(
                8,
                "Orin",
                1,
                TeamSide.Away,
                new Vector2(field.Right - tuning.GoalkeeperGoalLineOffset, 0f),
                tuning)
        };
        players[0].HasPossession = true;

        var ball = new BallState
        {
            Radius = tuning.BallRadius,
            Position = players[0].Position + players[0].Facing.ToUnitVector() * tuning.DribbleLeadDistance,
            PossessorId = players[0].Id,
            ControlKind = BallControlKind.Feet
        };

        return new MatchEngine(
            // With nobody playing there is no active player; 0 is no player's id.
            new MatchState(field, players, ball, activePlayerId: userControlledTeam is null ? 0 : 1) { Seed = seed },
            tuning,
            userControlledTeam,
            opponentMovementEnabled: opponentMovementEnabled);
    }

    public int Step(float deltaSeconds, PlayerInput input)
    {
        if (deltaSeconds <= 0f)
        {
            return 0;
        }

        _accumulatedSeconds += deltaSeconds;
        var ticks = 0;
        while (_accumulatedSeconds >= Tuning.TickSeconds)
        {
            AdvanceOneTick(PlayerControlFrame.ForPlayer(State.ActivePlayerId, input));
            _accumulatedSeconds -= Tuning.TickSeconds;
            ticks++;
        }

        return ticks;
    }

    public int Step(float deltaSeconds, PlayerInputBuffer inputBuffer)
    {
        ArgumentNullException.ThrowIfNull(inputBuffer);
        if (deltaSeconds <= 0f)
        {
            return 0;
        }

        _accumulatedSeconds += deltaSeconds;
        var ticks = 0;
        while (_accumulatedSeconds >= Tuning.TickSeconds)
        {
            AdvanceOneTick(PlayerControlFrame.ForPlayer(
                State.ActivePlayerId,
                inputBuffer.ConsumeForTick()));
            _accumulatedSeconds -= Tuning.TickSeconds;
            ticks++;
        }

        return ticks;
    }

    /// <summary>
    /// Advances the fixed-tick simulation while autonomous brains fill every outfield slot not assigned to the active user.
    /// </summary>
    /// <param name="deltaSeconds">Elapsed presentation time to add to the fixed-tick accumulator.</param>
    /// <param name="inputBuffer">The active user's buffered input.</param>
    /// <param name="computerPlayers">The controller that supplies unassigned outfield inputs.</param>
    /// <returns>The number of fixed simulation ticks processed.</returns>
    public int Step(
        float deltaSeconds,
        PlayerInputBuffer inputBuffer,
        ComputerPlayerController computerPlayers)
    {
        ArgumentNullException.ThrowIfNull(inputBuffer);
        ArgumentNullException.ThrowIfNull(computerPlayers);
        if (deltaSeconds <= 0f)
        {
            return 0;
        }

        _accumulatedSeconds += deltaSeconds;
        var ticks = 0;
        while (_accumulatedSeconds >= Tuning.TickSeconds)
        {
            var assignedInputs = new Dictionary<int, PlayerInput>
            {
                [State.ActivePlayerId] = inputBuffer.ConsumeForTick()
            };
            var frame = computerPlayers.CreateControlFrame(CurrentSnapshot, assignedInputs);
            AdvanceOneTick(frame);
            _accumulatedSeconds -= Tuning.TickSeconds;
            ticks++;
        }

        return ticks;
    }

    public void StepTick(PlayerInput input)
    {
        AdvanceOneTick(PlayerControlFrame.ForPlayer(State.ActivePlayerId, input));
        _accumulatedSeconds = 0f;
    }

    /// <summary>
    /// Advances exactly one authoritative tick using simultaneous per-player inputs.
    /// </summary>
    /// <param name="frame">The resolved control frame for this tick.</param>
    public void StepTick(PlayerControlFrame frame)
    {
        ArgumentNullException.ThrowIfNull(frame);
        AdvanceOneTick(frame);
        _accumulatedSeconds = 0f;
    }

    /// <summary>
    /// Advances exactly one authoritative tick while autonomous brains fill slots not assigned to the active user.
    /// </summary>
    /// <param name="input">The active user's input.</param>
    /// <param name="computerPlayers">The controller that supplies unassigned outfield inputs.</param>
    public void StepTick(PlayerInput input, ComputerPlayerController computerPlayers)
    {
        ArgumentNullException.ThrowIfNull(computerPlayers);
        var assignedInputs = new Dictionary<int, PlayerInput>
        {
            [State.ActivePlayerId] = input
        };
        StepTick(computerPlayers.CreateControlFrame(CurrentSnapshot, assignedInputs));
    }

    public MatchSnapshot GetRenderSnapshot()
    {
        return MatchSnapshot.Interpolate(PreviousSnapshot, CurrentSnapshot, RenderAlpha);
    }

    public void ApplyTuning(PhysicsTuning tuning)
    {
        Tuning = tuning;
        State.Ball.Radius = tuning.BallRadius;

        foreach (var player in State.Players)
        {
            var speed01 = player.Attributes.Speed01;
            player.Radius = tuning.PlayerRadius;
            player.MaxSpeedPerTick = player.IsGoalkeeper
                ? tuning.GoalkeeperMaxSpeedPerTick
                : tuning.PlayerMaxSpeedPerTick * Lerp(0.82f, 1.1f, speed01);
            player.AccelerationPerTick = player.IsGoalkeeper
                ? tuning.GoalkeeperAccelerationPerTick
                : tuning.PlayerAccelerationPerTick;
            player.FrictionPerTick = tuning.PlayerFrictionPerTick;
        }
    }

    private void AdvanceOneTick(PlayerControlFrame frame)
    {
        ValidateControlFrame(frame);
        PreviousSnapshot = CurrentSnapshot;
        SimulateTick(frame);
        CurrentSnapshot = MatchSnapshot.FromState(State);
    }

    private void SimulateTick(PlayerControlFrame frame)
    {
        State.Tick++;

        if (State.IsBallOutOfPlay)
        {
            if (State.IsMatchOver)
            {
                return;
            }

            // Futsal uses a stopped clock, so match time does not advance here.
            HoldActionsDuringStoppage(frame);
            State.RestartTicksRemaining--;
            if (State.RestartTicksRemaining <= 0)
            {
                ResumeFromRestart();
            }

            return;
        }

        State.ClockSeconds += Tuning.TickSeconds;
        if (State.ClockSeconds >= Tuning.HalfLengthSeconds * State.Period)
        {
            EndPeriod();
            return;
        }

        State.LastEvent = MatchEvent.None;
        foreach (var player in State.Players)
        {
            player.KickAnimation01 = Math.Max(
                0f,
                player.KickAnimation01 - 1f / Math.Max(1, Tuning.KickAnimationTicks));
        }

        _ballCollectionCooldownTicks = Math.Max(0, _ballCollectionCooldownTicks - 1);
        _newPossessionProtectionTicks = Math.Max(0, _newPossessionProtectionTicks - 1);
        _releasedByCollectionBlockTicks = Math.Max(0, _releasedByCollectionBlockTicks - 1);
        if (_releasedByCollectionBlockTicks == 0)
        {
            _releasedByPlayerId = null;
        }

        foreach (var playerId in frame.PlayerIds)
        {
            var controlledPlayer = State.Players.Single(player => player.Id == playerId);
            MoveControlledPlayer(controlledPlayer, frame.GetInputOrEmpty(playerId).NormalizedMove);
            ResolvePlayerBounds(controlledPlayer);
        }

        MoveGoalkeepers();

        var releasedBallThisTick = ProcessPlayerActions(frame);

        if (!releasedBallThisTick && TryAutoDistributeGoalkeeper())
        {
            releasedBallThisTick = true;
        }

        var possessor = State.Players.FirstOrDefault(player => player.HasPossession);
        if (possessor is not null)
        {
            ControlBall(possessor);
            if (possessor.HasPossession && !possessor.IsGoalkeeper)
            {
                TryGoalkeeperCatch(State.Ball.Position);
            }
        }
        else if (!releasedBallThisTick)
        {
            var aftertouchInput = _aftertouchPlayerId is int aftertouchPlayerId
                ? frame.GetInputOrEmpty(aftertouchPlayerId).NormalizedMove
                : Vector2.Zero;
            ApplyAftertouchInput(aftertouchInput);
            var previousBallPosition = State.Ball.Position;
            MoveLooseBall();
            if (!TryGoalkeeperCatch(previousBallPosition))
            {
                TryCollectBall();
            }
        }

        foreach (var slidingPlayer in State.Players
                     .Where(player => player.IsSliding)
                     .OrderBy(player => player.Id))
        {
            HandleSlidingBallInteraction(slidingPlayer);
        }

        CheckBallRules();
    }

    private void ValidateControlFrame(PlayerControlFrame frame)
    {
        foreach (var playerId in frame.PlayerIds)
        {
            if (!State.Players.Any(player => player.Id == playerId))
            {
                throw new ArgumentException(
                    $"Control frame references player {playerId}, who is not in this match.",
                    nameof(frame));
            }
        }
    }

    private bool ProcessPlayerActions(PlayerControlFrame frame)
    {
        var playerIds = frame.PlayerIds
            .Concat(_playerActionStates.Keys)
            .Distinct()
            .OrderBy(playerId => playerId)
            .ToArray();
        var releasedBallThisTick = false;

        foreach (var playerId in playerIds)
        {
            var player = State.Players.FirstOrDefault(candidate => candidate.Id == playerId);
            if (player is null)
            {
                _playerActionStates.Remove(playerId);
                continue;
            }

            var input = frame.GetInputOrEmpty(playerId);
            if (!_playerActionStates.TryGetValue(playerId, out var actionState))
            {
                actionState = new PlayerActionInputState();
                _playerActionStates.Add(playerId, actionState);
            }

            releasedBallThisTick |= ProcessPlayerAction(player, input, actionState);
        }

        return releasedBallThisTick;
    }

    private bool ProcessPlayerAction(
        PlayerState player,
        PlayerInput input,
        PlayerActionInputState actionState)
    {
        var actionPressed = !actionState.PreviousActionDown && input.ActionDown;
        var actionReleased = actionState.PreviousActionDown && !input.ActionDown;
        if (actionPressed)
        {
            actionState.ActionCommittedThisHold = false;
            if (player.HasPossession)
            {
                State.AddFeedback(
                    GameplayFeedbackType.ActionAccepted,
                    player,
                    player.Position,
                    player.Facing.ToUnitVector(),
                    0.32f);
            }
        }

        if (actionPressed
            && player.Role == PlayerRole.Outfield
            && !player.HasPossession
            && !player.IsSliding
            && !player.IsRecoveringFromSlide)
        {
            StartSlidingTackle(player);
            actionState.ActionCommittedThisHold = true;
        }

        var releasedBallThisTick = false;
        if (input.ActionDown)
        {
            actionState.ActionHeldTicks = Math.Min(
                actionState.ActionHeldTicks + 1,
                Tuning.MaxKickHoldTicks);
            player.ActionCharge01 = !actionState.ActionCommittedThisHold && player.HasPossession
                ? Math.Clamp((float)actionState.ActionHeldTicks / (Tuning.TapPassMaxTicks + 1), 0f, 1f)
                : 0f;

            if (!actionState.ActionCommittedThisHold
                && player.HasPossession
                && actionState.ActionHeldTicks > Tuning.TapPassMaxTicks)
            {
                KickBall(player);
                actionState.ActionCommittedThisHold = true;
                _shotTeamWhileActionHeld = player.Team;
                _shotActionPlayerId = player.Id;
                releasedBallThisTick = true;
            }
        }

        if (actionReleased)
        {
            // Releasing without the ball does nothing: the only tackle is the slide started on the press.
            if (!actionState.ActionCommittedThisHold && player.HasPossession)
            {
                UseAction(player, actionState.ActionHeldTicks);
                releasedBallThisTick = true;
            }

            actionState.ActionCommittedThisHold = false;
            actionState.ActionHeldTicks = 0;
            player.ActionCharge01 = 0f;
            if (_shotActionPlayerId == player.Id)
            {
                _shotTeamWhileActionHeld = null;
                _shotActionPlayerId = null;
            }
        }

        actionState.PreviousActionDown = input.ActionDown;
        return releasedBallThisTick;
    }

    // ponytail: teams do not change ends at half-time and there is no extra time or
    // penalty shoot-out; add them with competition modes.
    private void EndPeriod()
    {
        if (State.Period >= 2)
        {
            StopForRestart(new MatchEvent(
                MatchEventType.FullTime,
                RestartKind.None,
                Team: null,
                Position: State.Ball.Position,
                Description: $"Full time: Home {State.HomeScore} - {State.AwayScore} Away."));
            State.IsMatchOver = true;
            return;
        }

        State.Period++;
        StopForRestart(new MatchEvent(
            MatchEventType.HalfTime,
            RestartKind.Kickoff,
            Team: null,
            Position: Vector2.Zero,
            Description: "Half-time."));
        // Home takes the opening kickoff, so Away starts the second half.
        ResetForKickoff(TeamSide.Away);
        // ponytail: the interval is three restart pauses; give it its own tuning field once there is a half-time screen.
        State.RestartTicksRemaining = Tuning.RestartDelayTicks * 3;
    }

    public void ResumeFromRestart()
    {
        if (!State.IsBallOutOfPlay || State.IsMatchOver)
        {
            return;
        }

        State.IsBallOutOfPlay = false;
        State.RestartTicksRemaining = 0;
        State.LastEvent = MatchEvent.None;
    }

    /// <summary>
    /// Keeps a button that is held through a stoppage from reading as a fresh press when play resumes,
    /// which would fire an unintended pass, shot or slide on the first live tick.
    /// </summary>
    private void HoldActionsDuringStoppage(PlayerControlFrame frame)
    {
        foreach (var playerId in frame.PlayerIds)
        {
            if (!_playerActionStates.TryGetValue(playerId, out var actionState))
            {
                actionState = new PlayerActionInputState();
                _playerActionStates.Add(playerId, actionState);
            }

            var actionDown = frame.GetInputOrEmpty(playerId).ActionDown;
            actionState.PreviousActionDown = actionDown;
            actionState.ActionCommittedThisHold = actionDown;
            actionState.ActionHeldTicks = 0;
        }
    }

    private void MoveControlledPlayer(PlayerState player, Vector2 move)
    {
        if (!OpponentMovementEnabled && player.Team != UserControlledTeam)
        {
            player.Velocity = Vector2.Zero;
            return;
        }

        if (player.IsGoalkeeper)
        {
            // In the classic Sensible control model the goalkeeper is not an extra
            // freely selected runner. Directional input only aims a held-ball release.
            if (IsHoldingBallInHands(player) && move.LengthSquared() > 0.0001f)
            {
                player.Facing = Azimuth.FromVector(move).Quantized(8);
            }

            player.Velocity = Vector2.Zero;
            return;
        }

        if (player.IsSliding)
        {
            player.Position += player.Velocity;
            player.SlideTicksRemaining--;
            if (player.SlideTicksRemaining <= 0)
            {
                player.SlideTicksRemaining = 0;
                player.SlideRecoveryTicksRemaining = Tuning.SlideRecoveryTicks;
                player.Velocity = Vector2.Zero;
            }

            return;
        }

        if (player.IsRecoveringFromSlide)
        {
            player.SlideRecoveryTicksRemaining--;
            player.Velocity = Vector2.Zero;
            return;
        }

        if (move.LengthSquared() <= 0.0001f)
        {
            player.Velocity = Approach(player.Velocity, Vector2.Zero, Tuning.PlayerDecelerationPerTick);
        }
        else
        {
            player.Facing = Azimuth.FromVector(move).Quantized(8);
            var desiredDirection = player.Facing.ToUnitVector();
            var desiredVelocity = desiredDirection * player.MaxSpeedPerTick;
            var response = ComputePlayerMovementResponse(player, desiredDirection);
            player.Velocity = Approach(player.Velocity, desiredVelocity, response);
        }

        player.Position += player.Velocity;
    }

    private void MoveGoalkeepers()
    {
        var field = State.Field;
        _ticksSinceBallReleased = State.Ball.IsPossessed ? int.MaxValue / 2 : _ticksSinceBallReleased + 1;
        foreach (var goalkeeper in State.Players.Where(player => player.IsGoalkeeper))
        {
            if (!OpponentMovementEnabled && goalkeeper.Team != UserControlledTeam)
            {
                goalkeeper.Velocity = Vector2.Zero;
                continue;
            }

            if (goalkeeper.HasPossession)
            {
                goalkeeper.Velocity = Vector2.Zero;
                continue;
            }

            var attributes = goalkeeper.GoalkeeperAttributes ?? GoalkeeperAttributes.Default;
            var target = GoalkeeperGuardPosition(goalkeeper, attributes, out var isSavingShot);
            var response = Tuning.GoalkeeperAccelerationPerTick * Lerp(0.8f, 1.2f, attributes.Reflexes01);

            // A shot is read late: the goalkeeper holds its ground for a reaction time before stepping across.
            var reactionTicks = Tuning.GoalkeeperReactionTicks * Lerp(1.25f, 0.75f, attributes.Reflexes01);
            var desiredVelocity = isSavingShot && _ticksSinceBallReleased < reactionTicks
                ? Vector2.Zero
                : SafeNormalize(target - goalkeeper.Position, goalkeeper.Facing.ToUnitVector())
                    * Tuning.GoalkeeperMaxSpeedPerTick;
            goalkeeper.Velocity = Approach(goalkeeper.Velocity, desiredVelocity, response);

            if (Vector2.DistanceSquared(goalkeeper.Position, target) <= goalkeeper.Velocity.LengthSquared())
            {
                goalkeeper.Position = target;
                goalkeeper.Velocity = Vector2.Zero;
            }
            else
            {
                goalkeeper.Position += goalkeeper.Velocity;
            }

            var toBall = State.Ball.Position - goalkeeper.Position;
            if (toBall.LengthSquared() > 0.0001f)
            {
                goalkeeper.Facing = Azimuth.FromVector(toBall).Quantized(8);
            }

            ResolveGoalkeeperZone(goalkeeper);
        }
    }

    /// <summary>
    /// Places the goalkeeper on the line from the goal centre to the ball: out toward the edge of its
    /// reach while the ball is far away, back near its line as the ball comes within shooting range.
    /// </summary>
    private Vector2 GoalkeeperGuardPosition(
        PlayerState goalkeeper,
        GoalkeeperAttributes attributes,
        out bool isSavingShot)
    {
        isSavingShot = false;
        var field = State.Field;
        var isHome = goalkeeper.Team == TeamSide.Home;
        var goalCentre = new Vector2(isHome ? field.Left : field.Right, 0f);
        var outward = isHome ? Vector2.UnitX : -Vector2.UnitX;
        // A dead ball is claimed only where the goalkeeper is allowed to stand.
        var ballFromGoal = State.Ball.Position - goalCentre;
        var ballDepth = Vector2.Dot(ballFromGoal, outward);
        if (!State.Ball.IsPossessed
            && State.Ball.Velocity.Length() < Tuning.GoalkeeperClaimBallSpeedPerTick
            && ballDepth >= 0f
            && ballDepth <= Tuning.GoalkeeperMaxAdvanceDistance
            && MathF.Abs(ballFromGoal.Y) <= field.GoalWidth * 0.5f + Tuning.GoalkeeperLateralPadding)
        {
            return State.Ball.Position;
        }

        // A loose ball travelling at this goal is a shot to save: step across to the nearest point of its path.
        var ballSpeedTowardGoal = -Vector2.Dot(State.Ball.Velocity, outward);
        if (!State.Ball.IsPossessed && ballSpeedTowardGoal > Tuning.GoalkeeperClaimBallSpeedPerTick)
        {
            var ticksToGoalLine = MathF.Abs(State.Ball.Position.X - goalCentre.X) / ballSpeedTowardGoal;
            var crossing = State.Ball.Position + State.Ball.Velocity * ticksToGoalLine;
            if (MathF.Abs(crossing.Y) <= field.GoalWidth * 0.5f + Tuning.GoalkeeperLateralPadding)
            {
                isSavingShot = true;
                return ClosestPointOnSegment(goalkeeper.Position, State.Ball.Position, crossing);
            }
        }

        var toBall = State.Ball.Position - goalCentre;
        var ballDistance = toBall.Length();
        var advance01 = Math.Clamp(
            (ballDistance - Tuning.GoalkeeperRetreatBallDistance)
                / MathF.Max(0.01f, Tuning.GoalkeeperAdvanceBallDistance - Tuning.GoalkeeperRetreatBallDistance),
            0f,
            1f);
        var advance = Lerp(
            Tuning.GoalkeeperGoalLineOffset,
            Tuning.GoalkeeperMaxAdvanceDistance,
            advance01 * (0.75f + attributes.Positioning01 * 0.25f));
        var offset = SafeNormalize(toBall, outward) * advance;

        // Even with the ball on the byline the goalkeeper stays a step off its line, at the near post.
        var depth = MathF.Max(Tuning.GoalkeeperGoalLineOffset * 0.5f, Vector2.Dot(offset, outward));
        return new Vector2(
            goalCentre.X + outward.X * depth,
            Math.Clamp(
                offset.Y,
                field.GoalTop - Tuning.GoalkeeperLateralPadding,
                field.GoalBottom + Tuning.GoalkeeperLateralPadding));
    }

    private bool TryAutoDistributeGoalkeeper()
    {
        var goalkeeper = State.Players.FirstOrDefault(IsHoldingBallInHands);
        if (goalkeeper is null)
        {
            _goalkeeperPossessionTicks = 0;
            return false;
        }

        _goalkeeperPossessionTicks++;
        var distributionTicks = goalkeeper.Team == UserControlledTeam
            ? Tuning.GoalkeeperUserDistributionTimeoutTicks
            : Tuning.GoalkeeperAiDistributionDelayTicks;
        if (_goalkeeperPossessionTicks < distributionTicks)
        {
            return false;
        }

        var target = FindGoalkeeperDistributionTarget(goalkeeper);
        if (target is not null)
        {
            goalkeeper.Facing = Azimuth.FromVector(target.Position - goalkeeper.Position).Quantized(8);
            PassBall(goalkeeper);
        }
        else
        {
            KickBall(goalkeeper);
        }

        return true;
    }

    private PlayerState? FindGoalkeeperDistributionTarget(PlayerState goalkeeper)
    {
        return State.Players
            .Where(player => player.Team == goalkeeper.Team && player.Role == PlayerRole.Outfield)
            .OrderBy(player => Vector2.DistanceSquared(player.Position, goalkeeper.Position))
            .ThenBy(player => player.Id)
            .FirstOrDefault();
    }

    private float ComputePlayerMovementResponse(PlayerState player, Vector2 desiredDirection)
    {
        var speed = player.Velocity.Length();
        var speed01 = Math.Clamp(speed / MathF.Max(0.0001f, player.MaxSpeedPerTick), 0f, 1f);
        var forwardAcceleration = Lerp(
            Tuning.PlayerBurstAccelerationPerTick,
            Tuning.PlayerSustainAccelerationPerTick,
            speed01);
        if (speed <= 0.0001f)
        {
            return Math.Max(0f, forwardAcceleration);
        }

        var currentDirection = player.Velocity / speed;
        var alignment = Math.Clamp(Vector2.Dot(currentDirection, desiredDirection), -1f, 1f);
        if (alignment >= 0f)
        {
            // Digital football controls need intent to read quickly. Momentum still rounds the path,
            // but lateral steering cannot inherit the sluggish top-speed acceleration rate.
            var turn01 = MathF.Pow(1f - alignment, 0.75f);
            return Math.Max(0f, Lerp(forwardAcceleration, Tuning.PlayerTurnAccelerationPerTick, turn01));
        }

        return Math.Max(
            0f,
            Lerp(Tuning.PlayerTurnAccelerationPerTick, Tuning.PlayerReverseAccelerationPerTick, -alignment));
    }

    private void ControlBall(PlayerState player)
    {
        if (IsHoldingBallInHands(player))
        {
            HoldBallInGoalkeeperHands(player);
            return;
        }

        MoveControlledBall(player);
        AlignControlledBallToFacing(player);
        SettleControlledBallWhenStationary(player);
        EvaluateDribbleControlRisk(player);
        if (!player.HasPossession)
        {
            return;
        }

        if (!IsBallInControlEnvelope(player))
        {
            LoseBall(player);
            return;
        }

        TouchBallOnStride(player);
        State.Ball.Height = 0f;
        State.Ball.VerticalVelocity = 0f;
        State.Ball.PossessorId = player.Id;
        State.Ball.ControlKind = BallControlKind.Feet;

        TryPassiveRepossession(player);
    }

    private void HoldBallInGoalkeeperHands(PlayerState goalkeeper)
    {
        goalkeeper.Velocity = Vector2.Zero;
        State.Ball.Position = goalkeeper.Position
            + goalkeeper.Facing.ToUnitVector() * Tuning.GoalkeeperHeldBallForwardOffset;
        State.Ball.Velocity = Vector2.Zero;
        State.Ball.Height = Tuning.GoalkeeperHeldBallHeight;
        State.Ball.VerticalVelocity = 0f;
        State.Ball.CurveSpinZ = 0f;
        State.Ball.PossessorId = goalkeeper.Id;
        State.Ball.ControlKind = BallControlKind.GoalkeeperHands;
    }

    private void AlignControlledBallToFacing(PlayerState player)
    {
        var speed = player.Velocity.Length();
        if (speed <= Tuning.StationaryControlSpeedThresholdPerTick)
        {
            return;
        }

        var facing = player.Facing.ToUnitVector();
        var right = new Vector2(-facing.Y, facing.X);
        var offset = State.Ball.Position - player.Position;
        var forward = Vector2.Dot(offset, facing);
        var side = Vector2.Dot(offset, right);

        if (forward < Tuning.MovingFrontAlignMinForwardDistance)
        {
            var needed = Tuning.MovingFrontAlignMinForwardDistance - forward;
            State.Ball.Position += facing * Math.Min(needed, Tuning.MovingFrontAlignPositionPerTick);
        }

        var lateralStep = Tuning.MovingFrontAlignPositionPerTick;
        State.Ball.Position -= right * Math.Clamp(side, -lateralStep, lateralStep);

        var velocity = State.Ball.Velocity;
        var velocityForward = Vector2.Dot(velocity, facing);
        var velocitySide = Vector2.Dot(velocity, right);
        velocitySide = ApproachScalar(velocitySide, 0f, Tuning.MovingFrontAlignVelocityPerTick);
        State.Ball.Velocity = facing * Math.Max(0f, velocityForward) + right * velocitySide;
    }

    private void SettleControlledBallWhenStationary(PlayerState player)
    {
        var speed = player.Velocity.Length();
        if (speed > Tuning.StationaryControlSpeedThresholdPerTick)
        {
            return;
        }

        var control01 = player.Attributes.BallControl01;
        var restingLead = Tuning.DribbleLeadDistance
            + Lerp(Tuning.StationaryLowControlLeadExtra, Tuning.StationaryHighControlLeadExtra, control01);
        var targetPosition = player.Position + player.Facing.ToUnitVector() * restingLead;
        State.Ball.Position = Approach(State.Ball.Position, targetPosition, Tuning.StationaryBallSettlePositionPerTick);
        State.Ball.Velocity = Approach(State.Ball.Velocity, Vector2.Zero, Tuning.StationaryBallSettleVelocityPerTick);

        var maxDistance = (player.Radius + State.Ball.Radius + Tuning.PossessionDistance) * Tuning.StationaryPossessionClampFactor;
        var offset = State.Ball.Position - player.Position;
        var distance = offset.Length();
        if (distance > maxDistance)
        {
            var direction = SafeNormalize(offset, player.Facing.ToUnitVector());
            State.Ball.Position = player.Position + direction * maxDistance;
            State.Ball.Velocity = Vector2.Zero;
        }
    }

    private void MoveControlledBall(PlayerState player)
    {
        State.Ball.Position += State.Ball.Velocity;
        State.Ball.Velocity = Approach(State.Ball.Velocity, player.Velocity, Tuning.DribbleControlFrictionPerTick);

        if (player.Velocity.LengthSquared() < 0.0001f)
        {
            player.DribbleStepDistance = 0f;
        }
    }

    private void TouchBallOnStride(PlayerState player)
    {
        var playerSpeed = player.Velocity.Length();
        if (playerSpeed <= 0.0001f)
        {
            return;
        }

        player.DribbleStepDistance += playerSpeed;
        var control01 = player.Attributes.BallControl01;
        var stepDistance = Lerp(Tuning.DribbleMinStepDistance, Tuning.DribbleMaxStepDistance, control01);
        if (player.DribbleStepDistance < stepDistance)
        {
            return;
        }

        player.DribbleStepDistance -= stepDistance;
        var facing = player.Facing.ToUnitVector();
        var speed01 = Math.Clamp(playerSpeed / player.MaxSpeedPerTick, 0f, 1f);
        var turnPenalty = ComputeDribbleTurnPenalty(player, facing, speed01, control01);
        var impulse = Tuning.DribbleTouchImpulsePerTick * (1f + speed01 + turnPenalty);
        var playerSpeedAlongFacing = Math.Max(0f, Vector2.Dot(player.Velocity, facing));
        var ballLead = Vector2.Dot(State.Ball.Position - player.Position, facing);
        var comfortableLead = Lerp(Tuning.DribbleMaxLeadDistance, Tuning.DribbleMinLeadDistance, control01)
            + speed01 * Lerp(Tuning.DribbleLowControlSpeedLead, Tuning.DribbleHighControlSpeedLead, control01);
        var nextSpeed = ballLead > comfortableLead
            ? playerSpeedAlongFacing * 0.92f
            : playerSpeedAlongFacing + impulse;

        State.Ball.Velocity = facing * nextSpeed;
        State.Ball.Velocity = ClampLength(State.Ball.Velocity, player.MaxSpeedPerTick * 1.18f);
        _dribbleSecureTicksRemaining = Tuning.DribbleSecureTicksAfterTouch;
        _dribbleRisk *= 0.92f;
    }

    private float ComputeDribbleTurnPenalty(PlayerState player, Vector2 facing, float speed01, float control01)
    {
        var movementDirection = SafeNormalize(player.Velocity, facing);
        var alignment = Math.Clamp(Vector2.Dot(movementDirection, facing), -1f, 1f);
        var turn01 = (1f - alignment) * 0.5f;
        return turn01 * speed01 * Lerp(Tuning.DribbleLowControlTurnPenalty, Tuning.DribbleHighControlTurnPenalty, control01);
    }

    private bool IsBallInControlEnvelope(PlayerState player)
    {
        var offset = State.Ball.Position - player.Position;
        var distance = offset.Length();
        var control01 = player.Attributes.BallControl01;
        var maxDistance = player.Radius
            + State.Ball.Radius
            + Lerp(Tuning.DribbleLowControlMaxDistance, Tuning.DribbleHighControlMaxDistance, control01);
        if (distance > maxDistance)
        {
            return false;
        }

        if (distance <= 0.0001f)
        {
            return true;
        }

        var closeControlDistance = player.Radius + State.Ball.Radius + Lerp(0.05f, 0.45f, control01);
        if (distance <= closeControlDistance)
        {
            return true;
        }

        var behindAlignment = Vector2.Dot(offset / distance, player.Facing.ToUnitVector());
        var minBehindAlignment = Lerp(-0.1f, -0.9f, control01);
        return behindAlignment > minBehindAlignment;
    }

    private void LoseBall(PlayerState player)
    {
        var control01 = player.Attributes.BallControl01;
        var facing = player.Facing.ToUnitVector();
        var right = new Vector2(-facing.Y, facing.X);
        var side = Vector2.Dot(State.Ball.Position - player.Position, right);
        var sideSign = side >= 0f ? 1f : -1f;
        var looseSpeed = Tuning.DribbleMistouchLooseSpeedPerTick * Lerp(1.12f, 0.68f, control01);
        var turnSideInfluence = Math.Clamp(_lastDribbleTurn01 * 0.18f, 0f, 0.18f);
        var forwardCarry = Math.Max(0f, Vector2.Dot(player.Velocity, facing));
        var forwardReleaseSpeed = Math.Max(forwardCarry * 0.9f, looseSpeed * 0.95f);
        var wobbleSpeed = looseSpeed * turnSideInfluence;

        player.HasPossession = false;
        player.DribbleStepDistance = 0f;
        State.Ball.PossessorId = null;
        State.Ball.ControlKind = BallControlKind.None;
        State.Ball.Velocity = facing * forwardReleaseSpeed + right * (sideSign * wobbleSpeed);
        _ballCollectionCooldownTicks = Math.Max(_ballCollectionCooldownTicks, Tuning.DribbleMistouchCollectionCooldownTicks);
        _releasedByPlayerId = player.Id;
        _releasedByCollectionBlockTicks = Math.Max(_releasedByCollectionBlockTicks, Tuning.DribbleMistouchCollectionCooldownTicks);
        _aftertouchTicksRemaining = 0;
        _aftertouchPlayerId = null;
        _dribbleSecureTicksRemaining = 0;
        _dribbleRisk = 0f;
        _dribbleSustain01 = 0f;
        _previousDribbleMoveDirection = Vector2.Zero;
        _previousDribbleSideDistance = 0f;
        _lastDribbleTurn01 = 0f;
    }

    private void TryPassiveRepossession(PlayerState possessor)
    {
        if (_newPossessionProtectionTicks > 0)
        {
            return;
        }

        var challenger = State.Players
            .Where(player => player.Team != possessor.Team && player.Role == PlayerRole.Outfield)
            // A player on the ground wins the ball with the slide itself, not by lying beside it.
            .Where(player => !player.IsSliding && !player.IsRecoveringFromSlide)
            .Select(player => new
            {
                Player = player,
                Distance = Vector2.Distance(player.Position, State.Ball.Position)
            })
            .Where(candidate => candidate.Distance <= playerBallContestDistance(candidate.Player))
            .OrderBy(candidate => candidate.Distance)
            .FirstOrDefault()
            ?.Player;

        if (challenger is null)
        {
            return;
        }

        var score = ComputePassiveContestScore(possessor, challenger);
        if (score >= Tuning.PassiveContestCleanWinMargin)
        {
            possessor.HasPossession = false;
            challenger.HasPossession = true;
            BeginControlledPossession(challenger);
            return;
        }

        if (score >= 0f)
        {
            KnockBallLoose(possessor, challenger);
        }

        float playerBallContestDistance(PlayerState player)
        {
            return player.Radius + State.Ball.Radius + Tuning.PassiveContestDistance;
        }
    }

    private float ComputePassiveContestScore(PlayerState possessor, PlayerState challenger)
    {
        var possessorControl = possessor.Attributes.BallControl01;
        var challengerTackling = challenger.Attributes.EffectiveTackling / 7f;
        var ballOffset = State.Ball.Position - possessor.Position;
        var controlDistance = MathF.Max(0.0001f, ballOffset.Length());
        var exposedBall01 = Math.Clamp(
            (controlDistance - Tuning.DribbleLeadDistance) /
            MathF.Max(0.0001f, Tuning.DribbleHighControlMaxDistance),
            0f,
            1f);
        var possessorFacing = possessor.Facing.ToUnitVector();
        var challengerDirection = SafeNormalize(challenger.Position - possessor.Position, -possessorFacing);
        var frontChallenge01 = (Vector2.Dot(challengerDirection, possessorFacing) + 1f) * 0.5f;
        var challengerSpeed01 = Math.Clamp(challenger.Velocity.Length() / MathF.Max(0.0001f, challenger.MaxSpeedPerTick), 0f, 1f);
        var possessorSpeed01 = Math.Clamp(possessor.Velocity.Length() / MathF.Max(0.0001f, possessor.MaxSpeedPerTick), 0f, 1f);

        return challengerTackling * 0.58f
            + exposedBall01 * 0.28f
            + frontChallenge01 * 0.12f
            + challengerSpeed01 * 0.08f
            - possessorControl * 0.46f
            - (1f - exposedBall01) * 0.18f
            - possessorSpeed01 * 0.06f
            - Tuning.PassiveContestPossessorAdvantage;
    }

    private void KnockBallLoose(PlayerState possessor, PlayerState challenger)
    {
        var direction = SafeNormalize(State.Ball.Position - challenger.Position, possessor.Facing.ToUnitVector());
        possessor.HasPossession = false;
        possessor.DribbleStepDistance = 0f;
        State.Ball.PossessorId = null;
        State.Ball.ControlKind = BallControlKind.None;
        State.Ball.Velocity = direction * Tuning.PassiveContestKnockLooseSpeedPerTick + challenger.Velocity * 0.25f;
        State.LastTouchTeam = challenger.Team;
        _ballCollectionCooldownTicks = Math.Max(_ballCollectionCooldownTicks, 2);
        _releasedByPlayerId = possessor.Id;
        _releasedByCollectionBlockTicks = Math.Max(_releasedByCollectionBlockTicks, Tuning.DispossessedCollectionBlockTicks);
        _aftertouchTicksRemaining = 0;
        _aftertouchPlayerId = null;
        State.Ball.CurveSpinZ = 0f;
    }

    private void UseAction(PlayerState activePlayer, int heldTicks)
    {
        if (heldTicks <= Tuning.TapPassMaxTicks)
        {
            PassBall(activePlayer);
        }
        else
        {
            KickBall(activePlayer);
        }
    }

    private void PassBall(PlayerState player)
    {
        var facing = player.Facing.ToUnitVector();
        var target = FindPassTarget(player, facing);
        var direction = target is null
            ? facing
            : Vector2.Normalize(target.Position - player.Position);
        var targetDistance = target is null ? 0f : Vector2.Distance(player.Position, target.Position);
        var lift = targetDistance >= Tuning.LongPassDistance ? Tuning.PassLiftPerTick : 0f;

        ReleaseBall(player, direction, Tuning.PassSpeedPerTick, lift, Tuning.PassCollectionCooldownTicks, enableAftertouch: false);
        State.AddFeedback(
            GameplayFeedbackType.PassContact,
            player,
            State.Ball.Position,
            direction,
            0.62f);
        player.ActionCharge01 = 0f;
        player.KickAnimation01 = 1f;
        player.Velocity = Vector2.Zero;
        if (target is not null)
        {
            TransferUserControlToOutfielder(target);
        }
    }

    private void KickBall(PlayerState player)
    {
        var facing = player.Facing.ToUnitVector();
        var direction = ResolveShotDirection(player, facing);
        var power01 = Math.Clamp(Tuning.ImmediateKickPower01, 0f, 1f);
        var speed = Lerp(Tuning.MinKickSpeedPerTick, Tuning.MaxKickSpeedPerTick, power01);
        var lift01 = Math.Clamp((power01 - Tuning.KickLiftStartsAtPower) / (1f - Tuning.KickLiftStartsAtPower), 0f, 1f);
        var lift = Lerp(Tuning.MinKickLiftPerTick, Tuning.MaxKickLiftPerTick, lift01);

        ReleaseBall(
            player,
            direction,
            speed,
            lift,
            Tuning.KickCollectionCooldownTicks,
            enableAftertouch: true,
            preserveDirectionAgainstRunnerCarry: true);
        State.AddFeedback(
            GameplayFeedbackType.ShotContact,
            player,
            State.Ball.Position,
            direction,
            Math.Clamp(0.7f + power01 * 0.3f, 0f, 1f));
        player.ActionCharge01 = 0f;
        player.KickAnimation01 = 1f;
    }

    private void ReleaseBall(
        PlayerState player,
        Vector2 direction,
        float speed,
        float lift,
        int collectionCooldownTicks,
        bool enableAftertouch,
        bool preserveDirectionAgainstRunnerCarry = false)
    {
        var wasGoalkeeperDistribution = IsHoldingBallInHands(player);
        var runnerCarry = player.Velocity * Tuning.RunnerVelocityCarryToKick;
        var impulseDirection = preserveDirectionAgainstRunnerCarry
            ? CompensateForRunnerCarry(direction, speed, runnerCarry)
            : direction;
        player.HasPossession = false;
        State.Ball.PossessorId = null;
        State.Ball.ControlKind = BallControlKind.None;
        State.Ball.Position = player.Position + direction * (player.Radius + State.Ball.Radius + 0.2f);
        State.Ball.Velocity = impulseDirection * speed + runnerCarry;
        State.Ball.Height = 0f;
        State.Ball.VerticalVelocity = lift;
        _ballCollectionCooldownTicks = collectionCooldownTicks;
        _releasedByPlayerId = player.Id;
        _releasedByCollectionBlockTicks = Tuning.KickCollectionCooldownTicks;
        _ticksSinceBallReleased = 0;
        _aftertouchPlayerId = enableAftertouch ? player.Id : null;
        _aftertouchTicksRemaining = enableAftertouch ? Tuning.AftertouchWindowTicks : 0;
        _aftertouchCurveApplied = false;
        _aftertouchHeightApplied = false;
        State.Ball.CurveSpinZ = 0f;
        _aftertouchBasisForward = SafeNormalize(State.Ball.Velocity, direction);
        _aftertouchBasisRight = new Vector2(-_aftertouchBasisForward.Y, _aftertouchBasisForward.X);

        if (wasGoalkeeperDistribution)
        {
            _goalkeeperPossessionTicks = 0;
            if (player.Team == UserControlledTeam && State.ActivePlayerId == player.Id)
            {
                var nextPlayer = State.Players
                    .Where(candidate => candidate.Team == player.Team && candidate.Role == PlayerRole.Outfield)
                    .OrderBy(candidate => Vector2.DistanceSquared(candidate.Position, State.Ball.Position))
                    .ThenBy(candidate => candidate.Id)
                    .FirstOrDefault();
                if (nextPlayer is not null)
                {
                    State.ActivePlayerId = nextPlayer.Id;
                }
            }
        }
    }

    private PlayerState? FindPassTarget(PlayerState passer, Vector2 facing)
    {
        var maxTargetDistance = MathF.Max(0f, Tuning.PassTargetMaxDistance);
        var maxTargetDistanceSquared = maxTargetDistance * maxTargetDistance;
        return State.Players
            .Where(player => player.Id != passer.Id
                && player.Team == passer.Team
                && player.Role == PlayerRole.Outfield)
            .Select(player =>
            {
                var offset = player.Position - passer.Position;
                var distanceSquared = offset.LengthSquared();
                if (distanceSquared <= 0.0001f)
                {
                    return new { Player = player, DistanceSquared = distanceSquared, Alignment = -1f };
                }

                var alignment = Vector2.Dot(Vector2.Normalize(offset), facing);
                return new { Player = player, DistanceSquared = distanceSquared, Alignment = alignment };
            })
            .Where(candidate => candidate.Alignment >= Tuning.PassTargetConeCosine
                && candidate.DistanceSquared <= maxTargetDistanceSquared)
            .OrderBy(candidate => candidate.DistanceSquared)
            .FirstOrDefault()
            ?.Player;
    }

    private Vector2 ResolveShotDirection(PlayerState player, Vector2 facing)
    {
        if (player.Role != PlayerRole.Outfield || Tuning.ShotGoalAimMaxDistance <= 0f)
        {
            return facing;
        }

        var field = State.Field;
        var goalX = player.Team == TeamSide.Home ? field.Right : field.Left;
        var toGoalCenter = new Vector2(goalX, 0f) - player.Position;
        var distanceToGoalCenter = toGoalCenter.Length();
        if (distanceToGoalCenter <= 0.0001f || distanceToGoalCenter > Tuning.ShotGoalAimMaxDistance)
        {
            return facing;
        }

        var alignment = Vector2.Dot(facing, toGoalCenter / distanceToGoalCenter);
        if (alignment < Tuning.ShotGoalAimMinAlignmentCosine)
        {
            return facing;
        }

        var distanceToGoalLine = goalX - player.Position.X;
        if (MathF.Abs(facing.X) <= 0.0001f || distanceToGoalLine / facing.X <= 0f)
        {
            return facing;
        }

        var projectedGoalLineY = player.Position.Y + facing.Y * distanceToGoalLine / facing.X;
        var goalHalfWidth = field.GoalWidth * 0.5f;
        var postMargin = Math.Clamp(Tuning.ShotGoalAimPostMargin, 0f, MathF.Max(0f, goalHalfWidth - 0.1f));
        var targetY = Math.Clamp(
            projectedGoalLineY,
            field.GoalTop + postMargin,
            field.GoalBottom - postMargin);

        // A digital diagonal expresses a goal-side choice near goal. It does not become
        // a literal 45-degree miss, while already goal-bound directions remain unchanged.
        return SafeNormalize(new Vector2(goalX, targetY) - player.Position, facing);
    }

    private void MoveLooseBall()
    {
        if (MathF.Abs(State.Ball.CurveSpinZ) > 0.0001f && State.Ball.Velocity.LengthSquared() > 0.0001f)
        {
            var forward = Vector2.Normalize(State.Ball.Velocity);
            var right = new Vector2(-forward.Y, forward.X);
            var speed = State.Ball.Velocity.Length();
            var isAirborne = State.Ball.Height > 0f || State.Ball.VerticalVelocity > 0f;
            var surfaceFactor = isAirborne ? 1f : Tuning.GroundMagnusFactor;
            var curveModeFactor = Tuning.BallCurveMode == BallCurveMode.SwosLikeLut
                ? Tuning.SwosCurveMagnusMultiplier
                : 1f;
            var magnus = right * (
                State.Ball.CurveSpinZ
                * speed
                * Tuning.MagnusLateralPerTick
                * surfaceFactor
                * curveModeFactor);
            State.Ball.Velocity += magnus;
        }

        State.Ball.Position += State.Ball.Velocity;
        State.Ball.Velocity = Approach(State.Ball.Velocity, Vector2.Zero, Tuning.BallFrictionPerTick);
        State.Ball.Height += State.Ball.VerticalVelocity;
        State.Ball.VerticalVelocity -= Tuning.BallGravityPerTick;
        State.Ball.CurveSpinZ *= Tuning.SpinDecayPerTick;

        if (State.Ball.Height <= 0f)
        {
            State.Ball.Height = 0f;
            if (State.Ball.VerticalVelocity < -Tuning.BallMinBounceVelocityPerTick)
            {
                State.Ball.VerticalVelocity = -State.Ball.VerticalVelocity * Tuning.BallBounceEnergyRetention;
            }
            else
            {
                State.Ball.VerticalVelocity = 0f;
            }

            var groundSpinRetention = Math.Min(
                Tuning.SpinGroundDampingPerTick,
                Tuning.MaxGroundSpinRetentionPerTick);
            State.Ball.CurveSpinZ *= Math.Clamp(groundSpinRetention, 0f, 1f);
        }

        if (State.Ball.Velocity.LengthSquared() < Tuning.MinBallSpeedPerTick * Tuning.MinBallSpeedPerTick)
        {
            State.Ball.Velocity = Vector2.Zero;
        }
    }

    private bool TryGoalkeeperCatch(Vector2 previousBallPosition)
    {
        // A ball at an outfielder's feet can be smothered by the opposing goalkeeper;
        // otherwise a dribbler could walk it past a set goalkeeper into the goal.
        var carrier = State.Ball.IsPossessed
            ? State.Players.First(player => player.Id == State.Ball.PossessorId)
            : null;
        if (State.Ball.Height > Tuning.GoalkeeperCatchMaxHeight
            || (carrier is not null && State.Ball.ControlKind != BallControlKind.Feet))
        {
            return false;
        }

        foreach (var goalkeeper in State.Players
            .Where(player => player.IsGoalkeeper && player.Team != carrier?.Team)
            .OrderBy(player => Vector2.DistanceSquared(player.Position, State.Ball.Position)))
        {
            if (goalkeeper.Id == _releasedByPlayerId && _releasedByCollectionBlockTicks > 0)
            {
                continue;
            }

            var catchPoint = ClosestPointOnSegment(
                goalkeeper.Position,
                previousBallPosition,
                State.Ball.Position);
            if (!IsInsideOwnPenaltyArea(goalkeeper.Team, catchPoint))
            {
                continue;
            }

            var attributes = goalkeeper.GoalkeeperAttributes ?? GoalkeeperAttributes.Default;
            var catchDistance = Tuning.GoalkeeperCatchReach
                * Lerp(0.75f, 1.1f, (attributes.Handling01 + attributes.Reflexes01) * 0.5f);
            if (carrier is not null)
            {
                // A dribbled ball is taken once it runs into the goalkeeper's body.
                catchDistance = MathF.Max(catchDistance, goalkeeper.Radius);
            }

            if (Vector2.DistanceSquared(goalkeeper.Position, catchPoint) > catchDistance * catchDistance)
            {
                continue;
            }

            BeginGoalkeeperHandsPossession(goalkeeper);
            return true;
        }

        return false;
    }

    private void BeginGoalkeeperHandsPossession(PlayerState goalkeeper)
    {
        var previousPossessorId = State.Ball.PossessorId;
        var previousActivePlayerId = State.ActivePlayerId;
        var possessionTeamChanged = _lastPossessionTeam != goalkeeper.Team;
        foreach (var player in State.Players)
        {
            player.HasPossession = player.Id == goalkeeper.Id;
            player.ActionCharge01 = 0f;
        }

        goalkeeper.Velocity = Vector2.Zero;
        goalkeeper.DribbleStepDistance = 0f;
        State.Ball.PossessorId = goalkeeper.Id;
        State.Ball.ControlKind = BallControlKind.GoalkeeperHands;
        HoldBallInGoalkeeperHands(goalkeeper);
        _goalkeeperPossessionTicks = 0;
        _aftertouchTicksRemaining = 0;
        _aftertouchPlayerId = null;
        _dribbleSecureTicksRemaining = 0;
        _dribbleRisk = 0f;
        _dribbleSustain01 = 0f;
        _lastPossessionTeam = goalkeeper.Team;
        State.LastTouchTeam = goalkeeper.Team;

        if (previousPossessorId != goalkeeper.Id)
        {
            State.AddFeedback(
                GameplayFeedbackType.GoalkeeperCatch,
                goalkeeper,
                goalkeeper.Position,
                goalkeeper.Facing.ToUnitVector(),
                0.82f);
        }

        if (goalkeeper.Team == UserControlledTeam)
        {
            State.ActivePlayerId = goalkeeper.Id;
            _playerActionStates.Remove(previousActivePlayerId);
            _playerActionStates.Remove(goalkeeper.Id);
            _shotTeamWhileActionHeld = null;
            _shotActionPlayerId = null;
        }
        else
        {
            SelectUserDefenderForOpponentPossession(
                goalkeeper.Position,
                forceSwitch: possessionTeamChanged);
        }
    }

    private void TryCollectBall()
    {
        if (State.Ball.IsPossessed)
        {
            return;
        }

        if (_ballCollectionCooldownTicks > 0)
        {
            return;
        }

        var collector = State.Players
            .Where(player => player.Role == PlayerRole.Outfield)
            .Where(player => !player.IsSliding && !player.IsRecoveringFromSlide)
            .Where(player => _shotTeamWhileActionHeld is null || player.Team != _shotTeamWhileActionHeld)
            .Where(player => player.Id != _releasedByPlayerId || _releasedByCollectionBlockTicks <= 0)
            .Select(player => new
            {
                Player = player,
                Distance = Vector2.Distance(player.Position, State.Ball.Position)
            })
            .Where(candidate => candidate.Distance <= candidate.Player.Radius + State.Ball.Radius + Tuning.PossessionDistance)
            .OrderBy(candidate => candidate.Distance)
            .FirstOrDefault()
            ?.Player;

        if (collector is null)
        {
            return;
        }

        collector.HasPossession = true;
        BeginControlledPossession(collector);
    }

    private void ApplyAftertouchInput(Vector2 move)
    {
        if (_aftertouchTicksRemaining <= 0 || _aftertouchPlayerId is null || State.Ball.IsPossessed)
        {
            return;
        }

        var moveLocalRight = Vector2.Dot(move, _aftertouchBasisRight);
        var moveLocalForward = Vector2.Dot(move, _aftertouchBasisForward);
        var lateralInput = NormalizeAftertouchInput(moveLocalRight, Tuning.AftertouchInputDeadzone);
        var heightInput = NormalizeAftertouchInput(moveLocalForward, Tuning.AftertouchInputDeadzone);

        if (State.Ball.Velocity.LengthSquared() > 0.0001f)
        {
            var timing01 = Math.Clamp(
                (float)_aftertouchTicksRemaining / Math.Max(1, Tuning.AftertouchWindowTicks),
                0f,
                1f);
            var timingStrength = 0.45f + timing01 * 0.55f;
            if (!_aftertouchCurveApplied && lateralInput != 0f)
            {
                var spinImpulse = Tuning.BallCurveMode == BallCurveMode.SwosLikeLut
                    ? Tuning.SwosCurveSpinImpulse
                    : Tuning.AftertouchSpinImpulse;
                State.Ball.CurveSpinZ = Math.Clamp(
                    lateralInput * spinImpulse * timingStrength,
                    -Tuning.SpinMaxAbs,
                    Tuning.SpinMaxAbs);
                _aftertouchCurveApplied = true;
            }

            var heightInputWindowOpen = Tuning.AftertouchWindowTicks - _aftertouchTicksRemaining
                < Tuning.AftertouchHeightInputTicks;
            if (!_aftertouchHeightApplied && heightInputWindowOpen && heightInput > 0f)
            {
                State.Ball.VerticalVelocity = Math.Max(
                    0f,
                    State.Ball.VerticalVelocity
                    - heightInput * Tuning.AftertouchDriveVerticalImpulse * timingStrength);
                _aftertouchHeightApplied = true;
            }
            else if (!_aftertouchHeightApplied && heightInputWindowOpen && heightInput < 0f)
            {
                State.Ball.VerticalVelocity = Math.Min(
                    Tuning.AftertouchMaxVerticalVelocityPerTick,
                    State.Ball.VerticalVelocity
                    - heightInput * Tuning.AftertouchLoftVerticalImpulse * timingStrength);
                _aftertouchHeightApplied = true;
            }
        }

        _aftertouchTicksRemaining--;
    }

    private void StartSlidingTackle(PlayerState player)
    {
        player.SlideTicksRemaining = Tuning.SlideTravelTicks;
        player.SlideRecoveryTicksRemaining = 0;
        player.DribbleStepDistance = 0f;
        player.Velocity = player.Facing.ToUnitVector() * Tuning.SlideSpeedPerTick;
        State.AddFeedback(
            GameplayFeedbackType.TackleStarted,
            player,
            player.Position,
            player.Facing.ToUnitVector(),
            0.55f);
    }

    private void HandleSlidingBallInteraction(PlayerState slider)
    {
        var slideDirection = SafeNormalize(slider.Velocity, slider.Facing.ToUnitVector());
        var hitDistance = slider.Radius + State.Ball.Radius + Tuning.SlideTackleHitDistance;

        var possessor = State.Players.FirstOrDefault(player => player.Id != slider.Id && player.HasPossession);
        if (possessor is not null)
        {
            if (IsHoldingBallInHands(possessor))
            {
                return;
            }

            var toPossessor = possessor.Position - slider.Position;
            if (toPossessor.Length() <= hitDistance && Vector2.Dot(SafeNormalize(toPossessor, slideDirection), slideDirection) > 0.1f)
            {
                KnockBallLoose(possessor, slider);
            }

            return;
        }

        if (State.Ball.IsPossessed)
        {
            return;
        }

        var toBall = State.Ball.Position - slider.Position;
        if (toBall.Length() > hitDistance || Vector2.Dot(SafeNormalize(toBall, slideDirection), slideDirection) <= 0.1f)
        {
            return;
        }

        State.Ball.Velocity = slideDirection * Tuning.SlideLooseBallDeflectSpeedPerTick;
        State.LastTouchTeam = slider.Team;
        _ballCollectionCooldownTicks = Math.Max(_ballCollectionCooldownTicks, 2);
    }

    private void BeginControlledPossession(PlayerState player)
    {
        var previousPossessorId = State.Ball.PossessorId;
        var control01 = player.Attributes.BallControl01;
        var stepDistance = Lerp(Tuning.DribbleMinStepDistance, Tuning.DribbleMaxStepDistance, control01);
        var facing = player.Facing.ToUnitVector();

        foreach (var candidate in State.Players)
        {
            candidate.HasPossession = candidate.Id == player.Id;
        }

        player.DribbleStepDistance = -stepDistance * 0.5f;
        State.Ball.PossessorId = player.Id;
        State.Ball.ControlKind = BallControlKind.Feet;
        State.Ball.Position = player.Position + facing * Tuning.DribbleLeadDistance;
        State.Ball.Velocity = player.Velocity * 0.85f;
        State.Ball.Height = 0f;
        State.Ball.VerticalVelocity = 0f;
        State.Ball.CurveSpinZ = 0f;
        _aftertouchTicksRemaining = 0;
        _aftertouchPlayerId = null;
        _newPossessionProtectionTicks = Tuning.NewPossessionProtectionTicks;
        _dribbleSecureTicksRemaining = Tuning.DribbleSecureTicksAfterTouch;
        _dribbleRisk = 0f;
        _dribbleSustain01 = 0f;
        _previousDribbleMoveDirection = Vector2.Zero;
        _previousDribbleSideDistance = 0f;
        _lastDribbleTurn01 = 0f;
        State.LastTouchTeam = player.Team;

        UpdateUserControlForPossession(player);

        if (previousPossessorId != player.Id)
        {
            State.AddFeedback(
                GameplayFeedbackType.PossessionChanged,
                player,
                player.Position,
                player.Facing.ToUnitVector(),
                0.42f);
        }
    }

    private void TransferUserControlToOutfielder(PlayerState player)
    {
        if (player.Team != UserControlledTeam
            || player.Role != PlayerRole.Outfield
            || State.ActivePlayerId == player.Id)
        {
            return;
        }

        SetActiveUserPlayer(player);
    }

    private void UpdateUserControlForPossession(PlayerState possessor)
    {
        var possessionTeamChanged = _lastPossessionTeam != possessor.Team;
        _lastPossessionTeam = possessor.Team;
        if (possessor.Team == UserControlledTeam)
        {
            TransferUserControlToOutfielder(possessor);
            return;
        }

        SelectUserDefenderForOpponentPossession(
            possessor.Position,
            forceSwitch: possessionTeamChanged);
    }

    private void SelectUserDefenderForOpponentPossession(Vector2 threatPosition, bool forceSwitch)
    {
        var allOutfielders = State.Players
            .Where(player => player.Team == UserControlledTeam && player.Role == PlayerRole.Outfield)
            .ToArray();
        var availableOutfielders = allOutfielders
            .Where(player => !player.IsSliding && !player.IsRecoveringFromSlide)
            .ToArray();
        var candidates = availableOutfielders.Length > 0 ? availableOutfielders : allOutfielders;
        var best = candidates
            .OrderBy(player => DefensiveControlScore(player, threatPosition))
            .ThenBy(player => player.Id)
            .FirstOrDefault();
        if (best is null)
        {
            return;
        }

        var current = candidates.FirstOrDefault(player => player.Id == State.ActivePlayerId);
        if (!forceSwitch
            && current is not null
            && DefensiveControlScore(current, threatPosition)
                <= DefensiveControlScore(best, threatPosition) + DefensiveControlSwitchHysteresis)
        {
            return;
        }

        SetActiveUserPlayer(best);
    }

    private float DefensiveControlScore(PlayerState player, Vector2 threatPosition)
    {
        var wrongSideDistance = UserControlledTeam == TeamSide.Home
            ? MathF.Max(0f, player.Position.X - threatPosition.X)
            : MathF.Max(0f, threatPosition.X - player.Position.X);
        return Vector2.Distance(player.Position, threatPosition)
            + wrongSideDistance * DefensiveWrongSidePenalty;
    }

    private void SetActiveUserPlayer(PlayerState player)
    {
        if (State.ActivePlayerId == player.Id)
        {
            return;
        }

        var previousActivePlayerId = State.ActivePlayerId;
        State.ActivePlayerId = player.Id;
        _playerActionStates.Remove(previousActivePlayerId);
        _playerActionStates.Remove(player.Id);
    }

    private void ResolveGoalkeeperZone(PlayerState goalkeeper)
    {
        var field = State.Field;
        var minX = goalkeeper.Team == TeamSide.Home
            ? field.Left + goalkeeper.Radius
            : field.Right - Tuning.GoalkeeperMaxAdvanceDistance;
        var maxX = goalkeeper.Team == TeamSide.Home
            ? field.Left + Tuning.GoalkeeperMaxAdvanceDistance
            : field.Right - goalkeeper.Radius;
        var minY = Math.Max(field.Top + goalkeeper.Radius, field.GoalTop - Tuning.GoalkeeperLateralPadding);
        var maxY = Math.Min(field.Bottom - goalkeeper.Radius, field.GoalBottom + Tuning.GoalkeeperLateralPadding);
        var clamped = new Vector2(
            Math.Clamp(goalkeeper.Position.X, minX, maxX),
            Math.Clamp(goalkeeper.Position.Y, minY, maxY));

        if (clamped.X != goalkeeper.Position.X)
        {
            goalkeeper.Velocity = new Vector2(0f, goalkeeper.Velocity.Y);
        }

        if (clamped.Y != goalkeeper.Position.Y)
        {
            goalkeeper.Velocity = new Vector2(goalkeeper.Velocity.X, 0f);
        }

        goalkeeper.Position = clamped;
    }

    private void ResolvePlayerBounds(PlayerState player)
    {
        var field = State.Field;
        var minX = field.Left + player.Radius;
        var maxX = field.Right - player.Radius;
        var minY = field.Top + player.Radius;
        var maxY = field.Bottom - player.Radius;
        var position = player.Position;
        var velocity = player.Velocity;

        if ((position.X < minX && velocity.X < 0f) || (position.X > maxX && velocity.X > 0f))
        {
            velocity.X = 0f;
        }

        if ((position.Y < minY && velocity.Y < 0f) || (position.Y > maxY && velocity.Y > 0f))
        {
            velocity.Y = 0f;
        }

        player.Position = new Vector2(
            Math.Clamp(position.X, minX, maxX),
            Math.Clamp(position.Y, minY, maxY));
        player.Velocity = velocity;
    }

    private void CheckBallRules()
    {
        var field = State.Field;
        var ball = State.Ball;
        if (ball.IsPossessed)
        {
            // A ball carried over a line at a player's feet is out like any other, against the carrier.
            // A goalkeeper holding the ball is left alone: carrying it over the own goal line is not modelled.
            var carrier = State.Players.First(player => player.Id == ball.PossessorId);
            var inside = ball.Position.X >= field.Left && ball.Position.X <= field.Right
                && ball.Position.Y >= field.Top && ball.Position.Y <= field.Bottom;
            if (ball.ControlKind != BallControlKind.Feet || inside)
            {
                return;
            }

            State.LastTouchTeam = carrier.Team;
        }


        if (ball.Position.Y < field.Top || ball.Position.Y > field.Bottom)
        {
            var kickInTeam = TeamAwardedAgainstLastTouch(ball.Position);
            var kickInSpot = ClampToField(ball.Position);
            StopForRestart(new MatchEvent(
                MatchEventType.TouchlineOut,
                RestartKind.KickIn,
                kickInTeam,
                kickInSpot,
                Description: $"Kick-in to {kickInTeam}."));
            PlaceRestartTaker(kickInTeam, kickInSpot, new Vector2(0f, -MathF.Sign(kickInSpot.Y)));
            return;
        }

        if (ball.Position.X < field.Left || ball.Position.X > field.Right)
        {
            if (ball.Position.Y >= field.GoalTop && ball.Position.Y <= field.GoalBottom)
            {
                var scoringTeam = ball.Position.X > field.Right ? TeamSide.Home : TeamSide.Away;
                if (scoringTeam == TeamSide.Home)
                {
                    State.HomeScore++;
                }
                else
                {
                    State.AwayScore++;
                }

                State.AddFeedback(
                    GameplayFeedbackType.GoalScored,
                    player: null,
                    ball.Position,
                    scoringTeam == TeamSide.Home ? Vector2.UnitX : -Vector2.UnitX,
                    1f);

                StopForRestart(new MatchEvent(
                    MatchEventType.GoalScored,
                    RestartKind.Kickoff,
                    scoringTeam,
                    Position: Vector2.Zero,
                    Description: $"{scoringTeam} scored."));
                ResetForKickoff(scoringTeam == TeamSide.Home ? TeamSide.Away : TeamSide.Home);
                return;
            }

            var defendingTeam = ball.Position.X < field.Left ? TeamSide.Home : TeamSide.Away;
            if (State.LastTouchTeam == defendingTeam)
            {
                var cornerTeam = Opponent(defendingTeam);
                var cornerSpot = new Vector2(
                    ball.Position.X < field.Left ? field.Left : field.Right,
                    ball.Position.Y < 0f ? field.Top : field.Bottom);
                StopForRestart(new MatchEvent(
                    MatchEventType.GoalLineOut,
                    RestartKind.Corner,
                    cornerTeam,
                    cornerSpot,
                    Description: $"Corner to {cornerTeam}."));
                PlaceRestartTaker(
                    cornerTeam,
                    cornerSpot,
                    new Vector2(-MathF.Sign(cornerSpot.X), -MathF.Sign(cornerSpot.Y)));
                return;
            }

            StopForRestart(new MatchEvent(
                MatchEventType.GoalLineOut,
                RestartKind.GoalClearance,
                defendingTeam,
                ClampToField(ball.Position),
                Description: $"Goal clearance to {defendingTeam}."));
            BeginGoalkeeperHandsPossession(State.Players.First(player =>
                player.Team == defendingTeam && player.IsGoalkeeper));
        }
    }

    private static TeamSide Opponent(TeamSide team)
    {
        return team == TeamSide.Home ? TeamSide.Away : TeamSide.Home;
    }

    private TeamSide TeamAwardedAgainstLastTouch(Vector2 ballPosition)
    {
        // With no recorded touch (hand-built states), give the ball to the team defending that half.
        return State.LastTouchTeam is TeamSide lastTouch
            ? Opponent(lastTouch)
            : ballPosition.X < 0f ? TeamSide.Home : TeamSide.Away;
    }

    /// <summary>
    /// Sets up a kick-in or corner: the awarded team's nearest outfielder gets the ball at the spot,
    /// facing into the court, and opponents are moved back to the required distance.
    /// </summary>
    // ponytail: the taker is teleported, players stand still during the stoppage, and the taker may
    // dribble the ball in rather than having to kick it. Near a corner the court edge can leave a
    // moved opponent closer than the required distance. Walk-to-ball, repositioning runs and the
    // four-second count are the upgrade path once restarts have been playtested.
    private void PlaceRestartTaker(TeamSide team, Vector2 spot, Vector2 inward)
    {
        var taker = State.Players
            .Where(player => player.Team == team && player.Role == PlayerRole.Outfield)
            .OrderBy(player => Vector2.DistanceSquared(player.Position, spot))
            .ThenBy(player => player.Id)
            .First();
        taker.Position = spot;
        taker.Velocity = Vector2.Zero;
        taker.Facing = Azimuth.FromVector(inward).Quantized(8);
        // The nearest player may have been mid-tackle when the ball went out.
        taker.SlideTicksRemaining = 0;
        taker.SlideRecoveryTicksRemaining = 0;

        foreach (var opponent in State.Players.Where(player =>
                     player.Team != team && player.Role == PlayerRole.Outfield))
        {
            var offset = opponent.Position - spot;
            if (offset.Length() >= Tuning.RestartOpponentDistance)
            {
                continue;
            }

            opponent.Position = ClampToField(
                spot + SafeNormalize(offset, inward) * Tuning.RestartOpponentDistance);
            opponent.Velocity = Vector2.Zero;
        }

        taker.HasPossession = true;
        BeginControlledPossession(taker);
    }

    private void StopForRestart(MatchEvent matchEvent)
    {
        State.Ball.Position = matchEvent.Position;
        State.Ball.Velocity = Vector2.Zero;
        State.Ball.Height = 0f;
        State.Ball.VerticalVelocity = 0f;
        State.Ball.CurveSpinZ = 0f;
        State.Ball.PossessorId = null;
        State.Ball.ControlKind = BallControlKind.None;
        foreach (var player in State.Players)
        {
            player.HasPossession = false;
            player.ActionCharge01 = 0f;
        }

        _playerActionStates.Clear();
        _shotTeamWhileActionHeld = null;
        _shotActionPlayerId = null;

        State.IsBallOutOfPlay = true;
        State.RestartTicksRemaining = Tuning.RestartDelayTicks;
        State.LastEvent = matchEvent;
    }

    private void ResetForKickoff(TeamSide kickoffTeam)
    {
        _playerActionStates.Clear();
        _shotTeamWhileActionHeld = null;
        _shotActionPlayerId = null;
        // Half-time can interrupt any action, so nothing from the old passage of play may leak through.
        _ballCollectionCooldownTicks = 0;
        _releasedByPlayerId = null;
        _releasedByCollectionBlockTicks = 0;
        _goalkeeperPossessionTicks = 0;
        foreach (var candidate in State.Players)
        {
            candidate.Position = candidate.KickoffPosition;
            candidate.Velocity = Vector2.Zero;
            candidate.Facing = FacingForTeam(candidate.Team);
            candidate.HasPossession = false;
            candidate.DribbleStepDistance = 0f;
            candidate.SlideTicksRemaining = 0;
            candidate.SlideRecoveryTicksRemaining = 0;
        }

        // The forward-most outfielder takes it, so the players behind stay available for the first pass.
        var player = State.Players
            .Where(candidate => candidate.Team == kickoffTeam && candidate.Role == PlayerRole.Outfield)
            .OrderBy(candidate => candidate.KickoffPosition.LengthSquared())
            .ThenBy(candidate => candidate.Id)
            .First();
        if (player.Team == UserControlledTeam)
        {
            TransferUserControlToOutfielder(player);
        }
        else
        {
            var userOutfielder = State.Players
                .Where(candidate => candidate.Team == UserControlledTeam && candidate.Role == PlayerRole.Outfield)
                .OrderBy(candidate => Vector2.DistanceSquared(candidate.Position, State.Ball.Position))
                .ThenBy(candidate => candidate.Id)
                .FirstOrDefault();
            if (userOutfielder is not null)
            {
                TransferUserControlToOutfielder(userOutfielder);
            }
        }

        // Standing over the ball: any further back and it is outside the taker's control,
        // which turns the kickoff into a loose ball that both teams run at.
        player.Position = -FacingForTeam(kickoffTeam).ToUnitVector() * Tuning.DribbleLeadDistance;
        player.Velocity = Vector2.Zero;
        player.Facing = FacingForTeam(kickoffTeam);
        player.HasPossession = true;

        State.Ball.Position = Vector2.Zero;
        State.Ball.Velocity = Vector2.Zero;
        State.Ball.Height = 0f;
        State.Ball.VerticalVelocity = 0f;
        State.Ball.CurveSpinZ = 0f;
        State.Ball.PossessorId = player.Id;
        State.Ball.ControlKind = BallControlKind.Feet;
        _aftertouchTicksRemaining = 0;
        _aftertouchPlayerId = null;
        _newPossessionProtectionTicks = Tuning.NewPossessionProtectionTicks;
        _dribbleSecureTicksRemaining = Tuning.DribbleSecureTicksAfterTouch;
        _dribbleRisk = 0f;
        _dribbleSustain01 = 0f;
        _previousDribbleMoveDirection = Vector2.Zero;
        _previousDribbleSideDistance = 0f;
        _lastDribbleTurn01 = 0f;
        _lastPossessionTeam = kickoffTeam;
        State.LastTouchTeam = kickoffTeam;
    }

    private Vector2 ClampToField(Vector2 position)
    {
        var field = State.Field;
        return new Vector2(
            Math.Clamp(position.X, field.Left, field.Right),
            Math.Clamp(position.Y, field.Top, field.Bottom));
    }

    private static Azimuth FacingForTeam(TeamSide team)
    {
        return team == TeamSide.Home ? Azimuth.East : Azimuth.West;
    }

    private static PlayerState CreatePlayer(
        int id,
        string name,
        byte shirtNumber,
        TeamSide team,
        FutsalOutfieldRole futsalRole,
        Vector2 position,
        PhysicsTuning tuning,
        Azimuth facing,
        PlayerAttributes attributes)
    {
        var speed01 = attributes.Speed01;
        return new PlayerState(id, name, team, position)
        {
            ShirtNumber = shirtNumber,
            Facing = facing,
            FutsalRole = futsalRole,
            Attributes = attributes,
            Radius = tuning.PlayerRadius,
            MaxSpeedPerTick = tuning.PlayerMaxSpeedPerTick * Lerp(0.82f, 1.1f, speed01),
            AccelerationPerTick = tuning.PlayerAccelerationPerTick,
            FrictionPerTick = tuning.PlayerFrictionPerTick
        };
    }

    private static PlayerState CreateGoalkeeper(
        int id,
        string name,
        byte shirtNumber,
        TeamSide team,
        Vector2 position,
        PhysicsTuning tuning)
    {
        var attributes = new PlayerAttributes(8, 4, 4, 4, 8, 8, 4);
        return new PlayerState(id, name, team, position)
        {
            ShirtNumber = shirtNumber,
            Role = PlayerRole.Goalkeeper,
            GoalkeeperAttributes = GoalkeeperAttributes.Default,
            Facing = FacingForTeam(team),
            Attributes = attributes,
            Radius = tuning.PlayerRadius,
            MaxSpeedPerTick = tuning.GoalkeeperMaxSpeedPerTick,
            AccelerationPerTick = tuning.GoalkeeperAccelerationPerTick,
            FrictionPerTick = tuning.PlayerFrictionPerTick
        };
    }

    private bool IsHoldingBallInHands(PlayerState player)
    {
        return player.IsGoalkeeper
            && player.HasPossession
            && State.Ball.PossessorId == player.Id
            && State.Ball.ControlKind == BallControlKind.GoalkeeperHands;
    }

    private bool IsInsideOwnPenaltyArea(TeamSide team, Vector2 position)
    {
        return State.Field.ContainsInPenaltyArea(team, position);
    }

    private static Vector2 ClosestPointOnSegment(Vector2 point, Vector2 segmentStart, Vector2 segmentEnd)
    {
        var segment = segmentEnd - segmentStart;
        var lengthSquared = segment.LengthSquared();
        if (lengthSquared <= 0.0001f)
        {
            return segmentStart;
        }

        var amount = Math.Clamp(Vector2.Dot(point - segmentStart, segment) / lengthSquared, 0f, 1f);
        return segmentStart + segment * amount;
    }

    private static Vector2 Approach(Vector2 current, Vector2 target, float maxDelta)
    {
        var delta = target - current;
        var distance = delta.Length();
        if (distance <= maxDelta || distance <= 0.0001f)
        {
            return target;
        }

        return current + delta / distance * maxDelta;
    }

    private static Vector2 ClampLength(Vector2 vector, float maxLength)
    {
        var lengthSquared = vector.LengthSquared();
        if (lengthSquared <= maxLength * maxLength)
        {
            return vector;
        }

        return Vector2.Normalize(vector) * maxLength;
    }

    private static Vector2 SafeNormalize(Vector2 vector, Vector2 fallback)
    {
        return vector.LengthSquared() <= 0.0001f ? fallback : Vector2.Normalize(vector);
    }

    private static Vector2 CompensateForRunnerCarry(Vector2 desiredDirection, float kickSpeed, Vector2 runnerCarry)
    {
        var desired = SafeNormalize(desiredDirection, Vector2.UnitX);
        if (kickSpeed <= 0.0001f || runnerCarry.LengthSquared() <= 0.0001f)
        {
            return desired;
        }

        var parallelCarry = Vector2.Dot(runnerCarry, desired);
        var lateralCarrySquared = MathF.Max(
            0f,
            runnerCarry.LengthSquared() - parallelCarry * parallelCarry);
        var kickSpeedSquared = kickSpeed * kickSpeed;
        if (lateralCarrySquared >= kickSpeedSquared)
        {
            return desired;
        }

        var resultingSpeed = parallelCarry + MathF.Sqrt(kickSpeedSquared - lateralCarrySquared);
        if (resultingSpeed <= 0.0001f)
        {
            return desired;
        }

        return SafeNormalize(desired * resultingSpeed - runnerCarry, desired);
    }

    private static float Lerp(float from, float to, float amount)
    {
        return from + (to - from) * amount;
    }

    private static float ApproachScalar(float current, float target, float maxDelta)
    {
        if (current < target)
        {
            return Math.Min(current + maxDelta, target);
        }

        return Math.Max(current - maxDelta, target);
    }

    private void EvaluateDribbleControlRisk(PlayerState player)
    {
        _dribbleSecureTicksRemaining = Math.Max(0, _dribbleSecureTicksRemaining - 1);
        _dribbleRisk *= Tuning.DribbleRiskDecayPerTick;

        var speed01 = Math.Clamp(player.Velocity.Length() / MathF.Max(0.0001f, player.MaxSpeedPerTick), 0f, 1f);
        if (speed01 <= 0.06f)
        {
            _dribbleRisk *= 0.84f;
            _dribbleSustain01 = Math.Max(0f, _dribbleSustain01 - 0.05f);
            _previousDribbleMoveDirection = Vector2.Zero;
            _previousDribbleSideDistance = 0f;
            _lastDribbleTurn01 = 0f;
            return;
        }

        var control01 = player.Attributes.BallControl01;
        var velocityDirection = SafeNormalize(player.Velocity, player.Facing.ToUnitVector());
        var currentMoveDirection = player.Facing.ToUnitVector();
        var turn01 = 0f;
        if (_previousDribbleMoveDirection.LengthSquared() > 0.0001f)
        {
            turn01 = (1f - Math.Clamp(Vector2.Dot(_previousDribbleMoveDirection, currentMoveDirection), -1f, 1f)) * 0.5f;
        }

        _previousDribbleMoveDirection = currentMoveDirection;
        var turn01Adjusted = MathF.Pow(Math.Clamp(turn01, 0f, 1f), 0.65f);
        _lastDribbleTurn01 = turn01Adjusted;

        var offset = State.Ball.Position - player.Position;
        var distance = offset.Length();
        var excessDistance01 = Math.Clamp(
            (distance - Tuning.DribbleLeadDistance) / MathF.Max(0.0001f, Tuning.DribbleHighControlMaxDistance),
            0f,
            1f);
        var right = new Vector2(-velocityDirection.Y, velocityDirection.X);
        var sideDistance = MathF.Abs(Vector2.Dot(offset, right));
        var sideChange01 = Math.Clamp(
            MathF.Abs(sideDistance - _previousDribbleSideDistance) /
            MathF.Max(0.0001f, player.Radius + State.Ball.Radius + Tuning.PossessionDistance),
            0f,
            1f);
        _previousDribbleSideDistance = sideDistance;
        var sideDistance01 = Math.Clamp(
            sideDistance / MathF.Max(0.0001f, player.Radius + State.Ball.Radius + Tuning.PossessionDistance),
            0f,
            1f);
        var careless01 = Math.Clamp(MathF.Max(turn01Adjusted, sideChange01 * 1.2f), 0f, 1f);
        if (careless01 <= 0.008f)
        {
            _dribbleRisk *= 0.93f;
        }

        _dribbleSustain01 = Math.Clamp(_dribbleSustain01 + Tuning.DribbleSustainRampPerTick, 0f, 1f);
        var sustainMultiplier = 1f + _dribbleSustain01 * (Tuning.DribbleSustainMaxMultiplier - 1f);

        var secureMultiplier = _dribbleSecureTicksRemaining > 0 ? 0.42f : 1f;
        var turnRisk = turn01Adjusted
            * speed01
            * (1f - control01)
            * Tuning.DribbleRiskTurnGainPerTick
            * secureMultiplier
            * sustainMultiplier;
        var distanceRisk = excessDistance01
            * speed01
            * careless01
            * (1.1f - control01 * 0.7f)
            * Tuning.DribbleRiskDistanceGainPerTick
            * sustainMultiplier;
        var sideRisk = sideDistance01
            * speed01
            * careless01
            * (1.05f - control01 * 0.6f)
            * Tuning.DribbleRiskDistanceGainPerTick
            * 0.9f
            * sustainMultiplier;

        // Running pressure amplifies poor touches, but a clean straight run is never a reason by itself
        // to take control away from the player.
        _dribbleRisk += turnRisk + distanceRisk + sideRisk;

        if (_dribbleRisk > Tuning.DribbleRiskBreakThreshold)
        {
            LoseBall(player);
        }
    }

    private static float NormalizeAftertouchInput(float value, float deadzone)
    {
        var abs = MathF.Abs(value);
        if (abs <= deadzone)
        {
            return 0f;
        }

        var normalized = (abs - deadzone) / Math.Max(0.0001f, 1f - deadzone);
        return MathF.Sign(value) * Math.Clamp(normalized, 0f, 1f);
    }

    private sealed class PlayerActionInputState
    {
        public bool PreviousActionDown { get; set; }
        public bool ActionCommittedThisHold { get; set; }
        public int ActionHeldTicks { get; set; }
    }
}
