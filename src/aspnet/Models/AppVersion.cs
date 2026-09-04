namespace Finort.Models;

public class AppVersion
{
    public string TagName { get; set; } = "";
    public DateTime PublishedAt { get; set; }
    public string HtmlUrl { get; set; } = "";
    public bool NovaVersaoDisponivel { get; set; }
    public string VersaoAtual { get; set; } = "";
}
