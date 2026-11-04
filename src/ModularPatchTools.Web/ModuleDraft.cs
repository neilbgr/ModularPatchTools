using ModularPatchTools.Core.Catalog;

namespace ModularPatchTools.Web;

/// <summary>
/// A module description being edited in the Modules tab: starts from the user's own description if there is one,
/// else from what the catalog knows (name, tags, port names) with as many ports as the patch's cables use.
/// </summary>
public sealed class ModuleDraft
{
    public ModuleDraft(string key, UserModule? own, ModuleInfo known, int inputsSeen, int outputsSeen)
    {
        Key = key;
        Name = own?.Name ?? known.Name ?? "";
        Role = own?.Role;
        Tags = new HashSet<string>(own?.Tags ?? known.Tags, StringComparer.OrdinalIgnoreCase);
        Inputs = Padded(own is { Inputs.Count: > 0 } ? own.Inputs : known.Inputs, inputsSeen);
        Outputs = Padded(own is { Outputs.Count: > 0 } ? own.Outputs : known.Outputs, outputsSeen);
    }

    public string Key { get; }

    public string Name { get; set; }

    /// <summary>Null: let the tags (or the name) decide, as for any module.</summary>
    public Role? Role { get; set; }

    public HashSet<string> Tags { get; }

    public List<string> Inputs { get; }

    public List<string> Outputs { get; }

    /// <summary>
    /// The description to save: blank names keep their place (shown "#n"), trailing blank ports are dropped, and tags
    /// keep Rack's order.
    /// </summary>
    public UserModule ToUserModule()
    {
        List<string> tags = RackTags.All.Where(Tags.Contains).Concat(Tags.Where(t => !RackTags.All.Contains(t, StringComparer.OrdinalIgnoreCase))).ToList();
        return new UserModule(Name.Trim().Length > 0 ? Name.Trim() : null, tags, Role, Trimmed(Inputs), Trimmed(Outputs));
    }

    private static List<string> Padded(IReadOnlyList<string> names, int count) =>
        names.Select(n => n.StartsWith('#') ? "" : n).Concat(Enumerable.Repeat("", Math.Max(0, count - names.Count))).ToList();

    private static List<string> Trimmed(List<string> names)
    {
        List<string> trimmed = names.Select(n => n.Trim()).ToList();
        while (trimmed.Count > 0 && trimmed[^1].Length == 0)
        {
            trimmed.RemoveAt(trimmed.Count - 1);
        }
        return trimmed;
    }
}