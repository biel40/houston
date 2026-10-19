using LibreHardwareMonitor.Hardware;

namespace Houston.App.Sensors;

/// <summary>Maps LibreHardwareMonitor's CPU sensors (Intel and AMD naming) to <see cref="CpuSnapshot"/>.</summary>
internal sealed class CpuReader(IHardware hardware)
{
    public void Update() => hardware.Update();

    public CpuSnapshot Read()
    {
        var load = SensorLookup.Find(hardware, SensorType.Load, "CPU Total");
        var temp = SensorLookup.Find(hardware, SensorType.Temperature,
            "CPU Package", "Core (Tctl/Tdie)", "Core Max", "Core Average");
        var power = SensorLookup.Find(hardware, SensorType.Power, "CPU Package", "Package");

        return new CpuSnapshot(
            hardware.Name,
            Vendor(),
            SensorLookup.Value(load),
            SensorLookup.Positive(temp),
            SensorLookup.Integer(AverageCoreClock()),
            SensorLookup.Positive(power));
    }

    private string? Vendor() => hardware.Identifier.ToString() switch
    {
        var id when id.StartsWith("/intelcpu", StringComparison.Ordinal) => HardwareVendor.Intel,
        var id when id.StartsWith("/amdcpu", StringComparison.Ordinal) => HardwareVendor.Amd,
        _ => null,
    };

    // Average over cores ("Core #1", "P-Core #1", "E-Core #1"...) so hybrid CPUs get one representative figure.
    private double? AverageCoreClock()
    {
        double sum = 0;
        var count = 0;
        foreach (var sensor in hardware.Sensors)
        {
            if (sensor.SensorType == SensorType.Clock
                && sensor.Name.Contains("Core #", StringComparison.Ordinal)
                && SensorLookup.Positive(sensor) is { } mhz)
            {
                sum += mhz;
                count++;
            }
        }

        return count > 0 ? sum / count : null;
    }
}
