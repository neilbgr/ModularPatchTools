using ModularPatchTools.Core.Catalog;

namespace ModularPatchTools.Tests;

public class UserCatalogTests
{
    private const string json = """
        {
          // a comment, as people write by hand
          "MyPlugin/MyOsc": { "name": "My Osc", "tags": ["Oscillator", ""], "role": "source", "inputs": ["V/Oct", "", "Sync"], "outputs": ["Saw"] },
          "MyPlugin/Bad": { "role": "Wobbler" },
          "NoSlash": {},
          "MyPlugin/NotAnObject": 3,
        }
        """;

    [Fact]
    public void Reads_what_it_can_and_says_what_it_left_out()
    {
        UserCatalog catalog = UserCatalog.FromJson(json);

        Assert.Equal(new[] { "MyPlugin/Bad", "MyPlugin/MyOsc" }, catalog.Modules.Keys);
        UserModule osc = catalog.Modules["MyPlugin/MyOsc"];
        Assert.Equal(("My Osc", Role.Source), (osc.Name, osc.Role));
        Assert.Equal(new[] { "Oscillator" }, osc.Tags);
        Assert.Equal(new[] { "V/Oct", "", "Sync" }, osc.Inputs);
        Assert.Equal(new[] { "Saw" }, osc.Outputs);
        Assert.Null(catalog.Modules["MyPlugin/Bad"].Role);
        Assert.Equal(3, catalog.Problems.Count);
        Assert.Contains(catalog.Problems, p => p.Contains("Wobbler"));
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void Nothing_stored_is_an_empty_catalog(string? stored) => Assert.Equal(0, UserCatalog.FromJson(stored).Count);

    [Fact]
    public void A_file_that_is_not_json_is_a_problem_not_an_error()
    {
        UserCatalog catalog = UserCatalog.FromJson("{ nope");

        Assert.Equal(0, catalog.Count);
        Assert.Single(catalog.Problems);
    }

    [Fact]
    public void Writing_and_reading_back_keeps_every_module()
    {
        UserCatalog catalog = UserCatalog.FromJson(json);

        UserCatalog again = UserCatalog.FromJson(catalog.ToJson());

        Assert.Equal(catalog.ToJson(), again.ToJson());
        Assert.Empty(again.Problems);
    }

    [Fact]
    public void Merging_lets_the_newer_catalog_win()
    {
        UserCatalog first = UserCatalog.Empty.With("P/A", new UserModule("Old", Array.Empty<string>(), null, Array.Empty<string>(), Array.Empty<string>()))
            .With("P/B", new UserModule("B", Array.Empty<string>(), null, Array.Empty<string>(), Array.Empty<string>()));
        UserCatalog second = UserCatalog.Empty.With("P/A", new UserModule("New", Array.Empty<string>(), Role.Mixer, Array.Empty<string>(), Array.Empty<string>()));

        UserCatalog merged = first.MergedWith(second);

        Assert.Equal("New", merged.Modules["P/A"].Name);
        Assert.Equal("B", merged.Modules["P/B"].Name);
        Assert.Equal(Role.Mixer, merged.Roles["P/A"]);
        Assert.Equal(0, merged.Without("P/A").Without("P/B").Count);
    }

    [Fact]
    public void User_roles_win_over_the_embedded_ones()
    {
        RoleCatalog roles = RoleCatalog.FromJson("""{ "P/X": "Mixer" }""").MergedWith(new Dictionary<string, Role> { ["P/X"] = Role.Effect });

        Assert.Equal(Role.Effect, roles.Resolve("P", "X", ModuleInfo.Empty).Role);
    }
}