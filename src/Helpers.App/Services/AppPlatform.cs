using Helpers.Core.Platform;

namespace Helpers.App.Services;

/// <summary>
/// The operating system, as the app sees it. This is the only file in the app
/// that names a platform project; everything else asks <see cref="Current"/>.
/// </summary>
public static class AppPlatform
{
    private static IPlatform? _current;

    public static IPlatform Current => _current ??= Create();

    private static IPlatform Create()
    {
#if WINDOWS
        return new Helpers.Windows.WindowsPlatform();
#else
        throw new PlatformNotSupportedException("Unmuddle doesn't run on this system yet.");
#endif
    }
}
