namespace own_pl.internas.Error;

public class MathError:Exception
{
    public MathError(string message):base(message)
    {
    }
    
    public MathError(string message, Exception innerException) : base(message, innerException)
    {
    }
}