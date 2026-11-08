namespace ModularPatchTools.Diagram.Layout;

/// <summary>
/// Crossing reduction of a layered drawing by the barycenter heuristic (Sugiyama, Tagawa &amp; Toda, 1981):
/// each element of a column moves to the average position of its neighbours in the column just swept,
/// sweeping left to right then right to left, and the order with the lowest cost wins. Then neighbours in a column
/// swap places while that lowers the cost (the "transpose" refinement), which the averages alone miss.
/// Elements never leave their lane: the sort is by lane first.
/// A segment can start or end below the top of its element (an insert stacked under its host): its offsets, fractions
/// of a position, say where, so what it links to lines up with it rather than with the host.
/// </summary>
public static class Barycenter
{
    private const int sweeps = 8;

    /// <summary>
    /// What a crossing costs more when one of its two segments links two boxes directly: such a crossing sits between
    /// port tabs and breaks a chain the eye follows, where one between long cables on their way is easily read across.
    /// </summary>
    private const int directCrossingCost = 3;

    /// <param name="columns">Initial order of the elements of each column.</param>
    /// <param name="segments">Links between elements of adjacent columns, from left (U) to right (V).</param>
    /// <param name="laneOf">Lane of each element.</param>
    /// <param name="offsets">Per segment, where it leaves U and enters V below their top, in [0, 1); none by default.</param>
    /// <param name="isTrack">Whether an element is a cable passing a column rather than a box; none by default.</param>
    public static List<List<string>> Order(List<List<string>> columns, List<(string U, string V)> segments, Func<string, int> laneOf, IReadOnlyList<(double U, double V)>? offsets = null, Func<string, bool>? isTrack = null)
    {
        Links links = new Links(segments, offsets, isTrack);
        List<List<string>> current = columns.Select(c => c.OrderBy(laneOf).ToList()).ToList();
        List<List<string>> best = Copy(current);
        int lowest = Cost(best, links);
        for (int sweep = 0; sweep < sweeps && lowest > 0; sweep++)
        {
            bool forward = sweep % 2 == 0;
            Dictionary<string, List<End>> neighbours = forward ? links.Left : links.Right;
            IEnumerable<int> order = forward ? Enumerable.Range(1, current.Count - 1) : Enumerable.Range(0, current.Count - 1).Reverse();
            // Positions as they are when each column is sorted: only the column just sorted changes.
            Dictionary<string, int> at = Positions(current);
            foreach (int col in order)
            {
                current[col] = current[col]
                    .OrderBy(laneOf)
                    .ThenBy(e => neighbours.TryGetValue(e, out List<End>? next) ? next.Average(n => at[n.Element] + n.Offset) : at[e])
                    .ThenBy(e => at[e])
                    .ToList();
                for (int i = 0; i < current[col].Count; i++)
                {
                    at[current[col][i]] = i;
                }
            }
            int cost = Cost(current, links);
            if (cost < lowest)
            {
                lowest = cost;
                best = Copy(current);
            }
        }
        Transpose(best, links, laneOf);
        return best;
    }

    /// <summary>Pairs of segments between the same two columns whose ends are in opposite orders.</summary>
    public static int Crossings(List<List<string>> columns, List<(string U, string V)> segments, IReadOnlyList<(double U, double V)>? offsets = null) =>
        Cost(columns, new Links(segments, offsets, null), weighted: false);

    /// <summary>The crossings, each costing more when it involves a direct link (see <see cref="directCrossingCost"/>).</summary>
    private static int Cost(List<List<string>> columns, Links links, bool weighted = true)
    {
        Dictionary<string, int> at = Positions(columns);
        Dictionary<string, int> columnOf = columns.SelectMany((c, i) => c.Select(e => (e, i))).ToDictionary(p => p.e, p => p.i);
        int cost = 0;
        foreach (IGrouping<int, int> layer in Enumerable.Range(0, links.Segments.Count).GroupBy(i => columnOf[links.Segments[i].U]))
        {
            List<(double U, double V, bool Direct)> list = layer
                .Select(i => (at[links.Segments[i].U] + links.Offset(i).U, at[links.Segments[i].V] + links.Offset(i).V, links.IsDirect(i)))
                .ToList();
            for (int i = 0; i < list.Count; i++)
            {
                for (int j = i + 1; j < list.Count; j++)
                {
                    if ((list[i].U - list[j].U) * (list[i].V - list[j].V) < 0)
                    {
                        cost += weighted ? Pair(list[i].Direct, list[j].Direct) : 1;
                    }
                }
            }
        }
        return cost;
    }

