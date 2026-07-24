using System.Collections.Generic;

namespace Own_Lang.Internal;

// Guarda las variables del programa: nombre -> valor.
// Para esta primera versión (una sola función, sin scopes anidados)
// un único Environment es suficiente.
internal sealed class Environment
{
    private readonly Dictionary<string, object?> values = new();

    // Crea una variable nueva (lo que hace 'let').
    public void Define(string name, object? value)
    {
        values[name] = value;
    }

    // Lee el valor de una variable; error si no existe.
    public object? Get(string name)
    {
        if (values.TryGetValue(name, out var value))
        {
            return value;
        }

        throw new System.Exception(
            $"Error en tiempo de ejecución: la variable '{name}' no está definida");
    }
}
