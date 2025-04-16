using System.Text.RegularExpressions;
using ScopeParser.Ast;

namespace ScopeParser.Backend;

public class MockSnippetProvider() : ISnippetProvider
{
    public string GetSnippet(string snippetName, params (string key, string value)[] replacements)
    {
        var res = "";
        foreach (var rep in replacements)
        {
            var value = rep.value;
            // Increase indent of value:
            value = "\n  " + string.Join("\n  ", value.Split('\n'));
            res += $"{rep.key} = {value}";
        }
        res += "\n";
        return res;
    }
}