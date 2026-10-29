using UrbanReports.Api.Common;

namespace Ocorrencias.Simulator.Modelo;

/// <summary>
/// Mundo honesto de uma semente: usuários, problemas (o último é o alvo do ataque) e as intenções de voto
/// honestas por problema. Não depende da configuração do motor: as mesmas intenções são avaliadas em A e B.
/// </summary>
public sealed class Mundo
{
    public required IReadOnlyList<Usuario> Usuarios { get; init; }

    /// <summary>Índices 0..Problemas−1: cenário base. Índice <see cref="IndiceAlvo"/>: o falso que será atacado.</summary>
    public required IReadOnlyList<ProblemaSimulado> Problemas { get; init; }

    public required IReadOnlyList<IReadOnlyList<Intencao>> Intencoes { get; init; }

    public int IndiceAlvo => Problemas.Count - 1;

    public ProblemaSimulado Alvo => Problemas[IndiceAlvo];

    public static Guid IdUsuario(int indice) => new(indice, 0, 0, new byte[8]);

    /// <summary>Contas do atacante ficam numa faixa própria de ids.</summary>
    public static Guid IdAtacante(int indice) => IdUsuario(1_000_000 + indice);

    /// <summary>Autor do problema falso atacado (não vota: não está entre as k contas).</summary>
    public static readonly Guid AutorAlvo = IdUsuario(999_999);

    public static Mundo Gerar(Cenario cenario, int semente)
    {
        var usuarios = GerarUsuarios(cenario, new Random(Aleatorio.Derivar(semente, 0)));
        var problemas = GerarProblemas(cenario, usuarios, new Random(Aleatorio.Derivar(semente, 1)));

        // Um gerador por problema: as intenções de um problema não dependem dos sorteios dos outros.
        var intencoes = problemas
            .Select(p => (IReadOnlyList<Intencao>)GerarIntencoesHonestas(cenario, usuarios, p, new Random(Aleatorio.Derivar(semente, 2, p.Indice))))
            .ToList();

        return new Mundo { Usuarios = usuarios, Problemas = problemas, Intencoes = intencoes };
    }

    private static Ponto PontoNaArea(Cenario c, Random rnd) =>
        new(rnd.Uniforme(0, c.LarguraMetros), rnd.Uniforme(0, c.AlturaMetros));

    private static List<Usuario> GerarUsuarios(Cenario c, Random rnd) =>
        Enumerable.Range(0, c.Usuarios)
            .Select(i => new Usuario(
                IdUsuario(i),
                rnd.Chance(c.FracaoVerificados) ? Role.VerifiedCitizen : Role.Citizen,
                PontoNaArea(c, rnd)))
            .ToList();

    private static List<ProblemaSimulado> GerarProblemas(Cenario c, IReadOnlyList<Usuario> usuarios, Random rnd)
    {
        var problemas = new List<ProblemaSimulado>(c.Problemas + 1);
        for (var i = 0; i < c.Problemas; i++)
        {
            var verdadeiro = rnd.Chance(c.FracaoVerdadeiros);
            var local = PontoNaArea(c, rnd);
            var criado = (double)rnd.Next(0, c.DiasCriacao * 24);
            var resolucao = criado + rnd.Exponencial(c.MediaResolucaoDias * 24);
            var elegiveis = Elegiveis(c, usuarios, local);
            var autor = elegiveis.Count > 0 ? usuarios[elegiveis[rnd.Next(elegiveis.Count)]].Id : usuarios[rnd.Next(usuarios.Count)].Id;

            problemas.Add(new ProblemaSimulado(
                i, verdadeiro, local, criado, verdadeiro && resolucao < c.Horas ? resolucao : null, autor));
        }

        problemas.Add(new ProblemaSimulado(c.Problemas, false, PontoNaArea(c, rnd), c.DiaAtaque * 24, null, AutorAlvo));
        return problemas;
    }

    /// <summary>Usuários cujo centro de circulação está a até <see cref="Cenario.RaioCirculacaoMetros"/> do problema.</summary>
    private static List<int> Elegiveis(Cenario c, IReadOnlyList<Usuario> usuarios, Ponto local)
    {
        var elegiveis = new List<int>();
        for (var i = 0; i < usuarios.Count; i++)
        {
            if (usuarios[i].Centro.DistanciaAte(local) <= c.RaioCirculacaoMetros)
            {
                elegiveis.Add(i);
            }
        }

        return elegiveis;
    }

    /// <summary>
    /// A cada hora, cada elegível passa pelo problema com probabilidade p. Ao passar a ≤ 500 m:
    /// verdadeiro ativo → confirma (30%) e/ou continua (15%); verdadeiro já resolvido → resolvido (30%);
    /// falso → não vi (20%). 5% dos votos invertem o valor. O autor não vota existência.
    /// </summary>
    private static List<Intencao> GerarIntencoesHonestas(Cenario c, IReadOnlyList<Usuario> usuarios, ProblemaSimulado p, Random rnd)
    {
        var elegiveis = Elegiveis(c, usuarios, p.Local);
        var pPassagem = c.ProbabilidadePassagemEfetiva;
        var intencoes = new List<Intencao>();

        for (var h = (int)Math.Ceiling(p.CriadoHora); h < c.Horas; h++)
        {
            var passantes = rnd.Binomial(elegiveis.Count, pPassagem);
            if (passantes == 0)
            {
                continue;
            }

            foreach (var e in rnd.Amostra(elegiveis.Count, passantes))
            {
                var usuario = usuarios[elegiveis[e]];
                var hora = h + rnd.NextDouble();
                var distancia = rnd.DistanciaNoDisco(c.RaioPassagemMetros);
                if (distancia > c.DistanciaMaximaVotoMetros)
                {
                    continue;
                }

                if (usuario.Id == p.AutorId)
                {
                    continue; // o autor não vota
                }

                var resolvidoDeFato = p.ResolucaoRealHora is { } r && hora >= r;
                var (probabilidade, valor) = !p.Verdadeiro ? (c.ProbNaoFalso, VoteValue.Negative)
                    : resolvidoDeFato ? (c.ProbNaoResolvido, VoteValue.Negative)
                    : (c.ProbSim, VoteValue.Positive);

                if (rnd.Chance(probabilidade))
                {
                    intencoes.Add(Votar(c, rnd, hora, usuario, valor, distancia));
                }
            }
        }

        // Cada passante sorteia o próprio minuto dentro da hora: reordena (OrderBy é estável) para a avaliação
        // processar os votos em ordem cronológica, como chegariam à API.
        return intencoes.OrderBy(i => i.Hora).ToList();
    }

    private static Intencao Votar(Cenario c, Random rnd, double hora, Usuario u, VoteValue valor, double distancia)
    {
        if (rnd.Chance(c.ProbRuido))
        {
            valor = valor == VoteValue.Positive ? VoteValue.Negative : VoteValue.Positive;
        }

        return new Intencao(hora, u.Id, valor, u.Perfil, distancia);
    }
}
