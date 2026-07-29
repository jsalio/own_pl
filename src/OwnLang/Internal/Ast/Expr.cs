using System.Collections.Generic;

namespace Own_Lang.Internal;

/// <summary>
/// Base type for every AST node that <b>produces a value</b>.
/// The guiding question for an expression is "what does it evaluate to?".
/// The interpreter's <c>Evaluate</c> method returns a value for each of these.
/// </summary>
public abstract record Expr;

#region Literals

/// <summary>An integer literal, e.g. <c>1</c>, <c>123</c>.</summary>
/// <param name="Value">The parsed integer value.</param>
public record NumberLiteral(object Value) : Expr;

/// <summary>A string literal, e.g. <c>"resultado:"</c>.</summary>
/// <param name="Value">The text content, without the surrounding quotes.</param>
public record StringLiteral(string Value) : Expr;

/// <summary>A boolean literal, e.g. <c>true</c>, <c>false</c>.</summary>
/// <param name="Value">The parsed boolean value.</param>
public record BooleanLiteral(bool Value) : Expr;

/// <summary>A character literal, e.g. <c>'a'</c>.</summary>
/// <param name="Value">The single character.</param>
public record CharLiteral(char Value) : Expr;

#endregion

#region References

/// <summary>
/// A reference to a named variable, e.g. <c>val1</c>. Resolved against the
/// interpreter's <c>Environment</c> at evaluation time.
/// </summary>
/// <param name="Name">The variable's identifier.</param>
public record Variable(string Name) : Expr;

#endregion

#region Operations

/// <summary>
/// A binary operation, e.g. <c>val1 + val2</c>. Both operands are themselves
/// expressions, which is what makes the tree recursive.
/// </summary>
/// <param name="Left">The left-hand operand.</param>
/// <param name="Operator">The operator token type (e.g. <c>PLUS</c>).</param>
/// <param name="Right">The right-hand operand.</param>
public record Binary(Expr Left, TokenType Operator, Expr Right) : Expr;

/// <summary>
/// An assignment, e.g. <c>x = x + 1</c>. Evaluates <paramref name="Value"/> and
/// stores it into the already-declared variable <paramref name="Name"/>,
/// returning the assigned value (assignment is an expression).
/// </summary>
/// <param name="Name">The target variable's identifier (must already exist).</param>
/// <param name="Value">The expression whose value is stored.</param>
public record Assign(string Name, Expr Value) : Expr;

#endregion

#region Access & calls

/// <summary>
/// Member access on an object, e.g. the <c>term.out</c> part of
/// <c>term.out(x)</c>.
/// </summary>
/// <param name="Object">The expression the member is accessed on (e.g. <c>term</c>).</param>
/// <param name="Member">The accessed member's name (e.g. <c>out</c>).</param>
public record MemberAccess(Expr Object, string Member) : Expr;

/// <summary>
/// A call expression, e.g. <c>out(result)</c>. The callee is an expression
/// (often a <see cref="MemberAccess"/>), and the arguments are expressions too.
/// </summary>
/// <param name="Callee">The expression being invoked.</param>
/// <param name="Arguments">The argument expressions, in order (may be empty).</param>
public record Call(Expr Callee, IReadOnlyList<Expr> Arguments) : Expr;

#endregion
