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

    public static string FfmpegBinDir
    {
        get
        {
            var p1 = Path.Combine(AppDir, "ffmpeg", "bin");
            if (File.Exists(Path.Combine(p1, "ffmpeg.exe"))) return p1;

            // Fallback for local dev (from src/Presso/bin/Debug/net8.0-windows/)
            var p2 = Path.GetFullPath(Path.Combine(AppDir, "..", "..", "..", "..", "..", "ffmpeg", "bin"));
            if (File.Exists(Path.Combine(p2, "ffmpeg.exe"))) return p2;

            return p1;
        }
    }

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
