namespace Own_Lang.Internal.Contracts;

/// <summary>
/// Stage 3 of the pipeline: evaluation.
/// Walks the AST produced by <see cref="IParser"/> and executes it,
/// tree-walking style.
/// </summary>
internal interface IInterpreter
{
    /// <summary>
    /// Runs the program by locating the <c>Main</c> function among its
    /// declarations and executing its body. Throws at runtime if no
    /// <c>Main</c> exists, or on an evaluation error (undefined variable,
    /// unsupported operation/call).
    /// </summary>
    /// <param name="unit">The AST root to execute.</param>
    void Interpret(CompilationUnit unit);
}
