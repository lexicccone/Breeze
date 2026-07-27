namespace Breeze.Models;

/// <summary>A search provider. Add entries to the catalog to support more engines.</summary>
public sealed record SearchEngine(string Name, string QueryUrl)
{
    public string Search(string query) => QueryUrl + Uri.EscapeDataString(query);
}
