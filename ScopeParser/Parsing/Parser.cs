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
            return new Script(statements.ToArray());
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
            var identifier = parseIdentifier(false);
            if (identifier == null)
                return null;
            if (!match(TokenType.Equal))
                throw new ParseError("Expected '=' after identifier", next());
            var source = parseSource();
            expect(TokenType.SemiColon, ";");
            return new Assignment(identifier, source);
        }

        /// <summary>
        /// <OUTPUT> = "OUTPUT" <STREAM> "TO" <STRING> // Same, should be STRING_VALUE
        /// </summary>
        /// <returns></returns>
        public Output? parseOutput()
        {
            if (match(TokenType.Output))
            {
                var source = parseSource();
                expect(TokenType.To, "TO");
                // TODO: Support variable name as well.
                var outputFile = expect(TokenType.String, "filename");
                expect(TokenType.SemiColon, ";");
                return new Output(source, outputFile.ValueAs<string>());
            }
            return null;
        }

        public Identifier? parseIdentifier(bool throwOnFail = false)
        {
            if (match(TokenType.Identifier))
            {
                var token = previous();
                return new Identifier(token.ValueAs<string>());
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
                var fieldSpec = parseFieldSpec();
                expect(TokenType.From, "FROM");
                // TODO: Support variable name as well.
                var filename = expect(TokenType.String, "a string with filename");
                return new FileSource(fieldSpec, filename.ValueAs<string>());
            }
            return null;
        }

        /// <summary>
        /// <SELECT_QUERY> = "SELECT" <FIELD_SPEC> "FROM" <SOURCE>
        /// </summary>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        private SelectQuery? parseSelectQuery()
        {
            if (match(TokenType.Select))
            {
                var fieldSpec = parseFieldSpec();
                expect(TokenType.From, "FROM");
                var source = parseSource();
                return new SelectQuery(fieldSpec, source);
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
                return new Star();
            return null;
        }

        /// <summary>
        /// <FIELD_LIST> = <FIELD> | <FIELD> "," <FIELD>+
        /// </summary>
        /// <returns></returns>
        private FieldList parseFieldList()
        {
            var fields = new List<Field>
            {
                parseField()
            };
            while (match(TokenType.Comma))
            {
                fields.Add(parseField());
            }
            return new FieldList(fields.ToArray());
        }

        /// <summary>
        /// <FIELD> = <IDENTIFIER> | <FIELD_VALUE> "AS" <IDENTIFIER>
        /// </summary>
        /// <returns></returns>
        private Field parseField()
        {
            var identifier = expect(TokenType.Identifier, "field identifier");
            // TODO: Support "AS"
            // TODO: Support named source
            return new Field(identifier.ValueAs<string>());
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

        private bool match(TokenType type)
        {
            if (isAtEnd() ||
                tokens[current].TokenType != type)
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