namespace Houston.App.Sensors;

/// <summary>One sample pushed to the UI. Serialized as the versioned contract described in AGENTS.md.</summary>
public sealed record SensorSnapshot(int V, long T, CpuSnapshot? Cpu, GpuSnapshot? Gpu)
{
    public const int CurrentVersion = 1;
}
