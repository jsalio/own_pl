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
                if(d.DeclareType is not null)
                    CheckType(d.DeclareType, d.Name, value);
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
            _ => true
        };
        if (!ok)
            throw new System.Exception(
                    $"Error in type : '{name}' is declare as {declareType} but ,"+
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
            _ => throw new System.Exception(
                     $"Expresión no soportada: {expr.GetType().Name}")
        };
    }

    private static bool IsTruthy(object? value)
    {
        if (value is bool b) return b;
        throw new System.Exception("La condición de 'when' debe ser booleana");
    }

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
        => value?.ToString()??"";

    private object? EvaluateBinary(Binary b)
    {
        object? left = Evaluate(b.Left);
        object? right = Evaluate(b.Right);

        int TryDivide(object? a, object? b)
        {
            if (b is int divisor && divisor == 0)
            {
                throw new MathError("Division by zero detected");
            }
            return (int)a! / (int)b!;
        }

        object? Add(object? left, object? right)
        {
            if (left is string || right is string)
                return Stringify(left) + Stringify(right);
            else 
               return (int)left! + (int)right!;
                    
        }

        bool Less(object? a, object? b) => (int)a! < (int)b!;
        bool LessOrEqual(object? a, object? b) => (int)a! <= (int)b!;
        bool Greater(object? a, object? b) => (int)a! > (int)b!;
        bool GreaterOrEqual(object? a, object? b) => (int)a! >= (int)b!;
        bool Equals(object? a, object? b) => a!.Equals(b);

        return b.Operator switch
        {
            TokenType.PLUS => Add(left, right), //(int)left! + (int)right!,
            TokenType.MINUS => (int)left! - (int)right!,
            TokenType.STAR => (int)left! * (int)right!,
            TokenType.SLASH => TryDivide(left, right),
            TokenType.EQUAL_EQUAL => Equals(left, right),
            TokenType.BANG_EQUAL => !Equals(left, right),
            TokenType.LESS => Less(left, right),
            TokenType.LESS_EQUAL => LessOrEqual(left, right),
            TokenType.GREATER => Greater(left, right),
            TokenType.GREATER_EQUAL => GreaterOrEqual(left, right),
            _ => throw new System.Exception(
                     $"Operador no soportado: {b.Operator}")
        };
    }


}
