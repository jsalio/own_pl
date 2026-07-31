# Own_Lang

A custom, interpreted programming language written in C#.

Own_Lang is a **tree-walking interpreter**: it reads source code, turns it into a
syntax tree, and executes it by traversing that tree. The project is built in
layers, and every stage of the pipeline is independent and verifiable on its own.

## Example

```
def program {
    function empty Main()
    {
        let val1 = 1;
        let val2 = 2;
        let result = val1 + val2;
        Term.out("resultado:");
        Term.out(result);
    }
}
```

Output:

```
resultado:
3
```

## Requirements

- .NET SDK 10.0 or later

## Running it

The repository is a solution (`OwnLang.sln`) with the interpreter under
`src/OwnLang`. From the repo root:

```bash
dotnet build                                        # build the whole solution
dotnet run --project src/OwnLang -- examples/hello.own        # run a .own file
dotnet run --project src/OwnLang -- examples/hello.own --ast  # also dump the AST
```

`dotnet run` needs `--project` because the solution sits at the root with no
project file there; everything after `--` is passed to the program. The
interpreter reads the `.own` source file named as the first argument and runs it
through the full pipeline (source → tokens → AST → execution), printing the
program's output. It exits non-zero with a one-line `error: ...` message on a
missing/invalid file or a lex/parse/runtime error; the optional `--ast` flag
dumps the parsed top-level declarations before running. The file-reading logic
lives in `src/OwnLang/Runner.cs`; `Program.cs` is just a thin shell that forwards
the arguments.

## Testing

The project has an NUnit regression suite in `tests/OwnLang.Tests`, with one
fixture per pipeline stage:

| Fixture | Covers |
|---|---|
| `LexerTests` | text → tokens (`DetectString`, `DetectVals`, `DetectCodeBlock`, keywords, operators, errors) |
| `ParserTests` | tokens → AST (precedence, associativity, call shape, syntax errors) |
| `InterpreterTests` | end-to-end execution (arithmetic, precedence, variables, `Term.out` output) |
| `EnvironmentTests` | the runtime variable store |

```bash
dotnet test                                          # all tests (whole solution)
dotnet test --filter FullyQualifiedName~LexerTests   # one fixture
dotnet test --filter Name=DetectString               # one test
```

`InterpreterTests` verify behavior end-to-end and capture `Term.out` output by
redirecting `Console.Out`. The suite accesses the interpreter's `internal` types
through `InternalsVisibleTo` declared in `src/OwnLang/OwnLang.csproj`.

## The pipeline

The interpreter processes code in three chained stages:

```
Source code (string)
        │
        ▼
  [1. Lexer]        text   → List<Token>     Scans the text and produces tokens.
        │
        ▼
  [2. Parser]       tokens → AST             Recursive descent; builds the tree.
        │
        ▼
  [3. Interpreter]  AST    → execution       Walks the tree and executes each node.
```

### 1. Lexer (lexical analysis)

Converts the raw text into a list of `Token`. It recognizes:

- **Symbols:** `{ } ( ) [ ] ; , : . ... = + - * / == != < <= > >=`
- **Numbers:** integers (`1`, `123`), decimals (`3.14`), floats (`1.5f`)
- **Strings:** `"text"`
- **Chars:** `'a'` (exactly one character, single quotes)
- **Identifiers and keywords** (told apart using a keyword table)
- **Line comments** (`//`) and whitespace (ignored)

Each token stores its type, its lexeme (original text), and its line and column.

### 2. Parser (syntactic analysis)

Takes the tokens and builds the **AST** using the *recursive descent* technique
(one function per grammar rule). Operator precedence emerges from the call
hierarchy between those functions.

### 3. Interpreter (evaluation)

Walks the AST with *pattern matching* over the nodes (`record`), separating:

- `Evaluate(Expr)` → produces a value (`object?`)
- `Execute(Stmt)` → performs an action (`void`)

