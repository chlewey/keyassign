namespace S1.BuildDll;

// The tables of a keyboard layout DLL, as kbd.h defines them (x64 layout), kept as plain data.
// Reader.cs fills them from a loaded DLL; DllWriter.cs turns them into a DLL.

internal sealed record VkRow(byte Vk, byte Attributes, ushort[] Chars);

internal sealed record VkTable(byte Columns, List<VkRow> Rows);

internal sealed record DeadRow(char Base, char Accent, char Composed, ushort Flags);

internal sealed record LigatureRow(byte Vk, ushort Column, ushort[] Chars);

internal sealed record NamedKey(byte Scan, string Name);

internal sealed class RawTables
{
    public const byte VkShift = 0x10, VkControl = 0x11, VkMenu = 0x12;
    public const ushort WchNone = 0xF000, WchDead = 0xF001, WchLgtr = 0xF002;
    public const byte CapLok = 0x01, SgCaps = 0x02, CapLokAltGr = 0x04;
    public const byte ShftInvalid = 0x0F;
    public const uint KbdVersion1 = 1;
    public const uint KllfAltGr = 1;

    public List<(byte Vk, byte Bits)> VkToBit { get; set; } = new();

    /// <summary>Modifier bits (index) to column (value), <see cref="ShftInvalid"/> for none.</summary>
    public byte[] ModNumber { get; set; } = Array.Empty<byte>();

    public List<VkTable> VkToWchar { get; set; } = new();

    public List<DeadRow> Dead { get; set; } = new();

    public List<NamedKey> KeyNames { get; set; } = new();

    public List<NamedKey> KeyNamesExt { get; set; } = new();

    public List<string> KeyNamesDead { get; set; } = new();

    /// <summary>Scan code (index) to virtual key plus KBD* flags.</summary>
    public ushort[] ScanToVk { get; set; } = Array.Empty<ushort>();

    public List<(byte Scan, ushort Vk)> ScanToVkE0 { get; set; } = new();

    public List<(byte Scan, ushort Vk)> ScanToVkE1 { get; set; } = new();

    public uint LocaleFlags { get; set; }

    public byte LigatureMax { get; set; }

    public List<LigatureRow> Ligatures { get; set; } = new();

    public uint KeyboardType { get; set; } = 4;

    public uint KeyboardSubType { get; set; }
}
