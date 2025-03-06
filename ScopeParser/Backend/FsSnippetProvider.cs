namespace ScopeParser.Backend;

public class FsSnippetProvider(string snippetsDirectory) : ISnippetProvider
{
    public string GetSnippet(string snippetName, params (string key, string value)[] replacements)
    {
        var fileName = $"{snippetName}.ts";
        var filePath = Path.Combine(snippetsDirectory, fileName);
        var template = File.ReadAllText(filePath);
        foreach (var replacement in replacements)
        {
            template = template.Replace($"/*%{replacement.key}%*/", replacement.value);
            template = template.Replace($"__{replacement.key}__", replacement.value);
        }
        return template;
    }
}