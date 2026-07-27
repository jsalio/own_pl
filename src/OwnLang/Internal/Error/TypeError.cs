namespace Own_Lang.Internal.Error;

public class TypeError : System.Exception
{

    public static string ErrorMessage(string name, string declareType, string valueType)
    => "Type error in variable '" + name + "'"
       + ": expected " + declareType + ", got " + valueType;

    public TypeError(string message) : base(message)
    {
    }
}