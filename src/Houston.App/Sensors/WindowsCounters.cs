using Houston.App.Interop;

namespace Houston.App.Sensors;

/// <summary>
/// Fallback readings from Windows performance counters. They need neither administrator rights nor a
/// kernel driver, so they fill gaps when LibreHardwareMonitor has no PawnIO driver to talk to.
/// Not thread-safe: used only from the sampling task.
/// </summary>
internal sealed class WindowsCounters : IDisposable
{
    private readonly nint _query;
    private readonly nint _cpuPerformance;
    private readonly nint _cpuBaseFrequency;
    private readonly nint _packagePower;
    private readonly nint _graphicsPower;

    public WindowsCounters()
    {
        if (NativeMethods.PdhOpenQuery(null, 0, out _query) != 0)
        {
            _query = 0;
            return;
        }

        // Task Manager's "speed": nominal frequency scaled by the current performance percentage.
        _cpuPerformance = Add(@"\Processor Information(_Total)\% Processor Performance");
        _cpuBaseFrequency = Add(@"\Processor Information(_Total)\Processor Frequency");
        // RAPL energy meters exposed by Windows (Intel): whole package, and the integrated graphics plane.
        _packagePower = Add(@"\Energy Meter(RAPL_Package0_PKG)\Power");
        _graphicsPower = Add(@"\Energy Meter(RAPL_Package0_PP1)\Power");
    }

    public int? CpuClockMhz =>
        Read(_cpuPerformance) is double performance and > 0 && Read(_cpuBaseFrequency) is double baseMhz and > 0
            ? (int)Math.Round(performance * baseMhz / 100)
            : null;

    public double? CpuPackagePowerW => MilliwattsToWatts(Read(_packagePower));

    public double? IntegratedGraphicsPowerW => MilliwattsToWatts(Read(_graphicsPower));

    /// <summary>Takes a new sample; rate counters need two samples before they report a value.</summary>
    public void Collect()
    {
        if (_query != 0)
        {
            NativeMethods.PdhCollectQueryData(_query);
        }
    }

    public void Dispose()
    {
        if (_query != 0)
        {
            NativeMethods.PdhCloseQuery(_query);
        }
    }

    private nint Add(string path) =>
        _query != 0 && NativeMethods.PdhAddEnglishCounter(_query, path, 0, out var counter) == 0 ? counter : 0;

    private static double? Read(nint counter)
    {
        if (counter == 0
            || NativeMethods.PdhGetFormattedCounterValue(
                counter, NativeMethods.PDH_FMT_DOUBLE | NativeMethods.PDH_FMT_NOCAP100, out _, out var value) != 0
            || value.CStatus != 0
            || !double.IsFinite(value.DoubleValue))
        {
            return null;
        }

        return value.DoubleValue;
    }

    private static double? MilliwattsToWatts(double? milliwatts) =>
        milliwatts is double mw and >= 0 ? Math.Round(mw / 1000, 1) : null;
}
