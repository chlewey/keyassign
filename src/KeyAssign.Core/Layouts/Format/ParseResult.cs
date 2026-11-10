namespace KeyAssign.Core.Layouts.Format;

/// <summary>A problem in a layout file. Line and column are 1-based; a column counts UTF-16 characters.</summary>
public sealed record ParseError(int Line, int Column, string Message)
{
    public override string ToString() => $"{Line}:{Column}: {Message}";
}

/// <summary>
/// The outcome of reading a layout file. <see cref="Layout"/> is set only when there
/// are no errors: a file with errors is never applied (R1.4).
/// </summary>
public sealed class ParseResult
{
    public Layout? Layout { get; }

    public IReadOnlyList<ParseError> Errors { get; }

    public bool Succeeded => Layout is not null;

    private ParseResult(Layout? layout, IReadOnlyList<ParseError> errors)
    {
        Layout = layout;
        Errors = errors;
    }

    internal static ParseResult Success(Layout layout) => new(layout, []);

    internal static ParseResult Failure(IReadOnlyList<ParseError> errors) => new(null, errors);
}
