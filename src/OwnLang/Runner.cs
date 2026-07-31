using System;
using System.IO;
using Own_Lang.Internal;
using Own_Lang.Internal.AST;

namespace Own_Lang;

/// <summary>
/// Command-line entry logic: reads an <c>.own</c> source file and runs it
/// through the pipeline (lexer → parser → interpreter).
/// </summary>
/// <remarks>
/// Kept separate from <c>Program.cs</c> so it can be unit-tested: the top-level
/// program is just a thin shell that forwards its arguments here. It takes the
/// output/error writers as parameters (rather than using <see cref="Console"/>
/// directly) so tests can capture what a run prints and check its exit code.
/// </remarks>
internal static class Runner
{
    /// <summary>The file extension every Own_Lang source file must use.</summary>
    private const string SourceExtension = ".own";

    /// <summary>
    /// Runs the program described by the command-line arguments.
    /// </summary>
    /// <remarks>
    /// Usage: <c>ownlang &lt;file.own&gt; [--ast]</c>. Returns an exit code so the
    /// shell can tell success from failure: <c>0</c> when the program ran, <c>1</c>
    /// for a usage/validation/compile/runtime error. All errors are reported as a
    /// clean one-line message on <paramref name="error"/> — a stray C# stack trace
    /// would leak interpreter internals to the user.
    /// </remarks>
    /// <param name="args">The process arguments: the source path and optional flags.</param>
    /// <param name="output">Where program output (e.g. <c>Term.out</c>) is written.</param>
    /// <param name="error">Where usage and error messages are written.</param>
    /// <returns><c>0</c> on success, <c>1</c> on any error.</returns>
    public static int RunFile(string[] args, TextWriter output, TextWriter error)
    {
        string? path = null;
        bool dumpAst = false;
        foreach (string arg in args)
        {
            if (arg == "--ast") dumpAst = true;
            else if (path is null) path = arg;
        }

        if (path is null)
        {
            error.WriteLine("usage: ownlang <file.own> [--ast]");
            return 1;
        }

        if (!path.EndsWith(SourceExtension, StringComparison.OrdinalIgnoreCase))
        {
            error.WriteLine($"error: expected a '{SourceExtension}' file, got '{path}'");
            return 1;
        }

        if (!File.Exists(path))
        {
            error.WriteLine($"error: file not found: '{path}'");
            return 1;
        }

        string source;
        try
        {
            source = File.ReadAllText(path);
        }
        catch (IOException e)
        {
            error.WriteLine($"error: could not read '{path}': {e.Message}");
            return 1;
        }

        return Run(source, output, error, dumpAst);
    }

    /// <summary>
    /// Runs source text through the full pipeline, reporting any failure cleanly.
    /// </summary>
    /// <remarks>
    /// Shared by <see cref="RunFile"/> and useful on its own for running source
    /// that does not come from a file. Any exception thrown by a pipeline stage is
    /// turned into a one-line error message and a non-zero exit code, so a bad
    /// program never crashes the host with a stack trace.
    /// </remarks>
    /// <param name="source">The source code to run.</param>
    /// <param name="output">Where program output is written.</param>
    /// <param name="error">Where error messages are written.</param>
    /// <param name="dumpAst">When true, prints the parsed top-level declarations before running.</param>
    /// <returns><c>0</c> on success, <c>1</c> if a stage threw.</returns>
    public static int Run(string source, TextWriter output, TextWriter error, bool dumpAst = false)
    {
        try
        {
            var tokens = new Lexer(source).Tokenize();
            CompilationUnit unit = new Parser(tokens).Parse();

            if (dumpAst)
                foreach (var declaration in unit.Program.Declarations)
                    output.WriteLine(declaration.ToString());

            new Interpreter().Interpret(unit);
            return 0;
        }
        catch (Exception e)
        {
            error.WriteLine($"error: {e.Message}");
            return 1;
        }
    }
}
