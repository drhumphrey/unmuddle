using System.Diagnostics;
using System.Security.Cryptography;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using Helpers.App.Windows;
using Helpers.Core.Settings;
using Helpers.Core.Updates;

namespace Helpers.App.Services;

/// <summary>
/// The opt-in update check. When the user has said yes, asks GitHub for the
/// latest release's version file at most once a day, a while after start-up,
/// and offers the new version in a toast. Update now fetches the installer,
/// checks it, and runs it silently.
/// </summary>
public sealed class UpdateService : IDisposable
{
    private static readonly TimeSpan FirstCheckDelay = TimeSpan.FromSeconds(45);
    private static readonly TimeSpan Recheck = TimeSpan.FromHours(4);

    private readonly SettingsStore _settings;
    private readonly ToastService _toasts;
    private readonly DispatcherTimer _timer;
    private UpdateWindow? _window;
    private bool _busy;
    private bool _updating;

    public UpdateService(SettingsStore settings, ToastService toasts)
    {
        _settings = settings;
        _toasts = toasts;
        _timer = new DispatcherTimer(FirstCheckDelay, DispatcherPriority.Background, (_, _) => OnTimer());
        _timer.Stop();
    }

    /// <summary>The running version, three parts.</summary>
    public static string CurrentVersion => typeof(UpdateService).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";

    public bool Enabled => _settings.Current.Updates.CheckForUpdates;

    public DateTime? LastCheckedUtc => _settings.Current.Updates.LastCheckedUtc;

    /// <summary>Starts the quiet daily check if the user has opted in. Safe to call again after the setting changes.</summary>
    public void Start()
    {
        _timer.Stop();
        if (Enabled)
        {
            _timer.Interval = FirstCheckDelay;
            _timer.Start();
        }
    }

    /// <summary>The Check now button: always checks, and always says what it found.</summary>
    public Task CheckNowAsync() => CheckAsync(manual: true);

    public void Dispose()
    {
        _timer.Stop();
        _window?.Close();
        _window = null;
    }

    private void OnTimer()
    {
        _timer.Interval = Recheck;
        if (Enabled && UpdateCheck.IsDue(LastCheckedUtc, DateTime.UtcNow))
        {
            _ = CheckAsync(manual: false);
        }
    }

    private async Task CheckAsync(bool manual)
    {
        if (_busy)
        {
            return;
        }

        _busy = true;
        try
        {
            ReleaseInfo? info;
            try
            {
                info = await FetchAsync();
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException)
            {
                if (manual)
                {
                    _toasts.Error($"Couldn't reach GitHub to check: {ex.Message}");
                }

                return;
            }

            _settings.Update(s => s.Updates.LastCheckedUtc = DateTime.UtcNow);

            if (info is null)
            {
                if (manual)
                {
                    _toasts.Info("No release has been published yet, so there is nothing newer.");
                }

                return;
            }

            var current = CurrentVersion;
            if (!UpdateCheck.IsNewer(info.Version, current))
            {
                if (manual)
                {
                    _toasts.Success($"You have the latest version, {current}.");
                }

                return;
            }

            if (!manual && string.Equals(info.Version, _settings.Current.Updates.SkippedVersion, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            _toasts.Offer($"Unmuddle {info.Version} is available. You have {current}.", "See what's new", () => ShowWindow(info));
        }
        finally
        {
            _busy = false;
        }
    }

    /// <summary>One request, for one small file. The app's name and version are the only things it carries.</summary>
    private static async Task<ReleaseInfo?> FetchAsync()
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd($"Unmuddle/{CurrentVersion}");
        using var response = await http.GetAsync(UpdateCheck.VersionUrl);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync();
        return UpdateCheck.Parse(json);
    }

    private void ShowWindow(ReleaseInfo info)
    {
        // Wired once per window: showing it again from a second toast must not add a second download.
        if (_window is null)
        {
            _window = new UpdateWindow();
            _window.UpdateRequested += release => _ = UpdateNowAsync(release);
            _window.SkipRequested += release => _settings.Update(s => s.Updates.SkippedVersion = release.Version);
            _window.Closed += (_, _) => _window = null;
        }

        _window.Show(info, CurrentVersion, AppPlatform.Current.IsInstalled);
    }

    /// <summary>Downloads the installer, checks it against the checksum, runs it silently, and starts the new version.</summary>
    private async Task UpdateNowAsync(ReleaseInfo release)
    {
        if (release.Installer is null)
        {
            Links.Open(release.Page);
            return;
        }

        if (_updating)
        {
            return;
        }

        _updating = true;
        var toast = _toasts.Progress($"Downloading Unmuddle {release.Version}…");
        var path = Path.Combine(Path.GetTempPath(), $"Unmuddle-{release.Version}-setup.exe");
        try
        {
            using var http = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
            http.DefaultRequestHeaders.UserAgent.ParseAdd($"Unmuddle/{CurrentVersion}");
            using var response = await http.GetAsync(release.Installer.Url, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();
            var total = response.Content.Headers.ContentLength ?? 0;

            using var sha = SHA256.Create();
            await using (var source = await response.Content.ReadAsStreamAsync())
            await using (var target = File.Create(path))
            {
                var buffer = new byte[1 << 16];
                long done = 0;
                int read;
                while ((read = await source.ReadAsync(buffer)) > 0)
                {
                    await target.WriteAsync(buffer.AsMemory(0, read));
                    sha.TransformBlock(buffer, 0, read, null, 0);
                    done += read;
                    if (total > 0)
                    {
                        toast.Fraction = (double)done / total;
                    }
                }

                sha.TransformFinalBlock([], 0, 0);
            }

            var actual = Convert.ToHexString(sha.Hash!).ToLowerInvariant();
            if (actual != release.Installer.Sha256)
            {
                File.Delete(path);
                _toasts.Error("The download didn't match its checksum, so it was discarded. Try again later.");
                return;
            }

            // Run the installer silently once this app has gone, then start the new version.
            var arguments = UpdateCheck.HandOverArguments(path, Environment.ProcessPath);
            if (arguments is null)
            {
                _toasts.Error("Couldn't start the installer from here.", "Open the download page", () => Links.Open(release.Page));
                return;
            }

            Process.Start(new ProcessStartInfo("cmd.exe", arguments)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
            });

            (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.Shutdown();
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException or UnauthorizedAccessException)
        {
            _toasts.Error($"Couldn't update: {ex.Message}", "Open the download page", () => Links.Open(release.Page));
        }
        finally
        {
            _toasts.Dismiss(toast);
            _updating = false;
        }
    }
}
