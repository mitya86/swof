namespace Swof.Engine;

public sealed record PhysicsTuning
{
    public const int CurrentProfileVersion = 2;

    public static PhysicsTuning Default { get; } = new();

    /// <summary>Identifies the compatible persisted tuning contract.</summary>
    public int ProfileVersion { get; init; } = CurrentProfileVersion;
    public int TickRate { get; init; } = 60;
    /// <summary>Readable arcade-futsal running pace in metres per simulation tick.</summary>
    public float PlayerMaxSpeedPerTick { get; init; } = 14f / 60f;
    public float PlayerAccelerationPerTick { get; init; } = 70f / (60f * 60f);
    public float PlayerFrictionPerTick { get; init; } = 9f / (60f * 60f);
    public float PlayerBurstAccelerationPerTick { get; init; } = 0.055f;
    public float PlayerSustainAccelerationPerTick { get; init; } = 0.014f;
    /// <summary>Maximum velocity change while steering across the current run direction.</summary>
    public float PlayerTurnAccelerationPerTick { get; init; } = 0.07f;
    /// <summary>Maximum velocity change when reversing against the current run direction.</summary>
    public float PlayerReverseAccelerationPerTick { get; init; } = 0.085f;
    public float PlayerDecelerationPerTick { get; init; } = 0.09f;
    public float PlayerRadius { get; init; } = 1.05f;
    /// <summary>Maximum goalkeeper travel speed while the AI guards its goal.</summary>
    public float GoalkeeperMaxSpeedPerTick { get; init; } = 12f / 60f;
    /// <summary>Maximum goalkeeper velocity change per simulation tick.</summary>
    public float GoalkeeperAccelerationPerTick { get; init; } = 75f / (60f * 60f);
    /// <summary>Resting distance between a goalkeeper and the defended goal line.</summary>
    public float GoalkeeperGoalLineOffset { get; init; } = 1.4f;
    /// <summary>Furthest distance a goalkeeper AI may advance from its goal line.</summary>
    public float GoalkeeperMaxAdvanceDistance { get; init; } = 10f;
    /// <summary>Extra lateral tracking range beyond each goal post.</summary>
    public float GoalkeeperLateralPadding { get; init; } = 5f;
    /// <summary>Additional planar reach used for deterministic catches and saves.</summary>
    public float GoalkeeperCatchReach { get; init; } = 0.9f;
    /// <summary>Highest ball a standing or diving goalkeeper may catch.</summary>
    public float GoalkeeperCatchMaxHeight { get; init; } = 2.25f;
    /// <summary>Rendered ball height while held by a goalkeeper.</summary>
    public float GoalkeeperHeldBallHeight { get; init; } = 1.15f;
    /// <summary>Distance that a held ball sits in front of the goalkeeper.</summary>
    public float GoalkeeperHeldBallForwardOffset { get; init; } = 0.35f;
    /// <summary>Delay before a computer-controlled goalkeeper distributes the ball.</summary>
    public int GoalkeeperAiDistributionDelayTicks { get; init; } = 45;
    /// <summary>Safety timeout before a user-controlled goalkeeper auto-distributes.</summary>
    public int GoalkeeperUserDistributionTimeoutTicks { get; init; } = 240;
    /// <summary>Pause between the ball leaving play and the awarded restart becoming live.</summary>
    public int RestartDelayTicks { get; init; } = 60;
    /// <summary>Distance opponents are moved back from a kick-in or corner (5 m in the futsal laws).</summary>
    public float RestartOpponentDistance { get; init; } = 5f;
    public float BallRadius { get; init; } = 0.42f;
    public float BallFrictionPerTick { get; init; } = 5.2f / (60f * 60f);
    public float MinBallSpeedPerTick { get; init; } = 0.03f / 60f;
    public float BallGravityPerTick { get; init; } = 46f / (60f * 60f);
    public float BallBounceEnergyRetention { get; init; } = 0.42f;
    public float BallMinBounceVelocityPerTick { get; init; } = 0.035f;
    public float PossessionDistance { get; init; } = 1.5f;
    public float DribbleLeadDistance { get; init; } = 1.15f;
    public float DribbleMinLeadDistance { get; init; } = 0.7f;
    public float DribbleMaxLeadDistance { get; init; } = 1.1f;
    public float DribbleMinStepDistance { get; init; } = 0.85f;
    public float DribbleMaxStepDistance { get; init; } = 1.35f;
    public float DribbleTouchImpulsePerTick { get; init; } = 0.018f;
    public float DribbleControlFrictionPerTick { get; init; } = 10f / (60f * 60f);
    public float DribbleLowControlSpeedLead { get; init; } = 1.2f;
    public float DribbleHighControlSpeedLead { get; init; } = 0.45f;
    public float DribbleLowControlMaxDistance { get; init; } = 1.02f;
    public float DribbleHighControlMaxDistance { get; init; } = 1.62f;
    public float DribbleLowControlTurnPenalty { get; init; } = 1.2f;
    public float DribbleHighControlTurnPenalty { get; init; } = 0.35f;
    public int DribbleSecureTicksAfterTouch { get; init; } = 3;
    public float DribbleRiskDecayPerTick { get; init; } = 0.94f;
    public float DribbleRiskTurnGainPerTick { get; init; } = 0.18f;
    public float DribbleRiskDistanceGainPerTick { get; init; } = 0.24f;
    public float DribbleSustainRampPerTick { get; init; } = 0.01f;
    public float DribbleSustainMaxMultiplier { get; init; } = 2.2f;
    public float DribbleRiskBreakThreshold { get; init; } = 0.34f;
    public float DribbleMistouchLooseSpeedPerTick { get; init; } = 0.3f;
    public int DribbleMistouchCollectionCooldownTicks { get; init; } = 8;
    public float MovingFrontAlignPositionPerTick { get; init; } = 0.045f;
    public float MovingFrontAlignVelocityPerTick { get; init; } = 0.03f;
    public float MovingFrontAlignMinForwardDistance { get; init; } = 0.72f;
    public float StationaryControlSpeedThresholdPerTick { get; init; } = 0.02f;
    public float StationaryBallSettlePositionPerTick { get; init; } = 0.12f;
    public float StationaryBallSettleVelocityPerTick { get; init; } = 0.07f;
    public float StationaryHighControlLeadExtra { get; init; } = 0.04f;
    public float StationaryLowControlLeadExtra { get; init; } = 0.16f;
    public float StationaryPossessionClampFactor { get; init; } = 0.92f;
    public float PassiveContestDistance { get; init; } = 0.72f;
    public float PassiveContestCleanWinMargin { get; init; } = 0.1f;
    public float PassiveContestKnockLooseSpeedPerTick { get; init; } = 0.22f;
    /// <summary>Brief travel window for a compact futsal tackle.</summary>
    public int SlideTravelTicks { get; init; } = 9;
    /// <summary>Short recovery window before the tackler can move again.</summary>
    public int SlideRecoveryTicks { get; init; } = 15;
    public float SlideSpeedPerTick { get; init; } = 22f / 60f;
    public float SlideTackleHitDistance { get; init; } = 0.6f;
    public float SlideLooseBallDeflectSpeedPerTick { get; init; } = 18f / 60f;
    public float TackleDistance { get; init; } = 2.2f;
    public int TapPassMaxTicks { get; init; } = 7;
    /// <summary>Ground-pass pace chosen to keep short futsal combinations readable.</summary>
    public float PassSpeedPerTick { get; init; } = 20f / 60f;
    public float PassLiftPerTick { get; init; } = 0.12f;
    public float LongPassDistance { get; init; } = 16f;
    /// <summary>Minimum facing alignment for a teammate to receive pass assistance.</summary>
    public float PassTargetConeCosine { get; init; } = MathF.Cos(MathF.PI / 8f);
    /// <summary>Furthest teammate distance eligible for automatic pass targeting.</summary>
    public float PassTargetMaxDistance { get; init; } = 32f;
    public int MaxKickHoldTicks { get; init; } = 45;
    public int KickAnimationTicks { get; init; } = 8;
    public float ImmediateKickPower01 { get; init; } = 0.7f;
    public float MinKickSpeedPerTick { get; init; } = 18f / 60f;
    public float MaxKickSpeedPerTick { get; init; } = 42f / 60f;
    public float MinKickLiftPerTick { get; init; } = 0.03f;
    public float MaxKickLiftPerTick { get; init; } = 0.32f;
    public float KickLiftStartsAtPower { get; init; } = 0.35f;
    public int PassCollectionCooldownTicks { get; init; } = 3;
    public int KickCollectionCooldownTicks { get; init; } = 11;
    public float RunnerVelocityCarryToKick { get; init; } = 0.25f;
    /// <summary>Maximum distance from goal where digital goal-lane aiming is available.</summary>
    public float ShotGoalAimMaxDistance { get; init; } = 30f;
    /// <summary>Minimum shot-facing alignment with goal center before goal-lane aiming applies.</summary>
    public float ShotGoalAimMinAlignmentCosine { get; init; } = 0.64f;
    /// <summary>Distance kept between an assisted shot target and either goal post.</summary>
    public float ShotGoalAimPostMargin { get; init; } = 0.55f;
    public BallCurveMode BallCurveMode { get; init; } = BallCurveMode.SpinPhysics;
    public int AftertouchWindowTicks { get; init; } = 12;
    public int AftertouchHeightInputTicks { get; init; } = 6;
    public float AftertouchInputDeadzone { get; init; } = 0.18f;
    public float SwosCurveSpinImpulse { get; init; } = 2.1f;
    public float SwosCurveMagnusMultiplier { get; init; } = 0.85f;
    public float AftertouchDriveVerticalImpulse { get; init; } = 0.11f;
    public float AftertouchLoftVerticalImpulse { get; init; } = 0.1f;
    public float AftertouchMaxVerticalVelocityPerTick { get; init; } = 0.46f;
    public float AftertouchSpinImpulse { get; init; } = 2.4f;
    public float SpinMaxAbs { get; init; } = 3.1f;
    public float SpinDecayPerTick { get; init; } = 0.992f;
    public float SpinGroundDampingPerTick { get; init; } = 0.72f;
    public float MaxGroundSpinRetentionPerTick { get; init; } = 0.72f;
    public float GroundMagnusFactor { get; init; } = 0.08f;
    public float MagnusLateralPerTick { get; init; } = 0.0055f;

    public float TickSeconds => 1f / TickRate;
}
