using System.IO;
using ScopeParser.Backend;
using ScopeParser.Lexing;
using ScopeParser.Parsing;

class Program
{
  public static int Main(string[] args)
  {
    var argList = parseArgs(args);
    if (args.Length == 0 || argList.ContainsKey("interact") || argList.ContainsKey("repl"))
    {
      runREPL();
    }
    else if (!argList.ContainsKey("input"))
    {
      Console.WriteLine("TsScope Compiler");
      Console.WriteLine("Usage: scope <input> [<output>] [options]");
      Console.WriteLine("Options:");
      Console.WriteLine("  --help       Show this help message and exit");
      Console.WriteLine("  --version    Show version information and exit");
      Console.WriteLine("  --interactive Run in interactive mode");
      Console.WriteLine("  --repl       Run in interactive mode");
      return -1;
    }
    var inputFile = argList["input"];
    var outputFile = argList.ContainsKey("output") ? argList["output"] : inputFile.Replace(".scope", ".ts");
    runFile(inputFile, outputFile);
    return 1;
  }

  private static Dictionary<string, string> parseArgs(string[] args)
  {
    var positionalArguments = new string[] { "input", "output" };
    var booleans = new string[] { "help", "version", "interactive", "repl" };
    var argList = new Dictionary<string, string>();
    var argsAsList = args.ToList();
    while (argsAsList.Count > 0)
    {
      var arg = argsAsList[0];
      argsAsList.RemoveAt(0);
      if (arg.StartsWith("--"))
      {
        arg = arg.Replace("--", "");
        arg = arg.Replace("-", "");
        if (booleans.Contains(arg))
        {
          argList.Add(arg, "true");
          continue;
        }
        else if (argsAsList.Count == 0)
        {
          throw new ArgumentException($"Missing value for argument {arg}");
        }
        var value = argsAsList[0];
        argsAsList.RemoveAt(0);
        argList.Add(arg, value);
      }
      else
      {
        if (positionalArguments.Length == 0)
        {
          throw new ArgumentException($"Unexpected argument {arg}");
        }
        var name = positionalArguments[0];
        positionalArguments = positionalArguments[1..];
        argList.Add(name, arg);
      }
    }
    return argList;
  }

  private static void runFile(string input, string output)
  {
    if (!File.Exists(input))
    {
      Console.Error.WriteLine($"File {input} does not exist.");
      return;
    }
    if (Path.GetExtension(input) != ".scope")
    {
      Console.Error.WriteLine($"File {input} is not a .scope file.");
      return;
    }
    if (Path.GetExtension(output) != ".ts")
    {
      Console.Error.WriteLine($"File {output} is not a .ts file.");
      return;
    }
    string source = File.ReadAllText(input);
    string? res = run(source);
    if (res != null)
    {
      File.WriteAllText(output, res);
    }
  }

  private static void runREPL()
  {
    while (true)
    {
      Console.Write("> ");
      string source = Console.ReadLine() ?? "";
      var res = run(source);
      if (res != null)
      {
        Console.WriteLine(res);
      }
    }
  }

  private static string? run(string source)
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
        return null;
      }
      else
      {
        var snippetProvider = new FsSnippetProvider("ScopeParser/Backend/TypeScriptSnippets");
        var backend = new TypeScriptBackend(snippetProvider);
        return backend.Visit(script);
      }
    }
    catch (LexError e)
    {
      Console.Error.WriteLine($"Syntax error: {e.Problem} at line {e.Line}, column {e.Column}");
      return null;
    }
  }
}
