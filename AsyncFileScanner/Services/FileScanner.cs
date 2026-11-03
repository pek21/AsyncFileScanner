using System.Diagnostics;
using AsyncFileScanner.Models;

namespace AsyncFileScanner.Services;

public sealed class FileScanner
{
    private readonly FileProbe _probe;

    public FileScanner(FileProbe probe)
    {
        _probe = probe;
    }

    public async Task<ScanSummary> ScanAsync(
        ScanOptions options,
        CancellationToken cancellationToken)
    {
        ValidateOptions(options);

        var stopwatch = Stopwatch.StartNew();
        var results = new List<ScanResult>();
        var errors = new List<string>();

        int scannedFiles = 0;
        long totalBytes = 0;

        var files = Directory.EnumerateFiles(
            options.DirectoryPath,
            "*.*",
            SearchOption.AllDirectories);

        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var extension = Path.GetExtension(file).ToLowerInvariant();

            if (!options.Extensions.Contains(extension))
                continue;

            scannedFiles++;

            try
            {
                var fileInfo = new FileInfo(file);

                await _probe.ReadHeaderAsync(file, cancellationToken);

                var fileName = Path.GetFileName(file);
                var result = new ScanResult(fileName, fileInfo.Length);
                results.Add(result);

                totalBytes += fileInfo.Length;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (UnauthorizedAccessException ex)
            {
                errors.Add($"Нет доступа к файлу: {file} - {ex.Message}");
            }
            catch (IOException ex)
            {
                errors.Add($"Ошибка ввода-вывода: {file} - {ex.Message}");
            }
            catch (Exception ex)
            {
                errors.Add($"Неизвестная ошибка: {file} - {ex.Message}");
            }
        }

        stopwatch.Stop();

        return new ScanSummary(
            scannedFiles,
            results.Count,
            totalBytes,
            stopwatch.Elapsed,
            results,
            errors);
    }

    private static void ValidateOptions(ScanOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.DirectoryPath))
            throw new ArgumentException("Путь к директории не может быть пустым", nameof(options.DirectoryPath));

        if (!Directory.Exists(options.DirectoryPath))
            throw new DirectoryNotFoundException($"Директория не найдена: {options.DirectoryPath}");

        if (options.Extensions == null || options.Extensions.Length == 0)
            throw new ArgumentException("Список расширений не может быть пустым", nameof(options.Extensions));
    }
}