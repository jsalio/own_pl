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
    #region State

    /// <summary>
    /// Reserved words and their corresponding token.
    /// </summary>
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
        ["string"] = TokenType.TYPE_STRING,
        ["bool"] = TokenType.TYPE_BOOL,
        ["char"] = TokenType.TYPE_CHAR,
        ["int"] = TokenType.TYPE_INT,
        ["uint"] = TokenType.TYPE_UINT,
        ["long"] = TokenType.TYPE_LONG,
        ["ulong"] = TokenType.TYPE_ULONG,
        ["double"] = TokenType.TYPE_DOUBLE,
        ["float"] = TokenType.TYPE_FLOAT,
        ["return"] = TokenType.RETURN,
        ["contract"] = TokenType.CONTRACT,
        ["module"] = TokenType.MODULE,
        ["external"] = TokenType.EXTERNAL,
    };

    /// <summary>
    /// Source text.
    /// </summary>
    private readonly string source;

    /// <summary>
    /// List of tokens.
    /// </summary>
    private readonly List<Token> tokens = new();

    /// <summary>
    /// Start position of the current token.
    /// </summary>
    private int start = 0;

    /// <summary>
    /// Current cursor position.
    /// </summary>
    private int current = 0;

    /// <summary>
    /// Current line.
    /// </summary>
    private int line = 1;

    /// <summary>
    /// Current column.
    /// </summary>
    private int column = 1;

    /// <summary>
    /// Delimiter character for char literals.
    /// </summary>
    private const char CHARDEFINITION = '\'';

    #endregion

    #region Public API

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

    #endregion

    #region Scanning (one token at a time)

    /// <summary>
    /// Scan next token.
    /// </summary>
    /// <remarks>
    /// Scans one token at a time, using the <c>current</c> cursor and the
    /// <c>start</c> marker to delimit the current token.
    /// </remarks>
    /// <exception cref="System.Exception">
    /// Throws an exception if an unexpected character is encountered.
    /// </exception>
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
                    // saw ".."; require the third dot to form "..."
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
                // whitespace: ignored
                break;

            case '\n':
                line++;
                column = 1;
                break;

            case CHARDEFINITION:
                Char();
                break;

            case '&':
                if (!Match('&'))
                    throw new System.Exception("se esperaba '&&' en la línea " + line + ", columna " + column);
                AddToken(TokenType.AND);
                break;

            case '|':
                if (!Match('|'))
                    throw new Exception("se esperaba '||' en la línea " + line + ", columna " + column);
                AddToken(TokenType.OR);
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

    /// <summary>
    /// Reads consecutive digits to form a number.
    /// </summary>
    /// <remarks>
    /// If there are digits after the dot, forms a decimal number.
    /// If there is an 'f' or 'F' suffix, forms a float.
    /// </remarks>
    /// <exception cref="System.Exception">
    /// Throws an exception if an unexpected character is encountered.
    /// </exception>
    private void Number()
    {
        while (char.IsDigit(Peek()))
        {
            Advance();
        }

        // Fractional part: only if the '.' is followed by a digit
        // (so '1...3' and 'Term.out' are not mistaken for a decimal).
        if (Peek() == '.' && char.IsDigit(PeekNext()))
        {
            Advance(); // consume the '.'
            while (char.IsDigit(Peek()))
            {
                Advance();
            }
        }

        // Float suffix: 3.14f or 5f
        if (Peek() == 'f' || Peek() == 'F')
        {
            Advance();
        }

        AddToken(TokenType.NUMBER);
    }

    /// <summary>
    /// Scans a string.
    /// </summary>
    /// <remarks>
    /// Reads consecutive characters until a closing quote is found.
    /// If there is a line break, increments the line counter.
    /// </remarks>
    /// <exception cref="System.Exception">
    /// Throws an exception if an unexpected character is encountered.
    /// </exception>
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

        Advance(); // consume the closing quote "

        AddToken(TokenType.STRING);
    }

    /// <summary>
    /// Scans a char.
    /// </summary>
    /// <remarks>
    /// Reads characters until a single quote is found.
    /// If there is a line break, increments the line counter.
    /// </remarks>
    /// <exception cref="System.Exception">
    /// Throws an exception if an unexpected character is encountered.
    /// </exception>
    private void Char()
    {
        if (IsAtEnd() || Peek() == CHARDEFINITION)
            throw new Exception($"Char empty in line {line}...");
        Advance();
        if (!Match(CHARDEFINITION))
            throw new Exception($"Wait \"'\" for close...");
        AddToken(TokenType.CHAR);
    }

    /// <summary>
    /// Scans an identifier.
    /// </summary>
    /// <remarks>
    /// Reads consecutive characters until a non-alphanumeric character is found.
    /// </remarks>
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

    #endregion

    #region Character classification

    /// <summary>
    /// Checks if a character is an alphabet letter.
    /// </summary>
    /// <param name="c">The character to check.</param>
    /// <returns>True if the character is a letter, false otherwise.</returns>
    private static bool IsAlpha(char c)
        => char.IsLetter(c) || c == '_';

    /// <summary>
    /// Checks if a character is an alphabet letter or a digit.
    /// </summary>
    /// <param name="c">The character to check.</param>
    /// <returns>True if the character is a letter or a digit, false otherwise.</returns>
    private static bool IsAlphaNumeric(char c)
        => IsAlpha(c) || char.IsDigit(c);

    #endregion

    #region Cursor & helpers

    /// <summary>
    /// Checks if the lexer has reached the end of the source code.
    /// </summary>
    /// <returns>True if the lexer is at the end of the source code, false otherwise.</returns>
    private bool IsAtEnd() => current >= source.Length;

    /// <summary>
    /// Advances the lexer to the next character.
    /// </summary>
    /// <returns>The next character in the source code.</returns>
    private char Advance()
    {
        column++;
        return source[current++];
    }

    /// <summary>
    /// Peeks at the next character without advancing the lexer.
    /// </summary>
    /// <returns>The next character in the source code, or '\0' if the lexer is at the end of the source code.</returns>
    private char Peek()
    {
        if (IsAtEnd()) return '\0';
        return source[current];
    }

    /// <summary>
    /// Peeks at the next character without advancing the lexer.
    /// </summary>
    /// <returns>The next character in the source code, or '\0' if the lexer is at the end of the source code.</returns>
    private char PeekNext()
    {
        if (current + 1 >= source.Length) return '\0';
        return source[current + 1];
    }

    /// <summary>
    /// Adds a token to the list of tokens.
    /// </summary>
    /// <param name="type">The type of the token.</param>
    private void AddToken(TokenType type)
    {
        string lexeme = source.Substring(start, current - start);
        tokens.Add(new Token(type, lexeme, line, column));
    }

    /// <summary>
    /// Checks if the next character in the source code matches the expected character.
    /// </summary>
    /// <param name="expected">The character to match.</param>
    /// <returns>True if the next character matches the expected character, false otherwise.</returns>
    private bool Match(char expected)
    {
        if (IsAtEnd()) return false;
        if (source[current] != expected) return false;
        current++;
        column++;
        return true;
    }

    #endregion
}
