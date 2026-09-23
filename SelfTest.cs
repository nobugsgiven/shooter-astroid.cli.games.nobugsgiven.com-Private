namespace ShooterAstroidConsole;

/// <summary>
/// Headless sanity checks for the collision and animation logic, runnable
/// without a terminal: <c>dotnet run -- --selftest</c>. Exits 0 on success.
/// </summary>
internal static class SelfTest
{
    public static int Run()
    {
        int failures = 0;

        // 1. Laser hits a rock it sweeps through at 60 FPS.
        {
            var rock = MakeRock(x: 10, y: 5);
            var laser = new Laser(10, 20);
            bool hit = false;
            for (int i = 0; i < 120 && !hit; i++)
            {
                laser.Update(1.0 / 60.0);
                hit = laser.Crosses(rock);
            }
            Check("laser hits rock at 60fps", hit, ref failures);
        }

        // 2. Same shot at 5 FPS (dt clamp 0.1): the bolt jumps ~5.5 cells per
        //    frame and would tunnel through a 1-row rock without swept tests.
        {
            var rock = MakeRock(x: 10, y: 5);
            var laser = new Laser(10, 20);
            bool hit = false;
            for (int i = 0; i < 10 && !hit; i++)
            {
                laser.Update(0.1);
                hit = laser.Crosses(rock);
            }
            Check("swept test catches low-fps tunneling", hit, ref failures);
        }

        // 3. A laser in the wrong column never hits, however long it flies.
        {
            var rock = MakeRock(x: 10, y: 5);
            var laser = new Laser(30, 20);
            bool hit = false;
            for (int i = 0; i < 120 && !hit; i++)
            {
                laser.Update(1.0 / 60.0);
                hit = laser.Crosses(rock);
            }
            Check("laser in wrong column misses", !hit, ref failures);
        }

        // 4. Drifting rocks bounce off the walls and stay in bounds.
        {
            var rock = MakeRock(x: 0, y: 5, vx: -8);
            for (int i = 0; i < 600; i++)
                rock.Update(1.0 / 60.0, fieldWidth: 40);
            Check("rock stays in bounds after wall bounce",
                rock.X >= 0 && rock.X + rock.Width <= 40, ref failures);
        }

        // 5. A rock falling straight onto the ship is detected as a hit,
        //    even at low frame rates (large dt steps).
        {
            var player = new Player();
            player.Reset(width: 44, height: 30);
            var rock = new Asteroid(player.CenterX - 0.5, 1, vx: 0, vy: 15,
                sprite: new[] { "o" }, points: 100, AsteroidSize.Small, ConsoleColor.DarkGray);
            bool hit = false;
            for (int i = 0; i < 100 && !hit; i++)
            {
                rock.Update(0.1, fieldWidth: 44);
                hit = player.Overlaps(rock);
            }
            Check("rock falling onto ship hits player", hit, ref failures);
        }

        // 6. Spawn distribution covers the whole field (no clustering).
        {
            var rng = new Random(42);
            int left = 0, right = 0;
            for (int i = 0; i < 2000; i++)
            {
                var rock = Asteroid.CreateRandom(rng, fieldWidth: 44, score: 0);
                if (rock.X < 22) left++; else right++;
            }
            Check("spawn X covers both halves of the field",
                left > 800 && right > 800, ref failures);
        }

        // 7. End-to-end headless simulation: stationary ship at the bottom of
        //    a 44x30 field, rocks spawned with the game's own factory and
        //    cadence; the ship must be hit within a reasonable game time.
        {
            var rng = new Random(1234);
            var player = new Player();
            player.Reset(width: 44, height: 30);
            var rocks = new List<Asteroid>();
            double spawnTimer = 0.8, elapsed = 0;
            bool hit = false;
            const double dt = 1.0 / 60.0;

            while (elapsed < 120 && !hit)
            {
                elapsed += dt;
                spawnTimer -= dt;
                if (spawnTimer <= 0)
                {
                    spawnTimer = Math.Max(0.22, 1.0) * (0.55 + rng.NextDouble() * 0.9);
                    if (rocks.Count < 6)
                        rocks.Add(Asteroid.CreateRandom(rng, 44, 0));
                }
                for (int i = rocks.Count - 1; i >= 0; i--)
                {
                    rocks[i].Update(dt, 44);
                    if (rocks[i].Y > 28)
                        rocks.RemoveAt(i);
                    else if (player.Overlaps(rocks[i]))
                        hit = true;
                }
            }
            Check("stationary ship is hit within 120s of game time", hit, ref failures);
            Console.WriteLine($"       (hit after {elapsed:F1}s of simulated game time)");
        }

        // 8. Held-key smoothing: one press event, a 0.4s OS repeat-delay gap,
        //    then an 80ms repeat stream. The key must read "down" the whole
        //    time (no move-stop-move stutter), and release promptly after
        //    the stream ends.
        {
            var input = new InputManager();
            const ConsoleKey key = ConsoleKey.D;

            input.OnKeyEvent(key, 0.0);                 // physical press
            bool gapCovered = input.IsDownAt(0.4, key); // repeat delay not over yet

            double t = 0.5;                             // repeat stream begins
            bool streamSmooth = true;
            while (t < 2.0)
            {
                input.OnKeyEvent(key, t);
                t += 0.08;
            }
            for (double probe = 0.0; probe <= 2.0; probe += 0.01)
            {
                if (!input.IsDownAt(probe, key))
                    streamSmooth = false;
            }

            bool released = !input.IsDownAt(2.0 + 0.5, key);

            Check("initial press bridges OS repeat delay", gapCovered, ref failures);
            Check("held key stays down through repeat stream", streamSmooth, ref failures);
            Check("key releases promptly after stream ends", released, ref failures);
        }

        // 9. Momentum movement: speed ramps up while held; pressing Up after
        //    holding Right produces a diagonal (Right's velocity persists
        //    even though its key events stop); ship glides to a stop.
        {
            var input = new InputManager();
            var player = new Player();
            player.Reset(width: 80, height: 30);
            const double dt = 1.0 / 60;
            double t = 0;
            double x0 = player.X;

            // Hold Right for 1s (press + 80ms repeat stream).
            input.OnKeyEvent(ConsoleKey.D, 0);
            double distEarly = 0, distLate = 0;
            for (int i = 0; i < 60; i++)
            {
                t += dt;
                if (i % 5 == 0) input.OnKeyEvent(ConsoleKey.D, t);
                input.Tick(t);
                player.Update(dt, input, 80, 30);
                if (i == 12) distEarly = player.X - x0;          // first ~0.22s
                if (i == 36) distLate = player.X - x0 - distEarly; // next ~0.4s
            }
            Check("ship accelerates (faster later in the hold)",
                distEarly < distLate * 0.54, ref failures);
            Check("ship covered real distance while held",
                player.X - x0 > 10, ref failures);

            // Now hold Up instead (Right events stop, as on a real OS).
            // Momentum must carry the ship right while it climbs: a diagonal.
            double xBeforeUp = player.X, yBeforeUp = player.Y;
            for (int i = 0; i < 18; i++) // 0.3s
            {
                t += dt;
                if (i % 5 == 0) input.OnKeyEvent(ConsoleKey.W, t);
                input.Tick(t);
                player.Update(dt, input, 80, 30);
            }
            Check("pressing Up while gliding Right moves diagonally",
                player.Y < yBeforeUp && player.X > xBeforeUp, ref failures);

            // Release everything: the ship glides, then settles.
            for (int i = 0; i < 120; i++) // 2s
            {
                t += dt;
                input.Tick(t);
                player.Update(dt, input, 80, 30);
            }
            double xSettled = player.X, ySettled = player.Y;
            for (int i = 0; i < 30; i++) // 0.5s more
            {
                t += dt;
                input.Tick(t);
                player.Update(dt, input, 80, 30);
            }
            Check("ship glides to a stop after release",
                Math.Abs(player.X - xSettled) < 0.05 && Math.Abs(player.Y - ySettled) < 0.05,
                ref failures);
        }

        // 10. Explosion plays all frames and then reports done.
        {
            var explosion = new Explosion(10, 10);
            for (int i = 0; i < 60; i++)
                explosion.Update(1.0 / 60.0);
            Check("explosion finishes", explosion.IsDone, ref failures);
        }

        Console.WriteLine(failures == 0
            ? "Self-test: all checks passed."
            : $"Self-test: {failures} check(s) FAILED.");
        return failures == 0 ? 0 : 1;
    }

    private static Asteroid MakeRock(double x, double y, double vx = 0) =>
        new(x, y, vx, vy: 0, sprite: new[] { "o" }, points: 100,
            AsteroidSize.Small, ConsoleColor.DarkGray);

    private static void Check(string name, bool ok, ref int failures)
    {
        if (!ok)
            failures++;
        Console.WriteLine($"  {(ok ? "PASS" : "FAIL")}  {name}");
    }
}
