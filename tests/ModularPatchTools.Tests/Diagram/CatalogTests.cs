using ModularPatchTools.Core.Catalog;

namespace ModularPatchTools.Tests;

public class CatalogTests
{
    private static readonly PortCatalog catalog = PortCatalog.LoadEmbedded();

    [Theory]
    [InlineData("Fundamental", "ADSR", 4, "Gate")]
    [InlineData("Fundamental", "ADSR", 5, "Retrigger")]
    [InlineData("Bogaudio", "Bogaudio-Mix4", 4, "Channel 2 pan CV")]
    [InlineData("SurgeXTRack", "SurgeXTVCF", 2, "Modulation Signal 1")]
    public void EmbeddedCatalogNamesInputs(string plugin, string model, int index, string expected) =>
        Assert.Equal(expected, catalog.InputName(plugin, model, index));

    [Fact]
    public void EmbeddedCatalogNamesOutputs() => Assert.Equal("1V/octave pitch", catalog.OutputName("Cardinal", "HostMIDI", 0));

    [Fact]
    public void UnknownPortFallsBackToIndex() => Assert.Equal("in#3", catalog.InputName("Nope", "Nothing", 3));

    [Fact]
    public void OverridesKeepGeneratedNameAndTags()
    {
        PortCatalog generated = new PortCatalog(new Dictionary<string, ModuleInfo>
        {
            ["P/M"] = new ModuleInfo("Nice", new[] { "Filter" }, new[] { "a" }, new[] { "b" }),
        });
        PortCatalog overrides = PortCatalog.FromJson("""{ "P/M": { "inputs": ["Left"] } }""");

        ModuleInfo merged = generated.MergedWith(overrides).Get("P", "M");

        Assert.Equal("Nice", merged.Name);
        Assert.Equal(new[] { "Left" }, merged.Inputs);
        Assert.Equal(new[] { "b" }, merged.Outputs);
    }

    [Fact]
    public void RoleFromTagsWhenNotExplicit()
    {
        RoleCatalog roles = RoleCatalog.FromJson("{}");

        (Role role, bool guessed) = roles.Resolve("P", "X", new ModuleInfo(null, new[] { "Polyphonic", "Reverb" }, Array.Empty<string>(), Array.Empty<string>()));

        Assert.Equal(Role.Effect, role);
        Assert.False(guessed);
    }

    [Theory]
    [InlineData("Limiter")]
    [InlineData("Compressor")]
    [InlineData("Dynamics")]
    [InlineData("Lowpass gate")]
    public void DynamicsTagsMakeAModifier(string tag)
    {
        // Bogaudio LMTR (tags Limiter, Dynamics) used to be guessed a controller.
        (Role role, bool guessed) = RoleCatalog.FromJson("{}").Resolve("P", "Lmtr", new ModuleInfo(null, new[] { tag, "Polyphonic" }, Array.Empty<string>(), Array.Empty<string>()));

        Assert.Equal(Role.Modifier, role);
        Assert.False(guessed);
    }

    [Theory]
    [InlineData("Drums")]
    [InlineData("Percussion")]
    [InlineData("drum")]
    public void RackTagAliasesDecideTheRole(string tag)
    {
        // dbRackModules Drums is tagged "Drums", Rack's alias of "Drum": it used to fall through to a guessed controller.
        (Role role, bool guessed) = RoleCatalog.FromJson("{}").Resolve("dbRackModules", "Drums", new ModuleInfo("Drums", new[] { tag }, Array.Empty<string>(), Array.Empty<string>()));

        Assert.Equal(Role.Source, role);
        Assert.False(guessed);
    }

    [Theory]
    [InlineData("Drums", Role.Source)]
    [InlineData("KickDrum", Role.Source)]
    [InlineData("Perc8", Role.Source)]
    [InlineData("DrumSeq", Role.Time)]
    public void DrumNamesAreGuessedAsVoices(string model, Role expected)
    {
        (Role role, bool guessed) = RoleCatalog.FromJson("{}").Resolve("Acme", model, ModuleInfo.Empty);

        Assert.Equal(expected, role);
        Assert.True(guessed);
    }

    [Theory]
    [InlineData("Drums", "Drum")]
    [InlineData("lfo", "Low-frequency oscillator")]
    [InlineData(" VCA ", "Voltage-controlled amplifier")]
    [InlineData("reverb", "Reverb")]
    [InlineData("Made up", "Made up")]
    public void TagsTakeRacksOwnName(string tag, string expected) => Assert.Equal(expected, RackTags.Canonical(tag));

    [Fact]
    public void RoleGuessedFromSlugIsFlagged()
    {
        (Role role, bool guessed) = RoleCatalog.FromJson("{}").Resolve("P", "SuperLFO", ModuleInfo.Empty);

        Assert.Equal(Role.Controller, role);
        Assert.True(guessed);
    }

    [Fact]
    public void PluginNameInModelSlugIsNotAKeyword()
    {
        // "Bogaudio-Switch" used to match "Audio" and be taken for an I/O module.
        (Role role, bool _) = RoleCatalog.FromJson("{}").Resolve("Bogaudio", "Bogaudio-Switch", new ModuleInfo(null, new[] { "Switch" }, Array.Empty<string>(), Array.Empty<string>()));

        Assert.Equal(Role.Controller, role);
    }
}