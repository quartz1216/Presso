using System;
using System.IO;

namespace Presso.Core;

public static class AppPaths
{
    public static string AppDir
    {
        get
        {
            var exe = Environment.ProcessPath;
            if (!string.IsNullOrEmpty(exe))
                return Path.GetDirectoryName(exe) ?? AppContext.BaseDirectory;
            return AppContext.BaseDirectory;
        }
    }

    public static string FfmpegBinDir => Path.Combine(AppDir, "ffmpeg", "bin");

    public static readonly string[] SupportedExtensions =
        { ".mp4", ".mov", ".mkv", ".webm", ".avi" };

    public static bool IsSupportedVideo(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        return Array.IndexOf(SupportedExtensions, ext) >= 0;
    }

    public static string BuildOutputPath(string inputPath, string? overrideDir = null)
    {
        var dir = overrideDir ?? Path.GetDirectoryName(inputPath) ?? AppDir;
        var name = Path.GetFileNameWithoutExtension(inputPath);
        return Path.Combine(dir, name + "_presso.mp4");
    }
}
