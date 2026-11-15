namespace KeyAssign.Core.Layouts;

/// <summary>What Caps Lock does to one key. Set key by key, never layout-wide.</summary>
public enum CapsBehavior
{
    /// <summary>Caps Lock has no effect, as for digits: <c>[5]</c> types 5 and Shift+<c>[5]</c> types %.</summary>
    Ignored,

    /// <summary>
    /// Caps Lock swaps the Normal and Shift outputs, as for letters: with Caps Lock,
    /// <c>[A]</c> types A and Shift+<c>[A]</c> types a.
    /// </summary>
    InvertsShift,

    /// <summary>Like <see cref="InvertsShift"/>, and the same swap between AltGr and AltGr+Shift.</summary>
    InvertsShiftAndAltGr,
}
