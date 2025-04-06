namespace ScopeParser.Test;

using Xunit;
using ScopeParser.Lexing;
using ScopeParser.Parsing;
using FluentAssertions;
using ScopeParser.Ast;

public class ParserTest
{
    [Fact]
    public void TestEmpty()
    {
        // Given
        var source = new List<Token> {
            fromTokenType(TokenType.EndOfFile)
        };
        var parser = new Parser(source);

        // When
        var script = parser.parse();

        // Then
        validateScript(parser, script, 0);
    }

    [Fact]
    public void TestUnexpected()
    {
        // Given
        var source = new List<Token> {
            fromTokenType(TokenType.SemiColon),
            fromTokenType(TokenType.Star),
            fromTokenType(TokenType.EndOfFile),
            };

        // When
        var parser = new Parser(source);
        var script = parser.parse();

        // Then
        script.Should().NotBeNull();
        script.Statements.Should().BeEmpty();
        parser.HasErrors.Should().BeTrue();
        parser.Errors.Should().HaveCount(1);
        parser.Errors[0].Message.Should().Be("Unexpected token");
        parser.Errors[0].Token.Should().Be(source[1]);
    }


    [Fact]
    public void TestSynchronization()
    {
        // Given
        var source = new List<Token> {
            new Token(TokenType.Identifier, "source_name", 1, 1),
            fromTokenType(TokenType.Equal),
            new Token(TokenType.Identifier, "source_name", 1, 1),
            fromTokenType(TokenType.SemiColon),
            new Token(TokenType.Identifier, "source_name", 1, 1),
            fromTokenType(TokenType.Equal),
            fromTokenType(TokenType.Select),
            fromTokenType(TokenType.Star),
            fromTokenType(TokenType.SemiColon),
            new Token(TokenType.Identifier, "source_name", 1, 1),
            fromTokenType(TokenType.Equal),
            new Token(TokenType.Identifier, "source_name", 1, 1),
            fromTokenType(TokenType.SemiColon),
            fromTokenType(TokenType.Star),
            fromTokenType(TokenType.Star),
            fromTokenType(TokenType.EndOfFile),
            };

        // When
        var parser = new Parser(source);
        var script = parser.parse();

        // Then
        script.Should().NotBeNull();
        parser.HasErrors.Should().BeTrue();
        parser.Errors.Should().HaveCount(2);
        parser.Errors[0].Message.Should().Be("Expected 'FROM'");
        parser.Errors[1].Message.Should().Be("Unexpected token");
        parser.Errors[1].Token.TokenType.Should().Be(TokenType.Star);
    }

    [Fact]
    public void TestAssignmentFromExtract()
    {
        // Given
        var source = new List<Token> {
            new Token(TokenType.Identifier, "source_name", 1, 1),
            fromTokenType(TokenType.Equal),
            fromTokenType(TokenType.Extract),
            fromTokenType(TokenType.Star),
            fromTokenType(TokenType.From),
            new Token(TokenType.String, "file.csv", 1, 10),
            fromTokenType(TokenType.SemiColon),
            fromTokenType(TokenType.EndOfFile),
        };

        // when
        var parser = new Parser(source);
        var script = parser.parse();

        // then
        validateScript(parser, script);
        var assignment = validateAssignment(script.Statements[0], "source_name");
        FileSource fileSource = validateFileSource(assignment);
        ValidateStringLiteral(fileSource.FileName, "file.csv");
        validateStar(fileSource.FieldSpec);
    }

    [Fact]
    public void TestAssignmentFromExtractWithVariableName()
    {
        // Given
        var source =
            defineVariable("file", "string", "file.csv").Concat(
            new List<Token> {
                new Token(TokenType.Identifier, "source_name", 1, 1),
                fromTokenType(TokenType.Equal),
                fromTokenType(TokenType.Extract),
                fromTokenType(TokenType.Star),
                fromTokenType(TokenType.From),
                fromTokenType(TokenType.At),
                new Token(TokenType.Identifier, "file", 1, 10),
                fromTokenType(TokenType.SemiColon),
                fromTokenType(TokenType.EndOfFile),
            }).ToList();

        // when
        var parser = new Parser(source);
        var script = parser.parse();

        // then
        validateScript(parser, script, 2);
        var assignment = validateAssignment(script.Statements[1], "source_name");
        FileSource fileSource = validateFileSource(assignment);
        validateVariableIdentifier(fileSource.FileName, "file");
        validateStar(fileSource.FieldSpec);
    }

