using System.Text;

namespace Flac2M4A;

/// <summary>往 m4a 的 moov 末尾注入 udta/ilst 标签（复刻 inject_tags.py 的字节布局）。
/// moov 必须位于文件末尾（ffmpeg 默认如此）；先移除 ffmpeg 自带的空 udta，再追加新的。</summary>
public static class M4aTagger
{
    private static readonly byte[] Nam = { 0xA9, (byte)'n', (byte)'a', (byte)'m' };
    private static readonly byte[] Art = { 0xA9, (byte)'A', (byte)'R', (byte)'T' };
    private static readonly byte[] Alb = { 0xA9, (byte)'a', (byte)'l', (byte)'b' };
    private static readonly byte[] Day = { 0xA9, (byte)'d', (byte)'a', (byte)'y' };

    public static void Inject(string m4aPath, string title, string artist, string album, string date,
                              string? trackNumber, byte[]? cover, bool coverIsPng)
    {
        var data = File.ReadAllBytes(m4aPath);
        var (moovOff, moovSize) = FindLastTopLevelBox(data, "moov");

        // 移除 ffmpeg 自带的空 udta（若存在）
        var (udtaOff, udtaSize) = FindChild(data, moovOff, moovSize, "udta");
        if (udtaOff >= 0)
        {
            var tmp = new byte[data.Length - udtaSize];
            Buffer.BlockCopy(data, 0, tmp, 0, udtaOff);
            Buffer.BlockCopy(data, udtaOff + udtaSize, tmp, udtaOff, data.Length - udtaOff - udtaSize);
            data = tmp;
            moovSize -= udtaSize;
        }

        var udta = BuildUdta(title, artist, album, date, trackNumber, cover, coverIsPng);
        var result = new byte[data.Length + udta.Length];
        Buffer.BlockCopy(data, 0, result, 0, data.Length);
        Buffer.BlockCopy(udta, 0, result, data.Length, udta.Length);
        WriteBe32(result, moovOff, moovSize + udta.Length);
        File.WriteAllBytes(m4aPath, result);
    }

    private static (int Off, int Size) FindLastTopLevelBox(byte[] data, string type)
    {
        int off = 0, lastOff = -1, lastSize = 0;
        while (off + 8 <= data.Length)
        {
            long size = ReadBe32(data, off);
            if (size == 1)
            {
                if (off + 16 > data.Length) break;
                size = ReadBe64(data, off + 8);
            }
            if (size < 8 || off + size > data.Length) break;
            lastOff = off;
            lastSize = (int)size;
            off += (int)size;
        }
        if (lastOff < 0 || Encoding.ASCII.GetString(data, lastOff + 4, 4) != type)
            throw new InvalidDataException($"{type} 不是最后一个 box，无法注入标签");
        return (lastOff, lastSize);
    }

    private static (int Off, int Size) FindChild(byte[] data, int parentOff, int parentSize, string type)
    {
        int off = parentOff + 8;
        int end = parentOff + parentSize;
        while (off + 8 <= end)
        {
            long size = ReadBe32(data, off);
            if (size == 1 && off + 16 <= end) size = ReadBe64(data, off + 8);
            if (size < 8 || off + size > end) break;
            if (Encoding.ASCII.GetString(data, off + 4, 4) == type) return (off, (int)size);
            off += (int)size;
        }
        return (-1, 0);
    }

    private static byte[] BuildUdta(string title, string artist, string album, string date,
                                    string? trackNumber, byte[]? cover, bool coverIsPng)
    {
        var items = new List<byte[]>
        {
            Box(Nam, DataText(title)),
            Box(Art, DataText(artist)),
            Box("aART", DataText(artist)),
            Box(Alb, DataText(album)),
            Box(Day, DataText(date)),
        };
        if (TryParseTrack(trackNumber, out int num, out int total))
            items.Add(Box("trkn", DataTrkn(num, total)));
        if (cover is { Length: > 0 })
            items.Add(Box("covr", DataCovr(cover, coverIsPng)));

        var ilst = Box("ilst", Concat(items));

        // hdlr payload 25 字节：version/flags(4) + pre_defined(4) + "mdir"(4) + reserved(12) + null(1)
        var hdlrPayload = new byte[25];
        Encoding.ASCII.GetBytes("mdir").CopyTo(hdlrPayload, 8);
        var hdlr = Box("hdlr", hdlrPayload);

        // meta 是 fullbox：4 字节 version/flags 后才是 hdlr + ilst
        var metaPayload = new byte[4 + hdlr.Length + ilst.Length];
        hdlr.CopyTo(metaPayload, 4);
        ilst.CopyTo(metaPayload, 4 + hdlr.Length);
        var meta = Box("meta", metaPayload);

        return Box("udta", meta);
    }

