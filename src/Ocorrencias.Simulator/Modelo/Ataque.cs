using Ocorrencias.Api.Common;

namespace Ocorrencias.Simulator.Modelo;

/// <summary>
/// Ataque coordenado ao problema falso alvo: <c>k</c> contas respondem "sim" a "Ainda está lá?" ao longo de 6 h
/// a partir de 1 h após o registro.
/// </summary>
public static class Ataque
{
    public static double Inicio(Cenario c, ProblemaSimulado alvo) => alvo.CriadoHora + c.InicioAtaqueAposCriacaoHoras;

    public static double Fim(Cenario c, ProblemaSimulado alvo) => Inicio(c, alvo) + c.DuracaoAtaqueHoras;

    public static List<Intencao> Gerar(Cenario c, ProblemaSimulado alvo, Variante variante, int k, int semente)
    {
        var rnd = new Random(Aleatorio.Derivar(semente, 3, (int)variante, k));
        var inicio = Inicio(c, alvo);
        var fim = Fim(c, alvo);
        var intencoes = new List<Intencao>(k);

        for (var i = 0; i < k; i++)
        {
            var id = Mundo.IdAtacante(i);
            var perfil = variante == Variante.S3 && i == 0 ? Perfil.CidadaoVerificado : Perfil.Cidadao;
            double? distancia = variante == Variante.S1 ? null : rnd.Uniforme(0, c.DistanciaMaximaGpsFalsoMetros);
            intencoes.Add(new Intencao(rnd.Uniforme(inicio, fim), id, ValorVoto.Positivo, perfil, distancia));
        }

        return intencoes;
    }
}
