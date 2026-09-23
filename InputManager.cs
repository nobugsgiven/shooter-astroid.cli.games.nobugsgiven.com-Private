namespace ShooterAstroidConsole;

/// <summary>
/// Non-blocking keyboard input. Consoles only deliver key *down* events
/// (via the terminal's key repeat), never key-up events, so a key is
/// considered "held" for a short window after its last event.
///
/// Two windows are used per key:
/// - The first event of a press gets a long window (<see cref="InitialHoldSeconds"/>)
///   that bridges the OS "delay until repeat", so holding a key moves
///   smoothly from the very first instant instead of stuttering once.
/// - Each repeat event gets a short window adapted to the observed repeat
///   rate, so releasing the key still stops movement quickly.
/// </summary>
internal sealed class InputManager
{
    private const double InitialHoldSeconds = 0.55;
    private const double MinRepeatHoldSeconds = 0.15;
    private const double MaxRepeatHoldSeconds = 0.45;

    private readonly Dictionary<ConsoleKey, double> _heldUntil = new();
    private readonly Dictionary<ConsoleKey, double> _lastEventAt = new();
    private readonly HashSet<ConsoleKey> _pressedThisFrame = new();
    private double _now;

    public void Poll(double now)
    {
        _now = now;
        _pressedThisFrame.Clear();

        try
        {
            // Drain every queued key event; never block waiting for one.
            while (Console.KeyAvailable)
            {
                ConsoleKeyInfo key = Console.ReadKey(intercept: true);
                OnKeyEvent(key.Key, now);
            }
        }
        catch (InvalidOperationException)
        {
            // Input was redirected out from under us; treat as "no input".
        }
        catch (IOException)
        {
            // Some terminals throw here during a resize; skip this frame's input.
        }

        // Keep the dictionaries from growing if many distinct keys were hit.
        if (_heldUntil.Count > 16)
        {
            var expired = new List<ConsoleKey>();
            foreach (var pair in _heldUntil)
            {
                if (pair.Value < now)
                    expired.Add(pair.Key);
            }
            foreach (var key in expired)
            {
                _heldUntil.Remove(key);
                _lastEventAt.Remove(key);
            }
        }
    }

    /// <summary>Advances the clock without any key events. Poll does this
    /// every frame; the headless self-test uses it directly.</summary>
    internal void Tick(double now)
    {
        _now = now;
        _pressedThisFrame.Clear();
    }

    /// <summary>Records one key-down event. Separated from Poll so the
    /// timing logic can be exercised headlessly by the self-test.</summary>
    internal void OnKeyEvent(ConsoleKey key, double now)
    {
        bool wasHeld = _heldUntil.TryGetValue(key, out double until) && now <= until;

        double window = InitialHoldSeconds;
        if (wasHeld && _lastEventAt.TryGetValue(key, out double lastAt))
        {
            // Repeat event: adapt the window to this terminal's repeat rate
            // (with a margin), so slow repeat settings don't stutter and
            // fast ones still release crisply.
            double interval = now - lastAt;
            window = Math.Clamp(interval * 1.8, MinRepeatHoldSeconds, MaxRepeatHoldSeconds);
        }

        _heldUntil[key] = now + window;
        _lastEventAt[key] = now;
        _pressedThisFrame.Add(key);
        _now = now;
    }

    /// <summary>True while any of the keys is considered held down.</summary>
    public bool IsDown(params ConsoleKey[] keys) => IsDownAt(_now, keys);

    /// <summary>Time-explicit variant, used by the headless self-test.</summary>
    internal bool IsDownAt(double now, params ConsoleKey[] keys)
    {
        foreach (var key in keys)
        {
            if (_heldUntil.TryGetValue(key, out double until) && now <= until)
                return true;
        }
        return false;
    }

    /// <summary>True only on the frame the key event arrived (edge trigger).</summary>
    public bool WasPressed(params ConsoleKey[] keys)
    {
        foreach (var key in keys)
        {
            if (_pressedThisFrame.Contains(key))
                return true;
        }
        return false;
    }
}
