using System.Numerics;

namespace Swof.Engine;

public sealed class MatchEngine
{
    private bool _previousActionDown;
    private bool _actionCommittedThisHold;
    private int _actionHeldTicks;
    private int _ballCollectionCooldownTicks;
    private int? _releasedByPlayerId;
    private int _releasedByCollectionBlockTicks;
    private int _aftertouchTicksRemaining;
    private int? _aftertouchPlayerId;
    private int _dribbleSecureTicksRemaining;
    private float _dribbleRisk;
    private float _dribbleSustain01;
    private Vector2 _previousDribbleMoveDirection = Vector2.Zero;
    private float _previousDribbleSideDistance;
    private float _lastDribbleTurn01;
    private Vector2 _aftertouchBasisForward = Vector2.UnitX;
    private Vector2 _aftertouchBasisRight = Vector2.UnitY;
    private float _accumulatedSeconds;

    public MatchEngine(MatchState state, PhysicsTuning? tuning = null)
    {
        State = state;
        Tuning = tuning ?? PhysicsTuning.Default;
        CurrentSnapshot = MatchSnapshot.FromState(State);
        PreviousSnapshot = CurrentSnapshot;
    }

    public MatchState State { get; }
    public PhysicsTuning Tuning { get; private set; }
    public MatchSnapshot PreviousSnapshot { get; private set; }
    public MatchSnapshot CurrentSnapshot { get; private set; }
    public float RenderAlpha => Math.Clamp(_accumulatedSeconds / Tuning.TickSeconds, 0f, 1f);

    public static MatchEngine CreateDefault(PhysicsTuning? tuning = null)
    {
        tuning ??= PhysicsTuning.Default;
        var field = FieldDimensions.Default;
        var poor = new PlayerAttributes(4, 4, 4, 4, 4, 4, 4);
        var moderate = new PlayerAttributes(8, 8, 8, 8, 8, 8, 8);
        var elite = new PlayerAttributes(15, 15, 15, 15, 15, 15, 15);
        var players = new List<PlayerState>
        {
            CreatePlayer(1, "Home Poor", TeamSide.Home, new Vector2(-24f, 0f), tuning, Azimuth.East, poor),
            CreatePlayer(2, "Home Mid", TeamSide.Home, new Vector2(-35f, -15f), tuning, Azimuth.East, moderate),
            CreatePlayer(3, "Home Elite", TeamSide.Home, new Vector2(-35f, 15f), tuning, Azimuth.East, elite),
            CreatePlayer(4, "Away Poor", TeamSide.Away, new Vector2(24f, 0f), tuning, Azimuth.West, poor),
            CreatePlayer(5, "Away Mid", TeamSide.Away, new Vector2(35f, -15f), tuning, Azimuth.West, moderate),
            CreatePlayer(6, "Away Elite", TeamSide.Away, new Vector2(35f, 15f), tuning, Azimuth.West, elite)
        };
        players[0].HasPossession = true;

        var ball = new BallState
        {
            Radius = tuning.BallRadius,
            Position = players[0].Position + players[0].Facing.ToUnitVector() * tuning.DribbleLeadDistance,
            PossessorId = players[0].Id
        };

        return new MatchEngine(new MatchState(field, players, ball, activePlayerId: 1), tuning);
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
            AdvanceOneTick(input);
            _accumulatedSeconds -= Tuning.TickSeconds;
            ticks++;
        }

