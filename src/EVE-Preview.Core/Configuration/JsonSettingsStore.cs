using System.Text;
using System.Text.Json;

namespace EvePreview.Core;

public enum SettingsLoadStatus
{
    Loaded,
    Missing,
    RecoveredCorrupt
}

public sealed record SettingsLoadResult(
    AppSettings Settings,
    SettingsLoadStatus Status,
    string? Diagnostic = null,
    string? BackupPath = null,
    bool RequiresConfirmation = false);

public sealed class SettingsPersistenceException : IOException
{
    public SettingsPersistenceException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

public sealed class JsonSettingsStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true };
    private readonly string _path;
    private readonly object _ioLock = new();
    private bool _corruptConfigurationPendingReset;

    public JsonSettingsStore(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        _path = System.IO.Path.GetFullPath(path);
    }

    public string Path => _path;

    public SettingsLoadResult Load()
    {
        lock (_ioLock)
        {
            return LoadCore();
        }
    }

    private SettingsLoadResult LoadCore()
    {
        if (!File.Exists(_path))
        {
            var settings = new AppSettings();
            SaveCore(settings);
            return new SettingsLoadResult(settings, SettingsLoadStatus.Loaded);
        }

        string json;
        try
        {
            json = File.ReadAllText(_path, Encoding.UTF8);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new SettingsPersistenceException(
                $"Unable to read configuration '{_path}'. The original file was left unchanged.",
                exception);
        }

        try
        {
            var repair = AppSettingsRepair.Repair(json);
            if (!repair.WasRepaired)
            {
                _corruptConfigurationPendingReset = false;
                return new SettingsLoadResult(repair.Settings, SettingsLoadStatus.Loaded);
            }

            return PersistRepair(
                repair.Settings,
                $"Repaired configuration modules: {string.Join(", ", repair.RepairedModules)}.");
        }
        catch (Exception exception) when (exception is JsonException or InvalidDataException or NotSupportedException)
        {
            return PersistRepair(
                new AppSettings(),
                $"The configuration could not be parsed and was replaced with defaults. {exception.Message}");
        }
    }

    private SettingsLoadResult PersistRepair(AppSettings settings, string detail)
    {
        _corruptConfigurationPendingReset = true;
        var backupPath = TryPreserveCorruptFile();
        if (backupPath is null)
        {
            throw new SettingsPersistenceException(
                $"Unable to back up invalid configuration '{_path}'. The original file was left unchanged.",
                new IOException("The diagnostic backup could not be created."));
        }

        SaveAfterCorruption(settings);
        return new SettingsLoadResult(
            settings,
            SettingsLoadStatus.RecoveredCorrupt,
            $"{detail} The repaired configuration was saved. The original file was preserved at '{backupPath}'.",
            backupPath);
    }

    public void Save(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        lock (_ioLock)
        {
            if (_corruptConfigurationPendingReset)
            {
                throw new InvalidOperationException(
                    "The loaded configuration was corrupt. Confirm a reset before saving over the original file.");
            }

            SaveCore(settings);
        }
    }

    public void SaveAfterCorruption(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        lock (_ioLock)
        {
            _corruptConfigurationPendingReset = false;
            try
            {
                SaveCore(settings);
            }
            catch
            {
                _corruptConfigurationPendingReset = true;
                throw;
            }
        }
    }

    private void SaveCore(AppSettings settings)
    {
        var copy = settings.DeepCopy();
        copy.Normalize();
        var validation = copy.Validate();
        if (!validation.IsValid)
        {
            throw new InvalidDataException(string.Join(Environment.NewLine, validation.Errors));
        }

        var directory = System.IO.Path.GetDirectoryName(_path)
            ?? throw new InvalidOperationException($"Configuration path '{_path}' has no parent directory.");
        if (!Directory.Exists(directory))
        {
            throw new DirectoryNotFoundException($"Configuration directory '{directory}' does not exist.");
        }

        var tempPath = System.IO.Path.Combine(
            directory,
            $".{System.IO.Path.GetFileName(_path)}.{Guid.NewGuid():N}.tmp");
        try
        {
            using (var stream = new FileStream(
                       tempPath,
                       FileMode.CreateNew,
                       FileAccess.Write,
                       FileShare.None,
                       bufferSize: 4096,
                       FileOptions.WriteThrough))
            {
                JsonSerializer.Serialize(stream, copy, SerializerOptions);
                stream.Flush(flushToDisk: true);
            }

            if (File.Exists(_path))
            {
                File.Replace(tempPath, _path, _path + ".bak", ignoreMetadataErrors: true);
            }
            else
            {
                File.Move(tempPath, _path);
            }

            _corruptConfigurationPendingReset = false;
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            TryDelete(tempPath);
            throw new SettingsPersistenceException(
                $"Unable to save configuration '{_path}'. The previous file was kept.",
                exception);
        }
    }

    private string? TryPreserveCorruptFile()
    {
        var backupPath = $"{_path}.corrupt-{DateTime.UtcNow:yyyyMMddHHmmssfff}.bak";
        try
        {
            File.Copy(_path, backupPath, overwrite: false);
            return backupPath;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
        }
    }
}

