namespace Houston.App;

/// <summary>
/// The only persisted data (settings.json). The position is the window's top-left corner in physical
/// screen pixels, which stays exact across monitors with different DPI. The width is in device-independent
/// pixels; the height always follows from the UI's aspect ratio. The card layout is opaque to the host:
/// the UI owns the card ids and validates them when they come back.
/// </summary>
public sealed record AppSettings(
    int? WindowX = null,
    int? WindowY = null,
    double? WindowWidth = null,
    IReadOnlyList<string>? CardOrder = null,
    IReadOnlyList<string>? HiddenCards = null);
