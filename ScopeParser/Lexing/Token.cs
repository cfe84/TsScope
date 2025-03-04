namespace ScopeParser.Lexing;

public class Token(TokenType tokenType, object? value, int line, int column)
{
  public TokenType TokenType => tokenType;
  public object? Value => value;
  public int Line => line;
  public int Column => column;

  public override string ToString()
  {
    return $"{TokenType} {Value} {Line} {Column}";
  }

  public override bool Equals(object? obj)
  {
    if (obj is Token token)
    {
      return TokenType == token.TokenType && Value == token.Value && Line == token.Line && Column == token.Column;
    }
    return false;
  }

  public override int GetHashCode()
  {
    return HashCode.Combine(TokenType, Value, Line, Column);
  }
}
