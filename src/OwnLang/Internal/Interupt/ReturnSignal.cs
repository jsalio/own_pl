namespace Own_Lang.Internal.Interupt;

/// <summary>
/// Control-flow signal thrown to return from a function (the language's <c>return</c>),
/// carrying the returned value.
/// </summary>
/// <remarks>
/// The value-carrying sibling of <see cref="BreakSignal"/>, and it works the same
/// way: the interpreter throws it for a <c>ReturnStmt</c> and <c>CallFunction</c>
/// catches it, so a <c>return</c> nested inside loops or <c>when</c> blocks unwinds
/// cleanly all the way out of the call. Modeling <c>return</c> as an exception
/// (rather than threading a "did we return?" flag through every statement) is what
/// lets it exit from anywhere in the body.
/// </remarks>
internal sealed class ReturnSignal : System.Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ReturnSignal"/> class.
    /// </summary>
    /// <remarks>The value is captured at the throw site so the catching <c>CallFunction</c> can hand it back as the call's result.</remarks>
    /// <param name="value">The value produced by the <c>return</c>, or <c>null</c> for a bare <c>return;</c>.</param>
    public ReturnSignal(object? value) => Value = value;

    /// <summary>
    /// Gets the value returned by the return statement.
    /// </summary>
    /// <remarks>Read by <c>CallFunction</c> when it catches the signal; it is then coerced to the function's declared return type (unless the function is <c>empty</c>).</remarks>
    public object? Value { get; }
}