        return ticks;
    }

    public void StepTick(PlayerInput input)
    {
        AdvanceOneTick(input);
        _accumulatedSeconds = 0f;
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
            player.MaxSpeedPerTick = tuning.PlayerMaxSpeedPerTick * Lerp(0.82f, 1.1f, speed01);
            player.AccelerationPerTick = tuning.PlayerAccelerationPerTick;
            player.FrictionPerTick = tuning.PlayerFrictionPerTick;
        }
    }

    private void AdvanceOneTick(PlayerInput input)
    {
        PreviousSnapshot = CurrentSnapshot;
        SimulateTick(input);
        CurrentSnapshot = MatchSnapshot.FromState(State);
    }

    private void SimulateTick(PlayerInput input)
    {
        State.Tick++;
        State.ClockSeconds += Tuning.TickSeconds;

        if (State.IsBallOutOfPlay)
        {
            return;
        }

        State.LastEvent = MatchEvent.None;
        _ballCollectionCooldownTicks = Math.Max(0, _ballCollectionCooldownTicks - 1);
        _releasedByCollectionBlockTicks = Math.Max(0, _releasedByCollectionBlockTicks - 1);
        if (_releasedByCollectionBlockTicks == 0)
        {
            _releasedByPlayerId = null;
        }

        var movedPlayer = State.ActivePlayer;
        MoveActivePlayer(movedPlayer, input.NormalizedMove);
        ResolvePlayerBounds(movedPlayer);

        var actionPressed = !_previousActionDown && input.ActionDown;
        var actionReleased = _previousActionDown && !input.ActionDown;
        if (actionPressed)
        {
            _actionCommittedThisHold = false;
        }

        if (actionPressed && !movedPlayer.HasPossession && !movedPlayer.IsSliding && !movedPlayer.IsRecoveringFromSlide)
        {
            StartSlidingTackle(movedPlayer);
            _actionCommittedThisHold = true;
        }

        var releasedBallThisTick = false;
        if (input.ActionDown)
        {
            _actionHeldTicks = Math.Min(_actionHeldTicks + 1, Tuning.MaxKickHoldTicks);
        }

        if (actionReleased)
        {
            if (!_actionCommittedThisHold)
            {
                releasedBallThisTick = UseAction(movedPlayer, _actionHeldTicks);
            }

            _actionCommittedThisHold = false;
            _actionHeldTicks = 0;
        }

        _previousActionDown = input.ActionDown;

        var possessor = State.Players.FirstOrDefault(player => player.HasPossession);
        if (possessor is not null)
        {
            ControlBall(possessor);
        }
        else if (!releasedBallThisTick)
        {
            ApplyAftertouchInput(input.NormalizedMove);
            MoveLooseBall();
            TryCollectBall();
        }

        if (movedPlayer.IsSliding)
        {
            HandleSlidingBallInteraction(movedPlayer);
        }

        CheckBallRules();
    }

    public void ResumeFromRestart()
    {
        if (!State.IsBallOutOfPlay)
        {
            return;
        }

        State.IsBallOutOfPlay = false;
        State.LastEvent = MatchEvent.None;
    }

    private void MoveActivePlayer(PlayerState player, Vector2 move)
    {
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

        TryPassiveRepossession(player);
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
        var challenger = State.Players
            .Where(player => player.Team != possessor.Team)
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
            - possessorSpeed01 * 0.06f;
    }

    private void KnockBallLoose(PlayerState possessor, PlayerState challenger)
    {
        var direction = SafeNormalize(State.Ball.Position - challenger.Position, possessor.Facing.ToUnitVector());
        possessor.HasPossession = false;
        possessor.DribbleStepDistance = 0f;
        State.Ball.PossessorId = null;
        State.Ball.Velocity = direction * Tuning.PassiveContestKnockLooseSpeedPerTick + challenger.Velocity * 0.25f;
        _ballCollectionCooldownTicks = Math.Max(_ballCollectionCooldownTicks, 2);
        _aftertouchTicksRemaining = 0;
        _aftertouchPlayerId = null;
        State.Ball.CurveSpinZ = 0f;
    }

    private bool UseAction(PlayerState activePlayer, int heldTicks)
    {
        if (activePlayer.HasPossession)
        {
            if (heldTicks <= Tuning.TapPassMaxTicks)
            {
                PassBall(activePlayer);
            }
            else
            {
                KickBall(activePlayer, heldTicks);
            }

            return true;
        }

        TryTackle(activePlayer);
        return false;
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

        ReleaseBall(player, direction, Tuning.PassSpeedPerTick, lift, Tuning.PassCollectionCooldownTicks, enableAftertouch: false, kickPower01: 0f);
        player.Velocity = Vector2.Zero;
        if (target is not null)
        {
            State.ActivePlayerId = target.Id;
        }
    }

    private void KickBall(PlayerState player, int heldTicks)
    {
        var direction = player.Facing.ToUnitVector();
        var power01 = Math.Clamp((float)heldTicks / Tuning.MaxKickHoldTicks, 0.18f, 1f);
        var speed = Lerp(Tuning.MinKickSpeedPerTick, Tuning.MaxKickSpeedPerTick, power01);
        var lift01 = Math.Clamp((power01 - Tuning.KickLiftStartsAtPower) / (1f - Tuning.KickLiftStartsAtPower), 0f, 1f);
        var lift = Lerp(Tuning.MinKickLiftPerTick, Tuning.MaxKickLiftPerTick, lift01);

        ReleaseBall(player, direction, speed, lift, Tuning.KickCollectionCooldownTicks, enableAftertouch: true, power01);
    }

    private void ReleaseBall(PlayerState player, Vector2 direction, float speed, float lift, int collectionCooldownTicks, bool enableAftertouch, float kickPower01)
    {
        player.HasPossession = false;
        State.Ball.PossessorId = null;
        State.Ball.Position = player.Position + direction * (player.Radius + State.Ball.Radius + 0.2f);
        State.Ball.Velocity = direction * speed + player.Velocity * Tuning.RunnerVelocityCarryToKick;
        State.Ball.Height = 0f;
        State.Ball.VerticalVelocity = lift;
        _ballCollectionCooldownTicks = collectionCooldownTicks;
        _releasedByPlayerId = player.Id;
        _releasedByCollectionBlockTicks = Tuning.KickCollectionCooldownTicks;
        _aftertouchPlayerId = enableAftertouch ? player.Id : null;
        _aftertouchTicksRemaining = enableAftertouch ? Tuning.AftertouchWindowTicks : 0;
        State.Ball.CurveSpinZ = enableAftertouch ? Tuning.SpinInitialFromKickPower * kickPower01 : 0f;
        _aftertouchBasisForward = SafeNormalize(direction, player.Facing.ToUnitVector());
        _aftertouchBasisRight = new Vector2(-_aftertouchBasisForward.Y, _aftertouchBasisForward.X);
    }

    private PlayerState? FindPassTarget(PlayerState passer, Vector2 facing)
    {
        return State.Players
            .Where(player => player.Id != passer.Id && player.Team == passer.Team)
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
            .Where(candidate => candidate.Alignment >= Tuning.PassTargetConeCosine)
            .OrderBy(candidate => candidate.DistanceSquared)
            .FirstOrDefault()
            ?.Player;
    }

    private void TryTackle(PlayerState activePlayer)
    {
        var target = State.Players
            .Where(player => player.Id != activePlayer.Id && player.HasPossession)
            .OrderBy(player => Vector2.DistanceSquared(player.Position, activePlayer.Position))
            .FirstOrDefault();

        if (target is null)
        {
            return;
        }

        if (Vector2.Distance(target.Position, activePlayer.Position) > Tuning.TackleDistance)
        {
            return;
        }

        target.HasPossession = false;
        activePlayer.HasPossession = true;
        BeginControlledPossession(activePlayer);
    }

    private void MoveLooseBall()
    {
        if (Tuning.BallCurveMode == BallCurveMode.SpinPhysics && MathF.Abs(State.Ball.CurveSpinZ) > 0.0001f && State.Ball.Velocity.LengthSquared() > 0.0001f)
        {
            var forward = Vector2.Normalize(State.Ball.Velocity);
            var right = new Vector2(-forward.Y, forward.X);
            var speed = State.Ball.Velocity.Length();
            var magnus = right * (State.Ball.CurveSpinZ * speed * Tuning.MagnusLateralPerTick);
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

            State.Ball.CurveSpinZ *= Tuning.SpinGroundDampingPerTick;
        }

        if (State.Ball.Velocity.LengthSquared() < Tuning.MinBallSpeedPerTick * Tuning.MinBallSpeedPerTick)
        {
            State.Ball.Velocity = Vector2.Zero;
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
            .Where(player => !player.IsSliding && !player.IsRecoveringFromSlide)
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
        var signedInput = NormalizeAftertouchInput(moveLocalRight, Tuning.AftertouchInputDeadzone);
        if (signedInput == 0f && Tuning.BallCurveMode == BallCurveMode.SwosLikeLut)
        {
            _aftertouchTicksRemaining--;
            return;
        }

        if (State.Ball.Velocity.LengthSquared() > 0.0001f)
        {
            var forward = Vector2.Normalize(State.Ball.Velocity);
            var right = new Vector2(-forward.Y, forward.X);
            if (Tuning.BallCurveMode == BallCurveMode.SwosLikeLut)
            {
                var curve01 = Math.Clamp((float)_aftertouchTicksRemaining / Math.Max(1, Tuning.AftertouchWindowTicks), 0f, 1f);
                var lateral = signedInput * Tuning.SwosCurveLateralAccelPerTick * (0.45f + curve01 * 0.55f);
                State.Ball.Velocity += right * lateral;

                var side = Vector2.Dot(State.Ball.Velocity, right);
                side = Math.Clamp(side, -Tuning.SwosCurveMaxSideSpeedPerTick, Tuning.SwosCurveMaxSideSpeedPerTick);
                var forwardSpeed = Math.Max(0f, Vector2.Dot(State.Ball.Velocity, forward));
                State.Ball.Velocity = forward * forwardSpeed + right * side;
            }
            else
            {
                State.Ball.CurveSpinZ = Math.Clamp(
                    State.Ball.CurveSpinZ + signedInput * Tuning.SpinInputAddPerTick,
                    -Tuning.SpinMaxAbs,
                    Tuning.SpinMaxAbs);
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
    }

    private void HandleSlidingBallInteraction(PlayerState slider)
    {
        var slideDirection = SafeNormalize(slider.Velocity, slider.Facing.ToUnitVector());
        var hitDistance = slider.Radius + State.Ball.Radius + Tuning.SlideTackleHitDistance;

        var possessor = State.Players.FirstOrDefault(player => player.Id != slider.Id && player.HasPossession);
        if (possessor is not null)
        {
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
        _ballCollectionCooldownTicks = Math.Max(_ballCollectionCooldownTicks, 2);
    }

    private void BeginControlledPossession(PlayerState player)
    {
        var control01 = player.Attributes.BallControl01;
        var stepDistance = Lerp(Tuning.DribbleMinStepDistance, Tuning.DribbleMaxStepDistance, control01);
        var facing = player.Facing.ToUnitVector();

        foreach (var candidate in State.Players)
        {
            candidate.HasPossession = candidate.Id == player.Id;
        }

        player.DribbleStepDistance = -stepDistance * 0.5f;
        State.Ball.PossessorId = player.Id;
        State.Ball.Position = player.Position + facing * Tuning.DribbleLeadDistance;
        State.Ball.Velocity = player.Velocity * 0.85f;
        State.Ball.Height = 0f;
        State.Ball.VerticalVelocity = 0f;
        State.Ball.CurveSpinZ = 0f;
        _aftertouchTicksRemaining = 0;
        _aftertouchPlayerId = null;
        _dribbleSecureTicksRemaining = Tuning.DribbleSecureTicksAfterTouch;
        _dribbleRisk = 0f;
        _dribbleSustain01 = 0f;
        _previousDribbleMoveDirection = Vector2.Zero;
        _previousDribbleSideDistance = 0f;
        _lastDribbleTurn01 = 0f;
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
        if (State.Ball.IsPossessed)
        {
            return;
        }

        var field = State.Field;
        var ball = State.Ball;

        if (ball.Position.Y < field.Top || ball.Position.Y > field.Bottom)
        {
            StopForRestart(new MatchEvent(
                MatchEventType.TouchlineOut,
                RestartKind.ThrowIn,
                Team: null,
                Position: ClampToField(ball.Position),
                Description: "Ball crossed the touchline."));
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

                StopForRestart(new MatchEvent(
                    MatchEventType.GoalScored,
                    RestartKind.Kickoff,
                    scoringTeam,
                    Position: Vector2.Zero,
                    Description: $"{scoringTeam} scored."));
                ResetForKickoff(scoringTeam == TeamSide.Home ? TeamSide.Away : TeamSide.Home);
                return;
            }

            StopForRestart(new MatchEvent(
                MatchEventType.GoalLineOut,
                RestartKind.GoalKick,
                Team: null,
                Position: ClampToField(ball.Position),
                Description: "Ball crossed the goal line."));
        }
    }

    private void StopForRestart(MatchEvent matchEvent)
    {
        State.Ball.Position = matchEvent.Position;
        State.Ball.Velocity = Vector2.Zero;
        State.Ball.Height = 0f;
        State.Ball.VerticalVelocity = 0f;
        State.Ball.CurveSpinZ = 0f;
        State.Ball.PossessorId = null;
        foreach (var player in State.Players)
        {
            player.HasPossession = false;
        }

        State.IsBallOutOfPlay = true;
        State.LastEvent = matchEvent;
    }

    private void ResetForKickoff(TeamSide kickoffTeam)
    {
        var player = State.Players.First(candidate => candidate.Team == kickoffTeam);
        State.ActivePlayerId = player.Id;
        player.Position = kickoffTeam == TeamSide.Home ? new Vector2(-4f, 0f) : new Vector2(4f, 0f);
        player.Velocity = Vector2.Zero;
        player.Facing = FacingForTeam(kickoffTeam);
        player.HasPossession = true;

        State.Ball.Position = Vector2.Zero;
        State.Ball.Velocity = Vector2.Zero;
        State.Ball.Height = 0f;
        State.Ball.VerticalVelocity = 0f;
        State.Ball.CurveSpinZ = 0f;
        State.Ball.PossessorId = player.Id;
        _aftertouchTicksRemaining = 0;
        _aftertouchPlayerId = null;
        _dribbleSecureTicksRemaining = Tuning.DribbleSecureTicksAfterTouch;
        _dribbleRisk = 0f;
        _dribbleSustain01 = 0f;
        _previousDribbleMoveDirection = Vector2.Zero;
        _previousDribbleSideDistance = 0f;
        _lastDribbleTurn01 = 0f;
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
        TeamSide team,
        Vector2 position,
        PhysicsTuning tuning,
        Azimuth facing,
        PlayerAttributes attributes)
    {
        var speed01 = attributes.Speed01;
        return new PlayerState(id, name, team, position)
        {
            Facing = facing,
            Attributes = attributes,
            Radius = tuning.PlayerRadius,
            MaxSpeedPerTick = tuning.PlayerMaxSpeedPerTick * Lerp(0.82f, 1.1f, speed01),
            AccelerationPerTick = tuning.PlayerAccelerationPerTick,
            FrictionPerTick = tuning.PlayerFrictionPerTick
        };
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
}
