TSScope is a toy implementation of a scope script compiler to Typescript.

# Usage

`ScopeCompiler` runs interactive mode.

`ScopeCompiler script.scope` outputs `script.ts` that can then be ran with `node --import=tsx script.ts`. Additional options can be found using `ScopeCompiler --help`.

The scripts it generates can be imported from other TypeScript sources.

Examples in the `Examples` folder. As of this version, it can support reading and outputting to CSV, selecting, filtering, and inner/outer joins between multiple sources.

This compiler is using this syntax:

```EBNF
<SCRIPT> ::= (<STATEMENT> ";") *

<STATEMENT> ::=
      <ASSIGNMENT>
    | <VARIABLE_DEFINITION>
    | <VARIABLE_ASSIGNMENT>
    | <PARAM>
    | <OUTPUT>
    | <IMPORT>
    | <EXPORT>

<EXPORT> ::= "EXPORT" <SOURCE>  [ "AS" <STRING> ]

<SOURCE> ::=
      <FILE_SOURCE>
    | <SELECT_QUERY>
    | <IDENTIFIER>

<IDENTIFIER> ::= <STRING>

<SELECT_QUERY> ::= "SELECT" <FIELD_SPEC> "FROM" <SELECT_SOURCE> [ <WHERE_STATEMENT> ]

<WHERE_STATEMENT> ::= "WHERE" <TS_EXPRESSION>

<TS_EXPRESSION> ::= "{{" <STRING> "}}"

<SELECT_SOURCE> ::=
      <ALIASABLE_SOURCE>
    | <JOIN_QUERY>

<JOIN_QUERY> ::= <SELECT_SOURCE> <ALIASABLE_SOURCE> <JOIN_TYPE> "ON" <TS_EXPRESSION>

<JOIN_TYPE> ::= "INNER" | "LEFT OUTER" | "RIGHT OUTER" | "OUTER"

<ALIASABLE_SOURCE> ::=
      <SOURCE>
    | <ALIASED_SOURCE>

<ALIASED_SOURCE> ::= <IDENTIFIER> "=" <SOURCE>

<FIELD_SPEC> ::=
      <FIELD_LIST>
    | <STAR>

<STAR> ::= "*"

<FIELD_LIST> ::= (<FIELD> ",") *

<FIELD> ::=
      <ALIASED_FIELD>
    | <FIELD_VALUE>

<FIELD_VALUE> ::=
      <INPUT_FIELD>
    | <TS_EXPRESSION>
    | <STRING_LITERAL>
    | <NUMBER_LITERAL>
    | <BOOLEAN_LITERAL>

<BOOLEAN_LITERAL> ::= <BOOL>

<NUMBER_LITERAL> ::= <DECIMAL>

<STRING_LITERAL> ::= <STRING>

<INPUT_FIELD> ::= <STRING> "." [ <STRING> ]

<ALIASED_FIELD> ::= <FIELD_VALUE> "AS" <STRING>

<FILE_SOURCE> ::= "EXTRACT" <FIELD_SPEC> "FROM" <STRING_VALUE>

<STRING_VALUE> ::=
      <STRING_LITERAL>
    | <VARIABLE_IDENTIFIER>
    | <TS_EXPRESSION>

<VARIABLE_IDENTIFIER> ::= "@" <STRING>

<IMPORT> ::= "IMPORT" <IDENTIFIER> "{" <TYPED_FIELD> "}"

<TYPED_FIELD> ::= <STRING> ":" <STRING>

<OUTPUT> ::= "OUTPUT" <SOURCE> "TO" <STRING_VALUE>

<PARAM> ::= "PARAM" <STRING> ":" [ <PARAM_DEFAULT_VALUE> ]

<PARAM_DEFAULT_VALUE> ::=
      <STRING_LITERAL>
    | <TS_EXPRESSION>

<VARIABLE_ASSIGNMENT> ::= "@" <STRING> "=" <VARIABLE_VALUE>

<VARIABLE_VALUE> ::=
      <TS_EXPRESSION>
    | <STRING_LITERAL>
    | <NUMBER_LITERAL>
    | <BOOLEAN_LITERAL>
    | <VARIABLE_IDENTIFIER>

<VARIABLE_DEFINITION> ::= "@" <STRING> ":" <STRING> "=" <VARIABLE_VALUE>

<ASSIGNMENT> ::= <IDENTIFIER> "=" <SOURCE>
```

# Development

Conditions and filtering are done using Typescript expressions, which are encapsulated using `{  }` for parsing simplicity. Closing brackets within must be escaped using `\}`.

Parser is mostly recursive, except for JOIN conditions, which are left-associative and parsed iteratively.

AST is generated using `node Tools/generateAst.js ./ScopeParser/AST`. It also generates a BNF syntax for the AST it generated.
