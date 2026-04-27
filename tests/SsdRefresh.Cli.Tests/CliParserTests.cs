using Shouldly;
using Xunit;

namespace SsdRefresh.Cli.Tests;

public sealed class CliParserTests
{
    [Fact]
    public void Parse_missingRequiredArguments_shouldFailSoProgramReturnsExitCode2()
    {
        var parser = new CliParser();
        var result = parser.Parse(["scan"]);

        result.Success.ShouldBeFalse();
        result.Errors.ShouldContain("--manifest is required.");
        result.Errors.ShouldContain("--report is required.");
    }
}
