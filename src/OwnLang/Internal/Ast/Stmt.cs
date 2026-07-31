using System.Collections.Generic;

namespace Own_Lang.Internal.AST;

/// <summary>
/// Base type for every AST node that <b>performs an action</b> (has an effect
/// but no value). The guiding question for a statement is "what does it execute?".
/// The interpreter's <c>Execute</c> method runs each of these and returns nothing.
/// </summary>
/// <remarks>
/// The action half of the AST (its counterpart is <see cref="Expr"/>). The
/// interpreter runs these via a pattern-matching <c>Execute</c> that returns void,
/// mirroring the value-returning <c>Evaluate</c> for expressions. Statements hold
/// expressions and other statements as fields, so a whole program is one nested
/// tree of these records.
/// </remarks>
public abstract record Stmt;

#region Declarations

/// <summary>
/// A program declaration, e.g. <c>def program { ... }</c>. Holds the
/// top-level declarations (functions and statements) that contain <c>Main</c>.
/// </summary>
/// <remarks>
/// The one executable unit of a compilation. The interpreter registers its
/// functions and then runs the function named <c>Main</c>, so a program with no
/// <c>Main</c> is a runtime error.
/// </remarks>
/// <param name="Name">The program's name.</param>
/// <param name="Declarations">The top-level declarations inside the program.</param>
public record ProgramDecl(string Name, IReadOnlyList<Stmt> Declarations) : Stmt;

/// <summary>
/// A function declaration, e.g. <c>function empty Main() { ... }</c>.
/// </summary>
/// <remarks>
/// Shared by program-level functions and module functions. The
/// <paramref name="IsExternal"/> flag is what distinguishes a native binding from
/// a language-bodied function: when true the <paramref name="Body"/> is empty and
/// the interpreter dispatches to a registered native instead of executing it. The
/// declared types drive coercion of arguments and the return value at call time.
/// </remarks>
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
/// <remarks>
/// Deliberately not a <see cref="Stmt"/> and has no body — it only describes a
/// shape. Mirroring <see cref="FunctionDecl"/>'s header (return type, name, typed
/// parameters) lets the interpreter check a module's function against it field by
/// field when validating that the module honors its contract.
/// </remarks>
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
/// <remarks>
/// The language's interface. It carries no behavior on its own; its value is that
/// a module can name it and the interpreter then enforces the module implements
/// every member — which is also the groundwork for future dynamic dispatch by
/// contract.
/// </remarks>
/// <param name="Name">The contract's name (e.g. <c>ITerminal</c>).</param>
/// <param name="Members">The declared function signatures.</param>
public record ContractDecl(string Name, IReadOnlyList<FunctionSig> Members) : Stmt;

/// <summary>
/// A module (implementation) declaration, e.g.
/// <c>def module Term : ITerminal { external function empty out(string message); }</c>.
/// Groups functions under a name; each is either <c>external</c> (native) or
/// written in the language.
/// </summary>
/// <remarks>
/// The unit of the standard library: a namespace resolved statically by name, so
/// <c>Term.out(...)</c> finds this module then its function. The optional
/// <paramref name="Contract"/> is validated at registration; mixing
/// <c>external</c> and language-bodied functions is what lets primitives bottom
/// out in the host while the rest is written in the language.
/// </remarks>
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
/// <remarks>
/// A <i>declaration</i> that creates a new binding, unlike an <see cref="Assign"/>
/// which mutates an existing one. When <paramref name="DeclareType"/> is set the
/// interpreter type-checks and coerces the initializer at this point; a null type
/// means the type is inferred (<c>let</c>) and left as whatever the initializer
/// produced.
/// </remarks>
/// <param name="DeclareType">Declared type name, or null when inferred (<c>let</c>).</param>
/// <param name="Name">The variable's identifier.</param>
/// <param name="Initializer">The expression whose value initializes the variable.</param>
public record VarDecl(string? DeclareType, string Name, Expr Initializer) : Stmt;

/// <summary>
/// The root of the AST: a whole compilation unit. Holds the single program
/// (the one with <c>Main</c>) plus any top-level contracts and modules, which
/// are siblings of <c>def program</c> — not nested inside it.
/// </summary>
/// <remarks>
/// What <c>Parser.Parse</c> returns and <c>Interpreter.Interpret</c> consumes.
/// Note it is intentionally <b>not</b> a <see cref="Stmt"/> — it is never executed
/// as a statement, it is the container the interpreter registers and then runs.
/// Grouping contracts and modules alongside the program (rather than inside it) is
/// what makes them reusable, program-independent units.
/// </remarks>
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
/// <remarks>
/// The bridge that lets an expression stand where a statement is expected. Used
/// for expressions run purely for their side effect — a call like
/// <c>Term.out(x);</c> or a bare assignment <c>x = 5;</c> — whose resulting value
/// the interpreter discards.
/// </remarks>
/// <param name="Expression">The expression to evaluate.</param>
public record ExpressionStmt(Expr Expression) : Stmt;

