using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Own_Lang.Internal.AST;
using Own_Lang.Internal.Contracts;

namespace Own_Lang.Internal;

/// <summary>
/// Stage 2 implementation: a recursive-descent parser. Holds a cursor
/// (<c>current</c>) over the token list and exposes one private method per
/// grammar rule.
/// </summary>
/// <remarks>
/// This is the middle stage of the pipeline: it turns the flat token list from
/// the lexer into a tree (the AST) that the interpreter can walk. Each grammar
/// rule is a private method that consumes tokens and returns an AST node,
/// calling the methods for the rules nested inside it — that recursion is what
/// "recursive descent" means.
/// <para>
/// Why it matters: <b>operator precedence and associativity are encoded purely
/// by the call order of the expression methods</b>, not by any lookup table.
/// The chain is Expression → Assignment → LogicOr → LogicAnd → Equality →
/// Comparison → Additive → Multiplicative → Unary → Call → Primary; a rule that
/// sits lower in the chain binds tighter. Adding a new precedence level means
/// inserting a method at the right spot in that chain, so understanding this
/// ordering is the key to reading the whole expression grammar.
/// </para>
/// </remarks>
internal sealed class Parser : IParser
{
    #region State

    /// <summary>The token stream being parsed, as produced by the lexer.</summary>
    /// <remarks>Read-only: the parser never mutates the tokens, it only walks them with <see cref="current"/>.</remarks>
    private readonly IReadOnlyList<Token> tokens;

    /// <summary>Index of the next token to read (the cursor).</summary>
    /// <remarks>
    /// Every helper (<see cref="Peek"/>, <see cref="Advance"/>, <see cref="Match"/>…) is defined
    /// in terms of this single index, so the whole parser shares one source of truth for "where am I".
    /// </remarks>
    private int current = 0;

    /// <summary>How many loops currently enclose the rule being parsed.</summary>
    /// <remarks>
    /// Incremented on entering a loop and decremented on leaving it (see <see cref="LoopStatement"/>).
    /// It exists so <see cref="StopStatement"/> can reject a <c>stop</c> that appears outside any loop
    /// at parse time — catching the error before execution instead of letting it crash the interpreter.
    /// </remarks>
    private int loopDepth = 0;

    /// <summary>Builders for primary expressions, indexed by the token that starts them.</summary>
    /// <remarks>
    /// Built once per parser (not on every <see cref="Primary"/> call). Replacing a big
    /// <c>switch</c> with this table keeps <see cref="Primary"/> tiny and makes adding a new
    /// literal a one-line registration.
    /// </remarks>
    private readonly Dictionary<TokenType, Func<Expr>> primaryBuilders;

    /// <summary>The set of tokens that can begin a primary expression.</summary>
    /// <remarks>
    /// Derived from <see cref="primaryBuilders"/> so there is a single source of truth —
    /// the "can this token start a primary?" test and the builder table can never drift apart.
    /// </remarks>
    private readonly TokenType[] primaryStartTokens;

    #endregion

    #region Public API

    /// <summary>Creates a parser over a token stream (as produced by the lexer).</summary>
    /// <remarks>
    /// The constructor also builds the <see cref="primaryBuilders"/> dispatch table and derives
    /// <see cref="primaryStartTokens"/> from it, so that work happens once here rather than on
    /// every expression parsed.
    /// </remarks>
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
    /// <remarks>
    /// The entry point and the AST root builder. It loops over the top-level <c>def</c> blocks,
    /// each of which is a program, a contract, or a module, and groups them into a single
    /// <see cref="CompilationUnit"/>. This is why contracts and modules are <b>siblings</b> of
    /// <c>def program</c> rather than nested inside it. Exactly one program is required (it holds
    /// <c>Main</c>); more than one, or none, is a syntax error.
    /// </remarks>
    public CompilationUnit Parse()
    {
        ProgramDecl? program = null;
        var contracts = new List<ContractDecl>();
        var modules = new List<ModuleDecl>();

        while (!IsAtEnd())
        {
            var token = Consume(TokenType.DEF, "expected 'def' at the start of the program");
            if (Match(TokenType.CONTRACT))
            {
                contracts.Add(ContractDeclaration());
            }
            else if (Match(TokenType.MODULE))
            {
                modules.Add(ModuleDeclaration());
            }
            else
            {
                if (program is not null)
                    throw new System.Exception("only one program is allowed");
                program = Program();
            }
        }

        if (program is null)
            throw new System.Exception("expected a program");
        return new CompilationUnit(program, contracts, modules);

    }

