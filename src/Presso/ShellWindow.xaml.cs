using Presso.Core;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace Presso;

public partial class ShellWindow : Window
{
    private readonly string _input;
    private readonly CancellationTokenSource _cts = new();
    private bool _finished;

    public ShellWindow(string inputPath)
    {
        InitializeComponent();
        _input = inputPath;
        FileNameText.Text = Path.GetFileName(inputPath);
        StatusText.Text = "準備中...";
        Loaded += async (_, _) => await RunAsync();
    }

    private async Task RunAsync()
    {
        var config = AppConfig.Load();
        var compressor = new Compressor(AppPaths.FfmpegBinDir);

        if (!compressor.ToolsAvailable)
        {
            StatusText.Text = "ffmpeg.exe が見つかりません";
            ActionButton.Content = "閉じる";
            _finished = true;
            return;
        }
        if (!File.Exists(_input))
        {
            StatusText.Text = "入力ファイルが見つかりません";
            ActionButton.Content = "閉じる";
            _finished = true;
            return;
        }

        // Right-click invocation always outputs next to the source.
        string outPath = AppPaths.BuildOutputPath(_input, null);

        var progress = new Progress<CompressProgress>(p =>
        {
            ProgressBarCtl.Value = p.Percent;
            StatusText.Text = p.Status;
        });

        try
        {
            bool ok = await compressor.CompressAsync(
                _input, outPath, config.TargetMB, config.UseHevc, progress, _cts.Token);

            _finished = true;
            ActionButton.Content = "閉じる";
            if (_cts.IsCancellationRequested)
            {
                StatusText.Text = "キャンセルされました";
            }
            else if (ok)
            {
                ProgressBarCtl.Value = 100;
                AutoCloseAfter(TimeSpan.FromSeconds(3));
            }
            else
            {
                StatusText.Text = "失敗しました";
            }
        }
        catch (Exception ex)
        {
            _finished = true;
            ActionButton.Content = "閉じる";
            StatusText.Text = "エラー: " + ex.Message;
        }
    }

    private void AutoCloseAfter(TimeSpan delay)
    {
        var t = new DispatcherTimer { Interval = delay };
        t.Tick += (_, _) => { t.Stop(); Close(); };
        t.Start();
    }

    private void Action_Click(object sender, RoutedEventArgs e)
    {
        if (_finished) Close();
        else
        {
            _cts.Cancel();
            ActionButton.IsEnabled = false;
            ActionButton.Content = "中止中...";
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        if (!_cts.IsCancellationRequested) _cts.Cancel();
        base.OnClosed(e);
    }
}
