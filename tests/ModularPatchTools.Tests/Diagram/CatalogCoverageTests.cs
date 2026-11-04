using ModularPatchTools.Core.Catalog;
using ModularPatchTools.Core.Patch;
using ModularPatchTools.Diagram.Analysis;

namespace ModularPatchTools.Tests.Diagram;

/// <summary>The modules a forum user's patch ("buchla_248t_emulation_Ciani_03") showed as unknown or guessed.</summary>
public class CatalogCoverageTests
{
    private static PatchAnalysis Analyze(params (string Plugin, string Model)[] modules)
    {
        // Each module fed by a VCO, so every one of them is shown.
        List<PatchModule> all = modules.Select((m, i) => new PatchModule(i + 2, m.Plugin, m.Model, i * 10, 0)).Prepend(new PatchModule(1, "Fundamental", "VCO", 0, 1)).ToList();
        List<PatchCable> cables = all.Skip(1).Select(m => new PatchCable(m.Id, new PortRef(1, 0), new PortRef(m.Id, 0), null)).ToList();
        return PatchAnalyzer.CreateDefault().Analyze(new PatchDocument("2.6.6", all, cables));
    }

    [Theory]
    [InlineData("Fundamental", "Process", Role.Controller)]
    [InlineData("HoyerHoppes", "phase_driven_multihit_envelope", Role.Controller)]
    [InlineData("Bogaudio", "Bogaudio-Lmtr", Role.Modifier)]
    [InlineData("NYSTHI", "PolyLPG", Role.Modifier)]
    [InlineData("NYSTHI", "b208_envelope", Role.Controller)]
    [InlineData("FLAG-Prism-Of-Perception", "Prism-Of-Perception", Role.Effect)]
    public void Published_modules_are_no_longer_guessed(string plugin, string model, Role role)
    {
        PatchAnalysis analysis = Analyze((plugin, model));

        Assert.Equal(role, analysis.Module(2).Role);
        Assert.DoesNotContain(analysis.Diagnostics, d => d.Contains("guessed"));
    }

    [Fact]
    public void Closed_source_modules_are_listed_for_a_report()
    {
        PatchAnalysis analysis = Analyze(("NYSTHI", "PolyLPG"), ("Fundamental", "Process"));

        Assert.Equal(new[] { "NYSTHI/PolyLPG" }, analysis.MissingPortNames);
        Assert.Contains("Polyphonic LPG: NYSTHI/PolyLPG has no readable sources: ports shown by number", analysis.Diagnostics);
    }

    [Fact]
    public void A_private_plugin_is_said_to_be_unpublished_and_not_reported()
    {
        PatchAnalysis analysis = Analyze(("reminiscence-of-the-unattained", "Marf248"));

        Assert.Empty(analysis.MissingPortNames);
        string diagnostic = Assert.Single(analysis.Diagnostics);
        Assert.Contains("is not in the VCV Library (private or unpublished plugin): ports shown by number, role guessed as", diagnostic);
        Assert.DoesNotContain("roles.json", diagnostic);
    }
}