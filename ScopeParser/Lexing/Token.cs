namespace ScopeParser.Lexing;

public class Token(TokenType tokenType, object? value, int line, int column)
{
  public TokenType TokenType => tokenType;
  public int Line => line;
  public int Column => column;
  public bool HasValue => value != null;
  public object? Value => value;
  public T ValueAs<T>()
  {
    if (value == null)
      throw new NullReferenceException($"Tried to access value on {this}");
    if (typeof(T) != value.GetType())
      throw new InvalidCastException($"Tried to cast ${value} of type ${value.GetType().Name} to ${typeof(T).Name}");
    return (T)value;
  }

  public override string ToString()
  {
    var value = Value != null ? $"\"{Value}\"" : "";
    return $"{TokenType} {value} (line {Line}, column {Column})";
  }

  public override bool Equals(object? obj)
  {
    if (obj is Token token)
    {
      var valueType = value != null ? value.GetType() : typeof(object);
      return TokenType == token.TokenType
        && Value == token.Value
        && Line == token.Line
        && Column == token.Column;
    }
    return false;
  }

  public override int GetHashCode()
  {
    return HashCode.Combine(TokenType, value, Line, Column);
  }
}