    [Fact]
    public void TestAssignmentFromExtractWithTsExpression()
    {
        // Given
        var source = new List<Token> {
            new Token(TokenType.Identifier, "source_name", 1, 1),
            fromTokenType(TokenType.Equal),
            fromTokenType(TokenType.Extract),
            fromTokenType(TokenType.Star),
            fromTokenType(TokenType.From),
            new Token(TokenType.TsExpression, "SomeExpression", 1, 10),
            fromTokenType(TokenType.SemiColon),
            fromTokenType(TokenType.EndOfFile),
        };

        // when
        var parser = new Parser(source);
        var script = parser.parse();

        // then
        validateScript(parser, script, 1);
        var assignment = validateAssignment(script.Statements[0], "source_name");
        FileSource fileSource = validateFileSource(assignment);
        validateTsExpression(fileSource.FileName, "SomeExpression");
        validateStar(fileSource.FieldSpec);
    }

    [Fact]
    public void TestAssignmentFromExtractWithIncorrectVariableType()
    {
        // Given
        var source =
            defineVariable("file", "number", "1.1").Concat(
            new List<Token> {
                new Token(TokenType.Identifier, "source_name", 1, 1),
                fromTokenType(TokenType.Equal),
                fromTokenType(TokenType.Extract),
                fromTokenType(TokenType.Star),
                fromTokenType(TokenType.From),
                fromTokenType(TokenType.At),
                new Token(TokenType.Identifier, "file", 1, 10),
                fromTokenType(TokenType.SemiColon),
                fromTokenType(TokenType.EndOfFile),
            }).ToList();

        // when
        var parser = new Parser(source);
        var script = parser.parse();

        // then
        validateError(parser, "Expected a variable of type 'string'", TokenType.At);
    }

    [Fact]
    public void TestAssignmentFromOtherSource()
    {
        // Given
        var source = new List<Token> {
            new Token(TokenType.Identifier, "source_name_1", 1, 1),
            fromTokenType(TokenType.Equal),
            new Token(TokenType.Identifier, "source_name_2", 1, 1),
            fromTokenType(TokenType.SemiColon),
            fromTokenType(TokenType.EndOfFile),
        };

        // when
        var parser = new Parser(source);
        var script = parser.parse();

        // then
        validateScript(parser, script);
        var assignment = validateAssignment(script.Statements[0], "source_name_1");
        validateIdentifierSource(assignment.Source, "source_name_2");
    }

    [Fact]
    public void TestVariableDefinition()
    {
        // Given
        var source = new List<Token> {
            fromTokenType(TokenType.At),
            new Token(TokenType.Identifier, "variable_name", 1, 1),
            fromTokenType(TokenType.Colon),
            new Token(TokenType.Identifier, "type", 1, 1),
            fromTokenType(TokenType.Equal),
            new Token(TokenType.String, "value", 1, 1),
            fromTokenType(TokenType.SemiColon),
            fromTokenType(TokenType.EndOfFile),
        };

        // when
        var parser = new Parser(source);
        var script = parser.parse();

        // then
        validateScript(parser, script);
        var variableAssignment = ValidateVariableDefinition(script.Statements[0], "variable_name", "type");
        ValidateStringLiteral(variableAssignment.Value, "value");
    }

