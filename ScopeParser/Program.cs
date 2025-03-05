using ScopeParser.Lexing;
using ScopeParser.Parsing;

class Program
{
  public static int Main(string[] args)
  {
    if (args.Length > 1)
    {
      Console.WriteLine($"Usage: scope <filename>");
      return -1;
    }
    else if (args.Length == 1)
    {
      runFile(args[0]);
    }
    else
    {
      runREPL();
    }
    return 1;
  }

  private static void runFile(string filename)
  {
    string source = System.IO.File.ReadAllText(filename);
    run(source);
  }

  private static void runREPL()
  {
    while (true)
    {
      Console.Write("> ");
      string source = Console.ReadLine() ?? "";
      run(source);
    }
  }

  private static void run(string source)
  {
    var lexer = new Lexer(source);
    try
    {
      var tokens = lexer.Scan().ToList();
      var parser = new Parser(tokens);
      var script = parser.parse();
      if (parser.HasErrors)
      {
        foreach (var error in parser.Errors)
        {
          Console.Error.WriteLine($"Parsing error: {error}");
        }
      }
      else
      {
        Console.WriteLine(script);
      }
    }
    catch (LexError e)
    {
      Console.Error.WriteLine($"Syntax error: {e.Problem} at line {e.Line}, column {e.Column}");
    }
  }
}
