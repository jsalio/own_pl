using System.IO;

namespace Own_Lang;

/// <summary>
/// Source of input lines for the REPL, given a prompt to show first.
/// </summary>
/// <remarks>
/// The abstraction lets the REPL loop stay the same whether input comes from an
/// interactive terminal (with line editing and history, see
/// <see cref="ConsoleLineReader"/>) or from a redirected stream / test
/// (<see cref="TextReaderLineReader"/>). It returns <c>null</c> at end of input.
/// </remarks>
internal interface ILineReader
{
    /// <summary>Writes the prompt and reads one line, or returns null at end of input.</summary>
    /// <param name="prompt">The prompt to display before reading.</param>
    string? ReadLine(string prompt);
}

/// <summary>
/// A plain <see cref="ILineReader"/> over a <see cref="TextReader"/>: writes the
/// prompt and calls <see cref="TextReader.ReadLine"/>.
/// </summary>
/// <remarks>
/// Used when input is redirected (a pipe or file) and in tests. No line editing
/// or history — the underlying stream already delivers whole lines, and editing
/// only makes sense against a live terminal.
/// </remarks>
internal sealed class TextReaderLineReader : ILineReader
{
    private readonly TextReader input;
    private readonly TextWriter output;

    /// <summary>Creates a reader over <paramref name="input"/>, showing prompts on <paramref name="output"/>.</summary>
    /// <param name="input">The stream to read lines from.</param>
    /// <param name="output">Where the prompt is written.</param>
    public TextReaderLineReader(TextReader input, TextWriter output)
    {
        this.input = input;
        this.output = output;
    }

    /// <inheritdoc/>
    public string? ReadLine(string prompt)
    {
        output.Write(prompt);
        return input.ReadLine();
    }
}