    private static int Pair(bool direct, bool other) => 1 + (directCrossingCost * ((direct ? 1 : 0) + (other ? 1 : 0)));

    /// <summary>
    /// Swaps two neighbours of a column (in one lane) whenever that lowers the cost, until no swap does. Only the pairs of
    /// segments with one end on each of the two change: those are the only ones counted.
    /// </summary>
    private static void Transpose(List<List<string>> columns, Links links, Func<string, int> laneOf)
    {
        Dictionary<string, int> at = Positions(columns);

        int Crossed(string upper, string lower, Dictionary<string, List<End>> side)
        {
            if (!side.TryGetValue(upper, out List<End>? ups) || !side.TryGetValue(lower, out List<End>? downs))
            {
                return 0;
            }
            int cost = 0;
            foreach (End up in ups)
            {
                foreach (End down in downs)
                {
                    if (at[up.Element] + up.Offset > at[down.Element] + down.Offset)
                    {
                        cost += Pair(up.Direct, down.Direct);
                    }
                }
            }
            return cost;
        }

        for (bool improved = true; improved;)
        {
            improved = false;
            foreach (List<string> column in columns)
            {
                for (int i = 0; i + 1 < column.Count; i++)
                {
                    string upper = column[i];
                    string lower = column[i + 1];
                    if (laneOf(upper) != laneOf(lower))
                    {
                        continue;
                    }
                    int now = Crossed(upper, lower, links.Left) + Crossed(upper, lower, links.Right);
                    int swapped = Crossed(lower, upper, links.Left) + Crossed(lower, upper, links.Right);
                    if (swapped < now)
                    {
                        column[i] = lower;
                        column[i + 1] = upper;
                        at[lower] = i;
                        at[upper] = i + 1;
                        improved = true;
                    }
                }
            }
        }
    }

    /// <summary>The far end of a segment, seen from one of its elements, with the offset at that far end.</summary>
    private sealed record End(string Element, double Offset, bool Direct);

    /// <summary>The segments, with their offsets, whether each links two boxes directly, and each element's neighbours.</summary>
    private sealed class Links
    {
        private readonly IReadOnlyList<(double U, double V)>? offsets;
        private readonly Func<string, bool>? isTrack;

        public Links(List<(string U, string V)> segments, IReadOnlyList<(double U, double V)>? offsets, Func<string, bool>? isTrack)
        {
            Segments = segments;
            this.offsets = offsets;
            this.isTrack = isTrack;
            for (int i = 0; i < segments.Count; i++)
            {
                Add(Left, segments[i].V, new End(segments[i].U, Offset(i).U, IsDirect(i)));
                Add(Right, segments[i].U, new End(segments[i].V, Offset(i).V, IsDirect(i)));
            }
        }

        public List<(string U, string V)> Segments { get; }

        /// <summary>Per element, the far ends of its segments on its left.</summary>
        public Dictionary<string, List<End>> Left { get; } = new Dictionary<string, List<End>>();

        /// <summary>Per element, the far ends of its segments on its right.</summary>
        public Dictionary<string, List<End>> Right { get; } = new Dictionary<string, List<End>>();

        public (double U, double V) Offset(int i) => offsets is null ? (0, 0) : offsets[i];

        public bool IsDirect(int i) => isTrack is not null && !isTrack(Segments[i].U) && !isTrack(Segments[i].V);

        private static void Add(Dictionary<string, List<End>> map, string key, End end)
        {
            if (!map.TryGetValue(key, out List<End>? list))
            {
                list = new List<End>();
                map[key] = list;
            }
            list.Add(end);
        }
    }

    private static Dictionary<string, int> Positions(List<List<string>> columns) =>
        columns.SelectMany(c => c.Select((e, i) => (e, i))).ToDictionary(p => p.e, p => p.i);

    private static List<List<string>> Copy(List<List<string>> columns) => columns.Select(c => c.ToList()).ToList();
}