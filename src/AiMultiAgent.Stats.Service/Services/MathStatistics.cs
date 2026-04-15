using AiMultiAgent.Stats.Contracts.Dashboard;

namespace AiMultiAgent.Stats.Service.Services;

public static class MathStatistics
{
    public static NumericSummaryDto Summarize(IEnumerable<double> source)
    {
        var values = source
            .Where(x => !double.IsNaN(x) && !double.IsInfinity(x))
            .OrderBy(x => x)
            .ToArray();

        if (values.Length == 0)
        {
            return new NumericSummaryDto();
        }

        var count = values.Length;
        var sum = values.Sum();
        var mean = sum / count;
        var median = Quantile(values, 0.5);
        var min = values[0];
        var max = values[^1];
        var variance = SampleVariance(values, mean);
        var stdDev = Math.Sqrt(Math.Max(0, variance));
        var p05 = Quantile(values, 0.05);
        var p25 = Quantile(values, 0.25);
        var p75 = Quantile(values, 0.75);
        var p95 = Quantile(values, 0.95);
        var iqr = p75 - p25;
        var mad = MedianAbsoluteDeviation(values, median);
        var skewness = Skewness(values, mean, stdDev);
        var kurtosis = KurtosisExcess(values, mean, stdDev);
        var jb = JarqueBera(values.Length, skewness, kurtosis);
        var ci = MeanConfidence95(mean, stdDev, count);

        return new NumericSummaryDto
        {
            Count = count,
            Sum = sum,
            Mean = mean,
            Median = median,
            Min = min,
            Max = max,
            Range = max - min,
            Variance = variance,
            StandardDeviation = stdDev,
            CoefficientOfVariation = NearlyZero(mean) ? 0 : stdDev / Math.Abs(mean),
            Percentile05 = p05,
            Percentile25 = p25,
            Percentile75 = p75,
            Percentile95 = p95,
            Iqr = iqr,
            Mad = mad,
            Skewness = skewness,
            KurtosisExcess = kurtosis,
            JarqueBera = jb,
            MeanConfidence95 = ci
        };
    }

    public static ConfidenceIntervalDto MeanConfidence95(double mean, double stdDev, int count)
    {
        if (count <= 1)
        {
            return new ConfidenceIntervalDto { Lower = mean, Upper = mean };
        }

        var margin = 1.96 * stdDev / Math.Sqrt(count);
        return new ConfidenceIntervalDto
        {
            Lower = mean - margin,
            Upper = mean + margin
        };
    }

    public static ConfidenceIntervalDto WilsonConfidence95(int successCount, int total)
    {
        if (total <= 0)
        {
            return new ConfidenceIntervalDto();
        }

        const double z = 1.96;
        var phat = successCount / (double)total;
        var denom = 1 + z * z / total;
        var centre = phat + z * z / (2 * total);
        var margin = z * Math.Sqrt((phat * (1 - phat) + z * z / (4 * total)) / total);

        return new ConfidenceIntervalDto
        {
            Lower = Math.Max(0, (centre - margin) / denom),
            Upper = Math.Min(1, (centre + margin) / denom)
        };
    }

    public static HistogramDto Histogram(string name, IEnumerable<double> source)
    {
        var values = source
            .Where(x => !double.IsNaN(x) && !double.IsInfinity(x))
            .OrderBy(x => x)
            .ToArray();

        if (values.Length == 0)
        {
            return new HistogramDto { Name = name };
        }

        var min = values[0];
        var max = values[^1];

        if (NearlyEqual(min, max))
        {
            return new HistogramDto
            {
                Name = name,
                Buckets =
                [
                    new HistogramBucketDto
                    {
                        FromInclusive = min,
                        ToExclusive = max,
                        Count = values.Length
                    }
                ]
            };
        }

        var bins = Math.Max(5, Math.Min(20, (int)Math.Ceiling(Math.Log2(values.Length) + 1)));
        var width = (max - min) / bins;

        var buckets = new List<HistogramBucketDto>(bins);
        for (var i = 0; i < bins; i++)
        {
            buckets.Add(new HistogramBucketDto
            {
                FromInclusive = min + width * i,
                ToExclusive = i == bins - 1 ? max + 0.0000001 : min + width * (i + 1),
                Count = 0
            });
        }

        foreach (var value in values)
        {
            var idx = (int)Math.Min(bins - 1, Math.Floor((value - min) / width));
            var current = buckets[idx];
            buckets[idx] = new HistogramBucketDto
            {
                FromInclusive = current.FromInclusive,
                ToExclusive = current.ToExclusive,
                Count = current.Count + 1
            };
        }

        return new HistogramDto { Name = name, Buckets = buckets };
    }

