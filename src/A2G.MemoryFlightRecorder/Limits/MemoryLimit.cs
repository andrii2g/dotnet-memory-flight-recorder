namespace A2G.MemoryFlightRecorder.Limits;

public sealed record MemoryLimit(long? Bytes, string Source)
{
    public bool IsAvailable => Bytes is > 0;

    public static MemoryLimit Available(long bytes, string source) => new(bytes, source);

    public static MemoryLimit Unavailable(string source) => new(null, source);
}
