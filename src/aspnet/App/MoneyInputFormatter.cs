using System.Globalization;

namespace Finort.App;

public static class MoneyInputFormatter
{
    private static readonly CultureInfo PtBr = new("pt-BR");

    public static string FiltrarDigitos(string? text)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;
        return new string(text.Where(char.IsAsciiDigit).ToArray());
    }

    public static string Formatar(string? digitos, int casas = 2)
    {
        var d = FiltrarDigitos(digitos);
        if (d.Length == 0) return string.Empty;
        if (casas == 0)
        {
            return d;
        }
        var s = d.PadLeft(casas + 1, '0');
        var valorDec = decimal.Parse(s[..^casas] + "." + s[^casas..], CultureInfo.InvariantCulture);
        return valorDec.ToString($"N{casas}", PtBr);
    }

    public static decimal? ParaValor(string? digitos, int casas = 2)
    {
        var d = FiltrarDigitos(digitos);
        if (d.Length == 0) return null;
        if (casas == 0)
            return long.Parse(d, CultureInfo.InvariantCulture);
        var s = d.PadLeft(casas + 1, '0');
        return decimal.Parse(s[..^casas] + "." + s[^casas..], CultureInfo.InvariantCulture);
    }

    public static string DigitosDeValor(decimal valor, int casas = 2)
    {
        var fator = (decimal)Math.Pow(10, casas);
        var escalado = Math.Abs(valor) * fator;
        return ((long)Math.Round(escalado, MidpointRounding.AwayFromZero))
            .ToString(CultureInfo.InvariantCulture);
    }
}
