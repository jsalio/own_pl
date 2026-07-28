using Own_Lang.Internal;
using Own_Lang.Internal.Contracts;

string codigo = @"def program {
    function empty Main()
    {
        long a = 3000000000;
        term.out(a);
        long b = 5;
        term.out(b + 1);
        ulong c = 5;
        term.out(c * 2);
        term.out(3000000000 + 3000000000);
        int i = 5;
        long l = 10;
        term.out(i + l);
    }
}";

var lexer = new Lexer(codigo);
var tokens = lexer.Tokenize();

var parser = new Parser(tokens);
ProgramDecl program = parser.Parse();

foreach (var stm in program.Declarations)  
{
    Console.Out.WriteLine(stm.ToString());
}

var interpreter = new Interpreter();
interpreter.Interpret(program);
