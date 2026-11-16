using Microsoft.Xna.Framework.Input;
using NumericsVector2 = System.Numerics.Vector2;

namespace Swof.Game;

/// <summary>
/// One key or one touch held between two times after launch, written <c>from-to:what</c>:
/// <c>0.5-0.6:P</c> holds the P key, <c>1-1.5:0.9x0.1</c> holds a touch nine tenths across and one tenth down the screen.
/// Touches that end in the same <c>#number</c> are one finger, so <c>1-1.1:0.2x0.5#1,1.1-2:0.3x0.5#1</c> is a drag.
/// </summary>
internal sealed record ScriptedInput(double FromSeconds, double ToSeconds, Keys? Key, NumericsVector2? Touch, int Finger = 0)
{
    public bool IsHeldAt(double seconds) => seconds >= FromSeconds && seconds < ToSeconds;

    public static ScriptedInput Parse(string item)
    {
        var parts = item.Split(':');
        var times = parts[0].Split('-');
        if (parts.Length != 2 || times.Length != 2)
        {
            throw new ArgumentException($"Scripted input '{item}' is not from-to:what.", nameof(item));
        }

        var from = SwofGameLaunchOptions.ParseSeconds(times[0]);
        var to = SwofGameLaunchOptions.ParseSeconds(times[1]);
        var finger = parts[1].Split('#');
        var place = finger[0].Split('x');
        if (place.Length == 2 && char.IsDigit(parts[1][0]))
        {
            return new ScriptedInput(
                from,
                to,
                Key: null,
                new NumericsVector2((float)SwofGameLaunchOptions.ParseSeconds(place[0]), (float)SwofGameLaunchOptions.ParseSeconds(place[1])),
                finger.Length == 2 ? int.Parse(finger[1], System.Globalization.CultureInfo.InvariantCulture) : 0);
        }

        return Enum.TryParse<Keys>(parts[1], ignoreCase: true, out var key) && Enum.IsDefined(key)
            ? new ScriptedInput(from, to, key, Touch: null)
            : throw new ArgumentException($"Scripted input '{item}' names no key and no place on the screen.", nameof(item));
    }
}
