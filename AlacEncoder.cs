using System.Diagnostics;

namespace Flac2M4A;

/// <summary>调用 ffmpeg 把 FLAC 转成 ALAC(.m4a)。-map_metadata -1 让产物不含标签，后续由 M4aTagger 注入。</summary>
public static class AlacEncoder
{
    public static string? LocateFfmpeg()
    {
        var local = Path.Combine(AppContext.BaseDirectory, "ffmpeg.exe");
        if (File.Exists(local)) return local;

        var path = Environment.GetEnvironmentVariable("PATH") ?? "";
        foreach (var dir in path.Split(';'))
        {
            if (string.IsNullOrWhiteSpace(dir)) continue;
            var p = Path.Combine(dir.Trim(), "ffmpeg.exe");
            if (File.Exists(p)) return p;
        }
        return null;
    }

    public static async Task Transcode(string flacPath, string outputM4a)
    {
        var ffmpeg = LocateFfmpeg()
            ?? throw new Exception("找不到 ffmpeg.exe（请与程序放在同一目录，或加入 PATH）");

        var psi = new ProcessStartInfo(ffmpeg)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardError = true,
        };
        foreach (var a in new[]
        {
            "-y", "-hide_banner", "-loglevel", "error",
            "-i", flacPath, "-vn", "-map_metadata", "-1", "-c:a", "alac", outputM4a,
        })
        {
            psi.ArgumentList.Add(a);
        }

        using var p = Process.Start(psi) ?? throw new Exception("无法启动 ffmpeg");
        var errTask = p.StandardError.ReadToEndAsync();
        await p.WaitForExitAsync();
        var err = await errTask;

        if (p.ExitCode != 0 || !File.Exists(outputM4a))
            throw new Exception("转码失败：" + (err.Length > 200 ? err[..200] : err));
    }
}
