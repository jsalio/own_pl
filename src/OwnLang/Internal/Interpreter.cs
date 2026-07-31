using System.Globalization;
using Own_Lang.Internal.AST;
using Own_Lang.Internal.Contracts;
using Own_Lang.Internal.Error;
using Own_Lang.Internal.Interrupt;

namespace Own_Lang.Internal;

/// <summary>
/// Stage 3 implementation: a tree-walking interpreter. Splits traversal into
/// <c>Evaluate</c> (for <see cref="Expr"/>, returning a value) and <c>Execute</c>
/// (for <see cref="Stmt"/>, performing an action), both dispatching by pattern
/// matching over the AST record types. Variables are held in a chain of
/// <see cref="Environment"/> scopes (each block opens a child); function calls
/// run in a child of the global scope, and top-level functions are registered in
/// a name table so calls can resolve them.
/// </summary>
internal sealed class Interpreter : IInterpreter
{
    #region State

    /// <summary>
    /// The global environment.
    /// </summary>
    private readonly Environment globals = new();

    /// <summary>
    /// The current environment.
    /// </summary>
    private Environment environment = new();

    /// <summary>
    /// The function declarations.
    /// </summary>
    private readonly Dictionary<string, FunctionDecl> functions = new();

    /// <summary>
    /// The contract declarations.
    /// </summary>
    private readonly Dictionary<string, ContractDecl> contracts = new();

    /// <summary>
    /// The module declarations.
    /// </summary>
    private readonly Dictionary<string, ModuleDecl> modules = new();

    /// <summary>
    /// Native (C#) implementations of <c>external</c> functions, keyed by <c>"Module.function"</c>.
    /// </summary>
    /// <remarks>
    /// This is the boundary between the interpreted language and the host. When a module's
    /// <c>external</c> function is called, the interpreter looks up its implementation here rather than
    /// executing a body. It is the seam the standard library plugs into (e.g. <c>"Term.out"</c>).
    /// </remarks>
    private readonly Dictionary<string, System.Func<IReadOnlyList<object?>, object?>> natives = new();

    #endregion

    #region Entry point

    /// <summary>
    /// Initializes a new instance of the <see cref="Interpreter"/> class.
    /// </summary>
    public Interpreter()
    {
        environment = globals;
        RegisterNatives();
        LoadPrelude();
    }

    /// <summary>Registers the native (C#) implementations of external functions.</summary>
    /// <remarks>The module declarations that use them (e.g. <c>Term</c>) live in the prelude; here we only bind the primitives.</remarks>
    private void RegisterNatives()
    {
        natives["Term.out"] = args =>
        {
            object? message = args.Count > 0 ? args[0] : null;
            System.Console.WriteLine(Stringify(message));
            return null;
        };
    }

    /// <summary>Loads the embedded <c>prelude.own</c> and registers its declarations.</summary>
    /// <remarks>
    /// Runs once per interpreter, before any user program, so the standard library
    /// (the <c>Term</c> module, <c>Math</c>, …) is always available. The prelude is
    /// ordinary Own_Lang source parsed with the same lexer/parser; its <c>external</c>
    /// functions bind to the natives registered in <see cref="RegisterNatives"/>.
    /// </remarks>
    private void LoadPrelude()
    {
        string source = ReadPreludeSource();
        var tokens = new Lexer(source).Tokenize();
        var prelude = new Parser(tokens).ParsePrelude();
        RegisterDeclarations(prelude.Contracts, prelude.Modules);
    }

    /// <summary>Registers contracts and modules into their tables and validates the modules.</summary>
    /// <remarks>Shared by the prelude loader and <see cref="Interpret"/>; a name already present (a prelude/built-in) is a redefinition error.</remarks>
    private void RegisterDeclarations(
        IReadOnlyList<ContractDecl> contractDecls, IReadOnlyList<ModuleDecl> moduleDecls)
    {
        foreach (var contract in contractDecls)
        {
            if (contracts.ContainsKey(contract.Name))
                throw new System.Exception(
                    $"Runtime error: contract '{contract.Name}' is already defined");
            contracts[contract.Name] = contract;
        }

        foreach (var module in moduleDecls)
        {
            if (modules.ContainsKey(module.Name))
                throw new System.Exception(
                    $"Runtime error: module '{module.Name}' is already defined");
            modules[module.Name] = module;
        }

        foreach (var module in moduleDecls)
            ValidateModule(module);
    }