    public static DistributionDto Distribution(string name, IDictionary<string, double> values)
    {
        var total = values.Values.Sum();
        var items = values
            .OrderByDescending(x => x.Value)
            .Select(x => new DistributionItemDto
            {
                Key = x.Key,
                Value = x.Value,
                Share = total <= 0 ? 0 : x.Value / total
            })
            .ToList();

        return new DistributionDto
        {
            Name = name,
            Entropy = Entropy(items.Select(x => x.Share)),
            Items = items
        };
    }

    public static double Entropy(IEnumerable<double> probabilities)
    {
        var sum = 0d;
        foreach (var p in probabilities.Where(x => x > 0))
        {
            sum -= p * Math.Log(p, 2);
        }

        return sum;
    }

    public static double Pearson(IReadOnlyList<double> xs, IReadOnlyList<double> ys)
    {
        if (xs.Count == 0 || ys.Count == 0 || xs.Count != ys.Count)
        {
            return 0;
        }

        var meanX = xs.Average();
        var meanY = ys.Average();

        double numerator = 0;
        double xDen = 0;
        double yDen = 0;

        for (var i = 0; i < xs.Count; i++)
        {
            var dx = xs[i] - meanX;
            var dy = ys[i] - meanY;
            numerator += dx * dy;
            xDen += dx * dx;
            yDen += dy * dy;
        }

        if (NearlyZero(xDen) || NearlyZero(yDen))
        {
            return 0;
        }

        return numerator / Math.Sqrt(xDen * yDen);
    }

    public static double Spearman(IReadOnlyList<double> xs, IReadOnlyList<double> ys)
    {
        if (xs.Count == 0 || ys.Count == 0 || xs.Count != ys.Count)
        {
            return 0;
        }

        var rx = Ranks(xs);
        var ry = Ranks(ys);
        return Pearson(rx, ry);
    }

    public static LinearTrendDto LinearTrend(IReadOnlyList<double> ys)
    {
        if (ys.Count == 0)
        {
            return new LinearTrendDto();
        }

        if (ys.Count == 1)
        {
            return new LinearTrendDto
            {
                Slope = 0,
                Intercept = ys[0],
                RSquared = 1,
                Direction = "flat",
                StartValue = ys[0],
                EndValue = ys[0]
            };
        }

        var xs = Enumerable.Range(0, ys.Count).Select(x => (double)x).ToArray();
        var meanX = xs.Average();
        var meanY = ys.Average();

        double numerator = 0;
        double denominator = 0;

        for (var i = 0; i < ys.Count; i++)
        {
            var dx = xs[i] - meanX;
            numerator += dx * (ys[i] - meanY);
            denominator += dx * dx;
        }

        var slope = NearlyZero(denominator) ? 0 : numerator / denominator;
        var intercept = meanY - slope * meanX;

        double sse = 0;
        double sst = 0;

        for (var i = 0; i < ys.Count; i++)
        {
            var predicted = intercept + slope * xs[i];
            sse += Math.Pow(ys[i] - predicted, 2);
            sst += Math.Pow(ys[i] - meanY, 2);
        }

        var r2 = NearlyZero(sst) ? 1 : Math.Max(0, 1 - sse / sst);

        var direction = slope switch
        {
            > 0.0001 => "up",
            < -0.0001 => "down",
            _ => "flat"
        };

        return new LinearTrendDto
        {
            Slope = slope,
            Intercept = intercept,
            RSquared = r2,
            Direction = direction,
            StartValue = ys[0],
            EndValue = ys[^1]
        };
    }

    public static double Gini(IEnumerable<double> source)
    {
        var values = source.Where(x => x >= 0).OrderBy(x => x).ToArray();
        if (values.Length == 0)
        {
            return 0;
        }

        var total = values.Sum();
        if (NearlyZero(total))
        {
            return 0;
        }

        double cum = 0;
        for (var i = 0; i < values.Length; i++)
        {
            cum += (2d * (i + 1) - values.Length - 1) * values[i];
        }

        return cum / (values.Length * total);
    }

    public static double Hhi(IEnumerable<double> source)
    {
        var values = source.Where(x => x > 0).ToArray();
        var total = values.Sum();
        if (NearlyZero(total))
        {
            return 0;
        }

        return values.Select(x => Math.Pow(x / total, 2)).Sum();
    }

