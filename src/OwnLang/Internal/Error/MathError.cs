namespace Own_Lang.Internal.Error;

/// <summary>
/// Runtime error raised by an invalid arithmetic operation (e.g. division by
/// zero) while the interpreter evaluates an expression.
/// </summary>
public class MathError : Exception
{
    /// <summary>Creates a math error with a message.</summary>
    /// <param name="message">Description of what went wrong.</param>
    public MathError(string message) : base(message)
    {
    }

    /// <summary>Creates a math error that wraps an underlying exception.</summary>
    /// <param name="message">Description of what went wrong.</param>
    /// <param name="innerException">The underlying cause.</param>
    public MathError(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
