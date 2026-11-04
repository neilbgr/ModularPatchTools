using ModularPatchTools.Cli;
using ModularPatchTools.Core;
using ModularPatchTools.Core.Catalog;
using ModularPatchTools.Core.Patch;
using ModularPatchTools.Diagram;
using ModularPatchTools.Diagram.Analysis;
using ModularPatchTools.Diagram.Layout;

try
{
    return Run(args);
}
catch (InvalidDataException e)
{
    // A broken or foreign file: one readable line, not a stack trace.
    Console.Error.WriteLine($"Invalid patch: {e.Message}");
    return 1;
}

static int Run(string[] args)
{
    if (args.Length == 1 && args[0] is "--version" or "version")
    {
        Console.WriteLine($"modpatch {ProductVersion.Display}");
        return 0;
    }

    if (args.Length >= 2 && args[0] == "catalog" && args[1] == "build")
    {
        List<string> src = Options(args, "--src");
        if (src.Count == 0)
        {
            PrintUsage();
            return 1;
        }
        return CatalogCommand.Build(src, Option(args, "--out") ?? "catalog/ports.json", Option(args, "--report"));
    }

    if (args.Length >= 2 && args[0] == "catalog" && args[1] == "scan")
    {
        string? plugin = Option(args, "--src");
        if (plugin is null)
        {
            PrintUsage();
            return 1;
        }
        return CatalogCommand.Scan(plugin, Option(args, "--out"));
    }

    if (args.Length >= 2 && args[0] == "catalog" && args[1] == "library")
    {
        string? manifests = Option(args, "--manifests");
        if (manifests is null)
        {
            PrintUsage();
            return 1;
        }
        return CatalogCommand.Library(manifests, Option(args, "--ports") ?? "catalog/ports.json", Option(args, "--out") ?? "catalog/library.json");
    }

    if (args.Length >= 2 && args[0] == "catalog" && args[1] == "cardinal")
    {
        string? upstream = Option(args, "--upstream");
        string? fork = Option(args, "--fork");
        if (upstream is null || fork is null)
        {
            PrintUsage();
            return 1;
        }
        return CatalogCommand.Cardinal(upstream, fork, Option(args, "--out") ?? "catalog/cardinal.json");
    }

    // "inspect" and "render" are the names these had in vcvdiagram.
    if (args.Length >= 2 && args[0] is "info" or "inspect")
    {
        return Info(args[1], args);
    }

    if (args.Length >= 1 && args[0] == "convert")
    {
        return ConvertCommand.Run(args, Console.Out, Console.Error);
    }

    if (args.Length >= 2 && args[0] is "diagram" or "render")
    {
        return Render(args);
    }

    PrintUsage();
    return 1;
}

static int Render(string[] args)
{
    string path = InputPath.Resolve(args[1]);
    string? output = Option(args, "-o");
    string formatText = Option(args, "-f") ?? (output is not null ? Path.GetExtension(output) : "html");
    if (!PatchDiagram.TryParseFormat(formatText, out DiagramFormat format))
    {
        Console.Error.WriteLine($"Unknown format \"{formatText}\" (html, svg, dot, mmd, json)");
        return 1;
    }

    PatchAnalysis? analysis = Load(path, args);
    if (analysis is null)
    {
        return 1;
    }

    string title = Path.GetFileNameWithoutExtension(path);
    // --unfold all | <group,group…> ; default: the folded overview.
    string? unfoldOption = Option(args, "--unfold");
    IReadOnlySet<string>? unfolded = unfoldOption switch
    {
        null => null,
        "all" => PatchDiagram.GroupKeys(analysis),
        _ => unfoldOption.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToHashSet(),
    };
    // --scopes (or --scopes=badge): a badge on what they watch; --scopes=modules: boxes in a column on the right.
    MonitorView monitors = args.Contains("--scopes=modules") ? MonitorView.Modules
        : args.Contains("--scopes") || args.Contains("--scopes=badge") ? MonitorView.Badge : MonitorView.Hidden;
    string content = PatchDiagram.Export(PatchDiagram.Layout(analysis, title, unfolded, monitors: monitors, functionNames: args.Contains("--functions"), portTabs: !args.Contains("--no-ports"), libraryLinks: !args.Contains("--no-links")), format);
    output ??= Path.ChangeExtension(Path.GetFileName(path), PatchDiagram.Extension(format));
    File.WriteAllText(output, content);
    Console.WriteLine($"{format} → {output}");
    return 0;
}

