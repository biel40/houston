namespace Houston.App.Sensors;

public sealed record GpuSnapshot(
    string Name,
    string? Vendor,
    double? Load,
    double? Temp,
    int? ClockMhz,
    int? VramUsedMb,
    int? VramTotalMb,
    double? PowerW,
    int? FanRpm);
