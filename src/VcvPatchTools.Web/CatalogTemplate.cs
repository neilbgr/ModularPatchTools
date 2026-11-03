using System.Text;
using System.Text.Json;
using VcvPatchTools.Core.Catalog;
using VcvPatchTools.Diagram.Analysis;

namespace VcvPatchTools.Web;

/// <summary>
/// A module catalog file to fill in by hand: the format explained in comments (the reader skips them), with one entry per
/// module of the patch the catalog can't explain, as many ports as its cables use; an example when there is none.
/// It imports back as is (blank names stay unnamed ports).
/// </summary>
public static class CatalogTemplate
{
    private static readonly string[] header =
    {
        "// VCV Patch Tools module catalog: describes modules the embedded catalog doesn't know.",
        "// One entry per module, keyed \"Plugin/Model\": the plugin and model slugs of the patch file (and of plugin.json).",
        "//   name     optional, the module's name as shown in Rack",
        "//   tags     optional, Rack's tags: Oscillator, Filter, Voltage-controlled amplifier, Mixer, Envelope generator, Sequencer…",
        $"//   role     optional, one of {string.Join(", ", Enum.GetNames<Role>())}; left out, the tags decide",
        "//   inputs   port names in the module's port order, as Rack shows them when hovering each port (\"\" leaves one unnamed)",
        "//   outputs  the same, for the outputs",
        "// Lines starting with // are comments.",
    };

    public static string Json(IReadOnlyList<UncataloguedModule> modules, PortCatalog known)
    {
        IEnumerable<(string Key, string? Name, IReadOnlyList<string> Tags, int Inputs, int Outputs)> entries = modules.Count > 0
            ? modules.Select(m =>
            {
                string[] parts = m.Key.Split('/');
                ModuleInfo info = known.Get(parts[0], parts[1]);
                return (m.Key, (string?)(info.Name ?? m.Name), info.Tags, Math.Max(m.Inputs, info.Inputs.Count), Math.Max(m.Outputs, info.Outputs.Count));
            })
            : new[] { ("MyPlugin/MyOsc", (string?)"My Osc", (IReadOnlyList<string>)new[] { "Oscillator" }, 3, 3) };

        StringBuilder json = new StringBuilder();
        foreach (string line in header)
        {
            json.Append(line).Append('\n');
        }
        json.Append("{\n");
        json.Append(string.Join(",\n", entries.Select(e =>
            $"  {Quote(e.Key)}: {{\n" +
            $"    \"name\": {Quote(e.Name ?? "")},\n" +
            $"    \"tags\": [{string.Join(", ", e.Tags.Select(Quote))}],\n" +
            $"    \"inputs\": [{string.Join(", ", Enumerable.Repeat("\"\"", e.Inputs))}],\n" +
            $"    \"outputs\": [{string.Join(", ", Enumerable.Repeat("\"\"", e.Outputs))}]\n" +
            "  }")));
        json.Append("\n}\n");
        return json.ToString();
    }

    private static string Quote(string text) => JsonSerializer.Serialize(text);
}