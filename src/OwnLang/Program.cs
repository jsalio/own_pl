using Own_Lang.Internal;
using Own_Lang.Internal.Contracts;

string codigo = @"def program {
    function empty Main()
    {
        loop[i: 1...3] { term.out(i); }

        let x = 0;
        loop when(x < 3) {
            term.out(""while"");
            let x = x + 1;
        }

        let n = 0;
        loop {
            when(n == 2) { stop; }
            term.out(""inf"");
            let n = n + 1;
        }
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
