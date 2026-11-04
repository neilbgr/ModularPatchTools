using System.Text.RegularExpressions;

namespace ModularPatchTools.Core.Catalog;

/// <summary>
/// A role read from a module's one-line description in the VCV Library ("Split polyphonic input to 16 outputs" is a
/// router, "Gate to Trigger Converter" a time utility), for the modules whose tags say nothing ("Utility",
/// "Polyphonic"). Used when generating catalog/library-roles.json; the role stays flagged as guessed.
/// </summary>
public static class DescriptionRoles
{
    // Ordered: the first match wins, the most telling phrases first.
    private static readonly (Role Role, Regex Pattern)[] rules =
    {
        // Rack helpers driven by the mouse or the keyboard: controls played live, whatever else they mention.
        (Role.Performance, Rx(@"\bmouse\b|\bhotkeys?\b|keyboard shortcut|\bhovered\b|push.?buttons?|\bbuttons? (to|that|for)\b|\bbuttons with\b|momentary|touch.?strip")),
        // Phrases that would otherwise read as something else: a voltage source "with display", a "BPM to delay" helper.
        (Role.Controller, Rx(@"voltage.?sources?|\bparameters?\b|\bmapping\b|sample.?(&|and).?hold|\bs&h\b")),
        (Role.Monitor, Rx(@"\bshows? (the )?(input|values?|voltages?)")),
        (Role.Modifier, Rx(@"cross.?fad")),
        (Role.Time, Rx(@"\bbpm\b|\bcounts? (up|down)\b|\bcounters?\b")),
        (Role.Router, Rx(@"\bpatch ?bay|\bbypass|wireless|\bselector\b|\bbus(es)?\b|send/return|connects an input to an output|copy a mono|stereo (signal )?to poly|to n-poly|concatenat|monophonic\W+(signals? )?(to|into)\W+(a )?polyphonic|polyphonic\W+(signals? )?(to|into)\W+(a )?monophonic|interleav|\bclon(e|er|es)\b")),
        (Role.Source, Rx(@"noise generator|audio player|speech synth|granular|test signal|signal generator")),
        (Role.Monitor, Rx(@"\bmonitor|mix checker|\btuner\b|oscilloscope|\bscope\b|\bmeters?\b|level ?meter|\bdisplays?\b|visuali[sz]|\bshows? (the )?(input|values?|voltages?)|\btext (area|panel|field|module)|\bnotes? (module|panel|area)|\bpatch notes\b|\bcomments?\b|\blabels?\b")),
        (Role.Io, Rx(@"\boutput module\b|\bmidi\b|\brecord(er|ing|s)?\b|open sound control|\bosc (in|out|messages?)\b|audio interface|\bwav files?\b")),
        (Role.Router, Rx(@"\bmerg|\bsplit|\bmultiples\b|(active|buffered|passive|polyphonic|dual) multiple\b|\bmults?\b|\bswitch|\bsort|multiplex|\bmux|demux|\brout(e|er|es|ing)\b|\bmut(e|es|ing)\b|\bpoly(phonic)? ?(to|into) ?mono|\bmono ?(to|into) ?poly|\bbundles?\b|\bchannels? (into|to) (a|one) poly")),
        (Role.Time, Rx(@"\bclocks?\b|\bcounts?\b|\bdivid|\btriggers?\b|\bgates?\b|\blogic\b|boolean|flip.?flop|shift register|sequenc|\bburst|\bpulses?\b|\btimer|ratchet")),
        (Role.Pitch, Rx(@"quanti[sz]|circle of fifths|\bnotes?\b|(musical|polyphonic|user-defined|additional|intonation) scales?\b|\bscales for\b|\bchords?\b|\boctaves?\b|\bpitch|harmon|transpos|\bintervals?\b|arpegg|v/oct|\bintonation\b")),
        (Role.Mixer, Rx(@"\bmixer\b|\bmixes\b")),
        (Role.Modifier, Rx(@"vactrol|stereo spread|stereo image|waveshap|preamp|\bfades? (audio|signals?)\b|\bpan(ner|ning)?\b|\bgain\b|\bvca\b|amplif|stereo width|mid.?side|distort|\bfilter|ring mod|\bfold|crossfad|cross-fad|\bclip")),
        (Role.Controller, Rx(@"minimum|maximum|arithmetic|\bmath\b|multiplier|integrator|accumulat|\bglide|invert|\badder\b|dc voltage|voltage generator|fixed (output )?voltages|voltage range|range adapter|attenu|\boffsets?\b|\bconstants?\b|\blfo|envelope|\brandom|\bslew|\bcv\b|rescal|min/max|compar|\bknobs?\b|\bsliders?\b|\bfaders?\b|\bmacro")),
        (Role.Source, Rx(@"oscillator|\bnoise\b|sample (player|playback)|\bsampler\b|\bdrums?\b|\bsynth voice")),
        (Role.Effect, Rx(@"vocoder|masteriz|\breverb|\bdelay\b|\becho\b|\bchorus|\beffects?\b")),
    };

    /// <summary>The role the description points to; null when it says nothing usable.</summary>
    public static Role? Guess(string? description)
    {
        // A blank panel or a separator has no ports: nothing to place.
        if (string.IsNullOrWhiteSpace(description) || blank.IsMatch(description))
        {
            return null;
        }
        foreach ((Role role, Regex pattern) in rules)
        {
            if (pattern.IsMatch(description))
            {
                return role;
            }
        }
        return null;
    }

    private static readonly Regex blank = Rx(@"\bblank\b|\bseparator\b");

    private static Regex Rx(string pattern) => new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(200));
}