namespace PrintStamp.Core;

public static class HiddenFolder
{
    public static string Ensure(string path)
    {
        Directory.CreateDirectory(path);
        if (OperatingSystem.IsWindows())
        {
            var dir = new DirectoryInfo(path);
            const FileAttributes want = FileAttributes.Hidden | FileAttributes.System;
            if ((dir.Attributes & want) != want) dir.Attributes |= want;
        }
        return path;
    }
}