using Own_Lang.Internal;
using Own_Lang.Internal.AST;

string codigo = @"def program {
    function empty Main()
    {
        double precio = 19.99;
        float descuento = 0.1f;
        Term.out(""total: "" + (precio - precio * descuento));
        int s = Sum(1,2);
        Term.out(s);
    }
    function int Sum(int a, int b)
    {
        return a + b;
    }
}";

var lexer = new Lexer(codigo);
var tokens = lexer.Tokenize();

var parser = new Parser(tokens);
CompilationUnit unit = parser.Parse();

var program = unit.Program;

foreach (var stm in program.Declarations)
{
    Console.Out.WriteLine(stm.ToString());
}

var interpreter = new Interpreter();
interpreter.Interpret(unit);
