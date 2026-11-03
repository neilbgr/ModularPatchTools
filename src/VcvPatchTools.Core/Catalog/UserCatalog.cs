using System.Text.Json;
using System.Text.Json.Nodes;

namespace VcvPatchTools.Core.Catalog;

/// <summary>A module described by its user: anything left out (null name, no ports, no role) stays as the catalog has it.</summary>
public sealed record UserModule(string? Name, IReadOnlyList<string> Tags, Role? Role, IReadOnlyList<string> Inputs, IReadOnlyList<string> Outputs)
{
    public ModuleInfo Info => new ModuleInfo(Name, Tags, Inputs, Outputs);
}

/// <summary>
/// Modules a user describes for themselves (their own plugin, one not published yet), on top of the embedded catalog:
/// same layout as catalog/ports.overrides.json, plus an optional "role".
/// <code>{ "MyPlugin/MyOsc": { "name": "My Osc", "tags": ["Oscillator"], "role": "Source", "inputs": ["V/Oct"], "outputs": ["Saw"] } }</code>
/// Reading never throws on content: what can't be used is left out and said in <see cref="Problems"/>.
/// The file comes from anyone (shared, imported, stored by another page of the same site), so it is bounded and cleaned
/// on the way in, whatever the source: sizes are capped, and control, line-separator and bidirectional-override characters
/// are dropped from every text (they could break a statement of an exported .dot / .mmd, or disguise a name).
/// Nothing in it is ever run or used as a type name: it only ends up as escaped text.
/// </summary>
public sealed class UserCatalog
{
    /// <summary>The largest catalog file read, in characters.</summary>
    public const int MaxJsonLength = 1024 * 1024;
    public const int MaxModules = 5000;
    public const int MaxPorts = 256;
    public const int MaxTags = 64;
    public const int MaxText = 200;

