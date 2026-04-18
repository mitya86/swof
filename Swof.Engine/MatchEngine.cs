using System.Numerics;

namespace Swof.Engine;

public sealed class MatchEngine
{
    private bool _previousActionDown;
    private int _actionHeldTicks;
    private int _ballCollectionCooldownTicks;
    private float _accumulatedSeconds;

    public MatchEngine(MatchState state, PhysicsTuning? tuning = null)
    {
        State = state;
        Tuning = tuning ?? PhysicsTuning.Default;
    }

    public MatchState State { get; }
    public PhysicsTuning Tuning { get; }

    public static MatchEngine CreateDefault(PhysicsTuning? tuning = null)
    {
        tuning ??= PhysicsTuning.Default;
        var field = FieldDimensions.Default;
        var players = new List<PlayerState>
        {
            CreatePlayer(1, TeamSide.Home, new Vector2(-24f, 0f), tuning),
            CreatePlayer(2, TeamSide.Home, new Vector2(-35f, -15f), tuning),
            CreatePlayer(3, TeamSide.Home, new Vector2(-35f, 15f), tuning),
            CreatePlayer(4, TeamSide.Away, new Vector2(24f, 0f), tuning),
            CreatePlayer(5, TeamSide.Away, new Vector2(35f, -15f), tuning),
            CreatePlayer(6, TeamSide.Away, new Vector2(35f, 15f), tuning)
        };
        players[0].HasPossession = true;

        var ball = new BallState
        {
            Radius = tuning.BallRadius,
            Position = players[0].Position + new Vector2(tuning.DribbleLeadDistance, 0f),
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
            StepTick(input);
            _accumulatedSeconds -= Tuning.TickSeconds;
            ticks++;
        }

        return ticks;
    }

    public void StepTick(PlayerInput input)
    {
        State.Tick++;
        State.ClockSeconds += Tuning.TickSeconds;

        if (State.IsBallOutOfPlay)
        {
            return;
        }

        State.LastEvent = MatchEvent.None;
        _ballCollectionCooldownTicks = Math.Max(0, _ballCollectionCooldownTicks - 1);

        var activePlayer = State.ActivePlayer;
        MoveActivePlayer(activePlayer, input.NormalizedMove);

        var actionReleased = _previousActionDown && !input.ActionDown;
        if (input.ActionDown)
        {
            _actionHeldTicks++;
        }

        if (actionReleased)
        {
            UseAction(activePlayer, input.NormalizedMove, _actionHeldTicks);
            _actionHeldTicks = 0;
        }

        _previousActionDown = input.ActionDown;

        if (activePlayer.HasPossession)
        {
            CarryBall(activePlayer, input.NormalizedMove);
        }
        else
        {
            MoveLooseBall();
            TryCollectBall(activePlayer);
        }

        ResolvePlayerBounds(activePlayer);
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
        if (move == Vector2.Zero)
        {
            player.Velocity = Approach(player.Velocity, Vector2.Zero, player.FrictionPerTick);
        }
        else
        {
            player.Velocity += move * player.AccelerationPerTick;
            player.Velocity = ClampLength(player.Velocity, player.MaxSpeedPerTick);
        }

        player.Position += player.Velocity;
    }

    private void CarryBall(PlayerState player, Vector2 inputDirection)
    {
        var facing = inputDirection == Vector2.Zero
            ? DirectionForTeam(player.Team)
            : inputDirection;

        State.Ball.Position = player.Position + facing * Tuning.DribbleLeadDistance;
        State.Ball.Velocity = player.Velocity;
        State.Ball.PossessorId = player.Id;
    }

    private void UseAction(PlayerState activePlayer, Vector2 inputDirection, int heldTicks)
    {
        if (activePlayer.HasPossession)
        {
            KickBall(activePlayer, inputDirection, heldTicks);
            return;
        }

        TryTackle(activePlayer);
    }

    private void KickBall(PlayerState player, Vector2 inputDirection, int heldTicks)
    {
        var direction = inputDirection == Vector2.Zero
            ? DirectionForTeam(player.Team)
            : inputDirection;

        var power01 = Math.Clamp((float)heldTicks / Tuning.MaxKickHoldTicks, 0.18f, 1f);
        var speed = Lerp(Tuning.MinKickSpeedPerTick, Tuning.MaxKickSpeedPerTick, power01);

        player.HasPossession = false;
        State.Ball.PossessorId = null;
        State.Ball.Position = player.Position + direction * (player.Radius + State.Ball.Radius + 0.2f);
        State.Ball.Velocity = direction * speed + player.Velocity * Tuning.RunnerVelocityCarryToKick;
        _ballCollectionCooldownTicks = Tuning.KickCollectionCooldownTicks;
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
        State.Ball.PossessorId = activePlayer.Id;
    }

    private void MoveLooseBall()
    {
        State.Ball.Position += State.Ball.Velocity;
        State.Ball.Velocity = Approach(State.Ball.Velocity, Vector2.Zero, Tuning.BallFrictionPerTick);

        if (State.Ball.Velocity.LengthSquared() < Tuning.MinBallSpeedPerTick * Tuning.MinBallSpeedPerTick)
        {
            State.Ball.Velocity = Vector2.Zero;
        }
    }

    private void TryCollectBall(PlayerState activePlayer)
    {
        if (State.Ball.IsPossessed)
        {
            return;
        }

        if (_ballCollectionCooldownTicks > 0)
        {
            return;
        }

        var distance = Vector2.Distance(activePlayer.Position, State.Ball.Position);
        if (distance > activePlayer.Radius + State.Ball.Radius + Tuning.PossessionDistance)
        {
            return;
        }

        activePlayer.HasPossession = true;
        State.Ball.PossessorId = activePlayer.Id;
        State.Ball.Velocity = Vector2.Zero;
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
        player.HasPossession = true;

        State.Ball.Position = Vector2.Zero;
        State.Ball.Velocity = Vector2.Zero;
        State.Ball.PossessorId = player.Id;
    }

    private Vector2 ClampToField(Vector2 position)
    {
        var field = State.Field;
        return new Vector2(
            Math.Clamp(position.X, field.Left, field.Right),
            Math.Clamp(position.Y, field.Top, field.Bottom));
    }

    private static Vector2 DirectionForTeam(TeamSide team)
    {
        return team == TeamSide.Home ? Vector2.UnitX : -Vector2.UnitX;
    }

    private static PlayerState CreatePlayer(int id, TeamSide team, Vector2 position, PhysicsTuning tuning)
    {
        return new PlayerState(id, team, position)
        {
            Radius = tuning.PlayerRadius,
            MaxSpeedPerTick = tuning.PlayerMaxSpeedPerTick,
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

    private static float Lerp(float from, float to, float amount)
    {
        return from + (to - from) * amount;
    }
}
