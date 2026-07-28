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
    /// <summary>The <c>when</c> keyword (conditional).</summary>
    WHEN,
    /// <summary>The <c>else</c> keyword (conditional alternative).</summary>
    ELSE,
    /// <summary>The <c>loop</c> keyword (loop, in all its forms).</summary>
    LOOP,
    /// <summary>The <c>stop</c> keyword (break out of the innermost loop).</summary>
    STOP,

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
    /// <summary>Multiplication operator <c>*</c>.</summary>
    STAR,
    /// <summary>Division operator <c>/</c>.</summary>
    SLASH,
    /// <summary>Member-access operator <c>.</c>.</summary>
    DOT,
    /// <summary>Argument/parameter separator <c>,</c>.</summary>
    COMMA,
    /// <summary>Left bracket <c>[</c> (opens a counted-loop range).</summary>
    LBRACKET,
    /// <summary>Right bracket <c>]</c> (closes a counted-loop range).</summary>
    RBRACKET,
    /// <summary>Colon <c>:</c> (binds the counter name in a counted loop).</summary>
    COLON,
    /// <summary>Inclusive range operator <c>...</c> (counted-loop bounds).</summary>
    RANGE,
    /// <summary>A char literal (e.g. <c>'a'</c>).</summary>
    CHAR,

    // Literals and identifiers
    /// <summary>An identifier: a variable or function name (e.g. <c>val1</c>).</summary>
    IDENTIFIER,
    /// <summary>An integer literal (e.g. <c>123</c>).</summary>
    NUMBER,
    /// <summary>A string literal, including its surrounding quotes in the lexeme.</summary>
    STRING,

    //Data type
    /// <summary>The <c>string</c> keyword (string type).</summary>
    TYPE_STRING,
    /// <summary>The <c>bool</c> keyword (boolean type).</summary>
    TYPE_BOOL,
    /// <summary>The <c>char</c> keyword (char type).</summary>
    TYPE_CHAR,
    /// <summary>The <c>int</c> keyword (int type).</summary>
    TYPE_INT,
    /// <summary>The <c>uint</c> keyword (uint type).</summary>
    TYPE_UINT,
    /// <summary>The <c>long</c> keyword (long type).</summary>
    TYPE_LONG,
    /// <summary>The <c>ulong</c> keyword (ulong type).</summary>
    TYPE_ULONG,
    // TYPE_FLOAT,
    // TYPE_DOUBLE,
    // TYPE_DECIMAL,


    //Comparison operators
    /// <summary>Equal operator</summary>
    EQUAL_EQUAL,
    /// <summary>Less than operator</summary>
    LESS_EQUAL,
    /// <summary>Greater than operator</summary>
    GREATER_EQUAL,
    /// <summary>Not equal operator</summary>
    BANG_EQUAL,
    /// <summary>Less than operator</summary>
    LESS,
    /// <summary>Greater than operator</summary>
    GREATER,
    /// <summary>Not operator</summary>
    BANG,

    // Booleans
    /// <summary>Boolean true value</summary>
    TRUE,
    /// <summary>Boolean false value</summary>
    FALSE,

    // Control
    /// <summary>End-of-input marker; the last token in every stream.</summary>
    EOF,

}
