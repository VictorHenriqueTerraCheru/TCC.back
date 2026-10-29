using UrbanReports.Api.Common;

namespace UrbanReports.Api.Saude;

public static class ErrosSaude
{
    public static readonly ErroAplicacao BancoIndisponivel = ErroAplicacao.Regra(
        StatusCodes.Status503ServiceUnavailable, "banco-indisponivel", "Banco de dados indisponível.");
}
