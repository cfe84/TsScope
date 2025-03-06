namespace ScopeParser.Backend
{
    public interface ISnippetProvider
    {
        string GetSnippet(string snippetName, params (string key, string value)[] replacements);
    }
}