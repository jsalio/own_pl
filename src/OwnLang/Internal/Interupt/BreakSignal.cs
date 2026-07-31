using System;

namespace Own_Lang.Internal.Interupt;

/// <summary>
/// Control-flow signal thrown to break out of a loop (the language's <c>stop</c>).
/// </summary>
/// <remarks>
/// Implemented as an exception on purpose: a <c>stop</c> may sit deep inside
/// nested blocks and scopes, and throwing lets it unwind through all of them
/// cleanly until the nearest loop catches it — so it always breaks the
/// <b>innermost</b> loop with no manual signaling between levels. The interpreter
/// throws it for a <c>StopStmt</c> and wraps each loop's body in a
/// <c>try/catch (BreakSignal)</c>. It carries no data; its type is the whole
/// message. A <c>stop</c> outside any loop can never produce an uncaught signal
/// because the parser rejects that case up front.
/// </remarks>
internal sealed class BreakSignal : System.Exception { }

