using Own_Lang.Internal.AST;

namespace Own_Lang.Internal.Contracts;

/// <summary>
/// Stage 2 of the pipeline: syntactic analysis.
/// Consumes the token stream produced by <see cref="ILexer"/> and builds
/// the abstract syntax tree (AST) using recursive descent.
/// </summary>
internal interface IParser
{
    /// <summary>
    /// Parses the whole token stream into an AST and returns its root.
    /// Throws on a syntax error (unexpected token, or trailing tokens
    /// after the program) with the offending token's line and column.
    /// </summary>
    /// <returns>The <see cref="CompilationUnit"/> root of the AST.</returns>
    CompilationUnit Parse();
}
