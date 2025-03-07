namespace ScopeParser.Lexing
{
  public enum TokenType
  {
    Declare, Set,
    If, Else, End,
    Error, Warn,

    Identifier, VariableName,
    Equal,

    Select, Extract, From, Where, As, On, Inner, Left, Right, Outer, Join,

    Output, To,

    String, Integer, Float,

    LParen, RParen,

    Star, Comma, SemiColon, Dot,

    TsExpression,

    EndOfFile,
  }
}