static int Info(string rawPath, string[] args)
{
    byte[]? bytes = Read(rawPath);
    if (bytes is null || UserCatalogs(args) is not UserCatalog user)
    {
        return 1;
    }
    ConvertCommand.WriteOrigin(PatchArchive.Read(bytes), Console.Out);
    Console.WriteLine();
    return InspectCommand.Run(PatchDiagram.Analyze(bytes, user), Console.Out);
}

static PatchAnalysis? Load(string rawPath, string[] args) =>
    Read(rawPath) is byte[] bytes && UserCatalogs(args) is UserCatalog user ? PatchDiagram.Analyze(bytes, user) : null;

// --catalog my-modules.json, repeatable: the user's own module descriptions, later files winning.
static UserCatalog? UserCatalogs(string[] args)
{
    UserCatalog merged = UserCatalog.Empty;
    foreach (string option in Options(args, "--catalog"))
    {
        string path = InputPath.Resolve(option);
        if (!File.Exists(path))
        {
            Console.Error.WriteLine($"Catalog not found: {path}");
            return null;
        }
        UserCatalog read = UserCatalog.FromJson(File.ReadAllText(path));
        foreach (string problem in read.Problems)
        {
            Console.Error.WriteLine($"{Path.GetFileName(path)}: {problem}");
        }
        merged = merged.MergedWith(new UserCatalog(read.Modules));
    }
    return merged;
}

static byte[]? Read(string rawPath)
{
    string path = InputPath.Resolve(rawPath);
    if (!File.Exists(path))
    {
        Console.Error.WriteLine($"File not found: {path}");
        return null;
    }
    return File.ReadAllBytes(path);
}

static string? Option(string[] args, string name)
{
    int index = Array.IndexOf(args, name);
    return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
}

/// <summary>Every value of a repeatable option ("--src a --src b").</summary>
static List<string> Options(string[] args, string name) =>
    args.Select((arg, i) => (arg, i)).Where(a => a.arg == name && a.i + 1 < args.Length).Select(a => args[a.i + 1]).ToList();

static void PrintUsage()
{
    Console.Error.WriteLine($"modpatch {ProductVersion.Display}");
    Console.Error.WriteLine("Usage:");
    Console.Error.WriteLine("  modpatch info <patch.vcv>");
    Console.Error.WriteLine("    (format, Rack or Cardinal origin, modules by band, voices, groups, typed cables, diagnostics)");
    Console.Error.WriteLine($"  {ConvertCommand.Usage}");
    Console.Error.WriteLine("    (direction from the detected origin unless --to; output next to the input, e.g. Patch.rack.vcv)");
    Console.Error.WriteLine("  modpatch diagram <patch.vcv> [-f html|svg|dot|mmd|json] [-o <output file>] [--unfold all|<group,...>]");
    Console.Error.WriteLine("                   [--scopes[=badge|modules]] [--functions] [--no-ports] [--no-links]");
    Console.Error.WriteLine("    (default: folded overview; group keys are listed by \"info\")");
    Console.Error.WriteLine("  modpatch catalog build --src <Cardinal/plugins> [--src <more plugins>…] [--out catalog/ports.json] [--report catalog/scan-report.txt]");
    Console.Error.WriteLine("  modpatch catalog scan --src <plugin folder or .zip> [--out my-modules.json]   (a user catalog, for --catalog)");
    Console.Error.WriteLine("  modpatch catalog library --manifests <VCVRack/library clone>/manifests [--ports catalog/ports.json] [--out catalog/library.json]");
    Console.Error.WriteLine("  modpatch catalog cardinal --upstream <Cardinal tree> --fork <Cardinal tree> [--out catalog/cardinal.json]");
    Console.Error.WriteLine("  modpatch --version");
}