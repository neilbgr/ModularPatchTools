using ModularPatchTools.Core;

namespace ModularPatchTools.Tests;

public class ProductVersionTests
{
    [Fact]
    public void The_commit_is_shortened_to_eight_digits() => Assert.Equal("0.1.0 (d6a0afb1)", ProductVersion.Format("0.1.0+d6a0afb1f2e3d4c5b6a79881726354a4b3c2d1e0"));

    [Fact]
    public void A_version_without_commit_is_shown_as_is() => Assert.Equal("0.1.0-rc1", ProductVersion.Format("0.1.0-rc1"));

    [Fact]
    public void The_built_version_starts_with_the_project_version() => Assert.StartsWith(typeof(ProductVersion).Assembly.GetName().Version!.ToString(3), ProductVersion.Display);

    [Theory]
    [InlineData("1.2.0+49592e1abcdef", "1.2.0", "49592e1abcdef")]
    [InlineData("1.2.0", "1.2.0", null)]
    [InlineData("1.2.0+", "1.2.0", null)]
    public void Version_and_commit_are_split(string informational, string version, string? commit)
    {
        Assert.Equal(version, ProductVersion.VersionOf(informational));
        Assert.Equal(commit, ProductVersion.CommitOf(informational));
    }

    [Fact]
    public void The_release_date_is_the_changelog_s_for_this_version()
    {
        string? dir = AppContext.BaseDirectory;
        while (dir is not null && !File.Exists(Path.Combine(dir, "CHANGELOG.md")))
        {
            dir = Path.GetDirectoryName(dir);
        }
        Assert.NotNull(dir);
        System.Text.RegularExpressions.Match dated = System.Text.RegularExpressions.Regex.Match(
            File.ReadAllText(Path.Combine(dir, "CHANGELOG.md")), $@"## \[{System.Text.RegularExpressions.Regex.Escape(ProductVersion.Version)}\] - (\d{{4}}-\d{{2}}-\d{{2}})");

        Assert.Equal(dated.Success ? dated.Groups[1].Value : null, ProductVersion.ReleaseDate);
    }
}