    public static (double LowerFence, double UpperFence, HashSet<int> Indexes) IqrOutliers(IReadOnlyList<double> values)
    {
        if (values.Count == 0)
        {
            return (0, 0, []);
        }

        var sorted = values.OrderBy(x => x).ToArray();
        var q1 = Quantile(sorted, 0.25);
        var q3 = Quantile(sorted, 0.75);
        var iqr = q3 - q1;
        var lower = q1 - 1.5 * iqr;
        var upper = q3 + 1.5 * iqr;

        var indexes = new HashSet<int>();
        for (var i = 0; i < values.Count; i++)
        {
            if (values[i] < lower || values[i] > upper)
            {
                indexes.Add(i);
            }
        }

        return (lower, upper, indexes);
    }

    public static HashSet<int> ZScoreOutliers(IReadOnlyList<double> values, double threshold = 3.0)
    {
        if (values.Count == 0)
        {
            return [];
        }

        var mean = values.Average();
        var variance = SampleVariance(values.ToArray(), mean);
        var stdDev = Math.Sqrt(Math.Max(0, variance));

        if (NearlyZero(stdDev))
        {
            return [];
        }

        var result = new HashSet<int>();
        for (var i = 0; i < values.Count; i++)
        {
            var z = Math.Abs((values[i] - mean) / stdDev);
            if (z >= threshold)
            {
                result.Add(i);
            }
        }

        return result;
    }

    public static double MovingAverage(IReadOnlyList<double> values, int endInclusive, int window)
    {
        if (values.Count == 0 || endInclusive < 0)
        {
            return 0;
        }

        var start = Math.Max(0, endInclusive - window + 1);
        var slice = values.Skip(start).Take(endInclusive - start + 1).ToArray();
        return slice.Length == 0 ? 0 : slice.Average();
    }

    private static double Quantile(double[] sortedValues, double p)
    {
        if (sortedValues.Length == 0)
        {
            return 0;
        }

        if (sortedValues.Length == 1)
        {
            return sortedValues[0];
        }

        var position = (sortedValues.Length - 1) * p;
        var lower = (int)Math.Floor(position);
        var upper = (int)Math.Ceiling(position);

        if (lower == upper)
        {
            return sortedValues[lower];
        }

        var weight = position - lower;
        return sortedValues[lower] * (1 - weight) + sortedValues[upper] * weight;
    }

    private static double SampleVariance(double[] values, double mean)
    {
        if (values.Length <= 1)
        {
            return 0;
        }

        var sum = values.Sum(x => Math.Pow(x - mean, 2));
        return sum / (values.Length - 1);
    }

    private static double MedianAbsoluteDeviation(double[] values, double median)
    {
        var deviations = values.Select(x => Math.Abs(x - median)).OrderBy(x => x).ToArray();
        return Quantile(deviations, 0.5);
    }

    private static double Skewness(double[] values, double mean, double stdDev)
    {
        if (values.Length < 3 || NearlyZero(stdDev))
        {
            return 0;
        }

        var n = values.Length;
        var m3 = values.Sum(x => Math.Pow((x - mean) / stdDev, 3));
        return (double)n / ((n - 1d) * (n - 2d)) * m3;
    }

    private static double KurtosisExcess(double[] values, double mean, double stdDev)
    {
        if (values.Length < 4 || NearlyZero(stdDev))
        {
            return 0;
        }

        var n = values.Length;
        var z4 = values.Sum(x => Math.Pow((x - mean) / stdDev, 4));
        var a = (n * (n + 1d)) / ((n - 1d) * (n - 2d) * (n - 3d)) * z4;
        var b = (3d * Math.Pow(n - 1d, 2)) / ((n - 2d) * (n - 3d));
        return a - b;
    }

    private static double JarqueBera(int n, double skewness, double kurtosisExcess)
    {
        if (n <= 1)
        {
            return 0;
        }

        return n / 6d * (skewness * skewness + 0.25d * kurtosisExcess * kurtosisExcess);
    }

    private static double[] Ranks(IReadOnlyList<double> values)
    {
        var indexed = values
            .Select((value, index) => new { value, index })
            .OrderBy(x => x.value)
            .ToArray();

        var ranks = new double[values.Count];
        var i = 0;

        while (i < indexed.Length)
        {
            var j = i;
            while (j + 1 < indexed.Length && NearlyEqual(indexed[j + 1].value, indexed[i].value))
            {
                j++;
            }

            var avgRank = (i + j + 2) / 2d;
            for (var k = i; k <= j; k++)
            {
                ranks[indexed[k].index] = avgRank;
            }

            i = j + 1;
        }

        return ranks;
    }

    private static bool NearlyZero(double x) => Math.Abs(x) < 1e-12;
    private static bool NearlyEqual(double a, double b) => Math.Abs(a - b) < 1e-12;
}
