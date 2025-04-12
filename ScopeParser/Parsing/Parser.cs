using System.Collections.Immutable;
using ScopeParser.Ast;
using ScopeParser.Lexing;

namespace ScopeParser.Parsing
{
    public class Parser(List<Token> tokens)
    {
        public List<ParseError> Errors = new List<ParseError>();
        public bool HasErrors => Errors.Count > 0;

        private Dictionary<string, string> variables = new Dictionary<string, string>();
        private int current = 0;

        /// <summary>
        /// <SCRIPT> ::= <STATEMENT>* EOF
        /// </summary>
        /// <returns></returns>
        public Script parse()
        {
            List<Statement> statements = new List<Statement>();
            while (!isAtEnd())
            {
                try
                {
                    Statement? statement = parseStatement();
                    if (statement != null)
                    {
                        statements.Add(statement);
                        expect(TokenType.SemiColon, ";");
                    }
                }
                catch (ParseError e)
                {
                    Errors.Add(e);
                    synchronize();
                }
            }
            return new Script(tokens[0], statements.ToArray());
        }

        /// <summary>
        /// <STATEMENT> ::= <COMMENT> | <ASSIGNMENT> | <VARIABLE_DEFINITION> | <VARIABLE_ASSIGNMENT> | <PARAM> | <OUTPUT>
        /// </summary>
        /// <returns></returns>
        private Statement? parseStatement()
        {
            Statement? statement;
            if (match(TokenType.SemiColon))
                return null;
            var statementParsers = new List<Func<Statement?>>
            {
                parseParam,
                parseImport,
                parseExport,
                parseAssignment,
                parseOutput,
                parseVariableAssignmentOrDefinition,
            };
            foreach (var parser in statementParsers)
            {
                statement = parser();
                if (statement != null)
                    return statement;
            }
            Errors.Add(new ParseError("Unexpected token", next()));
            synchronize();
            return null;
        }

        /// <summary>
        /// <ASSIGNMENT> ::= <SOURCE_NAME> '=' <SOURCE>
        /// </summary>
        /// <returns></returns>
        /// <exception cref="ParseError"></exception>
        private Assignment? parseAssignment()
        {
            var token = peek();
            var identifier = parseIdentifier(false);
            if (identifier == null)
                return null;
            expect(TokenType.Equal, "=");
            var source = parseSource();
            return new Assignment(token, identifier, source);
        }

        /// <summary>
        /// Parses both variable assignment and definition given that they start similarly,
        /// then branch out to the correct parsing method.
        /// </summary>
        /// <returns></returns>
        /// <exception cref="ParseError"></exception>
        private Statement? parseVariableAssignmentOrDefinition()
        {
            VariableIdentifier? variableIdentifier;
            if ((variableIdentifier = parseVariableIdentifier()) != null)
            {
                // If next token is a colon, it's a variable definition.
                if (match(TokenType.Colon))
                    return parseVariableDefinition(variableIdentifier);
                // Otherwise, it's a variable assignment.
                return parseVariableAssignment(variableIdentifier);
            }
            return null;
        }

        /// <summary>
        /// <VARIABLE_ASSIGNMENT> ::= <VARIABLE_IDENTIFIER> '=' <VARIABLE_VALUE>
        /// </summary>
        /// <returns></returns>
        /// <exception cref="ParseError"></exception>
        private VariableAssignment parseVariableAssignment(VariableIdentifier variableIdentifier)
        {
            expect(TokenType.Equal, "=");
            var value = parseVariableValue();
            return new VariableAssignment(variableIdentifier.Token, variableIdentifier.VariableName, value);
        }

