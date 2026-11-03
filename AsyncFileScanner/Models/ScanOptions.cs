namespace AsyncFileScanner.Models;

public sealed record ScanOptions(
    string DirectoryPath,
    string[] Extensions,
    long MinSizeBytes,
    long MaxSizeBytes,
    int MaxConcurrency);