using AsyncFileScanner.Models;
using AsyncFileScanner.Services;

Console.WriteLine("=== FILE SCANNER ===");
Console.WriteLine();

Console.WriteLine("Введите путь к директории:");
var directoryPath = Console.ReadLine()?.Trim() ?? string.Empty;

Console.WriteLine();
Console.WriteLine("Введите расширения через запятую:");
var extensionsInput = Console.ReadLine()?.Trim() ?? string.Empty;
var extensions = extensionsInput
    .Split(',', StringSplitOptions.RemoveEmptyEntries)
    .Select(e => e.Trim().ToLowerInvariant())
    .ToArray();

var probe = new FileProbe();
var scanner = new FileScanner(probe);

var cts = new CancellationTokenSource();

var options = new ScanOptions(directoryPath, extensions);
var scanTask = scanner.ScanAsync(options, cts.Token);

Console.WriteLine();
Console.WriteLine("Найденные файлы:");
Console.WriteLine();

var cancelTask = Task.Run(() =>
{
    Console.WriteLine("Нажмите Enter для отмены...");
    Console.ReadLine();
    cts.Cancel();
});

try
{
    var summary = await Task.WhenAny(scanTask, cancelTask) == scanTask
        ? await scanTask
        : throw new OperationCanceledException();

    int index = 1;
    foreach (var result in summary.Results)
    {
        Console.WriteLine($"[{index++}] {result.FileName,-20} {result.FileSizeBytes} байт");
    }

    Console.WriteLine();
    Console.WriteLine("Сканирование завершено.");
    Console.WriteLine();
    Console.WriteLine($"Всего найдено файлов: {summary.SuccessfulFilesCount}");
    Console.WriteLine($"Общий размер: {summary.TotalSizeBytes} байт");
    Console.WriteLine($"Время выполнения: {summary.ElapsedTime.TotalMilliseconds:F2} мс");

    if (summary.Errors.Count > 0)
    {
        Console.WriteLine();
        Console.WriteLine($"Ошибок: {summary.Errors.Count}");
        foreach (var error in summary.Errors)
        {
            Console.WriteLine($"  - {error}");
        }
    }
}
catch (OperationCanceledException)
{
    Console.WriteLine();
    Console.WriteLine("Сканирование отменено пользователем.");

    if (scanTask.IsCompletedSuccessfully)
    {
        var summary = scanTask.Result;
        Console.WriteLine($"Обработано файлов: {summary.ScannedFiles}");
        Console.WriteLine($"Найдено файлов: {summary.SuccessfulFilesCount}");
    }
}
catch (Exception ex)
{
    Console.WriteLine();
    Console.WriteLine($"Ошибка: {ex.Message}");
}