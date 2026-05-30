namespace DotNet.MemoryFlightRecorder.Limits;

public interface IMemoryLimitProvider
{
    MemoryLimit GetMemoryLimit();
}
