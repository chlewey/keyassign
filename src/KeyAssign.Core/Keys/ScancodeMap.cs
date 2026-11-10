using System.Buffers.Binary;

namespace KeyAssign.Core.Keys;

/// <summary>
/// Encodes and decodes the binary <c>Scancode Map</c> value that Windows reads at
/// sign-in from <c>HKLM\SYSTEM\CurrentControlSet\Control\Keyboard Layout</c>.
/// This class only converts bytes; writing the registry is a separate concern.
/// </summary>
/// <remarks>
/// Layout, all little-endian: an 8-byte zero header (version and flags), a 4-byte
/// entry count that includes the terminator, one 4-byte entry per remap (the low
/// word is the code to send, the high word the key pressed), and a 4-byte zero
/// terminator.
/// </remarks>
public static class ScancodeMap
{
    private const int HeaderSize = 8;
    private const int EntrySize = 4;

    public static byte[] Encode(IReadOnlyCollection<KeyRemap> remaps)
    {
        ArgumentNullException.ThrowIfNull(remaps);

        var duplicate = remaps.GroupBy(r => r.From).FirstOrDefault(g => g.Count() > 1);
        if (duplicate is not null)
        {
            throw new ArgumentException($"Key {duplicate.Key} is remapped more than once.", nameof(remaps));
        }

        var count = remaps.Count + 1;
        var bytes = new byte[HeaderSize + EntrySize + count * EntrySize];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(HeaderSize), (uint)count);

        var offset = HeaderSize + EntrySize;
        foreach (var remap in remaps)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(offset), remap.To.Value);
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(offset + 2), remap.From.Value);
            offset += EntrySize;
        }

        return bytes;
    }

    public static IReadOnlyList<KeyRemap> Decode(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < HeaderSize + EntrySize * 2)
        {
            throw new FormatException("Scancode Map is too short.");
        }

        var count = BinaryPrimitives.ReadUInt32LittleEndian(bytes[HeaderSize..]);
        if (count == 0 || bytes.Length < HeaderSize + EntrySize + (long)count * EntrySize)
        {
            throw new FormatException("Scancode Map entry count doesn't match its length.");
        }

        var remaps = new List<KeyRemap>((int)count - 1);
        var offset = HeaderSize + EntrySize;
        for (var i = 0; i < count - 1; i++)
        {
            var to = BinaryPrimitives.ReadUInt16LittleEndian(bytes[offset..]);
            var from = BinaryPrimitives.ReadUInt16LittleEndian(bytes[(offset + 2)..]);
            remaps.Add(new KeyRemap(new ScanCode(from), new ScanCode(to)));
            offset += EntrySize;
        }

        if (BinaryPrimitives.ReadUInt32LittleEndian(bytes[offset..]) != 0)
        {
            throw new FormatException("Scancode Map is missing its zero terminator.");
        }

        return remaps;
    }
}
