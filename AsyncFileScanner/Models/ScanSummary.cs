namespace AsyncFileScanner.Models;

public sealed record ScanSummary(
    int ScannedFiles,
    int SuccessfulFilesCount,
    long TotalSizeBytes,
    TimeSpan ElapsedTime,
    IReadOnlyList<ScanResult> Results,
    IReadOnlyList<string> Errors);