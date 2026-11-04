using ModularPatchTools.Core.Catalog;
using ModularPatchTools.Core.Patch;
using ModularPatchTools.Diagram;
using ModularPatchTools.Diagram.Analysis;
using ModularPatchTools.Diagram.Layout;
using System.IO.Compression;

namespace ModularPatchTools.Tests;

/// <summary>A user catalog comes from anyone: what a hostile file can and can't do.</summary>
public class UserCatalogSafetyTests
{
    [Fact]
    public void Control_bidi_and_line_separator_characters_are_dropped()
    {
        UserCatalog catalog = UserCatalog.FromJson("""
            { "P/X": { "name": "Saw\u001b[31m\u202Eevil\u2028next\r\nline", "inputs": ["V/Oct\u0000", "🎹 Keys"] } }
            """);

        UserModule module = catalog.Modules["P/X"];
        Assert.Equal("Saw[31meviln​extline".Replace("\u200b", ""), module.Name);
        Assert.Equal(new[] { "V/Oct", "🎹 Keys" }, module.Inputs);
    }

    [Fact]
    public void A_key_hiding_control_characters_is_left_out_and_not_echoed_raw()
    {
        UserCatalog catalog = UserCatalog.FromJson("""{ "P/X\u001b]0;pwned\u0007": {} }""");

        Assert.Equal(0, catalog.Count);
        Assert.DoesNotContain(catalog.Problems, p => p.Any(char.IsControl));
    }

    [Fact]
    public void Sizes_are_capped()
    {
        string ports = string.Join(",", Enumerable.Range(0, 1000).Select(i => $"\"{new string('x', 500)}\""));
        UserCatalog catalog = UserCatalog.FromJson($$"""{ "P/X": { "inputs": [{{ports}}] } }""");

        UserModule module = catalog.Modules["P/X"];
        Assert.Equal(UserCatalog.MaxPorts, module.Inputs.Count);
        Assert.All(module.Inputs, p => Assert.True(p.Length <= UserCatalog.MaxText));
        Assert.Contains("larger", UserCatalog.FromJson("{\"P/X\": {\"name\": \"" + new string('a', UserCatalog.MaxJsonLength) + "\"}}").Problems.Single());
    }

    [Fact]
    public void Deep_nesting_is_refused_without_throwing()
    {
        string deep = "{\"P/X\": {\"name\": " + new string('[', 100) + new string(']', 100) + "}}";

        Assert.Single(UserCatalog.FromJson(deep).Problems);
    }

    [Fact]
    public void Type_hints_are_plain_data()
    {
        UserCatalog catalog = UserCatalog.FromJson("""{ "$type": "System.Diagnostics.Process", "P/X": { "$type": "System.IO.File", "name": "ok" } }""");

        Assert.Equal("ok", catalog.Modules["P/X"].Name);
        Assert.Contains(catalog.Problems, p => p.Contains("$type"));
    }

    [Theory]
    [InlineData("</text><script>alert(1)</script>")]
    [InlineData("\" onload=\"alert(1)")]
    [InlineData("a\"]; click n_m1 href \"javascript:alert(1)\"")]
    public void Names_are_escaped_in_every_export(string hostile)
    {
        UserCatalog user = UserCatalog.Empty.With("P/X", new UserModule(hostile, Array.Empty<string>(), Role.Source, new[] { hostile }, new[] { hostile }));
        PatchDocument patch = new PatchDocument("2.6.6",
            new[] { new PatchModule(1, "P", "X", 0, 0), new PatchModule(2, "Fundamental", "VCF", 10, 0) },
            new[] { new PatchCable(10, new PortRef(1, 0), new PortRef(2, 0), null) });
        DiagramLayout layout = PatchDiagram.Layout(PatchAnalyzer.Create(user).Analyze(patch), "t", PatchDiagram.GroupKeys(PatchAnalyzer.Create(user).Analyze(patch)));

        string html = PatchDiagram.Export(layout, DiagramFormat.Html);
        string svg = PatchDiagram.Export(layout, DiagramFormat.Svg);
        string mermaid = PatchDiagram.Export(layout, DiagramFormat.Mermaid);
        string dot = PatchDiagram.Export(layout, DiagramFormat.Dot);

        Assert.DoesNotContain("<script>alert", html);
        Assert.DoesNotContain("\" onload=\"", svg);
        // The text stays inside its quoted label: no statement of its own, no link.
        Assert.DoesNotContain("href \"javascript", mermaid);
        Assert.DoesNotContain("\"]; click", mermaid);
        Assert.DoesNotContain("\" onload=\"", dot);
    }

    [Fact]
    public void A_zip_bomb_stops_at_the_budget()
    {
        using MemoryStream buffer = new MemoryStream();
        using (ZipArchive zip = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            using Stream entry = zip.CreateEntry("src/huge.cpp", CompressionLevel.SmallestSize).Open();
            byte[] zeros = new byte[1024 * 1024];
            for (int i = 0; i < (PluginSources.MaxSourceBytes / zeros.Length) + 2; i++)
            {
                entry.Write(zeros);
            }
        }

        PluginScan scan = PluginSources.Scan(new[] { new PluginFile("bomb.zip", buffer.ToArray()) });

        Assert.Contains(scan.Problems, p => p.Contains("MB of sources"));
    }
}