using System.Diagnostics;

namespace ShooterAstroidConsole;

/// <summary>
/// Owns the game state and the real-time game loop. Rendering is delegated
/// to <see cref="ConsoleRenderer"/>, keyboard handling to <see cref="InputManager"/>.
/// </summary>
internal sealed class Game
{
    private const double TargetFps = 60.0;
    private const double FireCooldownSeconds = 0.16;
    private const double InitialSpawnDelay = 0.8;

    private readonly ConsoleRenderer _renderer = new();
    private readonly InputManager _input = new();
    private readonly Random _random = new();

    private readonly Player _player = new();
    private readonly List<Laser> _lasers = new(32);
    private readonly List<Asteroid> _asteroids = new(32);
    private readonly List<Explosion> _explosions = new(16);

    private int _score;
    private int _destroyed;
    private bool _gameOver;
    private bool _quit;
    private bool _autoFire;
    private double _fireTimer;
    private double _spawnTimer = InitialSpawnDelay;

    public int Run()
    {
        if (Console.IsInputRedirected || Console.IsOutputRedirected)
        {
            Console.WriteLine("Asteroid Shooter needs an interactive terminal (no redirected input/output).");
            return 1;
        }

        // Make Ctrl+C quit cleanly so the finally block restores the terminal.
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            _quit = true;
        };

        // The header uses Unicode glyphs (★, ═); make sure they render
        // on legacy Windows consoles too (a no-op on modern terminals).
        try { Console.OutputEncoding = System.Text.Encoding.UTF8; } catch { }

        try { Console.CursorVisible = false; } catch { /* some terminals disallow this */ }
        try { Console.Clear(); } catch { }

        var stopwatch = Stopwatch.StartNew();
        double last = stopwatch.Elapsed.TotalSeconds;

        try
        {
            // Size the renderer before placing the player.
            _renderer.BeginFrame();
            Reset();

            while (!_quit)
            {
                double now = stopwatch.Elapsed.TotalSeconds;
                double dt = Math.Min(now - last, 0.1); // clamp huge frames (debugger, hiccups)
                last = now;

                _input.Poll(now);
                if (_input.WasPressed(ConsoleKey.Escape))
                    break;

                Update(dt);
                Render();

                // Frame cap: sleep off the leftover budget so we don't spin the CPU.
                double frameSeconds = stopwatch.Elapsed.TotalSeconds - now;
                int sleepMs = (int)(1000.0 / TargetFps - frameSeconds * 1000.0);
                if (sleepMs > 1)
                    Thread.Sleep(sleepMs);
            }
        }
        finally
        {
            try
            {
                Console.ResetColor();
                Console.CursorVisible = true;
                Console.SetCursorPosition(0, Math.Max(0, Console.WindowHeight - 1));
            }
            catch { /* never leave the terminal broken because teardown failed */ }
        }

