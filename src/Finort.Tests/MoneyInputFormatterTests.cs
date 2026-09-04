using Finort.App;

namespace Finort.Tests;

public class MoneyInputFormatterTests
{
    [Theory]
    [InlineData(null, "")]
    [InlineData("", "")]
    [InlineData("a1b2c,3", "123")]
    [InlineData("12.345", "12345")]
    public void FiltrarDigitos_RemoveNaoNumericos(string? input, string expected)
    {
        Assert.Equal(expected, MoneyInputFormatter.FiltrarDigitos(input));
    }

    [Fact]
    public void ParaValor_Vazio_RetornaNull()
        => Assert.Null(MoneyInputFormatter.ParaValor(""));

    [Theory]
    [InlineData("5", 0.05)]
    [InlineData("123", 1.23)]
    [InlineData("12345", 123.45)]
    [InlineData("1234567", 12345.67)]
    public void ParaValor_PreencheCentavosDaDireitaParaEsquerda(string digitos, decimal expected)
        => Assert.Equal(expected, MoneyInputFormatter.ParaValor(digitos));

    [Theory]
    [InlineData("", "")]
    [InlineData("5", "0,05")]
    [InlineData("12345", "123,45")]
    [InlineData("1234567", "12.345,67")]
    public void Formatar_FormataEmPtBr(string digitos, string expected)
        => Assert.Equal(expected, MoneyInputFormatter.Formatar(digitos));

    [Theory]
    [InlineData(0.05, "5")]
    [InlineData(123.45, "12345")]
    [InlineData(-123.45, "12345")]
    public void DigitosDeValor_IgnoraSinal(decimal valor, string expected)
        => Assert.Equal(expected, MoneyInputFormatter.DigitosDeValor(valor));

    [Theory]
    [InlineData("5", 0.00000005)]
    [InlineData("12345678901", 123.45678901)]
    public void ParaValor_OitoCasas_PreencheDaDireita(string digitos, decimal expected)
        => Assert.Equal(expected, MoneyInputFormatter.ParaValor(digitos, 8));

    [Fact]
    public void Formatar_OitoCasas_FormataComOitoDecimais()
        => Assert.Equal("0,00000005", MoneyInputFormatter.Formatar("5", 8));

    [Fact]
    public void DigitosDeValor_OitoCasas_EscalaCorretamente()
        => Assert.Equal("12345678901",
            MoneyInputFormatter.DigitosDeValor(123.45678901m, 8));

    [Fact]
    public void ParaValor_DuasCasas_ComportamentoInalterado()
        => Assert.Equal(1.23m, MoneyInputFormatter.ParaValor("123"));

    [Theory]
    [InlineData("5", 5)]
    [InlineData("100", 100)]
    [InlineData("12345", 12345)]
    public void ParaValor_ZeroCasas_RetornaInteiro(string digitos, decimal expected)
        => Assert.Equal(expected, MoneyInputFormatter.ParaValor(digitos, 0));

    [Theory]
    [InlineData("5", "5")]
    [InlineData("100", "100")]
    [InlineData("12345", "12345")]
    public void Formatar_ZeroCasas_FormataSemDecimais(string digitos, string expected)
        => Assert.Equal(expected, MoneyInputFormatter.Formatar(digitos, 0));

    [Theory]
    [InlineData(5, "5")]
    [InlineData(100, "100")]
    [InlineData(-12345, "12345")]
    public void DigitosDeValor_ZeroCasas_EscalaInteira(decimal valor, string expected)
        => Assert.Equal(expected, MoneyInputFormatter.DigitosDeValor(valor, 0));
}