    // Reads the embedded prelude.own resource shipped inside the assembly.
    private static string ReadPreludeSource()
    {
        var assembly = typeof(Interpreter).Assembly;
        string name = assembly.GetManifestResourceNames()
            .First(resource => resource.EndsWith("prelude.own", System.StringComparison.OrdinalIgnoreCase));
        using var stream = assembly.GetManifestResourceStream(name)!;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    /// <summary>
    /// Runs one REPL line against the current (persistent) environment and
    /// returns its printable value, or null when there is nothing to print.
    /// </summary>
    /// <remarks>
    /// Reuses the same interpreter instance across lines, so a <c>let</c> binds into
    /// the long-lived global scope and a <c>function</c> declaration into the
    /// long-lived function table, both staying visible on later lines. A
    /// <see cref="FunctionDecl"/> is registered (not executed) — redefining a name
    /// simply replaces it; an expression statement returns its evaluated value (so
    /// the REPL can echo it); every other statement runs for its effect and returns null.
    /// </remarks>
    public string? RunReplLine(Stmt statement)
    {
        if (statement is FunctionDecl function)
        {
            functions[function.Name] = function;
            return null;
        }

        if (statement is ExpressionStmt expr)
        {
            object? value = Evaluate(expr.Expression);
            return value is null ? null : Stringify(value);
        }

        Execute(statement);
        return null;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// The interpreter's driver. It runs in phases before executing anything: first it registers every
    /// top-level function, contract and module into their name tables (rejecting duplicate or built-in
    /// names), then it validates each module against its contract and native bindings, and only then
    /// does it locate the <c>Main</c> function and execute its body. Registering before executing is
    /// what lets code call any function or module regardless of source order.
    /// </remarks>
    public void Interpret(CompilationUnit unit)
    {
        ProgramDecl? program = unit.Program;
        FunctionDecl? main = null;

        foreach (var decl in program.Declarations)
        {
            if (decl is FunctionDecl fn)
                functions[fn.Name] = fn;
        }

        RegisterDeclarations(unit.Contracts, unit.Modules);

        foreach (var decl in unit.Program.Declarations)
        {
            if (decl is FunctionDecl fn && fn.Name == "Main")
            {
                main = fn;
                break;
            }
        }

        if (main is null)
        {
            throw new System.Exception(
                "Runtime error: function 'Main' was not found");
        }

        Execute(main.Body);
    }

    #endregion

    #region Statement execution (actions, produce no value)

    /// <summary>Executes a single statement for its effect.</summary>
    /// <remarks>
    /// The statement half of the tree-walk (its counterpart is <see cref="Evaluate"/> for expressions).
    /// A <c>switch</c> pattern-matches the AST record type and performs its action — declaring a
    /// variable, running a block, looping, etc. Control-flow statements are handled by throwing signals:
    /// <c>stop</c> throws to break a loop and <c>return</c> throws to unwind a call, so those are caught
    /// where the loop/call is driven rather than handled inline here.
    /// </remarks>
    /// <param name="stmt">The statement node to execute.</param>
    private void Execute(Stmt stmt)
    {
        switch (stmt)
        {
            case VarDecl d:
                object? value = Evaluate(d.Initializer);
                if (d.DeclareType is not null)
                    value = Coerce(d.DeclareType, d.Name, value);
                environment.Define(d.Name, value);
                break;

            case ExpressionStmt e:
                Evaluate(e.Expression); // se evalúa por su efecto; el valor se descarta
                break;

            case Block b:
                ExecutionBlock(b, new Environment(environment));
                break;

            case WhenStmt w:
                if (IsTruthy(Evaluate(w.Condition)))
                    Execute(w.Then);
                else if (w.Else is not null)
                    Execute(w.Else);
                break;

            case StopStmt:
                throw new BreakSignal();

            case LoopStmt l:
                try
                {
                    while (true)
                        Execute(l.Body);
                }
                catch (BreakSignal) { }
                break;

            case WhileStmt w:
                try
                {
                    while (IsTruthy(Evaluate(w.Condition)))
                        Execute(w.Body);
                }
                catch (BreakSignal) { }
                break;

            case RangeLoopStmt r:
                int from = (int)Evaluate(r.From)!, to = (int)Evaluate(r.To)!;
                Environment loppEnv = new Environment(environment);
                Environment previous = environment;
                try
                {
                    environment = loppEnv;
                    for (int index = from; index <= to; index++)
                    {
                        loppEnv.Define(r.Variable, index);
                        Execute(r.Body);
                    }
                }
                catch (BreakSignal) { }
                finally
                {
                    environment = previous;
                }
                break;

            case ReturnStmt r:
                throw new ReturnSignal(r.value is null ? null : Evaluate(r.value));

            default:
                throw new System.Exception(
                    $"Unsupported statement: {stmt.GetType().Name}");
        }
    }

    /// <summary>Runs a block's statements in the given (child) environment, then restores the previous one.</summary>
    /// <remarks>
    /// This is what gives every block its own lexical scope: it swaps in <paramref name="blockEnv"/>,
    /// runs the statements, and restores the outer environment in a <c>finally</c> — so the scope is
    /// unwound cleanly even when a <c>stop</c>/<c>return</c> signal is thrown out of the block. Because
    /// declarations bind into the current environment, a <c>let</c> inside the block dies with it.
    /// </remarks>
    /// <param name="block">The block whose statements to run.</param>
    /// <param name="blockEnv">The fresh environment (child scope) to run them in.</param>
    private void ExecutionBlock(Block block, Environment blockEnv)
    {
        Environment previous = environment;
        try
        {
            environment = blockEnv;
            foreach (var inner in block.Statements)
                Execute(inner);
        }
        finally
        {
            environment = previous;
        }
    }

    #endregion

    #region Expression evaluation (produce a value)

    /// <summary>Evaluates an expression and returns its runtime value.</summary>
    /// <remarks>
    /// The expression half of the tree-walk. A <c>switch</c> maps each AST node to its value: literals
    /// return themselves, a <see cref="Variable"/> is looked up in the environment chain, and compound
    /// nodes delegate to a dedicated helper (<see cref="EvaluateBinary"/>, <see cref="EvaluateCall"/>,
    /// etc.). Runtime values are plain <c>object?</c> backed by the matching C# type (<c>int</c>,
    /// <c>double</c>, <c>string</c>, <c>bool</c>…), which the numeric and coercion helpers interpret.
    /// </remarks>
    /// <param name="expr">The expression node to evaluate.</param>
    private object? Evaluate(Expr expr)
    {
        return expr switch
        {
            NumberLiteral n => n.Value,
            StringLiteral s => s.Value,
            Variable v => environment.Get(v.Name),
            Assign a => AssignVariable(a),
            Binary b => EvaluateBinary(b),
            Call c => EvaluateCall(c),
            BooleanLiteral b => b.Value,
            CharLiteral c => c.Value,
            Logical l => EvaluateLogical(l),
            Unary u => EvaluateUnary(u),
            _ => throw new System.Exception(
                     $"Unsupported expression: {expr.GetType().Name}")
        };
    }

    /// <summary>Evaluates an assignment expression and returns the assigned value.</summary>
    /// <remarks>
    /// Assignment is an <i>expression</i>, so it yields a value (enabling <c>x = y = 5</c>). It
    /// <b>mutates</b> an already-declared variable via <c>environment.Assign</c>, which searches up the
    /// scope chain and errors if the name was never declared — this is the distinction from a <c>let</c>
    /// declaration, which always creates a new binding in the current scope.
    /// </remarks>
    private object? AssignVariable(Assign a)
    {
        object? value = Evaluate(a.Value);
        environment.Assign(a.Name, value);
        return value;
    }

    /// <summary>Evaluates a binary operation, choosing the arithmetic type by its operands.</summary>
    /// <remarks>
    /// Both operands are evaluated up front (which is exactly why <c>&amp;&amp;</c>/<c>||</c> are a
    /// separate <see cref="Logical"/> node — they must short-circuit). The order of the checks encodes
    /// the type rules: string <c>+</c> concatenates; <c>==</c>/<c>!=</c> compare any type; otherwise a
    /// <b>floating layer</b> (if either operand is <c>double</c>, else <c>float</c>) promotes integers to
    /// the float; else an <b>integer layer</b> promotes by max width with signedness winning, dispatching
    /// to the matching <c>Numeric*</c> helper. So <c>int + long</c> → long and <c>2.0 + 3</c> → double.
    /// </remarks>
    private object? EvaluateBinary(Binary b)
    {
        object? left = Evaluate(b.Left);
        object? right = Evaluate(b.Right);

        int width = System.Math.Max(Width(left), Width(right));
        bool signed = Signed(left) || Signed(right);

        if (b.Operator == TokenType.PLUS && (left is string || right is string))
            return Stringify(left) + Stringify(right);

        if (b.Operator == TokenType.EQUAL_EQUAL) return left!.Equals(right);
        if (b.Operator == TokenType.BANG_EQUAL) return !left!.Equals(right);

        // Floating layer: if any operand is decimal, the operation is decimal.
        // double wins over float; integers are promoted to the floating type.
        if (left is double || right is double)
            return NumericDouble(b.Operator, ToDouble(left), ToDouble(right));
        if (left is float || right is float)
            return NumericFloat(b.Operator, ToFloat(left), ToFloat(right));

        // Integers: promotion by max width, signed wins.
        return (width, signed) switch
        {
            (32, true) => NumericInt(b.Operator, ToInt(left), ToInt(right)),
            (32, false) => NumericUInt(b.Operator, ToUInt(left), ToUInt(right)),
            (64, true) => NumericLong(b.Operator, ToLong(left), ToLong(right)),
            _ => NumericUlong(b.Operator, ToULong(left), ToULong(right))
        };
    }

    /// <summary>Evaluates a call, resolving it to either a module member or a top-level function.</summary>
    /// <remarks>
    /// The callee's <i>shape</i> decides the target: a <c>Module.member(args)</c> (a
    /// <see cref="MemberAccess"/> on a known module name) dispatches through
    /// <see cref="CallModuleFunction"/>; a bare <c>name(args)</c> that matches a registered function runs
    /// via <see cref="CallFunction"/>. Anything else is an unsupported call. This static, name-based
    /// resolution is why modules are namespaces rather than first-class values in this version.
    /// </remarks>
    private object? EvaluateCall(Call call)
    {
        // Module member call: Module.function(args)
        if (call.Callee is MemberAccess member
            && member.Object is Variable moduleRef
            && modules.TryGetValue(moduleRef.Name, out var module))
        {
            return CallModuleFunction(module, member.Member, call.Arguments);
        }

        // User-defined top-level function: name(args)
        if (call.Callee is Variable fnRef && functions.TryGetValue(fnRef.Name, out var fn))
            return CallFunction(fn, call.Arguments);

        throw new System.Exception(
            "Unsupported call: expected 'Module.function(...)' or a defined function");
    }

    /// <summary>Resolves and invokes a function inside a module.</summary>
    /// <remarks>
    /// Finds the named function in the module, then splits on <see cref="FunctionDecl.IsExternal"/>: an
    /// <c>external</c> function dispatches to the native registered under <c>"Module.function"</c>, while
    /// a language-bodied one runs through <see cref="CallFunction"/>. Note that external arguments are
    /// passed <b>raw</b> (evaluated but not coerced) — the native does its own marshaling, which is why
    /// <c>Term.out</c> can print any type; coercion applies only to language-bodied functions.
    /// </remarks>
    /// <param name="module">The module the call targets.</param>
    /// <param name="name">The member function name.</param>
    /// <param name="args">The argument expressions, evaluated here.</param>
    private object? CallModuleFunction(ModuleDecl module, string name, IReadOnlyList<Expr> args)
    {
        FunctionDecl? fn = null;
        foreach (var candidate in module.Functions)
            if (candidate.Name == name) { fn = candidate; break; }

        if (fn is null)
            throw new System.Exception(
                $"module '{module.Name}' has no function '{name}'");

        if (!fn.IsExternal)
            return CallFunction(fn, args);

        string key = $"{module.Name}.{name}";
        if (!natives.TryGetValue(key, out var native))
            throw new System.Exception($"no native implementation for '{key}'");

        if (args.Count != fn.Parameters.Count)
            throw new System.Exception(
                $"'{key}' expects {fn.Parameters.Count} argument(s), but received {args.Count}");

        // external functions do NOT coerce: the native receives the raw values
        // and does its own marshaling (e.g. Term.out prints any type via
        // Stringify). Type coercion is the job of language-bodied functions
        // (CallFunction).
        var values = new List<object?>(args.Count);
        for (int i = 0; i < args.Count; i++)
            values.Add(Evaluate(args[i]));

        return native(values);
    }


    /// <summary>Calls a language-bodied function: checks arity, binds coerced arguments, runs the body, returns the result.</summary>
    /// <remarks>
    /// The call runs in a <b>child of the global scope, not the caller's</b> — that is what makes scoping
    /// lexical (a function cannot see its caller's locals). Arguments are evaluated in the caller's
    /// context but coerced to each parameter's declared type before binding. A <c>return</c> unwinds via
    /// a <see cref="ReturnSignal"/> caught here; the result is then coerced to the declared return type
    /// unless the function is <c>empty</c> (void).
    /// </remarks>
    /// <param name="fn">The function to call.</param>
    /// <param name="args">The argument expressions.</param>
    private object? CallFunction(FunctionDecl fn, IReadOnlyList<Expr> args)
    {
        int count = fn.Parameters.Count;
        if (args.Count != count)
            throw new System.Exception(
                $"function '{fn.Name}' expects {count} argument(s), but received {args.Count}");

        // Child of the global scope (lexical), not of the caller.
        var callEnv = new Environment(globals);

        // Evaluate each arg in the current context and define it already coerced;
        // the intermediate `values` array is avoided.
        for (int i = 0; i < count; i++)
        {
            Param param = fn.Parameters[i];
            object? value = Evaluate(args[i]);
            callEnv.Define(param.Name, Coerce(param.Type, param.Name, value));
        }

        object? result = null;
        Environment previous = environment;
        environment = callEnv;
        try
        {
            var body = fn.Body.Statements;
            for (int i = 0; i < body.Count; i++)
                Execute(body[i]);
        }
        catch (ReturnSignal signal)
        {
            result = signal.Value;
        }
        finally
        {
            environment = previous;
        }

        if (result is not null && fn.ReturnType != "empty")
            result = Coerce(fn.ReturnType, fn.Name, result);

        return result;
    }

    /// <summary>Evaluates a short-circuiting logical <c>&amp;&amp;</c> / <c>||</c>.</summary>
    /// <remarks>
    /// Evaluates the left operand first, then leans on C#'s own <c>&amp;&amp;</c>/<c>||</c> so the right
    /// operand's <see cref="Evaluate"/> only runs when needed — genuine short-circuit (e.g.
    /// <c>false &amp;&amp; (1/0==1)</c> yields <c>false</c> without a division error). Operands must be
    /// strict booleans via <see cref="IsTruthy"/>; there is no truthiness coercion.
    /// </remarks>
    private object? EvaluateLogical(Logical l)
    {
        bool left = IsTruthy(Evaluate(l.Left));
        return l.Operator == TokenType.OR
            ? left || IsTruthy(Evaluate(l.Right))
            : left && IsTruthy(Evaluate(l.Right));
    }

    /// <summary>Evaluates the prefix logical-not (<c>!</c>).</summary>
    /// <remarks>
    /// Negates its operand, which must be a strict boolean (via <see cref="IsTruthy"/>), consistent with
    /// <c>when</c> conditions and the logical operators.
    /// </remarks>
    private object? EvaluateUnary(Unary u)
    {
        object? rightValue = Evaluate(u.Right);
        return !IsTruthy(rightValue);
    }

    #endregion

    #region Numeric operations (per-type arithmetic + classification)

    /// <summary>Applies an arithmetic/comparison operator to two <c>int</c> operands.</summary>
    /// <remarks>
    /// Integer arithmetic is <c>checked</c>: overflow is turned into an <see cref="OverflowError"/> and
    /// division by zero into a <see cref="MathError"/>, rather than wrapping or throwing a raw CLR
    /// exception. Comparison operators return a <c>bool</c>. Equality is handled earlier in
    /// <see cref="EvaluateBinary"/>, so it is intentionally absent here.
    /// </remarks>
    private static object? NumericInt(TokenType op, int a, int b)
    {
        try
        {
            return op switch
            {
                TokenType.PLUS => checked(a + b),
                TokenType.MINUS => checked(a - b),
                TokenType.STAR => checked(a * b),
                TokenType.SLASH => b == 0 ? throw new MathError("Division by zero detected") : checked(a / b),
                TokenType.GREATER => a > b,
                TokenType.GREATER_EQUAL => a >= b,
                TokenType.LESS => a < b,
                TokenType.LESS_EQUAL => a <= b,
                // TokenType.EQUAL_EQUAL => a == b,
                // TokenType.BANG_EQUAL => a != b,
                _ => throw new MathError("Invalid operator for int operation: " + op)
            };
        }
        catch (System.OverflowException)
        {
            throw new OverflowError($"Overflow detected in int operation : {op} {a} {b}");
        }
    }

    /// <summary>Applies an operator to two <c>uint</c> operands (checked; same rules as <see cref="NumericInt"/>).</summary>
    private static object? NumericUInt(TokenType op, uint a, uint b)
    {
        try
        {
            return op switch
            {
                TokenType.PLUS => checked(a + b),
                TokenType.MINUS => checked(a - b),
                TokenType.STAR => checked(a * b),
                TokenType.SLASH => b == 0 ? throw new MathError("Division by zero detected") : checked(a / b),
                TokenType.GREATER => a > b,
                TokenType.GREATER_EQUAL => a >= b,
                TokenType.LESS => a < b,
                TokenType.LESS_EQUAL => a <= b,
                // TokenType.EQUAL_EQUAL => a == b,
                // TokenType.BANG_EQUAL => a != b,
                _ => throw new MathError("Invalid operator for uint operation: " + op)
            };
        }
        catch (System.OverflowException)
        {
            throw new OverflowError($"Overflow detected in uint operation : {op} {a} {b}");
        }
    }

    /// <summary>Applies an operator to two <c>long</c> operands (checked; same rules as <see cref="NumericInt"/>).</summary>
    private static object? NumericLong(TokenType op, long a, long b)
    {
        try
        {
            return op switch
            {
                TokenType.PLUS => checked(a + b),
                TokenType.MINUS => checked(a - b),
                TokenType.STAR => checked(a * b),
                TokenType.SLASH => b == 0 ? throw new MathError("Division by zero detected") : checked(a / b),
                TokenType.GREATER => a > b,
                TokenType.GREATER_EQUAL => a >= b,
                TokenType.LESS => a < b,
                TokenType.LESS_EQUAL => a <= b,
                // TokenType.EQUAL_EQUAL => a == b,
                // TokenType.BANG_EQUAL => a != b,
                _ => throw new MathError("Invalid operator for int operation: " + op)
            };
        }
        catch (System.OverflowException)
        {
            throw new OverflowError($"Overflow detected in long operation : {op} {a} {b}");
        }
    }

    /// <summary>Applies an operator to two <c>ulong</c> operands (checked; same rules as <see cref="NumericInt"/>).</summary>
    private static object? NumericUlong(TokenType op, ulong a, ulong b)
    {
        try
        {
            return op switch
            {
                TokenType.PLUS => checked(a + b),
                TokenType.MINUS => checked(a - b),
                TokenType.STAR => checked(a * b),
                TokenType.SLASH => b == 0 ? throw new MathError("Division by zero detected") : checked(a / b),
                TokenType.GREATER => a > b,
                TokenType.GREATER_EQUAL => a >= b,
                TokenType.LESS => a < b,
                TokenType.LESS_EQUAL => a <= b,
                _ => throw new MathError("Invalid operator for ulong operation: " + op)
            };
        }
        catch (System.OverflowException)
        {
            throw new OverflowError($"Overflow detected in ulong operation : {op} {a} {b}");
        }
    }

    /// <summary>Applies an operator to two <c>double</c> operands using native IEEE semantics.</summary>
    /// <remarks>
    /// Unlike the integer helpers, floating-point is <b>not</b> checked and has no special
    /// division-by-zero case: <c>/0.0</c> yields Infinity, overflow yields Infinity, and NaN is a valid
    /// value. This mirrors how the CLR (and hardware) treat IEEE floats.
    /// </remarks>
    private static object? NumericDouble(TokenType op, double a, double b) => op switch
    {
        TokenType.PLUS => a + b,
        TokenType.MINUS => a - b,
        TokenType.STAR => a * b,
        TokenType.SLASH => a / b,
        TokenType.GREATER => a > b,
        TokenType.GREATER_EQUAL => a >= b,
        TokenType.LESS => a < b,
        TokenType.LESS_EQUAL => a <= b,
        _ => throw new MathError("Invalid operator for double operation: " + op)
    };

    /// <summary>Applies an operator to two <c>float</c> operands using native IEEE semantics (see <see cref="NumericDouble"/>).</summary>
    private static object? NumericFloat(TokenType op, float a, float b) => op switch
    {
        TokenType.PLUS => a + b,
        TokenType.MINUS => a - b,
        TokenType.STAR => a * b,
        TokenType.SLASH => a / b,
        TokenType.GREATER => a > b,
        TokenType.GREATER_EQUAL => a >= b,
        TokenType.LESS => a < b,
        TokenType.LESS_EQUAL => a <= b,
        _ => throw new MathError("Invalid operator for float operation: " + op)
    };

    /// <summary>Classifies a value's integer bit width: 64 for <c>long</c>/<c>ulong</c>, else 32.</summary>
    /// <remarks>Half of the integer promotion rule in <see cref="EvaluateBinary"/> (the wider width wins).</remarks>
    private static int Width(object? v)
        => v is long || v is ulong ? 64 : 32;

    /// <summary>Classifies whether a value's integer type is signed (<c>int</c>/<c>long</c>).</summary>
    /// <remarks>The other half of the promotion rule: if either operand is signed, the result type is signed.</remarks>
    private static bool Signed(object? v)
        => v is int || v is long;

    #endregion

    #region Type coercion & conversion

    /// <summary>Checks and converts a value to a declared type name, or throws a typed error.</summary>
    /// <remarks>
    /// The single gate for the language's <b>dynamic</b> type checking. Reference-like types
    /// (<c>string</c>/<c>bool</c>/<c>char</c>) must match exactly or a <see cref="TypeError"/> is thrown;
    /// numeric types delegate to the <c>To*</c> helpers, which range-check and convert (so <c>uint x = 0 - 1;</c>
    /// overflows). Used by typed <c>let</c> declarations, parameter binding and return values — an unknown
    /// type name passes the value through unchanged.
    /// </remarks>
    /// <param name="type">The declared type name to coerce to.</param>
    /// <param name="name">The variable/parameter name, used only for error messages.</param>
    /// <param name="value">The runtime value to check and convert.</param>
    private static object? Coerce(string type, string name, object? value) => type switch
    {
        "string" => value is string ? value : throw new TypeError(TypeError.ErrorMessage(name, "string", value?.GetType().Name ?? "null")),
        "bool" => value is bool ? value : throw new TypeError(TypeError.ErrorMessage(name, "bool", value?.GetType().Name ?? "null")),
        "char" => value is char ? value : throw new TypeError(TypeError.ErrorMessage(name, "char", value?.GetType().Name ?? "null")),
        "int" => ToInt(value),
        "uint" => ToUInt(value),
        "long" => ToLong(value),
        "ulong" => ToULong(value),
        "double" => ToDouble(value),
        "float" => ToFloat(value),
        _ => value
    };

    /// <summary>Converts a numeric value to <c>int</c>, throwing <see cref="OverflowError"/> if it does not fit.</summary>
    /// <remarks>Routes through <see cref="AsLong"/> first, so a non-integer (decimal) source is rejected before the range check.</remarks>
    private static int ToInt(object? value)
    {
        long number = AsLong(value);
        if (number < int.MinValue || number > int.MaxValue)
            throw new OverflowError($"value {number} does not fit in int");
        return (int)number;
    }

    /// <summary>Converts a numeric value to <c>uint</c>, throwing <see cref="OverflowError"/> if negative or too large.</summary>
    /// <remarks>Since there are no unsigned literals, this range-checked conversion is the only way an unsigned value is produced.</remarks>
    private static uint ToUInt(object? value)
    {
        long number = AsLong(value);
        if (number < 0 || number > uint.MaxValue)
            throw new OverflowError($"value {number} does not fit in uint");
        return (uint)number;
    }

    /// <summary>Widens any integer value to <c>long</c> (a <c>ulong</c> too large to fit throws).</summary>
    private static long ToLong(object? value) => value switch
    {
        int i => i,
        uint u => u,
        long l => l,
        ulong ul => ul <= long.MaxValue ? (long)ul : throw new OverflowError("can't insert ulong into long"),
        _ => throw new MathError($"Invalid number {value}")
    };

    /// <summary>Converts an integer value to <c>ulong</c> (a negative source throws).</summary>
    private static ulong ToULong(object? value) => value switch
    {
        int i => i >= 0 ? (ulong)i : throw new OverflowError("negative value is not ulong"),
        long l => l >= 0 ? (ulong)l : throw new OverflowError("negative valur is not ulong"),
        uint u => u,
        ulong ul => ul,
        _ => throw new MathError($"Invalid number {value}")
    };

    /// <summary>Widens any numeric value to <c>double</c> (the widest floating type).</summary>
    /// <remarks>Used by the floating layer of <see cref="EvaluateBinary"/> to promote integer operands before a double operation.</remarks>
    private static double ToDouble(object? value) => value switch
    {
        int i => i,
        uint u => u,
        long l => l,
        ulong ul => ul,
        double d => d,
        float f => f,
        _ => throw new MathError($"Invalid number {value}")
    };

    /// <summary>Converts a numeric value to <c>float</c>, narrowing a <c>double</c> if needed.</summary>
    /// <remarks>The <c>double</c> → <c>float</c> narrowing is intentional (a <c>float</c> operation may lose precision).</remarks>
    private static float ToFloat(object? value) => value switch
    {
        int i => i,
        uint u => u,
        long l => l,
        ulong ul => ul,
        float f => f,
        double d => (float)d,   // narrowing double->float (expected)
        _ => throw new MathError($"Invalid number {value}")
    };

    /// <summary>Reads any integer value as a <c>long</c> — the shared integer path used by <see cref="ToInt"/>/<see cref="ToUInt"/>.</summary>
    /// <remarks>
    /// Crucially, it <b>rejects decimals</b>: a <c>double</c>/<c>float</c> source throws, which is what makes
    /// assigning a decimal to an integer-typed variable an error rather than a silent truncation.
    /// </remarks>
    private static long AsLong(object? value) => value switch
    {
        int i => i,
        uint u => u,
        long l => l,
        ulong ul => ul <= long.MaxValue ? (long)ul : throw new OverflowError("ulong don't ose same space that long"),
        double or float => throw new OverflowError("cannot assign a decimal to an integer type"),
        _ => throw new MathError($"Invalid number {value}")
    };

    #endregion

    #region Runtime helpers

    /// <summary>Requires a value to be a strict boolean, returning it or throwing.</summary>
    /// <remarks>
    /// There is <b>no truthiness coercion</b>: a non-bool (e.g. <c>when(5)</c>) is a runtime error, not
    /// "true". Shared by <c>when</c> conditions, loop conditions, and the logical operators so all three
    /// enforce the same strict rule.
    /// </remarks>
    private static bool IsTruthy(object? value)
    {
        if (value is bool b) return b;
        throw new System.Exception("the 'when' condition must be a boolean");
    }

    /// <summary>Renders a runtime value as text for output and string concatenation.</summary>
    /// <remarks>
    /// <c>null</c> becomes the empty string. Decimals are formatted with
    /// <see cref="CultureInfo.InvariantCulture"/> so the output uses <c>.</c> (matching the input syntax)
    /// regardless of the machine's locale. This is what <c>Term.out</c> and string <c>+</c> use to print
    /// any value.
    /// </remarks>
    private static string Stringify(object? value) => value switch
    {
        null => "",
        double d => d.ToString(CultureInfo.InvariantCulture),
        float f => f.ToString(CultureInfo.InvariantCulture),
        _ => value.ToString() ?? ""
    };

    /// <summary>Validates a module at registration time, throwing on any inconsistency.</summary>
    /// <remarks>
    /// Two checks give modules and contracts their meaning: (1) if the module declares a contract, it
    /// must implement every declared signature with a matching shape — name, parameter types and return
    /// type — otherwise the <c>: IContract</c> would be decorative; and (2) every <c>external</c> function
    /// must have a native registered under <c>"Module.function"</c>, so an unbound native is caught up
    /// front rather than at the first call.
    /// </remarks>
    /// <param name="module">The module to validate.</param>
    private void ValidateModule(ModuleDecl module)
    {
        if (module.Contract is not null)
        {
            if (!contracts.TryGetValue(module.Contract, out var contract))
                throw new System.Exception(
                    $"module '{module.Name}' implements an unknown contract '{module.Contract}'");

            foreach (var sig in contract.Members)
            {
                FunctionDecl? impl = null;
                foreach (var f in module.Functions)
                    if (f.Name == sig.Name) { impl = f; break; }

                if (impl is null)
                    throw new System.Exception(
                        $"module '{module.Name}' does not implement '{sig.Name}' from contract '{contract.Name}'");

                bool sameShape = impl.ReturnType == sig.ReturnType
                    && impl.Parameters.Count == sig.Parameters.Count;
                for (int i = 0; sameShape && i < sig.Parameters.Count; i++)
                    if (impl.Parameters[i].Type != sig.Parameters[i].Type)
                        sameShape = false;

                if (!sameShape)
                    throw new System.Exception(
                        $"the signature of '{sig.Name}' in '{module.Name}' does not match contract '{contract.Name}'");
            }
        }

        foreach (var f in module.Functions)
            if (f.IsExternal && !natives.ContainsKey($"{module.Name}.{f.Name}"))
                throw new System.Exception(
                    $"external function '{module.Name}.{f.Name}' has no registered native implementation");
    }

    #endregion
}
