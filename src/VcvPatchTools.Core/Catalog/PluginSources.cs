using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace VcvPatchTools.Core.Catalog;

/// <summary>A file handed over by its user: a plugin.json, a source file, or a .zip of a whole plugin.</summary>
public sealed record PluginFile(string Path, byte[] Bytes);

/// <param name="Plugin">The plugin's slug, from its plugin.json (null when none was found).</param>
public sealed record PluginScan(string? Plugin, IReadOnlyDictionary<string, ModuleInfo> Modules, IReadOnlyList<string> Problems);

/// <summary>
/// Reads a plugin's port names from its sources held in memory, the way "vcvpatch catalog build" reads a plugin folder
/// (see <see cref="SourceScanner"/>): files picked in the browser, a .zip of a repository, or a folder read by the CLI.
/// The plugin.json closest to the top is the plugin's; its sources are the C++ files under the "src" folder next to it
/// (all of them when there is no such folder, as for files picked one by one), plus the headers they include from
/// elsewhere in the plugin. Nothing is built or run.
/// </summary>
public static partial class PluginSources
{
    /// <summary>The most the files may hold in sources, once decompressed (a .zip can claim any size: what is read counts).</summary>
    public const long MaxSourceBytes = 64L * 1024 * 1024;

    /// <summary>The most entries of a .zip looked at: a repository has far fewer, a crafted archive may have millions.</summary>
    public const int MaxZipEntries = 50_000;

    public static PluginScan Scan(IEnumerable<PluginFile> files)
    {
        List<string> problems = new List<string>();
        List<(string Path, string Text)> texts = Expand(files, problems);

        (string Path, string Text)? manifest = texts
            .Where(f => FileName(f.Path) == "plugin.json" && IsManifest(f.Text))
            .OrderBy(f => f.Path.Count(c => c == '/')).ThenBy(f => f.Path, StringComparer.Ordinal)
            .Cast<(string, string)?>().FirstOrDefault();
        if (manifest is not (string manifestPath, string manifestText))
        {
            problems.Add("No plugin.json with a \"slug\" and \"modules\" among the files.");
            return new PluginScan(null, new Dictionary<string, ModuleInfo>(), problems);
        }

        string root = manifestPath.Contains('/') ? manifestPath[..(manifestPath.LastIndexOf('/') + 1)] : "";
        List<(string Path, string Text)> own = texts.Where(f => f.Path.StartsWith(root, StringComparison.Ordinal) && (IsSource(f.Path) || IsHeader(f.Path))).ToList();
        string sourceDir = root + "src/";
        List<(string Path, string Text)> sources = own.Any(f => f.Path.StartsWith(sourceDir, StringComparison.Ordinal))
            ? own.Where(f => f.Path.StartsWith(sourceDir, StringComparison.Ordinal)).ToList()
            : own;
        HashSet<string> included = sources.SelectMany(s => IncludeRegex().Matches(s.Text)).Select(m => FileName(m.Groups[1].Value)).ToHashSet(StringComparer.Ordinal);
        HashSet<string> sourcePaths = sources.Select(s => s.Path).ToHashSet(StringComparer.Ordinal);
        List<SourceFile> headers = own.Where(f => IsHeader(f.Path) && !sourcePaths.Contains(f.Path) && included.Contains(FileName(f.Path)))
            .OrderBy(f => f.Path, StringComparer.Ordinal).Select(f => new SourceFile(f.Path, f.Text)).ToList();
        if (sources.Count == 0)
        {
            problems.Add("No C++ source (.cpp, .hpp…) next to plugin.json: only module names and tags are known.");
        }

        try
        {
            ScanResult scan = SourceScanner.ScanPlugin(manifestText, sources.Select(s => new SourceFile(s.Path, s.Text)).ToList(), headers);
            problems.AddRange(scan.Problems);
            return new PluginScan(Slug(manifestText) is string slug ? UserCatalog.Printable(slug) : null, scan.Modules, problems.Select(UserCatalog.Printable).ToList());
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException)
        {
            problems.Add($"plugin.json can't be read: {ex.Message}");
            return new PluginScan(null, new Dictionary<string, ModuleInfo>(), problems);
        }
    }

