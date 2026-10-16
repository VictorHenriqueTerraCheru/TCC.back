using Ocorrencias.Api.Common;

namespace Ocorrencias.Api.Incidentes;

public static class ErrosIncidente
{
    public static readonly ErroAplicacao CategoriaInexistente = ErroAplicacao.DeCampo("categoriaId", "Categoria inexistente.");

    public static readonly ErroAplicacao CategoriaNaoEIncidente = ErroAplicacao.Regra(
        StatusCodes.Status422UnprocessableEntity, "categoria-nao-e-incidente", "A categoria escolhida é de problema, não de incidente.");

    public static readonly ErroAplicacao HorarioForaDaJanela = ErroAplicacao.Regra(
        StatusCodes.Status422UnprocessableEntity, "horario-fora-da-janela", "O horário do incidente precisa estar nas últimas 24 horas.");

    public static readonly ErroAplicacao LimiteDiarioIncidentes = ErroAplicacao.Regra(
        StatusCodes.Status429TooManyRequests, "limite-diario-incidentes", "Limite de 10 incidentes em 24 horas atingido.");
}