    #endregion

    #region Grammar: declarations

    /// <summary>Parses a program body: <c>IDENTIFIER "{" Declaration* "}"</c>.</summary>
    /// <remarks>
    /// The <c>def</c> keyword was already consumed by <see cref="Parse"/>, so this starts at the
    /// program name. A program is the only top-level block that contains executable declarations
    /// (functions and statements) and, by convention, the <c>Main</c> the interpreter runs.
    /// </remarks>
    private ProgramDecl Program()
    {
        Token name = Consume(TokenType.IDENTIFIER,
            "expected the program name after 'def'");
        Consume(TokenType.LBRACE, "expected '{' after the program name");

        var declarations = new List<Stmt>();
        while (!Check(TokenType.RBRACE) && !IsAtEnd())
        {
            declarations.Add(Declaration());
        }

        Consume(TokenType.RBRACE, "expected '}' to close the program");
        return new ProgramDecl(name.Lexeme, declarations);
    }

    /// <summary>Parses a contract: <c>IDENTIFIER "{" functionSig* "}"</c>.</summary>
    /// <remarks>
    /// The <c>contract</c> keyword was already consumed by <see cref="Parse"/>. A contract is the
    /// language's interface: a list of function <i>signatures</i> with no bodies. It carries no
    /// behavior on its own — its purpose is to let a module declare "I implement this", which the
    /// interpreter later validates against.
    /// </remarks>
    private ContractDecl ContractDeclaration()
    {
        Token name = Consume(TokenType.IDENTIFIER,
            "expected the contract name after 'contract'");
        Consume(TokenType.LBRACE, "expected '{' after the contract name");

        var members = new List<FunctionSig>();
        while (!Check(TokenType.RBRACE) && !IsAtEnd())
            members.Add(FunctionSignature());

        Consume(TokenType.RBRACE, "expected '}' to close the contract");
        return new ContractDecl(name.Lexeme, members);
    }

    /// <summary>Parses one contract member: <c>"function" returnType IDENTIFIER "(" params ")" ";"</c>.</summary>
    /// <remarks>
    /// Same header as a real function (<see cref="Function"/>) but terminated by <c>;</c> instead of a
    /// body block — a signature only. Reusing <see cref="ParameterList"/> and <see cref="ReturnType"/>
    /// keeps the signature shape identical to an implementable function, so the later "does this module
    /// match the contract?" check can compare them directly.
    /// </remarks>
    private FunctionSig FunctionSignature()
    {
        Consume(TokenType.FUNCTION, "expected 'function' in the contract signature");
        string returnType = ReturnType();
        Token name = Consume(TokenType.IDENTIFIER,
            "expected the function name");
        var parameters = ParameterList();
        Consume(TokenType.SEMICOLON, "expected ';' at the end of the contract signature");
        return new FunctionSig(returnType, name.Lexeme, parameters);
    }

    /// <summary>Parses a module: <c>IDENTIFIER ( ":" IDENTIFIER )? "{" moduleFunction* "}"</c>.</summary>
    /// <remarks>
    /// The <c>module</c> keyword was already consumed by <see cref="Parse"/>. A module is a named
    /// group of functions (a namespace). The optional <c>: IDENTIFIER</c> records which contract it
    /// claims to implement (null when absent). Its functions are either <c>external</c> (backed by a
    /// native implementation) or written in the language — this is how the standard library, such as
    /// the built-in <c>Term</c>, replaces the old hardcoded call.
    /// </remarks>
    private ModuleDecl ModuleDeclaration()
    {
        Token name = Consume(TokenType.IDENTIFIER,
                    "expected the module name after 'module'");

        string? contract = null;
        if (Match(TokenType.COLON))
            contract = Consume(TokenType.IDENTIFIER,
                "expected the contract name after ':'").Lexeme;

        Consume(TokenType.LBRACE, "expected '{' after the module header");

        var functions = new List<FunctionDecl>();
        while (!Check(TokenType.RBRACE) && !IsAtEnd())
            functions.Add(ModuleFunction());

        Consume(TokenType.RBRACE, "expected '}' to close the module");
        return new ModuleDecl(name.Lexeme, contract, functions);
    }

