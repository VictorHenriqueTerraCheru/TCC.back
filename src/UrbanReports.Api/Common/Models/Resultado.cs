namespace UrbanReports.Api.Common;

/// <summary>Valor ou erro de um service. Converte implicitamente de <typeparamref name="T"/> e de <see cref="ErroAplicacao"/>.</summary>
public readonly struct Resultado<T>
{
    private Resultado(T? valor, ErroAplicacao? erro)
    {
        Valor = valor;
        Erro = erro;
    }

    public T? Valor { get; }

    public ErroAplicacao? Erro { get; }

    public bool Sucesso => Erro is null;

    public static implicit operator Resultado<T>(T valor) => new(valor, null);

    public static implicit operator Resultado<T>(ErroAplicacao erro) => new(default, erro);
}

/// <summary>Valor de sucesso de operações sem retorno (ex.: aprovar uma verificação).</summary>
public readonly record struct Vazio;
