using Own_Lang.Internal;
using Own_Lang.Internal.Contracts;

string codigo = @"def program {
    function empty Main()
    {
        term.out(1 < 2);
        term.out(2 <= 2);
        term.out(3 == 3);
        term.out(3 != 4);
        term.out(true);
        term.out(false);
        term.out(1 + 2 == 3);
        term.out(2 * 3 > 5);
    }
}";

var lexer = new Lexer(codigo);
var tokens = lexer.Tokenize();

var parser = new Parser(tokens);
ProgramDecl program = parser.Parse();

var interpreter = new Interpreter();
interpreter.Interpret(program);
