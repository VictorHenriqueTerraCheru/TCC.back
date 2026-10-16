namespace Ocorrencias.Api.Common;

/// <summary>Resposta paginada de docs/API.md: <c>{ itens, pagina, tamanho, total }</c>.</summary>
public sealed record Paginado<T>(IReadOnlyList<T> Itens, int Pagina, int Tamanho, int Total);

public static class Paginacao
{
    public const int TamanhoPadrao = 20;
    public const int TamanhoMaximo = 100;

    public static int Pular(int pagina, int tamanho) => (pagina - 1) * tamanho;
}
