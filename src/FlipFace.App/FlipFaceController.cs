using System.Windows.Threading;
using FlipFace.Core;
using FlipFace.Input;
using FlipFace.Runtime;

namespace FlipFace.App;

/// <summary>
/// Owns the remap session (on its own thread), the profile being edited, and the app settings.
/// Everything here is called and raised on the UI thread.
/// </summary>
public sealed class FlipFaceController : IDisposable
{
    private readonly Dispatcher _dispatcher = Dispatcher.CurrentDispatcher;
    private readonly DispatcherTimer _poll;
    private readonly RemapSession _session;
    private CancellationTokenSource? _cancel;
    private Thread? _thread;
    private GamepadInfo? _lastController;
    private SessionSnapshot _lastSnapshot = SessionSnapshot.Idle;

    public FlipFaceController()
    {
        Settings = AppSettings.Load();
        Profile = Store.Find(Settings.LastProfile) ?? Presets.MatchLabels;
        _session = new RemapSession(new Mapping(Profile), message => _dispatcher.BeginInvoke(() => Log?.Invoke(message)))
        {
            Enabled = Settings.RemappingEnabled,
        };
        _session.LeftoversRestored += paths => _dispatcher.BeginInvoke(() => LeftoversRestored?.Invoke(paths));
        // ~60 Hz: fast enough for the live view, and how controller (dis)connects are noticed.
        _poll = new DispatcherTimer(TimeSpan.FromMilliseconds(16), DispatcherPriority.Normal, (_, _) => Poll(), _dispatcher);
    }

    public ProfileStore Store { get; } = new(ProfileStore.DefaultDirectory);
    public AppSettings Settings { get; }

    /// <summary>The profile in use. It may have edits that aren't saved yet (<see cref="HasUnsavedChanges"/>).</summary>
    public Profile Profile { get; private set; }

    public bool HasUnsavedChanges { get; private set; }

    /// <summary>Null while running, otherwise why the session isn't running.</summary>
    public string? StartError { get; private set; }

    public SessionSnapshot Snapshot => _lastSnapshot;

    /// <summary>True from <see cref="Start"/> until the session ends.</summary>
    public bool IsRunning => _thread is not null;

    public bool Enabled
    {
        get => _session.Enabled;
        set
        {
            _session.Enabled = value;
            Settings.RemappingEnabled = value;
            Settings.Save();
            StateChanged?.Invoke();
        }
    }

    /// <summary>Raised ~60 times a second while the session's snapshot is changing.</summary>
    public event Action? SnapshotChanged;

    /// <summary>Profile switched or edited, remapping toggled, session started or stopped.</summary>
    public event Action? StateChanged;

    public event Action<string>? Log;

    /// <summary>The session found devices a crashed earlier run left hidden, and unhid them.</summary>
    public event Action<IReadOnlyList<string>>? LeftoversRestored;

    public void Start()
    {
        if (_thread is not null)
            return;
        StartError = null;
        _cancel = new CancellationTokenSource();
        var token = _cancel.Token;
        _thread = new Thread(() =>
        {
            string? error = null;
            try
            {
                _session.Run(token);
            }
            catch (CannotStartException e)
            {
                error = e.Message;
            }
            catch (Exception e)
            {
                error = $"FlipFace stopped unexpectedly: {e.Message}";
            }
            _dispatcher.BeginInvoke(() => OnSessionEnded(error));
        })
        {
            Name = "FlipFace remap",
            IsBackground = true,
            Priority = ThreadPriority.AboveNormal,
        };
        _thread.Start();
        _poll.Start();
        StateChanged?.Invoke();
    }

    private void OnSessionEnded(string? error)
    {
        _thread = null;
        StartError = error ?? (_cancel?.IsCancellationRequested == true ? null : "FlipFace stopped.");
        if (error is not null)
            Log?.Invoke(error);
        StateChanged?.Invoke();
    }

    public IReadOnlyList<Profile> Profiles() => Store.List(out _);

    /// <summary>Switches to a saved or built-in profile, dropping unsaved edits. Remembered for the connected controller.</summary>
    public void SelectProfile(string name)
    {
        if (Store.Find(name) is not { } profile)
            return;
        Use(profile, unsaved: false);
        Settings.LastProfile = profile.Name;
        if (_lastController is not null)
            Settings.ProfileByController[AppSettings.ControllerKey(_lastController)] = profile.Name;
        Settings.Save();
    }

    /// <summary>Applies an edit right away; it stays unsaved until <see cref="Save"/>.</summary>
    public void Edit(Func<Profile, Profile> change) => Use(change(Profile), unsaved: true);

    public void Save()
    {
        Store.Save(Profile);
        HasUnsavedChanges = false;
        StateChanged?.Invoke();
    }

    /// <summary>Saves the current layout under a new name and switches to it.</summary>
    public void SaveAs(string name)
    {
        Use(Profile with { Name = name, Buttons = new(Profile.Buttons) }, unsaved: true);
        Save();
        SelectProfile(name);
    }

    /// <summary>Deletes the saved copy of the current profile. A preset it replaced comes back.</summary>
    public void DeleteSaved()
    {
        var name = Profile.Name;
        Store.Delete(name);
        SelectProfile(Store.Find(name) is not null ? name : Presets.MatchLabels.Name);
    }

    public void Revert() => SelectProfile(Profile.Name);

    private void Use(Profile profile, bool unsaved)
    {
        Profile = profile;
        HasUnsavedChanges = unsaved;
        _session.Mapping = new Mapping(profile);
        StateChanged?.Invoke();
    }

    private void Poll()
    {
        var snapshot = _session.Snapshot;
        var previous = _lastSnapshot;
        if (ReferenceEquals(snapshot, previous))
            return;
        _lastSnapshot = snapshot;

        if (snapshot.Controller != _lastController)
        {
            _lastController = snapshot.Controller;
            // Switch to the profile this controller used last time, unless that would throw away edits.
            if (_lastController is not null && !HasUnsavedChanges
                && Settings.ProfileByController.TryGetValue(AppSettings.ControllerKey(_lastController), out var remembered)
                && !string.Equals(remembered, Profile.Name, StringComparison.OrdinalIgnoreCase))
                SelectProfile(remembered);
            StateChanged?.Invoke();
        }
        else if (snapshot.Remapping != previous.Remapping)
        {
            StateChanged?.Invoke();
        }
        SnapshotChanged?.Invoke();
    }

    /// <summary>Stops the session and waits (briefly) for it to unhide the controller.</summary>
    public void Dispose()
    {
        _poll.Stop();
        _cancel?.Cancel();
        _thread?.Join(TimeSpan.FromSeconds(3));
    }
}
