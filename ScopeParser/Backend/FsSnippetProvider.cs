namespace ScopeParser.Backend;

public class FsSnippetProvider : ISnippetProvider
{
    string snippetsDirectory;

    public FsSnippetProvider(string snippetsDirectory)
    {
        if (!Directory.Exists(snippetsDirectory))
        {
            throw new DirectoryNotFoundException($"The directory {snippetsDirectory} does not exist in {Directory.GetCurrentDirectory()}");
        }
        snippetsDirectory = Path.GetFullPath(snippetsDirectory);
        this.snippetsDirectory = snippetsDirectory;
    }

    public string GetSnippet(string snippetName, params (string key, string value)[] replacements)
    {
        var fileName = $"{snippetName}.ts";
        var filePath = Path.Combine(snippetsDirectory, fileName);
        var template = File.ReadAllText(filePath);
        foreach (var replacement in replacements)
        {
            var containsKey = template.Contains($"/*%{replacement.key}%*/") ||
                              template.Contains($"__{replacement.key}__");

            if (!containsKey)
            {
                throw new ArgumentException($"The snippet {fileName} does not contain the key {replacement.key}");
            }

            template = template.Replace($"/*%{replacement.key}%*/", replacement.value);
            template = template.Replace($"__{replacement.key}__", replacement.value);
        }
        return template;
    }
}