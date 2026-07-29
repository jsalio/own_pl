using Own_Lang.Internal;
using Own_Lang.Internal.Contracts;

string codigo = @"def program {
    function empty Main()
    {
        double precio = 19.99;
        float descuento = 0.1f;
        term.out(""total: "" + (precio - precio * descuento));
        int s = Sum(1,2);
        term.out(s);
    }
    function int Sum(int a, int b)
    {
        return a + b;
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
