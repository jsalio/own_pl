using System.Collections.Generic;

namespace Own_Lang.Internal;

internal interface ILexer
{
    IReadOnlyList<Token> Tokenize();
}