        /// <summary>
        /// <VARIABLE_DEFINITION> ::= <VARIABLE_IDENTIFIER> ':' <IDENTIFIER> '=' <VARIABLE_VALUE>   
        /// </summary>
        /// <returns></returns>
        /// <exception cref="ParseError"></exception>
        private VariableDefinition parseVariableDefinition(VariableIdentifier variableIdentifier)
        {
            if (variables.ContainsKey(variableIdentifier.VariableName))
                throw new ParseError($"Variable '{variableIdentifier.VariableName}' is already defined", variableIdentifier.Token);
            // We already consumed the ":" in parseVariableAssignment
            var type = parseIdentifier(true)!;
            variables[variableIdentifier.VariableName] = type.Value;
            expect(TokenType.Equal, "=");
            var value = parseVariableValue();
            return new VariableDefinition(variableIdentifier.Token, variableIdentifier.VariableName, type.Value, value);
        }

        private VariableIdentifier? parseVariableIdentifier()
        {
            if (match(TokenType.At))
            {
                var token = previous();
                var name = expect(TokenType.Identifier, "variable name");
                return new VariableIdentifier(token, name.ValueAs<string>());
            }
            return null;
        }

        private VariableValue parseVariableValue()
        {
            VariableValue? value;
            if ((value = parseLiteralVariableValue()) != null)
            {
                return value!;
            }
            else if ((value = parseVariableIdentifier()) != null)
            {
                return value;
            }
            else if ((value = parseTsExpression(false)) != null)
            {
                return value;
            }

            throw new ParseError("Expected variable value", next());
        }

        private VariableValue? parseLiteralVariableValue()
        {
            VariableValue? literalValue;
            if ((literalValue = parseStringLiteral()) != null || (literalValue = parseNumberLiteral()) != null || (literalValue = parseBooleanLiteral()) != null)
            {
                return literalValue;
            }
            return null;
        }

        private Param? parseParam()
        {
            if (match(TokenType.Param))
            {
                var token = previous();
                var identifier = parseVariableIdentifier();
                if (identifier == null)
                    throw new ParseError("Expected identifier", next());
                variables.Add(identifier.VariableName, "string");
                ParamDefaultValue? variableValue = null;
                if (match(TokenType.Equal))
                {
                    // Has default value
                    variableValue = parseParamDefaultValue();
                }
                return new Param(token, identifier.VariableName, variableValue);
            }
            return null;
        }

        private ParamDefaultValue parseParamDefaultValue()
        {
            ParamDefaultValue? value;
            if ((value = parseStringLiteral()) != null)
            {
                return value!;
            }
            else if ((value = parseTsExpression(false)) != null)
            {
                return value;
            }

            throw new ParseError("Expected default value", next());
        }


        /// <summary>
        /// <OUTPUT> = "OUTPUT" <STREAM> "TO" <STRING> // Same, should be STRING_VALUE
        /// </summary>
        /// <returns></returns>
        public Output? parseOutput()
        {
            if (match(TokenType.Output))
            {
                var token = previous();
                var source = parseSource();
                expect(TokenType.To, "TO");

                var outputFile = parseStringValue();
                return new Output(token, source, outputFile);
            }
            return null;
        }

        /// <summary>
        /// <IMPORT> = "IMPORT" <IDENTIFIER> [ "{" <TYPED_FIELD_LIST> "}" ]
        public Import? parseImport()
        {
            if (match(TokenType.Import))
            {
                var token = previous();
                var name = parseIdentifier(true)!;
                var fields = new List<TypedField>();
                if (nextIs(TokenType.LBracket))
                {
                    next();
                    while (!nextIs(TokenType.RBracket))
                    {
                        if (match(TokenType.Comma))
                        {
                            // Just ignore commas. That technically makes them optional.
                            continue;
                        }
                        var field = parseTypedField();
                        if (field == null)
                            continue;
                        fields.Add(field);
                    }
                    next();
                }

                return new Import(token, name, fields);
            }
            return null;
        }

        public TypedField? parseTypedField()
        {
            var name = parseIdentifier(true)!;
            expect(TokenType.Colon, ":");
            var type = parseIdentifier(true)!;
            return new TypedField(name.Token, name.Value, type.Value);
        }

