using System.Collections.Immutable;
using ScopeParser.Ast;
using ScopeParser.Lexing;

namespace ScopeParser.Parsing
{
    public class Parser(List<Token> tokens)
    {
        public List<ParseError> Errors = new List<ParseError>();
        public bool HasErrors => Errors.Count > 0;

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
                        statements.Add(statement);
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
        /// <STATEMENT> ::= <COMMENT> | <ASSIGNMENT> | <OUTPUT>
        /// </summary>
        /// <returns></returns>
        private Statement? parseStatement()
        {
            if (match(TokenType.SemiColon))
                return null;
            var assignment = parseAssignment();
            if (assignment != null) return assignment;
            var output = parseOutput();
            if (output != null) return output;
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
            expect(TokenType.SemiColon, ";");
            return new Assignment(token, identifier, source);
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
                // TODO: Support variable name as well.
                var outputFile = expect(TokenType.String, "filename");
                expect(TokenType.SemiColon, ";");
                return new Output(token, source, outputFile.ValueAs<string>());
            }
            return null;
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
                // TODO: Support variable name as well.
                var filename = expect(TokenType.String, "a string with filename");
                return new FileSource(token, fieldSpec, filename.ValueAs<string>());
            }
            return null;
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
            // Parsing Join is a bit different, since it's left recursive.
            // They're not nested.
            // We use iteration instead of recursion to parse the join.
            while (match(TokenType.Left, TokenType.Right, TokenType.Inner, TokenType.Outer))
            {
                var joinTypeToken = previous();
                var joinType = joinTypeToken.TokenType switch
                {
                    TokenType.Left => JoinType.Left,
                    TokenType.Right => JoinType.Right,
                    TokenType.Inner => JoinType.Inner,
                    TokenType.Outer => JoinType.Outer,
                    _ => throw new ParseError("Invalid join type", joinTypeToken)
                };
                expect(TokenType.Join, "JOIN");
                var right = parseAliasableSource();
                expect(TokenType.On, "ON");
                var condition = parseTsExpression();
                left = new JoinQuery(joinTypeToken, left, right, joinType, condition);
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
                var expression = parseTsExpression();
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
            var identifier = expect(TokenType.Identifier, "field or source identifier");
            if (match(TokenType.Dot))
            {
                var field = expect(TokenType.Identifier, "field identifier");
                if (field == null)
                    throw new ParseError("Expected field after dot", next());
                return new Field(token, field.ValueAs<string>(), identifier.ValueAs<string>());
            }
            return new Field(token, identifier.ValueAs<string>(), null);
            // TODO: Support "AS"
        }

        private TsExpression parseTsExpression()
        {
            var token = expect(TokenType.TsExpression, "a valid TS expression");
            return new TsExpression(token, token.ValueAs<string>());
        }

        private void synchronize()
        {
            if (isAtEnd()) return;
            do
            {
                var token = peek();
                switch (token.TokenType)
                {
                    case TokenType.Extract:
                    case TokenType.Identifier:
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
            if (isAtEnd() ||
                !types.Contains(tokens[current].TokenType))
                return false;
            next();
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