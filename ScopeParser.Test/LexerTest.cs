namespace ScopeParser.Test;

using Xunit;
using ScopeParser.Lexing;
using FluentAssertions;

public class LexerTest
{

    [Fact]
    public void TestEmpty()
    {
        // Given
        var source = "";
        var lexer = new Lexer(source);

        // When
        var tokens = lexer.Scan().ToList();

        // Then
        tokens.Should().HaveCount(1);
        tokens[0].Should().Be(new Token(TokenType.EndOfFile, null, 1, 1));
    }

    [Fact]
    public void TestWhitespace()
    {
        // Given
        var source = " \n\t\r";
        var lexer = new Lexer(source);

        // When
        var tokens = lexer.Scan().ToList();

        // Then
        tokens[0].Should().Be(new Token(TokenType.EndOfFile, null, 2, 3));
    }

    [Fact]
    public void TestChars()
    {
        // Given
        var source = "* \n ;,=()";
        var lexer = new Lexer(source);

        // When
        var tokens = lexer.Scan().ToList();

        // Then
        CheckTokens([TokenType.Star, TokenType.SemiColon, TokenType.Comma, TokenType.Equal, TokenType.LParen,
            TokenType.RParen, TokenType.EndOfFile], tokens);
    }

    [Fact]
    public void TestJustAComment()
    {
        // Given
        var source = "// This is a comment";
        var lexer = new Lexer(source);

        // When
        var tokens = lexer.Scan().ToList();

        // Then
        tokens[0].Should().Be(new Token(TokenType.EndOfFile, null, 1, 21));
    }

    [Fact]
    public void TestSingleLineComment()
    {
        // Given
        var source = "// This is a comment\n// This is another comment\n*";
        var lexer = new Lexer(source);

        // When
        var tokens = lexer.Scan().ToList();

        // Then
        CheckTokens([TokenType.Star, TokenType.EndOfFile], tokens);
        tokens[1].Line.Should().Be(3);
        tokens[1].Column.Should().Be(2);
    }

    [Fact]
    public void TestMultiLineComment()
    {
        // Given
        var source = "/* This is a comment\nThis is another comment */*";
        var lexer = new Lexer(source);

        // When
        var tokens = lexer.Scan().ToList();

        // Then
        CheckTokens([TokenType.Star, TokenType.EndOfFile], tokens);
        tokens[1].Line.Should().Be(2);
        tokens[1].Column.Should().Be(29);
    }

    [Fact]
    public void TestUnclosedMultineComment()
    {
        // Given
        var source = "/* This is a comment\nThis is another comment *";
        var lexer = new Lexer(source);

        // When
        Action action = () => lexer.Scan().ToList();

        // Then
        action.Should().Throw<LexError>().WithMessage("Unterminated block comment");
    }

    [Theory]
    [InlineData("#DECLARE", TokenType.Declare)]
    [InlineData("#SET", TokenType.Set)]
    [InlineData("#IF", TokenType.If)]
    [InlineData("#ELSE", TokenType.Else)]
    [InlineData("#END", TokenType.End)]
    [InlineData("#ERROR", TokenType.Error)]
    [InlineData("#WARN", TokenType.Warn)]
    [InlineData("SELECT", TokenType.Select)]
    [InlineData("FROM", TokenType.From)]
    [InlineData("OUTER", TokenType.Outer)]
    [InlineData("INNER", TokenType.Inner)]
    [InlineData("JOIN", TokenType.Join)]
    [InlineData("LEFT", TokenType.Left)]
    [InlineData("RIGHT", TokenType.Right)]
    [InlineData("EXTRACT", TokenType.Extract)]
    [InlineData("OUTPUT", TokenType.Output)]
    [InlineData("WHERE", TokenType.Where)]
    [InlineData("AS", TokenType.As)]
    [InlineData("ON", TokenType.On)]
    [InlineData("TO", TokenType.To)]
    public void TestDirectiveAndKeywords(string input, TokenType expected)
    {
        // Given
        var source = $"* \n // Some comment \n {input}\n*";
        var lexer = new Lexer(source);

        // When
        var tokens = lexer.Scan().ToList();

        // Then
        CheckTokens([TokenType.Star, expected, TokenType.Star, TokenType.EndOfFile], tokens);
        tokens[1].Line.Should().Be(3);
        tokens[1].Column.Should().Be(2);
    }

    [Theory]
    [InlineData("select")]
    [InlineData("SELEC")]
    [InlineData("Select")]
    [InlineData("SELECt")]
    [InlineData("SELECTION")]
    public void TestIncorrectKeyword(string input)
    {
        // Given
        var source = $"* \n // Some comment \n {input}\n*";
        var lexer = new Lexer(source);

        // When
        var tokens = lexer.Scan().ToList();

        // Then
        CheckTokens([TokenType.Star, TokenType.Identifier, TokenType.Star, TokenType.EndOfFile], tokens);
        tokens[1].Value.Should().Be(input);
        tokens[1].Line.Should().Be(3);
        tokens[1].Column.Should().Be(2);
    }

