using System.Text.RegularExpressions;

namespace Breeze.Services;

/// <summary>Turns address bar input into either a navigable URL or a DuckDuckGo search.</summary>
public static partial class UrlResolver
{
    public static string? Resolve(string? input)
    {
        var text = input?.Trim();
        if (string.IsNullOrEmpty(text))
        {
            return null;
        }

        if (Uri.TryCreate(text, UriKind.Absolute, out var uri) &&
            uri.Scheme is "http" or "https" or "file")
        {
            return uri.AbsoluteUri;
        }

        if (HostPattern().IsMatch(text))
        {
            return "https://" + text;
        }

        return SearchEngines.Current.Search(text);
    }

    [GeneratedRegex(@"^(localhost(:\d+)?|[^\s/?#]+\.[^\s/?#.]{2,})([/?#]\S*)?$", RegexOptions.IgnoreCase, 1000)]
    private static partial Regex HostPattern();
}
