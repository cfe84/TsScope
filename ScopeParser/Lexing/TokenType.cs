namespace ScopeParser.Lexing
{
  public enum TokenType
  {
    Declare, Set,
    If, Else, End,
    Error, Warn,

    Identifier, VariableName,
    Equal,

    Select, Extract, From, Where, As,

    Output, To,

    String, Integer, Float,

    LParen, RParen,

    Star, Comma, SemiColon,

    TsExpression,

    EndOfFile,
  }
}