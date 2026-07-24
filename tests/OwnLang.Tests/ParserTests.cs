using NUnit.Framework;
using Own_Lang.Internal;

namespace OwnLang.Tests;

/// <summary>
/// Stage 2 regression tests: tokens -> AST.
/// Verifies the shape of the tree the <see cref="Parser"/> builds, especially
/// operator precedence and associativity.
/// </summary>
[TestFixture]
public class ParserTests
{
    private static ProgramDecl Parse(string source)
        => new Parser(new Lexer(source).Tokenize()).Parse();

    // Wraps an expression in a minimal program and returns its parsed tree,
    // so precedence can be asserted without boilerplate in every test.
    private static Expr ParseExpression(string expression)
    {
        string source =
            $"def program {{ function empty Main() {{ let x = {expression}; }} }}";
        var program = Parse(source);
        var function = (FunctionDecl)program.Declarations[0];
        var varDecl = (VarDecl)function.Body.Statements[0];
        return varDecl.Initializer;
    }

    [Test]
    public void ParsesProgramWithMainFunction()
    {
        var program = Parse("def program { function empty Main() { } }");

        Assert.That(program.Name, Is.EqualTo("program"));
        Assert.That(program.Declarations, Has.Count.EqualTo(1));

        var function = (FunctionDecl)program.Declarations[0];
        Assert.That(function.Name, Is.EqualTo("Main"));
        Assert.That(function.ReturnType, Is.EqualTo("empty"));
        Assert.That(function.Parameters, Is.Empty);
    }

    [Test]
    public void ParsesVarDeclaration()
    {
        var initializer = ParseExpression("42");

        Assert.That(initializer, Is.TypeOf<NumberLiteral>());
        Assert.That(((NumberLiteral)initializer).Value, Is.EqualTo(42));
    }

    [Test]
    public void AdditionIsLeftAssociative()
    {
        // 1 + 2 + 3  =>  ((1 + 2) + 3)
        var expr = ParseExpression("1 + 2 + 3");

        var outer = (Binary)expr;
        Assert.That(outer.Operator, Is.EqualTo(TokenType.PLUS));
        Assert.That(outer.Right, Is.TypeOf<NumberLiteral>());   // el 3
        Assert.That(outer.Left, Is.TypeOf<Binary>());           // (1 + 2)
    }

    [Test]
    public void MultiplicationBindsTighterThanAddition()
    {
        // 2 + 3 * 4  =>  2 + (3 * 4)
        var expr = ParseExpression("2 + 3 * 4");

        var outer = (Binary)expr;
        Assert.That(outer.Operator, Is.EqualTo(TokenType.PLUS));
        Assert.That(outer.Left, Is.TypeOf<NumberLiteral>());    // el 2

        var inner = (Binary)outer.Right;                        // (3 * 4)
        Assert.That(inner.Operator, Is.EqualTo(TokenType.STAR));
    }

    [Test]
    public void ComparisonBindsLooserThanArithmetic()
    {
        // 1 + 2 == 3  =>  (1 + 2) == 3
        var expr = ParseExpression("1 + 2 == 3");

        var outer = (Binary)expr;
        Assert.That(outer.Operator, Is.EqualTo(TokenType.EQUAL_EQUAL));
        Assert.That(outer.Right, Is.TypeOf<NumberLiteral>());   // el 3
        Assert.That(outer.Left, Is.TypeOf<Binary>());           // (1 + 2)

        var addition = (Binary)outer.Left;
        Assert.That(addition.Operator, Is.EqualTo(TokenType.PLUS));
    }

    [Test]
    public void ParsesBooleanLiteral()
    {
        var expr = ParseExpression("true");

        Assert.That(expr, Is.TypeOf<BooleanLiteral>());
        Assert.That(((BooleanLiteral)expr).Value, Is.True);
    }

    [Test]
    public void ParsesMemberCall()
    {
        // term.out(result)  =>  Call(MemberAccess(Variable term, "out"), [Variable result])
        var expr = ParseExpression("term.out(result)");

        var call = (Call)expr;
        Assert.That(call.Arguments, Has.Count.EqualTo(1));
        Assert.That(call.Arguments[0], Is.TypeOf<Variable>());

        var member = (MemberAccess)call.Callee;
        Assert.That(member.Member, Is.EqualTo("out"));
        Assert.That(((Variable)member.Object).Name, Is.EqualTo("term"));
    }

    [Test]
    public void ParsesCallWithMultipleArguments()
    {
        var expr = ParseExpression("f(a, b, c)");

        var call = (Call)expr;
        Assert.That(call.Arguments, Has.Count.EqualTo(3));
    }

    [Test]
    public void MissingSemicolonThrows()
    {
        Assert.That(
            () => Parse("def program { function empty Main() { let x = 1 } }"),
            Throws.Exception);
    }

    [Test]
    public void TrailingTokensAfterProgramThrow()
    {
        Assert.That(
            () => Parse("def program { function empty Main() { } } extra"),
            Throws.Exception);
    }
}
