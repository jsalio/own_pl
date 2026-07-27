using System.Collections.Generic;
using Own_Lang.Internal.Contracts;

namespace Own_Lang.Internal;

/// <summary>
/// Stage 1 implementation: scans source text into tokens with a single
/// left-to-right pass, using a character cursor (<c>current</c>) and a marker
/// (<c>start</c>) for the token currently being read. Keywords are told apart
/// from identifiers via the static <see cref="Keywords"/> table.
/// </summary>
internal sealed class Lexer : ILexer
{
    private static readonly Dictionary<string, TokenType> Keywords = new()
    {
        ["def"] = TokenType.DEF,
        ["function"] = TokenType.FUNCTION,
        ["empty"] = TokenType.EMPTY,
        ["let"] = TokenType.LET,
        ["true"] = TokenType.TRUE,
        ["false"] = TokenType.FALSE,
        ["when"] = TokenType.WHEN,
        ["else"] = TokenType.ELSE,
        ["loop"] = TokenType.LOOP,
        ["stop"] = TokenType.STOP,
        ["string"]= TokenType.TYPE_STRING,
        ["bool"]=TokenType.TYPE_BOOL,
        ["char"] = TokenType.TYPE_CHAR,
    };

    private readonly string source;
    private readonly List<Token> tokens = new();

    private int start = 0;
    private int current = 0;
    private int line = 1;
    private int column = 1;

    private const char CHARDEFINITION = '\'';

    /// <summary>Creates a lexer over the given source text.</summary>
    /// <param name="source">The full source code to tokenize.</param>
    public Lexer(string source)
    {
        this.source = source;
    }

    /// <inheritdoc/>
    public IReadOnlyList<Token> Tokenize()
    {
        while (!IsAtEnd())
        {
            start = current;
            ScanToken();
        }

        start = current;
        AddToken(TokenType.EOF);
        return tokens;
    }

    private void ScanToken()
    {
        char c = Advance();
        switch (c)
        {
            case '{': AddToken(TokenType.LBRACE); break;
            case '}': AddToken(TokenType.RBRACE); break;
            case '(': AddToken(TokenType.LPAREN); break;
            case ')': AddToken(TokenType.RPAREN); break;
            case ';': AddToken(TokenType.SEMICOLON); break;
            case '=': AddToken(Match('=') ? TokenType.EQUAL_EQUAL : TokenType.EQUAL); break;
            case '!': AddToken(Match('=') ? TokenType.BANG_EQUAL : TokenType.BANG); break;
            case '<': AddToken(Match('=') ? TokenType.LESS_EQUAL : TokenType.LESS); break;
            case '>': AddToken(Match('=') ? TokenType.GREATER_EQUAL : TokenType.GREATER); break;
            case '+': AddToken(TokenType.PLUS); break;
            case '-': AddToken(TokenType.MINUS); break;
            case '.':
                if (Match('.'))
                {
                    // vimos ".."; exigimos el tercer punto para formar "..."
                    if (!Match('.'))
                    {
                        throw new System.Exception(
                            $"Se esperaba '...' en la línea {line}, columna {column}");
                    }
                    AddToken(TokenType.RANGE);
                }
                else
                {
                    AddToken(TokenType.DOT);
                }
                break;
            case ',': AddToken(TokenType.COMMA); break;
            case '*': AddToken(TokenType.STAR); break;
            case '/': AddToken(TokenType.SLASH); break;
            case '[': AddToken(TokenType.LBRACKET); break;
            case ']': AddToken(TokenType.RBRACKET); break;
            case ':': AddToken(TokenType.COLON); break;


            case '"': String(); break;

            case ' ':
            case '\t':
            case '\r':
                // espacios en blanco: se ignoran
                break;

            case '\n':
                line++;
                column = 1;
                break;

            case CHARDEFINITION:
                Char();
                break;

            default:
                if (char.IsDigit(c))
                {
                    Number();
                }
                else if (IsAlpha(c))
                {
                    Identifier();
                }
                else
                {
                    throw new System.Exception(
                        $"Carácter inesperado '{c}' en la línea {line}, columna {column}");
                }
                break;
        }
    }

    private void Number()
    {
        while (char.IsDigit(Peek()))
        {
            Advance();
        }

        AddToken(TokenType.NUMBER);
    }

    private void String()
    {
        while (Peek() != '"' && !IsAtEnd())
        {
            if (Peek() == '\n')
            {
                line++;
                column = 1;
            }
            Advance();
        }

        if (IsAtEnd())
        {
            throw new System.Exception(
                $"String sin cerrar en la línea {line}, columna {column}");
        }

        Advance(); // consume la comilla de cierre "

        AddToken(TokenType.STRING);
    }

    private void Char()
    {
        if (IsAtEnd() || Peek() ==CHARDEFINITION)
            throw new Exception($"Char empty in line {line}...");
        Advance();
        if (!Match(CHARDEFINITION))
            throw new Exception($"Wait \"'\" for close...");
        AddToken(TokenType.CHAR);
    }

    private void Identifier()
    {
        while (IsAlphaNumeric(Peek()))
        {
            Advance();
        }

        string text = source.Substring(start, current - start);
        TokenType type = Keywords.TryGetValue(text, out var keyword)
            ? keyword
            : TokenType.IDENTIFIER;

        AddToken(type);
    }

    private static bool IsAlpha(char c)
        => char.IsLetter(c) || c == '_';

    private static bool IsAlphaNumeric(char c)
        => IsAlpha(c) || char.IsDigit(c);

    private bool IsAtEnd() => current >= source.Length;

    private char Advance()
    {
        column++;
        return source[current++];
    }

    private char Peek()
    {
        if (IsAtEnd()) return '\0';
        return source[current];
    }

    private void AddToken(TokenType type)
    {
        string lexeme = source.Substring(start, current - start);
        tokens.Add(new Token(type, lexeme, line, column));
    }

    private bool Match(char expected)
    {
        if (IsAtEnd()) return false;
        if (source[current] != expected) return false;
        current++;
        column++;
        return true;
    }
}
