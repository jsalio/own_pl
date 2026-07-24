using System.Collections.Generic;

namespace Own_Lang.Internal.Contracts;

/// <summary>
/// Stage 1 of the pipeline: lexical analysis.
/// Turns raw source text into a flat sequence of <see cref="Token"/>s,
/// discarding whitespace and comments.
/// </summary>
internal interface ILexer
{
    /// <summary>
    /// Scans the entire source and produces its tokens in order.
    /// The returned list always ends with a single <c>EOF</c> token,
    /// which downstream stages rely on to detect end of input.
    /// </summary>
    /// <returns>The ordered tokens, terminated by <c>EOF</c>.</returns>
    IReadOnlyList<Token> Tokenize();
}
