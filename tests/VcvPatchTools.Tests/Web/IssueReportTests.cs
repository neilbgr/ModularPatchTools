using VcvPatchTools.Web;

namespace VcvPatchTools.Tests;

public class IssueReportTests
{
    [Fact]
    public void The_issue_lists_each_module_with_its_library_page()
    {
        string url = IssueReport.MissingPortNamesUrl(new[] { "NYSTHI/PolyLPG", "NYSTHI/b208_envelope" }, "Ciani 03");

        Assert.StartsWith(IssueReport.NewIssueUrl + "?title=", url);
        string decoded = Uri.UnescapeDataString(url);
        Assert.Contains("title=Port names missing for NYSTHI/PolyLPG, NYSTHI/b208_envelope&", decoded);
        Assert.Contains("Patch: Ciani 03", decoded);
        Assert.Contains("- NYSTHI/b208_envelope (https://library.vcvrack.com/NYSTHI/b208_envelope)", decoded);
    }
}