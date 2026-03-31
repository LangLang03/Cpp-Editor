using System.Text;

namespace C__Editor;

internal interface ICompilerCommandBuilder
{
    ToolchainFamily Family { get; }

    IReadOnlyList<string> BuildArguments(IReadOnlyList<string> sourceFilePaths, string outputExecutablePath, string compilerArguments);
}

internal static class CompilerCommandBuilderFactory
{
    private static readonly IReadOnlyDictionary<ToolchainFamily, ICompilerCommandBuilder> Builders =
        new Dictionary<ToolchainFamily, ICompilerCommandBuilder>
        {
            [ToolchainFamily.Msvc] = new MsvcCompilerCommandBuilder(),
            [ToolchainFamily.GnuLike] = new GnuLikeCompilerCommandBuilder()
        };

    internal static ICompilerCommandBuilder Get(ToolchainFamily family)
    {
        return Builders[family];
    }
}

internal sealed class MsvcCompilerCommandBuilder : ICompilerCommandBuilder
{
    public ToolchainFamily Family => ToolchainFamily.Msvc;

    public IReadOnlyList<string> BuildArguments(IReadOnlyList<string> sourceFilePaths, string outputExecutablePath, string compilerArguments)
    {
        var arguments = CommandLineArgumentParser.Parse(compilerArguments).ToList();
        if (!arguments.Any(arg => arg.Equals("/nologo", StringComparison.OrdinalIgnoreCase)))
        {
            arguments.Add("/nologo");
        }

        foreach (var sourceFilePath in sourceFilePaths)
        {
            if (!string.IsNullOrWhiteSpace(sourceFilePath))
            {
                arguments.Add(sourceFilePath);
            }
        }

        arguments.Add($"/Fe:{outputExecutablePath}");
        return arguments;
    }
}

internal sealed class GnuLikeCompilerCommandBuilder : ICompilerCommandBuilder
{
    public ToolchainFamily Family => ToolchainFamily.GnuLike;

    public IReadOnlyList<string> BuildArguments(IReadOnlyList<string> sourceFilePaths, string outputExecutablePath, string compilerArguments)
    {
        var arguments = CommandLineArgumentParser.Parse(compilerArguments).ToList();
        foreach (var sourceFilePath in sourceFilePaths)
        {
            if (!string.IsNullOrWhiteSpace(sourceFilePath))
            {
                arguments.Add(sourceFilePath);
            }
        }

        arguments.Add("-o");
        arguments.Add(outputExecutablePath);
        return arguments;
    }
}

internal static class CommandLineArgumentParser
{
    internal static IEnumerable<string> Parse(string? argumentsText)
    {
        if (string.IsNullOrWhiteSpace(argumentsText))
        {
            yield break;
        }

        var builder = new StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < argumentsText.Length; i++)
        {
            var current = argumentsText[i];

            if (current == '"')
            {
                inQuotes = !inQuotes;
                continue;
            }

            if (!inQuotes && char.IsWhiteSpace(current))
            {
                if (builder.Length > 0)
                {
                    yield return builder.ToString();
                    builder.Clear();
                }

                continue;
            }

            if (current == '\\' &&
                i + 1 < argumentsText.Length &&
                argumentsText[i + 1] == '"')
            {
                builder.Append('"');
                i++;
                continue;
            }

            builder.Append(current);
        }

        if (builder.Length > 0)
        {
            yield return builder.ToString();
        }
    }
}
