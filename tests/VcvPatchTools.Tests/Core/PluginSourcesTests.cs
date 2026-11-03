using System.IO.Compression;
using System.Text;
using VcvPatchTools.Core.Catalog;

namespace VcvPatchTools.Tests;

public class PluginSourcesTests
{
    private const string manifest = """{ "slug": "MyPlugin", "version": "2.0.0", "modules": [ { "slug": "MyOsc", "name": "My Osc", "tags": ["Oscillator"] } ] }""";

    private const string source = """
        #include "plugin.hpp"
        struct MyOsc : Module {
            enum InputId { PITCH_INPUT, SYNC_INPUT, INPUTS_LEN };
            enum OutputId { SAW_OUTPUT, OUTPUTS_LEN };
            MyOsc() {
                config(0, INPUTS_LEN, OUTPUTS_LEN, 0);
                configInput(PITCH_INPUT, "V/Oct");
                configInput(SYNC_INPUT, "Sync");
                configOutput(SAW_OUTPUT, "Saw");
            }
        };
        Model* modelMyOsc = createModel<MyOsc, ModuleWidget>("MyOsc");
        """;

    private static PluginFile Text(string path, string text) => new PluginFile(path, Encoding.UTF8.GetBytes(text));

    [Fact]
    public void Files_picked_one_by_one_name_the_ports()
    {
        PluginScan scan = PluginSources.Scan(new[] { Text("plugin.json", manifest), Text("MyOsc.cpp", source) });

        Assert.Equal("MyPlugin", scan.Plugin);
        ModuleInfo osc = scan.Modules["MyPlugin/MyOsc"];
        Assert.Equal(new[] { "V/Oct", "Sync" }, osc.Inputs);
        Assert.Equal(new[] { "Saw" }, osc.Outputs);
        Assert.Equal("My Osc", osc.Name);
    }

    [Fact]
    public void A_repository_zip_reads_the_src_folder_next_to_plugin_json()
    {
        using MemoryStream buffer = new MemoryStream();
        using (ZipArchive zip = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            void Add(string path, string text)
            {
                using StreamWriter writer = new StreamWriter(zip.CreateEntry(path).Open());
                writer.Write(text);
            }
            Add("MyPlugin-main/plugin.json", manifest);
            Add("MyPlugin-main/src/MyOsc.cpp", source);
            Add("MyPlugin-main/res/MyOsc.svg", "<svg/>");
            Add("MyPlugin-main/dep/other/plugin.json", """{ "slug": "Other", "modules": [] }""");
        }

        PluginScan scan = PluginSources.Scan(new[] { new PluginFile("MyPlugin-main.zip", buffer.ToArray()) });

        Assert.Equal("MyPlugin", scan.Plugin);
        Assert.Equal(new[] { "V/Oct", "Sync" }, scan.Modules["MyPlugin/MyOsc"].Inputs);
    }

    [Fact]
    public void Without_plugin_json_nothing_is_read()
    {
        PluginScan scan = PluginSources.Scan(new[] { Text("MyOsc.cpp", source), Text("notes.txt", "hello") });

        Assert.Null(scan.Plugin);
        Assert.Empty(scan.Modules);
        Assert.Contains(scan.Problems, p => p.Contains("notes.txt"));
        Assert.Contains(scan.Problems, p => p.Contains("No plugin.json"));
    }

    [Fact]
    public void A_damaged_zip_is_a_problem_not_an_error()
    {
        PluginScan scan = PluginSources.Scan(new[] { new PluginFile("broken.zip", new byte[] { 1, 2, 3 }) });

        Assert.Contains(scan.Problems, p => p.Contains("broken.zip"));
    }

    [Fact]
    public void The_scan_becomes_user_descriptions_with_the_role_left_to_the_tags()
    {
        UserCatalog catalog = PluginSources.ToUserCatalog(PluginSources.Scan(new[] { Text("plugin.json", manifest), Text("MyOsc.cpp", source) }));

        Assert.Null(catalog.Modules["MyPlugin/MyOsc"].Role);
        Assert.Equal(new[] { "Saw" }, catalog.Modules["MyPlugin/MyOsc"].Outputs);
    }
}