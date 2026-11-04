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

    [Theory]
    [InlineData("Merge", Role.Router)]
    [InlineData("Split", Role.Router)]
    [InlineData("Sum", Role.Router)]
    [InlineData("Mult", Role.Router)]
    [InlineData("Compare", Role.Controller)]
    [InlineData("Gates", Role.Time)]
    [InlineData("Push", Role.Performance)]
    [InlineData("Rescale", Role.Controller)]
    [InlineData("MidSide", Role.Modifier)]
    public void VcvFundamentalModulesAreNotGuessed(string model, Role expected)
    {
        // Tagged only "Utility" / "Polyphonic": a forum user's Merge came out as a guessed controller.
        (Role role, bool guessed) = RoleCatalog.LoadEmbedded().Resolve("Fundamental", model, PortCatalog.LoadEmbedded().Get("Fundamental", model));

        Assert.Equal(expected, role);
        Assert.False(guessed);
    }

    [Theory]
    [InlineData("Split polyphonic input to 16 output channels", Role.Router)]
    [InlineData("Gate to Trigger Converter", Role.Time)]
    [InlineData("16 voice polyphonic chord generator", Role.Pitch)]
    [InlineData("A Precise Voltage-Source with Display.", Role.Controller)]
    [InlineData("Filtered Noise Generator", Role.Source)]
    [InlineData("Polyphonic per-channel offset and scale", Role.Controller)]
    [InlineData("Additional scales for Quant.", Role.Pitch)]
    [InlineData("Utility for converting mouse-wheel movement into triggers", Role.Performance)]
    [InlineData("Stereo Audio Levelmeter", Role.Monitor)]
    [InlineData("BPM to delay/hz calculator helper", Role.Time)]
    public void LibraryDescriptionsPointToARole(string description, Role expected) => Assert.Equal(expected, DescriptionRoles.Guess(description));

    [Theory]
    [InlineData("Breaks polyphonic signals out to individual normalled output/input pairs")]
    [InlineData("Blank panel for CV funk.")]
    [InlineData("Particle animation generator")]
    [InlineData("")]
    public void VagueDescriptionsGiveNoRole(string description) => Assert.Null(DescriptionRoles.Guess(description));

    [Fact]
    public void LibraryDescriptionComesBelowTheTagsAndAboveTheName()
    {
        RoleCatalog catalog = new RoleCatalog(new Dictionary<string, Role>(), new Dictionary<string, Role> { ["P/Thing"] = Role.Router });
        ModuleInfo utility = new ModuleInfo(null, new[] { "Utility" }, Array.Empty<string>(), Array.Empty<string>());

        Assert.Equal((Role.Router, RoleSource.Description), catalog.ResolveWithSource("P", "Thing", utility));
        Assert.True(catalog.Resolve("P", "Thing", utility).Guessed);
        Assert.Equal((Role.Effect, RoleSource.Tags), catalog.ResolveWithSource("P", "Thing", new ModuleInfo(null, new[] { "Reverb" }, Array.Empty<string>(), Array.Empty<string>())));
    }

    [Theory]
    [InlineData("AutoScale", Role.Controller)]
    [InlineData("CVConverter", Role.Controller)]
    [InlineData("MultiPan", Role.Modifier)]
    [InlineData("PolyMuter8", Role.Router)]
    [InlineData("Merge8", Role.Router)]
    public void ShortNameKeywordsOnlyStartAWord(string model, Role expected) =>
        // "AutoScale" holds "osc", "CVConverter" holds "vco", "MultiPan" starts with "Mult": none of them is meant.
        Assert.Equal(expected, RoleCatalog.FromJson("{}").Resolve("Acme", model, ModuleInfo.Empty).Role);

    [Fact]
    public void TagsPackedInOneStringEachCount() => Assert.Equal(Role.Mixer, RoleCatalog.FromTags(new[] { "Mixer, Utilities" }));

    [Fact]
    public void DescribedRolesSkipTaggedAndExplicitModules()
    {
        string manifest = """
            { "slug": "P", "modules": [
              { "slug": "Splitter", "description": "Splits a polyphonic input into monophonic outputs", "tags": ["Utility"] },
              { "slug": "Verb", "description": "Splits the sound into a reverb", "tags": ["Reverb"] },
              { "slug": "Named", "description": "Merges channels", "tags": ["Utility"] },
              { "slug": "Mystery", "description": "Something nobody can tell", "tags": ["Utility"] } ] }
            """;
        Dictionary<string, Role> roles = LibraryManifests.DescribedRoles(new[] { manifest }, RoleCatalog.FromJson("""{ "P/Named": "Time" }"""));

        Assert.Equal(new Dictionary<string, Role> { ["P/Splitter"] = Role.Router }, roles);
    }

    [Fact]
    public void EmbeddedCatalogKnowsLibraryDescriptions() =>
        // 23volts Merge8: "8 voice polyphonic merger", tagged only Polyphonic/Utility.
        Assert.Equal((Role.Router, RoleSource.Description), RoleCatalog.LoadEmbedded().ResolveWithSource("23volts", "Merge8", PortCatalog.LoadEmbedded().Get("23volts", "Merge8")));

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