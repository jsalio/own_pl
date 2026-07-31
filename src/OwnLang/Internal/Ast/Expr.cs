using System.Collections.Generic;

namespace Own_Lang.Internal.AST;

/// <summary>
/// Base type for every AST node that <b>produces a value</b>.
/// The guiding question for an expression is "what does it evaluate to?".
/// The interpreter's <c>Evaluate</c> method returns a value for each of these.
/// </summary>
/// <remarks>
/// One of the two AST families (the other is <see cref="Stmt"/>). The split is the
/// backbone of the whole downstream design: the parser produces these records and
/// the interpreter dispatches on them with a pattern-matching <c>switch</c>, so
/// "expression vs statement" decides whether a node yields a value or performs an
/// action. Expressions nest inside expressions (an operand is itself an
/// <see cref="Expr"/>), which is exactly what makes the parse result a <i> tree </i>.
/// </remarks>
public abstract record Expr;

#region Literals

/// <summary>A numeric literal, e.g. <c>1</c>, <c>123</c>, <c>3.14</c>, <c>5f</c>.</summary>
/// <remarks>
/// <see cref="Value"/> is typed <c>object</c> on purpose: the parser has already
/// classified the concrete numeric type (int, long, double or float) and boxes it
/// here, so the literal carries its exact runtime type. The interpreter returns the
/// value as-is with no further work.
/// </remarks>
/// <param name="Value">The parsed numeric value, boxed as its concrete type.</param>
public record NumberLiteral(object Value) : Expr;

/// <summary>A string literal, e.g. <c>"resultado:"</c>.</summary>
/// <remarks>Holds the actual text — the surrounding quotes were stripped by the parser, not kept as in the raw lexeme.</remarks>
/// <param name="Value">The text content, without the surrounding quotes.</param>
public record StringLiteral(string Value) : Expr;

/// <summary>A boolean literal, e.g. <c>true</c>, <c>false</c>.</summary>
/// <remarks>The only source of real boolean values, which the strict <c>when</c>/logical checks require (there is no truthiness coercion).</remarks>
/// <param name="Value">The parsed boolean value.</param>
public record BooleanLiteral(bool Value) : Expr;

/// <summary>A character literal, e.g. <c>'a'</c>.</summary>
/// <remarks>Distinct from a one-character string: it evaluates to a C# <c>char</c>, a separate runtime type.</remarks>
/// <param name="Value">The single character.</param>
public record CharLiteral(char Value) : Expr;

#endregion

#region References

/// <summary>
/// A reference to a named variable, e.g. <c>val1</c>. Resolved against the
/// interpreter's <c>Environment</c> at evaluation time.
/// </summary>
/// <remarks>
/// The parser emits this for any bare name — it does not track scope or know
/// whether the name is a variable, a function, or a module. That resolution
/// happens at runtime (variable lookup walks the environment chain; a call site
/// may instead match the name against the function/module tables).
/// </remarks>
/// <param name="Name">The variable's identifier.</param>
public record Variable(string Name) : Expr;

#endregion

#region Operations

/// <summary>
/// A binary operation, e.g. <c>val1 + val2</c>. Both operands are themselves
/// expressions, which is what makes the tree recursive.
/// </summary>
/// <remarks>
/// Covers arithmetic, comparison and equality. The interpreter evaluates
/// <b>both</b> operands eagerly before applying the operator — which is precisely
/// why the short-circuiting <c>&amp;&amp;</c>/<c>||</c> are <b>not</b> modeled here
/// but as a separate <see cref="Logical"/> node. The operator is kept as a raw
/// token type rather than resolved, so one record serves every binary operator.
/// </remarks>
/// <param name="Left">The left-hand operand.</param>
/// <param name="Operator">The operator token type (e.g. <c>PLUS</c>).</param>
/// <param name="Right">The right-hand operand.</param>
public record Binary(Expr Left, TokenType Operator, Expr Right) : Expr;

/// <summary>
/// An assignment, e.g. <c>x = x + 1</c>. Evaluates <paramref name="Value"/> and
/// stores it into the already-declared variable <paramref name="Name"/>,
/// returning the assigned value (assignment is an expression).
/// </summary>
/// <remarks>
/// Being an <see cref="Expr"/> (not a statement) is deliberate: it yields the
/// assigned value, so <c>x = y = 5</c> works and a bare <c>x = 5;</c> is just an
/// expression statement. It <b>mutates</b> an existing binding — contrast with a
/// <see cref="VarDecl"/>, which declares a new one.
/// </remarks>
/// <param name="Name">The target variable's identifier (must already exist).</param>
/// <param name="Value">The expression whose value is stored.</param>
public record Assign(string Name, Expr Value) : Expr;

/// <summary>
/// A short-circuiting logical operation, e.g. <c>a && b</c> or <c>a || b</c>.
/// Kept separate from <see cref="Binary"/> because the right operand is only
/// evaluated when the operator cannot decide from the left one.
/// </summary>
/// <remarks>
/// Its whole reason to exist as a distinct node is short-circuit evaluation: the
/// interpreter can skip evaluating <paramref name="Right"/> entirely, so a guard
/// like <c>ptr != null &amp;&amp; ptr.ok</c> — or avoiding a divide-by-zero — is
/// possible. Operands are strict booleans; there is no truthiness coercion.
/// </remarks>
/// <param name="Left">The left-hand operand (a boolean).</param>
/// <param name="Operator">The operator token type (<c>AND</c> or <c>OR</c>).</param>
/// <param name="Right">The right-hand operand (a boolean), evaluated lazily.</param>
public record Logical(Expr Left, TokenType Operator, Expr Right) : Expr;

/// <summary>
/// A unary prefix operation, e.g. <c>!flag</c>.
/// </summary>
/// <remarks>
/// Currently only logical NOT (<c>!</c>). Note the operand field is named
/// <c>Right</c> (not <c>Operand</c>) — a small gotcha when pattern-matching. The
/// operand must be a strict boolean, like the <see cref="Logical"/> operands.
/// </remarks>
/// <param name="Operator">The operator token type (<c>BANG</c>).</param>
/// <param name="Right">The operand the operator applies to.</param>
public record Unary(TokenType Operator, Expr Right) : Expr;

#endregion

#region Access & calls

/// <summary>
/// Member access on an object, e.g. the <c>Term.out</c> part of
/// <c>Term.out(x)</c>.
/// </summary>
/// <remarks>
/// There is no real object system yet, so this exists mainly to form the
/// <c>Module.member</c> shape: wrapped in a <see cref="Call"/>, it is the exact
/// pattern the interpreter matches to dispatch a module function.
/// </remarks>
/// <param name="Object">The expression the member is accessed on (e.g. <c>Term</c>).</param>
/// <param name="Member">The accessed member's name (e.g. <c>out</c>).</param>
public record MemberAccess(Expr Object, string Member) : Expr;

/// <summary>
/// A call expression, e.g. <c>out(result)</c>. The callee is an expression
/// (often a <see cref="MemberAccess"/>), and the arguments are expressions too.
/// </summary>
/// <remarks>
/// The <see cref="Callee"/>'s shape is what the interpreter resolves on: a
/// <see cref="MemberAccess"/> on a module name is a module call, a bare
/// <see cref="Variable"/> naming a function is a plain call. Arguments are
/// arbitrary expressions, so calls compose (a call can be an argument to another).
/// </remarks>
/// <param name="Callee">The expression being invoked.</param>
/// <param name="Arguments">The argument expressions, in order (may be empty).</param>
public record Call(Expr Callee, IReadOnlyList<Expr> Arguments) : Expr;



#endregion
