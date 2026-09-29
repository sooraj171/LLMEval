namespace LLMEval;

/// <summary>Cosine similarity for embedding vectors (float32).</summary>
public static class VectorMath
{
    public static double CosineSimilarity(float[]? a, float[]? b)
    {
        if (a == null || b == null || a.Length == 0 || b.Length == 0)
            return 0;

        var n = Math.Min(a.Length, b.Length);
        double dot = 0, mag1 = 0, mag2 = 0;
        for (var i = 0; i < n; i++)
        {
            var x = a[i];
            var y = b[i];
            dot += x * (double)y;
            mag1 += x * (double)x;
            mag2 += y * (double)y;
        }

        if (mag1 == 0 || mag2 == 0)
            return 0;
        return dot / (Math.Sqrt(mag1) * Math.Sqrt(mag2));
    }
}
