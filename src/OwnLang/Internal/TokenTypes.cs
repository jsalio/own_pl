namespace Own_Lang.Internal;

/// <summary>
/// Every category of token the lexer can produce. A token's <c>Type</c> is one
/// of these values; for keywords and symbols the type alone carries the full
/// meaning, while for identifiers, numbers and strings the lexeme holds the
/// concrete content.
/// </summary>
/// <remarks>
/// This enum is the shared vocabulary between the lexer (which assigns a type to
/// each token) and the parser (which decides grammar rules by matching on it), so
/// adding a language feature usually starts by adding a value here. The
/// <c>#region</c> groups are purely organizational — the compiler ignores them —
/// but they mirror how the lexer produces tokens: keywords and type names are
/// resolved via the lexer's keyword table, symbols and multi-character operators
/// are scanned character by character, and literals/identifiers carry their value
/// in the lexeme. <see cref="EOF"/> is special: it terminates every stream and is
/// how the parser knows input has ended.
/// </remarks>
public enum TokenType
{
    #region Keywords

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
    /// <summary>The <c>contract</c> keyword (contract declaration).</summary>
    CONTRACT,
    /// <summary>The <c>module</c> keyword (module declaration).</summary>
    MODULE,
    /// <summary>The <c>external</c> keyword (external function declaration).</summary>
    EXTERNAL,
    /// <summary>The <c>yield</c> keyword (yield a value from a function).</summary>
    YIELD,
    /// <summary>The <c>return</c> keyword (return a value from a function).</summary>
    RETURN,

    #endregion

    #region Symbols

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

    #endregion

    #region Literals and identifiers

    /// <summary>An identifier: a variable or function name (e.g. <c>val1</c>).</summary>
    IDENTIFIER,
    /// <summary>A numeric literal — integer, decimal, or <c>f</c>-suffixed float (e.g. <c>123</c>, <c>3.14</c>, <c>5f</c>); the exact kind is classified later from the lexeme.</summary>
    NUMBER,
    /// <summary>A string literal, including its surrounding quotes in the lexeme.</summary>
    STRING,

    #endregion

    #region Data types

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
    /// <summary>The <c>double</c> keyword (64-bit IEEE 754 type).</summary>
    TYPE_DOUBLE,
    /// <summary>The <c>float</c> keyword (32-bit IEEE 754 type).</summary>
    TYPE_FLOAT,

    #endregion

    #region Comparison & logical operators

    /// <summary>Equality operator <c>==</c>.</summary>
    EQUAL_EQUAL,
    /// <summary>Less-than-or-equal operator <c>&lt;=</c>.</summary>
    LESS_EQUAL,
    /// <summary>Greater-than-or-equal operator <c>&gt;=</c>.</summary>
    GREATER_EQUAL,
    /// <summary>Inequality operator <c>!=</c>.</summary>
    BANG_EQUAL,
    /// <summary>Less-than operator <c>&lt;</c>.</summary>
    LESS,
    /// <summary>Greater-than operator <c>&gt;</c>.</summary>
    GREATER,
    /// <summary>Logical NOT operator <c>!</c> (prefix, strict boolean).</summary>
    BANG,
    /// <summary>Logical AND operator <c>&amp;&amp;</c> (short-circuiting).</summary>
    AND,
    /// <summary>Logical OR operator <c>||</c> (short-circuiting).</summary>
    OR,

    #endregion

    #region Booleans

    /// <summary>Boolean true value</summary>
    TRUE,
    /// <summary>Boolean false value</summary>
    FALSE,



    #endregion

    #region Control

    /// <summary>End-of-input marker; the last token in every stream.</summary>
    EOF,

    #endregion
}
