using System.ComponentModel.DataAnnotations.Schema;
using System.Runtime.InteropServices;
using System.Text;

namespace ScopeParser.Lexing;

public class Lexer(string source)
{
    int line = 1;
    int column = 1;
    int index = 0;
    int startingLine, startingColumn;

    public IEnumerable<Token> Scan()
    {
        while (!isFinished())
        {
            startingLine = line;
            startingColumn = column;
            var token = scanToken();
            if (token != null)
            {
                // Some tokens are ignored, e.g. comments and whitespace
                yield return token;
            }
        }

        yield return new Token(TokenType.EndOfFile, null, line, column);
    }

    private Token? scanToken()
    {
        var c = next();
        switch (c)
        {
            case ' ':
            case '\t':
            case '\r':
                return null;
            case '\n':
                line++;
                column = 1;
                return null;
            case ';':
                return new Token(TokenType.SemiColon, null, line, column);
            case ',':
                return new Token(TokenType.Comma, null, line, column);
            case '*':
                return new Token(TokenType.Star, null, line, column);
            case '=':
                return new Token(TokenType.Equal, null, line, column);
            case '(':
                return new Token(TokenType.LParen, null, line, column);
            case ')':
                return new Token(TokenType.RParen, null, line, column);
            case '.':
                return new Token(TokenType.Dot, null, line, column);
            case ':':
                return new Token(TokenType.Colon, null, line, column);
            case '@':
                return new Token(TokenType.At, null, line, column);
            case '}':
                return new Token(TokenType.RBracket, null, line, column);
            case '#':
                var directive = scanReservedKeyword(directives);
                if (directive != null)
                    return directive;
                throw new LexError($"Unknown directive '#{peekWord()}'", startingLine, startingColumn);
            case '"':
                return scanString();
            case '{':
                return scanTsOrLeftBracket();
            case '/':
                return comment(c);
            default:
                if (char.IsDigit(c))
                {
                    back(); // Put back the digit to allow scanning
                    return scanNumber();
                }
                else if (char.IsLetter(c))
                {
                    back(); // Put back the letter to allow scanning
                    var keyword = scanReservedKeyword(reservedKeywords);
                    if (keyword != null)
                        return keyword;
                    else
                        return scanIdentifier();
                }
                else
                {
                    throw new LexError($"Unexpected character '{c}'", startingLine, startingColumn);
                }
        }
    }

    private Token? comment(char c)
    {
        if (peek() == '/')
        {
            next();
            while (!isFinished() && peek() != '\n')
            {
                next();
            }
            return null;
        }
        else if (peek() == '*')
        {
            next();
            while (!isFinished() && peek(2) != "*/")
            {
                if (peek() == '\n')
                {
                    line++;
                    column = 1;
                }
                next();
            }
            if (isFinished())
            {
                throw new LexError("Unterminated block comment", startingLine, startingColumn);
            }
            next(2);
            return null;
        }
        else
        {
            throw new LexError($"Unexpected character '{c}'", startingLine, startingColumn);
        }
    }

    Dictionary<string, TokenType> directives = new Dictionary<string, TokenType>
    {
        { "DECLARE", TokenType.Declare },
        { "SET", TokenType.Set },
        { "IF", TokenType.If },
        { "ELSE", TokenType.Else },
        { "END", TokenType.End },
        { "WARN", TokenType.Warn },
        { "ERROR", TokenType.Error },
    };

    Dictionary<string, TokenType> reservedKeywords = new Dictionary<string, TokenType> {
        { "SELECT", TokenType.Select },
        { "OUTPUT", TokenType.Output },
        { "EXTRACT", TokenType.Extract },
        { "FROM", TokenType.From },
        { "INNER", TokenType.Inner },
        { "OUTER", TokenType.Outer },
        { "JOIN", TokenType.Join },
        { "LEFT", TokenType.Left },
        { "RIGHT", TokenType.Right },
        { "ON", TokenType.On },
        { "WHERE", TokenType.Where },
        { "AS", TokenType.As },
        { "TO", TokenType.To },
        { "true", TokenType.Boolean },
        { "false", TokenType.Boolean },
        { "PARAM", TokenType.Param },
        { "IMPORT", TokenType.Import },
        { "EXPORT", TokenType.Export },
    };