    [Fact]
    public void TestVariableRedefinition()
    {
        // Given
        var source = new List<Token> {
            fromTokenType(TokenType.At),
            new Token(TokenType.Identifier, "variable_name", 1, 1),
            fromTokenType(TokenType.Colon),
            new Token(TokenType.Identifier, "type", 1, 1),
            fromTokenType(TokenType.Equal),
            new Token(TokenType.String, "value", 1, 1),
            fromTokenType(TokenType.SemiColon),
        };
        source = source.Concat(source).Concat(new List<Token> { fromTokenType(TokenType.EndOfFile) }).ToList();

        // when
        var parser = new Parser(source);
        var script = parser.parse();

        // then
        validateError(parser, "Variable 'variable_name' is already defined", TokenType.At);
    }

    [Fact]
    public void TestVariableAssignment()
    {
        // Given
        // TODO: check for variable is not defined
        var source = new List<Token> {
            fromTokenType(TokenType.At),
            new Token(TokenType.Identifier, "variable_name", 1, 1),
            fromTokenType(TokenType.Equal),
            fromTokenType(TokenType.At),
            // Todo: test for no @
            new Token(TokenType.Identifier, "variable_name_2", 1, 1),
            fromTokenType(TokenType.SemiColon),
            fromTokenType(TokenType.EndOfFile),
        };

        // when
        var parser = new Parser(source);
        var script = parser.parse();

        // then
        validateScript(parser, script);
        var variableAssignment = validateVariableAssignment(script.Statements[0], "variable_name");
        validateVariableIdentifier(variableAssignment.Value, "variable_name_2");
    }


    [Fact]
    public void TestParamDefinitionWithoutDefaultValue()
    {
        // Given
        var source = new List<Token> {
            fromTokenType(TokenType.Param),
            fromTokenType(TokenType.At),
            new Token(TokenType.Identifier, "variable_name", 1, 1),
            fromTokenType(TokenType.SemiColon),
            fromTokenType(TokenType.EndOfFile),
        };

        // when
        var parser = new Parser(source);
        var script = parser.parse();

        // then
        validateScript(parser, script);
        validateParam(script.Statements[0], "variable_name", false);
    }

    [Fact]
    public void TestParamDefinitionWithDefaultValue()
    {
        // Given
        var source = new List<Token> {
            fromTokenType(TokenType.Param),
            fromTokenType(TokenType.At),
            new Token(TokenType.Identifier, "variable_name", 1, 1),
            fromTokenType(TokenType.Equal),
            new Token(TokenType.String, "1.1m", 1, 1),
            fromTokenType(TokenType.SemiColon),
            fromTokenType(TokenType.EndOfFile),
        };

        // when
        var parser = new Parser(source);
        var script = parser.parse();

        // then
        validateScript(parser, script);
        var variableAssignment = validateParam(script.Statements[0], "variable_name", true);
        ValidateStringLiteral(variableAssignment.DefaultValue!, "1.1m");
    }

    [Fact]
    public void TestMissingSemiColumn()
    {
        // Given
        var source = new List<Token> {
            new Token(TokenType.Identifier, "source_name_1", 1, 1),
            fromTokenType(TokenType.Equal),
            new Token(TokenType.Identifier, "source_name_2", 1, 1),
            fromTokenType(TokenType.EndOfFile),
        };

        // when
        var parser = new Parser(source);
        parser.parse();

        // then
        parser.HasErrors.Should().BeTrue();
        parser.Errors.Count.Should().Be(1);
        parser.Errors[0].Should().BeOfType<ParseError>();
        var error = (ParseError)parser.Errors[0];
        error.Problem.Should().Be("Unexpected end of file, expected ';'");
    }

    [Fact]
    public void TestMultiStatements()
    {
        // Given
        var source1 = new List<Token> {
            new Token(TokenType.Identifier, "source_name_1", 1, 1),
            fromTokenType(TokenType.Equal),
            new Token(TokenType.Identifier, "source_name_2", 1, 1),
            fromTokenType(TokenType.SemiColon),
        };
        var sources = source1.Concat(source1).ToList();
        sources.Add(fromTokenType(TokenType.EndOfFile));

        // when
        var parser = new Parser(sources);
        var script = parser.parse();

        // then
        validateScript(parser, script, 2);
        for (int i = 0; i < 2; i++)
        {
            var assignment = validateAssignment(script.Statements[i], "source_name_1");
            validateIdentifierSource(assignment.Source, "source_name_2");
        }
    }

