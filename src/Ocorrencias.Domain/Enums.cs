namespace Ocorrencias.Domain;

public enum Perfil
{
    Cidadao = 1,
    CidadaoVerificado = 2,
    Administrador = 3,
}

public enum TipoVoto
{
    Existencia = 1,
    Persistencia = 2,
}

/// <summary>Existência: Positivo = Confirma, Negativo = NaoVi. Persistência: Positivo = Continua, Negativo = Resolvido.</summary>
public enum ValorVoto
{
    Positivo = 1,
    Negativo = 2,
}

public enum StatusProblema
{
    Nova = 1,
    Ativa = 2,
    PossivelmenteResolvida = 3,
    Resolvida = 4,
}

public enum FaixaConfianca
{
    Baixa = 1,
    Media = 2,
    Alta = 3,
}
