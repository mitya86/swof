using System.Numerics;

namespace Swof.Engine;

public sealed class MatchEngine
{
    private bool _previousActionDown;
    private int _actionHeldTicks;
    private int _ballCollectionCooldownTicks;
    private int? _releasedByPlayerId;
    private int _releasedByCollectionBlockTicks;
    private float _accumulatedSeconds;

    public MatchEngine(MatchState state, PhysicsTuning? tuning = null)
    {
        State = state;
        Tuning = tuning ?? PhysicsTuning.Default;
        CurrentSnapshot = MatchSnapshot.FromState(State);
        PreviousSnapshot = CurrentSnapshot;
    }

    public MatchState State { get; }
    public PhysicsTuning Tuning { get; }
    public MatchSnapshot PreviousSnapshot { get; private set; }
    public MatchSnapshot CurrentSnapshot { get; private set; }
    public float RenderAlpha => Math.Clamp(_accumulatedSeconds / Tuning.TickSeconds, 0f, 1f);

    public static MatchEngine CreateDefault(PhysicsTuning? tuning = null)
    {
        tuning ??= PhysicsTuning.Default;
        var field = FieldDimensions.Default;
        var players = new List<PlayerState>
        {
            CreatePlayer(1, TeamSide.Home, new Vector2(-24f, 0f), tuning, Azimuth.East, new PlayerAttributes(12, 10, 8, 8, 15, 13, 10)),
            CreatePlayer(2, TeamSide.Home, new Vector2(-35f, -15f), tuning, Azimuth.East, new PlayerAttributes(11, 8, 8, 9, 10, 11, 8)),
            CreatePlayer(3, TeamSide.Home, new Vector2(-35f, 15f), tuning, Azimuth.East, new PlayerAttributes(9, 9, 10, 11, 8, 9, 9)),
            CreatePlayer(4, TeamSide.Away, new Vector2(24f, 0f), tuning, Azimuth.West, new PlayerAttributes(12, 10, 8, 8, 15, 13, 10)),
            CreatePlayer(5, TeamSide.Away, new Vector2(35f, -15f), tuning, Azimuth.West, new PlayerAttributes(9, 8, 10, 12, 8, 9, 8)),
            CreatePlayer(6, TeamSide.Away, new Vector2(35f, 15f), tuning, Azimuth.West, new PlayerAttributes(10, 11, 8, 8, 11, 12, 12))
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

        var actionPressed = !_previousActionDown && input.ActionDown;
        var actionReleased = _previousActionDown && !input.ActionDown;
        if (actionPressed && !movedPlayer.HasPossession && !movedPlayer.IsSliding && !movedPlayer.IsRecoveringFromSlide)
        {
            StartSlidingTackle(movedPlayer);
        }

        if (input.ActionDown)
        {
            _actionHeldTicks++;
        }

        var releasedBallThisTick = false;
        if (actionReleased)
        {
            releasedBallThisTick = UseAction(movedPlayer, _actionHeldTicks);
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
            MoveLooseBall();
            TryCollectBall();
        }

        if (movedPlayer.IsSliding)
        {
            HandleSlidingBallInteraction(movedPlayer);
        }

        ResolvePlayerBounds(movedPlayer);
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

        if (move == Vector2.Zero)
        {
            player.Velocity = Approach(player.Velocity, Vector2.Zero, Tuning.PlayerDecelerationPerTick);
        }
        else
        {
            player.Facing = Azimuth.FromVector(move).Quantized(8);
            var desiredVelocity = move * player.MaxSpeedPerTick;
            var speed01 = Math.Clamp(player.Velocity.Length() / MathF.Max(0.0001f, player.MaxSpeedPerTick), 0f, 1f);
            var acceleration = Lerp(Tuning.PlayerBurstAccelerationPerTick, Tuning.PlayerSustainAccelerationPerTick, speed01);
            player.Velocity = Approach(player.Velocity, desiredVelocity, acceleration);
        }

        player.Position += player.Velocity;
    }

    private void ControlBall(PlayerState player)
    {
        MoveControlledBall(player);
        SettleControlledBallWhenStationary(player);
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
        player.HasPossession = false;
        player.DribbleStepDistance = 0f;
        State.Ball.PossessorId = null;
        _ballCollectionCooldownTicks = Math.Max(_ballCollectionCooldownTicks, 3);
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

        ReleaseBall(player, direction, Tuning.PassSpeedPerTick, lift, Tuning.PassCollectionCooldownTicks);
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

        ReleaseBall(player, direction, speed, lift, Tuning.KickCollectionCooldownTicks);
    }

    private void ReleaseBall(PlayerState player, Vector2 direction, float speed, float lift, int collectionCooldownTicks)
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
        State.Ball.Position += State.Ball.Velocity;
        State.Ball.Velocity = Approach(State.Ball.Velocity, Vector2.Zero, Tuning.BallFrictionPerTick);
        State.Ball.Height += State.Ball.VerticalVelocity;
        State.Ball.VerticalVelocity -= Tuning.BallGravityPerTick;

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
    }

    private void ResolvePlayerBounds(PlayerState player)
    {
        var field = State.Field;
        player.Position = new Vector2(
            Math.Clamp(player.Position.X, field.Left + player.Radius, field.Right - player.Radius),
            Math.Clamp(player.Position.Y, field.Top + player.Radius, field.Bottom - player.Radius));
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
        State.Ball.PossessorId = player.Id;
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
        TeamSide team,
        Vector2 position,
        PhysicsTuning tuning,
        Azimuth facing,
        PlayerAttributes attributes)
    {
        var speed01 = attributes.Speed01;
        return new PlayerState(id, team, position)
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
}
