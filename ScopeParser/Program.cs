﻿using System.IO;
using ScopeParser.Backend;
using ScopeParser.Lexing;
using ScopeParser.Parsing;

public class Program
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

  public static bool runFile(string input, string output, string snippetDir = "ScopeParser/Backend/TypeScriptSnippets")
  {
    if (!File.Exists(input))
    {
      Console.Error.WriteLine($"File {input} does not exist.");
      return false;
    }
    if (Path.GetExtension(input) != ".scope")
    {
      Console.Error.WriteLine($"File {input} is not a .scope file.");
      return false;
    }
    if (Path.GetExtension(output) == "")
    {
      var file = Path.GetFileNameWithoutExtension(input);
      output = Path.Combine(output, file + ".ts");
    }
    if (Path.GetExtension(output) != ".ts")
    {
      Console.Error.WriteLine($"File {output} is not a .ts file.");
      return false;
    }
    string source = File.ReadAllText(input);
    var snippetProvider = new FsSnippetProvider(snippetDir);
    string? res = run(source, snippetProvider);

    if (res == null)
    {
      Console.Error.WriteLine($"Error while parsing {input}. Code generation failed.");
      return false;
    }

    File.WriteAllText(output, res);
    return true;
  }

  private static void runREPL()
  {
    var snippetProvider = new MockSnippetProvider();
    while (true)
    {
      Console.Write("> ");
      string source = Console.ReadLine() ?? "";
      var res = run(source, snippetProvider);
      if (res != null)
      {
        Console.WriteLine(res);
      }
    }
  }

  private static string? run(string source, ISnippetProvider snippetProvider)
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
        var backend = new TypeScriptBackend(snippetProvider);
        try
        {
          return backend.Visit(script);
        }
        catch (Exception e)
        {
          Console.Error.WriteLine($"Error during code generation: {e.Message}");
          return null;
        }
      }
    }
    catch (LexError e)
    {
      Console.Error.WriteLine($"Syntax error: {e.Problem} at line {e.Line}, column {e.Column}");
      return null;
    }
  }
}
