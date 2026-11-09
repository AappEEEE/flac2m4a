# Flac2M4A

把 FLAC 无损转成带完整元数据的 ALAC（`.m4a`）的 Windows 桌面程序。按文件名或 FLAC 内嵌标签识别歌曲，自动查 QQ 音乐补齐专辑 / 发行日期 / 封面，最后导出内嵌标签与封面的 `.m4a`。

只做「FLAC → m4a 导出」这一步，不碰 Apple Music 导入（导入仍交给桌面端 `flac2am.py`）。

## 功能

- 多选 FLAC，逐个处理，界面实时显示「待处理 / 处理中 / 完成 / 失败」。
- 从 FLAC 内嵌 `VORBIS_COMMENT` 读 `TITLE / ARTIST / ALBUM / DATE / TRACKNUMBER`，从 `PICTURE` 读封面。
- 缺元数据时按文件名 `艺人 - 歌名.flac` 判断，查 QQ 音乐补全。
- 用 ffmpeg 转 ALAC（无损），再手工写入 iTunes 兼容的 `ilst` 标签（title / artist / album artist / album / date / track / cover）。
- 每个文件成功后可直接在列表里「导出」到任意目录。

## 元数据规则

1. **文件名解析**：`艺人 - 歌名.flac`（分隔符为「空格-空格」）。没有分隔符时整串当作歌名，艺人留空。
2. **歌手信息**：优先取 FLAC 的 `ARTIST` 标签；缺标签时取文件名的「艺人」部分。
3. **拒绝（refuse）条件**：
   - 歌名为空 → refuse；
   - 缺专辑 / 日期 / 封面，且歌手是 `未知歌手 / unknown / unknown artist / 空` → refuse「缺歌手信息，无法查 QQ 音乐（文件名请用『艺人 - 歌名.flac』）」；
   - QQ 音乐查不到 `艺人 - 歌名` → refuse「QQ 音乐没找到 …」。
4. **选专辑**：歌名精确匹配 + 歌手匹配，取发行日期最早的那张原版专辑。
5. **封面**：优先 FLAC 内嵌封面；否则取 QQ 音乐 `1000x1000`（失败退 `500x500`）。

## 运行

### 直接使用（预编译）

1. 从 [Releases](../../releases) 下载 `Flac2M4A-win-x64.zip`。
2. 解压，把 `ffmpeg.exe` 放到 `Flac2M4A.exe` 同一目录（或保证 `ffmpeg` 在 `PATH` 中）。
3. 双击 `Flac2M4A.exe`。

> ffmpeg 需自行下载（如 [gyan.dev](https://www.gyan.dev/ffmpeg/builds/) 或 [BtbN](https://github.com/BtbN/FFmpeg-Builds/releases) 的 Windows 构建），取 `bin/ffmpeg.exe` 即可。仓库不包含 ffmpeg 二进制。

### 从源码构建

需要 [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)：

```powershell
dotnet build Flac2M4A.csproj -c Release

# 出 self-contained 文件夹（免装 .NET 运行时）
dotnet publish Flac2M4A.csproj -c Release -r win-x64 --self-contained true -o publish

# 或出单文件 EXE（首次运行会解包到 %TEMP%\.net）
dotnet publish Flac2M4A.csproj -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true -o publish
```

## 技术要点

- **UI**：WPF（.NET 8 原生 XAML），无需 Visual Studio / Windows SDK 也能 `dotnet build`。
- **转码**：`ffmpeg -y -hide_banner -loglevel error -i <in.flac> -vn -map_metadata -1 -c:a alac <out.m4a>`（`-map_metadata -1` 去掉 ffmpeg 自带的空标签）。
- **打标签**：手工向 `moov` 末尾追加 `udta → meta → ilst`，并 patch `moov` 的 size 字段（ffmpeg 产物的 `moov` 在文件末尾，`mdat` 的 `stco` 偏移不受影响）。`ilst` 的 `data` 原子含 `typeIndicator`（1 文本 / 0 trkn / 13 JPEG / 14 PNG）与 `locale(0)`；`©` 开头的 fourcc 是单字节 `0xA9`。
- **QQ 音乐接口**：搜索 `c.y.qq.com/soso/fcgi-bin/client_search_cp`，专辑详情 `u.y.qq.com/cgi-bin/musicu.fcg`（`music.musichallAlbum.AlbumInfoServer/GetAlbumDetail`），封面 `y.qq.com/music/photo_new/T002R1000x1000M000<album_mid>.jpg`。

## 已知限制

- 歌词不写入（与桌面端 `flac2am.py` 一致）。
- 单文件 EXE 首次运行会把原生库解包到 `%TEMP%\.net`，某些受限环境（如禁止写临时目录的沙箱）会失败；推荐用文件夹版。

## 许可

[MIT](LICENSE)
