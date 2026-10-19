using System.Diagnostics;
using LibreHardwareMonitor.Hardware;

namespace Houston.App.Sensors;

/// <summary>
/// Samples CPU and GPU sensors on a background task. LibreHardwareMonitor is not thread-safe, so
/// <see cref="Computer"/> is opened, updated and closed exclusively on that task.
/// </summary>
public sealed class SensorService : IAsyncDisposable
{
    private static readonly TimeSpan Interval = TimeSpan.FromMilliseconds(1000);

    private readonly CancellationTokenSource _cancellation = new();
    private Task? _loop;

    /// <summary>Raised on a thread-pool thread once per tick.</summary>
    public event Action<SensorSnapshot>? SnapshotReady;

    public void Start() => _loop ??= Task.Run(() => RunAsync(_cancellation.Token));

    public async ValueTask DisposeAsync()
    {
        await _cancellation.CancelAsync();
        if (_loop is not null)
        {
            await _loop;
        }

        _cancellation.Dispose();
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        var computer = new Computer { IsCpuEnabled = true, IsGpuEnabled = true };
        using var counters = new WindowsCounters();
        try
        {
            computer.Open();
            counters.Collect();
            foreach (var hardware in computer.Hardware)
            {
                hardware.Update();
            }

            var cpu = computer.Hardware.FirstOrDefault(h => h.HardwareType == HardwareType.Cpu) is { } cpuHardware
                ? new CpuReader(cpuHardware)
                : null;
            var gpu = GpuReader.SelectPreferred(computer.Hardware) is { } gpuHardware
                ? new GpuReader(gpuHardware)
                : null;

            using var timer = new PeriodicTimer(Interval);
            do
            {
                Sample(cpu, gpu, counters);
            }
            while (await timer.WaitForNextTickAsync(cancellationToken));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        finally
        {
            // Releases the driver handles so nothing stays loaded after the widget closes.
            computer.Close();
        }
    }

    private void Sample(CpuReader? cpu, GpuReader? gpu, WindowsCounters counters)
    {
        try
        {
            cpu?.Update();
            gpu?.Update();
            counters.Collect();

            // LibreHardwareMonitor wins when it has a reading (it does with the PawnIO driver);
            // otherwise fall back to what Windows exposes without a driver.
            var cpuSnapshot = cpu?.Read() is { } c
                ? c with { ClockMhz = c.ClockMhz ?? counters.CpuClockMhz, PowerW = c.PowerW ?? counters.CpuPackagePowerW }
                : null;
            var gpuSnapshot = gpu?.Read() is { } g
                ? g with { PowerW = g.PowerW ?? (gpu.IsIntegratedIntel ? counters.IntegratedGraphicsPowerW : null) }
                : null;

            var snapshot = new SensorSnapshot(
                SensorSnapshot.CurrentVersion,
                DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                cpuSnapshot,
                gpuSnapshot);
            SnapshotReady?.Invoke(snapshot);
        }
        catch (Exception ex)
        {
            // A flaky sensor must not stop sampling; the UI keeps the last values and the next tick retries.
            Debug.WriteLine($"Sensor sample failed: {ex}");
        }
    }
}
