namespace Own_Lang.Internal;

public enum TokenType
{
    // Keywords
    DEF,
    FUNCTION,
    EMPTY,
    LET,

    // Símbolos
    LBRACE,
    RBRACE,
    LPAREN,
    RPAREN,
    SEMICOLON,
    EQUAL,
    PLUS,
    DOT,
    COMMA,

    // Literales e identificadores
    IDENTIFIER,
    NUMBER,
    STRING,

    // Control
    EOF
}
