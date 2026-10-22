using Finort.App;

namespace Finort.Tests;

public class CalculadoraMatematicaTests
{
    /// <summary>Executa uma sequência de teclas: dígitos, ',', '+', '-', '*', '/', '%', '=', 'C' e 'B' (backspace).</summary>
    private static CalculadoraMatematica Teclado(params string[] teclas)
    {
        var calc = new CalculadoraMatematica();
        foreach (var tecla in teclas)
        {
            foreach (var c in tecla)
            {
                if (char.IsAsciiDigit(c)) calc.Digito(c);
                else if (c == ',') calc.Virgula();
                else if (c is '+' or '-' or '*' or '/') calc.Operador(c);
                else if (c == '%') calc.Porcento();
                else if (c == '=') calc.Igual();
                else if (c == 'C') calc.Limpar();
                else if (c == 'B') calc.Apoiar();
            }
        }
        return calc;
    }

    [Theory]
    [InlineData("1500", "1.500")]
    [InlineData("0", "0")]
    [InlineData("12,5", "12,5")]
    [InlineData("0,", "0,")]
    public void Visor_FormataEmPtBr(string teclas, string esperado)
        => Assert.Equal(esperado, Teclado(teclas).Visor);

    [Fact]
    public void Digito_LimitaCasasDecimais()
    {
        var calc = Teclado("1", ",", "2", "3");
        Assert.Equal("1,23", calc.Visor);
    }

    [Fact]
    public void Virgula_NaoRepete()
    {
        var calc = Teclado("1", ",", ",", "5");
        Assert.Equal("1,5", calc.Visor);
    }

    [Fact]
    public void Igual_Soma()
    {
        var calc = Teclado("5", "+", "3");
        Assert.Equal(8m, calc.Igual());
        Assert.Equal("8,00", calc.Visor);
    }

    [Fact]
    public void Igual_AvaliaDaEsquerdaParaDireita()
    {
        var calc = Teclado("5", "+", "3", "+", "2");
        Assert.Equal(10m, calc.Igual());
    }

    [Fact]
    public void Igual_SemPrecedencia_AplicaDaEsquerdaParaDireita()
    {
        Assert.Equal(20m, Teclado("2", "+", "3", "*", "4").Igual());
        Assert.Equal(10m, Teclado("2", "*", "3", "+", "4").Igual());
    }

    [Fact]
    public void Igual_Subtracao()
    {
        var calc = Teclado("100", "-", "30");
        Assert.Equal(70m, calc.Igual());
    }

    [Fact]
    public void Igual_MultiplicacaoEDivisao()
    {
        Assert.Equal(30m, Teclado("10", "*", "3").Igual());
        Assert.Equal(5m, Teclado("10", "/", "2").Igual());
    }

    [Fact]
    public void Igual_DivisaoPorZero_RetornaNuloComErro()
    {
        var calc = Teclado("10", "/", "0");
        Assert.Null(calc.Igual());
        Assert.NotNull(calc.Erro);
    }

    [Fact]
    public void Porcento_ComAdicao_UsaBaseAcumulada()
    {
        var calc = Teclado("200", "+", "10", "%");
        Assert.Equal(220m, calc.Igual());
    }

    [Fact]
    public void Porcento_ComMultiplicacao_DividePorCem()
    {
        var calc = Teclado("200", "*", "10", "%");
        Assert.Equal(20m, calc.Igual());
    }

    [Fact]
    public void Igual_Repetido_RepeteUltimaOperacao()
    {
        var calc = Teclado("2", "+", "3");
        Assert.Equal(5m, calc.Igual());
        Assert.Equal(8m, calc.Igual());
    }

    [Fact]
    public void Igual_DigitoNovo_ZeraOperacaoRepetida()
    {
        var calc = Teclado("2", "+", "3");
        Assert.Equal(5m, calc.Igual());
        calc.Digito('4');
        Assert.Equal(9m, calc.Igual());
    }

    [Fact]
    public void Apoiar_RemoveUltimoDigito()
    {
        var calc = Teclado("123");
        calc.Apoiar();
        Assert.Equal("12", calc.Visor);
    }

    [Fact]
    public void Limpar_ZeraTudo()
    {
        var calc = Teclado("12", "+", "3");
        calc.Limpar();
        Assert.Equal("0", calc.Visor);
        Assert.Null(calc.Erro);
        Assert.Equal(0m, calc.Igual());
    }

