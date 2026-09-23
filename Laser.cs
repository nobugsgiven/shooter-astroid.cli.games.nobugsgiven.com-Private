namespace ShooterAstroidConsole;

/// <summary>
/// A single laser bolt traveling straight up. Positions are tracked in
/// double precision so movement stays smooth at any frame rate, and the
/// previous frame's Y is kept so collision tests can sweep the whole path
/// covered this frame (a fast bolt must not tunnel through a 1-row rock
/// when the frame rate dips).
/// </summary>
internal sealed class Laser
{
    private const double Speed = 55.0; // cells per second, upward

    public double X { get; }
    public double Y { get; private set; }
    public double PrevY { get; private set; }

    public Laser(double x, double y)
    {
        X = x;
        Y = y;
        PrevY = y;
    }

    public void Update(double dt)
    {
        PrevY = Y;
        Y -= Speed * dt;
    }

    /// <summary>Swept hit test against the path traveled since the last frame.</summary>
    public bool Crosses(Asteroid asteroid)
    {
        if (X < asteroid.X || X >= asteroid.X + asteroid.Width)
            return false;
        // The bolt moved up from PrevY to Y this frame; did that segment
        // intersect the asteroid's vertical span?
        return PrevY >= asteroid.Y && Y < asteroid.Y + asteroid.Height;
    }

    public void Draw(ConsoleRenderer renderer) =>
        renderer.Set((int)Math.Round(X), (int)Math.Round(Y), '|', ConsoleColor.Yellow);
}
