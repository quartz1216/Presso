using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Presso.Core;

public static class FfmpegRunner
{
    public static async Task<int> RunWithProgressAsync(
        string ffmpeg,
        string args,
        double totalDurationSec,
        Action<double> onPercent,
        CancellationToken ct)
    {
        var psi = new ProcessStartInfo
        {
            FileName = ffmpeg,
            Arguments = args,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        using var p = new Process { StartInfo = psi, EnableRaisingEvents = true };
        p.OutputDataReceived += (_, e) =>
        {
            if (string.IsNullOrEmpty(e.Data) || totalDurationSec <= 0) return;
            // ffmpeg -progress emits key=value lines
            // Look for out_time_us=NNN
            if (e.Data.StartsWith("out_time_us=", StringComparison.Ordinal))
            {
                if (long.TryParse(e.Data.AsSpan("out_time_us=".Length), out var us))
                {
                    var sec = us / 1_000_000.0;
                    var pct = Math.Clamp(sec / totalDurationSec * 100.0, 0, 100);
                    onPercent(pct);
                }
            }
            else if (e.Data.StartsWith("progress=end", StringComparison.Ordinal))
            {
                onPercent(100);
            }
        };
        p.ErrorDataReceived += (_, _) => { /* swallow stderr */ };

        if (!p.Start()) return -1;
        p.BeginOutputReadLine();
        p.BeginErrorReadLine();

        using var reg = ct.Register(() =>
        {
            try { if (!p.HasExited) p.Kill(true); } catch { /* ignore */ }
        });

        try
        {
            await p.WaitForExitAsync(CancellationToken.None);
        }
        catch { /* ignore */ }

        return p.ExitCode;
    }
}