    [Fact]
    public void TestOutputFromSourceName()
    {
        // Given
        var source = new List<Token> {
            fromTokenType(TokenType.Output),
            new Token(TokenType.Identifier, "source_name", 1, 1),
            fromTokenType(TokenType.To),
            new Token(TokenType.String, "filename", 1, 1),
            fromTokenType(TokenType.SemiColon),
            fromTokenType(TokenType.EndOfFile),
        };

        // when
        var parser = new Parser(source);
        var script = parser.parse();

        // then
        validateScript(parser, script);
        var output = validateOutput(script.Statements[0]);
        ValidateStringLiteral(output.OutputFile, "filename");
        validateIdentifierSource(output.Source, "source_name");
    }

    [Fact]
    public void TestOutputFromSourceNameToVariable()
    {
        // Given
        var source = defineVariable("file", "string", "file.csv").Concat(
            new List<Token> {
                fromTokenType(TokenType.Output),
                new Token(TokenType.Identifier, "source_name", 1, 1),
                fromTokenType(TokenType.To),
                fromTokenType(TokenType.At),
                new Token(TokenType.Identifier, "file", 1, 1),
                fromTokenType(TokenType.SemiColon),
                fromTokenType(TokenType.EndOfFile),
            }).ToList();

        // when
        var parser = new Parser(source);
        var script = parser.parse();

        // then
        validateScript(parser, script, 2);
        var output = validateOutput(script.Statements[1]);
        validateIdentifierSource(output.Source, "source_name");
        var variable = validateVariableIdentifier(output.OutputFile, "file");
    }

    [Fact]
    public void TestOutputFromSourceNameToVariableNotFound()
    {
        // Given
        var source = new List<Token> {
            fromTokenType(TokenType.Output),
            new Token(TokenType.Identifier, "source_name", 1, 1),
            fromTokenType(TokenType.To),
            fromTokenType(TokenType.At),
            new Token(TokenType.Identifier, "file", 1, 1),
            fromTokenType(TokenType.SemiColon),
            fromTokenType(TokenType.EndOfFile),
        };

        // when
        var parser = new Parser(source);
        var script = parser.parse();

        // then
        validateError(parser, "Variable 'file' is not defined", TokenType.At);
    }

    [Fact]
    public void TestSelectStar()
    {
        // Given
        var source = new List<Token> {
            new Token(TokenType.Identifier, "output", 1, 1),
            fromTokenType(TokenType.Equal),
            fromTokenType(TokenType.Select),
            fromTokenType(TokenType.Star),
            fromTokenType(TokenType.From),
            new Token(TokenType.Identifier, "input", 1, 4),
            fromTokenType(TokenType.SemiColon),
            fromTokenType(TokenType.EndOfFile),
        };

        // when
        var parser = new Parser(source);
        var script = parser.parse();

        // then
        validateScript(parser, script);
        var assignment = validateAssignment(script.Statements[0], "output");
        var select = validateSelectQuery(assignment.Source);
        validateStar(select.Fields);
        validateIdentifierSource(select.Source, "input");
    }

    [Fact]
    public void TestSelectFields()
    {
        // Given
        var source = new List<Token> {
            new Token(TokenType.Identifier, "output", 1, 1),
            fromTokenType(TokenType.Equal),
            fromTokenType(TokenType.Select),
            new Token(TokenType.Identifier, "input", 1, 2),
            fromTokenType(TokenType.Dot),
            new Token(TokenType.Identifier, "field_name", 1, 2),
            fromTokenType(TokenType.Comma),
            new Token(TokenType.Identifier, "field_name_2", 1, 3),
            fromTokenType(TokenType.From),
            new Token(TokenType.Identifier, "input", 1, 4),
            fromTokenType(TokenType.SemiColon),
            fromTokenType(TokenType.EndOfFile),
        };

        // when
        var parser = new Parser(source);
        var script = parser.parse();

        // then
        validateScript(parser, script);
        var assignment = validateAssignment(script.Statements[0], "output");
        var select = validateSelectQuery(assignment.Source);
        validateInputFields(select.Fields, ("input", "field_name"), (null, "field_name_2"));
        validateIdentifierSource(select.Source, "input");
    }

