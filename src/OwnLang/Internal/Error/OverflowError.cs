namespace Own_Lang.Internal.Error;

/// <summary>
/// Excepción que se lanza cuando ocurre un desbordamiento aritmético.
/// </summary>
public class OverflowError : Exception
{
    public OverflowError(string message) : base(message)
    {
    }
}