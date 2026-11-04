using ModularPatchTools.Diagram.Layout;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;

namespace ModularPatchTools.Web;

/// <summary>
/// What the app remembers in the browser's localStorage: the display options, and the intents written for each patch.
/// Stored data may be from an older version of the app, or damaged: whatever can't be read is left out, never an error.
/// </summary>
public static class StoredState
{
    public const string OptionsKey = "vpd-options";

    /// <summary>The user's own module descriptions (<see cref="Core.Catalog.UserCatalog"/> JSON), shared by every patch.</summary>
    public const string UserCatalogKey = "vpd-user-catalog";

    /// <summary>The display options, remembered from one visit (and one patch) to the next.</summary>
    public sealed record Options(IReadOnlySet<string> HiddenLayers, bool ShowIntents, bool PortTabs, bool FunctionNames, bool LibraryLinks, MonitorView Monitors)
    {
        // Scopes and displays drawn as boxes, next to what they watch.
        public static Options Default { get; } = new Options(new HashSet<string>(), false, true, false, true, MonitorView.Modules);

        /// <summary>Whether Cardinal can load each module (Info tab, module sheet). On by default; Rack-only users may turn it off.</summary>
        public bool CardinalSupport { get; init; } = true;
    }

    /// <summary>
    /// Saved with the options. Version 2 made the scope boxes the default: a scope view saved before (with no version) was
    /// then always "hidden", the old default, whether chosen or not, so it is left out and the new default applies.
    /// </summary>
    private const int optionsVersion = 2;

    /// <summary>Saved options, each one that is missing or of the wrong kind left at its default.</summary>
    public static Options ParseOptions(string? json)
    {
        if (Parse(json) is not JsonElement root || root.ValueKind != JsonValueKind.Object)
        {
            return Options.Default;
        }
        bool Flag(string name, bool fallback) =>
            root.TryGetProperty(name, out JsonElement value) && value.ValueKind is JsonValueKind.True or JsonValueKind.False ? value.GetBoolean() : fallback;
        HashSet<string> hidden = root.TryGetProperty(nameof(Options.HiddenLayers), out JsonElement layers) && layers.ValueKind == JsonValueKind.Array
            ? layers.EnumerateArray().Where(l => l.ValueKind == JsonValueKind.String).Select(l => l.GetString()!).Where(DiagramLayout.Layers.Contains).ToHashSet()
            : new HashSet<string>();
        bool current = root.TryGetProperty("Version", out JsonElement version) && version.ValueKind == JsonValueKind.Number
            && version.TryGetInt32(out int saved) && saved >= optionsVersion;
        MonitorView monitors = current && root.TryGetProperty(nameof(Options.Monitors), out JsonElement view) && view.ValueKind == JsonValueKind.Number
            && view.TryGetInt32(out int number) && Enum.IsDefined((MonitorView)number)
            ? (MonitorView)number
            : Options.Default.Monitors;
        return new Options(
            hidden,
            Flag(nameof(Options.ShowIntents), Options.Default.ShowIntents),
            Flag(nameof(Options.PortTabs), Options.Default.PortTabs),
            Flag(nameof(Options.FunctionNames), Options.Default.FunctionNames),
            Flag(nameof(Options.LibraryLinks), Options.Default.LibraryLinks),
            monitors)
        {
            CardinalSupport = Flag(nameof(Options.CardinalSupport), Options.Default.CardinalSupport),
        };
    }

    public static string SerializeOptions(Options options) => JsonSerializer.Serialize(new
    {
        Version = optionsVersion,
        HiddenLayers = options.HiddenLayers.ToArray(),
        options.ShowIntents,
        options.PortTabs,
        options.FunctionNames,
        options.LibraryLinks,
        options.Monitors,
        options.CardinalSupport,
    });

    /// <summary>
    /// Where a patch's intents are kept: by its content, so two different patches with the same file name don't share
    /// them (a patch saved again after a change starts over from the suggestions).
    /// </summary>
    public static string IntentsKey(byte[] patchBytes) => "vpd-intents:" + Convert.ToHexStringLower(SHA256.HashData(patchBytes).AsSpan(0, 8));

    /// <summary>Where intents were kept before: by file name only. Read once, then moved to <see cref="IntentsKey"/>.</summary>
    public static string LegacyIntentsKey(string fileTitle) => "vpd-intents:" + fileTitle;

    /// <summary>Saved intents by cable id; entries that aren't an id and a text are left out.</summary>
    public static Dictionary<long, string> ParseIntents(string? json)
    {
        Dictionary<long, string> intents = new Dictionary<long, string>();
        if (Parse(json) is not JsonElement root || root.ValueKind != JsonValueKind.Object)
        {
            return intents;
        }
        foreach (JsonProperty entry in root.EnumerateObject())
        {
            if (long.TryParse(entry.Name, NumberStyles.Integer, CultureInfo.InvariantCulture, out long cableId) && entry.Value.ValueKind == JsonValueKind.String)
            {
                intents[cableId] = entry.Value.GetString()!;
            }
        }
        return intents;
    }

    public static string SerializeIntents(IReadOnlyDictionary<long, string> intents) => JsonSerializer.Serialize(intents);

    /// <summary>The JSON's root, or null when there is none or it isn't JSON.</summary>
    private static JsonElement? Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }
        try
        {
            using JsonDocument document = JsonDocument.Parse(json);
            return document.RootElement.Clone();
        }
        catch (JsonException)
        {
            return null;
        }
    }
}