    [Fact]
    public void TestSelectFieldsWithAlias()
    {
        // Given
        var source = new List<Token> {
            new Token(TokenType.Identifier, "output", 1, 1),
            fromTokenType(TokenType.Equal),
            fromTokenType(TokenType.Select),
            new Token(TokenType.Identifier, "input", 1, 2),
            fromTokenType(TokenType.Dot),
            new Token(TokenType.Identifier, "field_name", 1, 2),
            fromTokenType(TokenType.Comma),
            new Token(TokenType.Identifier, "field_name_2", 1, 3),
            fromTokenType(TokenType.As),
            new Token(TokenType.Identifier, "field_name_2_alias", 1, 4),
            fromTokenType(TokenType.Comma),
            new Token(TokenType.TsExpression, "tsExpression", 1, 5),
            fromTokenType(TokenType.As),
            new Token(TokenType.Identifier, "field_name_3_alias", 1, 6),
            fromTokenType(TokenType.From),
            new Token(TokenType.Identifier, "input", 1, 5),
            fromTokenType(TokenType.SemiColon),
            fromTokenType(TokenType.EndOfFile),
        };

        // when
        var parser = new Parser(source);
        var script = parser.parse();

        // then
        validateScript(parser, script);
        var assignment = validateAssignment(script.Statements[0], "output");
        var select = validateSelectQuery(assignment.Source);
        var fieldList = validateFieldList(select.Fields, 3);
        validateInputField(fieldList.Fields[0], "input", "field_name");
        var aliasedField = validateAliasedField(fieldList.Fields[1], "field_name_2_alias");
        validateInputField(aliasedField.Field, null, "field_name_2");
        var tsField = validateAliasedField(fieldList.Fields[2], "field_name_3_alias");
        validateTsExpression(tsField.Field, "tsExpression");
        validateIdentifierSource(select.Source, "input");
    }

    [Fact]
    public void TestSelectLiterals()
    {
        // Given
        var source = new List<Token> {
            new Token(TokenType.Identifier, "output", 1, 1),
            fromTokenType(TokenType.Equal),
            fromTokenType(TokenType.Select),
            new Token(TokenType.String, "string_value", 1, 7),
            fromTokenType(TokenType.As),
            new Token(TokenType.Identifier, "string_value_alias", 1, 8),
            fromTokenType(TokenType.Comma),
            new Token(TokenType.Decimal, 1.5m, 1, 7),
            fromTokenType(TokenType.Comma),
            new Token(TokenType.Boolean, false, 1, 5),
            fromTokenType(TokenType.From),
            new Token(TokenType.Identifier, "input", 1, 5),
            fromTokenType(TokenType.SemiColon),
            fromTokenType(TokenType.EndOfFile),
        };

        // when
        var parser = new Parser(source);
        var script = parser.parse();

        // then
        validateScript(parser, script);
        var assignment = validateAssignment(script.Statements[0], "output");
        var select = validateSelectQuery(assignment.Source);
        var fieldList = validateFieldList(select.Fields, 3);
        var stringField = validateAliasedField(fieldList.Fields[0], "string_value_alias");
        ValidateStringLiteral(stringField.Field, "string_value");

        ValidateNumberLiteral(fieldList.Fields[1], 1.5m);
        ValidateBooleanLiteral(fieldList.Fields[2], false);
    }

