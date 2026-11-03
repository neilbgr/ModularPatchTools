using VcvPatchTools.Cli;
using VcvPatchTools.Core.Catalog;

namespace VcvPatchTools.Tests;

public class CatalogCommandTests
{
    [Theory]
    [InlineData("2.1.0", "2.1.0")]
    [InlineData("2.6.5.rc1", "2.6.5")]
    [InlineData("2", "2.0")]
    [InlineData("v1.2", "0.0")]
    [InlineData(null, "0.0")]
    public void Plugin_versions_compare_by_their_leading_numbers(string? text, string expected) =>
        Assert.Equal(Version.Parse(expected), CatalogCommand.ParseVersion(text));

    [Fact]
    public void A_pre_release_is_newer_than_an_older_release() =>
        Assert.True(CatalogCommand.ParseVersion("2.6.5.rc1") > CatalogCommand.ParseVersion("2.1.0"));

    [Theory]
    [InlineData("MyPlugin", "MyPlugin")]
    [InlineData("../../etc/passwd", "etc_passwd")]
    [InlineData("C:\\Windows\\x", "C__Windows_x")]
    [InlineData("///", "plugin")]
    public void The_default_output_name_cannot_leave_the_folder(string slug, string expected) =>
        Assert.Equal(expected, CatalogCommand.SafeFileName(slug));

    [Fact]
    public void Scan_writes_a_user_catalog_from_a_plugin_folder()
    {
        string dir = Path.Combine(Path.GetTempPath(), $"vcvpatch-scan-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(dir, "src"));
        try
        {
            File.WriteAllText(Path.Combine(dir, "plugin.json"), """{ "slug": "MyPlugin", "modules": [ { "slug": "MyOsc", "name": "My Osc", "tags": ["Oscillator"] } ] }""");
            File.WriteAllText(Path.Combine(dir, "src", "MyOsc.cpp"), """
                struct MyOsc : Module {
                    enum InputId { PITCH_INPUT, INPUTS_LEN };
                    enum OutputId { SAW_OUTPUT, OUTPUTS_LEN };
                    MyOsc() { config(0, INPUTS_LEN, OUTPUTS_LEN, 0); configInput(PITCH_INPUT, "V/Oct"); configOutput(SAW_OUTPUT, "Saw"); }
                };
                Model* modelMyOsc = createModel<MyOsc, ModuleWidget>("MyOsc");
                """);
            string output = Path.Combine(dir, "out.json");

            Assert.Equal(0, CatalogCommand.Scan(dir, output));

            UserModule osc = UserCatalog.FromJson(File.ReadAllText(output)).Modules["MyPlugin/MyOsc"];
            Assert.Equal(new[] { "V/Oct" }, osc.Inputs);
            Assert.Equal(new[] { "Saw" }, osc.Outputs);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}