    /// <summary>
    /// Parses a module member, either native or language-bodied:
    /// <c>"external" "function" … ";"</c> or <c>"function" … block</c>.
    /// </summary>
    /// <remarks>
    /// The leading <c>external</c> is optional; when present, the function has no body (just <c>;</c>)
    /// and its <see cref="FunctionDecl.IsExternal"/> flag is set, marking it for the interpreter to
    /// dispatch to a registered native (e.g. <c>Term.out</c>). Without it, a normal body block is
    /// parsed and the function runs like any user function. This one method is what lets a module mix
    /// primitives that bottom out in C# with helpers written in the language itself.
    /// </remarks>
    private FunctionDecl ModuleFunction()
    {
        bool isExternal = Match(TokenType.EXTERNAL);
        Consume(TokenType.FUNCTION, "expected 'function'");
        string returnType = ReturnType();
        Token name = Consume(TokenType.IDENTIFIER,
            "expected the function name");
        var parameters = ParameterList();

        if (isExternal)
        {
            Consume(TokenType.SEMICOLON,
                "expected ';' after an 'external' function");
            return new FunctionDecl(returnType, name.Lexeme, parameters,
                new Block(new List<Stmt>()), IsExternal: true);
        }

        Block body = Block();
        return new FunctionDecl(returnType, name.Lexeme, parameters, body);
    }

    /// <summary>Parses one item inside a program body: a function or a statement.</summary>
    /// <remarks>
    /// A single token of lookahead decides: <c>function</c> starts a function declaration, anything
    /// else is a statement. This is the fork that lets a program hold both definitions and top-level
    /// executable code.
    /// </remarks>
    private Stmt Declaration()
    {
        if (Check(TokenType.FUNCTION)) return Function();
        return Statement();
    }

    /// <summary>Parses a program-level function: <c>"function" returnType IDENTIFIER "(" params ")" Block</c>.</summary>
    /// <remarks>
    /// The keyword-first shape (<c>function &lt;return&gt; &lt;name&gt;</c>) is shared with module and
    /// contract functions via <see cref="ParameterList"/>/<see cref="ReturnType"/>. Unlike a module's
    /// <c>external</c> function, this always has a body block and is never native.
    /// </remarks>
    private FunctionDecl Function()
    {
        Consume(TokenType.FUNCTION, "expected 'function'");
        string returnType = ReturnType();
        Token name = Consume(TokenType.IDENTIFIER,
            "expected the function name");

        var parameters = ParameterList();

        Block body = Block();
        return new FunctionDecl(returnType, name.Lexeme, parameters, body);
    }

    /// <summary>Parses a parenthesized, comma-separated list of typed parameters.</summary>
    /// <remarks>
    /// Grammar: <c>"(" ( type IDENT ( "," type IDENT )* )? ")"</c>. Extracted so functions, module
    /// functions and contract signatures parse their parameters identically — one definition, no drift.
    /// Each parameter carries its declared type, which the interpreter uses to coerce arguments at the call.
    /// </remarks>
    private List<Param> ParameterList()
    {
        Consume(TokenType.LPAREN, "expected '(' after the function name");
        var parameters = new List<Param>();
        if (!Check(TokenType.RPAREN))
        {
            do
            {
                string type = TypeName();
                Token param = Consume(TokenType.IDENTIFIER,
                    "expected a parameter name");
                parameters.Add(new Param(type, param.Lexeme));
            }
            while (Match(TokenType.COMMA));
        }
        Consume(TokenType.RPAREN, "expected ')' after the parameters");
        return parameters;
    }

    /// <summary>Parses a return type: <c>"empty"</c>, a type keyword, or an identifier.</summary>
    /// <remarks>
    /// <c>empty</c> means "no value" (void). A bare <c>IDENTIFIER</c> is accepted as a forward-looking
    /// hook for future named types; otherwise it falls through to <see cref="TypeName"/>. Return types
    /// are needed because a function's result is coerced to its declared type by the interpreter.
    /// </remarks>
    private string ReturnType()
    {
        if (Match(TokenType.EMPTY)) return "empty";
        if (Check(TokenType.IDENTIFIER)) return Advance().Lexeme;

        return TypeName();
    }

