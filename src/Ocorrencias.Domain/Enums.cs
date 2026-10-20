namespace Ocorrencias.Domain;

public enum Perfil
{
    Cidadao = 1,
    CidadaoVerificado = 2,
    Administrador = 3,
}

/// <summary>Resposta a "Ainda está lá?": Positivo = Sim, Negativo = Não (foi resolvido ou não foi encontrado).</summary>
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

public enum TipoOcorrencia
{
    Problema = 1,
    Incidente = 2,
}

/// <summary>Cor de um grupo de categorias: slot da paleta do front (tokens CSS por slot, claro e escuro).</summary>
public enum CorGrupo
{
    Laranja = 1,
    Ocre = 2,
    Marrom = 3,
    Vermelho = 4,
    Terracota = 5,
    Azul = 6,
    VerdeAgua = 7,
    Roxo = 8,
    Rosa = 9,
    Cinza = 10,
}

public enum TipoVinculo
{
    MoradorRegiao = 1,
    TrabalhaRegiao = 2,
    ServidorPublico = 3,
    Outro = 4,
}

public enum StatusVerificacao
{
    Pendente = 1,
    Aprovada = 2,
    Recusada = 3,
}

/// <summary>Concentração de incidentes numa área (total de 7 dias): &lt; 5 baixa, 5–14 média, ≥ 15 alta.</summary>
public enum NivelConcentracao
{
    Baixa = 1,
    Media = 2,
    Alta = 3,
}
