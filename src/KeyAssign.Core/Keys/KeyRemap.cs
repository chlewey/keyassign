namespace KeyAssign.Core.Keys;

/// <summary>
/// One key-to-key assignment: pressing <see cref="From"/> sends <see cref="To"/>.
/// A <see cref="To"/> of <see cref="ScanCode.None"/> disables the key.
/// </summary>
public readonly record struct KeyRemap(ScanCode From, ScanCode To)
{
    public bool DisablesKey => To == ScanCode.None;
}
