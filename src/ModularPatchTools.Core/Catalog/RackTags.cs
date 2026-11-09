namespace ModularPatchTools.Core.Catalog;

/// <summary>The tags Rack knows (Rack's src/tag.cpp, first name of each), the ones a plugin.json may use.</summary>
public static class RackTags
{
    public static IReadOnlyList<string> All { get; } = new[]
    {
        "Arpeggiator", "Attenuator", "Blank", "Chorus", "Clock generator", "Clock modulator", "Compressor", "Controller",
        "Delay", "Digital", "Distortion", "Drum", "Dual", "Dynamics", "Effect", "Envelope follower", "Envelope generator",
        "Equalizer", "Expander", "External", "Filter", "Flanger", "Function generator", "Granular", "Hardware clone",
        "Limiter", "Logic", "Low-frequency oscillator", "Low-pass gate", "MIDI", "Mixer", "Multiple", "Noise", "Oscillator",
        "Panning", "Phaser", "Physical modeling", "Polyphonic", "Quad", "Quantizer", "Random", "Recording", "Reverb",
        "Ring modulator", "Sample and hold", "Sampler", "Sequencer", "Slew limiter", "Speech", "Switch", "Synth voice",
        "Tuner", "Utility", "Visual", "Vocoder", "Voltage-controlled amplifier", "Waveshaper",
    };

    // Rack's other names for some tags (tag.cpp's tagAliases): a plugin.json may write "Drums", "LFO" or "VCA".
    private static readonly Dictionary<string, string> aliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["Clock"] = "Clock generator",
        ["Drums"] = "Drum",
        ["Percussion"] = "Drum",
        ["EQ"] = "Equalizer",
        ["VCF"] = "Filter",
        ["Voltage controlled filter"] = "Filter",
        ["Hardware"] = "Hardware clone",
        ["LFO"] = "Low-frequency oscillator",
        ["Low frequency oscillator"] = "Low-frequency oscillator",
        ["Low pass gate"] = "Low-pass gate",
        ["Lowpass gate"] = "Low-pass gate",
        ["VCO"] = "Oscillator",
        ["Voltage controlled oscillator"] = "Oscillator",
        ["Pan"] = "Panning",
        ["Poly"] = "Polyphonic",
        ["S&H"] = "Sample and hold",
        ["Sample & hold"] = "Sample and hold",
        ["Amplifier"] = "Voltage-controlled amplifier",
        ["VCA"] = "Voltage-controlled amplifier",
        ["Voltage controlled amplifier"] = "Voltage-controlled amplifier",
    };

    /// <summary>
    /// The tags under Rack's own names, each counted when a manifest packs several in one ("Mixer, Utilities").
    /// </summary>
    public static List<string> Canonical(IEnumerable<string> tags) =>
        tags.SelectMany(t => t.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)).Select(Canonical).ToList();

    /// <summary>The tag under Rack's own name ("Drums" is "Drum", "lfo" is "Low-frequency oscillator"); an unknown tag as is.</summary>
    public static string Canonical(string tag)
    {
        string trimmed = tag.Trim();
        return aliases.TryGetValue(trimmed, out string? name) ? name : All.FirstOrDefault(t => string.Equals(t, trimmed, StringComparison.OrdinalIgnoreCase)) ?? trimmed;
    }
}