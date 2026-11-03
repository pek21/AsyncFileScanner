using System.Diagnostics;
using System.Threading;
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

        int activeOperations = 0;
        int maxObservedConcurrency = 0;

        using var semaphore = new SemaphoreSlim(options.MaxConcurrency, options.MaxConcurrency);

        var matchingFiles = Directory.EnumerateFiles(
                options.DirectoryPath,
                "*.*",
                SearchOption.AllDirectories)
            .Where(f => options.Extensions.Contains(
                Path.GetExtension(f).ToLowerInvariant()))
            .ToList();

        int totalFilesScanned = matchingFiles.Count;

        var tasks = matchingFiles.Select(path => ProcessFileWithLimitsAsync(
            path,
            options,
            semaphore,
            () => Interlocked.Increment(ref activeOperations),
            () => Interlocked.Decrement(ref activeOperations),
            () => Interlocked.CompareExchange(ref maxObservedConcurrency, activeOperations, maxObservedConcurrency),
            cancellationToken));

        var completedResults = await Task.WhenAll(tasks);

        foreach (var result in completedResults)
        {
            if (result.IsSuccess)
            {
                results.Add(result.Result!);
            }
            else if (!string.IsNullOrEmpty(result.Error))
            {
                errors.Add(result.Error);
            }
        }

        stopwatch.Stop();

        return new ScanSummary(
            TotalFilesScanned: totalFilesScanned,
            MatchedFilesCount: results.Count,
            TotalMatchedSizeBytes: results.Sum(r => r.FileSizeBytes),
            ElapsedTime: stopwatch.Elapsed,
            Results: results.AsReadOnly(),
            Errors: errors.AsReadOnly(),
            MaxObservedConcurrency: maxObservedConcurrency);
    }

    private async Task<FileProcessingResult> ProcessFileWithLimitsAsync(
        string path,
        ScanOptions options,
        SemaphoreSlim semaphore,
        Action incrementCounter,
        Action decrementCounter,
        Func<int> updateMaxCounter,
        CancellationToken token)
    {
        await semaphore.WaitAsync(token);

        try
        {
            incrementCounter();
            updateMaxCounter();

            try
            {
                var fileInfo = new FileInfo(path);

                if (fileInfo.Length < options.MinSizeBytes ||
                    fileInfo.Length > options.MaxSizeBytes)
                {
                    return new FileProcessingResult(null, null);
                }

                await _probe.ReadHeaderAsync(path, token);

                return new FileProcessingResult(
                    new ScanResult(Path.GetFileName(fileInfo.Name), fileInfo.Length),
                    null);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                throw;
            }
            catch (UnauthorizedAccessException ex)
            {
                return new FileProcessingResult(null, $"Нет доступа: {path} - {ex.Message}");
            }
            catch (IOException ex)
            {
                return new FileProcessingResult(null, $"Ошибка I/O: {path} - {ex.Message}");
            }
            catch (Exception ex)
            {
                return new FileProcessingResult(null, $"Ошибка: {path} - {ex.Message}");
            }
            finally
            {
                decrementCounter();
            }
        }
        finally
        {
            semaphore.Release();
        }
    }

    private static void ValidateOptions(ScanOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.DirectoryPath))
            throw new ArgumentException("Путь не может быть пустым", nameof(options.DirectoryPath));
        if (!Directory.Exists(options.DirectoryPath))
            throw new DirectoryNotFoundException($"Директория не найдена: {options.DirectoryPath}");
        if (options.Extensions is not { Length: > 0 })
            throw new ArgumentException("Укажите хотя бы одно расширение", nameof(options.Extensions));
        if (options.MinSizeBytes < 0)
            throw new ArgumentOutOfRangeException(nameof(options.MinSizeBytes), "Минимальный размер не может быть отрицательным");
        if (options.MaxSizeBytes < options.MinSizeBytes)
            throw new ArgumentException("Максимальный размер должен быть >= минимального", nameof(options.MaxSizeBytes));
        if (options.MaxConcurrency < 1)
            throw new ArgumentOutOfRangeException(nameof(options.MaxConcurrency), "Параллелизм должен быть >= 1");
    }

    private sealed record FileProcessingResult(ScanResult? Result, string? Error)
    {
        public bool IsSuccess => Result is not null && Error is null;
    }
}