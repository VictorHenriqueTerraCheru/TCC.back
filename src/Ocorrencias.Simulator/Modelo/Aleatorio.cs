namespace Ocorrencias.Simulator.Modelo;

/// <summary>Sorteios sobre um <see cref="Random"/> com semente explícita (determinístico, RNF06).</summary>
public static class Aleatorio
{
    /// <summary>
    /// Sub-semente derivada por fórmula fixa. Nunca use <see cref="HashCode"/>: ele é aleatorizado por processo.
    /// </summary>
    public static int Derivar(int semente, params int[] partes)
    {
        unchecked
        {
            var h = (uint)semente * 2654435761u + 0x9E3779B9u;
            foreach (var p in partes)
            {
                h ^= (uint)p + 0x9E3779B9u + (h << 6) + (h >> 2);
                h *= 2246822519u;
            }

            return (int)(h & 0x7FFFFFFF);
        }
    }

    public static bool Chance(this Random rnd, double p) => rnd.NextDouble() < p;

    public static double Uniforme(this Random rnd, double min, double max) => min + rnd.NextDouble() * (max - min);

    /// <summary>Exponencial com a média dada (inversão).</summary>
    public static double Exponencial(this Random rnd, double media) => -media * Math.Log(1 - rnd.NextDouble());

    /// <summary>Distância de um ponto uniforme num disco de raio <paramref name="raio"/> até o centro.</summary>
    public static double DistanciaNoDisco(this Random rnd, double raio) => raio * Math.Sqrt(rnd.NextDouble());

    /// <summary>Binomial(n, p) por inversão da CDF; eficiente para n·p pequeno, como nas passagens por hora.</summary>
    public static int Binomial(this Random rnd, int n, double p)
    {
        if (n <= 0 || p <= 0)
        {
            return 0;
        }

        if (p >= 1)
        {
            return n;
        }

        var u = rnd.NextDouble();
        var q = 1 - p;
        var prob = Math.Pow(q, n);
        var acumulada = prob;
        var k = 0;
        while (u > acumulada && k < n)
        {
            prob *= (double)(n - k) / (k + 1) * p / q;
            k++;
            acumulada += prob;
        }

        return k;
    }

    /// <summary>Sorteia <paramref name="k"/> índices distintos de [0, n) (Fisher–Yates parcial sobre uma cópia).</summary>
    public static int[] Amostra(this Random rnd, int n, int k)
    {
        var indices = new int[n];
        for (var i = 0; i < n; i++)
        {
            indices[i] = i;
        }

        k = Math.Min(k, n);
        for (var i = 0; i < k; i++)
        {
            var j = rnd.Next(i, n);
            (indices[i], indices[j]) = (indices[j], indices[i]);
        }

        return indices[..k];
    }
}
