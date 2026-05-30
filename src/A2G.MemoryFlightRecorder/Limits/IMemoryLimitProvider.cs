namespace A2G.MemoryFlightRecorder.Limits;

public interface IMemoryLimitProvider
{
    MemoryLimit GetMemoryLimit();
}
