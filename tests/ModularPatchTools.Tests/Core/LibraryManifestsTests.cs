using ModularPatchTools.Core.Catalog;

namespace ModularPatchTools.Tests;

public class LibraryManifestsTests
{
    private const string manifest = """
        {
          "slug": "NYSTHI", "version": "2.4.23", "license": "proprietary",
          "modules": [
            { "slug": "PolyLPG", "name": "Polyphonic LPG", "tags": ["VCF", "VCA", "Lowpass gate"] },
            { "slug": "Blank", "tags": [] },
            { "name": "no slug" }
          ]
        }
        """;

    [Fact]
    public void Each_module_gets_its_name_and_tags_but_no_ports()
    {
        Dictionary<string, ModuleInfo> modules = LibraryManifests.Modules(manifest).ToDictionary();

        Assert.Equal(new[] { "NYSTHI/Blank", "NYSTHI/PolyLPG" }, modules.Keys.Order());
        Assert.Equal("Polyphonic LPG", modules["NYSTHI/PolyLPG"].Name);
        Assert.Equal(new[] { "VCF", "VCA", "Lowpass gate" }, modules["NYSTHI/PolyLPG"].Tags);
        Assert.Empty(modules["NYSTHI/PolyLPG"].Inputs);
        Assert.Null(modules["NYSTHI/Blank"].Name);
    }

    [Fact]
    public void The_written_file_reads_back_as_a_port_catalog()
    {
        PortCatalog catalog = PortCatalog.FromJson(LibraryManifests.ToJson(LibraryManifests.Modules(manifest)));

        Assert.Equal("Polyphonic LPG", catalog.Get("NYSTHI", "PolyLPG").Name);
        Assert.Equal("in#0", catalog.InputName("NYSTHI", "PolyLPG", 0));
    }

    [Fact]
    public void The_embedded_catalog_knows_closed_source_modules_by_name()
    {
        ModuleInfo info = PortCatalog.LoadEmbedded().Get("NYSTHI", "PolyLPG");

        Assert.Equal("Polyphonic LPG", info.Name);
        Assert.Contains("VCF", info.Tags);
    }

    [Fact]
    public void Only_published_plugins_get_a_library_link()
    {
        Assert.True(VcvLibrary.IsListed("NYSTHI"));
        Assert.True(VcvLibrary.IsListed("AmbientModules"));
        Assert.False(VcvLibrary.IsListed("reminiscence-of-the-unattained"));
        Assert.False(VcvLibrary.IsListed("Cardinal"));
    }
}