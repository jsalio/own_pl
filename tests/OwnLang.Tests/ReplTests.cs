using System.IO;
using NUnit.Framework;
using Own_Lang;

namespace OwnLang.Tests;

/// <summary>
/// Tests for the interactive REPL: expression echoing, state persisting across
/// lines, error recovery, and session termination.
/// </summary>
[TestFixture]
public class ReplTests
{
    // Feeds a whole session (newline-separated lines) to the REPL and returns
    // what it wrote to output and error, newline-normalized.
    private static (string output, string error) Feed(string session)
    {
        var outBuffer = new StringWriter();
        var errBuffer = new StringWriter();
        int code = Repl.Run(new StringReader(session), outBuffer, errBuffer);
        Assert.That(code, Is.EqualTo(0), "the REPL should always exit cleanly");
        return (outBuffer.ToString().Replace("\r\n", "\n"),
                errBuffer.ToString().Replace("\r\n", "\n"));
    }

    [Test]
    public void EvaluatesAnExpressionAndEchoesItsValue()
    {
        var (output, error) = Feed("1 + 2;\nexit\n");

        Assert.That(output, Does.Contain("3"));
        Assert.That(error, Is.Empty);
    }

    [Test]
    public void StatePersistsAcrossLines()
    {
        // 'x' declared on one line must still be visible on the next.
        var (output, _) = Feed("let x = 5;\nx + 1;\nexit\n");

        Assert.That(output, Does.Contain("6"));
    }

    [Test]
    public void AssignmentAcrossLinesMutatesPersistentState()
    {
        var (output, _) = Feed("let n = 1;\nn = n + 41;\nn;\nexit\n");

        Assert.That(output, Does.Contain("42"));
    }

    [Test]
    public void ErrorOnOneLineDoesNotEndTheSession()
    {
        // First line is a syntax error; the session must survive and evaluate the next.
        var (output, error) = Feed("1 +;\n40 + 2;\nexit\n");

        Assert.That(error, Does.Contain("error:"));
        Assert.That(output, Does.Contain("42"));
    }

    [Test]
    public void TypedDeclarationIsCheckedAndUsable()
    {
        var (output, _) = Feed("string s = \"hi\";\ns;\nexit\n");

        Assert.That(output, Does.Contain("hi"));
    }

    [Test]
    public void ADeclarationEchoesNothing()
    {
        // A `let` runs for its effect; only expression statements echo a value.
        var (output, error) = Feed("let x = 99;\nexit\n");

        Assert.That(output, Does.Not.Contain("99"));
        Assert.That(error, Is.Empty);
    }

    [Test]
    public void QuitAlsoEndsTheSession()
    {
        // Lines after 'quit' must not run (no echo of 7).
        var (output, _) = Feed("quit\n3 + 4;\n");

        Assert.That(output, Does.Not.Contain("7"));
    }

    [Test]
    public void EndOfInputEndsTheSession()
    {
        // No explicit exit: reaching EOF should terminate cleanly.
        var (output, _) = Feed("2 + 5;\n");

        Assert.That(output, Does.Contain("7"));
    }

    [Test]
    public void DefinesAFunctionAndCallsItOnALaterLine()
    {
        var (output, error) = Feed(
            "function int sq(int n) { return n * n; }\nsq(5);\nexit\n");

        Assert.That(output, Does.Contain("25"));
        Assert.That(error, Is.Empty);
    }

    [Test]
    public void RedefiningAFunctionReplacesIt()
    {
        var (output, _) = Feed(
            "function int f(int n) { return n * n; }\nf(3);\n" +   // 9
            "function int f(int n) { return n + n; }\nf(3);\n" +   // 6
            "exit\n");

        Assert.That(output, Does.Contain("9"));
        Assert.That(output, Does.Contain("6"));
    }

    [Test]
    public void DefiningAFunctionEchoesNothing()
    {
        var (output, error) = Feed(
            "function int id(int n) { return n; }\nexit\n");

        // The definition itself prints nothing (no echoed value); it is registered.
        Assert.That(output, Does.Not.Contain("id"));
        Assert.That(error, Is.Empty);
    }

    [Test]
    public void AFunctionCanCallAnotherDefinedFunction()
    {
        var (output, _) = Feed(
            "function int dbl(int n) { return n + n; }\n" +
            "function int quad(int n) { return dbl(dbl(n)); }\n" +
            "quad(5);\nexit\n");

        Assert.That(output, Does.Contain("20"));
    }

    [Test]
    public void PreludeIsAvailableInTheRepl()
    {
        // Math comes from the prelude, so it works without defining anything.
        var (output, _) = Feed("Math.Abs(0 - 9);\nexit\n");
        Assert.That(output, Does.Contain("9"));
    }
}
