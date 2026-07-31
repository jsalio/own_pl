using System;
using System.IO;
using Own_Lang.Internal;
using Own_Lang.Internal.AST;

namespace Own_Lang;

/// <summary>
/// Interactive Read-Eval-Print Loop: runs statements typed one line at a time
/// against a single, persistent interpreter.
/// </summary>
internal static class Repl
{
    /// <summary>
    /// Runs the REPL, reading lines from a plain <see cref="TextReader"/>.
    /// </summary>
    /// <remarks>Convenience overload for redirected input and tests; wraps the reader in a <see cref="TextReaderLineReader"/>.</remarks>
    /// <param name="input">Where lines are read from.</param>
    /// <param name="output">Where prompts and results are written.</param>
    /// <param name="error">Where error messages are written.</param>
    /// <returns>Always <c>0</c> (a clean session exit).</returns>
    public static int Run(TextReader input, TextWriter output, TextWriter error)
        => Run(new TextReaderLineReader(input, output), output, error);

    /// <summary>
    /// Runs the REPL until end-of-input or an <c>exit</c>/<c>quit</c> command.
    /// </summary>
    /// <remarks>
    /// One <see cref="Interpreter"/> is reused for the whole session, so bindings
    /// persist across lines. Lines come from an <see cref="ILineReader"/>, which
    /// is what lets the same loop serve both an interactive terminal (with editing
    /// and history) and a redirected stream. A parse/runtime error on one line is
    /// reported and the loop continues — a mistake never ends the session.
    /// </remarks>
    /// <param name="lines">The source of input lines (writes its own prompt).</param>
    /// <param name="output">Where results are written.</param>
    /// <param name="error">Where error messages are written.</param>
    /// <returns>Always <c>0</c> (a clean session exit).</returns>
    public static int Run(ILineReader lines, TextWriter output, TextWriter error)
    {
        var interpreter = new Interpreter();
        output.WriteLine("Own_Lang REPL — type 'exit' to quit.");

        while (true)
        {
            string? line = lines.ReadLine("> ");
            if (line is null) break;                 // end of input (e.g. Ctrl+D)

            string trimmed = line.Trim();
            if (trimmed.Length == 0) continue;
            if (trimmed == "exit" || trimmed == "quit") break;

            try
            {
                var tokens = new Lexer(line).Tokenize();
                Stmt statement = new Parser(tokens).ParseReplLine();
                string? result = interpreter.RunReplLine(statement);
                if (result is not null) output.WriteLine(result);
            }
            catch (Exception exception)
            {
                error.WriteLine($"error: {exception.Message}");
            }
        }

        return 0;
    }
}
