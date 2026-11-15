using KeyAssign.Core.Keys;
using System.Globalization;

namespace KeyAssign.Core.Layouts;

/// <summary>One physical key, by scan code, and what it types at each level.</summary>
public sealed record KeyBinding(
    ScanCode Code,
    KeyOutput Normal,
    KeyOutput Shift,
    KeyOutput AltGr,
    KeyOutput AltGrShift,
    CapsBehavior Caps)
{
    public KeyOutput this[Level level] => level switch
    {
        Level.Normal => Normal,
        Level.Shift => Shift,
        Level.AltGr => AltGr,
        Level.AltGrShift => AltGrShift,
        _ => throw new ArgumentOutOfRangeException(nameof(level)),
    };

    public KeyBinding With(Level level, KeyOutput output) => level switch
    {
        Level.Normal => this with { Normal = output },
        Level.Shift => this with { Shift = output },
        Level.AltGr => this with { AltGr = output },
        Level.AltGrShift => this with { AltGrShift = output },
        _ => throw new ArgumentOutOfRangeException(nameof(level)),
    };

    /// <summary>
    /// The Caps Lock behaviour a key gets when none is given: letters (Normal is the
    /// lowercase of Shift) invert Shift; everything else ignores Caps Lock.
    /// </summary>
    public static CapsBehavior DefaultCaps(KeyOutput normal, KeyOutput shift)
    {
        ArgumentNullException.ThrowIfNull(normal);
        ArgumentNullException.ThrowIfNull(shift);

        var isLetterPair = normal.Kind == OutputKind.Text
            && shift.Kind == OutputKind.Text
            && normal.Text != shift.Text
            && normal.Text.ToUpper(CultureInfo.InvariantCulture) == shift.Text;

        return isLetterPair ? CapsBehavior.InvertsShift : CapsBehavior.Ignored;
    }
}
