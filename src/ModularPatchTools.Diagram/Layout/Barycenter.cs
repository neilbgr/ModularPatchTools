namespace ModularPatchTools.Diagram.Layout;

/// <summary>
/// Crossing reduction of a layered drawing by the barycenter heuristic (Sugiyama, Tagawa &amp; Toda, 1981):
/// each element of a column moves to the average position of its neighbours in the column just swept,
/// sweeping left to right then right to left, and the order with the fewest crossings wins.
/// Elements never leave their lane: the sort is by lane first.
/// A segment can start or end below the top of its element (an insert stacked under its host): its offsets, fractions
/// of a position, say where, so what it links to lines up with it rather than with the host.
/// </summary>
public static class Barycenter
{
    private const int sweeps = 8;

    /// <param name="columns">Initial order of the elements of each column.</param>
    /// <param name="segments">Links between elements of adjacent columns, from left (U) to right (V).</param>
    /// <param name="offsets">Per segment, where it leaves U and enters V below their top, in [0, 1); none by default.</param>
    public static List<List<string>> Order(List<List<string>> columns, List<(string U, string V)> segments, Func<string, int> laneOf, IReadOnlyList<(double U, double V)>? offsets = null)
    {
        (double U, double V) Offset(int i) => offsets is null ? (0, 0) : offsets[i];
        Dictionary<string, List<(string Element, double Offset)>> left = segments.Select((s, i) => (s, i)).GroupBy(p => p.s.V)
            .ToDictionary(g => g.Key, g => g.Select(p => (p.s.U, Offset(p.i).U)).ToList());
        Dictionary<string, List<(string Element, double Offset)>> right = segments.Select((s, i) => (s, i)).GroupBy(p => p.s.U)
            .ToDictionary(g => g.Key, g => g.Select(p => (p.s.V, Offset(p.i).V)).ToList());

        List<List<string>> current = columns.Select(c => c.OrderBy(laneOf).ToList()).ToList();
        List<List<string>> best = Copy(current);
        int fewest = Crossings(best, segments, offsets);
        for (int sweep = 0; sweep < sweeps && fewest > 0; sweep++)
        {
            bool forward = sweep % 2 == 0;
            Dictionary<string, List<(string Element, double Offset)>> neighbours = forward ? left : right;
            IEnumerable<int> order = forward ? Enumerable.Range(1, current.Count - 1) : Enumerable.Range(0, current.Count - 1).Reverse();
            // Positions as they are when each column is sorted: only the column just sorted changes.
            Dictionary<string, int> at = Positions(current);
            foreach (int col in order)
            {
                current[col] = current[col]
                    .OrderBy(laneOf)
                    .ThenBy(e => neighbours.TryGetValue(e, out List<(string Element, double Offset)>? next) ? next.Average(n => at[n.Element] + n.Offset) : at[e])
                    .ThenBy(e => at[e])
                    .ToList();
                for (int i = 0; i < current[col].Count; i++)
                {
                    at[current[col][i]] = i;
                }
            }
            int crossings = Crossings(current, segments, offsets);
            if (crossings < fewest)
            {
                fewest = crossings;
                best = Copy(current);
            }
        }
        return best;
    }

    /// <summary>Pairs of segments between the same two columns whose ends are in opposite orders.</summary>
    public static int Crossings(List<List<string>> columns, List<(string U, string V)> segments, IReadOnlyList<(double U, double V)>? offsets = null)
    {
        Dictionary<string, int> at = Positions(columns);
        Dictionary<string, int> columnOf = columns.SelectMany((c, i) => c.Select(e => (e, i))).ToDictionary(p => p.e, p => p.i);
        int count = 0;
        foreach (IGrouping<int, int> layer in Enumerable.Range(0, segments.Count).GroupBy(i => columnOf[segments[i].U]))
        {
            List<(double U, double V)> list = layer.Select(i => (at[segments[i].U] + (offsets?[i].U ?? 0), at[segments[i].V] + (offsets?[i].V ?? 0))).ToList();
            for (int i = 0; i < list.Count; i++)
            {
                for (int j = i + 1; j < list.Count; j++)
                {
                    if ((list[i].U - list[j].U) * (list[i].V - list[j].V) < 0)
                    {
                        count++;
                    }
                }
            }
        }
        return count;
    }

    private static Dictionary<string, int> Positions(List<List<string>> columns) =>
        columns.SelectMany(c => c.Select((e, i) => (e, i))).ToDictionary(p => p.e, p => p.i);

    private static List<List<string>> Copy(List<List<string>> columns) => columns.Select(c => c.ToList()).ToList();
}