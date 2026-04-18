namespace Swof.Engine;

public sealed record PhysicsTuning
{
    public static PhysicsTuning Default { get; } = new();

    public int TickRate { get; init; } = 60;
    public float PlayerMaxSpeedPerTick { get; init; } = 18f / 60f;
    public float PlayerAccelerationPerTick { get; init; } = 70f / (60f * 60f);
    public float PlayerFrictionPerTick { get; init; } = 9f / (60f * 60f);
    public float PlayerRadius { get; init; } = 1.05f;
    public float BallRadius { get; init; } = 0.42f;
    public float BallFrictionPerTick { get; init; } = 5.2f / (60f * 60f);
    public float MinBallSpeedPerTick { get; init; } = 0.03f / 60f;
    public float PossessionDistance { get; init; } = 1.5f;
    public float DribbleLeadDistance { get; init; } = 1.15f;
    public float TackleDistance { get; init; } = 2.2f;
    public int MaxKickHoldTicks { get; init; } = 45;
    public float MinKickSpeedPerTick { get; init; } = 18f / 60f;
    public float MaxKickSpeedPerTick { get; init; } = 42f / 60f;
    public int KickCollectionCooldownTicks { get; init; } = 11;
    public float RunnerVelocityCarryToKick { get; init; } = 0.25f;

    public float TickSeconds => 1f / TickRate;
}