/// <summary>
/// A brace-delimited block of statements, e.g. a function body <c>{ ... }</c>.
/// Executed by running each contained statement in order.
/// </summary>
/// <remarks>
/// The interpreter runs a block in a fresh child scope, so a block is also the
/// unit of lexical scoping: a <c>let</c> inside it is local and dies when the
/// block ends. Function bodies, loop bodies and <c>when</c> branches are all blocks.
/// </remarks>
/// <param name="Statements">The statements the block contains, in source order.</param>
public record Block(IReadOnlyList<Stmt> Statements) : Stmt;

#endregion

#region Control flow

/// <summary>
/// A conditional: <c>when(Condition) Then</c>, with an optional else branch.
/// </summary>
/// <remarks>
/// An <c>else when</c> chain is represented simply by nesting another
/// <see cref="WhenStmt"/> in <paramref name="Else"/>, so the interpreter needs no
/// special case for it — it just recurses. The condition must be a strict boolean.
/// </remarks>
/// <param name="Condition">The condition; must evaluate to a boolean.</param>
/// <param name="Then">The block run when the condition is true.</param>
/// <param name="Else">Optional else branch: a <see cref="Block"/>, another
/// <see cref="WhenStmt"/> (for <c>else when</c>), or null.</param>
public record WhenStmt(Expr Condition, Block Then, Stmt? Else) : Stmt;

/// <summary>An infinite loop: <c>loop { ... }</c>. Exits only via <c>stop</c>.</summary>
/// <remarks>The interpreter loops forever until a <see cref="StopStmt"/> throws the break signal that its driver catches.</remarks>
/// <param name="Body">The block run repeatedly.</param>
public record LoopStmt(Block Body) : Stmt;

/// <summary>A pre-test loop: <c>loop when(Condition) { ... }</c>. Runs the body
/// while the condition (a boolean) holds; may run zero times.</summary>
/// <remarks>Pre-test: the condition is checked before each iteration, so a false condition on entry runs the body zero times.</remarks>
/// <param name="Condition">The loop condition; must evaluate to a boolean.</param>
/// <param name="Body">The block run on each iteration.</param>
public record WhileStmt(Expr Condition, Block Body) : Stmt;

/// <summary>A counted loop: <c>loop[Variable: From...To] { ... }</c>. Iterates
/// inclusively from <c>From</c> to <c>To</c>, binding the counter to
/// <c>Variable</c> on each iteration.</summary>
/// <remarks>
/// The counter <paramref name="Variable"/> lives in a scope that wraps the loop, so
/// it is visible inside the body but does not leak past the loop. Bounds are
/// inclusive on both ends.
/// </remarks>
/// <param name="Variable">Name bound to the current counter value.</param>
/// <param name="From">Inclusive lower bound (integer).</param>
/// <param name="To">Inclusive upper bound (integer).</param>
/// <param name="Body">The block run on each iteration.</param>
public record RangeLoopStmt(string Variable, Expr From, Expr To, Block Body) : Stmt;

/// <summary>Breaks out of the innermost enclosing loop: <c>stop;</c>.</summary>
/// <remarks>
/// Carries no data — its mere presence is the instruction. The interpreter runs it
/// by throwing a break signal caught around each loop, so it always exits the
/// innermost one; the parser guarantees a <c>stop</c> only appears inside a loop.
/// </remarks>
public record StopStmt : Stmt;


#endregion

#region Type-related AST nodes

/// <summary>A typed function parameter, e.g. <c>int a</c>.</summary>
/// <remarks>
/// Not a <see cref="Stmt"/> — a small data record used inside <see cref="FunctionDecl"/>
/// and <see cref="FunctionSig"/>. The <paramref name="Type"/> is what the interpreter
/// coerces each argument to when binding it at a call.
/// </remarks>
/// <param name="Type">The declared type name (e.g. <c>int</c>).</param>
/// <param name="Name">The parameter's identifier.</param>
public record Param(string Type, string Name);

/// <summary>A return: <c>return expr;</c> or bare <c>return;</c>.</summary>
/// <remarks>
/// The interpreter runs this by throwing a return signal carrying the value, which
/// the call machinery catches — so a <c>return</c> nested inside loops or <c>when</c>
/// blocks still unwinds cleanly out of the function. The field is nullable to allow a
/// valueless <c>return;</c> from an <c>empty</c> function.
/// </remarks>
/// <param name="value">The returned expression, or null for a valueless return.</param>
public record ReturnStmt(Expr? value) : Stmt;

#endregion
