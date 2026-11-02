using System.Text.Json;
using System.Text.Json.Nodes;

namespace VcvPatchTools.Core.Catalog;

/// <summary>
/// Module names and tags from the VCV Library's plugin manifests (github.com/VCVRack/library, manifests/*.json): the
/// Library lists every published plugin, closed-source and commercial ones too, so a module whose sources the catalog
/// can't read still gets its name and tags, hence its role and function. Manifests don't name ports: those stay "#n".
/// Generated into catalog/library.json by "vcvpatch catalog library", only for modules the port catalog lacks.
/// </summary>
public static class LibraryManifests
{
    private static readonly JsonDocumentOptions documentOptions = new JsonDocumentOptions
    {
        CommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    /// <summary>"Plugin/Model" → name and tags of every module of one manifest (none when it isn't one).</summary>
    public static IEnumerable<KeyValuePair<string, ModuleInfo>> Modules(string manifestJson)
    {
        if (JsonNode.Parse(manifestJson, documentOptions: documentOptions) is not JsonObject manifest
            || Text(manifest["slug"]) is not string plugin || manifest["modules"] is not JsonArray modules)
        {
            yield break;
        }
        foreach (JsonObject module in modules.OfType<JsonObject>())
        {
            if (Text(module["slug"]) is string model)
            {
                List<string> tags = (module["tags"] as JsonArray ?? new JsonArray()).Select(Text).OfType<string>().ToList();
                yield return new KeyValuePair<string, ModuleInfo>($"{plugin}/{model}", new ModuleInfo(Text(module["name"]), tags, Array.Empty<string>(), Array.Empty<string>()));
            }
        }
    }

    /// <summary>One line per module, name and tags only: the file stays small enough to embed in the web app.</summary>
    public static string ToJson(IEnumerable<KeyValuePair<string, ModuleInfo>> modules)
    {
        IEnumerable<string> lines = modules.OrderBy(kv => kv.Key, StringComparer.Ordinal).Select(kv =>
        {
            JsonObject entry = new JsonObject();
            if (kv.Value.Name is string name)
            {
                entry["name"] = name;
            }
            entry["tags"] = new JsonArray(kv.Value.Tags.Select(t => (JsonNode?)JsonValue.Create(t)).ToArray());
            return $"  {JsonSerializer.Serialize(kv.Key)}: {entry.ToJsonString()}";
        });
        return "{\n" + string.Join(",\n", lines) + "\n}\n";
    }

    private static string? Text(JsonNode? node) => node is JsonValue value && value.TryGetValue(out string? text) && text.Length > 0 ? text : null;
}