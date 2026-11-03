using VcvPatchTools.Core.Catalog;
using VcvPatchTools.Diagram.Analysis;
using VcvPatchTools.Web;

namespace VcvPatchTools.Tests;

public class CatalogTemplateTests
{
    [Fact]
    public void The_template_lists_the_patch_s_unknown_modules_and_imports_back()
    {
        UncataloguedModule unknown = new UncataloguedModule("MyPlugin/MyOsc", "MyOsc", 4, 3, true, true, false);

        string json = CatalogTemplate.Json(new[] { unknown }, PortCatalog.FromJson("{}"));
        UserCatalog read = UserCatalog.FromJson(json);

        Assert.Contains("// ", json);
        Assert.Empty(read.Problems);
        UserModule module = read.Modules["MyPlugin/MyOsc"];
        Assert.Equal(4, module.Inputs.Count);
        Assert.Equal(3, module.Outputs.Count);
        Assert.Equal("MyOsc", module.Name);
    }

    [Fact]
    public void Known_names_and_tags_prefill_the_template()
    {
        PortCatalog known = PortCatalog.FromJson("""{ "NYSTHI/PolyLPG": { "name": "Polyphonic LPG", "tags": ["VCF", "VCA"] } }""");

        UserModule module = UserCatalog.FromJson(CatalogTemplate.Json(new[] { new UncataloguedModule("NYSTHI/PolyLPG", "Polyphonic LPG", 2, 1, true, false, true) }, known)).Modules["NYSTHI/PolyLPG"];

        Assert.Equal("Polyphonic LPG", module.Name);
        Assert.Equal(new[] { "VCF", "VCA" }, module.Tags);
    }

    [Fact]
    public void An_unfilled_template_leaves_the_module_unknown()
    {
        UserCatalog unfilled = UserCatalog.FromJson(CatalogTemplate.Json(new[] { new UncataloguedModule("MyPlugin/MyOsc", "MyOsc", 1, 1, true, true, false) }, PortCatalog.FromJson("{}")));
        VcvPatchTools.Core.Patch.PatchDocument patch = new VcvPatchTools.Core.Patch.PatchDocument("2.6.6",
            new[] { new VcvPatchTools.Core.Patch.PatchModule(1, "MyPlugin", "MyOsc", 0, 0), new VcvPatchTools.Core.Patch.PatchModule(2, "Fundamental", "VCF", 10, 0) },
            new[] { new VcvPatchTools.Core.Patch.PatchCable(10, new VcvPatchTools.Core.Patch.PortRef(1, 0), new VcvPatchTools.Core.Patch.PortRef(2, 0), null) });

        Assert.Single(PatchAnalyzer.Create(unfilled).Analyze(patch).Uncatalogued, u => u.PortsUnknown);
    }

    [Fact]
    public void Without_unknown_modules_it_is_an_example()
    {
        UserCatalog read = UserCatalog.FromJson(CatalogTemplate.Json(Array.Empty<UncataloguedModule>(), PortCatalog.FromJson("{}")));

        Assert.True(read.Contains("MyPlugin/MyOsc"));
    }
}