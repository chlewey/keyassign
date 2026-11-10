namespace KeyAssign.Core.Layouts;

/// <summary>One line of a dead key's table: after the accent, <see cref="Base"/> types <see cref="Result"/>.</summary>
/// <remarks>Both are one UTF-16 character, as in a Windows layout DLL. Space as the base is the accent typed alone.</remarks>
public readonly record struct DeadEntry(char Base, char Result);

/// <summary>
/// A dead key's table, named by its accent. A character that isn't listed types the
/// accent and then itself (R3.3). A table that maps a whole alphabet is a layer (R3.4).
/// </summary>
public sealed class DeadKey : IEquatable<DeadKey>
{
    public char Accent { get; }

    public IReadOnlyList<DeadEntry> Entries { get; }

    public DeadKey(char accent, IEnumerable<DeadEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        var list = entries.ToArray();

        if (char.IsSurrogate(accent) || list.Any(e => char.IsSurrogate(e.Base) || char.IsSurrogate(e.Result)))
        {
            throw new ArgumentException("A dead key table holds BMP characters only.", nameof(entries));
        }

        var duplicate = list.GroupBy(e => e.Base).FirstOrDefault(g => g.Count() > 1);
        if (duplicate is not null)
        {
            throw new ArgumentException($"Dead key '{accent}' lists U+{(int)duplicate.Key:X4} twice.", nameof(entries));
        }

        Accent = accent;
        Entries = list;
    }

    /// <summary>The result after this dead key for <paramref name="baseChar"/>, or null when not listed.</summary>
    public char? Lookup(char baseChar)
    {
        foreach (var entry in Entries)
        {
            if (entry.Base == baseChar)
            {
                return entry.Result;
            }
        }

        return null;
    }

    public bool Equals(DeadKey? other) =>
        other is not null && Accent == other.Accent && Entries.SequenceEqual(other.Entries);

    public override bool Equals(object? obj) => Equals(obj as DeadKey);

    public override int GetHashCode() => HashCode.Combine(Accent, Entries.Count);
}
