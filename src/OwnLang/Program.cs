using Own_Lang;

// Entry point: read an .own source file (path given as the first argument) and
// run it. All the real work lives in Runner so it can be unit-tested; this shell
// just wires the process arguments and streams to it and returns its exit code.
// No file argument → interactive REPL; a .own file → run it.
return args.Length == 0
    ? Repl.Run(Console.In, Console.Out, Console.Error)
    : Runner.RunFile(args, Console.Out, Console.Error);
