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

- **Symbols:** `{ } ( ) ; = + . ,`
- **Numbers:** integers (`1`, `123`)
- **Strings:** `"text"`
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

Variables live in an `Environment` (a `name → value` map). The `term.out(...)`
output is wired to `Console.WriteLine` in this version.

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
| `Variable` | a reference to a name: `val1` |
| `Binary` | a binary operation: `val1 + val2` |
| `MemberAccess` | member access: `term.out` |
| `Call` | a call: `out(result)` |

**Statements** (`Stmt`) — "what does it execute?":

| Node | Represents |
|---|---|
| `VarDecl` | `let val1 = 1;` |
| `ExpressionStmt` | an expression used as a statement: `term.out(...);` |
| `Block` | a block `{ ... }` |
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

statement      → varDecl | exprStmt
varDecl        → "let" IDENT "=" expression ";"
exprStmt       → expression ";"

expression     → additive
additive       → multiplicative ( ( "+" | "-" ) multiplicative )*
multiplicative → call ( ( "*" | "/" ) call )*
call           → primary ( "." IDENT | "(" arguments? ")" )*
arguments      → expression ( "," expression )*
primary        → NUMBER | STRING | IDENT | "(" expression ")"
```

## Execution conventions

- The interpreter looks for the `Main` function inside `def` and executes its body.
- `term.out(x)` prints `x` to the console (hardcoded special case; there is no
  real object system yet).

## Current limitations

- Arithmetic operators `+ - * /` only. No comparison or boolean operators.
- Only integers and strings — no booleans or decimals; integer `/` truncates
  (e.g. `7 / 2` is `3`).
- No control flow (`if`, `while`).
- No user-defined function calls or effective parameters (the grammar accepts
  them, but the interpreter only runs `Main` and `term.out`).
- `term.out` is a hardcoded shortcut, not a real object with methods.

## Roadmap

- [x] `-` operator (subtraction), sharing the `additive` level with `+`.
- [x] `* /` operators with precedence (a `multiplicative` level below `additive`).
- [ ] User-defined function calls + parameters (a chained `Environment` per scope).
- [ ] Control flow: `if`, `while`.
- [ ] Booleans and comparison operators.
- [ ] Real objects instead of the `term.out` shortcut.
- [ ] Read `.own` source files and/or a REPL.

## Reference

Inspired by the architecture in [Crafting Interpreters](https://craftinginterpreters.com)
by Robert Nystrom.
