using LibreHardwareMonitor.Hardware;

namespace Houston.App.Sensors;

internal static class SensorLookup
{
    /// <summary>First sensor of <paramref name="type"/> matching the earliest name in <paramref name="names"/>.</summary>
    public static ISensor? Find(IHardware hardware, SensorType type, params string[] names)
    {
        foreach (var name in names)
        {
            foreach (var sensor in hardware.Sensors)
            {
                if (sensor.SensorType == type && sensor.Name == name)
                {
                    return sensor;
                }
            }
        }

        return null;
    }

    public static double? Value(ISensor? sensor) =>
        sensor?.Value is float value && float.IsFinite(value) ? Math.Round(value, 1) : null;

    /// <summary>
    /// For readings that can never be zero on a running machine (temperature, clock, power).
    /// Without the PawnIO driver LibreHardwareMonitor reports some of them as 0 instead of null.
    /// </summary>
    public static double? Positive(ISensor? sensor) => Value(sensor) is > 0 and var value ? value : null;

    public static int? Integer(double? value) => value is { } v ? (int)Math.Round(v) : null;
}