    [Fact]
    public void TestOneJoin()
    {
        // Given
        var source = new List<Token> {
            new Token(TokenType.Identifier, "output", 1, 1),
            fromTokenType(TokenType.Equal),
            fromTokenType(TokenType.Select),
            new Token(TokenType.Identifier, "input", 1, 2),
            fromTokenType(TokenType.Dot),
            new Token(TokenType.Identifier, "field_name", 1, 2),
            fromTokenType(TokenType.Comma),
            new Token(TokenType.Identifier, "field_name_2", 1, 3),
            fromTokenType(TokenType.From),
            new Token(TokenType.Identifier, "input", 1, 4),
            fromTokenType(TokenType.Inner),
            fromTokenType(TokenType.Join),
            new Token(TokenType.Identifier, "input_2", 1, 5),
            fromTokenType(TokenType.On),
            new Token(TokenType.TsExpression, "filter", 1, 6),
            fromTokenType(TokenType.SemiColon),
            fromTokenType(TokenType.EndOfFile),
        };

        // when
        var parser = new Parser(source);
        var script = parser.parse();

        // then
        validateScript(parser, script);
        var assignment = validateAssignment(script.Statements[0], "output");
        var select = validateSelectQuery(assignment.Source);
        validateInputFields(select.Fields, ("input", "field_name"), (null, "field_name_2"));
        var join = validateJoinQuery(select.Source);
        validateIdentifierSource(join.Left, "input");
        validateIdentifierSource(join.Right, "input_2");
        validateTsExpression(join.Condition, "filter");
    }

    [Fact]
    public void TestMultiJoin()
    {
        // Given
        var source = new List<Token> {
            new Token(TokenType.Identifier, "output", 1, 1),
            fromTokenType(TokenType.Equal),
            fromTokenType(TokenType.Select),
            new Token(TokenType.Identifier, "input", 1, 2),
            fromTokenType(TokenType.Dot),
            new Token(TokenType.Identifier, "field_name", 1, 2),
            fromTokenType(TokenType.Comma),
            new Token(TokenType.Identifier, "field_name_2", 1, 3),
            fromTokenType(TokenType.From),
            new Token(TokenType.Identifier, "input", 1, 4),
            fromTokenType(TokenType.Inner),
            fromTokenType(TokenType.Join),
            new Token(TokenType.Identifier, "input_2", 1, 5),
            fromTokenType(TokenType.As),
            new Token(TokenType.Identifier, "input_2_alias", 1, 6),
            fromTokenType(TokenType.On),
            new Token(TokenType.TsExpression, "filter_1", 1, 7),
            fromTokenType(TokenType.Outer),
            fromTokenType(TokenType.Join),
            new Token(TokenType.Identifier, "input_3", 1, 8),
            fromTokenType(TokenType.On),
            new Token(TokenType.TsExpression, "filter_2", 1, 9),
            fromTokenType(TokenType.SemiColon),
            fromTokenType(TokenType.EndOfFile),
        };

        // when
        var parser = new Parser(source);
        var script = parser.parse();

        // then
        validateScript(parser, script);
        var assignment = validateAssignment(script.Statements[0], "output");
        var select = validateSelectQuery(assignment.Source);
        validateInputFields(select.Fields, ("input", "field_name"), (null, "field_name_2"));
        var secondJoin = validateJoinQuery(select.Source);
        var firstJoin = validateJoinQuery(secondJoin.Left);
        validateIdentifierSource(firstJoin.Left, "input");
        validateAliasedSource(firstJoin.Right, "input_2", "input_2_alias");
        validateTsExpression(firstJoin.Condition, "filter_1");
        validateIdentifierSource(secondJoin.Right, "input_3");
        validateTsExpression(secondJoin.Condition, "filter_2");
    }

