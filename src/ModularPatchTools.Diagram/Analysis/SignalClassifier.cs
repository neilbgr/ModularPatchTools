using ModularPatchTools.Core.Catalog;

namespace ModularPatchTools.Diagram.Analysis;

/// <summary>
/// Types a cable from its color (the patch author's own convention, see <see cref="DefaultPalette"/>),
/// then cross-checks with the port names. When the color is unknown, the port names decide,
/// then the module roles (audio between sources/modifiers/mixers, CV otherwise).
/// </summary>
public sealed class SignalClassifier
{
    /// <summary>Neil's cable color convention.</summary>
    public static IReadOnlyDictionary<string, SignalType> DefaultPalette { get; } = new Dictionary<string, SignalType>(StringComparer.OrdinalIgnoreCase)
    {
        ["#ff5252"] = SignalType.Audio,
        ["#ffd452"] = SignalType.Pitch,
        ["#52ff7d"] = SignalType.Cv,
        ["#a8ff52"] = SignalType.Cv,
        ["#52beff"] = SignalType.Gate,
    };

    private static readonly string[] pitchWords = { "v/oct", "1v/octave", "pitch", "v/o", "voct" };
    private static readonly string[] cvWords = { "env", "envelope", "lfo", "cv", "phase", "mod", "modulation", "s&h", "sample", "random", "level" };
    private static readonly string[] gateWords = { "gate", "trig", "retrig", "clock", "clk", "reset", "run", "start", "stop", "continue" };
    // What a keyboard sends besides pitch and gate: these outputs are CV, wherever they're patched.
    private static readonly string[] expressionWords = { "velocity", "vel", "aftertouch", "aft", "pressure", "expression", "mod wheel", "modwheel" };
    private static readonly string[] cvInputWords = { "cv" };
    private static readonly string[] modulationInputWords = { "cv", "fm", "pm", "am", "sync", "mod", "modulation", "phase", "exp", "lin", "rate", "freq", "frequency", "cutoff", "fold", "shape", "pwm", "pw" };
    // Inputs that only ever take a command (besides pitch and gate inputs): never where an oscillator's sound goes.
    private static readonly string[] commandInputWords = { "cv", "fm", "pm", "sync", "modulation" };
    // What a logic module may also be and still take only gates: anything else (a sample and hold, a switch, a
    // waveshaper…) means its inputs take any voltage.
    private static readonly string[] gateLogicTags = { "Logic", "Clock generator", "Clock modulator", "Sequencer", "Utility", "Polyphonic", "Dual", "Quad", "Hardware clone", "Visual", "Expander" };
    private static readonly string[] controlWords = { "cv", "select", "index", "step", "steps" };

    private readonly IReadOnlyDictionary<string, SignalType> palette;

    public SignalClassifier(IReadOnlyDictionary<string, SignalType>? palette = null) => this.palette = palette ?? DefaultPalette;

    /// <summary>Whether this cable color is part of the palette (then it decides the signal type).</summary>
    public bool KnowsColor(string? color) => color is not null && palette.ContainsKey(color);

