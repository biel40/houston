using System.Text.Json;
using Houston.App.Sensors;
using Microsoft.Web.WebView2.Core;

namespace Houston.App.Interop;

/// <summary>
/// Message protocol between the host and the Svelte UI. Every message is a JSON object with a
/// <c>type</c> field. Must be used on the UI thread, except for <see cref="SerializeSensors"/>.
/// </summary>
internal sealed class UiBridge : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly CoreWebView2 _core;
    private readonly string _origin;
    private bool _disposed;

    public UiBridge(CoreWebView2 core, Uri uiUri)
    {
        _core = core;
        _origin = uiUri.GetLeftPart(UriPartial.Authority);
        _core.WebMessageReceived += OnWebMessageReceived;
    }

    public event Action? Ready;
    public event Action? DragRequested;
    public event Action? CloseRequested;
    public event Action? MinimizeRequested;
    public event Action? MaximizeToggleRequested;
    public event Action<bool>? TopmostRequested;
    public event Action<bool>? MenuChanged;

    /// <summary>Starts the native resize loop from an edge or corner (a <c>WMSZ_*</c> value, 1 to 8).</summary>
    public event Action<int>? ResizeRequested;

    /// <summary>Natural content size in CSS pixels at scale 1, which equal WPF device-independent pixels at zoom 100 %.</summary>
    public event Action<double, double>? SizeRequested;

    /// <summary>The user reordered, hid or showed a card: the card ids in order, and those hidden.</summary>
    public event Action<IReadOnlyList<string>, IReadOnlyList<string>>? LayoutChanged;

    /// <summary>Thread-safe, so serialization can happen on the sampling thread.</summary>
    public static string SerializeSensors(SensorSnapshot snapshot) =>
        JsonSerializer.Serialize(new { type = "sensors", data = snapshot }, JsonOptions);

    public void PostJson(string json)
    {
        if (!_disposed)
        {
            _core.PostWebMessageAsJson(json);
        }
    }

    public void PostWindowState(bool topmost, bool maximized) =>
        PostJson(JsonSerializer.Serialize(new { type = "window", topmost, maximized }, JsonOptions));

    public void PostDismissMenu() => PostJson("""{"type":"dismissMenu"}""");

    public void PostLayout(IReadOnlyList<string> order, IReadOnlyList<string> hidden) =>
        PostJson(JsonSerializer.Serialize(new { type = "layout", order, hidden }, JsonOptions));

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _core.WebMessageReceived -= OnWebMessageReceived;
    }

    private void OnWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        // Only our own page may drive the window.
        if (!e.Source.StartsWith(_origin + "/", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        using var document = JsonDocument.Parse(e.WebMessageAsJson);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object
            || !root.TryGetProperty("type", out var type)
            || type.ValueKind != JsonValueKind.String)
        {
            return;
        }

        switch (type.GetString())
        {
            case "ready":
                Ready?.Invoke();
                break;
            case "drag":
                DragRequested?.Invoke();
                break;
            case "close":
                CloseRequested?.Invoke();
                break;
            case "minimize":
                MinimizeRequested?.Invoke();
                break;
            case "toggleMaximize":
                MaximizeToggleRequested?.Invoke();
                break;
            case "resize" when root.TryGetProperty("edge", out var edge)
                               && edge.TryGetInt32(out var side)
                               && side is >= NativeMethods.WMSZ_LEFT and <= NativeMethods.WMSZ_BOTTOMRIGHT:
                ResizeRequested?.Invoke(side);
                break;
            case "setTopmost" when root.TryGetProperty("value", out var value)
                                   && value.ValueKind is JsonValueKind.True or JsonValueKind.False:
                TopmostRequested?.Invoke(value.GetBoolean());
                break;
            case "menu" when root.TryGetProperty("open", out var open)
                             && open.ValueKind is JsonValueKind.True or JsonValueKind.False:
                MenuChanged?.Invoke(open.GetBoolean());
                break;
            case "size" when root.TryGetProperty("width", out var width)
                             && root.TryGetProperty("height", out var height)
                             && width.TryGetDouble(out var w)
                             && height.TryGetDouble(out var h):
                SizeRequested?.Invoke(w, h);
                break;
            case "layout" when TryReadCardIds(root, "order", out var order)
                               && TryReadCardIds(root, "hidden", out var hidden):
                LayoutChanged?.Invoke(order, hidden);
                break;
        }
    }

    // Bounded so a misbehaving page cannot bloat settings.json; the UI has far fewer cards than this.
    private static bool TryReadCardIds(JsonElement root, string property, out IReadOnlyList<string> ids)
    {
        const int maxCount = 16;
        const int maxLength = 32;

        ids = [];
        if (!root.TryGetProperty(property, out var array)
            || array.ValueKind != JsonValueKind.Array
            || array.GetArrayLength() > maxCount)
        {
            return false;
        }

        var result = new List<string>();
        foreach (var item in array.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String || item.GetString() is not { Length: > 0 and <= maxLength } id)
            {
                return false;
            }

            result.Add(id);
        }

        ids = result;
        return true;
    }
}
