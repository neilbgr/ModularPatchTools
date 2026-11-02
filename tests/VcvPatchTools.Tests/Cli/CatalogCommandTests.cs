using VcvPatchTools.Cli;

namespace VcvPatchTools.Tests;

public class CatalogCommandTests
{
    [Theory]
    [InlineData("2.1.0", "2.1.0")]
    [InlineData("2.6.5.rc1", "2.6.5")]
    [InlineData("2", "2.0")]
    [InlineData("v1.2", "0.0")]
    [InlineData(null, "0.0")]
    public void Plugin_versions_compare_by_their_leading_numbers(string? text, string expected) =>
        Assert.Equal(Version.Parse(expected), CatalogCommand.ParseVersion(text));

    [Fact]
    public void A_pre_release_is_newer_than_an_older_release() =>
        Assert.True(CatalogCommand.ParseVersion("2.6.5.rc1") > CatalogCommand.ParseVersion("2.1.0"));
}