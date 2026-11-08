using System.Text.Json;
using System.Text.Json.Serialization;

namespace ModularPatchTools.Core.Catalog;

/// <summary>
/// Decides a module's <see cref="Role"/>: an explicit entry in catalog/roles.json wins,
/// otherwise the plugin.json tags (from the port catalog), otherwise keywords in the model slug.
/// </summary>
public sealed class RoleCatalog
{
    private static readonly JsonSerializerOptions jsonOptions = new JsonSerializerOptions
    {
        Converters = { new JsonStringEnumConverter() },
    };

    // Ordered: the first matching tag wins, so the most specific tags come first. Rack's names only: aliases ("LFO", "VCA",
    // "Drums") are turned into them first, see RackTags.Canonical.
    private static readonly (string Tag, Role Role)[] tagRoles =
    {
        // A module tagged both "Synth voice"/"Oscillator" and "Envelope generator" is a voice with its own VCA, not a controller.
        ("Synth voice", Role.Source),
        ("Visual", Role.Monitor),
        ("Quantizer", Role.Pitch),
        ("Arpeggiator", Role.Time),
        ("Sequencer", Role.Time),
        ("Clock generator", Role.Time),
        ("Clock modulator", Role.Time),
        ("Logic", Role.Time),
        ("Envelope generator", Role.Controller),
        ("Low-frequency oscillator", Role.Controller),
        ("Random", Role.Controller),
        ("Sample and hold", Role.Controller),
        ("Slew limiter", Role.Controller),
        ("Envelope follower", Role.Controller),
        ("Controller", Role.Controller),
        ("Switch", Role.Router),
        ("Voltage-controlled oscillator", Role.Source),
        ("Oscillator", Role.Source),
        ("Noise", Role.Source),
        ("Sampler", Role.Source),
        ("Drum", Role.Source),
        ("Physical modeling", Role.Source),
        ("Reverb", Role.Effect),
        ("Delay", Role.Effect),
        ("Chorus", Role.Effect),
        ("Flanger", Role.Effect),
        ("Phaser", Role.Effect),
        ("Mixer", Role.Mixer),
        ("Effect", Role.Effect),
        ("Filter", Role.Modifier),
        ("Voltage-controlled amplifier", Role.Modifier),
        ("Waveshaper", Role.Modifier),
        ("Distortion", Role.Modifier),
        ("Equalizer", Role.Modifier),
        ("Compressor", Role.Modifier),
        ("Limiter", Role.Modifier),
        ("Dynamics", Role.Modifier),
        ("Low-pass gate", Role.Modifier),
        ("External", Role.Io),
        ("MIDI", Role.Io),
        ("Recording", Role.Io),
        ("Tuner", Role.Monitor),
        ("Ring modulator", Role.Modifier),
        ("Panning", Role.Modifier),
        ("Function generator", Role.Controller),
        // Last: a module tagged only so passes signals on (a multiple) or scales a CV (an attenuator).
        ("Multiple", Role.Router),
        ("Attenuator", Role.Controller),
    };

    private static readonly (string Keyword, Role Role)[] slugRoles =
    {
        ("Host", Role.Io), ("Audio", Role.Io), ("MIDI", Role.Io),
        ("Seq", Role.Time), ("Clock", Role.Time), ("Clk", Role.Time),
        ("Quant", Role.Pitch), ("Chord", Role.Pitch), ("Octave", Role.Pitch), ("Harmon", Role.Pitch), ("Transpos", Role.Pitch),
        // Polyphony and routing utilities (Merge, Split, Mute…): they pass signals on.
        ("Merge", Role.Router), ("Split", Role.Router), ("Splirge", Role.Router), ("Sort", Role.Router), ("Mux", Role.Router),
        ("FlipFlop", Role.Time), ("Switch", Role.Router), ("Flip", Role.Router), ("Mute", Role.Router),
        ("Count", Role.Time), ("Divid", Role.Time), ("ShiftReg", Role.Time), ("Trig", Role.Time),
        ("Drum", Role.Source), ("Kick", Role.Source), ("Snare", Role.Source), ("Perc", Role.Source), ("Clap", Role.Source), ("Cymbal", Role.Source),
        ("LFO", Role.Controller), ("ADSR", Role.Controller), ("Env", Role.Controller), ("Random", Role.Controller), ("Slew", Role.Controller),
        ("VCO", Role.Source), ("Osc", Role.Source), ("Noise", Role.Source),
        ("Reverb", Role.Effect), ("Delay", Role.Effect), ("Mix", Role.Mixer),
        ("VCF", Role.Modifier), ("Filter", Role.Modifier), ("VCA", Role.Modifier), ("Shaper", Role.Modifier), ("EQ", Role.Modifier),
        ("Gain", Role.Modifier), ("MidSide", Role.Modifier),
        ("Atten", Role.Controller), ("Offset", Role.Controller), ("Const", Role.Controller), ("Scale", Role.Controller),
        // Last, being short: "Mult" starts "MultiFilter" too, "Pan" sits in other words.
        ("Mult", Role.Router), ("Pan", Role.Modifier),
    };

