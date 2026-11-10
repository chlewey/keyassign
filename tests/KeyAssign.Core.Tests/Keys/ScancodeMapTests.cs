using KeyAssign.Core.Keys;

namespace KeyAssign.Core.Tests.Keys;

public class ScancodeMapTests
{
    [Fact]
    public void Encode_CapsLockToLeftControl_MatchesKnownRegistryValue()
    {
        // The widely documented value for "Caps Lock acts as Left Ctrl".
        byte[] expected =
        [
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x02, 0x00, 0x00, 0x00,
            0x1D, 0x00, 0x3A, 0x00,
            0x00, 0x00, 0x00, 0x00,
        ];

        var bytes = ScancodeMap.Encode([new KeyRemap(ScanCode.CapsLock, ScanCode.LeftControl)]);

        Assert.Equal(expected, bytes);
    }

    [Fact]
    public void Encode_ExtendedKeyDisabled_WritesPrefixAndZeroTarget()
    {
        var bytes = ScancodeMap.Encode([new KeyRemap(ScanCode.LeftWindows, ScanCode.None)]);

        Assert.Equal(new byte[] { 0x00, 0x00, 0x5B, 0xE0 }, bytes[12..16]);
    }

    [Fact]
    public void Encode_NoRemaps_WritesTerminatorOnly()
    {
        var bytes = ScancodeMap.Encode([]);

        Assert.Equal(16, bytes.Length);
        Assert.Equal(1, BitConverter.ToInt32(bytes, 8));
    }

    [Fact]
    public void Decode_RoundTripsEncode()
    {
        KeyRemap[] remaps =
        [
            new(ScanCode.CapsLock, ScanCode.Escape),
            new(ScanCode.Escape, ScanCode.CapsLock),
            new(ScanCode.RightAlt, ScanCode.RightControl),
            new(ScanCode.Menu, ScanCode.None),
        ];

        var decoded = ScancodeMap.Decode(ScancodeMap.Encode(remaps));

        Assert.Equal(remaps, decoded);
    }

    [Fact]
    public void Encode_SameKeyTwice_Throws()
    {
        KeyRemap[] remaps =
        [
            new(ScanCode.CapsLock, ScanCode.Escape),
            new(ScanCode.CapsLock, ScanCode.LeftControl),
        ];

        Assert.Throws<ArgumentException>(() => ScancodeMap.Encode(remaps));
    }

    [Theory]
    [InlineData(new byte[] { 0, 0, 0, 0 })]
    [InlineData(new byte[] { 0, 0, 0, 0, 0, 0, 0, 0, 5, 0, 0, 0, 0, 0, 0, 0 })]
    [InlineData(new byte[] { 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 1, 0, 0, 0 })]
    public void Decode_Malformed_Throws(byte[] bytes)
    {
        Assert.Throws<FormatException>(() => ScancodeMap.Decode(bytes));
    }

    [Fact]
    public void ScanCode_ExtendedFlag()
    {
        Assert.True(ScanCode.RightAlt.IsExtended);
        Assert.False(ScanCode.LeftAlt.IsExtended);
    }
}
