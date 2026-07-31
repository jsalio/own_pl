using System.Collections.Generic;

namespace Own_Lang.Internal;

/// <summary>
/// Runtime storage for the program's variables: a <c>name → value</c> map,
/// optionally chained to an enclosing scope.
/// </summary>
/// <remarks>
/// This is where variables live while a program runs. Values are held as
/// <see cref="object"/> because the language mixes types (int, string, bool…) at
/// runtime. Environments form a chain — each one points at the scope that
/// encloses it — which is how lexical scoping works: the interpreter opens a new
/// child environment for every block and function call, so name lookups walk
/// outward through the chain and inner declarations shadow (but never overwrite)
/// outer ones. The chain is the single mechanism behind both variable visibility
/// and the difference between declaring (<see cref="Define"/>) and mutating
/// (<see cref="Assign"/>) a variable.
/// </remarks>
internal sealed class Environment
{
    /// <summary>
    /// Dictionary that stores this scope's own variable bindings.
    /// </summary>
    /// <remarks>
    /// Holds only the names declared <i>in this scope</i>; names from outer scopes
    /// are reached by delegating to <see cref="enclosing"/>. Keeping each scope's
    /// bindings separate is what lets a scope be discarded (and its locals with it)
    /// when its block ends.
    /// </remarks>
    private readonly Dictionary<string, object?> values = new();

    /// <summary>
    /// The parent (enclosing) scope, or <c>null</c> for the global scope.
    /// </summary>
    /// <remarks>
    /// The link that turns a flat map into a scope chain. <see cref="Get"/> and
    /// <see cref="Assign"/> follow it outward when a name is not found locally; a
    /// <c>null</c> value marks the outermost (global) environment, where the search stops.
    /// </remarks>
    private readonly Environment? enclosing;

    /// <summary>
    /// Creates an environment, optionally nested inside an enclosing scope.
    /// </summary>
    /// <remarks>
    /// Passing an <paramref name="enclosing"/> scope creates a child (used for each
    /// block and function call); passing none (the default) creates a root scope,
    /// used for the global environment.
    /// </remarks>
    /// <param name="enclosing">The parent scope, or <c>null</c> for a root environment.</param>
    public Environment(Environment? enclosing = null)
    {
        this.enclosing = enclosing;
    }

    /// <summary>
    /// Creates or overwrites a binding in <b>this</b> scope — the effect of a
    /// <c>let</c>/typed declaration or a parameter binding.
    /// </summary>
    /// <remarks>
    /// Always binds locally (never walks up the chain), which is the key contrast
    /// with <see cref="Assign"/>. This is what makes a <c>let</c> inside a block
    /// local to it, and lets an inner declaration shadow a same-named outer variable
    /// without touching the outer one.
    /// </remarks>
    /// <param name="name">The variable's name.</param>
    /// <param name="value">The value to bind (may be null).</param>
    public void Define(string name, object? value)
    {
        values[name] = value;
    }

    /// <summary>
    /// Reads the value bound to a variable, searching this scope then its enclosing scopes.
    /// </summary>
    /// <remarks>
    /// Implements name resolution: it checks the local <see cref="values"/> first and,
    /// on a miss, recurses into <see cref="enclosing"/> — so a variable is found in the
    /// innermost scope that declares it. Reaching a root scope without a match is a
    /// runtime error (an undefined variable), not a silent null.
    /// </remarks>
    /// <param name="name">The variable's name.</param>
    /// <returns>The bound value.</returns>
    /// <exception cref="System.Exception">Thrown if the variable is undefined in every scope.</exception>
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
            $"Runtime error: variable '{name}' is not defined");
    }

    /// <summary>
    /// Assigns to an <b>existing</b> variable, searching this scope and then its
    /// enclosing scopes. Unlike <see cref="Define"/>, it never creates a new
    /// binding — it mutates the variable in the scope where it already lives.
    /// </summary>
    /// <remarks>
    /// This is the runtime behavior of the <c>x = expr</c> assignment expression.
    /// Requiring the name to already exist (erroring otherwise) is deliberate: it
    /// separates mutation from declaration, so a typo like <c>x = 5</c> on an
    /// undeclared <c>x</c> is caught instead of silently creating a global. Mutating
    /// in the scope where the variable lives is what lets code inside a loop or block
    /// update a variable declared further out.
    /// </remarks>
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
            $"Runtime error: variable '{name}' is not defined");
    }
}
