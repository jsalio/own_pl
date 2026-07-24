using System.Collections.Generic;

namespace Own_Lang.Internal;

// Familia de nodos que SE EJECUTAN (acciones).
// Pregunta clave: "¿qué hace / qué ejecuta esto?"
public abstract record Stmt;

// Declaración de variable: let val1 = 1;
public record VarDecl(string Name, Expr Initializer) : Stmt;

// La bisagra: una expresión usada como sentencia -> term.out(...);
public record ExpressionStmt(Expr Expression) : Stmt;

// Un bloque de sentencias: { ... }
public record Block(IReadOnlyList<Stmt> Statements) : Stmt;

// Declaración de función: function empty Main() { ... }
public record FunctionDecl(
    string ReturnType,
    string Name,
    IReadOnlyList<string> Parameters,
    Block Body) : Stmt;

// El programa: def program { ... }
public record ProgramDecl(string Name, IReadOnlyList<Stmt> Declarations) : Stmt;
