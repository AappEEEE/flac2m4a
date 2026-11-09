using System.Text.RegularExpressions;

namespace Flac2M4A;

public static class FilenameParser
{
    /// <summary>「艺人 - 歌名.flac」；无分隔符时返回 ("", 整个文件名)。</summary>
    public static (string Artist, string Title) Parse(string path)
    {
        var base_ = Path.GetFileNameWithoutExtension(path) ?? "";
        var m = Regex.Match(base_, @"^(.+?)\s+-\s+(.+)$");
        if (m.Success)
            return (m.Groups[1].Value.Trim(), m.Groups[2].Value.Trim());
        return ("", base_.Trim());
    }
}
