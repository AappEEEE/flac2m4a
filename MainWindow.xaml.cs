using System.Collections.ObjectModel;
using System.Windows;
using Microsoft.Win32;

namespace Flac2M4A;

public partial class MainWindow : Window
{
    public sealed class FileRow
    {
        public string File { get; set; } = "";
        public string Status { get; set; } = "等待";
        public string Info { get; set; } = "";
    }

    private readonly ObservableCollection<FileRow> _rows = new();
    private string _outputDir = "";

    public MainWindow()
    {
        InitializeComponent();
        FileList.ItemsSource = _rows;
    }

    private void PickButton_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Title = "选择 FLAC 文件",
            Filter = "FLAC 文件 (*.flac)|*.flac|所有文件 (*.*)|*.*",
            Multiselect = true,
        };
        if (dlg.ShowDialog(this) != true) return;
        foreach (var f in dlg.FileNames)
        {
            if (!_rows.Any(r => r.File == f))
                _rows.Add(new FileRow { File = f, Status = "等待", Info = "" });
        }
        StartButton.IsEnabled = _rows.Count > 0;
        StatusText.Text = $"已选 {_rows.Count} 个文件";
    }

    private void OutButton_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFolderDialog { Title = "选择输出文件夹" };
        if (dlg.ShowDialog(this) == true)
        {
            _outputDir = dlg.FolderName;
            OutputDirText.Text = "输出到：" + _outputDir;
        }
    }

    private void ClearButton_Click(object sender, RoutedEventArgs e)
    {
        _rows.Clear();
        StartButton.IsEnabled = false;
        StatusText.Text = "就绪";
    }

    private async void StartButton_Click(object sender, RoutedEventArgs e)
    {
        if (_rows.Count == 0) return;
        PickButton.IsEnabled = OutButton.IsEnabled = ClearButton.IsEnabled = StartButton.IsEnabled = false;
        try
        {
            foreach (var row in _rows)
            {
                if (row.Status == "完成") continue;
                row.Status = "处理中…";
                row.Info = "";
                StatusText.Text = "处理：" + Path.GetFileName(row.File);

                var outDir = string.IsNullOrWhiteSpace(_outputDir)
                    ? (Path.GetDirectoryName(row.File) ?? "")
                    : _outputDir;

                var r = await Converter.Convert(row.File, outDir);
                row.Status = r.Ok ? "完成" : "拒绝/失败";
                row.Info = r.Ok ? $"{r.Artist} · {r.Album} · {r.Date}" : r.Message;
            }
            StatusText.Text = "全部处理完成";
        }
        catch (Exception ex)
        {
            StatusText.Text = "出错：" + ex.Message;
        }
        finally
        {
            PickButton.IsEnabled = OutButton.IsEnabled = ClearButton.IsEnabled = true;
            StartButton.IsEnabled = _rows.Any(r => r.Status != "完成");
        }
    }
}
