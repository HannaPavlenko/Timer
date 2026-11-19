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
}
