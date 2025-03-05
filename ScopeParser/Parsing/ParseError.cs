using ScopeParser.Lexing;

namespace ScopeParser.Parsing
{
    public class ParseError : Exception
    {
        public Token Token { get; }

        public string Problem { get; }

        public ParseError(string message, Token token) : base(message)
        {
            Token = token;
            Problem = message;
        }

        public override string ToString()
        {
            return $"{base.ToString()} (Line: {Token.Line}, Column: {Token.Column})";
        }
    }
}