    private static readonly JsonDocumentOptions documentOptions = new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true, MaxDepth = 8 };
    private static readonly JsonSerializerOptions writeOptions = new JsonSerializerOptions { WriteIndented = true };

    private readonly SortedDictionary<string, UserModule> modules;

    public UserCatalog(IEnumerable<KeyValuePair<string, UserModule>> modules, IReadOnlyList<string>? problems = null)
    {
        this.modules = new SortedDictionary<string, UserModule>(StringComparer.Ordinal);
        List<string> notes = (problems ?? Array.Empty<string>()).ToList();
        foreach ((string key, UserModule module) in modules)
        {
            string cleanKey = Clean(key);
            if (!IsKey(cleanKey))
            {
                notes.Add($"\"{Clean(key)}\": not a Plugin/Model key, left out.");
                continue;
            }
            if (this.modules.Count >= MaxModules && !this.modules.ContainsKey(cleanKey))
            {
                notes.Add($"More than {MaxModules} modules: the rest is left out.");
                break;
            }
            this.modules[cleanKey] = Cleaned(module);
        }
        Problems = notes;
    }

    public static UserCatalog Empty { get; } = new UserCatalog(Array.Empty<KeyValuePair<string, UserModule>>());

    public IReadOnlyDictionary<string, UserModule> Modules => modules;

    /// <summary>What reading the file left out, one line each.</summary>
    public IReadOnlyList<string> Problems { get; }

    public int Count => modules.Count;

    public bool Contains(string key) => modules.ContainsKey(key);

    /// <summary>Names, tags and ports, to merge over the embedded port catalog.</summary>
    public PortCatalog Ports => new PortCatalog(modules.ToDictionary(kv => kv.Key, kv => kv.Value.Info));

    /// <summary>The roles the user set, to merge over the embedded roles.</summary>
    public IReadOnlyDictionary<string, Role> Roles =>
        modules.Where(kv => kv.Value.Role is not null).ToDictionary(kv => kv.Key, kv => kv.Value.Role!.Value);

    /// <summary>A "Plugin/Model" key: both parts present, one slash, no control character, of a sensible length.</summary>
    public static bool IsKey(string key) => key.Length <= MaxText && key == Clean(key)
        && key.Split('/') is [string plugin, string model] && plugin.Trim().Length > 0 && model.Trim().Length > 0;

    /// <summary>
    /// Text as it may be shown and exported: no control characters (Cc), no format characters (Cf: bidirectional overrides,
    /// zero-width characters), no line or paragraph separators (Zl, Zp), trimmed, at most <see cref="MaxText"/> characters.
    /// </summary>
    public static string Clean(string text)
    {
        System.Text.StringBuilder kept = new System.Text.StringBuilder(Math.Min(text.Length, MaxText + 1));
        for (int i = 0; i < text.Length && kept.Length <= MaxText; i++)
        {
            char c = text[i];
            // Characters beyond the BMP (emoji…) come as a pair of surrogates: kept whole, a lone half dropped.
            if (char.IsHighSurrogate(c) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]))
            {
                kept.Append(c).Append(text[++i]);
                continue;
            }
            if (char.GetUnicodeCategory(c) is not (System.Globalization.UnicodeCategory.Control or System.Globalization.UnicodeCategory.Format
                or System.Globalization.UnicodeCategory.LineSeparator or System.Globalization.UnicodeCategory.ParagraphSeparator
                or System.Globalization.UnicodeCategory.Surrogate))
            {
                kept.Append(c);
            }
        }
        string trimmed = kept.ToString().Trim();
        return trimmed.Length <= MaxText ? trimmed : trimmed[..MaxText].TrimEnd();
    }

    /// <summary>A message safe to print in a terminal: without control or format characters (escape sequences), at any length.</summary>
    public static string Printable(string text) => new string(text.Where(c => char.GetUnicodeCategory(c) is not (System.Globalization.UnicodeCategory.Control
        or System.Globalization.UnicodeCategory.Format or System.Globalization.UnicodeCategory.LineSeparator or System.Globalization.UnicodeCategory.ParagraphSeparator)).ToArray());

    private static UserModule Cleaned(UserModule module) => new UserModule(
        module.Name is string name && Clean(name) is { Length: > 0 } cleanName ? cleanName : null,
        module.Tags.Select(Clean).Where(t => t.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).Take(MaxTags).ToList(),
        module.Role is Role role && Enum.IsDefined(role) ? role : null,
        module.Inputs.Take(MaxPorts).Select(Clean).ToList(),
        module.Outputs.Take(MaxPorts).Select(Clean).ToList());

    public UserCatalog With(string key, UserModule module) => IsKey(key)
        ? new UserCatalog(modules.Where(kv => kv.Key != key).Append(new KeyValuePair<string, UserModule>(key, module)))
        : throw new ArgumentException($"\"{key}\" is not a Plugin/Model key.", nameof(key));

    public UserCatalog Without(string key) => new UserCatalog(modules.Where(kv => kv.Key != key));

    /// <summary>This catalog with every module of <paramref name="other"/> added; theirs win on the same key.</summary>
    public UserCatalog MergedWith(UserCatalog other) =>
        new UserCatalog(modules.Where(kv => !other.Contains(kv.Key)).Concat(other.modules), Problems.Concat(other.Problems).ToList());

    public static UserCatalog FromJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return Empty;
        }
        if (json.Length > MaxJsonLength)
        {
            return new UserCatalog(Array.Empty<KeyValuePair<string, UserModule>>(), new[] { $"The file is larger than {MaxJsonLength / 1024} KB: not read." });
        }
        JsonNode? root;
        try
        {
            root = JsonNode.Parse(json, documentOptions: documentOptions);
        }
        catch (JsonException ex)
        {
            return new UserCatalog(Array.Empty<KeyValuePair<string, UserModule>>(), new[] { $"Not a JSON file: {ex.Message}" });
        }
        if (root is not JsonObject entries)
        {
            return new UserCatalog(Array.Empty<KeyValuePair<string, UserModule>>(), new[] { "The file must hold one object, keyed by \"Plugin/Model\"." });
        }

        List<KeyValuePair<string, UserModule>> modules = new List<KeyValuePair<string, UserModule>>();
        List<string> problems = new List<string>();
        foreach ((string key, JsonNode? value) in entries)
        {
            // Echoed cleaned: a message may end up in a terminal, where control characters would be escape sequences.
            if (!IsKey(key))
            {
                problems.Add($"\"{Clean(key)}\": not a Plugin/Model key, left out.");
                continue;
            }
            if (value is not JsonObject entry)
            {
                problems.Add($"{key}: not an object, left out.");
                continue;
            }
            Role? role = null;
            if (Text(entry["role"]) is string roleText)
            {
                if (Enum.TryParse(roleText, ignoreCase: true, out Role parsed) && Enum.IsDefined(parsed))
                {
                    role = parsed;
                }
                else
                {
                    problems.Add($"{key}: unknown role \"{Clean(roleText)}\" ({string.Join(", ", Enum.GetNames<Role>())}), left to the catalog.");
                }
            }
            modules.Add(new KeyValuePair<string, UserModule>(key, new UserModule(Text(entry["name"]), Texts(entry["tags"]).Where(t => t.Length > 0).ToList(), role, Texts(entry["inputs"]), Texts(entry["outputs"]))));
        }
        return new UserCatalog(modules, problems);
    }

    public string ToJson()
    {
        JsonObject root = new JsonObject();
        foreach ((string key, UserModule module) in modules)
        {
            JsonObject entry = new JsonObject();
            if (module.Name is string name)
            {
                entry["name"] = name;
            }
            if (module.Tags.Count > 0)
            {
                entry["tags"] = JsonList(module.Tags);
            }
            if (module.Role is Role role)
            {
                entry["role"] = role.ToString();
            }
            if (module.Inputs.Count > 0)
            {
                entry["inputs"] = JsonList(module.Inputs);
            }
            if (module.Outputs.Count > 0)
            {
                entry["outputs"] = JsonList(module.Outputs);
            }
            root[key] = entry;
        }
        return root.ToJsonString(writeOptions);
    }

    private static JsonArray JsonList(IEnumerable<string> items) => new JsonArray(items.Select(i => (JsonNode?)JsonValue.Create(i)).ToArray());

    private static string? Text(JsonNode? node) => node is JsonValue value && value.TryGetValue(out string? text) && text.Trim().Length > 0 ? text.Trim() : null;

    // A port left blank keeps its place: it shows as "#n".
    private static List<string> Texts(JsonNode? node) =>
        node is JsonArray array ? array.Select(n => n is JsonValue v && v.TryGetValue(out string? s) ? s.Trim() : "").ToList() : new List<string>();
}