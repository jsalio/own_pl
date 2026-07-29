using System;

namespace Own_Lang.Internal;

internal sealed class BreakSignal : System.Exception { }

internal sealed class ReturnSignal : System.Exception
{
    public ReturnSignal(object? value) => Value = value;
    public object? Value { get; }
}

