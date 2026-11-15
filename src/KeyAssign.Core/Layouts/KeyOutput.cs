
namespace KeyAssign.Core.Layouts;

/// <summary>What a key types at one level: nothing, text, or a dead key.</summary>
public sealed record KeyOutput
{
    /// <summary>A Windows layout holds at most four UTF-16 code units per key and level (a ligature).</summary>
    public const int MaxLength = 4;

    public static readonly KeyOutput None = new(OutputKind.None, "");

    public OutputKind Kind { get; }

    /// <summary>The characters typed, or the accent for a dead key; empty for <see cref="OutputKind.None"/>.</summary>
    public string Text { get; }

    private KeyOutput(OutputKind kind, string text)
    {
        Kind = kind;
        Text = text;
    }

    public static KeyOutput Chars(string text)
    {
        ArgumentException.ThrowIfNullOrEmpty(text);
        if (text.Length > MaxLength)
        {
            throw new ArgumentException($"A key can type at most {MaxLength} UTF-16 code units.", nameof(text));
        }

        if (!Utf16Text.IsWellFormed(text))
        {
            throw new ArgumentException("The text has an unpaired surrogate.", nameof(text));
        }

        return new KeyOutput(OutputKind.Text, text);
    }

    public static KeyOutput Dead(char accent)
    {
        if (char.IsSurrogate(accent))
        {
            throw new ArgumentException("A dead key's accent is one BMP character.", nameof(accent));
        }

        return new KeyOutput(OutputKind.Dead, accent.ToString());
    }

    /// <summary>The accent of a dead key, which names its table in the layout.</summary>
    public char Accent => Kind == OutputKind.Dead
        ? Text[0]
        : throw new InvalidOperationException("This output is not a dead key.");

    public override string ToString() => Kind switch
    {
        OutputKind.None => "none",
        OutputKind.Dead => $"dead({Text})",
        _ => Text,
    };
}
