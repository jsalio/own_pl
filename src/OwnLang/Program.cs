using Own_Lang;

// Entry point: read an .own source file (path given as the first argument) and
// run it. All the real work lives in Runner so it can be unit-tested; this shell
// just wires the process arguments and streams to it and returns its exit code.
// A .own file → run it. No file argument → interactive REPL: use the console
// line editor (arrow keys + history) on a real terminal, or a plain reader when
// input is redirected (a pipe or file) where key-by-key editing does not apply.
if (args.Length > 0)
    return Runner.RunFile(args, Console.Out, Console.Error);

return Console.IsInputRedirected
    ? Repl.Run(Console.In, Console.Out, Console.Error)
    : Repl.Run(new ConsoleLineReader(), Console.Out, Console.Error);
