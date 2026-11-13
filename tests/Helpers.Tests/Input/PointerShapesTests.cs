using Helpers.Core.Input;

namespace Helpers.Tests.Input;

public class PointerShapesTests
{
    [Theory]
    // Dragging over text, or double-clicking a word: the I-beam throughout.
    [InlineData(PointerShape.Text, PointerShape.Text, true)]
    // Started on text and let go over a picture, a link or the margin.
    [InlineData(PointerShape.Text, PointerShape.Plain, true)]
    [InlineData(PointerShape.Text, PointerShape.Other, true)]
    // Started in the margin beside a paragraph and dragged into it.
    [InlineData(PointerShape.Plain, PointerShape.Text, true)]
    // Opening a file with a double-click, or dragging a box around several: the arrow throughout.
    [InlineData(PointerShape.Plain, PointerShape.Plain, false)]
    // Dragging a file: the arrow, then the pointer Windows draws for a drag.
    [InlineData(PointerShape.Plain, PointerShape.Other, false)]
    // An app that draws its own pointers gets the benefit of the doubt.
    [InlineData(PointerShape.Other, PointerShape.Other, true)]
    [InlineData(PointerShape.Other, PointerShape.Plain, true)]
    [InlineData(PointerShape.Other, PointerShape.Text, true)]
    public void OnlyTheOrdinaryPointerAtThePressRulesTextOut(PointerShape atPress, PointerShape atRelease, bool expected)
    {
        Assert.Equal(expected, PointerShapes.CouldBeTextSelection(atPress, atRelease));
    }
}
