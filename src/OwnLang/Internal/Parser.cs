using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Own_Lang.Internal.Contracts;

namespace Own_Lang.Internal;

/// <summary>
/// Stage 2 implementation: a recursive-descent parser. Holds a cursor
/// (<c>current</c>) over the token list and exposes one private method per
/// grammar rule. Operator precedence is encoded by the call order of the
/// expression methods (Expression → Equality → Comparison → Additive →
/// Multiplicative → Call → Primary), not by any explicit precedence table.
/// </summary>
internal sealed class Parser : IParser
{
    #region State

    private readonly IReadOnlyList<Token> tokens;
    private int current = 0;

    // Constructores de expresión primaria indexados por el token que las inicia.
    // Se construye una sola vez por parser (no en cada llamada a Primary()).
    private readonly Dictionary<TokenType, Func<Expr>> primaryBuilders;

    // Tokens que pueden iniciar una expresión primaria. Se deriva del diccionario
    // para que exista una sola fuente de verdad (evita listas desincronizadas).
    private readonly TokenType[] primaryStartTokens;

    #endregion

    #region Public API

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
            { TokenType.TRUE,       BuildBooleanToken },
            { TokenType.FALSE,      BuildBooleanToken },
            { TokenType.CHAR, BuildCharToken },
        };

        primaryStartTokens = primaryBuilders.Keys.ToArray();
    }

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

    #endregion

    #region Grammar: declarations

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

    #endregion

    #region Grammar: statements

    // Statement -> VarDecl | ExpressionStmt
    private Stmt Statement()
    {
        if (Match(TokenType.LET)) return VarDeclaration(null);
        if (Match(TokenType.TYPE_STRING)) return VarDeclaration("string");
        if (Match(TokenType.TYPE_BOOL)) return VarDeclaration("bool");
        if (Match(TokenType.TYPE_CHAR)) return VarDeclaration("char");
        if (Match(TokenType.TYPE_INT)) return VarDeclaration("int");
        if (Match(TokenType.TYPE_UINT)) return VarDeclaration("uint");
        if (Match(TokenType.TYPE_LONG)) return VarDeclaration("long");
        if (Match(TokenType.TYPE_ULONG)) return VarDeclaration("ulong");
        if (Match(TokenType.TYPE_DOUBLE)) return VarDeclaration("double");
        if (Match(TokenType.TYPE_FLOAT)) return VarDeclaration("float");
        if (Match(TokenType.WHEN)) return WhenStatement();
        if (Match(TokenType.LOOP)) return LoopStatement();
        if (Match(TokenType.STOP)) return StopStatement();
        return ExpressionStatement();
    }

    // VarDecl -> "let" IDENTIFIER "=" Expression ";"   ("let" ya consumido)
    private Stmt VarDeclaration(string? declareType)
    {
        Token name = Consume(TokenType.IDENTIFIER,
            "se esperaba el nombre de la variable después de 'let'");
        Consume(TokenType.EQUAL,
            "se esperaba '=' después del nombre de la variable");
        Expr initializer = Expression();
        Consume(TokenType.SEMICOLON,
            "se esperaba ';' al final de la declaración");
        return new VarDecl(declareType, name.Lexeme, initializer);
    }

    private Stmt WhenStatement()
    {
        Consume(TokenType.LPAREN, "se esperaba '(' despues de 'when'");
        Expr condition = Expression();
        Consume(TokenType.RPAREN, "se esperaba ')' despues de la condicion");
        Block thenCodeBlock = Block();
        Stmt? elseCodeBlock = null;
        if (Match(TokenType.ELSE))
        {
            //else when(...) -> recursion; else {...} -> bloque
            elseCodeBlock = Match(TokenType.WHEN) ? WhenStatement() : Block();
        }
        return new WhenStmt(condition, thenCodeBlock, elseCodeBlock);
    }

    // loopStmt -> "loop" ( "[" IDENT ":" expr "..." expr "]" | "when" "(" expr ")" )? block
    // ("loop" ya consumido)
    private Stmt LoopStatement()
    {
        // loop[i: from...to] { }  -> bucle contado
        if (Match(TokenType.LBRACKET))
        {
            Token variable = Consume(TokenType.IDENTIFIER,
                "se esperaba el nombre del contador después de '['");
            Consume(TokenType.COLON, "se esperaba ':' después del contador");
            Expr from = Expression();
            Consume(TokenType.RANGE, "se esperaba '...' en el rango");
            Expr to = Expression();
            Consume(TokenType.RBRACKET, "se esperaba ']' para cerrar el rango");
            Block rangeBody = Block();
            return new RangeLoopStmt(variable.Lexeme, from, to, rangeBody);
        }

        // loop when(cond) { }  -> while pre-test
        if (Match(TokenType.WHEN))
        {
            Consume(TokenType.LPAREN, "se esperaba '(' después de 'when'");
            Expr condition = Expression();
            Consume(TokenType.RPAREN, "se esperaba ')' después de la condición");
            Block whileBody = Block();
            return new WhileStmt(condition, whileBody);
        }

        // loop { }  -> infinito (se sale con 'stop')
        Block body = Block();
        return new LoopStmt(body);
    }

    // stopStmt -> "stop" ";"   ("stop" ya consumido)
    private Stmt StopStatement()
    {
        Consume(TokenType.SEMICOLON, "se esperaba ';' después de 'stop'");
        return new StopStmt();
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

    #endregion

    #region Grammar: expressions (en orden de precedencia)

    // Expression -> Equality
    private Expr Expression() => Equality();

    private Expr Equality()
    {
        Expr expr = Comparison();

        while (Match(TokenType.EQUAL_EQUAL, TokenType.BANG_EQUAL))
        {
            TokenType op = Previous().Type;
            Expr right = Comparison();
            expr = new Binary(expr, op, right);
        }

        return expr;
    }

    private Expr Comparison()
    {
        Expr expr = Additive();

        while (Match(TokenType.EQUAL_EQUAL, TokenType.BANG_EQUAL, TokenType.LESS,
            TokenType.LESS_EQUAL, TokenType.GREATER, TokenType.GREATER_EQUAL))
        {
            TokenType op = Previous().Type;
            Expr right = Additive();
            expr = new Binary(expr, op, right);
        }

        return expr;
    }

    // Additive -> Multiplicative ( ("+" | "-") Multiplicative )*
    // '+' y '-' comparten nivel de precedencia y asociatividad izquierda,
    // por eso van en el MISMO método (un método por nivel, no por operador).
    // El mismo bucle empareja ambos, permitiendo mezclas como 10 - 2 + 3.
    private Expr Additive()
    {

        Expr expr = Multiplicative();

        while (Match(TokenType.PLUS, TokenType.MINUS))
        {
            TokenType op = Previous().Type;
            Expr right = Multiplicative();
            expr = new Binary(expr, op, right);
        }

        return expr;
    }

    private Expr Multiplicative()
    {
        Expr expr = Call();

        while (Match(TokenType.STAR, TokenType.SLASH))
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

    #endregion

    #region Primary builders (token -> Expr)

    private Expr BuildNumericToken()
    {
        string lexeme = Previous().Lexeme;

        // Sufijo 'f' -> float (3.14f, 5f)
        if (lexeme.EndsWith("f") || lexeme.EndsWith("F"))
        {
            float f = float.Parse(lexeme[..^1], CultureInfo.InvariantCulture);
            return new NumberLiteral(f);
        }

        // Punto decimal -> double
        if (lexeme.Contains('.'))
        {
            double d = double.Parse(lexeme, CultureInfo.InvariantCulture);
            return new NumberLiteral(d);
        }

        // Entero: auto-ensanchado (int si cabe, si no long)
        long number = long.Parse(lexeme);
        object value = (number >= int.MinValue && number <= int.MaxValue) ? (object)(int)number : (object)number;
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

    private Expr BuildBooleanToken()
    {
        bool value = Previous().Type == TokenType.TRUE;
        return new BooleanLiteral(value);
    }

    private Expr BuildCharToken()
    {
        string raw = Previous().Lexeme;
        return new CharLiteral(raw[1]);
    }

    #endregion

    #region Token navigation helpers

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

    #endregion
}