        /// <summary>
        /// <EXPORT> = "EXPORT" <SOURCE> [ "AS" <IDENTIFIER> ]
        /// </summary>
        /// <returns></returns>
        private Export? parseExport()
        {
            if (match(TokenType.Export))
            {
                var token = previous();
                var source = parseSource();
                string? name = null;
                if (nextIs(TokenType.As))
                {
                    next();
                    name = parseIdentifier(true)!.Value;
                }
                return new Export(token, source, name);
            }
            return null;
        }

        private void validateVariable(VariableIdentifier variableIdentifier, string? variableType)
        {
            if (!variables.ContainsKey(variableIdentifier.VariableName))
                throw new ParseError($"Variable '{variableIdentifier.VariableName}' is not defined", variableIdentifier.Token);
            if (variableType != null)
            {
                if (variables[variableIdentifier.VariableName] != variableType)
                    throw new ParseError($"Expected a variable of type '{variableType}'", variableIdentifier.Token);
            }
        }

        public Identifier? parseIdentifier(bool throwOnFail = false)
        {
            if (match(TokenType.Identifier))
            {
                var token = previous();
                return new Identifier(token, token.ValueAs<string>());
            }
            else if (throwOnFail)
            {
                throw new ParseError("Expected identifier", next());
            }
            return null;
        }

        /// <summary>
        /// <SOURCE> = <FILE_SOURCE> | <SELECT_QUERY> | <IDENTIFIER>
        /// </summary>
        /// <returns></returns>
        /// <exception cref="ParseError"></exception>
        public Source parseSource()
        {
            var fileSource = parseFileSource();
            if (fileSource != null) return fileSource;
            var select = parseSelectQuery();
            if (select != null) return select;
            var identifier = parseIdentifier();
            if (identifier != null) return identifier;
            throw new ParseError("Expected source definition", next());
        }

        /// <summary>
        /// <FILE_SOURCE> = "EXTRACT" <FIELDS> 'FROM' <STRING> // Should be STRING_VALUE
        /// </summary>
        /// <returns></returns>
        private FileSource? parseFileSource()
        {
            if (match(TokenType.Extract))
            {
                var token = previous();
                var fieldSpec = parseFieldSpec();
                expect(TokenType.From, "FROM");
                var filename = parseStringValue();
                return new FileSource(token, fieldSpec, filename);
            }
            return null;
        }

        private StringValue parseStringValue()
        {
            StringValue? stringValue;
            if ((stringValue = parseStringLiteral()) != null)
            {
                return stringValue;
            }
            else if ((stringValue = parseVariableIdentifier()) != null)
            {
                validateVariable((VariableIdentifier)stringValue, "string");
                return stringValue;
            }
            else if ((stringValue = parseTsExpression(false)) != null)
            {
                return stringValue;
            }

            throw new ParseError("Expected string value", next());
        }

        /// <summary>
        /// <SELECT_QUERY> = "SELECT" <FIELD_SPEC> "FROM" <SELECT_SOURCE> [ <WHERE_STATEMENT> ]
        /// </summary>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        private SelectQuery? parseSelectQuery()
        {
            if (match(TokenType.Select))
            {
                var token = previous();
                var fieldSpec = parseFieldSpec();
                expect(TokenType.From, "FROM");
                var source = ParseSelectSource();
                var where = parseWhereStatement();
                return new SelectQuery(token, fieldSpec, source, where);
            }
            return null;
        }

        /// <summary>
        /// <SELECT_SOURCE> = <JOIN_QUERY> | <ALIASABLE_SOURCE>
        /// </summary>
        private SelectSource ParseSelectSource()
        {
            var source = parseAliasableSource();
            // Parsing join is different because it's
            // right associative. This method returns source
            // if no join is found.
            var join = parseJoinQuery(source);
            return join;
        }

