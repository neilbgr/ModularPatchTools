namespace VcvPatchTools.Web;

/// <summary>
/// A link opening a new GitHub issue that lists the published modules whose port names the catalog lacks:
/// the details a report needs, with no file to attach.
/// </summary>
public static class IssueReport
{
    public const string NewIssueUrl = "https://github.com/neilbgr/VcvPatchTools/issues/new";

    // Browsers and GitHub cap a URL's length: a patch listing more modules reports the first ones.
    private const int maxModules = 30;

    public static string MissingPortNamesUrl(IReadOnlyList<string> modules, string? patchName)
    {
        string title = $"Port names missing for {string.Join(", ", modules.Take(3))}{(modules.Count > 3 ? ", …" : "")}";
        string body = $"Patch: {patchName ?? "?"}\nVersion: {Core.ProductVersion.Display}\n\nModules shown with numbered ports:\n"
            + string.Join("\n", modules.Take(maxModules).Select(m => $"- {m} (https://library.vcvrack.com/{m})"))
            + (modules.Count > maxModules ? $"\n- and {modules.Count - maxModules} more" : "")
            + "\n\nPort names as Rack shows them when hovering each port (optional):\n";
        return $"{NewIssueUrl}?title={Uri.EscapeDataString(title)}&body={Uri.EscapeDataString(body)}";
    }
}