    [Fact]
    public void TestSelectWhere()
    {
        // Given
        var source = new List<Token> {
            new Token(TokenType.Identifier, "output", 1, 1),
            fromTokenType(TokenType.Equal),
            fromTokenType(TokenType.Select),
            fromTokenType(TokenType.Star),
            fromTokenType(TokenType.From),
            new Token(TokenType.Identifier, "input", 1, 4),
            fromTokenType(TokenType.Where),
            new Token(TokenType.TsExpression, "filter", 1, 5),
            fromTokenType(TokenType.SemiColon),
            fromTokenType(TokenType.EndOfFile),
        };

        // when
        var parser = new Parser(source);
        var script = parser.parse();

        // then
        validateScript(parser, script);
        var assignment = validateAssignment(script.Statements[0], "output");
        var select = validateSelectQuery(assignment.Source);
        validateStar(select.Fields);
        validateIdentifierSource(select.Source, "input");
        validateWhereQuery(select, "filter");
    }

    private void validateScript(Parser parser, Script script, int statementCount = 1)
    {
        parser.HasErrors.Should().BeFalse();
        script.Should().NotBeNull();
        script.Statements.Should().HaveCount(statementCount);
    }

    private ParseError validateError(Parser parser, string expectedMessage, TokenType expectedTokenType)
    {
        parser.HasErrors.Should().BeTrue();
        parser.Errors.Should().HaveCount(1);
        parser.Errors[0].Should().BeOfType<ParseError>();
        var error = (ParseError)parser.Errors[0];
        error.Problem.Should().Be(expectedMessage);
        error.Token.TokenType.Should().Be(expectedTokenType);
        return error;
    }

    private Assignment validateAssignment(Statement statement, string expectedVariableName)
    {
        statement.Should().BeOfType<Assignment>();
        var assignment = (Assignment)statement;
        assignment.VariableName.Value.Should().Be(expectedVariableName);
        assignment.Source.Should().NotBeNull();
        return assignment;
    }

    private static Output validateOutput(Statement statement)
    {
        statement.Should().BeOfType<Output>();
        var output = (Output)statement;
        return output;
    }

    private static FileSource validateFileSource(Assignment assignment)
    {
        assignment.Source.Should().NotBeNull();
        assignment.Source.Should().BeOfType<FileSource>();
        var fileSource = (FileSource)assignment.Source;
        return fileSource;
    }

    private SelectQuery validateSelectQuery(Source source)
    {
        source.Should().BeOfType<SelectQuery>();
        return (SelectQuery)source;
    }

    private WhereStatement validateWhereQuery(SelectQuery selectQuery, string expected)
    {
        selectQuery.Where.Should().NotBeNull();
        var whereQuery = selectQuery.Where;
        validateTsExpression(whereQuery.Condition, expected);
        return (WhereStatement)selectQuery.Where;
    }

    private JoinQuery validateJoinQuery(SelectSource source)
    {
        source.Should().BeOfType<JoinQuery>();
        return (JoinQuery)source;
    }

    private Identifier validateIdentifierSource(SelectSource source, string expected)
    {
        source.Should().BeOfType<Identifier>();
        var identifier = (Identifier)source;
        identifier.Value.Should().Be(expected);
        return identifier;
    }

    private Identifier validateIdentifier(Identifier identifer, string expected)
    {
        identifer.Should().BeOfType<Identifier>();
        var identifier = (Identifier)identifer;
        identifier.Value.Should().Be(expected);
        return identifier;
    }

    private AliasedSource validateAliasedSource(SelectSource source, string expectedInput, string expectedAlias)
    {
        source.Should().BeOfType<AliasedSource>();
        var aliasedSource = (AliasedSource)source;
        validateIdentifierSource(aliasedSource.Source, expectedInput);
        validateIdentifier(aliasedSource.Alias, expectedAlias);
        return aliasedSource;
    }

    private Star validateStar(FieldSpec fieldSpec)
    {
        fieldSpec.Should().BeOfType<Star>();
        return (Star)fieldSpec;
    }

    private FieldList validateFieldList(FieldSpec fieldSpec, int expectedLength)
    {
        fieldSpec.Should().BeOfType<FieldList>();
        var fieldList = (FieldList)fieldSpec;
        fieldList.Fields.Should().HaveCount(expectedLength);
        return fieldList;
    }

