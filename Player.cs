namespace ShooterAstroidConsole;

/// <summary>
/// The player's ship. Movement is momentum-based: held keys accelerate the
/// ship up to a cruise speed, and when input stops the ship glides to a
/// halt under friction instead of stopping dead. Besides feeling natural,
/// this makes diagonals work even though terminals only auto-repeat the
/// most recently pressed key — pressing Up while gliding right curves the
/// ship because the rightward velocity outlives the Right key's events.
/// Moves horizontally across the whole playfield and vertically within
/// roughly the bottom 30% of it.
/// </summary>
internal sealed class Player
{
    public static readonly string[] Sprite =
    {
        @"  /\  ",
        @" /==\ ",
        @"/*||*\",
    };

    public const int SpriteWidth = 6;
    public const int SpriteHeight = 3;

    private const double MaxSpeedX = 30.0; // cells per second
    private const double MaxSpeedY = 20.0;
    private const double AccelX = 95.0;    // cells per second^2 (~0.3s to cruise)
    private const double AccelY = 65.0;
    private const double Friction = 3.2;   // exponential decay per second when no input

    private double _vx;
    private double _vy;

    public double X { get; private set; }
    public double Y { get; private set; }

    public double CenterX => X + SpriteWidth / 2.0;
    public double CenterY => Y + SpriteHeight / 2.0;

    public void Reset(int width, int height)
    {
        X = Math.Max(0, (width - SpriteWidth) / 2.0);
        Y = MaxY(height);
        _vx = 0;
        _vy = 0;
    }

    // Rows above Layout.PlayTop are the header; the last row is reserved
    // by the renderer.
    private static int MaxY(int height) => Math.Max(Layout.PlayTop, height - 2 - SpriteHeight);

    private static int MinY(int height)
    {
        int range = Math.Max(4, (int)Math.Round(height * 0.30));
        return Math.Max(Layout.PlayTop, MaxY(height) - range);
    }

    public void Update(double dt, InputManager input, int width, int height)
    {
        int dx = 0, dy = 0;
        if (input.IsDown(ConsoleKey.LeftArrow, ConsoleKey.A)) dx -= 1;
        if (input.IsDown(ConsoleKey.RightArrow, ConsoleKey.D)) dx += 1;
        if (input.IsDown(ConsoleKey.UpArrow, ConsoleKey.W)) dy -= 1;
        if (input.IsDown(ConsoleKey.DownArrow, ConsoleKey.S)) dy += 1;

        // Accelerate while a direction is held; otherwise bleed off speed.
        if (dx != 0)
            _vx += dx * AccelX * dt;
        else
            _vx *= Math.Exp(-Friction * dt);

        if (dy != 0)
            _vy += dy * AccelY * dt;
        else
            _vy *= Math.Exp(-Friction * dt);

        _vx = Math.Clamp(_vx, -MaxSpeedX, MaxSpeedX);
        _vy = Math.Clamp(_vy, -MaxSpeedY, MaxSpeedY);

        X += _vx * dt;
        Y += _vy * dt;

        double maxX = Math.Max(0, width - SpriteWidth);
        if (X <= 0 || X >= maxX)
            _vx = 0; // don't keep pushing into the wall
        X = Math.Clamp(X, 0, maxX);

        double minY = MinY(height), maxY = MaxY(height);
        if (Y <= minY || Y >= maxY)
            _vy = 0;
        Y = Math.Clamp(Y, minY, maxY);
    }

    public bool Overlaps(Asteroid asteroid)
    {
        // Slightly forgiving hitbox: inset from the sprite edges.
        double left = X + 1;
        double right = X + SpriteWidth - 1;
        double top = Y + 0.5;
        double bottom = Y + SpriteHeight;

        return asteroid.X < right
            && asteroid.X + asteroid.Width > left
            && asteroid.Y < bottom
            && asteroid.Y + asteroid.Height > top;
    }

    public void Draw(ConsoleRenderer renderer) =>
        renderer.DrawSprite((int)Math.Round(X), (int)Math.Round(Y), Sprite, ConsoleColor.Cyan);
}
