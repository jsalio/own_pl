using System;

namespace Own_Lang;

/// <summary>
/// Interactive <see cref="ILineReader"/> for a real terminal: reads key by key,
/// supporting left/right cursor movement, Home/End, Backspace/Delete, insert in
/// the middle, and up/down history recall.
/// </summary>
/// <remarks>
/// All the editing state lives in a single, reused <see cref="LineEditor"/>, so
/// history persists across lines. This class is only the thin console front-end:
/// it maps key presses to <see cref="LineEditor"/> calls and redraws the line
/// after each. It is used only when input is an interactive console (never with a
/// redirected stream), so it can safely drive <see cref="Console"/> directly.
/// <para>
/// v1 assumes the line fits on one terminal row; cursor columns are clamped to the
/// buffer width so a very long line degrades gracefully rather than throwing.
/// </para>
/// </remarks>
internal sealed class ConsoleLineReader : ILineReader
{
    private readonly LineEditor editor = new();

    /// <inheritdoc/>
    public string? ReadLine(string prompt)
    {
        Console.Write(prompt);
        int startColumn = Console.CursorLeft;
        int previousLength = 0;

        while (true)
        {
            ConsoleKeyInfo key = Console.ReadKey(intercept: true);

            // Ctrl+D / Ctrl+Z (or Ctrl+C) end the input stream.
            if ((key.Modifiers & ConsoleModifiers.Control) != 0 &&
                (key.Key == ConsoleKey.D || key.Key == ConsoleKey.Z || key.Key == ConsoleKey.C))
            {
                Console.WriteLine();
                return null;
            }

            switch (key.Key)
            {
                case ConsoleKey.Enter:
                    Console.WriteLine();
                    return editor.Accept();

                case ConsoleKey.LeftArrow: editor.MoveLeft(); break;
                case ConsoleKey.RightArrow: editor.MoveRight(); break;
                case ConsoleKey.UpArrow: editor.HistoryPrev(); break;
                case ConsoleKey.DownArrow: editor.HistoryNext(); break;
                case ConsoleKey.Home: editor.Home(); break;
                case ConsoleKey.End: editor.End(); break;
                case ConsoleKey.Backspace: editor.Backspace(); break;
                case ConsoleKey.Delete: editor.Delete(); break;

                default:
                    if (!char.IsControl(key.KeyChar)) editor.Insert(key.KeyChar);
                    break;
            }

            previousLength = Render(startColumn, previousLength);
        }
    }

    // Repaints the current line in place and positions the caret at the cursor.
    // Returns the length just drawn so the next repaint can erase leftover chars
    // when the line got shorter. Returns the current text length.
    private int Render(int startColumn, int previousLength)
    {
        string text = editor.Text;

        SetColumn(startColumn);
        Console.Write(text);

        // Erase any characters left over from a previously longer line.
        if (text.Length < previousLength)
            Console.Write(new string(' ', previousLength - text.Length));

        SetColumn(startColumn + editor.Cursor);
        return text.Length;
    }

    // Sets the cursor column, clamped to the buffer so a long line never throws.
    private static void SetColumn(int column)
    {
        int max = Math.Max(0, Console.BufferWidth - 1);
        Console.CursorLeft = Math.Min(column, max);
    }
}
