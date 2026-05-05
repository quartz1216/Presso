using System;
using System.Collections.Generic;
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
    private const int MinVideoKbps = 5;
    private const int MinAudioKbps = 8;
    private const int GpuMinKbps = 100;

    private bool _gpuDetected;
    private string? _gpuH264;
    private string? _gpuHevc;
    private GpuEncType _gpuType = GpuEncType.None;
    private readonly SemaphoreSlim _gpuLock = new(1, 1);

    private enum GpuEncType { None, Nvenc, Qsv, Amf }

    public Compressor(string ffmpegBinDir)
    {
        _ffmpeg = Path.Combine(ffmpegBinDir, "ffmpeg.exe");
        _ffprobe = Path.Combine(ffmpegBinDir, "ffprobe.exe");
    }

    public bool ToolsAvailable => File.Exists(_ffmpeg);

    public string AcceleratorLabel => _gpuType switch
    {
        GpuEncType.Nvenc => "NVENC",
        GpuEncType.Qsv   => "Intel QSV",
        GpuEncType.Amf   => "AMD AMF",
        _                => "CPU",
    };

    public Task PrewarmAsync(CancellationToken ct) => EnsureGpuDetectedAsync(ct);

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

        await EnsureGpuDetectedAsync(ct);

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

        var codecs = BuildCodecList(useHevc);

        foreach (var (codec, gpuType) in codecs)
        {
            int tryV = vKbps;
            for (int i = 1; i <= 8; i++)
            {
                // GPU エンコーダーは最小ビットレート制約があるため、低すぎる場合は CPU へ即フォールバック
                if (gpuType != GpuEncType.None && tryV < GpuMinKbps) break;
                var (vf, fps) = DecideScaleFps(tryV, info.Width, info.Height);
                string label = GetCodecLabel(codec);
                string passBase = Path.Combine(Path.GetTempPath(),
                    "presso_" + Guid.NewGuid().ToString("N"));

                try
                {
                    bool ok;
                    if (gpuType != GpuEncType.None)
                        ok = await EncodeSinglePassAsync(codec, gpuType, tryV, vf, fps, audioKbps,
                            info.Duration, inputPath, outputPath, i, label, progress, ct);
                    else
                        ok = await Encode2PassAsync(codec, gpuType, tryV, vf, fps, audioKbps,
                            info.Duration, inputPath, outputPath, passBase, i, label, progress, ct);

                    if (ct.IsCancellationRequested) return false;
                    if (!ok) break;

                    long size = new FileInfo(outputPath).Length;
                    if (size <= targetBytes)
                    {
                        progress.Report(new(100,
                            $"完了 ({FormatSize(size)} / 目標 {targetMB:0.#}MB, {label})"));
                        return true;
                    }

                    progress.Report(new(0, $"超過 {FormatSize(size)} > 目標 — 調整中..."));

                    double ratio = (double)targetBytes / size;
                    int next = (int)Math.Floor(tryV * ratio * Safety);
                    next = Math.Min(next, (int)(tryV * 0.95));
                    tryV = Math.Max(next, MinVideoKbps);
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

    private async Task EnsureGpuDetectedAsync(CancellationToken ct)
    {
        if (_gpuDetected) return;
        await _gpuLock.WaitAsync(ct);
        try
        {
            if (_gpuDetected) return;
            await DetectGpuAsync(ct);
            _gpuDetected = true;
        }
        finally { _gpuLock.Release(); }
    }

    private async Task DetectGpuAsync(CancellationToken ct)
    {
        var candidates = new[]
        {
            ("h264_nvenc", "hevc_nvenc", GpuEncType.Nvenc),
            ("h264_qsv",   "hevc_qsv",  GpuEncType.Qsv),
            ("h264_amf",   "hevc_amf",  GpuEncType.Amf),
        };

        foreach (var (h264, hevc, type) in candidates)
        {
            // 合成ソースで1フレームのテストエンコード（入力ファイル不要、~0.5秒）
            // NVENCは最小解像度制限があるため 256x256 を使用
            string testArgs = string.Format(
                "-y -hide_banner -f lavfi -i testsrc=duration=0.04:size=256x256:rate=1 " +
                "-c:v {0} -b:v 200k -frames:v 1 -f null NUL", h264);
            int rc = await FfmpegRunner.RunWithProgressAsync(_ffmpeg, testArgs, 0, _ => { }, ct);
            if (rc != 0) continue;

            _gpuH264 = h264;
            _gpuType = type;

            string hevcTest = string.Format(
                "-y -hide_banner -f lavfi -i testsrc=duration=0.04:size=256x256:rate=1 " +
                "-c:v {0} -b:v 200k -frames:v 1 -f null NUL", hevc);
            int rc2 = await FfmpegRunner.RunWithProgressAsync(_ffmpeg, hevcTest, 0, _ => { }, ct);
            _gpuHevc = (rc2 == 0) ? hevc : null;
            return;
        }
    }

    private (string codec, GpuEncType type)[] BuildCodecList(bool useHevc)
    {
        var list = new List<(string, GpuEncType)>();

        if (_gpuType != GpuEncType.None)
        {
            string? gpu = useHevc ? (_gpuHevc ?? _gpuH264) : _gpuH264;
            if (gpu != null) list.Add((gpu, _gpuType));
        }

        if (useHevc) list.Add(("libx265", GpuEncType.None));
        list.Add(("libx264", GpuEncType.None));
        return [.. list];
    }

    private static string GetCodecLabel(string codec) => codec switch
    {
        "libx265"    => "HEVC",
        "hevc_nvenc" => "HEVC/NVENC",
        "h264_nvenc" => "H.264/NVENC",
        "hevc_qsv"   => "HEVC/QSV",
        "h264_qsv"   => "H.264/QSV",
        "hevc_amf"   => "HEVC/AMF",
        "h264_amf"   => "H.264/AMF",
        _            => "H.264",
    };

    private async Task<bool> Encode2PassAsync(
        string codec, GpuEncType gpuType, int vKbps, string vf, int fps, int audioKbps,
        double duration, string input, string output, string passBase,
        int attempt, string label,
        IProgress<CompressProgress> progress, CancellationToken ct)
    {
        bool isNvenc = gpuType == GpuEncType.Nvenc;
        string preset = isNvenc ? "medium" : "faster";
        string extraArgs = isNvenc ? "-rc vbr " :
                           codec == "libx265" ? "-tag:v hvc1 " : "-tag:v avc1 ";

        // Pass 1
        progress.Report(new(0, $"試行 {attempt} ({label}) {vKbps}kbps — pass 1/2"));
        string p1Args = string.Format(CultureInfo.InvariantCulture,
            "-y -hide_banner -loglevel error -progress pipe:1 -i \"{0}\" " +
            "-map_metadata -1 -an -c:v {1} {2}-b:v {3}k -preset {4} -pix_fmt yuv420p " +
            "-vf {5} -r {6} -pass 1 -passlogfile \"{7}\" -f null NUL",
            input, codec, extraArgs, vKbps, preset, vf, fps, passBase);

        int rc1 = await FfmpegRunner.RunWithProgressAsync(_ffmpeg, p1Args, duration,
            pct => progress.Report(new(pct * 0.5,
                $"試行 {attempt} ({label}) {vKbps}kbps — pass 1/2 {pct:0}%")), ct);
        if (rc1 != 0 || ct.IsCancellationRequested) return false;

        // Pass 2
        progress.Report(new(50, $"試行 {attempt} ({label}) {vKbps}kbps — pass 2/2"));
        string p2Args = string.Format(CultureInfo.InvariantCulture,
            "-y -hide_banner -loglevel error -progress pipe:1 -i \"{0}\" " +
            "-map_metadata -1 -c:v {1} {2}-b:v {3}k -preset {4} -pix_fmt yuv420p " +
            "-vf {5} -r {6} -c:a aac -b:a {7}k -ac 1 " +
            "-pass 2 -passlogfile \"{8}\" -movflags +faststart \"{9}\"",
            input, codec, extraArgs, vKbps, preset, vf, fps, audioKbps, passBase, output);

        int rc2 = await FfmpegRunner.RunWithProgressAsync(_ffmpeg, p2Args, duration,
            pct => progress.Report(new(50 + pct * 0.5,
                $"試行 {attempt} ({label}) {vKbps}kbps — pass 2/2 {pct:0}%")), ct);
        return rc2 == 0;
    }

    private async Task<bool> EncodeSinglePassAsync(
        string codec, GpuEncType gpuType, int vKbps, string vf, int fps, int audioKbps,
        double duration, string input, string output,
        int attempt, string label,
        IProgress<CompressProgress> progress, CancellationToken ct)
    {
        string gpuOpts = gpuType == GpuEncType.Amf   ? "-rc cbr " :
                         gpuType == GpuEncType.Nvenc ? "-rc vbr -preset p4 " : "";
        int maxrate = (int)(vKbps * 1.5);
        int bufsize = vKbps * 2;

        progress.Report(new(0, $"試行 {attempt} ({label}) {vKbps}kbps"));
        string args = string.Format(CultureInfo.InvariantCulture,
            "-y -hide_banner -loglevel error -progress pipe:1 -i \"{0}\" " +
            "-map_metadata -1 -c:v {1} {2}-b:v {3}k -maxrate {4}k -bufsize {5}k " +
            "-pix_fmt yuv420p -vf {6} -r {7} -c:a aac -b:a {8}k -ac 1 " +
            "-movflags +faststart \"{9}\"",
            input, codec, gpuOpts, vKbps, maxrate, bufsize, vf, fps, audioKbps, output);

        int rc = await FfmpegRunner.RunWithProgressAsync(_ffmpeg, args, duration,
            pct => progress.Report(new(pct, $"試行 {attempt} ({label}) {vKbps}kbps {pct:0}%")), ct);
        return rc == 0;
    }

    private async Task<bool> CrfFallbackAsync(
        string input, string output, int audioKbps,
        IProgress<CompressProgress> progress, CancellationToken ct)
    {
        progress.Report(new(0, "CRFモード (サイズ非保証)"));
        string args = string.Format(CultureInfo.InvariantCulture,
            "-y -hide_banner -loglevel error -progress pipe:1 -i \"{0}\" " +
            "-map_metadata -1 -c:v libx264 -preset faster -crf 32 " +
            "-pix_fmt yuv420p -vf scale=720:-2 -r 24 -c:a aac -b:a {1}k -ac 1 " +
            "-movflags +faststart \"{2}\"",
            input, audioKbps, output);
        int rc = await FfmpegRunner.RunWithProgressAsync(_ffmpeg, args, 0, _ => { }, ct);
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
        double targetBits = targetMB * 1024 * 1024 * 8;
        double share = duration > 0 ? targetBits * 0.10 / duration / 1000 : 48;
        return (int)Math.Clamp(Math.Round(share), MinAudioKbps, 96);
    }

    private static int ComputeVideoKbps(double duration, long targetBytes, int audioKbps)
    {
        double targetBits = targetBytes * 8.0 * Safety;
        double audioBits = audioKbps * 1000.0 * Math.Max(duration, 1);
        double vBits = targetBits - audioBits;
        return Math.Max((int)Math.Floor(vBits / Math.Max(duration, 1) / 1000.0), MinVideoKbps);
    }

    private static (string vf, int fps) DecideScaleFps(int vKbps, int inW, int inH)
    {
        int fps = 30;
        int maxW = 1280;
        if (vKbps < 400) maxW = 720;
        if (vKbps < 200) maxW = 540;
        if (vKbps < 120) maxW = 360;
        if (vKbps < 80)  maxW = 240;
        if (vKbps < 40)  maxW = 160;
        if (vKbps < 20)  maxW = 128;
        if (vKbps < 10)  maxW = 96;
        if (vKbps < 150) fps = 24;
        if (vKbps < 100) fps = 20;
        if (vKbps < 60)  fps = 15;
        if (vKbps < 30)  fps = 10;
        if (vKbps < 15)  fps = 5;
        int tW = inW > 0 ? Math.Min(inW, maxW) : maxW;
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
                try { File.Delete(f); } catch { }
        }
        catch { }
    }

    private static string FormatSize(long bytes)
    {
        if (bytes >= 1024 * 1024) return $"{bytes / 1024.0 / 1024.0:0.##}MB";
        if (bytes >= 1024) return $"{bytes / 1024.0:0.##}KB";
        return $"{bytes}B";
    }
}