    [Fact]
    public void TestIncorrectDirective()
    {
        // Given
        var source = "// This is a comment\n #DEFINE \n*";
        var lexer = new Lexer(source);

        // When
        Action action = () => lexer.Scan().ToList();

        // Then
        action.Should().Throw<LexError>().WithMessage("Unknown directive '#DEFINE'");
    }

    [Fact]
    public void TestDirectiveAtTheEnd()
    {
        // Given
        var source = $"* \n // Some comment \n SELEC";
        var lexer = new Lexer(source);

        // When
        var tokens = lexer.Scan().ToList();

        // Then
        CheckTokens([TokenType.Star, TokenType.Identifier, TokenType.EndOfFile], tokens);
        tokens[1].Line.Should().Be(3);
        tokens[1].Column.Should().Be(2);
        tokens[1].ValueAs<string>().Should().Be("SELEC");
    }

    [Fact]
    public void TestIdentifier()
    {
        // Given
        var source = $"* \n // Some comment \n Identifier \n*";
        var lexer = new Lexer(source);

        // When
        var tokens = lexer.Scan().ToList();

        // Then
        CheckTokens([TokenType.Star, TokenType.Identifier, TokenType.Star, TokenType.EndOfFile], tokens);
        tokens[1].Line.Should().Be(3);
        tokens[1].Column.Should().Be(2);
        tokens[1].Value.Should().Be("Identifier");
    }

    [Theory]
    [InlineData("14", TokenType.Integer, 14, typeof(decimal))]
    [InlineData("14.5", TokenType.Decimal, 14.5, typeof(decimal))]
    [InlineData("true", TokenType.Boolean, true, typeof(bool))]
    [InlineData("false", TokenType.Boolean, false, typeof(bool))]
    public void TestLiterals(string input, TokenType expected, object value, Type expectedType)
    {
        // Given
        var source = $"* \n // Some comment \n {input} \n*";
        var lexer = new Lexer(source);

        // When
        var tokens = lexer.Scan().ToList();

        // Then
        CheckTokens([TokenType.Star, expected, TokenType.Star, TokenType.EndOfFile], tokens);
        tokens[1].Line.Should().Be(3);
        tokens[1].Column.Should().Be(2);
        tokens[1].Value.Should().BeOfType(expectedType);
        tokens[1].Value.Should().Be(value);
    }

    [Theory]
    [InlineData("%")]
    [InlineData("{")]
    public void TestUnexpectedCharacter(string input)
    {
        // Given
        var source = $"something fishy {input} in there";
        var lexer = new Lexer(source);

        // When
        Action action = () => lexer.Scan().ToList();

        // Then
        action.Should().Throw<LexError>().WithMessage($"Unexpected character '{input}'");
    }

    [Theory]
    [InlineData("\"Hello, World!\"")]
    [InlineData("\"String with an escaped \\\" double quote\"")]
    [InlineData("\"String with \\ Some more \\\\ escaping \\\t in the middle\"")]
    public void TestString(string input)
    {
        // Given
        var source = $"/* A comment */ \n {input} \n*";
        var lexer = new Lexer(source);

        // When
        var tokens = lexer.Scan().ToList();

        // Then
        CheckTokens([TokenType.String, TokenType.Star, TokenType.EndOfFile], tokens);
        tokens[0].Line.Should().Be(2);
        tokens[0].Column.Should().Be(2);
        tokens[0].ValueAs<string>().Should().Be(input.Substring(1, input.Length - 2));
    }

    [Theory]
    [InlineData("{{dfsfs.toString()}}")]
    [InlineData("{{dfsfs.toString(); \n another line}}")]
    [InlineData("{{ \"This string has \\}}\" }}")]
    public void TestTsExpression(string input)
    {
        // Given
        var source = $"/* A comment */ \n {input} \n*";
        var lexer = new Lexer(source);

        // When
        var tokens = lexer.Scan().ToList();

        // Then
        CheckTokens([TokenType.TsExpression, TokenType.Star, TokenType.EndOfFile], tokens);
        tokens[0].Line.Should().Be(2);
        tokens[0].Column.Should().Be(2);
        tokens[0].ValueAs<string>().Should().Be(input.Substring(2, input.Length - 4).Trim());
    }

    [Fact]
    public void TestUnclosedTsExpression()
    {
        // Given
        var source = "/* A comment */ \n {{this is an unclosed ts expression \n*";
        var lexer = new Lexer(source);

        // When
        Action action = () => lexer.Scan().ToList();

        // Then
        action.Should().Throw<LexError>().WithMessage("Unterminated typescript expression");
    }

    [Fact]
    public void TestParsesDot()
    {
        // Given
        var source = "a.b";
        var lexer = new Lexer(source);

        // When
        var tokens = lexer.Scan().ToList();

        // Then
        CheckTokens([TokenType.Identifier, TokenType.Dot, TokenType.Identifier, TokenType.EndOfFile], tokens);
        tokens[0].ValueAs<string>().Should().Be("a");
        tokens[2].ValueAs<string>().Should().Be("b");
    }

    private void CheckTokens(TokenType[] expected, List<Token> actual)
    {
        actual.Should().HaveCount(expected.Length);
        for (int i = 0; i < expected.Length; i++)
        {
            actual[i].TokenType.Should().Be(expected[i]);
        }
    }
}
