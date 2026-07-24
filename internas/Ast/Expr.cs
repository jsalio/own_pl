using System.Collections.Generic;

namespace Own_Lang.Internal;

// Familia de nodos que PRODUCEN un valor.
// Pregunta clave: "¿cuánto vale esto?"
public abstract record Expr;

// Un número literal: 1, 2, 123
public record NumberLiteral(int Value) : Expr;

// Un texto literal: "resultado:"  (sin las comillas)
public record StringLiteral(string Value) : Expr;

// La referencia a un nombre: val1, result, term
public record Variable(string Name) : Expr;

// Una operación binaria: val1 + val2
public record Binary(Expr Left, TokenType Operator, Expr Right) : Expr;

// Acceso a un miembro: term.out  (Object = term, Member = "out")
public record MemberAccess(Expr Object, string Member) : Expr;

// Una llamada: out(result)  (Callee = lo que se llama, Arguments = los argumentos)
public record Call(Expr Callee, IReadOnlyList<Expr> Arguments) : Expr;
