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
    /// Runs the REPL until end-of-input or an <c>exit</c>/<c>quit</c> command.
    /// </summary>
    /// <remarks>
    /// One <see cref="Interpreter"/> is reused for the whole session, so bindings
    /// persist across lines. A parse/runtime error on one line is reported and the
    /// loop continues — a mistake never ends the session.
    /// </remarks>
    /// <param name="input">Where lines are read from (e.g. the console).</param>
    /// <param name="output">Where prompts and results are written.</param>
    /// <param name="error">Where error messages are written.</param>
    /// <returns>Always <c>0</c> (a clean session exit).</returns>
    public static int Run(TextReader input, TextWriter output, TextWriter error)
    {
        var interpreter = new Interpreter();
        output.WriteLine("Own_Lang REPL — type 'exit' to quit.");

        while (true)
        {
            output.Write("> ");
            string? line = input.ReadLine();
            if (line is null) break;                 // EOF (Ctrl+D)

            string trimmed = line.Trim();
            if (trimmed.Length == 0) continue;
            if (trimmed == "exit" || trimmed == "quit") break;

            try
            {
                var tokens = new Lexer(line).Tokenize();
                Stmt statement = new Parser(tokens).ParseStatement();
                string? result = interpreter.RunReplLine(statement);
                if (result is not null) output.WriteLine(result);
            }
            catch (Exception e)
            {
                error.WriteLine($"error: {e.Message}");
            }
        }

        return 0;
    }
}
