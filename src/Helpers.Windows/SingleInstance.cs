namespace Helpers.Windows;

/// <summary>
/// Makes sure only one copy of the app runs for each signed-in user. Two
/// copies would fight over the shortcuts and each show its own Read button.
/// The first copy holds a named event; a later copy finds it already there,
/// signals it, and should then close.
/// </summary>
public sealed class SingleInstance : IDisposable
{
    private readonly EventWaitHandle _signal;
    private Thread? _listener;
    private volatile bool _stopping;

    private SingleInstance(EventWaitHandle signal)
    {
        _signal = signal;
    }

    /// <summary>Raised on a background thread when another copy was started and has stepped aside.</summary>
    public event Action? StartedAgain;

    /// <summary>
    /// Claims the name. Returns null when another copy already holds it; that
    /// copy has then been told, and the caller should exit.
    /// </summary>
    public static SingleInstance? TryClaim(string name)
    {
        // "Local\" keeps it to this sign-in session, so two people on one PC can each run a copy.
        var signal = new EventWaitHandle(false, EventResetMode.AutoReset, $@"Local\{name}", out var createdNew);
        if (createdNew)
        {
            return new SingleInstance(signal);
        }

        signal.Set();
        signal.Dispose();
        return null;
    }

    /// <summary>Starts listening for later copies. Call once, after subscribing to <see cref="StartedAgain"/>.</summary>
    public void Listen()
    {
        if (_listener is not null)
        {
            return;
        }

        _listener = new Thread(() =>
        {
            while (true)
            {
                _signal.WaitOne();
                if (_stopping)
                {
                    return;
                }

                StartedAgain?.Invoke();
            }
        })
        {
            IsBackground = true,
            Name = "Single instance listener",
        };
        _listener.Start();
    }

    public void Dispose()
    {
        _stopping = true;
        if (_listener is not null)
        {
            _signal.Set();
            _listener.Join(TimeSpan.FromSeconds(1));
        }

        _signal.Dispose();
    }
}
