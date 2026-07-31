using Own_Lang;

// Entry point: read an .own source file (path given as the first argument) and
// run it. All the real work lives in Runner so it can be unit-tested; this shell
// just wires the process arguments and streams to it and returns its exit code.
return Runner.RunFile(args, Console.Out, Console.Error);