    [Fact]
    public void DefinirValorInicial_MostraValorComCasasDecimais()
    {
        var calc = new CalculadoraMatematica();
        calc.DefinirValorInicial(1500m);
        Assert.Equal("1.500,00", calc.Visor);
    }

    [Fact]
    public void DefinirValorInicial_LimpaEstadoDeErroEOperacaoPendente()
    {
        var calc = Teclado("10", "/", "0");
        Assert.Null(calc.Igual());
        Assert.NotNull(calc.Erro);

        calc.DefinirValorInicial(1500m);

        Assert.Null(calc.Erro);
        Assert.Equal("1.500,00", calc.Visor);
        Assert.Equal(1500m, calc.Igual());
    }

    [Fact]
    public void DefinirValorInicial_DescartaOperacaoPendente()
    {
        var calc = Teclado("5", "+", "3");
        calc.DefinirValorInicial(1500m);

        Assert.Equal("1.500,00", calc.Visor);
        Assert.Equal(1500m, calc.Igual());
    }

    [Fact]
    public void OitoCasas_ExibeOitoDecimais()
    {
        var calc = new CalculadoraMatematica(8);
        calc.DefinirValorInicial(0.00000001m);
        Assert.Equal("0,00000001", calc.Visor);
    }

    [Fact]
    public void ZeroCasas_NaoAceitaVirgula()
    {
        var calc = new CalculadoraMatematica(0);
        calc.Digito('1');
        calc.Virgula();
        calc.Digito('5');
        Assert.Equal("15", calc.Visor);
    }

    [Fact]
    public void Resultado_ConverteParaOMascaraDoCampo()
    {
        var resultado = Teclado("1.500".Replace(".", ""), "+", "200").Igual();
        Assert.NotNull(resultado);

        var digitos = MoneyInputFormatter.DigitosDeValor(resultado.Value, 2);
        var valor = MoneyInputFormatter.ParaValor(digitos, 2);

        Assert.Equal("170000", digitos);
        Assert.Equal(1700m, valor);
        Assert.Equal("1.700,00", MoneyInputFormatter.Formatar(digitos, 2));
    }

    /// <summary>Conta os dígitos da parte inteira do visor formatado.</summary>
    private static int DigitosInteiros(string visor)
    {
        var i = visor.IndexOf(',');
        return (i < 0 ? visor : visor[..i]).Count(char.IsAsciiDigit);
    }

    [Fact]
    public void Porcento_MultiplicacaoEstouraODecimal_NaoLancaExcecao()
    {
        var calc = new CalculadoraMatematica();
        calc.DefinirValorInicial(1e26m);
        calc.Operador('+');
        calc.Digito('9');
        calc.Digito('9');
        calc.Digito('9');

        calc.Porcento();

        Assert.Equal("Cálculo fora do limite suportado.", calc.Erro);
    }

    [Theory]
    [InlineData(2, 13)]
    [InlineData(8, 16)]
    public void Digito_LimitaDigitosInteirosAoLimiteDaMascara(int casas, int maxInteiros)
    {
        var calc = new CalculadoraMatematica(casas);
        for (var i = 0; i < maxInteiros + 3; i++) calc.Digito('9');

        Assert.Equal(maxInteiros, DigitosInteiros(calc.Visor));
    }

    [Fact]
    public void Digito_NoLimiteTotal_ConverteSemExcecao()
    {
        var calc = new CalculadoraMatematica(8);
        for (var i = 0; i < 16; i++) calc.Digito('9');
        calc.Virgula();
        for (var i = 0; i < 8; i++) calc.Digito('9');

        Assert.Equal(24, calc.Visor.Count(char.IsAsciiDigit));
        Assert.Equal(9999999999999999.99999999m, calc.Igual());
    }

    [Fact]
    public void Igual_ResultadoMaiorQueAMascara_RetornaErroEmVezDoValor()
    {
        var calc = new CalculadoraMatematica();
        for (var i = 0; i < 13; i++) calc.Digito('9');
        calc.Operador('*');
        for (var i = 0; i < 13; i++) calc.Digito('9');

        Assert.Null(calc.Igual());
        Assert.Equal("Cálculo fora do limite suportado.", calc.Erro);
    }

    [Fact]
    public void Igual_ResultadoDentroDaMascara_ConfirmaValor()
    {
        var calc = new CalculadoraMatematica(8);
        for (var i = 0; i < 16; i++) calc.Digito('9');
        calc.Operador('/');
        calc.Digito('1');

        Assert.Equal(9999999999999999m, calc.Igual());
    }
}
