using Own_Lang.Internal;
using Own_Lang.Internal.Contracts;

string codigo = @"def program {
    function empty Main()
    {
        let val1 = 1;
        let val2 = 2;
        let result = val2 - val1;
        term.out(""resultado:"");
        term.out(result);
    }
}";

// Pipeline completo: texto -> tokens -> AST -> ejecución
var lexer = new Lexer(codigo);
foreach (var token in lexer.Tokenize())
{
    Console.Out.WriteLine($"Token: {token}");
}
var tokens = lexer.Tokenize();

var parser = new Parser(tokens);
ProgramDecl program = parser.Parse();

var interpreter = new Interpreter();
interpreter.Interpret(program);
