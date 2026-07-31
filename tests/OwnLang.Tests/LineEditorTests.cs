using NUnit.Framework;
using Own_Lang;

namespace OwnLang.Tests;

/// <summary>
/// Tests for the REPL line-editing state: the character buffer with a cursor and
/// the command history. These are the fiddly parts (cursor bounds, insert in the
/// middle, history navigation with a stashed in-progress line).
/// </summary>
[TestFixture]
public class LineEditorTests
{
    private static LineEditor WithText(string text)
    {
        var editor = new LineEditor();
        foreach (char character in text) editor.Insert(character);
        return editor;
    }

    [Test]
    public void InsertBuildsTextAndAdvancesCursor()
    {
        var editor = WithText("abc");
        Assert.That(editor.Text, Is.EqualTo("abc"));
        Assert.That(editor.Cursor, Is.EqualTo(3));
    }

    [Test]
    public void InsertsAtTheCursorPosition()
    {
        var editor = WithText("ac");
        editor.MoveLeft();          // cursor between a and c
        editor.Insert('b');
        Assert.That(editor.Text, Is.EqualTo("abc"));
        Assert.That(editor.Cursor, Is.EqualTo(2));
    }

    [Test]
    public void CursorMovementIsBounded()
    {
        var editor = WithText("ab");
        editor.MoveRight();                       // already at end, no-op
        Assert.That(editor.Cursor, Is.EqualTo(2));
        editor.MoveLeft(); editor.MoveLeft(); editor.MoveLeft();  // clamps at 0
        Assert.That(editor.Cursor, Is.EqualTo(0));
    }

    [Test]
    public void HomeAndEndJumpToTheEnds()
    {
        var editor = WithText("hello");
        editor.Home();
        Assert.That(editor.Cursor, Is.EqualTo(0));
        editor.End();
        Assert.That(editor.Cursor, Is.EqualTo(5));
    }

    [Test]
    public void BackspaceRemovesBeforeCursor()
    {
        var editor = WithText("abc");
        editor.Backspace();
        Assert.That(editor.Text, Is.EqualTo("ab"));
        Assert.That(editor.Cursor, Is.EqualTo(2));
    }

    [Test]
    public void DeleteRemovesAtCursor()
    {
        var editor = WithText("abc");
        editor.Home();
        editor.Delete();
        Assert.That(editor.Text, Is.EqualTo("bc"));
        Assert.That(editor.Cursor, Is.EqualTo(0));
    }

    [Test]
    public void AcceptReturnsLineRecordsHistoryAndResets()
    {
        var editor = WithText("let x = 1;");
        string line = editor.Accept();

        Assert.That(line, Is.EqualTo("let x = 1;"));
        Assert.That(editor.Text, Is.Empty);
        Assert.That(editor.Cursor, Is.EqualTo(0));
        Assert.That(editor.History, Has.Count.EqualTo(1));
    }

    [Test]
    public void BlankAndDuplicateLinesAreNotAddedToHistory()
    {
        var editor = new LineEditor();
        editor.Accept();                       // blank
        foreach (char character in "x;") editor.Insert(character);
        editor.Accept();                       // "x;"
        foreach (char character in "x;") editor.Insert(character);
        editor.Accept();                       // duplicate of last

        Assert.That(editor.History, Has.Count.EqualTo(1));
    }

    [Test]
    public void UpRecallsPreviousLinesOldestBoundHeld()
    {
        var editor = new LineEditor();
        foreach (char character in "first;") editor.Insert(character); editor.Accept();
        foreach (char character in "second;") editor.Insert(character); editor.Accept();

        editor.HistoryPrev();
        Assert.That(editor.Text, Is.EqualTo("second;"));
        editor.HistoryPrev();
        Assert.That(editor.Text, Is.EqualTo("first;"));
        editor.HistoryPrev();                  // already oldest, stays
        Assert.That(editor.Text, Is.EqualTo("first;"));
    }

    [Test]
    public void DownWalksForwardAndRestoresTheInProgressLine()
    {
        var editor = new LineEditor();
        foreach (char character in "old;") editor.Insert(character); editor.Accept();

        foreach (char character in "typing") editor.Insert(character);   // in-progress, not accepted
        editor.HistoryPrev();                             // stashes "typing", shows "old;"
        Assert.That(editor.Text, Is.EqualTo("old;"));

        editor.HistoryNext();                             // back down to the stash
        Assert.That(editor.Text, Is.EqualTo("typing"));
    }

    [Test]
    public void RecalledLineIsEditableAndReAccepts()
    {
        var editor = new LineEditor();
        foreach (char character in "a;") editor.Insert(character); editor.Accept();

        editor.HistoryPrev();                  // "a;", cursor at end
        editor.Backspace();                    // remove ';'
        editor.Insert('b');
        string line = editor.Accept();

        Assert.That(line, Is.EqualTo("ab"));
        Assert.That(editor.History, Has.Count.EqualTo(2));
    }
}
