using Presso.Core;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace Presso;

public partial class MainWindow : Window
{
    public ObservableCollection<JobItem> Items { get; } = new();
    private AppConfig _config;
    private readonly Compressor _compressor;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private CancellationTokenSource? _cts;

    public MainWindow()
    {
        InitializeComponent();
        _config = AppConfig.Load();
        _compressor = new Compressor(AppPaths.FfmpegBinDir);
        QueueList.ItemsSource = Items;
        UpdateHint();
        _ = PrewarmAsync();

        if (!_compressor.ToolsAvailable)
        {
            MessageBox.Show(this,
                $"ffmpeg.exe が見つかりません:\n{AppPaths.FfmpegBinDir}\n\n" +
                "インストールが破損している可能性があります。",
                "Presso", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private async Task PrewarmAsync()
    {
        try { await _compressor.PrewarmAsync(CancellationToken.None); }
        catch (Exception ex) { Title = $"Presso — 検出失敗: {ex.GetType().Name}"; }
        UpdateHint();
    }

    private void UpdateHint()
    {
        string codec = _config.UseHevc ? "HEVC" : "H.264";
        string accel = _compressor.AcceleratorLabel;
        string outDesc = _config.OutputMode == "FixedDir" && !string.IsNullOrWhiteSpace(_config.FixedOutputDir)
            ? $"出力: {_config.FixedOutputDir}"
            : "出力: 同フォルダ";
        HintText.Text = $"目標 {_config.TargetMB:0.#}MB / {codec} / {accel} / {outDesc}";
        Title = $"Presso — {accel}";
    }

    private void Window_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void Window_Drop(object sender, DragEventArgs e)
    {
        e.Handled = true;
        if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;
        var files = (string[])e.Data.GetData(DataFormats.FileDrop);
        var added = new List<JobItem>();
        foreach (var f in files)
        {
            if (!File.Exists(f)) continue;
            if (!AppPaths.IsSupportedVideo(f)) continue;
            var job = new JobItem(f);
            Items.Add(job);
            added.Add(job);
        }
        if (added.Count == 0) return;
        _ = ProcessQueueAsync(added);
    }

    private async Task ProcessQueueAsync(List<JobItem> jobs)
    {
        await _gate.WaitAsync();
        try
        {
            if (_cts == null || _cts.IsCancellationRequested)
            {
                _cts?.Dispose();
                _cts = new CancellationTokenSource();
            }
            CancelButton.IsEnabled = true;
            foreach (var job in jobs)
            {
                if (_cts.IsCancellationRequested) break;
                await RunJobAsync(job, _cts.Token);
            }
        }
        finally
        {
            _gate.Release();
            // Only flip the button off when no further work is queued behind us
            if (_gate.CurrentCount == 1) CancelButton.IsEnabled = false;
        }
    }

    private async Task RunJobAsync(JobItem job, CancellationToken ct)
    {
        try
        {
            string? outDir = null;
            if (_config.OutputMode == "FixedDir" && !string.IsNullOrWhiteSpace(_config.FixedOutputDir))
            {
                outDir = _config.FixedOutputDir;
                Directory.CreateDirectory(outDir);
            }
            string outPath = AppPaths.BuildOutputPath(job.InputPath, outDir);

            var progress = new Progress<CompressProgress>(p =>
            {
                job.Progress = p.Percent;
                job.Status = p.Status;
            });

            bool ok = await _compressor.CompressAsync(
                job.InputPath, outPath, _config.TargetMB, _config.UseHevc, progress, ct);

            if (ct.IsCancellationRequested)
            {
                job.Status = "キャンセル";
                job.Progress = 0;
            }
            else if (ok)
            {
                job.Progress = 100;
            }
            else
            {
                job.Status = "失敗";
            }
        }
        catch (Exception ex)
        {
            job.Status = $"エラー: {ex.Message}";
        }
    }

    private void Settings_Click(object sender, RoutedEventArgs e)
    {
        var w = new SettingsWindow(_config) { Owner = this };
        if (w.ShowDialog() == true)
        {
            _config = AppConfig.Load();
            UpdateHint();
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        _cts?.Cancel();
        CancelButton.IsEnabled = false;
    }

    private void Clear_Click(object sender, RoutedEventArgs e)
    {
        for (int i = Items.Count - 1; i >= 0; i--)
        {
            if (Items[i].Progress >= 100 || Items[i].Status.StartsWith("失敗") || Items[i].Status.StartsWith("エラー") || Items[i].Status == "キャンセル")
                Items.RemoveAt(i);
        }
    }

    private void OpenOutput_Click(object sender, RoutedEventArgs e)
    {
        string dir;
        if (_config.OutputMode == "FixedDir" && !string.IsNullOrWhiteSpace(_config.FixedOutputDir))
            dir = _config.FixedOutputDir;
        else if (Items.Count > 0)
            dir = Path.GetDirectoryName(Items[^1].InputPath) ?? AppPaths.AppDir;
        else
            dir = AppPaths.AppDir;

        if (!Directory.Exists(dir))
        {
            MessageBox.Show(this, "フォルダがありません: " + dir, "Presso");
            return;
        }
        Process.Start(new ProcessStartInfo("explorer.exe", $"\"{dir}\"") { UseShellExecute = true });
    }

    protected override void OnClosed(EventArgs e)
    {
        _cts?.Cancel();
        base.OnClosed(e);
    }
}
