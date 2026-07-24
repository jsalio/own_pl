using System;
using System.IO;
using NUnit.Framework;
using Own_Lang.Internal;
using Own_Lang.Internal.Contracts;

namespace OwnLang.Tests;

/// <summary>
/// Stage 3 regression tests: AST -> execution, verified end-to-end through the
/// full pipeline (lexer + parser + interpreter). Output produced by
/// <c>term.out</c> is captured by redirecting <see cref="Console.Out"/>.
/// </summary>
[TestFixture]
public class InterpreterTests
{
    // Runs a full program and returns everything it printed, newline-normalized.
    private static string Run(string source)
    {
        var program = new Parser(new Lexer(source).Tokenize()).Parse();

        TextWriter original = Console.Out;
        var buffer = new StringWriter();
        Console.SetOut(buffer);
        try
        {
            new Interpreter().Interpret(program);
        }
        finally
        {
            Console.SetOut(original);
        }

        return buffer.ToString().Replace("\r\n", "\n").Trim();
    }

    // Wraps a single expression in term.out(...) and returns the printed result.
    private static string Eval(string expression)
        => Run($"def program {{ function empty Main() {{ term.out({expression}); }} }}");

    [Test]
    public void PrintsString()
    {
        Assert.That(Eval("\"hola mundo\""), Is.EqualTo("hola mundo"));
    }

    [Test]
    public void EvaluatesAddition()
    {
        Assert.That(Eval("1 + 2"), Is.EqualTo("3"));
    }

    [Test]
    public void EvaluatesSubtraction()
    {
        Assert.That(Eval("10 - 4"), Is.EqualTo("6"));
    }

    [Test]
    public void RespectsOperatorPrecedence()
    {
        Assert.That(Eval("2 + 3 * 4"), Is.EqualTo("14"));
        Assert.That(Eval("10 - 6 / 2"), Is.EqualTo("7"));
    }

    [Test]
    public void IntegerDivisionTruncates()
    {
        Assert.That(Eval("7 / 2"), Is.EqualTo("3"));
    }

    [Test]
    public void ResolvesVariablesAndSum()
    {
        string source = @"def program {
            function empty Main() {
                let val1 = 1;
                let val2 = 2;
                let result = val1 + val2;
                term.out(result);
            }
        }";

        Assert.That(Run(source), Is.EqualTo("3"));
    }

    [Test]
    public void CanonicalProgramProducesExpectedOutput()
    {
        string source = @"def program {
            function empty Main() {
                let val1 = 1;
                let val2 = 2;
                let result = val1 + val2;
                term.out(""resultado:"");
                term.out(result);
            }
        }";

        Assert.That(Run(source), Is.EqualTo("resultado:\n3"));
    }

    [Test]
    public void UndefinedVariableThrows()
    {
        Assert.That(() => Eval("noExiste"), Throws.Exception);
    }

    [Test]
    public void MissingMainThrows()
    {
        Assert.That(
            () => Run("def program { function empty Otra() { } }"),
            Throws.Exception);
    }

    [Test]
    public void UnknownCallThrows()
    {
        // solo term.out(...) está soportado
        Assert.That(() => Eval("otra.cosa(1)"), Throws.Exception);
    }
}
