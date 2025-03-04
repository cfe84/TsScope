namespace ScopeParser.Lexing
{
  public enum TokenType
  {
    Declare, Set,
    If, Else, End,
    Error, Warn,

    Identifier, VariableName,
    Equal,

    Select, From, Stream, Where, As,

    String, Integer, Float,

    LParen, RParen,

    Star, Comma, SemiColon,
    EndOfFile,
  }
}