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
        var e = new LineEditor();
        foreach (char c in text) e.Insert(c);
        return e;
    }

    [Test]
    public void InsertBuildsTextAndAdvancesCursor()
    {
        var e = WithText("abc");
        Assert.That(e.Text, Is.EqualTo("abc"));
        Assert.That(e.Cursor, Is.EqualTo(3));
    }

    [Test]
    public void InsertsAtTheCursorPosition()
    {
        var e = WithText("ac");
        e.MoveLeft();          // cursor between a and c
        e.Insert('b');
        Assert.That(e.Text, Is.EqualTo("abc"));
        Assert.That(e.Cursor, Is.EqualTo(2));
    }

    [Test]
    public void CursorMovementIsBounded()
    {
        var e = WithText("ab");
        e.MoveRight();                       // already at end, no-op
        Assert.That(e.Cursor, Is.EqualTo(2));
        e.MoveLeft(); e.MoveLeft(); e.MoveLeft();  // clamps at 0
        Assert.That(e.Cursor, Is.EqualTo(0));
    }

    [Test]
    public void HomeAndEndJumpToTheEnds()
    {
        var e = WithText("hello");
        e.Home();
        Assert.That(e.Cursor, Is.EqualTo(0));
        e.End();
        Assert.That(e.Cursor, Is.EqualTo(5));
    }

    [Test]
    public void BackspaceRemovesBeforeCursor()
    {
        var e = WithText("abc");
        e.Backspace();
        Assert.That(e.Text, Is.EqualTo("ab"));
        Assert.That(e.Cursor, Is.EqualTo(2));
    }

    [Test]
    public void DeleteRemovesAtCursor()
    {
        var e = WithText("abc");
        e.Home();
        e.Delete();
        Assert.That(e.Text, Is.EqualTo("bc"));
        Assert.That(e.Cursor, Is.EqualTo(0));
    }

    [Test]
    public void AcceptReturnsLineRecordsHistoryAndResets()
    {
        var e = WithText("let x = 1;");
        string line = e.Accept();

        Assert.That(line, Is.EqualTo("let x = 1;"));
        Assert.That(e.Text, Is.Empty);
        Assert.That(e.Cursor, Is.EqualTo(0));
        Assert.That(e.History, Has.Count.EqualTo(1));
    }

    [Test]
    public void BlankAndDuplicateLinesAreNotAddedToHistory()
    {
        var e = new LineEditor();
        e.Accept();                       // blank
        foreach (char c in "x;") e.Insert(c);
        e.Accept();                       // "x;"
        foreach (char c in "x;") e.Insert(c);
        e.Accept();                       // duplicate of last

        Assert.That(e.History, Has.Count.EqualTo(1));
    }

    [Test]
    public void UpRecallsPreviousLinesOldestBoundHeld()
    {
        var e = new LineEditor();
        foreach (char c in "first;") e.Insert(c); e.Accept();
        foreach (char c in "second;") e.Insert(c); e.Accept();

        e.HistoryPrev();
        Assert.That(e.Text, Is.EqualTo("second;"));
        e.HistoryPrev();
        Assert.That(e.Text, Is.EqualTo("first;"));
        e.HistoryPrev();                  // already oldest, stays
        Assert.That(e.Text, Is.EqualTo("first;"));
    }

    [Test]
    public void DownWalksForwardAndRestoresTheInProgressLine()
    {
        var e = new LineEditor();
        foreach (char c in "old;") e.Insert(c); e.Accept();

        foreach (char c in "typing") e.Insert(c);   // in-progress, not accepted
        e.HistoryPrev();                             // stashes "typing", shows "old;"
        Assert.That(e.Text, Is.EqualTo("old;"));

        e.HistoryNext();                             // back down to the stash
        Assert.That(e.Text, Is.EqualTo("typing"));
    }

    [Test]
    public void RecalledLineIsEditableAndReAccepts()
    {
        var e = new LineEditor();
        foreach (char c in "a;") e.Insert(c); e.Accept();

        e.HistoryPrev();                  // "a;", cursor at end
        e.Backspace();                    // remove ';'
        e.Insert('b');
        string line = e.Accept();

        Assert.That(line, Is.EqualTo("ab"));
        Assert.That(e.History, Has.Count.EqualTo(2));
    }
}
