using System.Collections.Generic;
using System.Linq;
using Own_Lang.Internal.Contracts;

namespace Own_Lang.Internal;

/// <summary>
/// Stage 2 implementation: a recursive-descent parser. Holds a cursor
/// (<c>current</c>) over the token list and exposes one private method per
/// grammar rule. Operator precedence is encoded by the call order of the
/// expression methods (Expression → Addition → Call → Primary), not by any
/// explicit precedence table.
/// </summary>
internal sealed class Parser : IParser
{
    private readonly IReadOnlyList<Token> tokens;
    private int current = 0;

    // Constructores de expresión primaria indexados por el token que las inicia.
    // Se construye una sola vez por parser (no en cada llamada a Primary()).
    private readonly Dictionary<TokenType, Func<Expr>> primaryBuilders;

    // Tokens que pueden iniciar una expresión primaria. Se deriva del diccionario
    // para que exista una sola fuente de verdad (evita listas desincronizadas).
    private readonly TokenType[] primaryStartTokens;

    /// <summary>Creates a parser over a token stream (as produced by the lexer).</summary>
    /// <param name="tokens">The tokens to parse; must end with an <c>EOF</c> token.</param>
    public Parser(IReadOnlyList<Token> tokens)
    {
        this.tokens = tokens;

        primaryBuilders = new Dictionary<TokenType, Func<Expr>>
        {
            { TokenType.NUMBER,     BuildNumericToken },
            { TokenType.STRING,     BuildStringToken },
            { TokenType.IDENTIFIER, BuildIdentifierToken },
            { TokenType.LPAREN,     BuildParenToken },
        };

        primaryStartTokens = primaryBuilders.Keys.ToArray();
    }

    // ---- Punto de entrada (contrato de IParser) ----

    /// <inheritdoc/>
    public ProgramDecl Parse()
    {
        ProgramDecl program = Program();

        // No usamos Consume(EOF) porque Check() se corta con IsAtEnd():
        // Check(EOF) siempre daría false. Verificamos el fin directamente.
        if (!IsAtEnd())
        {
            Token token = Peek();
            throw new System.Exception(
                $"Error de sintaxis: se esperaba el final del programa, se encontró " +
                $"'{token.Lexeme}' ({token.Type}) en la línea {token.Line}, columna {token.Column}");
        }

        return program;
    }

    // ---- Gramática: declaraciones de alto nivel ----

    // Program -> "def" IDENTIFIER "{" Declaration* "}"
    private ProgramDecl Program()
    {
        Consume(TokenType.DEF, "se esperaba 'def' al inicio del programa");
        Token name = Consume(TokenType.IDENTIFIER,
            "se esperaba el nombre del programa después de 'def'");
        Consume(TokenType.LBRACE, "se esperaba '{' después del nombre del programa");

        var declarations = new List<Stmt>();
        while (!Check(TokenType.RBRACE) && !IsAtEnd())
        {
            declarations.Add(Declaration());
        }

        Consume(TokenType.RBRACE, "se esperaba '}' para cerrar el programa");
        return new ProgramDecl(name.Lexeme, declarations);
    }

    // Declaration -> Function | Statement
    private Stmt Declaration()
    {
        if (Check(TokenType.FUNCTION)) return Function();
        return Statement();
    }

    // Function -> "function" returnType IDENTIFIER "(" params ")" Block
    private FunctionDecl Function()
    {
        Consume(TokenType.FUNCTION, "se esperaba 'function'");
        string returnType = ReturnType();
        Token name = Consume(TokenType.IDENTIFIER,
            "se esperaba el nombre de la función");

        Consume(TokenType.LPAREN, "se esperaba '(' después del nombre de la función");
        var parameters = new List<string>();
        if (!Check(TokenType.RPAREN))
        {
            do
            {
                Token param = Consume(TokenType.IDENTIFIER,
                    "se esperaba un nombre de parámetro");
                parameters.Add(param.Lexeme);
            }
            while (Match(TokenType.COMMA));
        }
        Consume(TokenType.RPAREN, "se esperaba ')' después de los parámetros");

        Block body = Block();
        return new FunctionDecl(returnType, name.Lexeme, parameters, body);
    }

    // returnType -> "empty" | IDENTIFIER   (tipos con nombre, para el futuro)
    private string ReturnType()
    {
        if (Match(TokenType.EMPTY)) return "empty";

        Token type = Consume(TokenType.IDENTIFIER,
            "se esperaba el tipo de retorno de la función");
        return type.Lexeme;
    }

    // ---- Gramática: sentencias ----

    // Statement -> VarDecl | ExpressionStmt
    private Stmt Statement()
    {
        if (Match(TokenType.LET)) return VarDeclaration();
        return ExpressionStatement();
    }

    // VarDecl -> "let" IDENTIFIER "=" Expression ";"   ("let" ya consumido)
    private Stmt VarDeclaration()
    {
        Token name = Consume(TokenType.IDENTIFIER,
            "se esperaba el nombre de la variable después de 'let'");
        Consume(TokenType.EQUAL,
            "se esperaba '=' después del nombre de la variable");
        Expr initializer = Expression();
        Consume(TokenType.SEMICOLON,
            "se esperaba ';' al final de la declaración");
        return new VarDecl(name.Lexeme, initializer);
    }

