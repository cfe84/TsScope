namespace ScopeParser.Test;

using Xunit;
using ScopeParser.Lexing;
using FluentAssertions;
using ScopeParser.Ast;
using Moq;
using ScopeParser.Backend;
using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Text.Json;

public class BackendTest
{
    private static string GetToRootDirectory()
    {
        int max = 100;
        while (max-- > 0 && Directory.GetCurrentDirectory() != "/" && !Directory.GetDirectories(Directory.GetCurrentDirectory()).Any(f => f.EndsWith("ScopeParser.Test")))
        {
            var parent = Directory.GetParent(Directory.GetCurrentDirectory());
            Directory.SetCurrentDirectory(parent?.FullName ?? throw new DirectoryNotFoundException("Could not find ScopeParser directory"));
        }
        return Directory.GetCurrentDirectory();
    }

    // Method to dynamically get test input files
    public static IEnumerable<object[]> GetTestData()
    {
        // PrepareData();
        GetToRootDirectory();
        var testFiles = Directory.GetFiles(Path.Combine("Examples", "scripts"), "*.scope");
        foreach (var file in testFiles)
        {
            var fileName = Path.GetFileNameWithoutExtension(file);
            if (fileName.StartsWith("err") || fileName.StartsWith("library_mode") || fileName.StartsWith("params"))
            {
                continue; // Skip error files
            }
            var compiled = Path.Combine("Examples", "compiled", fileName + ".ts");
            var expectedFiles = Directory.GetFiles(Path.Combine("Examples", "expected"), fileName + ".*");
            yield return new object[] { fileName, file, compiled, expectedFiles };
        }
    }

    [Theory(DisplayName = "End to End Tests")]
    [MemberData(nameof(GetTestData))]
    public void TestEndToEnd(string displayName, string inputFile, string compiled, string[] expectedFiles)
    {
        var root = GetToRootDirectory();
        // Compile
        Program.runFile(inputFile, compiled).Should().BeTrue();
        File.Exists(compiled).Should().BeTrue();

        // Run the compiled file

        var relativeCompiled = compiled.Replace("Examples/", "");
        var startInfo = new ProcessStartInfo
        {
            FileName = "node",
            Arguments = $"--import=tsx {relativeCompiled}",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = "Examples"
        };

        using var process = new Process { StartInfo = startInfo };
        process.Start();
        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        var exitCode = process.ExitCode;
        output.Should().BeNullOrEmpty();
        error.Should().BeNullOrEmpty();
        exitCode.Should().Be(0);

        // Check outputs
        foreach (var expectedFile in expectedFiles)
        {
            var outputFile = expectedFile.Replace("/expected", "/outputs");
            File.Exists(outputFile).Should().BeTrue(expectedFile);
            if (expectedFile.EndsWith(".json"))
            {
                var expectedContent = JsonSerializer.Deserialize<List<Dictionary<string, string>>>(File.ReadAllText(expectedFile))!;
                var outputContent = JsonSerializer.Deserialize<List<Dictionary<string, string>>>(File.ReadAllText(outputFile))!;
                outputContent.Should().BeEquivalentTo(expectedContent);
            }
            else if (expectedFile.EndsWith(".csv"))
            {
                var expectedContent = LoadCSV(expectedFile);
                var outputContent = LoadCSV(outputFile);
                outputContent.Should().BeEquivalentTo(expectedContent);
            }
            else
            {
                var expectedContent = File.ReadAllText(expectedFile);
                var outputContent = File.ReadAllText(outputFile);
                outputContent.Should().Be(expectedContent);
            }
        }
    }

    private static List<Dictionary<string, object>> LoadCSV(string filePath)
    {
        var lines = File.ReadAllLines(filePath);
        var headers = lines[0].Split(',').Select(cleanField).ToArray();
        var data = new List<Dictionary<string, object>>();
        for (int i = 1; i < lines.Length; i++)
        {
            var values = lines[i].Split(',');
            var obj = new Dictionary<string, object>();
            for (int j = 0; j < headers.Length; j++)
            {
                obj.Add(headers[j], values[j]);
            }
            data.Add(obj);
        }
        return data;
    }

    private static string cleanField(string field)
    {
        if (field.StartsWith("\"") && field.EndsWith("\""))
        {
            field = field[1..^1];
        }
        if (field.StartsWith("'") && field.EndsWith("'"))
        {
            field = field[1..^1];
        }
        return field;
    }
}