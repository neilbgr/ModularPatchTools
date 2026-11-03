using System.Reflection;

namespace VcvPatchTools.Core;

/// <summary>The version every binary of the solution is built with (Directory.Build.props), and the commit it was built from.</summary>
public static class ProductVersion
{
    private static readonly string informational = typeof(ProductVersion).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "";

    /// <summary>"1.0.0 (d6a0afb1)": the version, then the commit when the build knew it.</summary>
    public static string Display { get; } = Format(informational);

    /// <summary>"1.2.0": the version alone.</summary>
    public static string Version { get; } = VersionOf(informational);

    /// <summary>The full commit the build was made from, when it knew it.</summary>
    public static string? Commit { get; } = CommitOf(informational);

    /// <summary>"2026-11-03": this version's date in CHANGELOG.md (see Directory.Build.props), null while it isn't released.</summary>
    public static string? ReleaseDate { get; } = typeof(ProductVersion).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
        .FirstOrDefault(a => a.Key == "ReleaseDate")?.Value is { Length: > 0 } date ? date : null;

    internal static string VersionOf(string informationalVersion) =>
        informationalVersion.IndexOf('+') is int plus and >= 0 ? informationalVersion[..plus] : informationalVersion;

    internal static string? CommitOf(string informationalVersion) =>
        informationalVersion.IndexOf('+') is int plus and >= 0 && plus + 1 < informationalVersion.Length ? informationalVersion[(plus + 1)..] : null;

    /// <summary>Shortens an informational version "1.0.0+&lt;full sha&gt;" to "1.0.0 (&lt;8 hex digits&gt;)".</summary>
    internal static string Format(string informationalVersion)
    {
        int plus = informationalVersion.IndexOf('+');
        if (plus < 0)
        {
            return informationalVersion;
        }
        string commit = informationalVersion[(plus + 1)..];
        return $"{informationalVersion[..plus]} ({commit[..Math.Min(8, commit.Length)]})";
    }
}