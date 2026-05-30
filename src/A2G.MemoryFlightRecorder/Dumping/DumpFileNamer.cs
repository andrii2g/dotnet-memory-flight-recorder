namespace A2G.MemoryFlightRecorder.Dumping;

public static class DumpFileNamer
{
    public static string CreateDumpPath(string dumpDirectory, int processId, DateTimeOffset timestampUtc)
    {
        var safeTimestamp = timestampUtc.UtcDateTime.ToString("yyyyMMdd_HHmmss_fff");
        return Path.Combine(dumpDirectory, $"memory_{safeTimestamp}_{processId}.dmp");
    }
}
