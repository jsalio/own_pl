using System.Globalization;
using Own_Lang.Internal.Error;

namespace Own_Lang.Internal.Contracts;

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

    private readonly Environment globals = new();
    private Environment environment = new();
    private readonly Dictionary<string, FunctionDecl> functions = new();
    private readonly Dictionary<string, ContractDecl> contracts = new();
    private readonly Dictionary<string, ModuleDecl> modules = new();
    private readonly Dictionary<string, System.Func<IReadOnlyList<object?>, object?>> natives = new();

    #endregion

    #region Entry point

    public Interpreter()
    {
        environment = globals;
        RegisterBuiltins();
    }

    // Registra los módulos primitivos que aterrizan en C# (la "stdlib nativa").
    // Esta es la costura por donde una futura prelude en .own se enchufará.
    private void RegisterBuiltins()
    {
        // def module Term { external function empty out(string message); }
        var outFn = new FunctionDecl("empty", "out",
            new List<Param> { new Param("string", "message") },
            new Block(new List<Stmt>()), IsExternal: true);
        modules["Term"] = new ModuleDecl("Term", null,
            new List<FunctionDecl> { outFn });

        natives["Term.out"] = args =>
        {
            object? message = args.Count > 0 ? args[0] : null;
            System.Console.WriteLine(Stringify(message));
            return null;
        };
    }

    /// <inheritdoc/>
    public void Interpret(CompilationUnit unit)
    {
        ProgramDecl? program = unit.Program;
        FunctionDecl? main = null;


        foreach (var decl in program.Declarations)
        {
            if (decl is FunctionDecl fn)
                functions[fn.Name] = fn;
        }

        foreach (var contract in unit.Contracts)
        {
            if (contracts.ContainsKey(contract.Name))
                throw new System.Exception(
                    $"Error en tiempo de ejecución: el contrato '{contract.Name}' ya está definido");
            contracts[contract.Name] = contract;
        }

        foreach (var module in unit.Modules)
        {
            if (modules.ContainsKey(module.Name))
                throw new System.Exception(
                    $"Error en tiempo de ejecución: el módulo '{module.Name}' ya está definido");
            modules[module.Name] = module;
        }

        foreach (var module in unit.Modules)
            ValidateModule(module);

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
                "Error en tiempo de ejecución: no se encontró la función 'Main'");
        }

        Execute(main.Body);
    }

    #endregion

    #region Statement execution (acciones, no devuelven valor)

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
                //foreach (var inner in b.Statements)
                //{
                //    Execute(inner);
                //}
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
                //int from = (int)Evaluate(r.From)!, to = (int)Evaluate(r.To)!;
                //try
                //{
                //    for (int index = from; index <= to; index++)
                //    {
                //        environment.Define(r.Variable, index); // expone el contador
                //        Execute(r.Body);
                //    }
                //}
                //catch (BreakSignal) { }
                break;

            case ReturnStmt r:
                throw new ReturnSignal(r.value is null ? null : Evaluate(r.value));

            default:
                throw new System.Exception(
                    $"Sentencia no soportada: {stmt.GetType().Name}");
        }
    }

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

    #region Expression evaluation (producen un valor)

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
                     $"Expresión no soportada: {expr.GetType().Name}")
        };
    }

    // La asignación es una expresión: muta la variable ya declarada (subiendo la
    // cadena de scopes) y devuelve el valor asignado.
    private object? AssignVariable(Assign a)
    {
        object? value = Evaluate(a.Value);
        environment.Assign(a.Name, value);
        return value;
    }

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

        // Capa flotante: si algún operando es decimal, la operación es decimal.
        // double gana a float; los enteros se promueven al flotante.
        if (left is double || right is double)
            return NumericDouble(b.Operator, ToDouble(left), ToDouble(right));
        if (left is float || right is float)
            return NumericFloat(b.Operator, ToFloat(left), ToFloat(right));

        // Enteros: promoción por ancho máximo, gana signed.
        return (width, signed) switch
        {
            (32, true) => NumericInt(b.Operator, ToInt(left), ToInt(right)),
            (32, false) => NumericUInt(b.Operator, ToUInt(left), ToUInt(right)),
            (64, true) => NumericLong(b.Operator, ToLong(left), ToLong(right)),
            _ => NumericUlong(b.Operator, ToULong(left), ToULong(right))
        };
    }

    private object? EvaluateCall(Call call)
    {
        // Llamada a miembro de módulo: Modulo.funcion(args)
        if (call.Callee is MemberAccess member
            && member.Object is Variable moduleRef
            && modules.TryGetValue(moduleRef.Name, out var module))
        {
            return CallModuleFunction(module, member.Member, call.Arguments);
        }

        // Función top-level definida por el usuario: nombre(args)
        if (call.Callee is Variable fnRef && functions.TryGetValue(fnRef.Name, out var fn))
            return CallFunction(fn, call.Arguments);

        throw new System.Exception(
            "Llamada no soportada: se esperaba 'Módulo.función(...)' o una función definida");
    }

    // Resuelve una función dentro de un módulo: si es 'external' despacha al
    // nativo registrado bajo "Módulo.función"; si no, ejecuta su cuerpo como
    // una función normal (CallFunction).
    private object? CallModuleFunction(ModuleDecl module, string name, IReadOnlyList<Expr> args)
    {
        FunctionDecl? fn = null;
        foreach (var candidate in module.Functions)
            if (candidate.Name == name) { fn = candidate; break; }

        if (fn is null)
            throw new System.Exception(
                $"el módulo '{module.Name}' no tiene una función '{name}'");

        if (!fn.IsExternal)
            return CallFunction(fn, args);

        string key = $"{module.Name}.{name}";
        if (!natives.TryGetValue(key, out var native))
            throw new System.Exception($"no hay implementación nativa para '{key}'");

        if (args.Count != fn.Parameters.Count)
            throw new System.Exception(
                $"'{key}' expects {fn.Parameters.Count} argument(s), but received {args.Count}");

        // Las funciones external NO coercionan: el nativo recibe los valores
        // crudos y hace su propio marshaling (p.ej. Term.out imprime cualquier
        // tipo vía Stringify). La coerción por tipo es cosa de las funciones
        // con cuerpo en el lenguaje (CallFunction).
        var values = new List<object?>(args.Count);
        for (int i = 0; i < args.Count; i++)
            values.Add(Evaluate(args[i]));

        return native(values);
    }


    private object? CallFunction(FunctionDecl fn, IReadOnlyList<Expr> args)
    {
        int count = fn.Parameters.Count;
        if (args.Count != count)
            throw new System.Exception(
                $"function '{fn.Name}' expects {count} argument(s), but received {args.Count}");

        // Scope hijo del global (léxico), no del llamante.
        var callEnv = new Environment(globals);

        // Evalúa cada arg en el contexto actual y define ya coercionado:
        // se elimina el array intermedio `values`.
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

    private object? EvaluateLogical(Logical l)
    {
        bool left = IsTruthy(Evaluate(l.Left));
        return l.Operator == TokenType.OR
            ? left || IsTruthy(Evaluate(l.Right))
            : left && IsTruthy(Evaluate(l.Right));
    }

    private object? EvaluateUnary(Unary u)
    {
        object? rightValue = Evaluate(u.Right);
        return !IsTruthy(rightValue);
    }

    #endregion

    #region Numeric operations (aritmética por tipo + clasificación)

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

    // Flotantes: semántica IEEE nativa. Sin checked ni división-por-cero especial
    // (/0.0 -> Infinity, overflow -> Infinity, NaN es un valor válido).
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

    private static int Width(object? v)
        => v is long || v is ulong ? 64 : 32;

    private static bool Signed(object? v)
        => v is int || v is long;

    #endregion

    #region Type coercion & conversion

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

    private static int ToInt(object? value)
    {
        long number = AsLong(value);
        if (number < int.MinValue || number > int.MaxValue)
            throw new OverflowError($"value {number} does not fit in int");
        return (int)number;
    }

    private static uint ToUInt(object? value)
    {
        long number = AsLong(value);
        if (number < 0 || number > uint.MaxValue)
            throw new OverflowError($"value {number} does not fit in uint");
        return (uint)number;
    }

    private static long ToLong(object? value) => value switch
    {
        int i => i,
        uint u => u,
        long l => l,
        ulong ul => ul <= long.MaxValue ? (long)ul : throw new OverflowError("can't insert ulong into long"),
        _ => throw new MathError($"Invalid number {value}")
    };

    private static ulong ToULong(object? value) => value switch
    {
        int i => i >= 0 ? (ulong)i : throw new OverflowError("negative value is not ulong"),
        long l => l >= 0 ? (ulong)l : throw new OverflowError("negative valur is not ulong"),
        uint u => u,
        ulong ul => ul,
        _ => throw new MathError($"Invalid number {value}")
    };

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

    private static float ToFloat(object? value) => value switch
    {
        int i => i,
        uint u => u,
        long l => l,
        ulong ul => ul,
        float f => f,
        double d => (float)d,   // narrowing double->float (esperado)
        _ => throw new MathError($"Invalid number {value}")
    };

    private static long AsLong(object? value) => value switch
    {
        int i => i,
        uint u => u,
        long l => l,
        ulong ul => ul <= long.MaxValue ? (long)ul : throw new OverflowError("ulong don't ose same space that long"),
        double or float => throw new OverflowError("no se puede asignar un decimal a un tipo entero"),
        _ => throw new MathError($"Invalid number {value}")
    };

    #endregion

    #region Runtime helpers

    private static bool IsTruthy(object? value)
    {
        if (value is bool b) return b;
        throw new System.Exception("La condición de 'when' debe ser booleana");
    }

    // Formatea decimales con InvariantCulture para que la salida use '.' (igual
    // que la sintaxis de entrada) sin depender del locale del sistema.
    private static string Stringify(object? value) => value switch
    {
        null => "",
        double d => d.ToString(CultureInfo.InvariantCulture),
        float f => f.ToString(CultureInfo.InvariantCulture),
        _ => value.ToString() ?? ""
    };

    // Valida un módulo al registrarlo: (1) si implementa un contrato, debe
    // proveer toda firma declarada (nombre + tipos de parámetros + retorno);
    // (2) toda función 'external' debe tener un nativo registrado.
    private void ValidateModule(ModuleDecl module)
    {
        if (module.Contract is not null)
        {
            if (!contracts.TryGetValue(module.Contract, out var contract))
                throw new System.Exception(
                    $"el módulo '{module.Name}' implementa un contrato desconocido '{module.Contract}'");

            foreach (var sig in contract.Members)
            {
                FunctionDecl? impl = null;
                foreach (var f in module.Functions)
                    if (f.Name == sig.Name) { impl = f; break; }

                if (impl is null)
                    throw new System.Exception(
                        $"el módulo '{module.Name}' no implementa '{sig.Name}' del contrato '{contract.Name}'");

                bool sameShape = impl.ReturnType == sig.ReturnType
                    && impl.Parameters.Count == sig.Parameters.Count;
                for (int i = 0; sameShape && i < sig.Parameters.Count; i++)
                    if (impl.Parameters[i].Type != sig.Parameters[i].Type)
                        sameShape = false;

                if (!sameShape)
                    throw new System.Exception(
                        $"la firma de '{sig.Name}' en '{module.Name}' no coincide con el contrato '{contract.Name}'");
            }
        }

        foreach (var f in module.Functions)
            if (f.IsExternal && !natives.ContainsKey($"{module.Name}.{f.Name}"))
                throw new System.Exception(
                    $"la función external '{module.Name}.{f.Name}' no tiene implementación nativa registrada");
    }

    #endregion
}
