namespace Own_Lang.Internal;

/// <summary>
/// A single lexical token produced by the <see cref="Lexer"/>: one meaningful
/// unit of source text (a symbol, keyword, identifier, number or string),
/// tagged with its category and source position. Immutable once created.
/// </summary>
/// <param name="type">The token's category.</param>
/// <param name="lexeme">The exact source text this token was scanned from.</param>
/// <param name="line">1-based line where the token appears.</param>
/// <param name="column">1-based column where the token ends.</param>
internal sealed class Token(TokenType type, string lexeme,
    int line, int column)
{
  /// <summary>The token's category.</summary>
  public TokenType Type {get;}= type;

  /// <summary>
  /// The exact source text this token was scanned from. Carries the concrete
  /// content for identifiers/numbers/strings (redundant for fixed symbols and
  /// keywords). String lexemes still include their surrounding quotes.
  /// </summary>
  public string Lexeme{get;} = lexeme;

  /// <summary>1-based line where the token appears.</summary>
  public int Line {get;}= line;

  /// <summary>1-based column where the token ends.</summary>
  public int Column{get;} = column;

  /// <summary>
  /// Debug representation in the form <c>TYPE 'lexeme' (línea L, col C)</c>,
  /// used when printing the token stream.
  /// </summary>
  public override string ToString()
      => $"{Type} '{Lexeme}' (línea {Line}, col {Column})";
}
