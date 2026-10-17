namespace Ocorrencias.Api.Common;

public enum TipoErro
{
    NaoEncontrado,
    NaoAutorizado,
    Campo,
    Regra,
}

/// <summary>
/// Erro devolvido pelos services, sem dependência de HTTP. O module traduz com <see cref="ErroAplicacaoExtensions"/>;
/// <c>Type</c> e <c>Title</c> seguem a tabela de erros de docs/API.md.
/// </summary>
public sealed record ErroAplicacao(TipoErro Tipo, int Status, string? Type, string? Title, string? Campo = null)
{
    public static ErroAplicacao NaoEncontrado { get; } = new(TipoErro.NaoEncontrado, StatusCodes.Status404NotFound, null, null);

    /// <summary>Token válido de um usuário que não existe mais.</summary>
    public static ErroAplicacao NaoAutorizado { get; } = new(TipoErro.NaoAutorizado, StatusCodes.Status401Unauthorized, null, null);

    /// <summary>Erro de um campo do corpo detectado só no banco (ex.: categoria inexistente) → 400 ValidationProblem.</summary>
    public static ErroAplicacao DeCampo(string campo, string mensagem) =>
        new(TipoErro.Campo, StatusCodes.Status400BadRequest, null, mensagem, campo);

    public static ErroAplicacao Regra(int status, string? type, string title) => new(TipoErro.Regra, status, type, title);
}
