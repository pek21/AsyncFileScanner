namespace AsyncFileScanner.Models;

public sealed record ScanSummary(
    int TotalFilesScanned,
    int MatchedFilesCount,
    long TotalMatchedSizeBytes,
    TimeSpan ElapsedTime,
    IReadOnlyList<ScanResult> Results,
    IReadOnlyList<string> Errors,
    int MaxObservedConcurrency);