using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;

namespace Presso;

public sealed class JobItem : INotifyPropertyChanged
{
    private double _progress;
    private string _status = "待機中";

    public string InputPath { get; }
    public string FileName => Path.GetFileName(InputPath);

    public double Progress
    {
        get => _progress;
        set { if (_progress != value) { _progress = value; OnChanged(); } }
    }

    public string Status
    {
        get => _status;
        set { if (_status != value) { _status = value; OnChanged(); } }
    }

    public JobItem(string path) { InputPath = path; }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnChanged([CallerMemberName] string? n = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}
