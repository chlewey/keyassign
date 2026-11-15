namespace KeyAssign.Core.Layouts;

internal static class Utf16Text
{
    /// <summary>True when every surrogate in the text is half of a proper pair.</summary>
    public static bool IsWellFormed(ReadOnlySpan<char> text)
    {
        for (var i = 0; i < text.Length; i++)
        {
            if (char.IsHighSurrogate(text[i]))
            {
                if (i + 1 >= text.Length || !char.IsLowSurrogate(text[i + 1]))
                {
                    return false;
                }

                i++;
            }
            else if (char.IsLowSurrogate(text[i]))
            {
                return false;
            }
        }

        return true;
    }
}
