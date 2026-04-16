using Level_2026.Models;
using MathNet.Numerics.LinearAlgebra;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Level_2026.Core
{
    public class WLSResult
    {
        public Dictionary<string, double> Heights { get; set; } = new();
        public double Sigma0 { get; set; }
    }

    public static class WLS
    {
        public static WLSResult Adjust(
            List<Observation> observations,
            Dictionary<string, double> fixedPoints)
        {
            // ----------------------------
            // 1. NODI E INCOGNITE
            // ----------------------------
            var nodes = observations
                .SelectMany(o => new[] { o.From, o.To })
                .Distinct()
                .ToList();

            var unknowns = nodes
                .Where(node => !fixedPoints.ContainsKey(node))
                .ToList();

            int obsCount = observations.Count;
            int unknownCount = unknowns.Count;

            var index = unknowns
                .Select((name, i) => new { name, i })
                .ToDictionary(x => x.name, x => x.i);

            // ----------------------------
            // 2. MATRICI
            // ----------------------------
            var A = Matrix<double>.Build.Dense(obsCount, unknownCount);
            var b = Vector<double>.Build.Dense(obsCount);
            var w = Vector<double>.Build.Dense(obsCount);

            for (int i = 0; i < obsCount; i++)
            {
                var o = observations[i];

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

                // PESI (qui puoi cambiare logica)
                double d = Math.Max(o.Dist, 1e-12);
                w[i] = 1.0 / d;
            }

            // ----------------------------
            // 3. WLS
            // ----------------------------
            var W = Matrix<double>.Build.DiagonalOfDiagonalVector(w);
            var N = A.TransposeThisAndMultiply(W) * A;
            var u = A.TransposeThisAndMultiply(W) * b;

            var solution = N.Solve(u);

            // ----------------------------
            // 4. QUOTE
            // ----------------------------
            var heights = new Dictionary<string, double>(fixedPoints);

            for (int i = 0; i < unknownCount; i++)
                heights[unknowns[i]] = solution[i];

            // ----------------------------
            // 5. RESIDUI + SIGMA0
            // ----------------------------
            var residuals = A * solution - b;

            double sigma0 = Math.Sqrt(
                (residuals.PointwiseMultiply(w)).Sum() /
                Math.Max(obsCount - unknownCount, 1)
            );

            return new WLSResult
            {
                Heights = heights,
                Sigma0 = sigma0
            };
        }
    }
}