namespace UrbanReports.Api.Common;

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

public readonly record struct Vazio;