    /// <param name="toTakes">
    /// What the receiving module's inputs take, from what it is, when the input's name doesn't say: a logic module's
    /// inputs take gates, an envelope follower's takes audio, a sample and hold's takes CV (see <see cref="InputsOf"/>).
    /// </param>
    public (SignalType Type, string? Mismatch) Classify(string? color, string fromPort, string toPort, Role fromRole, Role toRole, SignalType? toTakes = null)
    {
        SignalType? byColor = color is not null && palette.TryGetValue(color, out SignalType t) ? t : null;
        SignalType? byFromPort = FromPortName(fromPort);
        SignalType? byToPort = FromPortName(toPort);

        // A source's plain output (no CV/gate/pitch in its name) patched into a processor or a mixer is its audio,
        // whatever the cable color says: the color is then most likely a slip, so it's reported.
        // Not into a control input though: an oscillator patched into a delay's Clock or a filter's FM is used as
        // a clock or a modulation, and the color says which.
        bool sourceAudio = fromRole == Role.Source && toRole is Role.Modifier or Role.Effect or Role.Mixer
            && byFromPort is null && !IsCvName(fromPort) && FromPortName(toPort) is null && !HasWord(toPort, commandInputWords);
        if (sourceAudio && byColor is SignalType wrongColor && wrongColor != SignalType.Audio)
        {
            return (SignalType.Audio, $"color says {wrongColor}, but a source's audio output into a {toRole.ToString().ToLowerInvariant()} is audio: treated as audio");
        }

        if (byColor is SignalType colorType)
        {
            // Only the source port says what the signal *is*; a pitch CV patched into an "Attack" input is still pitch.
            string? mismatch = byFromPort is SignalType portType && portType != colorType
                ? $"color says {colorType}, output '{fromPort}' says {portType}"
                : null;
            return (colorType, mismatch);
        }

        if (byFromPort is SignalType fromType)
        {
            return (fromType, null);
        }
        // A velocity patched into a pitch input (to scale it with an octave shifter…) is still velocity.
        if (HasWord(fromPort, expressionWords))
        {
            return (SignalType.Cv, null);
        }
        if (byToPort is SignalType toType)
        {
            return (toType, null);
        }
        if (toTakes is SignalType taken && !HasWord(toPort, cvInputWords))
        {
            return (taken, null);
        }

        // An input named "CV" (a mixer's channel level CV…) takes a modulation, even from an audio processor.
        // What an oscillator or an effect sends out is its sound, wherever it goes (a reverb's output into a min/max
        // utility…), unless it goes into an input named CV.
        bool audioFrom = fromRole is Role.Source or Role.Modifier or Role.Effect or Role.Mixer;
        bool audioTo = (toRole is Role.Modifier or Role.Effect or Role.Mixer or Role.Io or Role.Router || fromRole is Role.Source or Role.Effect)
            && !HasWord(toPort, cvInputWords);
        return (audioFrom && audioTo ? SignalType.Audio : SignalType.Cv, null);
    }

    /// <summary>
    /// What a module's inputs take by what it is (its tags, under Rack's own names), when their names don't say: null
    /// when nothing. A logic module only takes gates when it's nothing else than logic, clocks and sequencing (not a
    /// window comparator tagged waveshaper, not a sample and hold); a sample and hold takes CV, even from an oscillator
    /// or a noise (a random voltage source).
    /// </summary>
    public static SignalType? InputsOf(IEnumerable<string> tags)
    {
        List<string> canonical = RackTags.Canonical(tags);
        bool Has(string tag) => canonical.Contains(tag, StringComparer.OrdinalIgnoreCase);
        if (Has("Logic") && canonical.All(t => gateLogicTags.Contains(t, StringComparer.OrdinalIgnoreCase)))
        {
            return SignalType.Gate;
        }
        return Has("Envelope follower") ? SignalType.Audio : Has("Sample and hold") ? SignalType.Cv : null;
    }

    /// <summary>A switch's control input (clock, reset, select…), as opposed to a signal it routes.</summary>
    public static bool IsControlPort(string portName) => FromPortName(portName) == SignalType.Gate || HasWord(portName, controlWords);

    /// <summary>An input that modulates rather than takes a signal to process: FM, sync, CV, pitch, gate…</summary>
    public static bool IsModulationInput(string portName) => FromPortName(portName) is not null || HasWord(portName, modulationInputWords);

    public static SignalType? FromPortName(string portName)
    {
        string lower = portName.ToLowerInvariant();
        if (pitchWords.Any(lower.Contains))
        {
            return SignalType.Pitch;
        }
        if (gateWords.Any(w => ContainsWord(lower, w)))
        {
            return SignalType.Gate;
        }
        return null;
    }

    /// <summary>Whole words only: "Envelope" is CV, "VCO (enveloped)" is not.</summary>
    private static bool IsCvName(string portName) => HasWord(portName, cvWords);

    private static bool HasWord(string portName, string[] words)
    {
        string lower = portName.ToLowerInvariant();
        return words.Any(w => System.Text.RegularExpressions.Regex.IsMatch(lower, $@"(^|[^a-z]){System.Text.RegularExpressions.Regex.Escape(w)}([^a-z]|$)"));
    }

    private static bool ContainsWord(string text, string word)
    {
        int index = text.IndexOf(word, StringComparison.Ordinal);
        return index >= 0 && (index == 0 || !char.IsLetter(text[index - 1]));
    }
}