using Microsoft.Win32;
using Presso.Core;
using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace Presso;

public partial class SettingsWindow : Window
{
    private readonly AppConfig _config;

    public SettingsWindow(AppConfig config)
    {
        InitializeComponent();
        _config = config;

        TargetSizeSlider.Value = Math.Clamp(config.TargetMB, 1, 500);
        UpdateLabel(TargetSizeSlider.Value);
        UseHevcCheck.IsChecked = config.UseHevc;
        if (config.OutputMode == "FixedDir") OutFixedDir.IsChecked = true;
        else OutSourceDir.IsChecked = true;
        FixedDirBox.Text = config.FixedOutputDir;
    }

    private void UpdateLabel(double v) =>
        TargetSizeLabel.Text = v.ToString("0.#", CultureInfo.InvariantCulture) + " MB";

    private void TargetSizeSlider_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (TargetSizeLabel != null) UpdateLabel(e.NewValue);
    }

    private void Preset_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button b && b.Tag is string s &&
            double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var v))
        {
            TargetSizeSlider.Value = v;
        }
    }

    private void Browse_Click(object sender, RoutedEventArgs e)
    {
        // WPF has OpenFolderDialog from .NET 8
        var dlg = new OpenFolderDialog
        {
            Title = "Select Output Folder",
            InitialDirectory = Directory.Exists(FixedDirBox.Text)
                ? FixedDirBox.Text
                : Environment.GetFolderPath(Environment.SpecialFolder.MyVideos),
        };
        if (dlg.ShowDialog(this) == true)
        {
            FixedDirBox.Text = dlg.FolderName;
        }
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        _config.TargetMB = Math.Round(TargetSizeSlider.Value, 1);
        _config.UseHevc = UseHevcCheck.IsChecked == true;
        _config.OutputMode = OutFixedDir.IsChecked == true ? "FixedDir" : "SourceDir";
        _config.FixedOutputDir = FixedDirBox.Text?.Trim() ?? "";

        if (_config.OutputMode == "FixedDir" && string.IsNullOrWhiteSpace(_config.FixedOutputDir))
        {
            MessageBox.Show(this, "Please specify an output folder.", "Presso",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            _config.Save();
            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Failed to save settings:\n{ex.Message}", "Presso",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void Licenses_Click(object sender, RoutedEventArgs e)
    {
        var dir = Path.Combine(AppPaths.AppDir, "licenses");
        if (!Directory.Exists(dir))
        {
            MessageBox.Show(this,
                "Licenses folder not found:\n" + dir,
                "Presso", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        Process.Start(new ProcessStartInfo("explorer.exe", $"\"{dir}\"") { UseShellExecute = true });
    }
}
