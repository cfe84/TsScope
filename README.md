TSScope is a toy implementation of a scope script compiler to Typescript.

Usage: `ScopeCompiler script.scope` outputs `script.ts` that can then be ran with `node --import=tsx script.ts`.

Examples in the `Examples` folder. As of this version, it can support reading and outputting to CSV, selecting, filtering, and inner joins between multiple sources.

This compiler is using this syntax:

```EBNF

<SCRIPT> ::= (<STATEMENT> ";") * EOF

<STATEMENT> ::=
	  <COMMENT>
	| <INPUT_PARAMETER>
	| <VARIABLE_DEFINITION>
	| <VARIABLE_ASSIGNMENT>
	| <ASSIGNMENT>
	| <OUTPUT>

<ASSIGNMENT> ::= <SOURCE_NAME> '=' <SOURCE>

<SOURCE> = <FILE_SOURCE> | <SELECT_QUERY> | <IDENTIFIER>

<FILE_SOURCE> = "EXTRACT" <FIELD_SPEC> 'FROM' <STRING> // Should be STRING_VALUE

<SELECT_QUERY> = "SELECT" <FIELD_SPEC> "FROM" <SELECT_SOURCE> [ <WHERE_STATEMENT> ]

<WHERE_STATEMENT> = "WHERE" <TS_EXPRESSION>

<SELECT_SOURCE> = <JOIN_QUERY> | <ALIASABLE_SOURCE>

<ALIASABLE_SOURCE> = <SOURCE> | <SOURCE> AS <IDENTIFIER>

<JOIN_QUERY> = <SELECT_SOURCE> <JOIN_TYPE> <ALIASABLE_SOURCE> "ON" <TS_EXPRESSION>

<JOIN_TYPE> = "INNER JOIN"

<OUTPUT> = "OUTPUT" <STREAM> "TO" <STRING> // Same, should be STRING_VALUE

<FIELD_SPEC> = "*"
	| <FIELD_LIST> // Todo: support *, somefield AS something, { somefield / 2 } AS somethingElse

<FIELD_LIST> = <FIELD>
	| <FIELD> "," <FIELD>+


<FIELD> = <UNALIASED_FIELD>	| <ALIASED_FIELD>

<ALIASED_FIELD> ::= <INPUT_FIELD> "AS" <IDENTIFIER>

<UNALIASED_FIELD> ::= <INPUT_FIELD>

<INPUT_FIELD> ::= <IDENTIFIER>
    | <IDENTIFIER> '.' <IDENTIFIER>
	| <TS_EXPRESSION>
	| <STRING_LITERAL>

<INPUT_PARAMETER> ::= "PARAM" <VARIABLE_IDENTIFIER> ":" <TYPE>

<VARIABLE_DEFINITION> ::= <VARIABLE_IDENTIFIER> ":" <TYPE> "=" <VARIABLE_VALUE>

<VARIABLE_ASSIGNMENT> ::= <VARIABLE_IDENTIFIER> "=" <VARIABLE_VALUE>

<VARIABLE_IDENTIFIER> ::= "@" <IDENTIFIER>

<VARIABLE_VALUE> ::= <LITERAL> | <TSEXPRESSION> | <VARIABLE_IDENTIFIER>

<COMMENT> ::=
	  <OC_COMMENT>
	| <EOL_COMMENT>

<EOL_COMMENT> ::= '//' <TEXT> <EOL>
<OC_COMMENT> ::= '/*' <TEXT> '*/'

<LITERAL> ::= <STRING> | <NUMBER> | <BOOL>

<TYPE> ::= <ALPHA_NUM>

<STRING_VALUE> ::= <STRING> | <VARIABLE_REFERENCE>

<STRING> ::= '"' [^"] '"'

<IDENTIFIER> ::= <ALPHA_NUM>

<ALPHA_NUM> ::= [a-zA-Z_][a-zA-Z0-9_-]*
<TEXT> ::= [^\n]*
<EOL> ::= \n
<TS_EXPRESSION> ::= '{{' [^}]+ '}}'
```

Conditions and filtering are done using Typescript expressions, which are encapsulated using `{  }` for parsing simplicity. Closing brackets within must be escaped using `\}`.

Parser is mostly recursive, except for JOIN conditions, which are left-associative and parsed iteratively.

AST is generated using `node Tools/generateAst.js`.
