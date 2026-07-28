using Own_Lang.Internal.Error;

namespace Own_Lang.Internal.Contracts;

/// <summary>
/// Stage 3 implementation: a tree-walking interpreter. Splits traversal into
/// <c>Evaluate</c> (for <see cref="Expr"/>, returning a value) and <c>Execute</c>
/// (for <see cref="Stmt"/>, performing an action), both dispatching by pattern
/// matching over the AST record types. Variables are held in a single
/// <see cref="Environment"/>.
/// </summary>
internal sealed class Interpreter : IInterpreter
{
    private readonly Environment environment = new();

    // ---- Punto de entrada (contrato de IInterpreter) ----

    /// <inheritdoc/>
    public void Interpret(ProgramDecl program)
    {
        FunctionDecl? main = null;
        foreach (var decl in program.Declarations)
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

    // ---- Ejecución de sentencias (hacen acciones, no devuelven valor) ----

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
                foreach (var inner in b.Statements)
                {
                    Execute(inner);
                }
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
                try
                {
                    for (int index = from; index <= to; index++)
                    {
                        environment.Define(r.Variable, index); // expone el contador
                        Execute(r.Body);
                    }
                }
                catch (BreakSignal) { }
                break;

            default:
                throw new System.Exception(
                    $"Sentencia no soportada: {stmt.GetType().Name}");
        }
    }

    // ---- Evaluación de expresiones (producen un valor) ----

    private static void CheckType(string declareType, string name, object? value)
    {
        bool ok = declareType switch
        {
            "string" => value is string,
            "bool" => value is bool,
            "char" => value is char,
            _ => true
        };
        if (!ok)
            throw new System.Exception(
                    $"Error in type : '{name}' is declare as {declareType} but ," +
                    $"received {value?.GetType().Name ?? null}"
                    );
    }

    private object? Evaluate(Expr expr)
    {
        return expr switch
        {
            NumberLiteral n => n.Value,
            StringLiteral s => s.Value,
            Variable v => environment.Get(v.Name),
            Binary b => EvaluateBinary(b),
            Call c => EvaluateCall(c),
            BooleanLiteral b => b.Value,
            CharLiteral c => c.Value,
            _ => throw new System.Exception(
                     $"Expresión no soportada: {expr.GetType().Name}")
        };
    }

    private static object? Coerce(string type, string name, object? value) => type switch
    {
        "string" => value is string ? value : throw new TypeError(TypeError.ErrorMessage(name, "string", value?.GetType().Name ?? "null")),
        "bool" => value is bool ? value : throw new TypeError(TypeError.ErrorMessage(name, "bool", value?.GetType().Name ?? "null")),
        "char" => value is char ? value : throw new TypeError(TypeError.ErrorMessage(name, "char", value?.GetType().Name ?? "null")),
        "int" => ToInt(value),
        "uint" => ToUInt(value),
        "long" => ToLong(value),
        "ulong" => ToULong(value),
        _ => value
    };

    private static bool IsTruthy(object? value)
    {
        if (value is bool b) return b;
        throw new System.Exception("La condición de 'when' debe ser booleana");
    }

    private static int ToInt(object? value)
    {
        long number = AsLong(value);
        if (number > int.MaxValue || number > int.MaxValue)
            throw new OverflowError("don't use int space");
        return (int)number;
    }

    private static uint ToUInt(object? value)
    {
        long number = AsLong(value);
        if (number < 0 || number > uint.MaxValue)
            throw new OverflowError("");
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

    private static long AsLong(object? value) => value switch
    {
        int i => i,
        uint u => u,
        long l => l,
        ulong ul => ul <= long.MaxValue ? (long)ul : throw new OverflowError("ulong don't ose same space that long"),
        _ => throw new MathError($"Invalid number {value}")
    };

    private object? EvaluateCall(Call call)
    {
        // Caso especial cableado: term.out(x) -> Console.WriteLine(x)
        // (aún no implementamos objetos ni métodos de verdad)
        if (call.Callee is MemberAccess member
            && member.Object is Variable target
            && target.Name == "term"
            && member.Member == "out")
        {
            object? argument = call.Arguments.Count > 0
                ? Evaluate(call.Arguments[0])
                : null;

            System.Console.WriteLine(argument);
            return null;
        }

        throw new System.Exception(
            "Llamada no soportada: por ahora solo existe 'term.out(...)'");
    }

    private static string Stringify(object? value)
        => value?.ToString() ?? "";

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
                // TokenType.EQUAL_EQUAL => a == b,
                // TokenType.BANG_EQUAL => a != b,
                _ => throw new MathError("Invalid operator for ulong operation: " + op)
            };
        }
        catch (System.OverflowException)
        {
            throw new OverflowError($"Overflow detected in ulong operation : {op} {a} {b}");
        }
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

        return (width, signed) switch
        {
            (32, true) => NumericInt(b.Operator, ToInt(left), ToInt(right)),
            (32, false) => NumericUInt(b.Operator, ToUInt(left), ToUInt(right)),
            (64, true) => NumericLong(b.Operator, ToLong(left), ToLong(right)),
            _ => NumericUlong(b.Operator, ToULong(left), ToULong(right))
        };

        //if (left is uint lu && right is uint ru)
        //    return NumericUInt(b.Operator, lu, ru);

        //return NumericInt(b.Operator, ToInt(left), ToInt(right));
    }

    private static int Width(object? v)
        => v is long || v is ulong ? 64 : 32;

    private static bool Signed(object? v)
        => v is int || v is long;
}
