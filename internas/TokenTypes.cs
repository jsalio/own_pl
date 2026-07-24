namespace Own_Lang.Internal;

/// <summary>
/// Every category of token the lexer can produce. A token's <c>Type</c> is one
/// of these values; for keywords and symbols the type alone carries the full
/// meaning, while for identifiers, numbers and strings the lexeme holds the
/// concrete content.
/// </summary>
public enum TokenType
{
    // Keywords
    /// <summary>The <c>def</c> keyword (program declaration).</summary>
    DEF,
    /// <summary>The <c>function</c> keyword (function declaration).</summary>
    FUNCTION,
    /// <summary>The <c>empty</c> keyword (void return type).</summary>
    EMPTY,
    /// <summary>The <c>let</c> keyword (variable declaration).</summary>
    LET,

    // Symbols
    /// <summary>Left brace <c>{</c>.</summary>
    LBRACE,
    /// <summary>Right brace <c>}</c>.</summary>
    RBRACE,
    /// <summary>Left parenthesis <c>(</c>.</summary>
    LPAREN,
    /// <summary>Right parenthesis <c>)</c>.</summary>
    RPAREN,
    /// <summary>Statement terminator <c>;</c>.</summary>
    SEMICOLON,
    /// <summary>Assignment operator <c>=</c>.</summary>
    EQUAL,
    /// <summary>Addition operator <c>+</c>.</summary>
    PLUS,
    ///<summary>Subtraction operator <c>-</c>.</summary>
    MINUS,
    /// <summary>Member-access operator <c>.</c>.</summary>
    DOT,
    /// <summary>Argument/parameter separator <c>,</c>.</summary>
    COMMA,

    // Literals and identifiers
    /// <summary>An identifier: a variable or function name (e.g. <c>val1</c>).</summary>
    IDENTIFIER,
    /// <summary>An integer literal (e.g. <c>123</c>).</summary>
    NUMBER,
    /// <summary>A string literal, including its surrounding quotes in the lexeme.</summary>
    STRING,

    // Control
    /// <summary>End-of-input marker; the last token in every stream.</summary>
    EOF
}
