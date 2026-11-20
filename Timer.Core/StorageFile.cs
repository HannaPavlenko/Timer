namespace Timer.Core;

internal static class StorageFile
{
    internal static string? ReadTextIfPresent(string path)
    {
        try
        {
            return File.ReadAllText(path);
        }
        catch (IOException exception) when (exception is FileNotFoundException or DirectoryNotFoundException)
        {
            // An absent parent is normal on first launch. A file blocking it must surface as a storage error.
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
            return null;
        }
    }

    internal static void WriteTextAtomically(string path, string name, string contents)
    {
        string directory = Path.GetDirectoryName(Path.GetFullPath(path))!;
        Directory.CreateDirectory(directory);
        string temporaryPath = Path.Combine(directory, $"{name}-{Guid.NewGuid():N}.tmp");

        try
        {
            File.WriteAllText(temporaryPath, contents);
            File.Move(temporaryPath, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);
        }
    }
}
