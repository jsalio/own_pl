using Own_Lang.Internal;
using Own_Lang.Internal.Contracts;

string codigo = @"def program {
    function empty Main()
    {
        char inicial = 'J';
        term.out(inicial);
        term.out(""inicial: "" + inicial);
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
