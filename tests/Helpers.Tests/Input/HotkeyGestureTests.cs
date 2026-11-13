using Helpers.Core.Input;

namespace Helpers.Tests.Input;

public class HotkeyGestureTests
{
    [Theory]
    [InlineData("Ctrl+Alt+Space", true, true, false, false, "Space")]
    [InlineData("ctrl + shift + r", true, false, true, false, "R")]
    [InlineData("Win+F9", false, false, false, true, "F9")]
    [InlineData("Control+Insert", true, false, false, false, "Insert")]
    [InlineData("Alt+pgdn", false, true, false, false, "PageDown")]
    public void ParsesModifiersAndKey(string text, bool ctrl, bool alt, bool shift, bool win, string key)
    {
        var gesture = HotkeyGesture.Parse(text);

        Assert.NotNull(gesture);
        Assert.Equal(new HotkeyGesture(ctrl, alt, shift, win, key), gesture);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Ctrl+Alt")]
    [InlineData("A+B")]
    public void RejectsThingsThatAreNotShortcuts(string text)
    {
        Assert.Null(HotkeyGesture.Parse(text));
    }

    [Fact]
    public void RoundTripsThroughText()
    {
        var gesture = HotkeyGesture.Parse("Shift+Alt+F5")!;

        Assert.Equal("Alt+Shift+F5", gesture.ToString());
        Assert.Equal(gesture, HotkeyGesture.Parse(gesture.ToString()));
    }

    [Fact]
    public void DefaultIsCtrlAltH()
    {
        Assert.Equal("Ctrl+Alt+H", HotkeyGesture.Default.ToString());
        Assert.True(HotkeyGesture.Default.HasModifier);
    }

    [Fact]
    public void ANewCopyStartsOnTheDefaultAndTheOldDefaultIsADifferentShortcut()
    {
        var settings = new Helpers.Core.Settings.AppSettings();

        Assert.Equal(HotkeyGesture.Default, HotkeyGesture.Parse(settings.ReadSelectionHotkey));
        Assert.NotEqual(HotkeyGesture.Default, HotkeyGesture.Parse(Helpers.Core.Settings.AppSettings.PreviousReadSelectionHotkey));
        Assert.NotEqual(HotkeyGesture.Default, HotkeyGesture.Parse(settings.ComposeHotkey));
    }

    [Fact]
    public void PlainKeyHasNoModifier()
    {
        Assert.False(HotkeyGesture.Parse("F9")!.HasModifier);
    }
}
