namespace VcvPatchTools.Core.Catalog;

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
}