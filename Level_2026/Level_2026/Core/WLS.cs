using Level_2026.Core;
using Level_2026.Models;
using MathNet.Numerics.LinearAlgebra;

public static class WLS
{
    public static AdjustmentResult Adjust(
        List<Observation> observations,
        Dictionary<string, double> fixedPoints,
        WeightType weightType)
    {
        if (fixedPoints.Count == 0)
            throw new Exception("Inserire almeno un caposaldo");

        // =========================
        // 1. FILTRO RETE CONNESSA
        // =========================
        var connected = GetConnectedNodes(observations, fixedPoints.Keys.ToHashSet());

        var usedObs = observations
            .Where(o => connected.Contains(o.From) && connected.Contains(o.To))
            .ToList();

        if (usedObs.Count == 0)
            throw new Exception("Nessuna osservazione connessa");

        // =========================
        // 2. LISTA NODI
        // =========================
        var nodes = usedObs
            .SelectMany(o => new[] { o.From, o.To })
            .Distinct()
            .ToList();

        var unknowns = nodes
            .Where(n => !fixedPoints.ContainsKey(n))
            .ToList();

        int m = usedObs.Count;
        int n = unknowns.Count;

        if (n == 0)
            throw new Exception("Nessuna incognita");

        var index = unknowns
            .Select((name, i) => new { name, i })
            .ToDictionary(x => x.name, x => x.i);

        var A = Matrix<double>.Build.Dense(m, n);
        var b = Vector<double>.Build.Dense(m);
        var w = Vector<double>.Build.Dense(m);

        // =========================
        // 3. COSTRUZIONE SISTEMA
        // =========================
        for (int i = 0; i < m; i++)
        {
            var o = usedObs[i];

            double rhs = o.Dh;

            // TO
            if (fixedPoints.ContainsKey(o.To))
                rhs -= fixedPoints[o.To];
            else
                A[i, index[o.To]] += 1;

            // FROM
            if (fixedPoints.ContainsKey(o.From))
                rhs += fixedPoints[o.From];
            else
                A[i, index[o.From]] -= 1;

            b[i] = rhs;

            double d = Math.Max(o.Dist, 1e-12);

            w[i] = weightType switch
            {
                WeightType.InverseDistance => 1.0 / d,
                WeightType.InverseDistanceSquared => 1.0 / (d * d),
                _ => 1
            };
        }

        // =========================
        // 4. RISOLUZIONE WLS
        // =========================
        var W = Matrix<double>.Build.DenseDiagonal(m, m, i => w[i]);

        var N = A.Transpose() * W * A;
        var u = A.Transpose() * W * b;

        Vector<double> x;

        try
        {
            x = N.Solve(u);
        }
        catch
        {
            double lambda = 1e6;

            var extraRow = Matrix<double>.Build.Dense(1, n, 1.0);
            var A2 = A.Stack(extraRow);

            var b2 = Vector<double>.Build.DenseOfEnumerable(b.Concat(new[] { 0.0 }));
            var w2 = Vector<double>.Build.DenseOfEnumerable(w.Concat(new[] { lambda }));

            var W2 = Matrix<double>.Build.DenseDiagonal(w2.Count, w2.Count, i => w2[i]);

            var N2 = A2.Transpose() * W2 * A2;
            var u2 = A2.Transpose() * W2 * b2;

            x = N2.Solve(u2);
        }

        // residui
        var v = A * x - b;

        double sigma0 = Math.Sqrt(
            v.PointwiseMultiply(w).PointwiseMultiply(v).Sum() / Math.Max(m - n, 1)
        );

        // =========================
        // 6. OUTPUT
        // =========================
        var heights = new Dictionary<string, double>(fixedPoints);

        for (int i = 0; i < n; i++)
            heights[unknowns[i]] = x[i];

        var residuals = new List<Residual>();

        for (int i = 0; i < m; i++)
        {
            var o = usedObs[i];

            residuals.Add(new Residual
            {
                Line = o.Line,
                From = o.From,
                To = o.To,
                Dh = o.Dh,
                Dist = o.Dist,
                V = v[i],
                W = w[i]
            });
        }

        return new AdjustmentResult
        {
            Heights = heights,
            Residuals = residuals,
            Sigma0 = sigma0,
            UnknownCount = n,
            UsedObservations = m
        };
    }

    // =========================
    // GRAFO CONNESSO
    // =========================
    private static HashSet<string> GetConnectedNodes(List<Observation> obs, HashSet<string> fixedNodes)
    {
        var graph = new Dictionary<string, HashSet<string>>();

        foreach (var o in obs)
        {
            if (!graph.ContainsKey(o.From)) graph[o.From] = new();
            if (!graph.ContainsKey(o.To)) graph[o.To] = new();

            graph[o.From].Add(o.To);
            graph[o.To].Add(o.From);
        }

        var visited = new HashSet<string>();
        var stack = new Stack<string>(fixedNodes);

        while (stack.Count > 0)
        {
            var n = stack.Pop();
            if (!visited.Add(n)) continue;

            if (!graph.ContainsKey(n)) continue;

            foreach (var nb in graph[n])
                if (!visited.Contains(nb))
                    stack.Push(nb);
        }

        return visited;
    }
}