using AsyncFileScanner.Models;
using AsyncFileScanner.Services;

Console.WriteLine("=== FILE SCANNER ===\n");

Console.Write("Введите путь к директории: ");
var directoryPath = Console.ReadLine()?.Trim() ?? throw new InvalidOperationException("Пустой путь");

Console.Write("Введите расширения через запятую (.cs,.txt): ");
var extensions = (Console.ReadLine()?.Trim() ?? "")
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
    .Select(e => e.ToLowerInvariant())
    .ToArray();

Console.Write("Минимальный размер (КБ, 0 для без ограничений): ");
long minKb = long.Parse(Console.ReadLine()?.Trim() ?? "0");

Console.Write("Максимальный размер (КБ, 0 для без ограничений): ");
long maxKb = long.Parse(Console.ReadLine()?.Trim() ?? "0");

Console.Write("Максимальное количество одновременных операций: ");
int maxConcurrency = int.Parse(Console.ReadLine()?.Trim() ?? "4");

long minBytes = minKb > 0 ? minKb * 1024 : 0;
long maxBytes = maxKb > 0 ? maxKb * 1024 : long.MaxValue;

var options = new ScanOptions(
    DirectoryPath: directoryPath,
    Extensions: extensions,
    MinSizeBytes: minBytes,
    MaxSizeBytes: maxBytes,
    MaxConcurrency: maxConcurrency);

var cts = new CancellationTokenSource();
var scanner = new FileScanner(new FileProbe());

Console.WriteLine("\nСканирование... Нажмите Enter для отмены.\n");

var scanTask = scanner.ScanAsync(options, cts.Token);

var cancelTask = Task.Run(() =>
{
    Console.ReadLine();
    cts.Cancel();
});

try
{
    var completedTask = await Task.WhenAny(scanTask, cancelTask);

    if (completedTask == cancelTask)
    {
        await scanTask;
    }

    var result = await scanTask;

    Console.WriteLine("\n=== РЕЗУЛЬТАТ СКАНИРОВАНИЯ ===\n");

    int index = 1;
    foreach (var r in result.Results)
    {
        Console.WriteLine($"[{index++}] {r.FileName,-30} {FormatSize(r.FileSizeBytes)}");
    }

    Console.WriteLine($"\nПросмотрено файлов: {result.TotalFilesScanned}");
    Console.WriteLine($"Подходящих файлов: {result.MatchedFilesCount}");
    Console.WriteLine($"Общий размер: {FormatSize(result.TotalMatchedSizeBytes)}");
    Console.WriteLine($"Ошибок: {result.Errors.Count}");
    Console.WriteLine($"Время выполнения: {result.ElapsedTime.TotalSeconds:F2} сек.");
    Console.WriteLine($"Ограничение: {maxConcurrency}");
    Console.WriteLine($"Максимум одновременно обрабатываемых файлов: {result.MaxObservedConcurrency}");

    if (result.Errors.Count > 0)
    {
        Console.WriteLine("\n--- Ошибки ---");
        foreach (var err in result.Errors)
            Console.WriteLine($"{err}");
    }
}
catch (OperationCanceledException)
{
    Console.WriteLine("\n Сканирование отменено пользователем.");
}
catch (Exception ex)
{
    Console.WriteLine($"\n💥 Ошибка: {ex.Message}");
}

static string FormatSize(long bytes)
{
    return bytes switch
    {
        >= 1_073_741_824 => $"{bytes / 1_073_741_824.0:F2} ГБ",
        >= 1_048_576 => $"{bytes / 1_048_576.0:F2} МБ",
        >= 1_024 => $"{bytes / 1_024.0:F2} КБ",
        _ => $"{bytes} байт"
    };
}