using System.Text.Json;
using Microsoft.Web.WebView2.Core;

namespace Breeze.Services;

/// <summary>Message bridge between the bundled start page and the shortcut store.
/// Messages from any other origin are ignored.</summary>
public static class StartPageBridge
{
    private static readonly JsonSerializerOptions Options = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public static void Attach(CoreWebView2 webView) =>
        webView.WebMessageReceived += async (_, e) =>
        {
            if (!StartPage.IsStartPage(e.Source))
            {
                return;
            }

            try
            {
                await HandleAsync(webView, e.WebMessageAsJson);
            }
            catch (Exception error)
            {
                // A malformed message, or a failed disk or network operation, must not reach
                // the event loop: this handler is async void.
                ErrorLog.Write("bridge", error);
            }
        };

    private static async Task HandleAsync(CoreWebView2 webView, string json)
    {
        using var document = JsonDocument.Parse(json);
        var message = document.RootElement;

        if (message.ValueKind != JsonValueKind.Object ||
            !message.TryGetProperty("type", out var type))
        {
            return;
        }

        // The page echoes the revision it last rendered; a stale one is refused by the store and
        // the page is refreshed with the current list below.
        var revision = Number(message, "revision");

        switch (type.GetString())
        {
            case "list":
                break;
            case "save":
                await ShortcutStore.SaveAsync(revision, Number(message, "index"), Text(message, "name"), Text(message, "url"));
                break;
            case "delete":
                await ShortcutStore.RemoveAsync(revision, Number(message, "index"));
                break;
            case "move":
                await ShortcutStore.MoveAsync(revision, Number(message, "from"), Number(message, "to"));
                break;
            default:
                return;
        }

        Publish(webView);
    }

    private static void Publish(CoreWebView2 webView) =>
        webView.PostWebMessageAsJson(JsonSerializer.Serialize(
            new
            {
                type = "shortcuts",
                items = ShortcutStore.Items,
                revision = ShortcutStore.Revision,
                searchUrl = SearchEngines.Current.QueryUrl
            }, Options));

    private static string Text(JsonElement message, string name) =>
        message.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;

    private static int Number(JsonElement message, string name) =>
        message.TryGetProperty(name, out var value) && value.TryGetInt32(out var number) ? number : -1;
}
