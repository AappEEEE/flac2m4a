namespace Flac2M4A;

public sealed record ConvertResult(bool Ok, string Message, string? OutputPath = null,
    string Title = "", string Artist = "", string Album = "", string Date = "");

/// <summary>编排：解析文件名 → 读 FLAC 标签 → refuse 判断 → QQ 补齐 → ffmpeg 转码 → 注入标签。</summary>
public static class Converter
{
    private static readonly HashSet<string> UnknownArtists =
        new(StringComparer.OrdinalIgnoreCase) { "未知歌手", "unknown", "unknown artist", "" };

    public static async Task<ConvertResult> Convert(string flacPath, string outputDir)
    {
        var (fArtist, fTitle) = FilenameParser.Parse(flacPath);

        FlacMetadata meta;
        try { meta = FlacReader.Read(flacPath); }
        catch (Exception e) { return new(false, "读取 FLAC 失败：" + e.Message); }

        var title = (string.IsNullOrWhiteSpace(meta.Title) ? fTitle : meta.Title.Trim()).Trim();
        var artist = (string.IsNullOrWhiteSpace(meta.Artist) ? fArtist : meta.Artist.Trim()).Trim();
        var album = meta.Album.Trim();
        var date = meta.Date.Trim();
        byte[]? cover = meta.CoverData;
        bool coverIsPng = meta.CoverMime.Contains("png", StringComparison.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(title))
            return new(false, "判断不出歌名，请用「艺人 - 歌名.flac」命名或写入 TITLE 标签");

        if (string.IsNullOrEmpty(album) || string.IsNullOrEmpty(date) || cover == null)
        {
            if (UnknownArtists.Contains(artist))
                return new(false, "缺歌手信息，无法查 QQ 音乐（文件名请用「艺人 - 歌名.flac」）");

            var release = await QqMusic.FindRelease(artist, title);
            if (release == null)
                return new(false, $"QQ 音乐没找到 {artist} - {title}");

            if (string.IsNullOrEmpty(album)) album = release.Album;
            if (string.IsNullOrEmpty(date)) date = release.Date;
            if (cover == null)
                (cover, coverIsPng) = await QqMusic.FetchCover(release.AlbumMid);
        }

        Directory.CreateDirectory(outputDir);
        var base_ = Path.GetFileNameWithoutExtension(flacPath);
        var outM4a = Path.Combine(outputDir, base_ + ".m4a");

        try { await AlacEncoder.Transcode(flacPath, outM4a); }
        catch (Exception e) { return new(false, e.Message); }

        try { M4aTagger.Inject(outM4a, title, artist, album, date, meta.TrackNumber, cover, coverIsPng); }
        catch (Exception e) { return new(false, "打标签失败：" + e.Message); }

        return new(true, "完成", outM4a, title, artist, album, date);
    }
}
