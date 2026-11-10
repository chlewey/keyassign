using KeyAssign.Core.Keys;
namespace KeyAssign.Core.Layouts;

/// <summary>
/// A keyboard layout, or a pack of dead keys, with no ties to the registry or to the
/// text format. Keys and dead keys keep the order they were given in, so a file
/// rewritten from the model stays close to the one that was read.
/// </summary>
public sealed class Layout : IEquatable<Layout>
{
    public string Name { get; }

    public LayoutKind Kind { get; }

    /// <summary>The installed layout this one was made from, if any, such as "Spanish (Spain)".</summary>
    public string? BaseLayout { get; }

    public IReadOnlyList<KeyBinding> Keys { get; }

    public IReadOnlyList<DeadKey> DeadKeys { get; }

    public Layout(
        string name,
        LayoutKind kind,
        string? baseLayout,
        IEnumerable<KeyBinding> keys,
        IEnumerable<DeadKey> deadKeys)
    {
        ArgumentNullException.ThrowIfNull(keys);
        ArgumentNullException.ThrowIfNull(deadKeys);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (name.Any(char.IsControl) || (baseLayout?.Any(char.IsControl) ?? false))
        {
            throw new ArgumentException("Names are one line of text.", nameof(name));
        }

        var keyList = keys.ToArray();
        var deadList = deadKeys.ToArray();

        if (keyList.Any(k => k.Code == ScanCode.None))
        {
            throw new ArgumentException("A key needs a scan code.", nameof(keys));
        }

        var duplicateKey = keyList.GroupBy(k => k.Code).FirstOrDefault(g => g.Count() > 1);
        if (duplicateKey is not null)
        {
            throw new ArgumentException($"Scan code {duplicateKey.Key} is bound twice.", nameof(keys));
        }

        var duplicateDead = deadList.GroupBy(d => d.Accent).FirstOrDefault(g => g.Count() > 1);
        if (duplicateDead is not null)
        {
            throw new ArgumentException($"Dead key '{duplicateDead.Key}' has two tables.", nameof(deadKeys));
        }

        Name = name;
        Kind = kind;
        BaseLayout = baseLayout;
        Keys = keyList;
        DeadKeys = deadList;
    }

    public KeyBinding? FindKey(ScanCode code) => Keys.FirstOrDefault(k => k.Code == code);

    public DeadKey? FindDeadKey(char accent) => DeadKeys.FirstOrDefault(d => d.Accent == accent);

    public bool Equals(Layout? other) =>
        other is not null
        && Name == other.Name
        && Kind == other.Kind
        && BaseLayout == other.BaseLayout
        && Keys.SequenceEqual(other.Keys)
        && DeadKeys.SequenceEqual(other.DeadKeys);

    public override bool Equals(object? obj) => Equals(obj as Layout);

    public override int GetHashCode() => HashCode.Combine(Name, Kind, Keys.Count, DeadKeys.Count);
}
