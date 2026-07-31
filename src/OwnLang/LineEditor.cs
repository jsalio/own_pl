using System.Collections.Generic;
using System.Text;

namespace Own_Lang;

/// <summary>
/// The in-memory state behind interactive line editing: the line being typed
/// (a character buffer plus a cursor) and the history of accepted lines.
/// </summary>
/// <remarks>
/// Pure state, with no console I/O, so the fiddly parts — cursor bounds, insert
/// in the middle, and history navigation — are unit-testable on their own. A
/// console front-end (<see cref="ConsoleLineReader"/>) drives it by translating
/// key presses into these method calls and rendering <see cref="Text"/> after each.
/// <para>
/// Two buffers cooperate: the <see cref="StringBuilder"/> holds the current line
/// (edited via the cursor), and the history <see cref="List{T}"/> holds past
/// lines (walked via up/down). When you walk up into history the in-progress line
/// is stashed so walking back down restores it.
/// </para>
/// </remarks>
internal sealed class LineEditor
{
    private readonly StringBuilder buffer = new();
    private readonly List<string> history = new();
    private int cursor;
    // Points into history for up/down navigation; == history.Count means "editing
    // a fresh line, not browsing history".
    private int historyIndex;
    // The fresh line saved when the user first walks up into history.
    private string stash = "";

    /// <summary>The current line text.</summary>
    public string Text => buffer.ToString();

    /// <summary>The cursor position (0..<see cref="Text"/>.Length).</summary>
    public int Cursor => cursor;

    /// <summary>The accepted lines, oldest first.</summary>
    public IReadOnlyList<string> History => history;

    /// <summary>Inserts a character at the cursor and advances past it.</summary>
    public void Insert(char c)
    {
        buffer.Insert(cursor, c);
        cursor++;
    }

    /// <summary>Moves the cursor one place left (no-op at the start).</summary>
    public void MoveLeft()
    {
        if (cursor > 0) cursor--;
    }

    /// <summary>Moves the cursor one place right (no-op at the end).</summary>
    public void MoveRight()
    {
        if (cursor < buffer.Length) cursor++;
    }

    /// <summary>Moves the cursor to the start of the line.</summary>
    public void Home() => cursor = 0;

    /// <summary>Moves the cursor to the end of the line.</summary>
    public void End() => cursor = buffer.Length;

    /// <summary>Deletes the character before the cursor (Backspace).</summary>
    public void Backspace()
    {
        if (cursor > 0)
        {
            buffer.Remove(cursor - 1, 1);
            cursor--;
        }
    }

    /// <summary>Deletes the character at the cursor (Delete).</summary>
    public void Delete()
    {
        if (cursor < buffer.Length) buffer.Remove(cursor, 1);
    }

    /// <summary>Recalls the previous line from history (Up).</summary>
    /// <remarks>
    /// The first step up stashes whatever is being typed, so <see cref="HistoryNext"/>
    /// can bring it back. Stops at the oldest entry.
    /// </remarks>
    public void HistoryPrev()
    {
        if (history.Count == 0) return;
        if (historyIndex == history.Count) stash = buffer.ToString();
        if (historyIndex > 0) historyIndex--;
        Replace(history[historyIndex]);
    }

    /// <summary>Recalls the next (more recent) line, or the stashed line (Down).</summary>
    /// <remarks>Walking past the newest entry restores the in-progress line stashed by <see cref="HistoryPrev"/>.</remarks>
    public void HistoryNext()
    {
        if (historyIndex == history.Count) return;
        historyIndex++;
        Replace(historyIndex == history.Count ? stash : history[historyIndex]);
    }

    /// <summary>
    /// Finishes the line: records it in history, resets the buffer, and returns it.
    /// </summary>
    /// <remarks>
    /// Blank lines and immediate duplicates of the last entry are not added to
    /// history (so pressing Enter repeatedly, or re-running the same command, does
    /// not clutter it), but the line is still returned to the caller.
    /// </remarks>
    /// <returns>The finished line text.</returns>
    public string Accept()
    {
        string line = buffer.ToString();
        if (line.Trim().Length > 0 && (history.Count == 0 || history[^1] != line))
            history.Add(line);

        buffer.Clear();
        cursor = 0;
        historyIndex = history.Count;
        stash = "";
        return line;
    }

    // Replaces the whole buffer (used by history navigation) and parks the cursor
    // at the end of the recalled text.
    private void Replace(string text)
    {
        buffer.Clear();
        buffer.Append(text);
        cursor = buffer.Length;
    }
}
