using Own_Lang.Internal;
using Own_Lang.Internal.Contracts;

string codigo = @"def program {
    function empty Main()
    {
        term.out(2 + 3 * 4);
        term.out(10 - 6 / 2);
        term.out(2 * 3 + 4);
        term.out(20 / 4 / 5);
    }
}";

var lexer = new Lexer(codigo);
var tokens = lexer.Tokenize();

var parser = new Parser(tokens);
ProgramDecl program = parser.Parse();

var interpreter = new Interpreter();
interpreter.Interpret(program);
