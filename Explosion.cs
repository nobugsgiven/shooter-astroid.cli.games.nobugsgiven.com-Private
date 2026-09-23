namespace ShooterAstroidConsole;

/// <summary>
/// Short animated explosion played where an asteroid (or the player) died.
/// Advances with game time inside the normal update loop — no Thread.Sleep.
/// Colors fade Yellow -&gt; Red -&gt; DarkRed as the frames play out.
/// </summary>
internal sealed class Explosion
{
    private static readonly (string[] Rows, ConsoleColor Color)[] Frames =
    {
        (new[] { "*" }, ConsoleColor.Yellow),
        (new[] { @"\|/", @"-*-", @"/|\" }, ConsoleColor.Yellow),
        (new[] { ". * .", "* . *", ". * ." }, ConsoleColor.Red),
        (new[] { ".   .", "     ", ".   ." }, ConsoleColor.DarkRed),
    };

    private const double FrameDuration = 0.09; // seconds per frame

    private readonly double _centerX;
    private readonly double _centerY;
    private double _elapsed;

    public bool IsDone => _elapsed >= Frames.Length * FrameDuration;

    public Explosion(double centerX, double centerY)
    {
        _centerX = centerX;
        _centerY = centerY;
    }

    public void Update(double dt) => _elapsed += dt;

    public void Draw(ConsoleRenderer renderer)
    {
        int index = Math.Min((int)(_elapsed / FrameDuration), Frames.Length - 1);
        var (rows, color) = Frames[index];

        int left = (int)Math.Round(_centerX - rows[0].Length / 2.0);
        int top = (int)Math.Round(_centerY - rows.Length / 2.0);
        renderer.DrawSprite(left, top, rows, color);
    }
}
