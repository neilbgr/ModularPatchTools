using ModularPatchTools.Core.Catalog;
using ModularPatchTools.Core.Patch;
using ModularPatchTools.Diagram;
using ModularPatchTools.Diagram.Analysis;

namespace ModularPatchTools.Tests.Diagram;

public class UserCatalogAnalysisTests
{
    // A private module (1) feeding a VCF (2) from its output 2, and fed on its input 3 by an LFO (3).
    private static readonly PatchDocument patch = new PatchDocument("2.6.6",
        new[] { new PatchModule(1, "MyPlugin", "MyOsc", 0, 0), new PatchModule(2, "Fundamental", "VCF", 10, 0), new PatchModule(3, "Fundamental", "LFO", 20, 0) },
        new[] { new PatchCable(10, new PortRef(1, 2), new PortRef(2, 3), null), new PatchCable(11, new PortRef(3, 0), new PortRef(1, 3), null) });

    [Fact]
    public void An_unknown_module_is_listed_with_the_ports_its_cables_use()
    {
        PatchAnalysis analysis = PatchAnalyzer.CreateDefault().Analyze(patch);

        UncataloguedModule module = Assert.Single(analysis.Uncatalogued);
        Assert.Equal(new UncataloguedModule("MyPlugin/MyOsc", "MyOsc", 4, 3, true, true, false), module);
    }

    [Fact]
    public void A_user_description_names_the_ports_and_sets_the_role()
    {
        UserCatalog user = UserCatalog.Empty.With("MyPlugin/MyOsc",
            new UserModule("My Osc", new[] { "Oscillator" }, Role.Source, new[] { "V/Oct", "FM", "Sync", "Shape CV" }, new[] { "Sine", "Square", "Saw" }));

        PatchAnalysis analysis = PatchAnalyzer.Create(user).Analyze(patch);

        AnalyzedModule osc = analysis.Module(1);
        Assert.True(osc.UserDefined);
        Assert.Equal(Role.Source, osc.Role);
        Assert.Equal("My Osc", osc.Title);
        Assert.Equal("Saw", analysis.Cables.Single(c => c.Cable.Id == 10).FromPort);
        Assert.Equal("Shape CV", analysis.Cables.Single(c => c.Cable.Id == 11).ToPort);
        Assert.Empty(analysis.Uncatalogued);
        Assert.DoesNotContain(analysis.Diagnostics, d => d.Contains("MyOsc") || d.Contains("My Osc"));
        Assert.False(analysis.Module(2).UserDefined);
    }

    [Fact]
    public void No_user_catalog_analyzes_as_before() =>
        Assert.Equal(PatchAnalyzer.CreateDefault().Analyze(patch).Diagnostics, PatchAnalyzer.Create(UserCatalog.Empty).Analyze(patch).Diagnostics);
}