    /// <summary>The files as text, with .zip archives opened; paths use "/".</summary>
    private static List<(string Path, string Text)> Expand(IEnumerable<PluginFile> files, List<string> problems)
    {
        List<(string Path, string Text)> texts = new List<(string, string)>();
        long total = 0;
        foreach (PluginFile file in files)
        {
            string path = file.Path.Replace('\\', '/').TrimStart('/');
            if (path.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    using ZipArchive zip = new ZipArchive(new MemoryStream(file.Bytes), ZipArchiveMode.Read);
                    if (zip.Entries.Count > MaxZipEntries)
                    {
                        problems.Add($"{UserCatalog.Printable(FileName(path))}: more than {MaxZipEntries} entries, not read.");
                        continue;
                    }
                    // Entry paths are only names here: nothing is ever written to disk, so "../" in them leads nowhere.
                    foreach (ZipArchiveEntry entry in zip.Entries.Where(e => IsWanted(e.FullName)))
                    {
                        using Stream content = entry.Open();
                        // Read against what is left of the budget, never trusting the size the entry declares (zip bomb).
                        byte[]? bytes = ReadAtMost(content, MaxSourceBytes - total);
                        if (bytes is null)
                        {
                            problems.Add($"{UserCatalog.Printable(FileName(path))}: more than {MaxSourceBytes / (1024 * 1024)} MB of sources, the rest is left out.");
                            break;
                        }
                        total += bytes.Length;
                        texts.Add((entry.FullName.Replace('\\', '/'), Encoding.UTF8.GetString(bytes)));
                    }
                }
                catch (InvalidDataException ex)
                {
                    problems.Add($"{UserCatalog.Printable(FileName(path))}: not a readable .zip ({ex.Message}).");
                }
            }
            else if (IsWanted(path))
            {
                total += file.Bytes.Length;
                if (total > MaxSourceBytes)
                {
                    problems.Add($"More than {MaxSourceBytes / (1024 * 1024)} MB of sources: {UserCatalog.Printable(FileName(path))} and the rest are left out.");
                    break;
                }
                texts.Add((path, Encoding.UTF8.GetString(file.Bytes)));
            }
            else
            {
                problems.Add($"{UserCatalog.Printable(FileName(path))}: not a plugin.json, C++ source or .zip, left out.");
            }
        }
        return texts;
    }

    /// <summary>The stream's bytes, or null when there are more than <paramref name="limit"/>.</summary>
    private static byte[]? ReadAtMost(Stream stream, long limit)
    {
        using MemoryStream buffer = new MemoryStream();
        byte[] chunk = new byte[81920];
        int read;
        while ((read = stream.Read(chunk, 0, chunk.Length)) > 0)
        {
            if (buffer.Length + read > limit)
            {
                return null;
            }
            buffer.Write(chunk, 0, read);
        }
        return buffer.ToArray();
    }

    private static bool IsWanted(string path) => FileName(path) == "plugin.json" || IsSource(path) || IsHeader(path);

    private static bool IsManifest(string text)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(text, new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
            return document.RootElement.ValueKind == JsonValueKind.Object
                && document.RootElement.TryGetProperty("slug", out _) && document.RootElement.TryGetProperty("modules", out _);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static string? Slug(string manifest)
    {
        using JsonDocument document = JsonDocument.Parse(manifest, new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
        return document.RootElement.TryGetProperty("slug", out JsonElement slug) && slug.ValueKind == JsonValueKind.String ? slug.GetString() : null;
    }

    private static string FileName(string path) => path[(path.LastIndexOfAny(new[] { '/', '\\' }) + 1)..];

    // 4ms writes .cc / .hh.
    private static bool IsSource(string path) => path.EndsWith(".cpp", StringComparison.Ordinal) || path.EndsWith(".cc", StringComparison.Ordinal);

    private static bool IsHeader(string path) =>
        path.EndsWith(".h", StringComparison.Ordinal) || path.EndsWith(".hpp", StringComparison.Ordinal) || path.EndsWith(".hh", StringComparison.Ordinal);

    [GeneratedRegex(@"#\s*include\s*[<""]([^>""]+)[>""]")]
    private static partial Regex IncludeRegex();

    /// <summary>The scanned modules as user catalog entries (role left to the tags).</summary>
    public static UserCatalog ToUserCatalog(PluginScan scan) => new UserCatalog(
        scan.Modules.Select(kv => new KeyValuePair<string, UserModule>(kv.Key, new UserModule(kv.Value.Name, kv.Value.Tags, null, kv.Value.Inputs, kv.Value.Outputs))),
        scan.Problems);
}