    private FieldList validateInputFields(FieldSpec fieldSpec, params (string?, string)[] expected)
    {
        var fieldList = validateFieldList(fieldSpec, expected.Length);
        for (int i = 0; i < expected.Length; i++)
        {
            validateInputField(fieldList.Fields[i], expected[i].Item1, expected[i].Item2);
        }
        return fieldList;
    }

    private InputField validateInputField(Field field, string? expectedNs, string expectedName)
    {
        field.Should().BeOfType<InputField>();
        var inputField = (InputField)field;
        inputField.Ns.Should().Be(expectedNs);
        inputField.Name.Should().Be(expectedName);
        return inputField;
    }

    private AliasedField validateAliasedField(Field field, string expectedAlias)
    {
        field.Should().BeOfType<AliasedField>();
        var aliasedField = (AliasedField)field;
        aliasedField.Alias.Should().Be(expectedAlias);
        return aliasedField;
    }

    private TsExpression validateTsExpression(Node node, string expected)
    {
        node.Should().BeOfType<TsExpression>();
        var expression = (TsExpression)node;
        expression.Expression.Should().Be(expected);
        return expression;
    }

    private StringLiteral ValidateStringLiteral(Node field, string expectedValue)
    {
        field.Should().BeOfType<StringLiteral>();
        var stringLiteral = (StringLiteral)field;
        stringLiteral.Value.Should().Be(expectedValue);
        return stringLiteral;
    }
    private NumberLiteral ValidateNumberLiteral(Node field, decimal expectedValue)
    {
        field.Should().BeOfType<NumberLiteral>();
        var numberLiteral = (NumberLiteral)field;
        numberLiteral.Value.Should().Be(expectedValue);
        return numberLiteral;
    }
    private BooleanLiteral ValidateBooleanLiteral(Node field, bool expectedValue)
    {
        field.Should().BeOfType<BooleanLiteral>();
        var booleanLiteral = (BooleanLiteral)field;
        booleanLiteral.Value.Should().Be(expectedValue);
        return booleanLiteral;
    }

    private VariableDefinition ValidateVariableDefinition(Statement statement, string expectedName, string expectedType)
    {
        statement.Should().BeOfType<VariableDefinition>();
        var variableDefinition = (VariableDefinition)statement;
        variableDefinition.VariableName.Should().Be(expectedName);
        variableDefinition.Type.Should().Be(expectedType);
        variableDefinition.Value.Should().NotBeNull();
        return variableDefinition;
    }

    private VariableAssignment validateVariableAssignment(Statement statement, string expectedVariableName)
    {
        statement.Should().BeOfType<VariableAssignment>();
        var variableAssignment = (VariableAssignment)statement;
        variableAssignment.VariableName.Should().Be(expectedVariableName);
        variableAssignment.Value.Should().NotBeNull();
        return variableAssignment;
    }

    private VariableIdentifier validateVariableIdentifier(Node node, string expectedName)
    {
        node.Should().BeOfType<VariableIdentifier>();
        var variableIdentifier = (VariableIdentifier)node;
        variableIdentifier.VariableName.Should().Be(expectedName);
        return variableIdentifier;
    }

    private Param validateParam(Statement statement, string expectedName, bool expectDefaultValue)
    {
        statement.Should().BeOfType<Param>();
        var param = (Param)statement;
        param.Name.Should().Be(expectedName);
        if (expectDefaultValue)
        {
            param.DefaultValue.Should().NotBeNull();
        }
        else
        {
            param.DefaultValue.Should().BeNull();
        }
        return param;
    }

    private Token fromTokenType(TokenType type)
    {
        return new Token(type, null, 1, 1);
    }

    private List<Token> defineVariable(string name, string type, string value)
    {
        return new List<Token> {
            fromTokenType(TokenType.At),
            new Token(TokenType.Identifier, name, 1, 1),
            fromTokenType(TokenType.Colon),
            new Token(TokenType.Identifier, type, 1, 1),
            fromTokenType(TokenType.Equal),
            new Token(TokenType.String, value, 1, 1),
            fromTokenType(TokenType.SemiColon),
        };
    }
}