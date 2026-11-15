using System.Text;

namespace KeyAssign.Core.Layouts.Format;

internal enum TokenKind
{
    /// <summary>Anything up to whitespace or one of the punctuation characters.</summary>
    Word,

    /// <summary>A "double-quoted" string, with <c>\"</c> and <c>\\</c> escapes. Used for names.</summary>
    Quoted,

    /// <summary>One of <c>{ } =</c>.</summary>
    Punct,
}

internal readonly record struct Token(TokenKind Kind, string Text, int Column, int Length)
{
    public int End => Column + Length;

    public bool IsPunct(char c) => Kind == TokenKind.Punct && Text[0] == c;
}

internal static class Tokenizer
{
    /// <summary>
    /// Splits one line, dropping a trailing <c>#</c> comment. Returns false after adding
    /// an error when the line can't be split (an unterminated string).
    /// </summary>
    public static bool Tokenize(string line, int lineNumber, List<ParseError> errors, List<Token> tokens)
    {
        var i = 0;
        var ok = true;

        while (i < line.Length)
        {
            var c = line[i];

            if (char.IsWhiteSpace(c))
            {
                i++;
            }
            else if (c == '#')
            {
                break;
            }
            else if (c is '{' or '}' or '=')
            {
                tokens.Add(new Token(TokenKind.Punct, c.ToString(), i + 1, 1));
                i++;
            }
            else if (c == '"')
            {
                var start = i++;
                var text = new StringBuilder();
                var closed = false;

                while (i < line.Length)
                {
                    var ch = line[i];
                    if (ch == '\\' && i + 1 < line.Length && line[i + 1] is '"' or '\\')
                    {
                        text.Append(line[i + 1]);
                        i += 2;
                    }
                    else if (ch == '\\')
                    {
                        errors.Add(new ParseError(lineNumber, i + 1, "Unknown escape; use \\\" or \\\\ inside a string."));
                        ok = false;
                        i++;
                    }
                    else if (ch == '"')
                    {
                        closed = true;
                        i++;
                        break;
                    }
                    else
                    {
                        text.Append(ch);
                        i++;
                    }
                }

                if (!closed)
                {
                    errors.Add(new ParseError(lineNumber, start + 1, "This string is missing its closing quote."));
                    return false;
                }

                tokens.Add(new Token(TokenKind.Quoted, text.ToString(), start + 1, i - start));
            }
            else
            {
                var start = i;
                while (i < line.Length && !char.IsWhiteSpace(line[i]) && line[i] is not ('{' or '}' or '=' or '#'))
                {
                    i++;
                }

                tokens.Add(new Token(TokenKind.Word, line[start..i], start + 1, i - start));
            }
        }

        return ok;
    }
}