    private Token? scanReservedKeyword(Dictionary<string, TokenType> reservedKeywords)
    {
        var identifier = peekWord();
        var matching = reservedKeywords.FirstOrDefault(keyword => keyword.Key == identifier);
        if (matching.Key != null)
        {
            next(identifier.Length);
            object? value = null;
            if (matching.Value == TokenType.Boolean)
                value = identifier == "true";
            return new Token(matching.Value, value, startingLine, startingColumn);
        }
        return null;
    }

    private string peekWord()
    {
        var end = this.index;

        while (end < source.Length && char.IsLetter(source[end]))
        {
            end++;
        }
        return source.Substring(this.index, end - this.index);
    }

    private Token scanString()
    {
        var str = new StringBuilder();
        var isEscaped = false;
        while (!isFinished() && (peek() != '"' || isEscaped))
        {
            if (peek() == '\\')
            {
                // Logic is just to account for escaping of \\. When
                // we see a \ we first check if we were already escaping before.
                isEscaped = !isEscaped;
            }
            else if (isEscaped)
            {
                isEscaped = false;
            }
            str.Append(next());
        }
        if (peek() != '"')
        {
            throw new LexError("Unterminated string", startingLine, startingColumn);
        }
        next();
        return new Token(TokenType.String, str.ToString(), startingLine, startingColumn);
    }

    private Token scanTsOrLeftBracket()
    {
        if (peek() != '{')
        {
            return new Token(TokenType.LBracket, null, startingLine, startingColumn);
        }
        else
        {
            // We have a ts expression
            next();
            return scanTsExpression();
        }
    }

    // To simplify the process, ts expressions are between {}
    private Token scanTsExpression()
    {
        var str = new StringBuilder();
        var isEscaped = false;
        while (!isFinished() && (peek(2) != "}}" || isEscaped))
        {
            if (peek() == '\\')
            {
                // Logic is just to account for escaping of \\. When
                // we see a \ we first check if we were already escaping before.
                isEscaped = !isEscaped;
            }
            else if (isEscaped)
            {
                isEscaped = false;
            }
            str.Append(next());
        }
        if (isFinished())
        {
            throw new LexError("Unterminated typescript expression", startingLine, startingColumn);
        }
        next(2);
        return new Token(TokenType.TsExpression, str.ToString().Trim(), startingLine, startingColumn);
    }

    private Token scanNumber()
    {
        var number = new StringBuilder();
        var tokenType = TokenType.Integer;
        while (char.IsDigit(peek()))
        {
            number.Append(next());
        }
        if (peek() == '.')
        {
            tokenType = TokenType.Decimal;
            number.Append(next());
            while (!isFinished() && char.IsDigit(peek()))
            {
                number.Append(next());
            }
        }
        return new Token(tokenType, decimal.Parse(number.ToString()), startingLine, startingColumn);
    }

    private Token scanIdentifier()
    {
        var identifier = new StringBuilder();
        while (!isFinished() && isIdentifierChar())
        {
            identifier.Append(next());
        }
        return new Token(TokenType.Identifier, identifier.ToString(), startingLine, startingColumn);
    }

    private bool isIdentifierChar()
    {
        char c = peek();
        return char.IsLetterOrDigit(c) || c == '_';
    }

    /**
    * Consume the next character from the source.
    */
    private char next()
    {
        column++;
        return source[index++];
    }

    private void back()
    {
        column--;
        index--;
    }

    /**
    * Consume the next n characters from the source.
    */
    private string next(int n)
    {
        column += n;
        var s = source.Substring(index, n);
        index += n;
        return s;
    }

    /**
    * Peek the next character from the source.
    */
    private char peek()
    {
        return source[index];
    }

    /**
    * Peek the next n characters from the source.
    */
    private string peek(int n)
    {
        return source.Substring(index, n > source.Length - index ? source.Length - index : n);
    }

    private bool isFinished()
    {
        return index >= source.Length;
    }
}