using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Own_Lang.Internal;

namespace OwnLang.Tests;

/// <summary>
/// Stage 1 regression tests: source text -> tokens.
/// Each test asserts on the token stream produced by <see cref="Lexer"/>.
/// </summary>
[TestFixture]
public class LexerTests
{
    // Helper: tokenizes and returns the types only (EOF trimmed off), which is
    // what most structural assertions care about.
    private static List<TokenType> TokenTypesOf(string source)
    {
        var tokens = new Lexer(source).Tokenize();
        return tokens.Select(t => t.Type).Where(t => t != TokenType.EOF).ToList();
    }

    private static Token FirstToken(string source)
        => new Lexer(source).Tokenize()[0];

    [Test]
    public void DetectString()
    {
        Token token = FirstToken("\"resultado:\"");

        Assert.That(token.Type, Is.EqualTo(TokenType.STRING));
        // el lexeme conserva las comillas (se quitan después, en el parser)
        Assert.That(token.Lexeme, Is.EqualTo("\"resultado:\""));
    }

    [Test]
    public void DetectVals()
    {
        // números y nombres de variables
        Token number = FirstToken("123");
        Assert.That(number.Type, Is.EqualTo(TokenType.NUMBER));
        Assert.That(number.Lexeme, Is.EqualTo("123"));

        Token identifier = FirstToken("val1");
        Assert.That(identifier.Type, Is.EqualTo(TokenType.IDENTIFIER));
        Assert.That(identifier.Lexeme, Is.EqualTo("val1"));
    }

    [Test]
    public void DetectCodeBlock()
    {
        Assert.That(TokenTypesOf("{ }"), Is.EqualTo(new[]
        {
            TokenType.LBRACE,
            TokenType.RBRACE,
        }));
    }

    [Test]
    public void DetectKeywordsVsIdentifiers()
    {
        // 'let' es keyword; 'result' es identificador
        Assert.That(TokenTypesOf("let result"), Is.EqualTo(new[]
        {
            TokenType.LET,
            TokenType.IDENTIFIER,
        }));
    }

    [Test]
    public void DetectArithmeticOperators()
    {
        Assert.That(TokenTypesOf("+ - * /"), Is.EqualTo(new[]
        {
            TokenType.PLUS,
            TokenType.MINUS,
            TokenType.STAR,
            TokenType.SLASH,
        }));
    }

    [Test]
    public void DetectComparisonOperators()
    {
        Assert.That(TokenTypesOf("== != < <= > >="), Is.EqualTo(new[]
        {
            TokenType.EQUAL_EQUAL,
            TokenType.BANG_EQUAL,
            TokenType.LESS,
            TokenType.LESS_EQUAL,
            TokenType.GREATER,
            TokenType.GREATER_EQUAL,
        }));
    }

    [Test]
    public void TwoCharOperatorIsASingleToken()
    {
        // '<=' debe ser UN token LESS_EQUAL, no LESS + EQUAL
        Assert.That(TokenTypesOf("<="), Is.EqualTo(new[] { TokenType.LESS_EQUAL }));
    }

    [Test]
    public void DetectBooleanKeywords()
    {
        Assert.That(TokenTypesOf("true false"), Is.EqualTo(new[]
        {
            TokenType.TRUE,
            TokenType.FALSE,
        }));
    }

    [Test]
    public void DetectLoopTokens()
    {
        Assert.That(TokenTypesOf("loop stop [ ] :"), Is.EqualTo(new[]
        {
            TokenType.LOOP,
            TokenType.STOP,
            TokenType.LBRACKET,
            TokenType.RBRACKET,
            TokenType.COLON,
        }));
    }

    [Test]
    public void RangeIsASingleTokenButDotStaysDot()
    {
        // '...' -> un RANGE ;  '.' solo -> DOT (para term.out)
        Assert.That(TokenTypesOf("..."), Is.EqualTo(new[] { TokenType.RANGE }));
        Assert.That(TokenTypesOf("."), Is.EqualTo(new[] { TokenType.DOT }));
    }

    [Test]
    public void DetectStringTypeKeyword()
    {
        // 'string' (el tipo) es TYPE_STRING, distinto de STRING (el literal)
        Assert.That(TokenTypesOf("string"), Is.EqualTo(new[] { TokenType.TYPE_STRING }));
        Assert.That(TokenTypesOf("\"hola\""), Is.EqualTo(new[] { TokenType.STRING }));
    }

    [Test]
    public void DetectBoolTypeKeyword()
    {
        Assert.That(TokenTypesOf("bool"), Is.EqualTo(new[] { TokenType.TYPE_BOOL }));
    }

    [Test]
    public void DetectCharTypeKeywordAndLiteral()
    {
        // 'char' es el tipo (TYPE_CHAR); 'a' es el literal (CHAR)
        Assert.That(TokenTypesOf("char"), Is.EqualTo(new[] { TokenType.TYPE_CHAR }));
        Assert.That(TokenTypesOf("'a'"), Is.EqualTo(new[] { TokenType.CHAR }));
    }

    [Test]
    public void DetectIntAndUintTypeKeywords()
    {
        Assert.That(TokenTypesOf("int"),  Is.EqualTo(new[] { TokenType.TYPE_INT }));
        Assert.That(TokenTypesOf("uint"), Is.EqualTo(new[] { TokenType.TYPE_UINT }));
    }

    [Test]
    public void DetectVarDeclarationSequence()
    {
        Assert.That(TokenTypesOf("let val1 = 1;"), Is.EqualTo(new[]
        {
            TokenType.LET,
            TokenType.IDENTIFIER,
            TokenType.EQUAL,
            TokenType.NUMBER,
            TokenType.SEMICOLON,
        }));
    }

    [Test]
    public void IgnoresWhitespaceAndTracksLines()
    {
        var tokens = new Lexer("let\n  x").Tokenize();

        Assert.That(tokens[0].Line, Is.EqualTo(1)); // let
        Assert.That(tokens[1].Line, Is.EqualTo(2)); // x, en la segunda línea
    }

    [Test]
    public void AlwaysEmitsEofAsLastToken()
    {
        var tokens = new Lexer("1 + 2").Tokenize();

        Assert.That(tokens.Last().Type, Is.EqualTo(TokenType.EOF));
    }

    [Test]
    public void UnterminatedStringThrows()
    {
        Assert.That(() => new Lexer("\"sin cerrar").Tokenize(),
            Throws.Exception);
    }

    [Test]
    public void UnexpectedCharacterThrows()
    {
        Assert.That(() => new Lexer("@").Tokenize(),
            Throws.Exception);
    }
}
