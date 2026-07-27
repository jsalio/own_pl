using Own_Lang.Internal;
using Own_Lang.Internal.Contracts;

string codigo = @"def program {
    function empty Main()
    {
        int a = 5;
        uint b = 5;
        term.out(a + b);
        term.out(7 / 2);
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
