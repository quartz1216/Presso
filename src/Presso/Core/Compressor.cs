using System;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Presso.Core;

public sealed record CompressProgress(double Percent, string Status);

public sealed class Compressor
{
    private readonly string _ffmpeg;
    private readonly string _ffprobe;
    private const double Safety = 0.97;

    public Compressor(string ffmpegBinDir)
    {
        _ffmpeg = Path.Combine(ffmpegBinDir, "ffmpeg.exe");
        _ffprobe = Path.Combine(ffmpegBinDir, "ffprobe.exe");
    }

    public bool ToolsAvailable => File.Exists(_ffmpeg);

    public async Task<bool> CompressAsync(
        string inputPath,
        string outputPath,
        double targetMB,
        bool useHevc,
        IProgress<CompressProgress> progress,
        CancellationToken ct)
    {
        if (!File.Exists(_ffmpeg))
            throw new FileNotFoundException("ffmpeg.exe が見つかりません", _ffmpeg);
        if (!File.Exists(inputPath))
            throw new FileNotFoundException("入力ファイルが見つかりません", inputPath);

        progress.Report(new(0, "解析中..."));
        var info = await Probe.GetInfoAsync(_ffprobe, _ffmpeg, inputPath, ct);
        if (info.Duration <= 0)
        {
            progress.Report(new(0, "尺の取得に失敗、CRFモードへ"));
            return await CrfFallbackAsync(inputPath, outputPath, ChooseAudioKbps(60, targetMB), progress, ct);
        }

        long targetBytes = (long)Math.Floor(targetMB * 1024 * 1024);
        int audioKbps = ChooseAudioKbps(info.Duration, targetMB);
        int vKbps = ComputeVideoKbps(info.Duration, targetBytes, audioKbps);

        var codecs = useHevc ? new[] { "libx265", "libx264" } : new[] { "libx264" };

        foreach (var codec in codecs)
        {
            int tryV = vKbps;
            for (int i = 1; i <= 5; i++)
            {
                var (vf, fps) = DecideScaleFps(tryV, info.Width, info.Height);
                var label = codec == "libx265" ? "HEVC" : "H.264";
                var passBase = Path.Combine(Path.GetTempPath(),
                    "presso_" + Guid.NewGuid().ToString("N"));

                try
                {
                    bool ok = await Encode2PassAsync(codec, tryV, vf, fps, audioKbps,
                        info.Duration, inputPath, outputPath, passBase,
                        i, label, progress, ct);

                    if (ct.IsCancellationRequested) return false;
                    if (!ok) break;

                    long size = new FileInfo(outputPath).Length;
                    if (size <= targetBytes)
                    {
                        progress.Report(new(100,
                            $"完了 ({FormatSize(size)} / 目標 {targetMB:0.#}MB, {label})"));
                        return true;
                    }

                    progress.Report(new(0,
                        $"超過 {FormatSize(size)} > 目標 — 調整中..."));

                    // Proportional feedback toward target
                    double ratio = (double)targetBytes / size;
                    int next = (int)Math.Floor(tryV * ratio * Safety);
                    // Don't reduce by less than 5% per step to ensure progress
                    next = Math.Min(next, (int)(tryV * 0.95));
                    tryV = Math.Max(next, 40);
                }
                finally
                {
                    CleanupPasslogs(passBase);
                }
            }
        }

        progress.Report(new(0, "目標達成できず、CRFモードで再試行"));
        return await CrfFallbackAsync(inputPath, outputPath, audioKbps, progress, ct);
    }