    /// <summary>Consumes a built-in type keyword and returns its canonical string name.</summary>
    /// <remarks>
    /// Types are their own tokens (e.g. <c>int</c> is <c>TYPE_INT</c>, not an identifier), so this maps
    /// each type token back to the string the interpreter's coercion logic expects. It is the single
    /// place that knows the set of built-in type names, used by parameters, declarations and return types.
    /// </remarks>
    private string TypeName()
    {
        if (Match(TokenType.TYPE_STRING)) return "string";
        if (Match(TokenType.TYPE_BOOL)) return "bool";
        if (Match(TokenType.TYPE_CHAR)) return "char";
        if (Match(TokenType.TYPE_INT)) return "int";
        if (Match(TokenType.TYPE_UINT)) return "uint";
        if (Match(TokenType.TYPE_LONG)) return "long";
        if (Match(TokenType.TYPE_ULONG)) return "ulong";
        if (Match(TokenType.TYPE_DOUBLE)) return "double";
        if (Match(TokenType.TYPE_FLOAT)) return "float";
        throw new System.Exception("expected a data type");
    }

    #endregion

    #region Grammar: statements

    /// <summary>Parses a single statement, dispatching on the leading keyword.</summary>
    /// <remarks>
    /// This is the hub of the statement grammar: a leading <c>let</c> or type keyword starts a
    /// declaration, <c>when</c>/<c>loop</c>/<c>stop</c>/<c>return</c> start their respective forms,
    /// and anything else is an expression statement (the fallback). Each branch <see cref="Match"/>es
    /// its keyword — consuming it — before delegating, so the sub-rule starts right after the keyword.
    /// </remarks>
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
        if (Match(TokenType.RETURN)) return ReturnStatement();
        return ExpressionStatement();
    }

    /// <summary>Parses a variable declaration: <c>( "let" | type ) IDENTIFIER "=" Expression ";"</c>.</summary>
    /// <remarks>
    /// The introducer keyword was already consumed by <see cref="Statement"/>. <paramref name="declareType"/>
    /// is the declared type name, or <c>null</c> for an inferred <c>let</c>; the interpreter uses it to
    /// decide whether to type-check and coerce the initializer. Note this is a <i>declaration</i> (it
    /// creates a binding), distinct from an assignment expression, which mutates an existing one.
    /// </remarks>
    /// <param name="declareType">The declared type, or <c>null</c> when inferred via <c>let</c>.</param>
    private Stmt VarDeclaration(string? declareType)
    {
        Token name = Consume(TokenType.IDENTIFIER,
            "expected the variable name after 'let'");
        Consume(TokenType.EQUAL,
            "expected '=' after the variable name");
        Expr initializer = Expression();
        Consume(TokenType.SEMICOLON,
            "expected ';' at the end of the declaration");
        return new VarDecl(declareType, name.Lexeme, initializer);
    }

    /// <summary>Parses a conditional: <c>"when" "(" Expression ")" Block ( "else" ( WhenStmt | Block ) )?</c>.</summary>
    /// <remarks>
    /// The optional <c>else</c> can be either a block or another <c>when</c> — and by recursing on the
    /// latter, an <c>else when</c> chain is represented simply as a <see cref="WhenStmt"/> nested in the
    /// <c>Else</c> slot. That recursion means the interpreter needs no special case for <c>else if</c>-style
    /// ladders; it just walks the nesting.
    /// </remarks>
    private Stmt WhenStatement()
    {
        Consume(TokenType.LPAREN, "expected '(' after 'when'");
        Expr condition = Expression();
        Consume(TokenType.RPAREN, "expected ')' after the condition");
        Block thenCodeBlock = Block();
        Stmt? elseCodeBlock = null;
        if (Match(TokenType.ELSE))
        {
            //else when(...) -> recursion; else {...} -> bloque
            elseCodeBlock = Match(TokenType.WHEN) ? WhenStatement() : Block();
        }
        return new WhenStmt(condition, thenCodeBlock, elseCodeBlock);
    }

    /// <summary>
    /// Parses all three loop forms: counted (<c>loop[i: a...b]</c>), pre-test
    /// (<c>loop when(c)</c>), and infinite (<c>loop</c>).
    /// </summary>
    /// <remarks>
    /// A single leading <c>loop</c> (already consumed) branches on what follows: <c>[</c> is a counted
    /// range loop, <c>when</c> is a while loop, and nothing is an infinite loop exited via <c>stop</c>.
    /// <para>
    /// Why the <c>try/finally</c>: it brackets the body with <see cref="loopDepth"/>++/-- so that any
    /// <c>stop</c> parsed inside counts as "inside a loop". The <c>finally</c> guarantees the counter is
    /// restored even on the method's several early returns or a parse error, which is what keeps
    /// <see cref="StopStatement"/>'s outside-a-loop check correct across nested loops.
    /// </para>
    /// </remarks>
    private Stmt LoopStatement()
    {
        loopDepth++;
        try
        {
            // loop[i: from...to] { }  -> bucle contado
            if (Match(TokenType.LBRACKET))
            {
                Token variable = Consume(TokenType.IDENTIFIER,
                    "expected the counter name after '['");
                Consume(TokenType.COLON, "expected ':' after the counter");
                Expr from = Expression();
                Consume(TokenType.RANGE, "expected '...' in the range");
                Expr to = Expression();
                Consume(TokenType.RBRACKET, "expected ']' to close the range");
                Block rangeBody = Block();
                return new RangeLoopStmt(variable.Lexeme, from, to, rangeBody);
            }

            // loop when(cond) { }  -> while pre-test
            if (Match(TokenType.WHEN))
            {
                Consume(TokenType.LPAREN, "expected '(' after 'when'");
                Expr condition = Expression();
                Consume(TokenType.RPAREN, "expected ')' after the condition");
                Block whileBody = Block();
                return new WhileStmt(condition, whileBody);
            }

            // loop { }  -> infinito (se sale con 'stop')
            Block body = Block();
            return new LoopStmt(body);
        }
        finally
        {
            loopDepth--;
        }
    }

    /// <summary>Parses a break: <c>"stop" ";"</c> (the <c>stop</c> was already consumed).</summary>
    /// <remarks>
    /// Rejects a <c>stop</c> that is not inside any loop (<see cref="loopDepth"/> == 0) as a syntax
    /// error. Doing this at parse time is deliberate: it catches the mistake even in a branch that
    /// never runs, and stops the break signal from escaping and crashing the interpreter at runtime.
    /// </remarks>
    private Stmt StopStatement()
    {
        if (loopDepth == 0)
            throw new System.Exception("Syntax error: 'stop' cannot appear outside a loop");

        Consume(TokenType.SEMICOLON, "expected ';' after 'stop'");
        return new StopStmt();
    }

    /// <summary>Parses a return: <c>"return" Expression? ";"</c> (the <c>return</c> was already consumed).</summary>
    /// <remarks>
    /// The value is optional: a bare <c>return;</c> yields a null value (used by <c>empty</c> functions).
    /// The interpreter turns this node into a control-flow signal that unwinds to the enclosing call,
    /// which is why <c>return</c> works from anywhere inside a function body.
    /// </remarks>
    private Stmt ReturnStatement()
    {
        Expr? value = Check(TokenType.SEMICOLON) ? null : Expression();
        Consume(TokenType.SEMICOLON, "expected ';' after 'return'");
        return new ReturnStmt(value);
    }

    /// <summary>Parses an expression used as a statement: <c>Expression ";"</c>.</summary>
    /// <remarks>
    /// The fallback of <see cref="Statement"/>. It is what lets an expression evaluated purely for its
    /// side effect — a call like <c>Term.out(x);</c> or a bare assignment <c>x = 5;</c> — stand as a
    /// statement; the interpreter evaluates it and discards the value.
    /// </remarks>
    private Stmt ExpressionStatement()
    {
        Expr expr = Expression();
        Consume(TokenType.SEMICOLON,
            "expected ';' at the end of the statement");
        return new ExpressionStmt(expr);
    }

    /// <summary>Parses a brace-delimited block: <c>"{" Statement* "}"</c>.</summary>
    /// <remarks>
    /// Groups zero or more statements into one <see cref="Block"/> node. Blocks are the unit the
    /// interpreter runs in a fresh child scope, so this is what gives function bodies, loop bodies and
    /// <c>when</c> branches their own lexical scope.
    /// </remarks>
    private Block Block()
    {
        Consume(TokenType.LBRACE, "expected '{' to open the block");

        var statements = new List<Stmt>();
        while (!Check(TokenType.RBRACE) && !IsAtEnd())
        {
            statements.Add(Statement());
        }

        Consume(TokenType.RBRACE, "expected '}' to close the block");
        return new Block(statements);
    }

    #endregion

    #region Grammar: expressions (in precedence order)

    /// <summary>Entry point of the expression grammar.</summary>
    /// <remarks>
    /// Delegates to the lowest-precedence rule (<see cref="Assignment"/>) and, through the chain of
    /// calls below it, parses any expression. Every place that needs "an expression" calls this, so the
    /// whole precedence ladder is reachable from one method.
    /// </remarks>
    private Expr Expression() => Assignment();

    /// <summary>Parses assignment: <c>IDENTIFIER "=" Assignment</c>, else falls through.</summary>
    /// <remarks>
    /// The lowest-precedence, right-associative level (recursing on the right lets <c>a = b = 5</c> work).
    /// It parses the left side as a normal expression first, then, on seeing <c>=</c>, requires that side
    /// to be a <see cref="Variable"/> — otherwise the target is invalid. Assignment being an <i>expression</i>
    /// (not a statement) is why <c>x = 5</c> has a value and can appear inside larger expressions.
    /// </remarks>
    private Expr Assignment()
    {
        Expr expr = LogicOr();

        if (Match(TokenType.EQUAL))
        {
            Expr value = Assignment();
            if (expr is Variable v)
                return new Assign(v.Name, value);

            throw new System.Exception("Invalid assignment target");
        }

        return expr;
    }

    /// <summary>Parses logical OR: <c>LogicAnd ( "||" LogicAnd )*</c>.</summary>
    /// <remarks>
    /// Sits just above <see cref="LogicAnd"/>, so <c>||</c> binds <i>looser</i> than <c>&amp;&amp;</c>
    /// (<c>a &amp;&amp; b || c</c> parses as <c>(a &amp;&amp; b) || c</c>). Emits a <see cref="Logical"/>
    /// node rather than a <see cref="Binary"/> one specifically so the interpreter can short-circuit —
    /// evaluate the right operand only when needed.
    /// </remarks>
    private Expr LogicOr()
    {
        Expr expr = LogicAnd();
        while (Match(TokenType.OR))
        {
            Expr right = LogicAnd();
            expr = new Logical(expr, TokenType.OR, right);
        }
        return expr;
    }

    /// <summary>Parses logical AND: <c>Equality ( "&amp;&amp;" Equality )*</c>.</summary>
    /// <remarks>
    /// One level below <see cref="LogicOr"/> (so it binds tighter) and, like it, produces a
    /// <see cref="Logical"/> node to enable short-circuit evaluation.
    /// </remarks>
    private Expr LogicAnd()
    {
        Expr expr = Equality();
        while (Match(TokenType.AND))
        {
            Expr right = Equality();
            expr = new Logical(expr, TokenType.AND, right);
        }
        return expr;
    }

    /// <summary>Parses equality: <c>Comparison ( ( "==" | "!=" ) Comparison )*</c>.</summary>
    /// <remarks>
    /// Left-associative. Both operators share this one level because they share a precedence — the
    /// codebase's rule is one method per precedence level, not per operator. Produces a
    /// <see cref="Binary"/> node whose operands are evaluated eagerly by the interpreter.
    /// </remarks>
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

    /// <summary>Parses relational comparisons: <c>Additive ( ( "&lt;" | "&lt;=" | "&gt;" | "&gt;=" ) Additive )*</c>.</summary>
    /// <remarks>
    /// One level below <see cref="Equality"/>, so comparisons bind tighter than <c>==</c>/<c>!=</c>
    /// (<c>a &lt; b == c</c> parses as <c>(a &lt; b) == c</c>). Left-associative, emitting
    /// <see cref="Binary"/> nodes.
    /// </remarks>
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

    /// <summary>Parses addition and subtraction: <c>Multiplicative ( ( "+" | "-" ) Multiplicative )*</c>.</summary>
    /// <remarks>
    /// <c>+</c> and <c>-</c> share this level because they share a precedence and left-associativity —
    /// one method per level, not per operator. The single loop pairs both, so mixed chains like
    /// <c>10 - 2 + 3</c> parse left-to-right. Sits above <see cref="Multiplicative"/>, so <c>*</c>/<c>/</c>
    /// bind tighter.
    /// </remarks>
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

    /// <summary>Parses multiplication and division: <c>Unary ( ( "*" | "/" ) Unary )*</c>.</summary>
    /// <remarks>
    /// The tightest-binding binary level. It calls <see cref="Unary"/> (not <see cref="Call"/> directly)
    /// so a prefix <c>!</c> binds tighter than <c>*</c>/<c>/</c>. Left-associative, emitting
    /// <see cref="Binary"/> nodes.
    /// </remarks>
    private Expr Multiplicative()
    {
        Expr expr = Unary(); //Call();

        while (Match(TokenType.STAR, TokenType.SLASH))
        {
            TokenType op = Previous().Type;
            Expr right = Unary(); //Call();
            expr = new Binary(expr, op, right);
        }

        return expr;
    }

    /// <summary>Parses a prefix unary: <c>"!" Unary | Call</c>.</summary>
    /// <remarks>
    /// Recurses on itself so stacked operators like <c>!!x</c> are allowed. Producing a
    /// <see cref="Unary"/> node here — above <see cref="Call"/> but below the binary levels — is what
    /// makes <c>!</c> bind tighter than comparisons (<c>!a == b</c> is <c>(!a) == b</c>).
    /// </remarks>
    private Expr Unary()
    {
        if (Match(TokenType.BANG))
        {
            Expr operand = Unary(); // permite !!x
            return new Unary(TokenType.BANG, operand);
        }
        return Call();
    }

    /// <summary>Parses postfix chains on a primary: <c>Primary ( "." IDENTIFIER | "(" arguments ")" )*</c>.</summary>
    /// <remarks>
    /// Loops so member access and calls can chain and stack (e.g. <c>Term.out(x)</c> is a call whose
    /// callee is a member access). A <c>.</c> wraps the current expression in a <see cref="MemberAccess"/>;
    /// a <c>(</c> turns it into a <see cref="Call"/> via <see cref="FinishCall"/>. This is the level that
    /// lets the interpreter recognize <c>Module.func(args)</c> shapes.
    /// </remarks>
    private Expr Call()
    {
        Expr expr = Primary();

        while (true)
        {
            if (Match(TokenType.DOT))
            {
                Token member = Consume(TokenType.IDENTIFIER,
                    "expected a member name after '.'");
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

    /// <summary>Finishes a call by parsing its argument list, with the opening <c>(</c> already consumed.</summary>
    /// <remarks>
    /// Reads zero or more comma-separated argument expressions and wraps them with the
    /// <paramref name="callee"/> into a <see cref="Call"/> node. Split out from <see cref="Call"/> to keep
    /// that loop readable and to be reusable if calls appear in more positions later.
    /// </remarks>
    /// <param name="callee">The expression being called (already parsed).</param>
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

        Consume(TokenType.RPAREN, "expected ')' after the arguments");
        return new Call(callee, arguments);
    }

    /// <summary>Parses the atoms of the grammar: literals, identifiers, and parenthesized expressions.</summary>
    /// <remarks>
    /// The bottom of the precedence chain and the recursion's base case. Instead of a large
    /// <c>switch</c>, it looks up the token in the <see cref="primaryBuilders"/> table and delegates to
    /// the matching builder; if the current token can start no primary, it is a syntax error (an
    /// expression was expected here).
    /// </remarks>
    private Expr Primary()
    {
        if (Match(primaryStartTokens))
        {
            return primaryBuilders[Previous().Type]();
        }

        Token token = Peek();
        throw new System.Exception(
            $"Syntax error: expected an expression, found " +
            $"'{token.Lexeme}' ({token.Type}) at line {token.Line}, column {token.Column}");
    }

    #endregion

    #region Primary builders (token -> Expr)

    /// <summary>Builds a numeric literal node from the just-consumed NUMBER token.</summary>
    /// <remarks>
    /// Classifies the literal by its lexeme: an <c>f</c>/<c>F</c> suffix is a <c>float</c>, a decimal
    /// point is a <c>double</c>, otherwise an integer that auto-widens (<c>int</c> if it fits, else
    /// <c>long</c>). Parsing with <see cref="CultureInfo.InvariantCulture"/> matters so <c>.</c> is always
    /// the decimal separator regardless of the machine's locale.
    /// </remarks>
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

    /// <summary>Builds a string literal node from the just-consumed STRING token.</summary>
    /// <remarks>
    /// The lexer keeps the surrounding quotes in the lexeme; this strips them (the <c>Substring</c>) so
    /// the AST holds the actual text value. Stripping here, not in the lexer, keeps the lexer's token
    /// faithful to the source.
    /// </remarks>
    private Expr BuildStringToken()
    {
        string raw = Previous().Lexeme;
        string value = raw.Substring(1, raw.Length - 2);
        return new StringLiteral(value);
    }

    /// <summary>Builds a variable reference from the just-consumed IDENTIFIER token.</summary>
    /// <remarks>
    /// A bare name is always parsed as a <see cref="Variable"/>; whether it is a real variable, a
    /// function, or a module name is resolved later by the interpreter — the parser does not track scope.
    /// </remarks>
    private Expr BuildIdentifierToken()
        => new Variable(Previous().Lexeme);

    /// <summary>Builds a parenthesized (grouped) expression; the <c>(</c> was already consumed.</summary>
    /// <remarks>
    /// Parses a full inner <see cref="Expression"/> and requires the closing <c>)</c>. Grouping is the
    /// one primary that produces no node of its own — it just returns the inner expression, letting
    /// parentheses override precedence.
    /// </remarks>
    private Expr BuildParenToken()
    {
        Expr expr = Expression();
        Consume(TokenType.RPAREN, "expected ')' to close the expression");
        return expr;
    }

    /// <summary>Builds a boolean literal from the just-consumed TRUE/FALSE token.</summary>
    /// <remarks>The value is read from which of the two keyword tokens was matched.</remarks>
    private Expr BuildBooleanToken()
    {
        bool value = Previous().Type == TokenType.TRUE;
        return new BooleanLiteral(value);
    }

    /// <summary>Builds a char literal from the just-consumed CHAR token.</summary>
    /// <remarks>
    /// The lexeme is quoted (e.g. <c>'a'</c>); index <c>1</c> is the single character between the quotes.
    /// </remarks>
    private Expr BuildCharToken()
    {
        string raw = Previous().Lexeme;
        return new CharLiteral(raw[1]);
    }

    #endregion

    #region Token navigation helpers

    /// <summary>Returns the current token without consuming it.</summary>
    /// <remarks>The one-token lookahead the whole grammar relies on to decide which rule applies.</remarks>
    private Token Peek() => tokens[current];

    /// <summary>Returns the token most recently consumed.</summary>
    /// <remarks>
    /// Lets a rule read what <see cref="Match"/> just accepted — e.g. which operator matched, or the
    /// lexeme a primary builder needs — without threading it through as a return value.
    /// </remarks>
    private Token Previous() => tokens[current - 1];

    /// <summary>True when the cursor has reached the <c>EOF</c> token.</summary>
    /// <remarks>
    /// The stop condition for every parsing loop. Note that, because the stream always ends with
    /// <c>EOF</c>, <see cref="Check"/>ing for <c>EOF</c> never succeeds — end-of-input is tested here instead.
    /// </remarks>
    private bool IsAtEnd() => Peek().Type == TokenType.EOF;

    /// <summary>Consumes the current token and returns it (does not move past <c>EOF</c>).</summary>
    /// <remarks>The single primitive that advances the cursor; every consuming helper ends up here.</remarks>
    private Token Advance()
    {
        if (!IsAtEnd()) current++;
        return Previous();
    }

    /// <summary>Tests whether the current token has the given type, without consuming it.</summary>
    /// <remarks>Pure lookahead — the non-consuming counterpart to <see cref="Match"/>.</remarks>
    /// <param name="type">The token type to test for.</param>
    private bool Check(TokenType type)
    {
        if (IsAtEnd()) return false;
        return Peek().Type == type;
    }

    /// <summary>Consumes the current token and returns true if it matches any of the given types; otherwise leaves it.</summary>
    /// <remarks>
    /// The workhorse of the grammar: "if the next token is one of these, take it." Its consume-on-match
    /// behavior is why each rule can <see cref="Match"/> its keyword and then start right after it.
    /// </remarks>
    /// <param name="types">The token types to accept.</param>
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

    /// <summary>Requires a token of the given type: consumes it if present, otherwise throws a syntax error.</summary>
    /// <remarks>
    /// The parser's assertion primitive. Rules use it for tokens that <i>must</i> be there (a closing
    /// <c>)</c>, a <c>;</c>, …); the <paramref name="message"/> becomes the error shown to the user, with
    /// the offending token's location appended. This is where most syntax errors are reported.
    /// </remarks>
    /// <param name="type">The token type that must appear next.</param>
    /// <param name="message">Human-readable description of what was expected, used in the error.</param>
    private Token Consume(TokenType type, string message)
    {
        if (Check(type)) return Advance();

        Token token = Peek();
        throw new System.Exception(
            $"Syntax error: {message}. Found '{token.Lexeme}' " +
            $"({token.Type}) at line {token.Line}, column {token.Column}");
    }

    #endregion
}
