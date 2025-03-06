namespace ScopeParser.Test;

using Xunit;
using ScopeParser.Lexing;
using FluentAssertions;
using ScopeParser.Ast;
using Moq;
using ScopeParser.Backend;

public class BackendTest
{

    [Fact]
    public void TestEmpty()
    {
        // Given
        var statements = new Statement[] { };
        var script = new Script(statements);
        var snippetProvider = new Mock<ISnippetProvider>();
        snippetProvider.Setup(x => x.GetSnippet("script", It.Is<(string key, string value)>(o => o.key == "statements" && o.value == "")))
            .Returns("script");

        // When

        var backend = new TypeScriptBackend(snippetProvider.Object);
        var result = backend.VisitScript(script);

        // Then
        result.Should().Be("script");
    }

    [Fact]
    public void TestRead()
    {
        // // Given
        // var statements = new Statement[] {
        //     new Assignment(new Identifier("x"), new FileSource(new Star(), "file.txt"))
        // };
        // var script = new Script(statements);
        // var snippetProvider = new Mock<ISnippetProvider>();
        // snippetProvider.Setup(x => x.GetSnippet("script", It.Is<(string key, string value)>(o => o.key == "statements" && o.value == ""))).Returns("script");
        // snippetProvider.Setup(x => x.GetSnippet("assignment", It.Is<(string key, string value)>(o => o.key == "statements" && o.value == "x = * file.txt"))).Returns("script");

        // // When

        // var backend = new TypeScriptBackend(snippetProvider.Object);
        // var result = backend.VisitScript(script);

        // // Then
        // result.Should().Be("script");
        // snippetProvider.VerifyNoOtherCalls();
    }
}