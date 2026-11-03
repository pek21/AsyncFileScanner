namespace AsyncFileScanner.Models;

public sealed record ScanOptions(
    string DirectoryPath,
    string[] Extensions);