using ModularPatchTools.Core.Patch;

namespace ModularPatchTools.Diagram.Analysis;

/// <summary>
/// Modules that carry signals without a cable, drawn as the direct cables they stand for.
/// Venom Bay: a Bay Output plays, port by port, what its source Bay Input receives (the source is picked in its menu
/// and saved as data.srcId); a Bay Norm does the same, but an unconnected source port falls back to its own input.
/// Each cable leaving a Bay Output / Bay Norm becomes a cable from the module that feeds the matching port, keeping
/// its id and color; the bays, left without cables, drop out of the diagram like any cableless module.
/// A port whose source is left unplugged carries nothing, so its cable goes; a bay with no source set (or a source
/// missing from the patch) keeps its cables as they are.
/// </summary>
public static class WirelessLinks
{
    private const string venom = "Venom";

    public static PatchDocument Bypass(PatchDocument patch)
    {
        Dictionary<long, PatchModule> byId = new Dictionary<long, PatchModule>();
        foreach (PatchModule module in patch.Modules)
        {
            byId.TryAdd(module.Id, module);
        }
        bool IsBayInput(long id) => byId.TryGetValue(id, out PatchModule? m) && m.Plugin == venom && m.Model == "BayInput";
        bool IsNorm(long id) => byId.TryGetValue(id, out PatchModule? m) && m.Plugin == venom && m.Model == "BayNorm";
        long? SourceOf(long id) => byId.TryGetValue(id, out PatchModule? m) && m.Plugin == venom && m.Model is "BayOutput" or "BayNorm"
            && m.LinkedSourceId is long source && IsBayInput(source) ? source : null;

        HashSet<long> linkedInputs = patch.Modules.Select(m => SourceOf(m.Id)).OfType<long>().ToHashSet();
        if (linkedInputs.Count == 0)
        {
            return patch;
        }

        // What plugs into each input port; Cardinal keeps one cable per input, Rack sums several: the first one stands for them.
        Dictionary<PortRef, PatchCable> feeding = new Dictionary<PortRef, PatchCable>();
        foreach (PatchCable cable in patch.Cables)
        {
            feeding.TryAdd(cable.To, cable);
        }

        // The output port that really drives a bay's output, following bays chained into bays.
        PortRef? Driver(PortRef output, HashSet<long> seen)
        {
            if (SourceOf(output.ModuleId) is not long source)
            {
                return output;
            }
            if (!seen.Add(output.ModuleId))
            {
                return null;
            }
            PatchCable? upstream = feeding.GetValueOrDefault(new PortRef(source, output.PortId))
                ?? (IsNorm(output.ModuleId) ? feeding.GetValueOrDefault(new PortRef(output.ModuleId, output.PortId)) : null);
            return upstream is null ? null : Driver(upstream.From, seen);
        }

        List<PatchCable> cables = new List<PatchCable>();
        foreach (PatchCable cable in patch.Cables)
        {
            // Into a linked Bay Input or a linked Bay Norm: those signals now leave by the cables they feed.
            if (linkedInputs.Contains(cable.To.ModuleId) || (IsNorm(cable.To.ModuleId) && SourceOf(cable.To.ModuleId) is not null))
            {
                continue;
            }
            if (SourceOf(cable.From.ModuleId) is null)
            {
                cables.Add(cable);
                continue;
            }
            if (Driver(cable.From, new HashSet<long>()) is PortRef driver)
            {
                cables.Add(cable with { From = driver });
            }
        }
        return patch with { Cables = cables };
    }
}