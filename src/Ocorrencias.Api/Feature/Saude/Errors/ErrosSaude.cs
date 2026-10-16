using Ocorrencias.Api.Common;

namespace Ocorrencias.Api.Saude;

public static class ErrosSaude
{
    /// <summary>503: é o status que balanceadores e hosts entendem como "fora do ar".</summary>
    public static readonly ErroAplicacao BancoIndisponivel = ErroAplicacao.Regra(
        StatusCodes.Status503ServiceUnavailable, "banco-indisponivel", "Banco de dados indisponível.");
}