    private async Task<bool> Encode2PassAsync(
        string codec, int vKbps, string vf, int fps, int audioKbps,
        double duration, string input, string output, string passBase,
        int attempt, string label,
        IProgress<CompressProgress> progress, CancellationToken ct)
    {
        string tag = codec == "libx265" ? "hvc1" : "avc1";
        const string preset = "medium";

        // Pass 1
        progress.Report(new(0, $"試行 {attempt} ({label}) {vKbps}kbps — pass 1/2"));
        string p1Args = string.Format(CultureInfo.InvariantCulture,
            "-y -hide_banner -loglevel error -progress pipe:1 -i \"{0}\" " +
            "-map_metadata -1 -an -c:v {1} -b:v {2}k -preset {3} -pix_fmt yuv420p " +
            "-tag:v {4} -vf {5} -r {6} -pass 1 -passlogfile \"{7}\" " +
            "-movflags +faststart -f mp4 NUL",
            input, codec, vKbps, preset, tag, vf, fps, passBase);

        int rc1 = await FfmpegRunner.RunWithProgressAsync(_ffmpeg, p1Args, duration,
            pct => progress.Report(new(pct * 0.5,
                $"試行 {attempt} ({label}) {vKbps}kbps — pass 1/2 {pct:0}%")),
            ct);
        if (rc1 != 0 || ct.IsCancellationRequested) return false;

        // Pass 2
        progress.Report(new(50, $"試行 {attempt} ({label}) {vKbps}kbps — pass 2/2"));
        string p2Args = string.Format(CultureInfo.InvariantCulture,
            "-y -hide_banner -loglevel error -progress pipe:1 -i \"{0}\" " +
            "-map_metadata -1 -c:v {1} -b:v {2}k -preset {3} -pix_fmt yuv420p " +
            "-tag:v {4} -vf {5} -r {6} -c:a aac -b:a {7}k -ac 1 " +
            "-pass 2 -passlogfile \"{8}\" -movflags +faststart \"{9}\"",
            input, codec, vKbps, preset, tag, vf, fps, audioKbps, passBase, output);

        int rc2 = await FfmpegRunner.RunWithProgressAsync(_ffmpeg, p2Args, duration,
            pct => progress.Report(new(50 + pct * 0.5,
                $"試行 {attempt} ({label}) {vKbps}kbps — pass 2/2 {pct:0}%")),
            ct);
        return rc2 == 0;
    }

    private async Task<bool> CrfFallbackAsync(
        string input, string output, int audioKbps,
        IProgress<CompressProgress> progress, CancellationToken ct)
    {
        progress.Report(new(0, "CRFモード (サイズ非保証)"));
        string args = string.Format(CultureInfo.InvariantCulture,
            "-y -hide_banner -loglevel error -progress pipe:1 -i \"{0}\" " +
            "-map_metadata -1 -c:v libx264 -preset medium -crf 32 " +
            "-pix_fmt yuv420p -vf scale=720:-2 -r 24 -c:a aac -b:a {1}k -ac 1 " +
            "-movflags +faststart \"{2}\"",
            input, audioKbps, output);
        int rc = await FfmpegRunner.RunWithProgressAsync(_ffmpeg, args, 0,
            _ => { /* CRF 1-pass progress would need duration; UI just shows running */ },
            ct);
        if (rc == 0)
        {
            long size = new FileInfo(output).Length;
            progress.Report(new(100, $"完了 (CRF, {FormatSize(size)})"));
            return true;
        }
        progress.Report(new(0, "失敗"));
        return false;
    }

    private static int ChooseAudioKbps(double duration, double targetMB)
    {
        // Short clips can afford better audio; long clips squeeze it.
        // Aim audio ~ 8-12% of total budget, clamped to [32, 96].
        double targetBits = targetMB * 1024 * 1024 * 8;
        double share = duration > 0 ? targetBits * 0.10 / duration / 1000 : 48;
        return (int)Math.Clamp(Math.Round(share), 32, 96);
    }

    private static int ComputeVideoKbps(double duration, long targetBytes, int audioKbps)
    {
        double targetBits = targetBytes * 8.0 * Safety;
        double audioBits = audioKbps * 1000.0 * Math.Max(duration, 1);
        double vBits = targetBits - audioBits;
        return Math.Max((int)Math.Floor(vBits / Math.Max(duration, 1) / 1000.0), 50);
    }

    private static (string vf, int fps) DecideScaleFps(int vKbps, int inW, int inH)
    {
        int fps = 30;
        int maxW = 1280;
        if (vKbps < 400) maxW = 720;
        if (vKbps < 200) maxW = 540;
        if (vKbps < 120) maxW = 360;
        if (vKbps < 80) maxW = 240;
        if (vKbps < 150) fps = 24;
        if (vKbps < 100) fps = 20;
        if (vKbps < 60) fps = 15;
        int tW = Math.Min(inW, maxW);
        return ($"scale={tW}:-2", fps);
    }

    private static void CleanupPasslogs(string passBase)
    {
        try
        {
            var dir = Path.GetDirectoryName(passBase) ?? "";
            var prefix = Path.GetFileName(passBase);
            if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) return;
            foreach (var f in Directory.GetFiles(dir, prefix + "*"))
            {
                try { File.Delete(f); } catch { /* ignore */ }
            }
        }
        catch { /* ignore */ }
    }

    private static string FormatSize(long bytes)
    {
        if (bytes >= 1024 * 1024) return $"{bytes / 1024.0 / 1024.0:0.##}MB";
        if (bytes >= 1024) return $"{bytes / 1024.0:0.##}KB";
        return $"{bytes}B";
    }
}
