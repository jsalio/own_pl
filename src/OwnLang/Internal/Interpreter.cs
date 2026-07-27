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
        _ => value
    };

    private static bool IsTruthy(object? value)
    {
        if (value is bool b) return b;
        throw new System.Exception("La condición de 'when' debe ser booleana");
    }

    private static int ToInt(object? value) => value switch
    {
        int i => i,
        uint u => u <= int.MaxValue ? (int)u : throw new OverflowError("uint value not cast to int"),
        _ => throw new MathError("Value " + value + " cannot be cast to int")
    };

    private static uint ToUInt(object? value) => value switch
    {
        uint u => u,
        int i => i >= 0 ? (uint)i : throw new OverflowError("int value not cast to uint"),
        _ => throw new MathError("Value " + value + " cannot be cast to uint")
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

    private object? EvaluateBinary(Binary b)
    {
        object? left = Evaluate(b.Left);
        object? right = Evaluate(b.Right);


        if (b.Operator == TokenType.PLUS && (left is string || right is string))
            return Stringify(left) + Stringify(right);

        if (b.Operator == TokenType.EQUAL_EQUAL) return left!.Equals(right);
        if (b.Operator == TokenType.BANG_EQUAL) return !left!.Equals(right);

        if (left is uint lu && right is uint ru)
            return NumericUInt(b.Operator, lu, ru);

        return NumericInt(b.Operator, ToInt(left), ToInt(right));
    }


}