Variables live in an `Environment` (a `name → value` map). Environments are
**chained**: each one has an optional enclosing parent, every block opens a child
scope, and name lookup/assignment walks up the chain. The `Term.out(...)` output
is wired to `Console.WriteLine` in this version.

## Project structure

```
own_pl/
├── OwnLang.sln                        The solution
├── src/
│   └── OwnLang/                       Interpreter project (assembly OwnLang)
│       ├── OwnLang.csproj
│       ├── Program.cs                 Entry point (thin shell over Runner)
│       ├── Runner.cs                  Reads a .own file and runs the pipeline
│       └── Internal/
│           ├── Token.cs               A token (type, lexeme, line, column)
│           ├── TokenTypes.cs          enum TokenType (all categories)
│           ├── Lexer.cs               Lexer : ILexer
│           ├── Parser.cs              Parser : IParser
│           ├── Environment.cs         Runtime variables (scope chain)
│           ├── Interpreter.cs         Interpreter : IInterpreter
│           ├── Ast/
│           │   ├── Expr.cs            Expressions (produce a value)
│           │   └── Stmt.cs            Statements (perform actions)
│           ├── Interrupt/             Control-flow signals
│           │   ├── BreakSignal.cs     'stop' unwinding
│           │   └── ReturnSignal.cs    'return' unwinding (carries a value)
│           ├── Error/                 Typed runtime errors (Math/Overflow/Type)
│           └── Contracts/
│               ├── ILexer.cs          interface ILexer
│               ├── IParser.cs         interface IParser
│               └── IInterpreter.cs    interface IInterpreter
├── examples/
│   └── hello.own                     Sample program to run
└── tests/
    └── OwnLang.Tests/                 NUnit regression suite
```

Namespaces: the core lives in `Own_Lang.Internal`; the contract interfaces and
the interpreter implementation in `Own_Lang.Internal.Contracts`. (Namespaces are
independent of the `src/` folder layout.)

## The AST

Two families of nodes:

**Expressions** (`Expr`) — "what does it evaluate to?":

| Node | Represents |
|---|---|
| `NumberLiteral` | a number: `1` |
| `StringLiteral` | a text: `"hello"` |
| `BooleanLiteral` | a boolean: `true`, `false` |
| `Variable` | a reference to a name: `val1` |
| `Binary` | a binary operation: `val1 + val2`, `a == b`, `x < 3` |
| `Logical` | a short-circuiting logical op: `a && b`, `a \|\| b` |
| `Unary` | a unary prefix op: `!flag` |
| `Assign` | an assignment to an existing variable: `x = x + 1` |
| `MemberAccess` | member access: `Term.out` |
| `Call` | a call: `out(result)` |

**Statements** (`Stmt`) — "what does it execute?":

| Node | Represents |
|---|---|
| `VarDecl` | `let val1 = 1;` |
| `ExpressionStmt` | an expression used as a statement: `Term.out(...);` |
| `Block` | a block `{ ... }` |
| `WhenStmt` | a conditional: `when(c) { } else { }` |
| `LoopStmt` | an infinite loop: `loop { }` |
| `WhileStmt` | a pre-test loop: `loop when(c) { }` |
| `RangeLoopStmt` | a counted loop: `loop[i: 1...3] { }` |
| `StopStmt` | break out of the innermost loop: `stop;` |
| `ReturnStmt` | return from a function: `return a + b;` |
| `FunctionDecl` | `function int suma(int a, int b) { ... }` (params are typed `Param`s; `IsExternal` marks a native binding) |
| `ProgramDecl` | `def program { ... }` (the program that holds `Main`) |
| `ContractDecl` | `def contract ITerminal { function empty out(string m); }` (interface: a list of `FunctionSig`) |
| `ModuleDecl` | `def module Term : ITerminal { ... }` (implementation; functions are `external` or language-bodied) |
| `CompilationUnit` | the AST root: the program plus its sibling contracts and modules |

## Grammar (current version)

