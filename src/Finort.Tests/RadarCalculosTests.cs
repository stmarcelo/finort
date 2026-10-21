using Finort.Models.Financeiro;
using Finort.Services;

namespace Finort.Tests;

public class RadarCalculosTests
{
    [Fact]
    public void IsSignificantChange_VariacaoPequena_RetornaFalso()
    {
        var resultado = RadarCalculos.IsSignificantChange(1010m, 1000m,
            RadarCalculos.VariacaoPercentualMinima, 20m);

        Assert.False(resultado);
    }

    [Fact]
    public void IsSignificantChange_VariacaoRelevante_RetornaTrue()
    {
        var resultado = RadarCalculos.IsSignificantChange(1200m, 1000m,
            RadarCalculos.VariacaoPercentualMinima, 20m);

        Assert.True(resultado);
    }

    [Fact]
    public void IsSignificantChange_ImpactoAbsolutoIrrelevante_RetornaFalso()
    {
        // 10 -> 11: 10% de variação, mas R$ 1 de diferença.
        var resultado = RadarCalculos.IsSignificantChange(11m, 10m,
            RadarCalculos.VariacaoPercentualMinima, 20m);

        Assert.False(resultado);
    }

    [Fact]
    public void IsSignificantChange_MediaZero_RetornaTrueQuandoPassaDoLimiarAbsoluto()
    {
        Assert.True(RadarCalculos.IsSignificantChange(50m, 0m,
            RadarCalculos.VariacaoPercentualMinima, 20m));
        Assert.False(RadarCalculos.IsSignificantChange(10m, 0m,
            RadarCalculos.VariacaoPercentualMinima, 20m));
    }

    [Fact]
    public void ParticipacaoMinimaConsumo_MeiaPorCentoDoConsumoDoPeriodo()
    {
        // O Radar mede consumo: a participação mínima é fração do consumo do período.
        Assert.Equal(0.005m, RadarCalculos.ParticipacaoMinimaConsumo);
    }

    [Fact]
    public void LimiarAbsoluto_UsaMaiorEntrePisoFixoEParticipacao()
    {
        // Total de consumo baixo: vale o piso de R$ 20.
        Assert.Equal(20m, RadarCalculos.LimiarAbsoluto(1000m));
        // Total alto: 0,5% do total passa a valer.
        Assert.Equal(50m, RadarCalculos.LimiarAbsoluto(10000m));
    }

    [Fact]
    public void DetectarTendencia_SequenciaCrescente_RetornaAlta()
    {
        var tendencia = RadarCalculos.DetectarTendencia(new[] { 100m, 110m, 120m });

        Assert.Equal(RadarTendencia.Alta, tendencia);
    }

    [Fact]
    public void DetectarTendencia_SequenciaDecrescente_RetornaBaixa()
    {
        var tendencia = RadarCalculos.DetectarTendencia(new[] { 120m, 110m, 100m });

        Assert.Equal(RadarTendencia.Baixa, tendencia);
    }

    [Fact]
    public void DetectarTendencia_SequenciaOscilante_RetornaIndefinida()
    {
        var tendencia = RadarCalculos.DetectarTendencia(new[] { 100m, 150m, 90m });

        Assert.Equal(RadarTendencia.Indefinida, tendencia);
    }

    [Fact]
    public void DetectarTendencia_ApenasDoisPeriodos_RetornaIndefinida()
    {
        var tendencia = RadarCalculos.DetectarTendencia(new[] { 100m, 120m });

        Assert.Equal(RadarTendencia.Indefinida, tendencia);
    }

    [Fact]
    public void DetectarTendencia_VariacaoPequena_RetornaEstavel()
    {
        var tendencia = RadarCalculos.DetectarTendencia(new[] { 100m, 101m, 102m });

        Assert.Equal(RadarTendencia.Estavel, tendencia);
    }

    [Fact]
    public void Media_VetorVazio_RetornaZero()
    {
        Assert.Equal(0m, RadarCalculos.Media(Array.Empty<decimal>()));
        Assert.Equal(20m, RadarCalculos.Media(new[] { 10m, 20m, 30m }));
    }

    [Fact]
    public void VariacaoPercentual_MediaZero_RetornaCemPorCento()
    {
        Assert.Equal(1m, RadarCalculos.VariacaoPercentual(500m, 0m));
        Assert.Equal(0m, RadarCalculos.VariacaoPercentual(0m, 0m));
        Assert.Equal(0.25m, RadarCalculos.VariacaoPercentual(1250m, 1000m));
    }

    [Fact]
    public void DesvioPadrao_ValoresIguais_RetornaZero()
    {
        var desvio = RadarCalculos.DesvioPadrao(new[] { 50m, 50m, 50m });

        Assert.Equal(0m, desvio);
    }
}
