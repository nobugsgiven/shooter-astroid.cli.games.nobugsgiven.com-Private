namespace ShooterAstroidConsole;

internal enum AsteroidSize
{
    Small,
    Medium,
    Large,
}

/// <summary>
/// A falling rock. Size, sprite, speed, horizontal drift and point value are
/// all randomized at spawn. Bigger rocks fall slower and are worth more.
/// </summary>
internal sealed class Asteroid
{
    private static readonly string[] SmallSprites = { "o", "O", "@" };

    private static readonly string[][] MediumSprites =
    {
        new[] { "(O)" },
        new[] { "(o)" },
        new[] { "[O]" },
    };

    private static readonly string[][] LargeSprites =
    {
        new[] { @" /OO\ ", @"(OOOO)", @" \OO/ " },
        new[] { @" /@@\ ", @"(@@@@)", @" \@@/ " },
    };

    public double X { get; private set; }
    public double Y { get; private set; }
    public double VelocityX { get; private set; }
    public double VelocityY { get; }
    public string[] Sprite { get; }
    public int Width { get; }
    public int Height { get; }
    public int Points { get; }
    public AsteroidSize Size { get; }
    public ConsoleColor Color { get; }

    public double CenterX => X + Width / 2.0;
    public double CenterY => Y + Height / 2.0;

    // Internal (rather than private) so the headless self-test can place rocks exactly.
    internal Asteroid(double x, double y, double vx, double vy, string[] sprite,
        int points, AsteroidSize size, ConsoleColor color)
    {
        X = x;
        Y = y;
        VelocityX = vx;
        VelocityY = vy;
        Sprite = sprite;
        Width = sprite[0].Length;
        Height = sprite.Length;
        Points = points;
        Size = size;
        Color = color;
    }

    public static Asteroid CreateRandom(Random random, int fieldWidth, int score)
    {
        double roll = random.NextDouble();
        AsteroidSize size = roll < 0.50
            ? AsteroidSize.Small
            : roll < 0.82
                ? AsteroidSize.Medium
                : AsteroidSize.Large;

        // Everything speeds up a little as the score climbs.
        double difficulty = 1.0 + Math.Min(1.2, score / 25000.0);

        string[] sprite;
        int basePoints;
        double speed;
        ConsoleColor color;

        switch (size)
        {
            case AsteroidSize.Small:
                sprite = new[] { SmallSprites[random.Next(SmallSprites.Length)] };
                basePoints = 100;
                speed = (11.0 + random.NextDouble() * 8.0) * difficulty;
                color = ConsoleColor.DarkGray;
                break;
            case AsteroidSize.Medium:
                sprite = MediumSprites[random.Next(MediumSprites.Length)];
                basePoints = 250;
                speed = (7.5 + random.NextDouble() * 5.0) * difficulty;
                color = ConsoleColor.Gray;
                break;
            default:
                sprite = LargeSprites[random.Next(LargeSprites.Length)];
                basePoints = 500;
                speed = (4.5 + random.NextDouble() * 3.5) * difficulty;
                color = ConsoleColor.White;
                break;
        }

        int width = sprite[0].Length;
        double x = 1 + random.NextDouble() * Math.Max(1, fieldWidth - width - 2);
        double drift = (random.NextDouble() * 2.0 - 1.0) * (size == AsteroidSize.Large ? 2.0 : 4.0);

        // Faster (more dangerous) rocks pay a small bonus on top of the size value.
        int points = basePoints + (int)Math.Round(speed * 4);

        return new Asteroid(x, Layout.PlayTop, drift, speed, sprite, points, size, color);
    }

    public void Update(double dt, int fieldWidth)
    {
        Y += VelocityY * dt;
        X += VelocityX * dt;

        // Drift bounces off the side walls (also keeps rocks inside after a resize).
        double maxX = Math.Max(0, fieldWidth - Width);
        if (X < 0)
        {
            X = 0;
            VelocityX = Math.Abs(VelocityX);
        }
        else if (X > maxX)
        {
            X = maxX;
            VelocityX = -Math.Abs(VelocityX);
        }
    }

    public void Draw(ConsoleRenderer renderer) =>
        renderer.DrawSprite((int)Math.Round(X), (int)Math.Round(Y), Sprite, Color);
}
