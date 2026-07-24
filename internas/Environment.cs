using System.Collections.Generic;

namespace Own_Lang.Internal;

/// <summary>
/// Runtime storage for the program's variables: a <c>name → value</c> map.
/// Values are boxed as <see cref="object"/> because the language mixes integers
/// and strings. For the current single-function language a single flat
/// environment suffices; nested scopes would require chaining environments.
/// </summary>
internal sealed class Environment
{
    private readonly Dictionary<string, object?> values = new();

    /// <summary>
    /// Creates or overwrites a variable binding — the effect of a <c>let</c>
    /// declaration.
    /// </summary>
    /// <param name="name">The variable's name.</param>
    /// <param name="value">The value to bind (may be null).</param>
    public void Define(string name, object? value)
    {
        values[name] = value;
    }

    /// <summary>
    /// Reads the value bound to a variable.
    /// </summary>
    /// <param name="name">The variable's name.</param>
    /// <returns>The bound value.</returns>
    /// <exception cref="System.Exception">Thrown if the variable is undefined.</exception>
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
