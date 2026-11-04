using ModularPatchTools.Core.Catalog;
using ModularPatchTools.Web;

namespace ModularPatchTools.Tests;

public class ModuleDraftTests
{
    [Fact]
    public void A_new_description_starts_from_the_catalog_with_the_ports_the_patch_uses()
    {
        ModuleInfo known = new ModuleInfo("Polyphonic LPG", new[] { "VCF", "Lowpass gate" }, Array.Empty<string>(), Array.Empty<string>());

        ModuleDraft draft = new ModuleDraft("NYSTHI/PolyLPG", null, known, 3, 1);

        Assert.Equal("Polyphonic LPG", draft.Name);
        Assert.Null(draft.Role);
        Assert.Equal(new[] { "", "", "" }, draft.Inputs);
        Assert.Single(draft.Outputs);
        Assert.Contains("vcf", draft.Tags);
    }

    [Fact]
    public void Saving_drops_trailing_blank_ports_and_keeps_rack_tag_order()
    {
        ModuleDraft draft = new ModuleDraft("P/X", null, ModuleInfo.Empty, 4, 0) { Name = "  ", Role = Role.Effect };
        draft.Inputs[0] = "In L";
        draft.Inputs[2] = " In R ";
        draft.Tags.Add("Reverb");
        draft.Tags.Add("Delay");

        UserModule module = draft.ToUserModule();

        Assert.Null(module.Name);
        Assert.Equal(new[] { "In L", "", "In R" }, module.Inputs);
        Assert.Equal(new[] { "Delay", "Reverb" }, module.Tags);
        Assert.Equal(Role.Effect, module.Role);
    }

    [Fact]
    public void An_existing_description_wins_over_the_catalog()
    {
        UserModule own = new UserModule("Mine", new[] { "Mixer" }, Role.Mixer, new[] { "A" }, new[] { "Out" });

        ModuleDraft draft = new ModuleDraft("P/X", own, new ModuleInfo("Theirs", new[] { "VCF" }, new[] { "x", "y" }, Array.Empty<string>()), 2, 1);

        Assert.Equal("Mine", draft.Name);
        Assert.Equal(new[] { "A", "" }, draft.Inputs);
        Assert.Equal(new[] { "Mixer" }, draft.Tags);
    }
}