using System;

namespace ScopeParser.Lexing
{
  public class LexError : Exception
  {
    public int Line { get; }
    public int Column { get; }

    public string Problem { get; }

    public LexError(string message, int line, int column) : base(message)
    {
      Line = line;
      Column = column;
      Problem = message;
    }

    public override string ToString()
    {
      return $"{base.ToString()} (Line: {Line}, Column: {Column})";
    }
  }
}