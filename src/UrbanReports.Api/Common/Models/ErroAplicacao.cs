namespace UrbanReports.Api.Common;

public enum TipoErro
{
    NaoEncontrado,
    NaoAutorizado,
    Campo,
    Regra,
}

public sealed record ErroAplicacao(TipoErro Tipo, int Status, string? Type, string? Title, string? Campo = null)
{
    public static ErroAplicacao NaoEncontrado { get; } = new(TipoErro.NaoEncontrado, StatusCodes.Status404NotFound, null, null);

    public static ErroAplicacao NaoAutorizado { get; } = new(TipoErro.NaoAutorizado, StatusCodes.Status401Unauthorized, null, null);

    public static ErroAplicacao DeCampo(string campo, string mensagem) =>
        new(TipoErro.Campo, StatusCodes.Status400BadRequest, null, mensagem, campo);

    public static ErroAplicacao Regra(int status, string? type, string title) => new(TipoErro.Regra, status, type, title);
}