        /// <summary>
        /// <JOIN_QUERY> = <SELECT_SOURCE> <JOIN_TYPE> <ALIASABLE_SOURCE> "ON" <TS_EXPRESSION>
        /// </summary>
        private SelectSource parseJoinQuery(SelectSource left)
        {
            Token? direction = null;
            // Parsing Join is a bit different, since it's left recursive.
            // They're not nested.
            // We use iteration instead of recursion to parse the join.
            while (match(TokenType.Left, TokenType.Right, TokenType.Inner, TokenType.Outer))
            {
                var token = previous();

                if (token.TokenType == TokenType.Left || token.TokenType == TokenType.Right)
                {
                    if (direction != null)
                        throw new ParseError($"Unexpected token: '{token.TokenType.ToString().ToUpper()}'", token);
                    direction = token;
                    continue;
                }

                var joinType = token.TokenType switch
                {
                    TokenType.Inner => JoinType.Inner,
                    TokenType.Outer => JoinType.Outer,
                    _ => throw new ParseError("Invalid join type", token)
                };

                if (direction != null)
                {
                    switch (joinType)
                    {
                        case JoinType.Inner:
                            throw new ParseError("Inner join cannot have a direction", direction);
                        case JoinType.Outer:
                            joinType = direction.TokenType == TokenType.Left ? JoinType.LeftOuter : JoinType.RightOuter;
                            break;
                        default:
                            throw new ParseError("Invalid join type", token);
                    }
                }

                expect(TokenType.Join, "JOIN");
                var right = parseAliasableSource();
                expect(TokenType.On, "ON");
                var condition = parseTsExpression(true)!;
                left = new JoinQuery(direction ?? token, left, right, joinType, condition);
            }
            return left;
        }

        private AliasableSource parseAliasableSource()
        {
            var source = parseSource();
            if (source == null)
                throw new ParseError("Expected source", next());
            if (match(TokenType.As))
            {
                var alias = parseIdentifier(true)!;
                return new AliasedSource(source.Token, source, alias);
            }
            return source;
        }

        /// <summary>
        /// "WHERE" <TS_EXPRESSION>
        /// </summary>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        private WhereStatement? parseWhereStatement()
        {
            if (match(TokenType.Where))
            {
                var token = previous();
                var expression = parseTsExpression(true)!;
                return new WhereStatement(token, expression);
            }
            return null;
        }

        /// <summary>
        /// <FIELD_SPEC> = "*" | <FIELD_LIST>
        /// </summary>
        /// <returns></returns>
        /// <exception cref="ParseError"></exception>
        private FieldSpec parseFieldSpec()
        {
            var star = parseStar();
            if (star != null) return star;
            var fieldList = parseFieldList();
            if (fieldList != null) return fieldList;
            throw new ParseError("Expected * or a list of fields", next());
        }

        private Star? parseStar()
        {
            if (match(TokenType.Star))
                return new Star(previous());
            return null;
        }

        /// <summary>
        /// <FIELD_LIST> = <FIELD> | <FIELD> "," <FIELD>+
        /// </summary>
        /// <returns></returns>
        private FieldList parseFieldList()
        {
            var token = peek();
            var fields = new List<Field>
            {
                parseField()
            };
            while (match(TokenType.Comma))
            {
                fields.Add(parseField());
            }
            return new FieldList(token, fields.ToArray());
        }