        Console.WriteLine();
        Console.WriteLine("Thanks for playing Asteroid Shooter!");
        return 0;
    }

    private void Reset()
    {
        _lasers.Clear();
        _asteroids.Clear();
        _explosions.Clear();
        _score = 0;
        _destroyed = 0;
        _gameOver = false;
        _autoFire = false;
        _fireTimer = 0;
        _spawnTimer = InitialSpawnDelay;
        _player.Reset(_renderer.Width, _renderer.Height);
    }

    private void Update(double dt)
    {
        int width = _renderer.Width;
        int height = _renderer.Height;

        if (_gameOver)
        {
            // Keep the world drifting behind the overlay; only input and FX advance.
            if (_input.WasPressed(ConsoleKey.R))
                Reset();

            UpdateAsteroids(dt, width);
            UpdateExplosions(dt);
            CullOffscreenAsteroids(height);
            return;
        }

        _player.Update(dt, _input, width, height);

        // F toggles autofire: terminals only auto-repeat the most recently
        // pressed key, so holding a movement key suppresses Space repeats.
        // With autofire on, shooting needs no key at all.
        if (_input.WasPressed(ConsoleKey.F))
            _autoFire = !_autoFire;

        _fireTimer -= dt;
        if ((_autoFire || _input.IsDown(ConsoleKey.Spacebar)) && _fireTimer <= 0)
        {
            _lasers.Add(new Laser(_player.CenterX, _player.Y - 1));
            _fireTimer = FireCooldownSeconds;
        }

        for (int i = _lasers.Count - 1; i >= 0; i--)
        {
            _lasers[i].Update(dt);
            if (_lasers[i].Y < Layout.PlayTop) // rows above are the header
                _lasers.RemoveAt(i);
        }

        UpdateSpawning(dt, width);
        UpdateAsteroids(dt, width);
        HandleCollisions();
        UpdateExplosions(dt);
        CullOffscreenAsteroids(height);
    }

    private void CullOffscreenAsteroids(int height)
    {
        for (int i = _asteroids.Count - 1; i >= 0; i--)
        {
            if (_asteroids[i].Y > height - 2)
                _asteroids.RemoveAt(i);
        }
    }

    private void UpdateSpawning(double dt, int width)
    {
        _spawnTimer -= dt;
        if (_spawnTimer > 0 || width < 10)
            return;

        // Difficulty ramps with score: shorter intervals, more simultaneous rocks.
        double interval = Math.Max(0.22, 1.0 - _score / 14000.0);
        _spawnTimer = interval * (0.55 + _random.NextDouble() * 0.9);

        int maxActive = Math.Min(6 + _score / 2500, 28);
        if (_asteroids.Count < maxActive)
            _asteroids.Add(Asteroid.CreateRandom(_random, width, _score));
    }

    private void UpdateAsteroids(double dt, int width)
    {
        foreach (var asteroid in _asteroids)
            asteroid.Update(dt, width);
    }

    private void UpdateExplosions(double dt)
    {
        for (int i = _explosions.Count - 1; i >= 0; i--)
        {
            _explosions[i].Update(dt);
            if (_explosions[i].IsDone)
                _explosions.RemoveAt(i);
        }
    }

    private void HandleCollisions()
    {
        // Lasers vs asteroids.
        for (int li = _lasers.Count - 1; li >= 0; li--)
        {
            var laser = _lasers[li];
            int hitIndex = -1;
            for (int ai = 0; ai < _asteroids.Count; ai++)
            {
                if (laser.Crosses(_asteroids[ai]))
                {
                    hitIndex = ai;
                    break;
                }
            }

            if (hitIndex < 0)
                continue;

            var asteroid = _asteroids[hitIndex];
            _asteroids.RemoveAt(hitIndex);
            _lasers.RemoveAt(li);
            _score += asteroid.Points;
            _destroyed++;
            _explosions.Add(new Explosion(asteroid.CenterX, asteroid.CenterY));
        }

        // Player vs asteroids.
        if (_gameOver)
            return;

        for (int ai = 0; ai < _asteroids.Count; ai++)
        {
            if (!_player.Overlaps(_asteroids[ai]))
                continue;

            var asteroid = _asteroids[ai];
            _explosions.Add(new Explosion(asteroid.CenterX, asteroid.CenterY));
            _explosions.Add(new Explosion(_player.CenterX, _player.CenterY));
            _asteroids.RemoveAt(ai);
            _gameOver = true;
            break;
        }
    }

    private void Render()
    {
        if (!_renderer.BeginFrame())
        {
            _renderer.DrawTooSmall();
            return;
        }

        DrawHeader();

        foreach (var asteroid in _asteroids)
            asteroid.Draw(_renderer);
        foreach (var laser in _lasers)
            laser.Draw(_renderer);
        if (!_gameOver)
            _player.Draw(_renderer);
        foreach (var explosion in _explosions)
            explosion.Draw(_renderer);

        if (_gameOver)
            DrawGameOver();

        _renderer.Flush();
    }

    private void DrawHeader()
    {
        int width = _renderer.Width;

        const string company = "★ NO BUGS GIVEN ★ ";
        const string division = "GAMES";
        const string title = "ASTEROID SHOOTER";
        // Anchored to the right edge of the *current* window width.
        string score = "SCORE: " + _score.ToString("D6");

        // "No Bugs Given" is the company, "Games" the division — two tones.
        _renderer.DrawText(1, 0, company, ConsoleColor.Magenta);
        _renderer.DrawText(1 + company.Length, 0, division, ConsoleColor.DarkYellow);
        _renderer.DrawText(width - score.Length - 1, 0, score, ConsoleColor.Yellow);

        _renderer.DrawText(1, 1, title, ConsoleColor.Cyan);

        // Controls on the right of the title line, but only when they fit
        // without overlapping the title (narrow windows get the short form).
        const string controlsFull = "WASD/Arrows Move | SPACE Fire | F Autofire | ESC Quit";
        const string controlsShort = "SPACE Fire | ESC Quit";
        int titleEnd = 1 + title.Length;
        string? controls =
            width - controlsFull.Length - 1 > titleEnd + 2 ? controlsFull :
            width - controlsShort.Length - 1 > titleEnd + 2 ? controlsShort :
            null;
        if (controls != null)
            _renderer.DrawText(width - controls.Length - 1, 1, controls, ConsoleColor.DarkGray);

        // Autofire indicator between title and controls when enabled.
        if (_autoFire)
        {
            const string auto = "[AUTO]";
            int autoX = titleEnd + 2;
            if (autoX + auto.Length < width - (controls?.Length ?? 0) - 2)
                _renderer.DrawText(autoX, 1, auto, ConsoleColor.Green);
        }

        _renderer.DrawText(0, 2, new string('═', width), ConsoleColor.DarkGray);
    }

    private void DrawGameOver()
    {
        string[] content =
        {
            "G A M E   O V E R",
            "",
            $"Final Score: {_score}",
            $"Asteroids Destroyed: {_destroyed}",
            "",
            "[R] Restart     [ESC] Quit",
        };

        int inner = 0;
        foreach (var line in content)
            inner = Math.Max(inner, line.Length);

        int boxWidth = inner + 4;
        int left = Math.Max(0, (_renderer.Width - boxWidth) / 2);
        int top = Math.Max(Layout.PlayTop, (_renderer.Height - content.Length - 2) / 2);

        string border = "+" + new string('-', boxWidth - 2) + "+";
        _renderer.DrawText(left, top, border, ConsoleColor.Red);
        for (int i = 0; i < content.Length; i++)
        {
            string centered = content[i].PadLeft((inner + content[i].Length) / 2).PadRight(inner);
            _renderer.DrawText(left, top + 1 + i, "| " + centered + " |",
                i == 0 ? ConsoleColor.Red : ConsoleColor.Gray);
        }
        _renderer.DrawText(left, top + content.Length + 1, border, ConsoleColor.Red);
    }
}
