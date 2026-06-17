using System.Diagnostics;
using System.Text;

namespace DeckFlow.Core.Tests;

/// <summary>
/// Regression tripwire for future <c>.editorconfig</c> edits. These fixtures run in CI as part of the
/// existing unfiltered <c>dotnet test DeckFlow.sln</c> flow and are excluded locally with
/// <c>--filter Category!=CarveOutGuard</c> because VSTest is unreliable in WSL. No runtime skip is used.
/// The helper must mirror the Plan 02 gate mode from 50-02-SUMMARY (full <c>dotnet format</c> by default).
/// </summary>
[Trait("Category", "CarveOutGuard")]
public sealed class CarveOutGuardTests
{
    [Fact]
    public void InitAccessor_SurvivesFormatting_ByteIdentical()
    {
        const string fixture = """
            namespace TempProject;

            public sealed record Example
            {
                public string Name { get; init; } = "";
            }
            """;

        AssertByteIdenticalAfterFormatting(AppendFinalNewline(fixture));
    }

    [Fact]
    public void RawStringLiteral_SurvivesFormatting_ByteIdentical()
    {
        const string fixture = """"
            namespace TempProject;

            public static class Example
            {
                public static string Value => """
                alpha
                  beta
                gamma
                """;
            }
            """";

        AssertByteIdenticalAfterFormatting(AppendFinalNewline(fixture));
    }

    [Fact]
    public void OwnLineAttribute_SurvivesFormatting_ByteIdentical()
    {
        const string fixture = """
            using System.Text.Json.Serialization;

            namespace TempProject;

            public sealed class Example
            {
                [JsonPropertyName("name")]
                public string Name { get; set; } = "";
            }
            """;

        AssertByteIdenticalAfterFormatting(AppendFinalNewline(fixture));
    }

    [Fact]
    public void SwitchExpression_SurvivesFormatting_ByteIdentical()
    {
        const string fixture = """
            namespace TempProject;

            public static class Example
            {
                public static string Describe(int value) =>
                    value switch
                    {
                        0 => "zero",
                        1 => "one",
                        _ => "many"
                    };
            }
            """;

        AssertByteIdenticalAfterFormatting(AppendFinalNewline(fixture));
    }

    private static string AppendFinalNewline(string value) => value + "\n";

    private static void AssertByteIdenticalAfterFormatting(string source)
    {
        var expectedBytes = Encoding.UTF8.GetBytes(source);
        var actualBytes = Encoding.UTF8.GetBytes(RunDotnetFormatOnSnippet(source));

        Assert.Equal(expectedBytes, actualBytes);
    }

    private static string RunDotnetFormatOnSnippet(string source)
    {
        var repoRoot = GetRepoRoot();
        var tempRoot = Path.Combine(repoRoot, "artifacts", "carveout-guard", Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(tempRoot);

        try
        {
            var projectPath = Path.Combine(tempRoot, "TempProject.csproj");
            var sourcePath = Path.Combine(tempRoot, "Fixture.cs");
            var editorConfigPath = Path.Combine(tempRoot, ".editorconfig");

            File.WriteAllText(projectPath, CreateProjectFileContents(), new UTF8Encoding(false));
            File.WriteAllText(sourcePath, source, new UTF8Encoding(false));
            File.Copy(Path.Combine(repoRoot, ".editorconfig"), editorConfigPath);

            // Mirror 50-02 exactly: full `dotnet format`, not whitespace-only, because the CI gate enforces
            // full solution formatting and this guard exists to trip on future gate-enforceable config drift.
            var startInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"format \"{projectPath}\"",
                WorkingDirectory = tempRoot,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            };

            using var process = Process.Start(startInfo);
            Assert.NotNull(process);

            var standardOutput = process.StandardOutput.ReadToEnd();
            var standardError = process.StandardError.ReadToEnd();

            process.WaitForExit();

            Assert.True(
                process.ExitCode == 0,
                $"dotnet format failed with exit code {process.ExitCode}.{Environment.NewLine}STDOUT:{Environment.NewLine}{standardOutput}{Environment.NewLine}STDERR:{Environment.NewLine}{standardError}");

            return File.ReadAllText(sourcePath, Encoding.UTF8);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    private static string GetRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, ".editorconfig"))
                && File.Exists(Path.Combine(directory.FullName, "DeckFlow.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate the repository root from the current test base directory.");
    }

    private static string CreateProjectFileContents()
        => """
           <Project Sdk="Microsoft.NET.Sdk">
             <PropertyGroup>
               <TargetFramework>net10.0</TargetFramework>
               <ImplicitUsings>enable</ImplicitUsings>
               <Nullable>enable</Nullable>
             </PropertyGroup>
           </Project>
           """;
}