        /// <summary>
        /// <FIELD> = <FIELD_IDENTIFIER> | <FIELD_VALUE> "AS" <IDENTIFIER>
        /// <FIELD_IDENTIFIER> ::= <IDENTIFIER> | <IDENTIFIER> '.' <IDENTIFIER>
        /// </summary>
        /// <returns></returns>
        private Field parseField()
        {
            var token = peek();
            FieldValue fieldValue;
            FieldValue? literalValue;
            if ((literalValue = parseTsExpression(false)) != null)
            {
                fieldValue = literalValue;
            }
            else if ((literalValue = parseLiteralFieldValue()) != null)
            {
                fieldValue = literalValue!;
            }
            else
            {
                var identifier = expect(TokenType.Identifier, "field or source identifier");
                if (match(TokenType.Dot)) // There's a namespace
                {
                    var field = expect(TokenType.Identifier, "field identifier");
                    if (field == null)
                        throw new ParseError("Expected field after dot", next());
                    fieldValue = new InputField(token, field.ValueAs<string>(), identifier.ValueAs<string>());
                }
                else
                {
                    fieldValue = new InputField(token, identifier.ValueAs<string>(), null);
                }
            }

            if (match(TokenType.As))
            {
                var alias = parseIdentifier(true)!;
                return new AliasedField(token, fieldValue, alias.Value);
            }
            return fieldValue;
        }

        private FieldValue? parseLiteralFieldValue()
        {
            FieldValue? literalValue;
            if ((literalValue = parseStringLiteral()) != null || (literalValue = parseNumberLiteral()) != null || (literalValue = parseBooleanLiteral()) != null)
            {
                return literalValue;
            }
            return null;
        }

        private StringLiteral? parseStringLiteral()
        {
            if (nextIs(TokenType.String))
            {
                var stringToken = expect(TokenType.String, "a string");
                return new StringLiteral(stringToken, stringToken.ValueAs<string>());
            }
            return null;
        }

        private NumberLiteral? parseNumberLiteral()
        {
            if (nextIs(TokenType.Integer))
            {
                var numberToken = expect(TokenType.Integer, "an integer");
                return new NumberLiteral(numberToken, numberToken.ValueAs<decimal>());
            }
            else if (nextIs(TokenType.Decimal))
            {
                var numberToken = expect(TokenType.Decimal, "a decimal");
                return new NumberLiteral(numberToken, numberToken.ValueAs<decimal>());
            }
            return null;
        }

        private BooleanLiteral? parseBooleanLiteral()
        {
            if (nextIs(TokenType.Boolean))
            {
                var booleanToken = expect(TokenType.Boolean, "a boolean");
                return new BooleanLiteral(booleanToken, booleanToken.ValueAs<bool>());
            }
            return null;
        }

        private TsExpression? parseTsExpression(bool throwOnFail = true)
        {
            if (nextIs(TokenType.TsExpression))
            {
                var token = next();
                return new TsExpression(token, token.ValueAs<string>());
            }
            if (throwOnFail)
                throw new ParseError("Expected a valid TS expression", next());
            return null;
        }

        private void synchronize()
        {
            if (isAtEnd()) return;
            do
            {
                var token = peek();
                switch (token.TokenType)
                {
                    case TokenType.SemiColon:
                    case TokenType.Extract:
                    case TokenType.Output:
                        return;
                }
                next();
            } while (!isAtEnd());
        }

        private Token expect(TokenType type, string token)
        {
            if (isAtEnd())
                throw new ParseError($"Unexpected end of file, expected '{token}'", previous());
            if (!match(type))
                throw new ParseError($"Expected '{token}'", next());
            return previous();
        }

        private bool match(params TokenType[] types)
        {
            var matched = nextIs(types);
            if (matched)
                next();
            return matched;
        }

        private bool nextIs(params TokenType[] types)
        {
            if (isAtEnd() ||
                !types.Contains(tokens[current].TokenType))
                return false;
            return true;
        }

        private Token previous()
        {
            return tokens[current - 1];
        }

        private Token peek()
        {
            return tokens[current];
        }

        private Token next()
        {
            return tokens[current++];
        }

        private bool isAtEnd()
        {
            if (current >= tokens.Count())
                throw new Exception("Reached end of tokens before finding EndOfFile");
            return tokens[current].TokenType == TokenType.EndOfFile;
        }
    }
}