```
compilationUnit → topLevel* EOF
topLevel        → "def" ( program | contract | module )
program         → IDENT block
contract        → "contract" IDENT "{" functionSig* "}"
module          → "module" IDENT ( ":" IDENT )? "{" moduleFunction* "}"
functionSig     → "function" returnType IDENT "(" params? ")" ";"
moduleFunction  → "external"? "function" returnType IDENT "(" params? ")" ( block | ";" )
declaration    → function | statement
function       → "function" returnType IDENT "(" params? ")" block
returnType     → "empty" | typeName | IDENT
params         → typeName IDENT ( "," typeName IDENT )*
block          → "{" statement* "}"

statement      → varDecl | whenStmt | loopStmt | stopStmt | returnStmt | exprStmt
returnStmt     → "return" expression? ";"
varDecl        → ( "let" | typeName ) IDENT "=" expression ";"
typeName       → "string" | "bool" | "char"
               | "int" | "uint" | "long" | "ulong" | "double" | "float"
whenStmt       → "when" "(" expression ")" block ( "else" ( whenStmt | block ) )?
loopStmt       → "loop" ( "[" IDENT ":" expression "..." expression "]"
                        | "when" "(" expression ")" )? block
stopStmt       → "stop" ";"
exprStmt       → expression ";"

expression     → assignment
assignment     → IDENT "=" assignment | logicOr
logicOr        → logicAnd ( "||" logicAnd )*
logicAnd       → equality ( "&&" equality )*
equality       → comparison ( ( "==" | "!=" ) comparison )*
comparison     → additive ( ( "<" | "<=" | ">" | ">=" ) additive )*
additive       → multiplicative ( ( "+" | "-" ) multiplicative )*
multiplicative → unary ( ( "*" | "/" ) unary )*
unary          → "!" unary | call
call           → primary ( "." IDENT | "(" arguments? ")" )*
arguments      → expression ( "," expression )*
primary        → NUMBER | STRING | "true" | "false" | IDENT | "(" expression ")"
```

## Execution conventions

