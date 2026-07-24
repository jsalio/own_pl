using own_pl.internas.Error;

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

            default:
                throw new System.Exception(
                    $"Sentencia no soportada: {stmt.GetType().Name}");
        }
    }

    // ---- Evaluación de expresiones (producen un valor) ----

    private object? Evaluate(Expr expr)
    {
        return expr switch
        {
            NumberLiteral n => n.Value,
            StringLiteral s => s.Value,
            Variable v      => environment.Get(v.Name),
            Binary b        => EvaluateBinary(b),
            Call c          => EvaluateCall(c),
            _ => throw new System.Exception(
                     $"Expresión no soportada: {expr.GetType().Name}")
        };
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

    private object? EvaluateBinary(Binary b)
    {
        object? left = Evaluate(b.Left);
        object? right = Evaluate(b.Right);
        
        int TryDivide(object? a, object? b)
        {
            if (b is int divisor && divisor == 0)
            {
                throw new MathError("Divission by zero detected");
            }
            return (int)a! / (int)b!;
        }

        return b.Operator switch
        {
            TokenType.PLUS => (int)left! + (int)right!,
            TokenType.MINUS => (int)left! - (int)right!,
            TokenType.STAR => (int)left! * (int)right!,
            TokenType.SLASH => TryDivide(left, right),
            _ => throw new System.Exception(
                     $"Operador no soportado: {b.Operator}")
        };
    }
}
