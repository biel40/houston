using LibreHardwareMonitor.Hardware;

namespace Houston.App.Sensors;

/// <summary>Maps LibreHardwareMonitor's GPU sensors (NVIDIA, AMD, Intel) to <see cref="GpuSnapshot"/>.</summary>
internal sealed class GpuReader(IHardware hardware)
{
    private const string IntegratedIntelIdPrefix = "/gpu-intel-integrated";

    private static readonly string[] DedicatedTotalNames = ["GPU Memory Total", "D3D Dedicated Memory Total"];

    public void Update() => hardware.Update();

    /// <summary>Intel integrated graphics: Windows' RAPL graphics-plane meter applies to it.</summary>
    public bool IsIntegratedIntel =>
        hardware.Identifier.ToString().StartsWith(IntegratedIntelIdPrefix, StringComparison.Ordinal);

    /// <summary>
    /// Prefers a dedicated GPU, then the one with the most memory. Chosen once at startup so the widget
    /// never flips between adapters; requires the hardware to have been updated at least once.
    /// </summary>
    public static IHardware? SelectPreferred(IEnumerable<IHardware> hardware) =>
        hardware
            .Where(h => h.HardwareType is HardwareType.GpuNvidia or HardwareType.GpuAmd or HardwareType.GpuIntel)
            .OrderByDescending(IsDedicated)
            .ThenByDescending(h => TotalMemoryMb(h) ?? 0)
            .FirstOrDefault();

    public GpuSnapshot Read()
    {
        var temp = SensorLookup.Find(hardware, SensorType.Temperature, "GPU Core", "GPU Hot Spot");
        var clock = SensorLookup.Find(hardware, SensorType.Clock, "GPU Core");
        var power = SensorLookup.Find(hardware, SensorType.Power, "GPU Package", "GPU Power", "GPU Core");
        var fan = hardware.Sensors.FirstOrDefault(s => s.SensorType == SensorType.Fan);

        return new GpuSnapshot(
            hardware.Name,
            hardware.HardwareType switch
            {
                HardwareType.GpuNvidia => HardwareVendor.Nvidia,
                HardwareType.GpuAmd => HardwareVendor.Amd,
                HardwareType.GpuIntel => HardwareVendor.Intel,
                _ => null,
            },
            Load(),
            SensorLookup.Positive(temp),
            SensorLookup.Integer(SensorLookup.Positive(clock)),
            SensorLookup.Integer(UsedMemoryMb()),
            SensorLookup.Integer(TotalMemoryMb(hardware)),
            SensorLookup.Positive(power),
            SensorLookup.Integer(SensorLookup.Value(fan)));
    }

    private static bool IsDedicated(IHardware gpu) =>
        !gpu.Identifier.ToString().StartsWith(IntegratedIntelIdPrefix, StringComparison.Ordinal)
        && SensorLookup.Value(SensorLookup.Find(gpu, SensorType.SmallData, DedicatedTotalNames)) is >= 1024;

    // Integrated GPUs have no dedicated memory, so their shared system memory is reported instead.
    private static double? TotalMemoryMb(IHardware gpu) =>
        SensorLookup.Value(SensorLookup.Find(gpu, SensorType.SmallData,
            "GPU Memory Total", "D3D Dedicated Memory Total", "D3D Shared Memory Total"));

    private double? UsedMemoryMb() =>
        SensorLookup.Value(SensorLookup.Find(hardware, SensorType.SmallData,
            "GPU Memory Used", "D3D Dedicated Memory Used", "D3D Shared Memory Used"));

    // "GPU Core" when the vendor API provides it; otherwise the busiest D3D engine, as Task Manager does.
    private double? Load()
    {
        if (SensorLookup.Value(SensorLookup.Find(hardware, SensorType.Load, "GPU Core")) is { } core)
        {
            return core;
        }

        double? max = null;
        foreach (var sensor in hardware.Sensors)
        {
            if (sensor.SensorType == SensorType.Load
                && sensor.Name.StartsWith("D3D", StringComparison.Ordinal)
                && SensorLookup.Value(sensor) is { } value
                && value > (max ?? double.MinValue))
            {
                max = value;
            }
        }

        return max;
    }
}
