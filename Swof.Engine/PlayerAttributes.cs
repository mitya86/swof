namespace Swof.Engine;

public sealed record PlayerAttributes(
    byte Passing,
    byte Shooting,
    byte Heading,
    byte Tackling,
    byte BallControl,
    byte Speed,
    byte Finishing)
{
    public static PlayerAttributes DefaultOutfield { get; } = new(
        Passing: 8,
        Shooting: 8,
        Heading: 8,
        Tackling: 8,
        BallControl: 8,
        Speed: 8,
        Finishing: 8);

    public int EffectivePassing => Effective(Passing);
    public int EffectiveShooting => Effective(Shooting);
    public int EffectiveHeading => Effective(Heading);
    public int EffectiveTackling => Effective(Tackling);
    public int EffectiveBallControl => Effective(BallControl);
    public int EffectiveSpeed => Effective(Speed);
    public int EffectiveFinishing => Effective(Finishing);

    public float BallControl01 => EffectiveBallControl / 7f;
    public float Speed01 => EffectiveSpeed / 7f;

    public static int Effective(byte value)
    {
        return Math.Clamp(value, (byte)0, (byte)15) % 8;
    }
}