    private readonly Dictionary<string, Role> explicitRoles;
    private readonly Dictionary<string, Role> describedRoles;

    /// <param name="describedRoles">Roles read from the VCV Library descriptions (catalog/library-roles.json): below the tags.</param>
    public RoleCatalog(IDictionary<string, Role> explicitRoles, IDictionary<string, Role>? describedRoles = null)
    {
        this.explicitRoles = new Dictionary<string, Role>(explicitRoles, StringComparer.Ordinal);
        this.describedRoles = new Dictionary<string, Role>(describedRoles ?? new Dictionary<string, Role>(), StringComparer.Ordinal);
    }

    public static RoleCatalog LoadEmbedded() =>
        new RoleCatalog(Parse(PortCatalog.ReadResource("catalog.roles.json")), Parse(PortCatalog.ReadResource("catalog.library-roles.json")));

    /// <summary>Explicit roles of <paramref name="overrides"/> on top of these (a user's own modules).</summary>
    public RoleCatalog MergedWith(IReadOnlyDictionary<string, Role> overrides) =>
        new RoleCatalog(explicitRoles.Concat(overrides).GroupBy(kv => kv.Key).ToDictionary(g => g.Key, g => g.Last().Value), describedRoles);

    public static RoleCatalog FromJson(string json) => new RoleCatalog(Parse(json));

    private static Dictionary<string, Role> Parse(string? json) =>
        json is null ? new Dictionary<string, Role>() : JsonSerializer.Deserialize<Dictionary<string, Role>>(json, jsonOptions) ?? new Dictionary<string, Role>();

    /// <summary>Whether a module has an explicit role in catalog/roles.json.</summary>
    public bool IsExplicit(string key) => explicitRoles.ContainsKey(key);

    /// <summary>The role the tags decide (under Rack's own names: "Drums" is "Drum"); null when none does.</summary>
    public static Role? FromTags(IEnumerable<string> tags)
    {
        // Some manifests pack several tags in one ("Mixer, Utilities"): each counts.
        List<string> canonical = tags.SelectMany(t => t.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)).Select(RackTags.Canonical).ToList();
        foreach ((string tag, Role role) in tagRoles)
        {
            if (canonical.Any(t => string.Equals(t, tag, StringComparison.OrdinalIgnoreCase)))
            {
                return role;
            }
        }
        return null;
    }

    /// <summary>Returns the role and whether it was a guess (no explicit entry, no recognized tag).</summary>
    public (Role Role, bool Guessed) Resolve(string plugin, string model, ModuleInfo info)
    {
        (Role role, RoleSource source) = ResolveWithSource(plugin, model, info);
        return (role, source >= RoleSource.Description);
    }

    /// <summary>The role and where it comes from: roles.json, the tags, the Library description, or the model's name.</summary>
    public (Role Role, RoleSource Source) ResolveWithSource(string plugin, string model, ModuleInfo info)
    {
        string key = $"{plugin}/{model}";
        if (explicitRoles.TryGetValue(key, out Role explicitRole))
        {
            return (explicitRole, RoleSource.Explicit);
        }
        if (FromTags(info.Tags) is Role tagged)
        {
            return (tagged, RoleSource.Tags);
        }
        if (describedRoles.TryGetValue(key, out Role described))
        {
            return (described, RoleSource.Description);
        }

        // "Bogaudio-Switch" must not match "Audio": look at the model name without its plugin prefix.
        string bareModel = model.Replace(plugin, "", StringComparison.OrdinalIgnoreCase).Trim('-', '_', ' ');
        foreach ((string keyword, Role role) in slugRoles)
        {
            if (NameHas(bareModel, keyword))
            {
                return (role, RoleSource.Name);
            }
        }

        return (Role.Controller, RoleSource.Default);
    }

    /// <summary>
    /// Long keywords anywhere in the name; short ones only where a word starts ("AutoScale" holds no "Osc",
    /// "CVConverter" no "VCO"), and "Mult" not as "Multi" ("MultiPan", "Multi_Meter").
    /// </summary>
    private static bool NameHas(string name, string keyword)
    {
        if (keyword.Length >= 5)
        {
            return name.Contains(keyword, StringComparison.OrdinalIgnoreCase);
        }
        for (int at = name.IndexOf(keyword, StringComparison.OrdinalIgnoreCase); at >= 0; at = name.IndexOf(keyword, at + 1, StringComparison.OrdinalIgnoreCase))
        {
            bool wordStart = at == 0 || !char.IsLetter(name[at - 1]) || (char.IsUpper(name[at]) && char.IsLower(name[at - 1]));
            bool multi = keyword == "Mult" && at + 4 < name.Length && char.ToLowerInvariant(name[at + 4]) == 'i';
            if (wordStart && !multi)
            {
                return true;
            }
        }
        return false;
    }
}

/// <summary>Where a module's role comes from, from the surest to a guess.</summary>
public enum RoleSource
{
    Explicit,
    Tags,
    Description,
    Name,
    Default,
}