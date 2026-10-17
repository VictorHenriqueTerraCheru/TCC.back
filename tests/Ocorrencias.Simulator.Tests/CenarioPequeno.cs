using Ocorrencias.Simulator.Modelo;

namespace Ocorrencias.Simulator.Tests;

internal static class CenarioPequeno
{
    /// <summary>Cenário reduzido para os testes rodarem em milissegundos.</summary>
    public static readonly Cenario Valor = new()
    {
        Usuarios = 150,
        Problemas = 20,
        Dias = 8,
        DiasCriacao = 4,
        KMaximo = 6,
        DiaAtaque = 1,
    };
}