- The interpreter looks for the `Main` function inside `def program` and executes its body.
- `Term.out(x)` prints `x` to the console. `Term` is a built-in **module** with an
  `external` function `out` bound to a native (C#) implementation — no longer a
  hardcoded call-site special case.

## Current limitations

- Arithmetic (`+ - * /`), comparison (`< <= > >=`) and equality (`== !=`)
  operators. `+` concatenates when either operand is a string, coercing the other
  via `ToString()` (`"n" + 35` → `"n35"`); otherwise it is integer addition.
- Logical operators `&&`, `||` (short-circuiting) and unary `!`. Operands must be
  booleans (no truthiness coercion — `5 && true` is an error). `&&` binds tighter
  than `||`, and `!` binds tighter than the comparison/equality operators.
- Integers come in 32-bit (`int`/`uint`) and 64-bit (`long`/`ulong`), signed and
  unsigned. **Integer** arithmetic is **checked**: any overflow — at declaration
  (`uint x = 0 - 1;`) or in an operation (`2000000000 + 2000000000`) — throws
  `OverflowError`; `/` truncates (`7 / 2` is `3`).
- Decimals are `double` (64-bit) and `float` (32-bit), with **IEEE** semantics:
  `1.0 / 0.0` is `Infinity` (no exception), `NaN` is a valid value. `float`
  arithmetic (`7.0 / 2`) does not truncate.
- Mixed-type operations promote: if any operand is a decimal the op is `double`
  (then `float`); otherwise integers promote to the **widest** type with **signed
  winning** ties (`int + long` → `long`, `int + uint` → `int`, `2.0 + 3` → `double`).
  Assigning a decimal to an integer type (`int x = 3.14;`) is an error.
- A bare integer literal auto-widens: it is `int` if it fits, otherwise `long`.
  A decimal literal (`3.14`) is `double`; the `f` suffix (`1.5f`) makes it `float`.
  `uint`/`ulong` values are produced by coercion at a typed declaration.
- Declarations are inferred (`let x = ...`) or typed (`string x = ...`,
  `bool b = ...`, `long n = 5;`); the type is checked dynamically at runtime
  (`string x = 5;` fails). Usable type keywords so far: `string`, `bool`, `char`,
  `int`, `uint`, `long`, `ulong`.
- Conditionals (`when` / `else` / `else when`) and loops (`loop`, `loop when`,
  `loop[i: 1...3]`, with `stop`) exist. Conditions must be booleans (no
  truthiness coercion). A `stop` outside any loop is rejected at parse time
  (the parser tracks loop nesting), so it can never reach the interpreter.
- Scopes are lexical: every block (`when`/loop body, function body) opens a child
  scope, so a `let` inside a block is local to it and dies when the block ends.
  Assignment `x = expr` mutates an existing variable (searching enclosing scopes);
  assigning to an undeclared name is an error. `let`/typed declarations always
  create a new binding in the current scope.
- User-defined functions with typed parameters work: `function int suma(int a,
  int b) { return a + b; }`, called as `suma(2, 3)`. Arguments are coerced to each
  parameter's type, the returned value is coerced to the declared return type
  (`empty` = void), arity is checked, and each call runs in a scope that is a child
  of the **global** scope (lexical — a function cannot see its caller's locals).
- Contracts and modules exist as **static namespaces**: `def contract IName { ... }`
  declares signatures; `def module Name : IName { ... }` implements them (validated at
  registration). A module's function is either `external` (bound to a native C#
  implementation, e.g. `Term.out`) or written in the language. `Module.func(...)` is
  resolved statically by name — modules are not yet first-class values (no
  `let t: IName = Term;`, no dynamic dispatch). `external` parameter types are the
  contract shape; they are not coerced at the call (the native marshals its own args).

## Roadmap

- [x] `-` operator (subtraction), sharing the `additive` level with `+`.
- [x] `* /` operators with precedence (a `multiplicative` level below `additive`).
- [x] Booleans (`true`/`false`) and comparison/equality operators
  (`< <= > >= == !=`), with `equality` and `comparison` precedence levels.
- [x] Conditionals: `when` / `else` / `else when` (`WhenStmt`, bool condition).
- [x] Loops: `loop` (infinite), `loop when` (while), `loop[i: 1...3]` (counted),
  with `stop` (break via `BreakSignal`).
- [x] Type system (dynamic): `string`, `bool`, `char`, and the full numeric family
  — `int`, `uint`, `long`, `ulong` (checked, width/signed promotion, auto-widening
  literals) and `double`, `float` (IEEE, decimal/`f`-suffix literals).
- [x] Lexical scopes: a chained `Environment` (each block opens a child scope) and
  assignment (`x = expr`, mutating an existing binding via `Assign`).
- [x] User-defined functions with typed parameters and `return` (each call runs in
  an `Environment` child of the global scope; `return` unwinds via `ReturnSignal`).
- [x] Logical operators: `&&`, `||` (short-circuiting, via a `Logical` node) and
  unary `!` (via a `Unary` node), with `logicOr`/`logicAnd`/`unary` precedence levels.
- [x] Standard library v1: `def contract` / `def module` / `external` functions.
  `Term` is now a built-in module (`external out` bound to a native), replacing the
  hardcoded `Term.out` shortcut. Static namespace resolution; contracts validated.
- [ ] Modules as first-class values + dynamic dispatch by contract
  (`let t: ITerminal = Term;`).
- [ ] A prelude written in `.own` that declares the built-in modules (needs file reading).
- [x] Read `.own` source files: `dotnet run --project src/OwnLang -- file.own`
  (see `Runner`, with `--ast` to dump the tree and clean error reporting).
- [ ] A REPL (interactive read-eval-print loop).

## Reference

Inspired by the architecture in [Crafting Interpreters](https://craftinginterpreters.com)
by Robert Nystrom.
