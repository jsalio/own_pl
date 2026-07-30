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
/// A program declaration, e.g. <c>def program { ... }</c>. Holds the
/// top-level declarations (functions and statements) that contain <c>Main</c>.
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
/// <param name="Body">The function body (empty when <paramref name="IsExternal"/> is true).</param>
/// <param name="IsExternal">
/// True when the body is provided by a host/native implementation (an
/// <c>external function</c> inside a module) rather than written in the language.
/// </param>
public record FunctionDecl(
    string ReturnType,
    string Name,
    IReadOnlyList<Param> Parameters,
    Block Body,
    bool IsExternal = false) : Stmt;

/// <summary>
/// A function signature without a body: a member of a <c>contract</c>, e.g.
/// <c>function empty out(string message);</c>. Same header as a
/// <see cref="FunctionDecl"/> but declares only the shape to implement.
/// </summary>
/// <param name="ReturnType">The declared return type.</param>
/// <param name="Name">The function's name.</param>
/// <param name="Parameters">The typed parameters, in order.</param>
public record FunctionSig(
    string ReturnType,
    string Name,
    IReadOnlyList<Param> Parameters);

/// <summary>
/// A contract (interface) declaration, e.g.
/// <c>def contract ITerminal { function empty out(string message); }</c>.
/// Declares a set of function signatures a module can claim to implement.
/// </summary>
/// <param name="Name">The contract's name (e.g. <c>ITerminal</c>).</param>
/// <param name="Members">The declared function signatures.</param>
public record ContractDecl(string Name, IReadOnlyList<FunctionSig> Members) : Stmt;

/// <summary>
/// A module (implementation) declaration, e.g.
/// <c>def module Term : ITerminal { external function empty out(string message); }</c>.
/// Groups functions under a name; each is either <c>external</c> (native) or
/// written in the language.
/// </summary>
/// <param name="Name">The module's name (e.g. <c>Term</c>).</param>
/// <param name="Contract">The implemented contract's name, or null if none.</param>
/// <param name="Functions">The module's functions (external or language-bodied).</param>
public record ModuleDecl(
    string Name,
    string? Contract,
    IReadOnlyList<FunctionDecl> Functions) : Stmt;

/// <summary>
/// A variable declaration, e.g. <c>let val1 = 1;</c>. Evaluating the initializer
/// and binding the result to the name is what creates the variable.
/// </summary>
/// <param name="DeclareType">Declared type name, or null when inferred (<c>let</c>).</param>
/// <param name="Name">The variable's identifier.</param>
/// <param name="Initializer">The expression whose value initializes the variable.</param>
public record VarDecl(string? DeclareType, string Name, Expr Initializer) : Stmt;

/// <summary>
/// The root of the AST: a whole compilation unit. Holds the single program
/// (the one with <c>Main</c>) plus any top-level contracts and modules, which
/// are siblings of <c>def program</c> — not nested inside it.
/// </summary>
/// <param name="Program">The program declaration (holds <c>Main</c>).</param>
/// <param name="Contracts">Top-level contract declarations.</param>
/// <param name="Modules">Top-level module declarations.</param>
public record CompilationUnit(ProgramDecl Program, IReadOnlyList<ContractDecl> Contracts, IReadOnlyList<ModuleDecl> Modules);


#endregion

#region Simple statements

/// <summary>
/// The bridge between the two node families: an expression used as a statement,
/// e.g. <c>Term.out(...);</c>. The expression is evaluated for its effect and
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
