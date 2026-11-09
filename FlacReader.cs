using System.Text;

namespace Flac2M4A;

public sealed class FlacMetadata
{
    public string Title = "";
    public string Artist = "";
    public string Album = "";
    public string AlbumArtist = "";
    public string Date = "";
    public string TrackNumber = "";
    public byte[]? CoverData;
    public string CoverMime = "";
}

/// <summary>手写解析 FLAC 元数据块（VORBIS_COMMENT type=4 + PICTURE type=6），无第三方依赖。</summary>
public static class FlacReader
{
    public static FlacMetadata Read(string path)
    {
        using var fs = File.OpenRead(path);
        Span<byte> magic = stackalloc byte[4];
        if (fs.Read(magic) != 4
            || magic[0] != (byte)'f' || magic[1] != (byte)'L'
            || magic[2] != (byte)'a' || magic[3] != (byte)'C')
            throw new InvalidDataException("不是有效的 FLAC 文件");

        var m = new FlacMetadata();
        var lenBuf = new byte[3];
        bool last = false;
        while (!last)
        {
            int h = fs.ReadByte();
            if (h < 0) break;
            last = (h & 0x80) != 0;
            int type = h & 0x7F;
            if (fs.Read(lenBuf, 0, 3) != 3) break;
            int len = (lenBuf[0] << 16) | (lenBuf[1] << 8) | lenBuf[2];
            var block = new byte[len];
            int read = 0;
            while (read < len)
            {
                int r = fs.Read(block, read, len - read);
                if (r <= 0) break;
                read += r;
            }
            if (read != len) break;

            if (type == 4) ParseVorbisComment(block, m);
            else if (type == 6) ParsePicture(block, m);
        }
        return m;
    }

    // VORBIS_COMMENT：小端
    private static void ParseVorbisComment(byte[] b, FlacMetadata m)
    {
        int p = 0;
        if (b.Length < 8) return;
        int vendorLen = Le32(b, p); p += 4;
        if (p + vendorLen > b.Length) return;
        p += vendorLen;
        if (p + 4 > b.Length) return;
        int n = Le32(b, p); p += 4;
        for (int i = 0; i < n; i++)
        {
            if (p + 4 > b.Length) return;
            int len = Le32(b, p); p += 4;
            if (p + len > b.Length) return;
            var s = Encoding.UTF8.GetString(b, p, len); p += len;
            int eq = s.IndexOf('=');
            if (eq <= 0) continue;
            var key = s[..eq].ToUpperInvariant();
            var val = s[(eq + 1)..];
            switch (key)
            {
                case "TITLE": m.Title = val; break;
                case "ARTIST": m.Artist = val; break;
                case "ALBUM": m.Album = val; break;
                case "ALBUMARTIST": m.AlbumArtist = val; break;
                case "DATE":
                case "YEAR": if (string.IsNullOrEmpty(m.Date)) m.Date = val; break;
                case "TRACKNUMBER": m.TrackNumber = val; break;
            }
        }
    }

    // PICTURE：大端；type 3 = front cover
    private static void ParsePicture(byte[] b, FlacMetadata m)
    {
        if (b.Length < 32) return;
        int p = 0;
        int type = Be32(b, p); p += 4;
        if (type != 3) return;

        int mimeLen = Be32(b, p); p += 4;
        if (p + mimeLen > b.Length) return;
        var mime = Encoding.ASCII.GetString(b, p, mimeLen); p += mimeLen;

        int descLen = Be32(b, p); p += 4;
        if (p + descLen > b.Length) return;
        p += descLen;

        p += 16; // width(4) + height(4) + depth(4) + colors(4)
        if (p + 4 > b.Length) return;
        int dataLen = Be32(b, p); p += 4;
        if (p + dataLen > b.Length) return;

        m.CoverData = new byte[dataLen];
        Array.Copy(b, p, m.CoverData, 0, dataLen);
        m.CoverMime = mime;
    }

    private static int Le32(byte[] b, int p) => b[p] | (b[p + 1] << 8) | (b[p + 2] << 16) | (b[p + 3] << 24);
    private static int Be32(byte[] b, int p) => (b[p] << 24) | (b[p + 1] << 16) | (b[p + 2] << 8) | b[p + 3];
}
