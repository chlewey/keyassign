namespace KeyAssign.Core.Layouts;

public enum OutputKind
{
    /// <summary>The key types nothing at this level.</summary>
    None,

    /// <summary>The key types one to four UTF-16 code units.</summary>
    Text,

    /// <summary>The key is a dead key and types nothing itself.</summary>
    Dead,
}

/// <summary>A layout is a whole keyboard; a pack is a reusable set of dead keys to add to one.</summary>
public enum LayoutKind
{
    Layout,
    Pack,
}
