namespace ClustersCopyAndAnalyze.Services.Copy;

internal static class CopyingService
{
    public static async Task CopyDirectoryAsync(string sourceDir, string destinationDir, IProgress<double> progress = null)
    {
        // Проверяем существование исходной директории
        if (!ValidatePaths(sourceDir, destinationDir, out string errorMessage))
            throw new DirectoryNotFoundException($"Source directory {sourceDir} not found.");

        Directory.CreateDirectory(destinationDir);

        string[] files = Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories);
        double totalFiles = files.Length;
        double copiedFiles = 0;

        foreach (var file in Directory.GetFiles(sourceDir))
        {
            string destFile = Path.Combine(destinationDir, Path.GetFileName(file));
            File.Copy(file, destFile, true);
            copiedFiles++;
            progress?.Report(copiedFiles / totalFiles * 100);
        }

        foreach (var dir in Directory.GetDirectories(sourceDir))
        {
            string destDir = Path.Combine(destinationDir, Path.GetFileName(dir));
            await CopyDirectoryAsync(dir, destDir, progress);
        }
    }

    
    /// <summary>
    /// Проверяет корректность исходного и целевого путей для копирования файлов.
    /// </summary>
    /// <param name="sourcePath">Путь к исходной папке.</param>
    /// <param name="destPath">Путь к целевой папке.</param>
    /// <param name="errorMessage">Сообщение об ошибке, если проверка не пройдена.</param>
    /// <param name="files">Список файлов в исходной папке, если она не пуста.</param>
    /// <returns>True, если пути прошли все проверки; False в противном случае.</returns>
    /// <remarks>
    /// Выполняет следующие проверки:
    /// 1. Оба пути заполнены (не пустые и не состоят только из пробелов).
    /// 2. Исходная папка существует.
    /// 3. Целевая папка существует или может быть создана.
    /// 4. Исходная и целевая папки не совпадают.
    /// 5. Исходная папка не пуста (содержит файлы).
    /// 6. Целевая папка не является дочерней для исходной.
    /// </remarks>
    public static bool ValidatePaths(string sourcePath, string destPath, out string errorMessage)
    {
        errorMessage = string.Empty;
        // Проверка 1: Оба пути заполнены
        if (string.IsNullOrWhiteSpace(sourcePath) || string.IsNullOrWhiteSpace(destPath))
        {
            errorMessage = "Выберите исходную и целевую папки!";
            return false;
        }

        // Проверка 2: Исходная папка существует
        if (!Directory.Exists(sourcePath))
        {
            errorMessage = "Исходная папка не существует!";
            return false;
        }

        // Проверка 3: Целевая папка существует или может быть создана
        try
        {
            if (!Directory.Exists(destPath))
            {
                Directory.CreateDirectory(destPath);
            }
        }
        catch (UnauthorizedAccessException)
        {
            errorMessage = "Нет прав доступа для создания или использования целевой папки!";
            return false;
        }
        catch (IOException ex)
        {
            errorMessage = $"Ошибка при создании целевой папки: {ex.Message}";
            return false;
        }

        // Проверка 4: Исходная и целевая папки не совпадают
        if (sourcePath.Equals(destPath, StringComparison.OrdinalIgnoreCase))
        {
            errorMessage = "Исходная и целевая папки не могут быть одинаковыми!";
            return false;
        }

        // Проверка 5: Исходная папка не пуста
        var files = Directory.GetFiles(sourcePath);
        if (files.Length == 0)
        {
            errorMessage = "Исходная папка пуста!";
            return false;
        }

        // Проверка 6: Целевая папка не является дочерней для исходной
        string normalizedSource = Path.GetFullPath(sourcePath).TrimEnd(Path.DirectorySeparatorChar);
        string normalizedDest = Path.GetFullPath(destPath).TrimEnd(Path.DirectorySeparatorChar);
        if (normalizedDest.StartsWith(normalizedSource + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            errorMessage = "Нельзя копировать папку в её собственную дочернюю папку!";
            return false;
        }

        return true;
    }
}
