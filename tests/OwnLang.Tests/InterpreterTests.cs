using System;
using System.IO;
using NUnit.Framework;
using Own_Lang.Internal;
using Own_Lang.Internal.Contracts;
using Own_Lang.Internal.Error;

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

    // Wraps statements in a Main body and returns what the program printed.
    private static string RunMain(string body)
        => Run($"def program {{ function empty Main() {{ {body} }} }}");

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
    public void EvaluatesRelationalOperators()
    {
        Assert.That(Eval("1 < 2"), Is.EqualTo("True"));
        Assert.That(Eval("2 <= 2"), Is.EqualTo("True"));
        Assert.That(Eval("5 > 9"), Is.EqualTo("False"));
        Assert.That(Eval("9 >= 9"), Is.EqualTo("True"));
    }

    [Test]
    public void EvaluatesEqualityOperators()
    {
        Assert.That(Eval("3 == 3"), Is.EqualTo("True"));
        Assert.That(Eval("3 != 4"), Is.EqualTo("True"));
        Assert.That(Eval("3 == 4"), Is.EqualTo("False"));
    }

    [Test]
    public void EvaluatesBooleanLiterals()
    {
        Assert.That(Eval("true"), Is.EqualTo("True"));
        Assert.That(Eval("false"), Is.EqualTo("False"));
    }

    [Test]
    public void ComparisonHasLowerPrecedenceThanArithmetic()
    {
        // se evalúa como (1 + 2) == 3  y  (2 * 3) > 5
        Assert.That(Eval("1 + 2 == 3"), Is.EqualTo("True"));
        Assert.That(Eval("2 * 3 > 5"), Is.EqualTo("True"));
    }

    [Test]
    public void WhenTrueExecutesBody()
    {
        Assert.That(RunMain(@"when(true) { term.out(""si""); }"), Is.EqualTo("si"));
    }

    [Test]
    public void WhenFalseSkipsBody()
    {
        Assert.That(RunMain(@"when(false) { term.out(""no""); }"), Is.Empty);
    }

    [Test]
    public void WhenFalseRunsElseBranch()
    {
        Assert.That(
            RunMain(@"when(false) { term.out(""a""); } else { term.out(""b""); }"),
            Is.EqualTo("b"));
    }

    [Test]
    public void ElseWhenChainSelectsMatchingBranch()
    {
        Assert.That(
            RunMain(@"when(false) { term.out(""a""); }
                      else when(true) { term.out(""b""); }
                      else { term.out(""c""); }"),
            Is.EqualTo("b"));
    }

    [Test]
    public void WhenConditionWithComparison()
    {
        Assert.That(RunMain(@"when(1 + 2 == 3) { term.out(""ok""); }"), Is.EqualTo("ok"));
    }

    [Test]
    public void WhenConditionMustBeBoolean()
    {
        Assert.That(() => RunMain("when(5) { }"), Throws.Exception);
    }

    [Test]
    public void RangeLoopIteratesInclusive()
    {
        // loop[i: 1...3] recorre 1, 2, 3
        Assert.That(
            RunMain(@"loop[i: 1...3] { term.out(i); }"),
            Is.EqualTo("1\n2\n3"));
    }

    [Test]
    public void WhileLoopRunsWhileConditionHolds()
    {
        Assert.That(
            RunMain(@"let x = 0;
                      loop when(x < 3) { term.out(x); let x = x + 1; }"),
            Is.EqualTo("0\n1\n2"));
    }

    [Test]
    public void InfiniteLoopExitsWithStop()
    {
        Assert.That(
            RunMain(@"let n = 0;
                      loop {
                          when(n == 2) { stop; }
                          term.out(n);
                          let n = n + 1;
                      }"),
            Is.EqualTo("0\n1"));
    }

    [Test]
    public void TypedStringDeclarationStoresValue()
    {
        Assert.That(
            RunMain(@"string s = ""jorge""; term.out(s);"),
            Is.EqualTo("jorge"));
    }

    [Test]
    public void StringConcatenation()
    {
        Assert.That(Eval("\"a\" + \"b\""), Is.EqualTo("ab"));
    }

    [Test]
    public void NumberIsCoercedToStringOnConcat()
    {
        // en cualquier orden: el número se convierte con ToString()
        Assert.That(Eval("\"n\" + 35"), Is.EqualTo("n35"));
        Assert.That(Eval("35 + \"n\""), Is.EqualTo("35n"));
    }

    [Test]
    public void PlusStaysIntegerWhenNoStringInvolved()
    {
        // 1 + 2 sigue siendo suma entera, NO "12"
        Assert.That(Eval("1 + 2"), Is.EqualTo("3"));
    }

    [Test]
    public void TypeMismatchInTypedDeclarationThrows()
    {
        Assert.That(() => RunMain("string x = 5;"), Throws.Exception);
    }

    [Test]
    public void TypedBoolDeclarationStoresValue()
    {
        Assert.That(RunMain(@"bool b = 3 < 5; term.out(b);"), Is.EqualTo("True"));
    }

    [Test]
    public void BoolTypeMismatchThrows()
    {
        Assert.That(() => RunMain("bool b = 5;"), Throws.Exception);
    }

    [Test]
    public void TypedCharDeclarationStoresValue()
    {
        Assert.That(RunMain(@"char c = 'J'; term.out(c);"), Is.EqualTo("J"));
    }

    [Test]
    public void CharTypeMismatchThrows()
    {
        Assert.That(() => RunMain("char c = 5;"), Throws.Exception);
    }

    [Test]
    public void IntDeclarationStoresValue()
    {
        Assert.That(RunMain(@"int a = 5; term.out(a);"), Is.EqualTo("5"));
    }

    [Test]
    public void UintCoercesFromIntLiteral()
    {
        Assert.That(RunMain(@"uint b = 5; term.out(b);"), Is.EqualTo("5"));
    }

    [Test]
    public void MixedUintAndIntArithmeticGivesResult()
    {
        // uint + int -> se opera en int
        Assert.That(RunMain(@"uint b = 5; term.out(b + 1);"), Is.EqualTo("6"));
    }

    [Test]
    public void UintArithmetic()
    {
        Assert.That(RunMain(@"uint c = 10; uint d = 3; term.out(c - d);"), Is.EqualTo("7"));
    }

    [Test]
    public void UintOutOfRangeThrowsOverflow()
    {
        // 0 - 1 = -1, no cabe en uint
        Assert.That(() => RunMain("uint x = 0 - 1;"), Throws.TypeOf<OverflowError>());
    }

    [Test]
    public void ArithmeticOverflowThrows()
    {
        Assert.That(() => Eval("2000000000 + 2000000000"), Throws.TypeOf<OverflowError>());
    }

    [Test]
    public void LongStoresBigValue()
    {
        // literal auto-ensanchado a long (no cabe en int)
        Assert.That(RunMain(@"long a = 3000000000; term.out(a);"), Is.EqualTo("3000000000"));
    }

    [Test]
    public void IntAndLongMixPromotesToLong()
    {
        Assert.That(RunMain(@"int i = 5; long l = 10; term.out(i + l);"), Is.EqualTo("15"));
    }

    [Test]
    public void LongAdditionDoesNotOverflow()
    {
        // ambos operandos son long -> cabe, no desborda (a diferencia de int)
        Assert.That(Eval("3000000000 + 3000000000"), Is.EqualTo("6000000000"));
    }

    [Test]
    public void DivisionByZeroThrowsMathError()
    {
        Assert.That(() => Eval("1 / 0"), Throws.TypeOf<MathError>());
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
