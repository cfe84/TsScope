namespace ScopeParser.Lexing
{
  public enum TokenType
  {
    Declare, Set,
    If, Else, End,
    Error, Warn,

    Identifier, Param,

    Equal,

    Select, Extract, From, Where, As, On, Inner, Left, Right, Outer, Join,

    Output, To, Import, Export,

    String, Integer, Decimal, Boolean,

    LParen, RParen, LBracket, RBracket,

    Star, Comma, SemiColon, Dot, Colon, At,

    TsExpression,

    EndOfFile,
  }
}