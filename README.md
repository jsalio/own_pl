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
        term.out("resultado:");
        term.out(result);
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
dotnet build                      # build the whole solution
dotnet run --project src/OwnLang  # execute the interpreter
```

`dotnet run` needs `--project` because the solution sits at the root with no
project file there. The code to interpret is defined as a string inside
`src/OwnLang/Program.cs`, which wires up the full pipeline:
source → tokens → AST → execution.

## Testing

The project has an NUnit regression suite in `tests/OwnLang.Tests`, with one
fixture per pipeline stage:

| Fixture | Covers |
|---|---|
| `LexerTests` | text → tokens (`DetectString`, `DetectVals`, `DetectCodeBlock`, keywords, operators, errors) |
| `ParserTests` | tokens → AST (precedence, associativity, call shape, syntax errors) |
| `InterpreterTests` | end-to-end execution (arithmetic, precedence, variables, `term.out` output) |
| `EnvironmentTests` | the runtime variable store |

```bash
dotnet test                                          # all tests (whole solution)
dotnet test --filter FullyQualifiedName~LexerTests   # one fixture
dotnet test --filter Name=DetectString               # one test
```

`InterpreterTests` verify behavior end-to-end and capture `term.out` output by
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
scope, and name lookup/assignment walks up the chain. The `term.out(...)` output
is wired to `Console.WriteLine` in this version.

## Project structure

```
own_pl/
├── OwnLang.sln                        The solution
├── src/
│   └── OwnLang/                       Interpreter project (assembly OwnLang)
│       ├── OwnLang.csproj
│       ├── Program.cs                 Entry point; wires up the pipeline
│       └── Internal/
│           ├── Token.cs               A token (type, lexeme, line, column)
│           ├── TokenTypes.cs          enum TokenType (all categories)
│           ├── Lexer.cs               Lexer : ILexer
│           ├── Parser.cs              Parser : IParser
│           ├── Environment.cs         Runtime variables
│           ├── Interpreter.cs         Interpreter : IInterpreter
│           ├── Ast/
│           │   ├── Expr.cs            Expressions (produce a value)
│           │   └── Stmt.cs            Statements (perform actions)
│           └── Contracts/
│               ├── ILexer.cs          interface ILexer
│               ├── IParser.cs         interface IParser
│               └── IInterpreter.cs    interface IInterpreter
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
| `Assign` | an assignment to an existing variable: `x = x + 1` |
| `MemberAccess` | member access: `term.out` |
| `Call` | a call: `out(result)` |

**Statements** (`Stmt`) — "what does it execute?":

| Node | Represents |
|---|---|
| `VarDecl` | `let val1 = 1;` |
| `ExpressionStmt` | an expression used as a statement: `term.out(...);` |
| `Block` | a block `{ ... }` |
| `WhenStmt` | a conditional: `when(c) { } else { }` |
| `LoopStmt` | an infinite loop: `loop { }` |
| `WhileStmt` | a pre-test loop: `loop when(c) { }` |
| `RangeLoopStmt` | a counted loop: `loop[i: 1...3] { }` |
| `StopStmt` | break out of the innermost loop: `stop;` |
| `FunctionDecl` | `function empty Main() { ... }` |
| `ProgramDecl` | `def program { ... }` (root node) |

## Grammar (current version)

```
program        → "def" IDENT block
declaration    → function | statement
function       → "function" returnType IDENT "(" params? ")" block
returnType     → "empty" | IDENT
params         → IDENT ( "," IDENT )*
block          → "{" statement* "}"

statement      → varDecl | whenStmt | loopStmt | stopStmt | exprStmt
varDecl        → ( "let" | typeName ) IDENT "=" expression ";"
typeName       → "string" | "bool" | "char"
               | "int" | "uint" | "long" | "ulong" | "double" | "float"
whenStmt       → "when" "(" expression ")" block ( "else" ( whenStmt | block ) )?
loopStmt       → "loop" ( "[" IDENT ":" expression "..." expression "]"
                        | "when" "(" expression ")" )? block
stopStmt       → "stop" ";"
exprStmt       → expression ";"

expression     → assignment
assignment     → IDENT "=" assignment | equality
equality       → comparison ( ( "==" | "!=" ) comparison )*
comparison     → additive ( ( "<" | "<=" | ">" | ">=" ) additive )*
additive       → multiplicative ( ( "+" | "-" ) multiplicative )*
multiplicative → call ( ( "*" | "/" ) call )*
call           → primary ( "." IDENT | "(" arguments? ")" )*
arguments      → expression ( "," expression )*
primary        → NUMBER | STRING | "true" | "false" | IDENT | "(" expression ")"
```

## Execution conventions

- The interpreter looks for the `Main` function inside `def` and executes its body.
- `term.out(x)` prints `x` to the console (hardcoded special case; there is no
  real object system yet).

## Current limitations

- Arithmetic (`+ - * /`), comparison (`< <= > >=`) and equality (`== !=`)
  operators. No logical operators (`&&`, `||`, `!`) yet. `+` concatenates when
  either operand is a string, coercing the other via `ToString()` (`"n" + 35` →
  `"n35"`); otherwise it is integer addition.
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
  truthiness coercion). `stop` outside a loop is an unhandled error.
- Scopes are lexical: every block (`when`/loop body, function body) opens a child
  scope, so a `let` inside a block is local to it and dies when the block ends.
  Assignment `x = expr` mutates an existing variable (searching enclosing scopes);
  assigning to an undeclared name is an error. `let`/typed declarations always
  create a new binding in the current scope.
- No user-defined function calls or effective parameters (the grammar accepts
  them, but the interpreter only runs `Main` and `term.out`).
- `term.out` is a hardcoded shortcut, not a real object with methods.

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
- [ ] Logical operators: `&&`, `||`, `!`.
- [ ] User-defined function calls + parameters (each call runs in an `Environment`
  child of the global scope).
- [ ] Real objects instead of the `term.out` shortcut.
- [ ] Read `.own` source files and/or a REPL.

## Reference

Inspired by the architecture in [Crafting Interpreters](https://craftinginterpreters.com)
by Robert Nystrom.
