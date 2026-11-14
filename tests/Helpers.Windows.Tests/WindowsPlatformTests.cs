using Helpers.Core.Input;
using Helpers.Core.Platform;

namespace Helpers.Windows.Tests;

/// <summary>The shared app reaches Windows only through this, so its plumbing is worth pinning down.</summary>
public class WindowsPlatformTests
{
    [Fact]
    public void AShortcutOnAKeyWindowsDoesNotKnowIsRefused()
    {
        using IPlatform platform = new WindowsPlatform();
        var gesture = new HotkeyGesture(Ctrl: true, Alt: true, Shift: false, Win: false, Key: "NoSuchKey");

        Assert.Equal(-1, platform.Hotkeys.Register(gesture));
    }

    [Fact]
    public void TheHotkeysAreTheSameOnesEachTimeTheyAreAskedFor()
    {
        using IPlatform platform = new WindowsPlatform();

        Assert.Same(platform.Hotkeys, platform.Hotkeys);
    }

    [Fact]
    public void NoWindowIsNeitherOursNorSomewhereToSendText()
    {
        using IPlatform platform = new WindowsPlatform();

        Assert.False(platform.Windows.IsOurs(0));
        Assert.False(platform.Windows.IsOnFrame(0, 0, 0));
        Assert.Null(platform.Windows.Target(0));
        Assert.Null(platform.Windows.Centre(0));
    }

    [Fact]
    public void ASecondClaimOnTheSameNameIsTurnedAway()
    {
        using IPlatform platform = new WindowsPlatform();
        var name = "Unmuddle-test-" + Guid.NewGuid().ToString("N");

        using var first = platform.TryClaimSingleInstance(name);
        using var second = platform.TryClaimSingleInstance(name);

        Assert.NotNull(first);
        Assert.Null(second);
    }

    [Fact]
    public void TheInputEventsAreNotInstalledUntilAsked()
    {
        using IPlatform platform = new WindowsPlatform();
        using var events = platform.CreateInputEvents();

        Assert.False(events.IsInstalled);
    }
}
