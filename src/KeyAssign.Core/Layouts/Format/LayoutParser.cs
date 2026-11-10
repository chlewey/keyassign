using KeyAssign.Core.Keys;
using System.Globalization;

namespace KeyAssign.Core.Layouts.Format;

/// <summary>
/// Reads the layout text format (<c>docs/FORMAT.md</c>). Statements are one per line,
/// except that a <c>dead</c> block runs from <c>{</c> to <c>}</c>. Every problem found is
/// reported with its line and column; a statement with an error is skipped and
/// reading goes on, so one pass reports many errors.
/// </summary>
internal sealed class LayoutParser
{
    private readonly List<ParseError> _errors = [];
    private readonly List<KeyBinding> _keys = [];
    private readonly HashSet<ScanCode> _scanCodes = [];
    private readonly List<DeadKey> _deadKeys = [];
    private readonly HashSet<char> _accents = [];

    private int _line;
    private string? _name;
    private LayoutKind _kind;
    private bool _headerSeen;
    private bool _missingHeaderReported;
    private string? _baseLayout;
    private bool _baseSeen;

    // State of the dead block being read, if any.
    private bool _inDead;
    private char _accent;
    private bool _keepBlock;
    private int _deadLine;
    private int _deadColumn;
    private List<DeadEntry> _entries = [];
    private HashSet<char> _bases = [];

    public ParseResult Run(string text)
    {
        if (text.StartsWith('﻿'))
        {
            text = text[1..];
        }

        var lines = text.Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            _line = i + 1;
            ParseLine(lines[i].TrimEnd('\r'));
        }

        if (_inDead)
        {
            Error(_deadLine, _deadColumn, "This dead key block is never closed with }.");
        }

        if (!_headerSeen && !_missingHeaderReported)
        {
            Error(1, 1, "The file must start with layout \"Name\" or pack \"Name\".");
        }

        if (_errors.Count > 0)
        {
            return ParseResult.Failure(_errors);
        }

