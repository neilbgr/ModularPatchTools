using VcvPatchTools.Core.Patch;
using VcvPatchTools.Diagram.Analysis;

namespace VcvPatchTools.Tests.Diagram;

public class WirelessLinksTests
{
    // VCO (1) → Bay Input (2) port 3 … Bay Output (3, source 2) port 3 → VCF (4) input 0.
    private static readonly PatchModule vco = new PatchModule(1, "Fundamental", "VCO", 0, 0);
    private static readonly PatchModule bayIn = new PatchModule(2, "Venom", "BayInput", 10, 0);
    private static readonly PatchModule bayOut = new PatchModule(3, "Venom", "BayOutput", 0, 1) { LinkedSourceId = 2 };
    private static readonly PatchModule vcf = new PatchModule(4, "Fundamental", "VCF", 10, 1);

    [Fact]
    public void A_bay_output_cable_starts_from_what_feeds_its_bay_input()
    {
        PatchDocument patch = new PatchDocument("2.6.6", new[] { vco, bayIn, bayOut, vcf }, new[]
        {
            new PatchCable(10, new PortRef(1, 2), new PortRef(2, 3), "#ff5252"),
            new PatchCable(11, new PortRef(3, 3), new PortRef(4, 0), "#52beff"),
        });

        PatchCable cable = Assert.Single(WirelessLinks.Bypass(patch).Cables);

        Assert.Equal(new PatchCable(11, new PortRef(1, 2), new PortRef(4, 0), "#52beff"), cable);
    }

    [Fact]
    public void An_unplugged_source_port_carries_nothing()
    {
        PatchDocument patch = new PatchDocument("2.6.6", new[] { vco, bayIn, bayOut, vcf }, new[]
        {
            new PatchCable(10, new PortRef(1, 2), new PortRef(2, 0), null),
            new PatchCable(11, new PortRef(3, 3), new PortRef(4, 0), null),
        });

        Assert.Empty(WirelessLinks.Bypass(patch).Cables);
    }

    [Fact]
    public void A_bay_without_source_keeps_its_cables()
    {
        PatchModule unlinked = bayOut with { LinkedSourceId = null };
        PatchDocument patch = new PatchDocument("2.6.6", new[] { vco, bayIn, unlinked, vcf }, new[]
        {
            new PatchCable(10, new PortRef(1, 2), new PortRef(2, 3), null),
            new PatchCable(11, new PortRef(3, 3), new PortRef(4, 0), null),
        });

        Assert.Same(patch, WirelessLinks.Bypass(patch));
    }

    [Fact]
    public void A_bay_norm_falls_back_to_its_own_input()
    {
        PatchModule norm = new PatchModule(3, "Venom", "BayNorm", 0, 1) { LinkedSourceId = 2 };
        PatchModule lfo = new PatchModule(5, "Fundamental", "LFO", 20, 1);
        PatchDocument patch = new PatchDocument("2.6.6", new[] { vco, bayIn, norm, vcf, lfo }, new[]
        {
            new PatchCable(10, new PortRef(1, 2), new PortRef(2, 0), null),
            new PatchCable(12, new PortRef(5, 0), new PortRef(3, 1), null),
            new PatchCable(13, new PortRef(3, 0), new PortRef(4, 0), null),
            new PatchCable(14, new PortRef(3, 1), new PortRef(4, 1), null),
        });

        IReadOnlyList<PatchCable> cables = WirelessLinks.Bypass(patch).Cables;

        Assert.Equal(new[] { (13L, 1L, 2), (14L, 5L, 0) }, cables.Select(c => (c.Id, c.From.ModuleId, c.From.PortId)));
    }

    [Fact]
    public void The_analysis_draws_the_direct_cable_and_keeps_the_saved_patch()
    {
        PatchDocument patch = new PatchDocument("2.6.6", new[] { vco, bayIn, bayOut, vcf }, new[]
        {
            new PatchCable(10, new PortRef(1, 2), new PortRef(2, 3), null),
            new PatchCable(11, new PortRef(3, 3), new PortRef(4, 0), null),
        });

        PatchAnalysis analysis = PatchAnalyzer.CreateDefault().Analyze(patch);

        Assert.Equal(new long[] { 1, 4 }, analysis.Modules.Select(m => m.Module.Id).Order());
        Assert.Equal(1, Assert.Single(analysis.Cables).Cable.From.ModuleId);
        Assert.Equal(2, analysis.Patch.Cables.Count);
    }
}