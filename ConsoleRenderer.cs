using System.Text;

namespace ShooterAstroidConsole;

/// <summary>
/// Double-buffered console renderer. All drawing goes into a back buffer;
/// <see cref="Flush"/> writes only the cells that changed since the previous
/// frame, which avoids full-screen clears (and the flicker they cause).
/// Every write is bounds-checked, so sprites can never crash on the edges
/// or mid-resize.
/// </summary>
internal sealed class ConsoleRenderer
{
    public const int MinWidth = 40;
    public const int MinHeight = 14;

    private const ConsoleColor DefaultColor = ConsoleColor.Gray;

    private struct Cell
    {
        public char Ch;
        public ConsoleColor Fg;
    }

    private Cell[] _back = Array.Empty<Cell>();
    private Cell[] _front = Array.Empty<Cell>();
    private readonly StringBuilder _runBuilder = new();

    public int Width { get; private set; }
    public int Height { get; private set; }

    /// <summary>
    /// Prepares a new frame: tracks window resizes and clears the back buffer.
    /// Returns false when the window is too small to render the game.
    /// </summary>
    public bool BeginFrame()
    {
        int w, h;
        try
        {
            w = Console.WindowWidth;
            h = Console.WindowHeight;
        }
        catch (IOException)
        {
            return false;
        }

        if (w <= 0 || h <= 0)
            return false;

        if (w != Width || h != Height)
        {
            Width = w;
            Height = h;
            _back = new Cell[w * h];
            _front = new Cell[w * h];
            InvalidateFront();
            try { Console.Clear(); }
            catch (IOException) { /* a clear failure is cosmetic only */ }
        }

        var blank = new Cell { Ch = ' ', Fg = DefaultColor };
        Array.Fill(_back, blank);

        return Width >= MinWidth && Height >= MinHeight;
    }

    public void Set(int x, int y, char ch, ConsoleColor fg)
    {
        if ((uint)x >= (uint)Width || (uint)y >= (uint)Height)
            return;
        int i = y * Width + x;
        _back[i].Ch = ch;
        _back[i].Fg = fg;
    }

    public void DrawText(int x, int y, string text, ConsoleColor fg)
    {
        for (int i = 0; i < text.Length; i++)
            Set(x + i, y, text[i], fg);
    }

    public void DrawSprite(int x, int y, string[] rows, ConsoleColor fg)
    {
        for (int row = 0; row < rows.Length; row++)
            DrawText(x, y + row, rows[row], fg);
    }

    /// <summary>
    /// Writes only the cells that differ from the previous frame, grouped
    /// into same-color runs to minimize cursor moves and color switches.
    /// </summary>
    public void Flush()
    {
        try
        {
            for (int y = 0; y < Height; y++)
            {
                // Never emit the bottom-right cell: writing it can scroll
                // the whole screen up on several terminals.
                int rowWidth = y == Height - 1 ? Width - 1 : Width;
                int rowStart = y * Width;
                int x = 0;

                while (x < rowWidth)
                {
                    int i = rowStart + x;
                    if (_back[i].Ch == _front[i].Ch && _back[i].Fg == _front[i].Fg)
                    {
                        x++;
                        continue;
                    }

                    int runStart = x;
                    ConsoleColor color = _back[i].Fg;
                    _runBuilder.Clear();

                    while (x < rowWidth)
                    {
                        i = rowStart + x;
                        if (_back[i].Fg != color)
                            break;
                        if (_back[i].Ch == _front[i].Ch && _back[i].Fg == _front[i].Fg)
                            break;
                        _runBuilder.Append(_back[i].Ch);
                        x++;
                    }

                    Console.SetCursorPosition(runStart, y);
                    Console.ForegroundColor = color;
                    Console.Write(_runBuilder);
                }
            }

            Array.Copy(_back, _front, _back.Length);
        }
        catch (Exception ex) when (ex is IOException or ArgumentOutOfRangeException)
        {
            // Most likely a resize landed mid-flush. Force a full repaint next frame.
            try { Console.Clear(); } catch { }
            InvalidateFront();
        }
    }

    /// <summary>Fallback message for windows below the minimum size.</summary>
    public void DrawTooSmall()
    {
        const string message = "Terminal too small - please resize";
        try
        {
            Console.SetCursorPosition(0, 0);
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.Write(Width > 0 && Width < message.Length ? message[..Width] : message);
        }
        catch { /* nothing sensible to do here */ }
    }

    private void InvalidateFront()
    {
        // A character that the game never draws, forcing a full repaint.
        for (int i = 0; i < _front.Length; i++)
        {
            _front[i].Ch = '\0';
            _front[i].Fg = DefaultColor;
        }
    }
}
