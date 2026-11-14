namespace Swof.Engine.AI;

/// <summary>
/// The only source of chance in a match: a number worked out from the match seed, the tick and
/// the player, so the same seed and inputs always replay the same match on any machine.
/// Computer decisions use it; physics never does.
/// </summary>
internal static class DecisionNoise
{
    /// <summary>
    /// Returns a value from 0 up to 1 that stays the same for one player and one purpose
    /// for <paramref name="holdTicks"/> ticks at a time.
    /// </summary>
    public static float Roll(MatchSnapshot match, int playerId, int purpose, int holdTicks = 1)
    {
        unchecked
        {
            var hash = Mix((uint)match.Seed * 0x9E3779B1u ^ (uint)(match.Tick / Math.Max(1, holdTicks)));
            hash = Mix(hash ^ ((uint)playerId * 0x85EBCA77u));
            hash = Mix(hash ^ ((uint)purpose * 0xC2B2AE3Du));
            return (hash >> 8) / 16777216f;
        }
    }

    private static uint Mix(uint hash)
    {
        unchecked
        {
            hash ^= hash >> 16;
            hash *= 0x7FEB352Du;
            hash ^= hash >> 15;
            hash *= 0x846CA68Bu;
            hash ^= hash >> 16;
            return hash;
        }
    }
}
