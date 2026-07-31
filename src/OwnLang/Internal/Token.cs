namespace Own_Lang.Internal;

/// <summary>
/// A single lexical token produced by the <see cref="Lexer"/>: one meaningful
/// unit of source text (a symbol, keyword, identifier, number or string),
/// tagged with its category and source position. Immutable once created.
/// </summary>
/// <remarks>
/// Tokens are the hand-off between stage 1 (lexer) and stage 2 (parser): the
/// lexer classifies raw characters into these tagged units, and the parser reads
/// them without ever looking at the source text again. Carrying the source
/// position (<see cref="Line"/>/<see cref="Column"/>) on every token is what lets
/// later stages report errors that point at the offending place. Immutability
/// matters because the parser only walks the token list — it must never rewrite it.
/// </remarks>
/// <param name="type">The token's category.</param>
/// <param name="lexeme">The exact source text this token was scanned from.</param>
/// <param name="line">1-based line where the token appears.</param>
/// <param name="column">1-based column where the token ends.</param>
internal sealed class Token(TokenType type, string lexeme,
    int line, int column)
{
  /// <summary>The token's category.</summary>
  /// <remarks>
  /// The primary thing the parser dispatches on. For keywords and symbols this
  /// category alone carries the full meaning; for identifiers/numbers/strings it
  /// says "what kind" while <see cref="Lexeme"/> holds the concrete value.
  /// </remarks>
  public TokenType Type {get;}= type;

  /// <summary>
  /// The exact source text this token was scanned from. Carries the concrete
  /// content for identifiers/numbers/strings (redundant for fixed symbols and
  /// keywords). String lexemes still include their surrounding quotes.
  /// </summary>
  /// <remarks>
  /// The parser reads this to recover a token's value — e.g. the name of an
  /// identifier or the digits of a number. String quotes are kept here and
  /// stripped later by the parser, so the token stays faithful to the source.
  /// </remarks>
  public string Lexeme{get;} = lexeme;

  /// <summary>1-based line where the token appears.</summary>
  /// <remarks>Recorded so syntax and runtime errors can name the line; also asserted directly in lexer tests.</remarks>
  public int Line {get;}= line;

  /// <summary>1-based column where the token ends.</summary>
  /// <remarks>Paired with <see cref="Line"/> to locate a token; note it marks where the token <i>ends</i>, not where it starts.</remarks>
  public int Column{get;} = column;

  /// <summary>
  /// Debug representation in the form <c>TYPE 'lexeme' (line L, col C)</c>,
  /// used when printing the token stream.
  /// </summary>
  /// <remarks>
  /// Purely for inspection (e.g. eyeballing the lexer's output from
  /// <c>Program.cs</c>); nothing in the pipeline parses this string, so its exact
  /// wording is not load-bearing.
  /// </remarks>
  public override string ToString()
      => $"{Type} '{Lexeme}' (line {Line}, col {Column})";
}
