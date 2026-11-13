namespace Helpers.Core.Input;

/// <summary>What the mouse pointer looked like at one moment.</summary>
public enum PointerShape
{
    /// <summary>The I-beam apps show over text that can be selected.</summary>
    Text,

    /// <summary>A standard pointer that isn't for text: the arrow, the hand, a resize arrow, busy.</summary>
    Plain,

    /// <summary>A pointer the app drew itself, or none. It might be over text; there's no telling.</summary>
    Other,
}

public static class PointerShapes
{
    /// <summary>
    /// Whether a drag or a double-click could have selected text, judged by the
    /// pointer when the button went down and when it came up. Opening a file,
    /// dragging one, or dragging a box around several all start on the ordinary
    /// arrow and never show the I-beam, so those are ruled out. A pointer the
    /// app drew itself gets the benefit of the doubt.
    /// </summary>
    public static bool CouldBeTextSelection(PointerShape atPress, PointerShape atRelease) =>
        atPress != PointerShape.Plain || atRelease == PointerShape.Text;
}
