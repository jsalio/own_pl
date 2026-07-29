using System.Collections.Generic;

namespace Own_Lang.Internal;

/// <summary>
/// Base type for every AST node that <b>performs an action</b> (has an effect
/// but no value). The guiding question for a statement is "what does it execute?".
/// The interpreter's <c>Execute</c> method runs each of these and returns nothing.
/// </summary>
public abstract record Stmt;

#region Declarations

/// <summary>
/// The root of the AST: a program, e.g. <c>def program { ... }</c>. Holds the
/// top-level declarations (currently functions).
/// </summary>
/// <param name="Name">The program's name.</param>
/// <param name="Declarations">The top-level declarations inside the program.</param>
public record ProgramDecl(string Name, IReadOnlyList<Stmt> Declarations) : Stmt;

/// <summary>
/// A function declaration, e.g. <c>function empty Main() { ... }</c>.
/// </summary>
/// <param name="ReturnType">The declared return type (e.g. <c>empty</c>).</param>
/// <param name="Name">The function's name (e.g. <c>Main</c>).</param>
/// <param name="Parameters">the typed parameters, in order</param>
/// <param name="Body">The function body.</param>
public record FunctionDecl(
    string ReturnType,
    string Name,
    IReadOnlyList<Param> Parameters,
    Block Body) : Stmt;

/// <summary>
/// A variable declaration, e.g. <c>let val1 = 1;</c>. Evaluating the initializer
/// and binding the result to the name is what creates the variable.
/// </summary>
/// <param name="DeclareType">Declared type name, or null when inferred (<c>let</c>).</param>
/// <param name="Name">The variable's identifier.</param>
/// <param name="Initializer">The expression whose value initializes the variable.</param>
public record VarDecl(string? DeclareType, string Name, Expr Initializer) : Stmt;

#endregion

#region Simple statements

/// <summary>
/// The bridge between the two node families: an expression used as a statement,
/// e.g. <c>term.out(...);</c>. The expression is evaluated for its effect and
/// its resulting value is discarded.
/// </summary>
/// <param name="Expression">The expression to evaluate.</param>
public record ExpressionStmt(Expr Expression) : Stmt;

/// <summary>
/// A brace-delimited block of statements, e.g. a function body <c>{ ... }</c>.
/// Executed by running each contained statement in order.
/// </summary>
/// <param name="Statements">The statements the block contains, in source order.</param>
public record Block(IReadOnlyList<Stmt> Statements) : Stmt;

#endregion

#region Control flow

/// <summary>
/// A conditional: <c>when(Condition) Then</c>, with an optional else branch.
/// </summary>
/// <param name="Condition">The condition; must evaluate to a boolean.</param>
/// <param name="Then">The block run when the condition is true.</param>
/// <param name="Else">Optional else branch: a <see cref="Block"/>, another
/// <see cref="WhenStmt"/> (for <c>else when</c>), or null.</param>
public record WhenStmt(Expr Condition, Block Then, Stmt? Else) : Stmt;

/// <summary>An infinite loop: <c>loop { ... }</c>. Exits only via <c>stop</c>.</summary>
/// <param name="Body">The block run repeatedly.</param>
public record LoopStmt(Block Body) : Stmt;

/// <summary>A pre-test loop: <c>loop when(Condition) { ... }</c>. Runs the body
/// while the condition (a boolean) holds; may run zero times.</summary>
/// <param name="Condition">The loop condition; must evaluate to a boolean.</param>
/// <param name="Body">The block run on each iteration.</param>
public record WhileStmt(Expr Condition, Block Body) : Stmt;

/// <summary>A counted loop: <c>loop[Variable: From...To] { ... }</c>. Iterates
/// inclusively from <c>From</c> to <c>To</c>, binding the counter to
/// <c>Variable</c> on each iteration.</summary>
/// <param name="Variable">Name bound to the current counter value.</param>
/// <param name="From">Inclusive lower bound (integer).</param>
/// <param name="To">Inclusive upper bound (integer).</param>
/// <param name="Body">The block run on each iteration.</param>
public record RangeLoopStmt(string Variable, Expr From, Expr To, Block Body) : Stmt;

/// <summary>Breaks out of the innermost enclosing loop: <c>stop;</c>.</summary>
public record StopStmt : Stmt;


#endregion

#region Type-related AST nodes

/// <summary>A typed function parameter, e.g. <c>int a</c>.</summary>
/// <param name="Type">The declared type name (e.g. <c>int</c>).</param>
/// <param name="Name">The parameter's identifier.</param>
public record Param(string Type, string Name);

/// <summary>A return: <c>return expr;</c> or bare <c>return;</c>.</summary>
/// <param name="value">The returned expression, or null for a valueless return.</param>
public record ReturnStmt(Expr? value) : Stmt;

#endregion