    private static bool TryParseTrack(string? s, out int num, out int total)
    {
        num = 0; total = 0;
        if (string.IsNullOrWhiteSpace(s)) return false;
        var parts = s.Trim().Split('/');
        if (!int.TryParse(parts[0].Trim(), out num) || num <= 0) return false;
        total = parts.Length > 1 && int.TryParse(parts[1].Trim(), out var t) ? t : 0;
        return true;
    }

    // ---------- box / data 构造 ----------

    private static byte[] Box(string type, byte[] payload) => Box(Encoding.ASCII.GetBytes(type), payload);

    private static byte[] Box(byte[] type, byte[] payload)
    {
        var d = new byte[8 + payload.Length];
        WriteBe32(d, 0, d.Length);
        Buffer.BlockCopy(type, 0, d, 4, 4);
        Buffer.BlockCopy(payload, 0, d, 8, payload.Length);
        return d;
    }

    private static byte[] DataText(string s)
    {
        var b = Encoding.UTF8.GetBytes(s);
        var d = new byte[16 + b.Length];
        WriteBe32(d, 0, d.Length);
        Encoding.ASCII.GetBytes("data").CopyTo(d, 4);
        WriteBe32(d, 8, 1);   // typeIndicator = UTF-8 文本
        WriteBe32(d, 12, 0);  // locale
        b.CopyTo(d, 16);
        return d;
    }

    private static byte[] DataTrkn(int track, int total)
    {
        var d = new byte[24];
        WriteBe32(d, 0, 24);
        Encoding.ASCII.GetBytes("data").CopyTo(d, 4);
        WriteBe32(d, 8, 0);   // typeIndicator = 二进制
        WriteBe32(d, 12, 0);
        WriteBe16(d, 16, 0);
        WriteBe16(d, 18, track);
        WriteBe16(d, 20, total);
        WriteBe16(d, 22, 0);
        return d;
    }

    private static byte[] DataCovr(byte[] img, bool isPng)
    {
        var d = new byte[16 + img.Length];
        WriteBe32(d, 0, d.Length);
        Encoding.ASCII.GetBytes("data").CopyTo(d, 4);
        WriteBe32(d, 8, isPng ? 14 : 13);  // 14 = PNG, 13 = JPEG
        WriteBe32(d, 12, 0);
        img.CopyTo(d, 16);
        return d;
    }

    private static byte[] Concat(IEnumerable<byte[]> parts)
    {
        var list = parts.ToList();
        var buf = new byte[list.Sum(p => p.Length)];
        int off = 0;
        foreach (var p in list) { p.CopyTo(buf, off); off += p.Length; }
        return buf;
    }

    // ---------- 大端读写 ----------

    private static long ReadBe32(byte[] b, int off) =>
        ((long)b[off] << 24) | ((long)b[off + 1] << 16) | ((long)b[off + 2] << 8) | b[off + 3];

    private static long ReadBe64(byte[] b, int off) => (ReadBe32(b, off) << 32) | ReadBe32(b, off + 4);

    private static void WriteBe32(byte[] b, int off, int v)
    {
        b[off] = (byte)(v >> 24);
        b[off + 1] = (byte)(v >> 16);
        b[off + 2] = (byte)(v >> 8);
        b[off + 3] = (byte)v;
    }

    private static void WriteBe16(byte[] b, int off, int v)
    {
        b[off] = (byte)(v >> 8);
        b[off + 1] = (byte)v;
    }
}
