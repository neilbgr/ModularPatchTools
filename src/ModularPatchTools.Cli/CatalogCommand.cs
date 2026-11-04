using ModularPatchTools.Core.Catalog;

namespace ModularPatchTools.Cli;

/// <summary>
/// "catalog build": scans every plugin under one or more plugin folders (Cardinal's "plugins", plugins fetched apart)
/// and writes catalog/ports.json. Rack's own Core plugin (MIDI, audio interfaces) lives next to Cardinal's, in src/Rack:
/// its manifest is Core.json.
/// </summary>
internal static class CatalogCommand
{
    public static int Build(IReadOnlyList<string> pluginsDirs, string outputPath, string? reportPath)
    {
        if (pluginsDirs.FirstOrDefault(d => !Directory.Exists(d)) is string missing)
        {
            Console.Error.WriteLine($"Plugins folder not found: {missing}");
            return 1;
        }

        Dictionary<string, ModuleInfo> all = new Dictionary<string, ModuleInfo>(StringComparer.Ordinal);
        List<string> problems = new List<string>();

        List<(string Manifest, string Sources)> plugins = pluginsDirs.SelectMany(pluginsDir =>
        {
            string rackDir = Path.Combine(pluginsDir, "..", "src", "Rack");
            return Directory.GetDirectories(pluginsDir)
                .OrderBy(d => d, StringComparer.Ordinal)
                .Select(d => (Path.Combine(d, "plugin.json"), Path.Combine(d, "src")))
                .Prepend((Path.Combine(rackDir, "Core.json"), Path.Combine(rackDir, "src", "core")));
        }).ToList();
        // Two folders can hold the same plugin (Cardinal has mscHack 1.0.2 and, for one CardinalMini module, 2.0.0):
        // the newest one is scanned, the one the VCV Library has.
        Dictionary<string, (Version Version, string Manifest)> newest = new Dictionary<string, (Version, string)>(StringComparer.Ordinal);
        foreach ((string manifestPath, string _) in plugins.Where(p => File.Exists(p.Manifest)))
        {
            if (ManifestIdentity(manifestPath) is (string slug, Version version) && (!newest.TryGetValue(slug, out (Version Version, string Manifest) best) || version > best.Version))
            {
                newest[slug] = (version, manifestPath);
            }
        }

        foreach ((string manifestPath, string sourceDir) in plugins)
        {
            string pluginDir = Path.GetDirectoryName(manifestPath)!;
            if (!File.Exists(manifestPath) || !Directory.Exists(sourceDir))
            {
                continue;
            }
            if (ManifestIdentity(manifestPath) is (string duplicate, Version older) && newest[duplicate].Manifest != manifestPath)
            {
                problems.Add($"{duplicate}: {older} in {Path.GetRelativePath(".", pluginDir)} skipped, {newest[duplicate].Version} in {Path.GetRelativePath(".", Path.GetDirectoryName(newest[duplicate].Manifest)!)} is newer");
                continue;
            }

            List<SourceFile> sources = Directory.EnumerateFiles(sourceDir, "*.*", SearchOption.AllDirectories)
                .Where(p => IsSource(p) || IsHeader(p))
                .Select(p => new SourceFile(p, File.ReadAllText(p)))
                .ToList();

            try
            {
                List<SourceFile> headers = IncludedHeaders(pluginDir, sourceDir, sources);
                ScanResult scan = SourceScanner.ScanPlugin(File.ReadAllText(manifestPath), sources, headers, EngineSources(pluginDir, sourceDir, headers));
                foreach ((string key, ModuleInfo info) in scan.Modules)
                {
                    all[key] = info;
                }
                problems.AddRange(scan.Problems);
            }
            catch (Exception ex) when (ex is System.Text.Json.JsonException or KeyNotFoundException)
            {
                problems.Add($"{Path.GetFileName(pluginDir)}: unreadable plugin.json ({ex.Message})");
            }
        }

        // A plugin no longer in the source tree (removed from the Cardinal fork…) keeps its last scanned ports: patches
        // made in Rack still use it.
        if (File.Exists(outputPath))
        {
            HashSet<string> scanned = all.Keys.Select(PluginOf).ToHashSet(StringComparer.Ordinal);
            List<IGrouping<string, KeyValuePair<string, ModuleInfo>>> kept = PortCatalog.FromJson(File.ReadAllText(outputPath)).Modules
                .Where(kv => !scanned.Contains(PluginOf(kv.Key))).GroupBy(kv => PluginOf(kv.Key)).ToList();
            foreach (IGrouping<string, KeyValuePair<string, ModuleInfo>> plugin in kept)
            {
                foreach ((string key, ModuleInfo info) in plugin)
                {
                    all[key] = info;
                }
                problems.Add($"{plugin.Key}: no sources found, {plugin.Count()} modules kept from the previous catalog");
            }
        }

        // Modules the hand-written overrides name (ports known only at run time…) aren't problems.
        string overridesPath = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(outputPath))!, "ports.overrides.json");
        if (File.Exists(overridesPath))
        {
            HashSet<string> overridden = PortCatalog.FromJson(File.ReadAllText(overridesPath)).Modules.Keys.ToHashSet(StringComparer.Ordinal);
            problems.RemoveAll(problem => problem.IndexOf(':') is int colon and > 0 && overridden.Contains(problem[..colon]));
        }

        File.WriteAllText(outputPath, new PortCatalog(all).ToJson());
        int resolved = all.Values.Count(m => m.Inputs.Count > 0 || m.Outputs.Count > 0);
        Console.WriteLine($"{all.Count} modules, {resolved} with ports resolved, {problems.Count} problems → {outputPath}");

        if (reportPath is not null)
        {
            File.WriteAllLines(reportPath, problems);
            Console.WriteLine($"Problems report → {reportPath}");
        }
        return 0;
    }

    /// <summary>Headers outside the source folder (a bundled engine, a library) that the sources include by name.</summary>
    private static List<SourceFile> IncludedHeaders(string pluginDir, string sourceDir, List<SourceFile> sources)
    {
        HashSet<string> included = sources
            .SelectMany(s => System.Text.RegularExpressions.Regex.Matches(s.Text, @"#\s*include\s*[<""]([^>""]+)[>""]"))
            .Select(m => Path.GetFileName(m.Groups[1].Value))
            .ToHashSet(StringComparer.Ordinal);
        string ownSources = Path.GetFullPath(sourceDir) + Path.DirectorySeparatorChar;
        return Directory.EnumerateFiles(pluginDir, "*.h*", SearchOption.AllDirectories)
            .Where(p => IsHeader(p)
                && !Path.GetFullPath(p).StartsWith(ownSources, StringComparison.Ordinal) && included.Contains(Path.GetFileName(p)))
            .OrderBy(p => p, StringComparer.Ordinal)
            .Select(p => new SourceFile(p, File.ReadAllText(p)))
            .ToList();
    }

    /// <summary>
    /// A bundled engine's implementation, two levels deep: the .cpp next to each header the sources include, the headers
    /// those include and their .cpp (Surge XT: dsp/Oscillator.h → Oscillator.cpp → oscillators/ClassicOscillator.h and .cpp).
    /// </summary>
    private static List<SourceFile> EngineSources(string pluginDir, string sourceDir, List<SourceFile> headers)
    {
        string ownSources = Path.GetFullPath(sourceDir) + Path.DirectorySeparatorChar;
        Dictionary<string, List<string>> outsideByName = Directory.EnumerateFiles(pluginDir, "*.*", SearchOption.AllDirectories)
            .Where(p => IsHeader(p) && !Path.GetFullPath(p).StartsWith(ownSources, StringComparison.Ordinal))
            .GroupBy(Path.GetFileName)
            .ToDictionary(g => g.Key!, g => g.ToList(), StringComparer.Ordinal);
        static string? Implementation(string header) =>
            Path.ChangeExtension(header, ".cpp") is string cpp && File.Exists(cpp) ? cpp : null;

        HashSet<string> engine = new HashSet<string>(StringComparer.Ordinal);
        List<string> level = headers.Select(h => h.Path).Select(Implementation).OfType<string>().ToList();
        for (int depth = 0; depth < 2 && level.Count > 0; depth++)
        {
            List<string> next = new List<string>();
            foreach (string cpp in level.Where(engine.Add))
            {
                foreach (System.Text.RegularExpressions.Match include in System.Text.RegularExpressions.Regex.Matches(File.ReadAllText(cpp), @"#\s*include\s*""([^""]+)"""))
                {
                    foreach (string header in outsideByName.GetValueOrDefault(Path.GetFileName(include.Groups[1].Value)) ?? new List<string>())
                    {
                        if (engine.Add(header) && Implementation(header) is string implementation)
                        {
                            next.Add(implementation);
                        }
                    }
                }
            }
            level = next;
        }
        engine.UnionWith(level);
        return engine.OrderBy(p => p, StringComparer.Ordinal).Select(p => new SourceFile(p, File.ReadAllText(p))).ToList();
    }

    /// <summary>Slug and version of a plugin.json (null when unreadable).</summary>
    private static (string Slug, Version Version)? ManifestIdentity(string manifestPath)
    {
        try
        {
            using System.Text.Json.JsonDocument manifest = System.Text.Json.JsonDocument.Parse(File.ReadAllText(manifestPath), new System.Text.Json.JsonDocumentOptions { CommentHandling = System.Text.Json.JsonCommentHandling.Skip, AllowTrailingCommas = true });
            string? slug = manifest.RootElement.TryGetProperty("slug", out System.Text.Json.JsonElement s) ? s.GetString() : null;
            string? version = manifest.RootElement.TryGetProperty("version", out System.Text.Json.JsonElement v) ? v.GetString() : null;
            return slug is null ? null : (slug, ParseVersion(version));
        }
        catch (System.Text.Json.JsonException)
        {
            return null;
        }
    }

    /// <summary>"2.6.5.rc1" → 2.6.5: the leading numeric part, so a pre-release still compares above older releases.</summary>
    internal static Version ParseVersion(string? text)
    {
        string numeric = new string((text ?? "").TakeWhile(c => char.IsAsciiDigit(c) || c == '.').ToArray()).Trim('.');
        string[] parts = numeric.Split('.', StringSplitOptions.RemoveEmptyEntries);
        string padded = string.Join('.', parts.Take(4).Concat(Enumerable.Repeat("0", Math.Max(0, 2 - parts.Length))));
        return Version.TryParse(padded, out Version? parsed) ? parsed : new Version(0, 0);
    }

    // 4ms writes .cc / .hh.
    private static bool IsSource(string path) => path.EndsWith(".cpp", StringComparison.Ordinal) || path.EndsWith(".cc", StringComparison.Ordinal);

    private static bool IsHeader(string path) =>
        path.EndsWith(".h", StringComparison.Ordinal) || path.EndsWith(".hpp", StringComparison.Ordinal) || path.EndsWith(".hh", StringComparison.Ordinal);

    private static string PluginOf(string key) => key[..key.IndexOf('/')];

    /// <summary>
    /// "catalog cardinal": which modules official Cardinal and the neilbgr edition load, from two Cardinal source trees
    /// (plugins/plugins.cpp, plugins/*/plugin.json, src/override/plugin.cpp). Upstream is exported by scripts/export-cardinal-upstream.sh.
    /// </summary>
    /// <summary>
    /// "catalog library": names and tags of the VCV Library's modules that neither ports.json nor ports.overrides.json
    /// (next to it) know, for closed-source plugins and sources not fetched.
    /// </summary>
    public static int Library(string manifestsDir, string portsPath, string outputPath)
    {
        if (!Directory.Exists(manifestsDir))
        {
            Console.Error.WriteLine($"Manifests folder not found: {manifestsDir}");
            return 1;
        }
        string overridesPath = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(portsPath))!, "ports.overrides.json");
        HashSet<string> known = new[] { portsPath, overridesPath }.Where(File.Exists)
            .SelectMany(p => PortCatalog.FromJson(File.ReadAllText(p)).Modules.Keys).ToHashSet(StringComparer.Ordinal);

        Dictionary<string, ModuleInfo> missing = new Dictionary<string, ModuleInfo>(StringComparer.Ordinal);
        SortedSet<string> plugins = new SortedSet<string>(StringComparer.Ordinal);
        int manifests = 0;
        foreach (string manifestPath in Directory.EnumerateFiles(manifestsDir, "*.json").Order(StringComparer.Ordinal))
        {
            try
            {
                foreach ((string key, ModuleInfo info) in LibraryManifests.Modules(File.ReadAllText(manifestPath)))
                {
                    plugins.Add(key[..key.IndexOf('/')]);
                    if (!known.Contains(key))
                    {
                        missing[key] = info;
                    }
                }
                manifests++;
            }
            catch (System.Text.Json.JsonException ex)
            {
                Console.Error.WriteLine($"{Path.GetFileName(manifestPath)}: unreadable ({ex.Message})");
            }
        }
        File.WriteAllText(outputPath, LibraryManifests.ToJson(missing));
        string pluginsPath = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(outputPath))!, "library-plugins.json");
        File.WriteAllText(pluginsPath, "[\n" + string.Join(",\n", plugins.Select(p => "  " + System.Text.Json.JsonSerializer.Serialize(p))) + "\n]\n");
        // Roles from the descriptions, for modules whose tags say nothing ("Utility", "Polyphonic").
        string catalogDir = Path.GetDirectoryName(Path.GetFullPath(outputPath))!;
        string rolesPath = Path.Combine(catalogDir, "roles.json");
        RoleCatalog explicitRoles = File.Exists(rolesPath) ? RoleCatalog.FromJson(File.ReadAllText(rolesPath)) : new RoleCatalog(new Dictionary<string, Role>());
        Dictionary<string, Role> described = LibraryManifests.DescribedRoles(
            Directory.EnumerateFiles(manifestsDir, "*.json").Order(StringComparer.Ordinal).Select(File.ReadAllText), explicitRoles);
        string describedPath = Path.Combine(catalogDir, "library-roles.json");
        File.WriteAllText(describedPath, LibraryManifests.RolesToJson(described));
        Console.WriteLine($"{manifests} manifests, {plugins.Count} plugins → {pluginsPath}, {missing.Count} modules without port names → {outputPath}, {described.Count} roles from descriptions → {describedPath}");
        return 0;
    }

    /// <summary>
    /// "catalog scan": one plugin's modules (a folder or a .zip of it) as a user catalog, for "--catalog": the way to
    /// describe a plugin that neither Cardinal nor the VCV Library has, without touching catalog/.
    /// </summary>
    public static int Scan(string source, string? outputPath)
    {
        string path = InputPath.Resolve(source);
        List<PluginFile> files;
        if (File.Exists(path))
        {
            files = new List<PluginFile> { new PluginFile(Path.GetFileName(path), File.ReadAllBytes(path)) };
        }
        else if (Directory.Exists(path))
        {
            // Build output and git data are never sources.
            string[] skipped = { ".git", "build", "dist" };
            files = Directory.EnumerateFiles(path, "*.*", SearchOption.AllDirectories)
                .Select(p => (Full: p, Relative: Path.GetRelativePath(path, p).Replace('\\', '/')))
                .Where(f => !skipped.Any(s => f.Relative.StartsWith(s + "/", StringComparison.Ordinal))
                    && (Path.GetFileName(f.Relative) == "plugin.json" || IsSource(f.Relative) || IsHeader(f.Relative)))
                .Select(f => new PluginFile(f.Relative, File.ReadAllBytes(f.Full)))
                .ToList();
        }
        else
        {
            Console.Error.WriteLine($"Plugin folder or .zip not found: {path}");
            return 1;
        }

        PluginScan scan = PluginSources.Scan(files);
        foreach (string problem in scan.Problems)
        {
            Console.Error.WriteLine(problem);
        }
        if (scan.Plugin is null)
        {
            return 1;
        }
        // The slug comes from the plugin.json read: only its file-name-safe characters make the default name ("../x" can't escape).
        outputPath ??= $"{SafeFileName(scan.Plugin)}.modules.json";
        File.WriteAllText(outputPath, new UserCatalog(PluginSources.ToUserCatalog(scan).Modules).ToJson());
        Console.WriteLine($"{scan.Plugin}: {scan.Modules.Count} modules → {outputPath} (use it with --catalog)");
        return 0;
    }

    internal static string SafeFileName(string text)
    {
        string safe = new string(text.Select(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' ? c : '_').ToArray()).Trim('_');
        return safe.Length > 0 ? safe[..Math.Min(safe.Length, 64)] : "plugin";
    }

    public static int Cardinal(string upstreamRoot, string forkRoot, string outputPath)
    {
        CardinalScan? upstream = ScanCardinal(upstreamRoot);
        CardinalScan? fork = ScanCardinal(forkRoot);
        if (upstream is null || fork is null)
        {
            return 1;
        }

        CardinalModules modules = CardinalModules.FromScans(upstream, fork);
        File.WriteAllText(outputPath, modules.ToJson());
        Console.WriteLine($"{modules.OfficialCount} official modules, {modules.NeilbgrCount} more in the neilbgr edition, {upstream.ModuleFallbacks.Count + upstream.PluginFallbacks.Count} fallbacks → {outputPath}");
        foreach (string problem in upstream.Problems.Select(p => $"upstream: {p}").Concat(fork.Problems.Select(p => $"fork: {p}")))
        {
            Console.Error.WriteLine(problem);
        }
        return 0;
    }

    private static CardinalScan? ScanCardinal(string root)
    {
        string pluginsCpp = Path.Combine(root, "plugins", "plugins.cpp");
        if (!File.Exists(pluginsCpp))
        {
            Console.Error.WriteLine($"Not a Cardinal source tree (no plugins/plugins.cpp): {root}");
            return null;
        }
        string overridePath = Path.Combine(root, "src", "override", "plugin.cpp");
        return CardinalScanner.Scan(
            File.ReadAllText(pluginsCpp),
            File.Exists(overridePath) ? File.ReadAllText(overridePath) : null,
            dir =>
            {
                string manifest = Path.Combine(root, "plugins", dir, "plugin.json");
                return File.Exists(manifest) ? File.ReadAllText(manifest) : null;
            });
    }
}