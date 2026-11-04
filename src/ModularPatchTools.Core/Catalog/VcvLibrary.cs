namespace ModularPatchTools.Core.Catalog;

/// <summary>
/// Links to a module's page and panel screenshot on library.vcvrack.com, built from its plugin and model slugs.
/// Nothing is fetched here: the browser loads a screenshot only when asked, and caches it.
/// </summary>
public static class VcvLibrary
{
    private const string site = "https://library.vcvrack.com";

    /// <summary>Plugins that only exist in Cardinal: the Library has no page for their modules.</summary>
    private static readonly HashSet<string> unlisted = new HashSet<string>(StringComparer.Ordinal) { "Cardinal" };

    /// <summary>Slugs of the plugins the Library publishes (catalog/library-plugins.json, from its manifests).</summary>
    private static readonly Lazy<HashSet<string>> listed = new Lazy<HashSet<string>>(() =>
        System.Text.Json.JsonSerializer.Deserialize<HashSet<string>>(PortCatalog.ReadResource("catalog.library-plugins.json") ?? "[]") ?? new HashSet<string>());

    /// <summary>
    /// True when the Library has a page for the plugin's modules: not for Cardinal's own modules, nor for a private or
    /// not yet published plugin (its link would only show "not in library"). Without the embedded list, any other plugin.
    /// </summary>
    public static bool IsListed(string plugin) =>
        plugin.Length > 0 && !unlisted.Contains(plugin) && (listed.Value.Count == 0 || listed.Value.Contains(plugin));

    /// <summary>"plugin/model", the key the renderers carry to build both links.</summary>
    public static string Key(string plugin, string model) => $"{plugin}/{model}";

    public static string PageUrl(string key) => $"{site}/{Escape(key)}";

    /// <param name="size">Width in pixels the Library serves: 100, 200 or 400.</param>
    public static string ScreenshotUrl(string key, int size) => $"{site}/screenshots/{size}/{Escape(key)}.webp";

    private static string Escape(string key) => string.Join("/", key.Split('/').Select(Uri.EscapeDataString));
}