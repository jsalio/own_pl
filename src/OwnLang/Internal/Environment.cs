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

    private readonly Environment? enclosing; 


    public Environment (Environment? enclosing= null)
    {
        this.enclosing = enclosing;
    }

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

        if (enclosing is not null)
        {
            return enclosing.Get(name);
        }

        throw new System.Exception(
            $"Error en tiempo de ejecución: la variable '{name}' no está definida");
    }

    /// <summary>
    /// Assigns to an <b>existing</b> variable, searching this scope and then its
    /// enclosing scopes. Unlike <see cref="Define"/>, it never creates a new
    /// binding — it mutates the variable in the scope where it already lives.
    /// </summary>
    /// <param name="name">The variable's name; must already be defined.</param>
    /// <param name="value">The new value.</param>
    /// <exception cref="System.Exception">Thrown if the variable is undefined in any scope.</exception>
    public void Assign(string name, object? value)
    {
        if (values.ContainsKey(name))
        {
            values[name] = value;
            return;
        }

        if (enclosing is not null)
        {
            enclosing.Assign(name, value);
            return;
        }

        throw new System.Exception(
            $"Error en tiempo de ejecución: la variable '{name}' no está definida");
    }
}
