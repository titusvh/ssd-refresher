using Shouldly;
using Xunit;

namespace SsdRefresh.Core.Tests;

public class VersionInfoTests
{
    [Fact]
    public void VersionInfo_version_shouldNotBeEmpty()
    {
        VersionInfo.Version.ShouldNotBeNullOrWhiteSpace();
    }
}
