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
                      loop when(x < 3) { term.out(x); x = x + 1; }"),
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
                          n = n + 1;
                      }"),
            Is.EqualTo("0\n1"));
    }

    [Test]
    public void AssignmentMutatesExistingVariable()
    {
        Assert.That(
            RunMain(@"let x = 1; x = 5; term.out(x);"),
            Is.EqualTo("5"));
    }

    [Test]
    public void AssignmentToUndeclaredVariableThrows()
    {
        Assert.That(() => RunMain(@"x = 5;"), Throws.Exception);
    }

    [Test]
    public void LetInsideBlockIsLocalAndDoesNotLeak()
    {
        // 'secreto' se declara dentro del when; fuera del bloque no existe.
        Assert.That(
            () => RunMain(@"when(true) { let secreto = 42; } term.out(secreto);"),
            Throws.Exception);
    }

    [Test]
    public void RangeLoopCounterDoesNotLeakAfterLoop()
    {
        // El contador 'i' vive en el scope del loop; afuera ya no existe.
        Assert.That(
            () => RunMain(@"loop[i: 1...3] { term.out(i); } term.out(i);"),
            Throws.Exception);
    }

    [Test]
    public void AssignmentInsideLoopReachesOuterScope()
    {
        // La asignación (no 'let') muta la variable declarada afuera.
        Assert.That(
            RunMain(@"let total = 0;
                      loop[i: 1...3] { total = total + i; }
                      term.out(total);"),
            Is.EqualTo("6"));
    }

    [Test]
    public void UserFunctionReturnsValue()
    {
        Assert.That(
            Run(@"def program {
                    function empty Main() { term.out(suma(2, 3)); }
                    function int suma(int a, int b) { return a + b; }
                  }"),
            Is.EqualTo("5"));
    }

    [Test]
    public void UserFunctionArgumentsAreCoercedToParamType()
    {
        // El argumento se valida contra el tipo del parámetro (como una decl tipada).
        Assert.That(
            () => Run(@"def program {
                          function empty Main() { term.out(f(""hola"")); }
                          function int f(int a) { return a; }
                        }"),
            Throws.Exception);
    }

    [Test]
    public void UserFunctionArityMismatchThrows()
    {
        Assert.That(
            () => Run(@"def program {
                          function empty Main() { term.out(suma(1)); }
                          function int suma(int a, int b) { return a + b; }
                        }"),
            Throws.Exception);
    }

    [Test]
    public void VoidFunctionRunsForItsEffect()
    {
        Assert.That(
            Run(@"def program {
                    function empty Main() { saluda(); }
                    function empty saluda() { term.out(""hola""); }
                  }"),
            Is.EqualTo("hola"));
    }

    [Test]
    public void FunctionScopeIsLexicalNotCallerScope()
    {
        // 'usa' NO ve las variables locales de 'Main': el scope de la llamada
        // es hijo del global, no del que llama.
        Assert.That(
            () => Run(@"def program {
                          function empty Main() { let secreto = 1; usa(); }
                          function empty usa() { term.out(secreto); }
                        }"),
            Throws.Exception);
    }

    [Test]
    public void ReturnExitsFunctionEarly()
    {
        Assert.That(
            Run(@"def program {
                    function empty Main() { term.out(pick(true)); }
                    function int pick(bool b) {
                        when(b) { return 1; }
                        return 2;
                    }
                  }"),
            Is.EqualTo("1"));
    }

    [Test]
    public void LogicalAndEvaluates()
    {
        Assert.That(Eval("true && false"), Is.EqualTo("False"));
        Assert.That(Eval("true && true"), Is.EqualTo("True"));
    }

    [Test]
    public void LogicalOrEvaluates()
    {
        Assert.That(Eval("false || true"), Is.EqualTo("True"));
        Assert.That(Eval("false || false"), Is.EqualTo("False"));
    }

    [Test]
    public void LogicalNotEvaluates()
    {
        Assert.That(Eval("!true"), Is.EqualTo("False"));
        Assert.That(Eval("!false"), Is.EqualTo("True"));
    }

    [Test]
    public void LogicalAndShortCircuitsAndSkipsRightSide()
    {
        // La derecha (1/0) haría MathError; el cortocircuito la evita porque left es false.
        Assert.That(Eval("false && (1 / 0 == 1)"), Is.EqualTo("False"));
    }

    [Test]
    public void LogicalOrShortCircuitsAndSkipsRightSide()
    {
        Assert.That(Eval("true || (1 / 0 == 1)"), Is.EqualTo("True"));
    }

    [Test]
    public void LogicalOperandsMustBeBoolean()
    {
        Assert.That(() => Eval("5 && true"), Throws.Exception);
    }

    [Test]
    public void LogicalAndBindsTighterThanOr()
    {
        // true || (false && false) -> true (&& antes que ||)
        Assert.That(Eval("true || false && false"), Is.EqualTo("True"));
    }

    [Test]
    public void NotBindsTighterThanEquality()
    {
        // (!true) == false -> false == false -> true
        Assert.That(Eval("!true == false"), Is.EqualTo("True"));
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
    public void DoubleDeclarationStoresValue()
    {
        Assert.That(RunMain(@"double d = 3.14; term.out(d);"), Is.EqualTo("3.14"));
    }

    [Test]
    public void FloatSuffixLiteralStoresValue()
    {
        Assert.That(RunMain(@"float f = 1.5f; term.out(f);"), Is.EqualTo("1.5"));
    }

    [Test]
    public void IntPromotesToDoubleInMixedOp()
    {
        Assert.That(Eval("2.0 + 3"), Is.EqualTo("5"));
    }

    [Test]
    public void FloatDivisionIsNotTruncated()
    {
        Assert.That(Eval("7.0 / 2"), Is.EqualTo("3.5"));   // vs 7/2 = 3 (entero)
    }

    [Test]
    public void FloatDivisionByZeroDoesNotThrow()
    {
        // IEEE: 1.0 / 0.0 -> Infinity, sin excepción
        Assert.That(() => Eval("1.0 / 0.0"), Throws.Nothing);
    }

    [Test]
    public void DecimalAssignedToIntegerThrows()
    {
        Assert.That(() => RunMain("int x = 3.14;"), Throws.TypeOf<OverflowError>());
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
