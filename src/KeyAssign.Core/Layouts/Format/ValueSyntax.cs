using System.Globalization;
using System.Text;

namespace KeyAssign.Core.Layouts.Format;

/// <summary>
/// How one character, or a few, is written in a layout file: bare when it is a plain
/// visible character, otherwise as <c>U+0061</c> (several: <c>U+0066+0066+0069</c>).
/// Shared by the parser and the writer so that what one writes the other reads.
/// </summary>
internal static class ValueSyntax
{
    /// <summary>Characters that mean something in the file, so they are written as U+XXXX.</summary>
    private const string Reserved = "#\"{}=";

    public static bool TryParseText(string word, out string text, out string error)
    {
        text = "";
        error = "";

        if (word.StartsWith("U+", StringComparison.OrdinalIgnoreCase))
        {
            var builder = new StringBuilder();
            foreach (var part in word[2..].Split('+'))
            {
                if (part.Length is < 4 or > 6
                    || !int.TryParse(part, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var scalar)
                    || !Rune.IsValid(scalar)
                    || scalar == 0)
                {
                    error = $"'{part}' is not a Unicode code point; write 4 to 6 hex digits, as in U+00E1.";
                    return false;
                }

                builder.Append(char.ConvertFromUtf32(scalar));
            }

            text = builder.ToString();
        }
        else
        {
            if (!Utf16Text.IsWellFormed(word))
            {
                error = "The text has an unpaired surrogate.";
                return false;
            }

            if (word.Any(char.IsControl))
            {
                error = "Write control characters as U+XXXX.";
                return false;
            }

            if (new StringInfo(word).LengthInTextElements != 1)
            {
                error = $"Expected one character, found '{word}'. To type several, write U+XXXX+XXXX.";
                return false;
            }

            text = word;
        }

        if (text.Length > KeyOutput.MaxLength)
        {
            error = $"At most {KeyOutput.MaxLength} UTF-16 code units fit in one key.";
            return false;
        }

        return true;
    }

    public static string FormatText(string text)
    {
        if (CanBeBare(text))
        {
            return text;
        }

        var scalars = new List<string>();
        foreach (var rune in text.EnumerateRunes())
        {
            scalars.Add(rune.Value.ToString("X4", CultureInfo.InvariantCulture));
        }

        return "U+" + string.Join('+', scalars);
    }

    public static string FormatChar(char c) => FormatText(c.ToString());

    private static bool CanBeBare(string text)
    {
        if (text.Length == 0 || !Utf16Text.IsWellFormed(text) || new StringInfo(text).LengthInTextElements != 1)
        {
            return false;
        }

        if (text.Any(c => char.IsWhiteSpace(c) || char.IsControl(c) || Reserved.Contains(c)))
        {
            return false;
        }

        // A lone combining mark, a format character or a private-use code point is
        // invisible or odd in an editor: spell it out.
        var first = CharUnicodeInfo.GetUnicodeCategory(text, 0);
        return first is not (UnicodeCategory.NonSpacingMark
            or UnicodeCategory.SpacingCombiningMark
            or UnicodeCategory.EnclosingMark
            or UnicodeCategory.Format
            or UnicodeCategory.PrivateUse
            or UnicodeCategory.OtherNotAssigned
            or UnicodeCategory.LineSeparator
            or UnicodeCategory.ParagraphSeparator);
    }
}
