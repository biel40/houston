namespace Houston.App.Sensors;

public sealed record CpuSnapshot(string Name, string? Vendor, double? Load, double? Temp, int? ClockMhz, double? PowerW);