        try
        {
            return ParseResult.Success(new Layout(_name!, _kind, _baseLayout, _keys, _deadKeys));
        }
        catch (ArgumentException ex)
        {
            return ParseResult.Failure([new ParseError(1, 1, ex.Message)]);
        }
    }

    private void ParseLine(string line)
    {
        var tokens = new List<Token>();
        if (!Tokenizer.Tokenize(line, _line, _errors, tokens) || tokens.Count == 0)
        {
            return;
        }

        if (_inDead)
        {
            ParseDeadBody(tokens, 0);
        }
        else
        {
            ParseStatement(tokens);
        }
    }

    private void ParseStatement(List<Token> tokens)
    {
        var first = tokens[0];
        if (first.Kind != TokenKind.Word)
        {
            Error(first, "Expected a statement: layout, pack, base, key or dead.");
            return;
        }

        if (first.Text is not ("layout" or "pack") && !_headerSeen && !_missingHeaderReported)
        {
            Error(first, "The file must start with layout \"Name\" or pack \"Name\".");
            _missingHeaderReported = true;
        }

        switch (first.Text)
        {
            case "layout":
            case "pack":
                ParseHeader(tokens);
                break;
            case "base":
                ParseBase(tokens);
                break;
            case "key":
                ParseKey(tokens);
                break;
            case "dead":
                ParseDeadHeader(tokens);
                break;
            default:
                Error(first, $"Unknown statement '{first.Text}'. Expected layout, pack, base, key or dead.");
                break;
        }
    }

    private void ParseHeader(List<Token> tokens)
    {
        var keyword = tokens[0].Text;
        if (_headerSeen)
        {
            Error(tokens[0], "A file has one layout or pack header.");
            return;
        }

        if (!TryParseName(tokens, keyword, out var name))
        {
            return;
        }

        _headerSeen = true;
        _name = name;
        _kind = keyword == "pack" ? LayoutKind.Pack : LayoutKind.Layout;
    }

    private void ParseBase(List<Token> tokens)
    {
        if (_baseSeen)
        {
            Error(tokens[0], "A file has one base statement.");
            return;
        }

        if (TryParseName(tokens, "base", out var name))
        {
            _baseSeen = true;
            _baseLayout = name;
        }
    }

    private bool TryParseName(List<Token> tokens, string keyword, out string name)
    {
        name = "";
        if (tokens.Count < 2)
        {
            Error(tokens[^1].End, $"Expected a name in quotes after {keyword}, as in {keyword} \"Name\".");
            return false;
        }

        if (tokens[1].Kind != TokenKind.Quoted)
        {
            Error(tokens[1], $"Expected a name in quotes after {keyword}.");
            return false;
        }

        if (string.IsNullOrWhiteSpace(tokens[1].Text) || tokens[1].Text.Any(char.IsControl))
        {
            Error(tokens[1], "A name is one line of text and is not empty.");
            return false;
        }

        if (tokens.Count > 2)
        {
            Error(tokens[2], "Unexpected text after the name.");
            return false;
        }

        name = tokens[1].Text;
        return true;
    }

    private void ParseKey(List<Token> tokens)
    {
        // key SCAN NORMAL SHIFT ALTGR ALTGR+SHIFT [CAPS]
        if (tokens.Count < 6)
        {
            Error(tokens[^1].End, "Expected key, a scan code and four outputs: normal, shift, altgr, altgr+shift (use none for no output).");
            return;
        }

        var valid = TryParseScanCode(tokens[1], out var code);
        if (valid && !_scanCodes.Add(code))
        {
            Error(tokens[1], $"Scan code {tokens[1].Text} is already bound.");
            valid = false;
        }

        var outputs = new KeyOutput[4];
        for (var i = 0; i < 4; i++)
        {
            if (TryParseOutput(tokens[2 + i], out var output))
            {
                outputs[i] = output;
            }
            else
            {
                valid = false;
            }
        }

        var caps = CapsBehavior.Ignored;
        if (tokens.Count >= 7)
        {
            if (!TryParseCaps(tokens[6], out caps))
            {
                valid = false;
            }
        }
        else if (valid)
        {
            caps = KeyBinding.DefaultCaps(outputs[0], outputs[1]);
        }

        if (tokens.Count > 7)
        {
            Error(tokens[7], "Unexpected text after the Caps Lock column.");
            valid = false;
        }

        if (valid)
        {
            _keys.Add(new KeyBinding(code, outputs[0], outputs[1], outputs[2], outputs[3], caps));
        }
    }

    private bool TryParseScanCode(Token token, out ScanCode code)
    {
        code = ScanCode.None;
        var text = token.Text;

        if (token.Kind == TokenKind.Word
            && text.Length is 2 or 4
            && ushort.TryParse(text, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var value)
            && (value & 0xFF) != 0
            && (value >> 8) is 0x00 or 0xE0 or 0xE1)
        {
            code = new ScanCode(value);
            return true;
        }

        Error(token, $"'{text}' is not a scan code. Write two hex digits (02) or E0/E1 and two more (E01D).");
        return false;
    }

    private bool TryParseCaps(Token token, out CapsBehavior caps)
    {
        caps = CapsBehavior.Ignored;
        if (token.Kind == TokenKind.Word)
        {
            switch (token.Text)
            {
                case "-":
                    return true;
                case "shift":
                    caps = CapsBehavior.InvertsShift;
                    return true;
                case "all":
                    caps = CapsBehavior.InvertsShiftAndAltGr;
                    return true;
            }
        }

        Error(token, $"'{token.Text}' is not a Caps Lock setting. Use - (ignored), shift or all.");
        return false;
    }

    private bool TryParseOutput(Token token, out KeyOutput output)
    {
        output = KeyOutput.None;
        if (!CheckWord(token))
        {
            return false;
        }

        var word = token.Text;
        if (word == "none")
        {
            return true;
        }

        if (word.StartsWith("dead(", StringComparison.Ordinal) && word.EndsWith(')') && word.Length > 6)
        {
            var inner = word[5..^1];
            if (ValueSyntax.TryParseText(inner, out var accent, out var innerError))
            {
                if (accent.Length == 1)
                {
                    output = KeyOutput.Dead(accent[0]);
                    return true;
                }

                Error(token.Column + 5, "A dead key's accent is one UTF-16 character.");
                return false;
            }

            Error(token.Column + 5, innerError);
            return false;
        }

        if (ValueSyntax.TryParseText(word, out var text, out var error))
        {
            output = KeyOutput.Chars(text);
            return true;
        }

        Error(token, error);
        return false;
    }

    private bool CheckWord(Token token)
    {
        switch (token.Kind)
        {
            case TokenKind.Word:
                return true;
            case TokenKind.Quoted:
                Error(token, "Expected a character, none or dead(...), not a quoted string. Write \" as U+0022.");
                return false;
            default:
                Error(token, $"Write '{token.Text}' as U+{(int)token.Text[0]:X4}; it has a meaning of its own in a file.");
                return false;
        }
    }

    private void ParseDeadHeader(List<Token> tokens)
    {
        // dead ACCENT {
        if (tokens.Count < 3)
        {
            Error(tokens[^1].End, "Expected dead, an accent and {, as in dead ´ {.");
            return;
        }

        var headerOk = CheckWord(tokens[1]);
        char accent = default;
        if (headerOk)
        {
            if (ValueSyntax.TryParseText(tokens[1].Text, out var text, out var error))
            {
                if (text.Length == 1)
                {
                    accent = text[0];
                }
                else
                {
                    Error(tokens[1], "A dead key's accent is one UTF-16 character.");
                    headerOk = false;
                }
            }
            else
            {
                Error(tokens[1], error);
                headerOk = false;
            }
        }

        if (!tokens[2].IsPunct('{'))
        {
            Error(tokens[2], "Expected { after the accent.");
            return;
        }

        _keepBlock = headerOk;
        if (headerOk && !_accents.Add(accent))
        {
            Error(tokens[1], $"Dead key '{tokens[1].Text}' already has a table.");
            _keepBlock = false;
        }

        _inDead = true;
        _accent = accent;
        _deadLine = _line;
        _deadColumn = tokens[0].Column;
        _entries = [];
        _bases = [];

        ParseDeadBody(tokens, 3);
    }

    private void ParseDeadBody(List<Token> tokens, int start)
    {
        // BASE = RESULT, any number per line, then }
        var i = start;
        while (i < tokens.Count)
        {
            var token = tokens[i];
            if (token.IsPunct('}'))
            {
                CloseDeadBlock();
                if (i + 1 < tokens.Count)
                {
                    Error(tokens[i + 1], "Unexpected text after }.");
                }

                return;
            }

            if (i + 2 >= tokens.Count || !tokens[i + 1].IsPunct('='))
            {
                var at = i + 1 < tokens.Count ? tokens[i + 1].Column : token.End;
                Error(at, "Expected base = result, as in a = á.");
                return;
            }

            ParseDeadEntry(tokens[i], tokens[i + 2]);
            i += 3;
        }
    }

    private void ParseDeadEntry(Token baseToken, Token resultToken)
    {
        var baseOk = TryParseDeadChar(baseToken, isResult: false, out var baseChar);
        var resultOk = TryParseDeadChar(resultToken, isResult: true, out var resultChar);
        if (!baseOk || !resultOk)
        {
            return;
        }

        if (!_bases.Add(baseChar))
        {
            Error(baseToken, $"'{baseToken.Text}' is already in this table.");
            return;
        }

        _entries.Add(new DeadEntry(baseChar, resultChar));
    }

    private bool TryParseDeadChar(Token token, bool isResult, out char value)
    {
        value = default;
        if (!CheckWord(token))
        {
            return false;
        }

        if (token.Text == "space")
        {
            value = ' ';
            return true;
        }

        if (token.Text.StartsWith("dead(", StringComparison.Ordinal))
        {
            Error(token, isResult
                ? "A dead key that gives another dead key (chaining) isn't supported yet."
                : "The base of a dead key table is a character, not a dead key.");
            return false;
        }

        if (!ValueSyntax.TryParseText(token.Text, out var text, out var error))
        {
            Error(token, error);
            return false;
        }

        if (text.Length != 1)
        {
            Error(token, "A dead key table holds single UTF-16 characters, one in and one out.");
            return false;
        }

        value = text[0];
        return true;
    }

    private void CloseDeadBlock()
    {
        _inDead = false;
        if (!_keepBlock)
        {
            return;
        }

        _deadKeys.Add(new DeadKey(_accent, _entries));
    }

    private void Error(Token token, string message) => Error(_line, token.Column, message);

    private void Error(int column, string message) => Error(_line, column, message);

    private void Error(int line, int column, string message) => _errors.Add(new ParseError(line, column, message));
}
