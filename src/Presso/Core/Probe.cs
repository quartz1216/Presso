using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Presso.Core;

public static class Probe
{
    public sealed record VideoInfo(double Duration, int Width, int Height);

    public static async Task<VideoInfo> GetInfoAsync(string ffprobe, string ffmpeg, string path, CancellationToken ct)
    {
        double duration = 0;
        int w = 1280, h = 720;

        if (File.Exists(ffprobe))
        {
            var dur = await RunCaptureAsync(ffprobe,
                $"-v error -show_entries format=duration -of default=nw=1:nk=1 \"{path}\"", ct);
            if (double.TryParse(dur.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var d))
                duration = d;

            var dim = await RunCaptureAsync(ffprobe,
                $"-v error -select_streams v:0 -show_entries stream=width,height -of csv=s=x:p=0 \"{path}\"", ct);
            var m = Regex.Match(dim.Trim(), @"^(\d+)x(\d+)$");
            if (m.Success)
            {
                w = int.Parse(m.Groups[1].Value);
                h = int.Parse(m.Groups[2].Value);
            }
        }

        if (duration <= 0 && File.Exists(ffmpeg))
        {
            // fallback parse
            var raw = await RunCaptureStderrAsync(ffmpeg, $"-hide_banner -i \"{path}\"", ct);
            var m = Regex.Match(raw, @"Duration:\s*(\d+):(\d+):(\d+\.\d+)");
            if (m.Success)
            {
                double hh = double.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
                double mm = double.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture);
                double ss = double.Parse(m.Groups[3].Value, CultureInfo.InvariantCulture);
                duration = hh * 3600 + mm * 60 + ss;
            }
        }

        return new VideoInfo(duration, w, h);
    }

    private static async Task<string> RunCaptureAsync(string exe, string args, CancellationToken ct)
    {
        var psi = new ProcessStartInfo
        {
            FileName = exe,
            Arguments = args,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        using var p = Process.Start(psi)!;
        var stdout = await p.StandardOutput.ReadToEndAsync();
        await p.WaitForExitAsync(ct);
        return stdout;
    }

    private static async Task<string> RunCaptureStderrAsync(string exe, string args, CancellationToken ct)
    {
        var psi = new ProcessStartInfo
        {
            FileName = exe,
            Arguments = args,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        using var p = Process.Start(psi)!;
        var stderr = await p.StandardError.ReadToEndAsync();
        await p.WaitForExitAsync(ct);
        return stderr;
    }
}