    // ExpressionStmt -> Expression ";"
    private Stmt ExpressionStatement()
    {
        Expr expr = Expression();
        Consume(TokenType.SEMICOLON,
            "se esperaba ';' al final de la sentencia");
        return new ExpressionStmt(expr);
    }

    // Block -> "{" Statement* "}"
    private Block Block()
    {
        Consume(TokenType.LBRACE, "se esperaba '{' para abrir el bloque");

        var statements = new List<Stmt>();
        while (!Check(TokenType.RBRACE) && !IsAtEnd())
        {
            statements.Add(Statement());
        }

        Consume(TokenType.RBRACE, "se esperaba '}' para cerrar el bloque");
        return new Block(statements);
    }

    // ---- Gramática: expresiones ----

    // Expression -> Additive
    private Expr Expression() => Additive();

    // Additive -> Call ( ("+" | "-") Call )*
    // '+' y '-' comparten nivel de precedencia y asociatividad izquierda,
    // por eso van en el MISMO método (un método por nivel, no por operador).
    // El mismo bucle empareja ambos, permitiendo mezclas como 10 - 2 + 3.
    private Expr Additive()
    {
        Expr expr = Call();

        while (Match(TokenType.PLUS, TokenType.MINUS))
        {
            TokenType op = Previous().Type;
            Expr right = Call();
            expr = new Binary(expr, op, right);
        }

        return expr;
    }

    // Call -> Primary ( "." IDENTIFIER | "(" argumentos ")" )*
    private Expr Call()
    {
        Expr expr = Primary();

        while (true)
        {
            if (Match(TokenType.DOT))
            {
                Token member = Consume(TokenType.IDENTIFIER,
                    "se esperaba un nombre de miembro después de '.'");
                expr = new MemberAccess(expr, member.Lexeme);
            }
            else if (Match(TokenType.LPAREN))
            {
                expr = FinishCall(expr);
            }
            else
            {
                break;
            }
        }

        return expr;
    }

    // Parsea la lista de argumentos ya con el "(" consumido.
    private Expr FinishCall(Expr callee)
    {
        var arguments = new List<Expr>();

        if (!Check(TokenType.RPAREN))
        {
            do
            {
                arguments.Add(Expression());
            }
            while (Match(TokenType.COMMA));
        }

        Consume(TokenType.RPAREN, "se esperaba ')' después de los argumentos");
        return new Call(callee, arguments);
    }

    // Primary -> NUMBER | STRING | IDENTIFIER | "(" Expression ")"
    private Expr Primary()
    {
        if (Match(primaryStartTokens))
        {
            return primaryBuilders[Previous().Type]();
        }

        Token token = Peek();
        throw new System.Exception(
            $"Error de sintaxis: se esperaba una expresión, se encontró " +
            $"'{token.Lexeme}' ({token.Type}) en la línea {token.Line}, columna {token.Column}");
    }

    private Expr BuildNumericToken()
    {
        int value = int.Parse(Previous().Lexeme);
        return new NumberLiteral(value);
    }

    private Expr BuildStringToken()
    {
        string raw = Previous().Lexeme;
        string value = raw.Substring(1, raw.Length - 2);
        return new StringLiteral(value);
    }

    private Expr BuildIdentifierToken()
        => new Variable(Previous().Lexeme);

    private Expr BuildParenToken()
    {
        Expr expr = Expression();
        Consume(TokenType.RPAREN, "se esperaba ')' para cerrar la expresión");
        return expr;
    }

    // ---- Helpers: navegación sobre los tokens ----

    // Mira el token actual sin consumirlo.
    private Token Peek() => tokens[current];

    // Devuelve el último token consumido.
    private Token Previous() => tokens[current - 1];

    // ¿Llegamos al final (token EOF)?
    private bool IsAtEnd() => Peek().Type == TokenType.EOF;

    // Consume el token actual y avanza. Devuelve el consumido.
    private Token Advance()
    {
        if (!IsAtEnd()) current++;
        return Previous();
    }

    // ¿El token actual es de este tipo? (sin consumir)
    private bool Check(TokenType type)
    {
        if (IsAtEnd()) return false;
        return Peek().Type == type;
    }

    // Si el token actual es de alguno de estos tipos, lo consume y devuelve true.
    private bool Match(params TokenType[] types)
    {
        foreach (var type in types)
        {
            if (Check(type))
            {
                Advance();
                return true;
            }
        }
        return false;
    }

    // Exige un tipo: si coincide lo consume; si no, lanza error de sintaxis.
    private Token Consume(TokenType type, string message)
    {
        if (Check(type)) return Advance();

        Token token = Peek();
        throw new System.Exception(
            $"Error de sintaxis: {message}. Se encontró '{token.Lexeme}' " +
            $"({token.Type}) en la línea {token.Line}, columna {token.Column}");
    }
}
