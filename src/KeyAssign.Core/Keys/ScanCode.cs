namespace KeyAssign.Core.Keys;

/// <summary>
/// A keyboard scan code (set 1, the codes Windows reports). Extended keys carry
/// the <c>E0</c> prefix in the high byte, so Left Windows is <c>0xE05B</c>.
/// </summary>
public readonly record struct ScanCode(ushort Value)
{
    /// <summary>Mapping a key to this code disables it.</summary>
    public static readonly ScanCode None = new(0x0000);

    public static readonly ScanCode Escape = new(0x0001);
    public static readonly ScanCode Tab = new(0x000F);
    public static readonly ScanCode LeftControl = new(0x001D);
    public static readonly ScanCode LeftShift = new(0x002A);
    public static readonly ScanCode LeftAlt = new(0x0038);
    public static readonly ScanCode CapsLock = new(0x003A);
    public static readonly ScanCode RightControl = new(0xE01D);
    public static readonly ScanCode RightAlt = new(0xE038);
    public static readonly ScanCode LeftWindows = new(0xE05B);
    public static readonly ScanCode RightWindows = new(0xE05C);
    public static readonly ScanCode Menu = new(0xE05D);

    public bool IsExtended => (Value & 0xFF00) == 0xE000;

    public override string ToString() => $"0x{Value:X4}";
}
