namespace Helpers.Windows.Tests;

public class SingleInstanceTests
{
    [Fact]
    public void ASecondCopyIsTurnedAwayAndTheFirstIsTold()
    {
        var name = "Unmuddle-test-" + Guid.NewGuid().ToString("N");
        using var first = SingleInstance.TryClaim(name);
        Assert.NotNull(first);

        using var told = new ManualResetEventSlim();
        first.StartedAgain += told.Set;
        first.Listen();

        using var second = SingleInstance.TryClaim(name);

        Assert.Null(second);
        Assert.True(told.Wait(TimeSpan.FromSeconds(5)));
    }

    [Fact]
    public void TheNameIsFreeAgainOnceTheFirstCopyHasGone()
    {
        var name = "Unmuddle-test-" + Guid.NewGuid().ToString("N");
        var first = SingleInstance.TryClaim(name);
        Assert.NotNull(first);
        first.Listen();
        first.Dispose();

        using var next = SingleInstance.TryClaim(name);

        Assert.NotNull(next);
    }
}
