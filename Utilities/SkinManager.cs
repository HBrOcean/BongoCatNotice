using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using SkiaSharp;

namespace BongoCatNotice.Utilities
{
    public class SkinManager
    {
        private static SkinManager instance = null!;
        private static readonly object obj = new();

        /// <summary>
        /// 外部皮肤目录：%AppData%/BongoCatNotice/skins/
        /// 放进去的皮肤无需重新编译即可出现在列表里。
        /// </summary>
        private static readonly string ExternalSkinRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            GlobalHelper.Name,
            "skins");

        private ItemsJson items;
        private HatOffsetJson[] offset;
        private string _skinId = "0";
        private string _skinMode = "layer";
        private string _hatId = "0";
        private List<Bitmap> _skinImage = [];
        private Bitmap? _hatImage;
        private Position _hatOffset = new() { X = 30, Y = -70 };

        private SkinManager()
        {
            Stream itemsJson = AssetLoader.Open(new Uri($"avares://{GlobalHelper.ProjectName}/Assets/items.json"));
            items = JsonSerializer.Deserialize(itemsJson, ItemsJsonContext.Default.ItemsJson)!;

            Stream offsetJson = AssetLoader.Open(new Uri($"avares://{GlobalHelper.ProjectName}/Assets/offset.json"));
            offset = JsonSerializer.Deserialize(offsetJson, HatOffsetJsonContext.Default.HatOffsetJsonArray)!;

            DefaultImages();
            LoadExternalSkins();
            RebuildImageCache();
        }

        public static SkinManager Instance
        {
            get
            {
                lock (obj)
                {
                    instance ??= new SkinManager();
                    return instance;
                }
            }
        }

        public ItemsJson Items { get => items; }

        public string SkinId
        {
            get => _skinId;
            set
            {
                _skinId = ValidateItem(value, Items.Skin);

                // 释放上一套皮肤的位图:反复换肤(含随机换肤)时不释放会导致非托管内存持续增长
                foreach (Bitmap old in _skinImage)
                    old.Dispose();
                _skinImage.Clear();

                if (_skinId == "0")
                {
                    _skinMode = "layer";
                    DefaultImages();
                }
                else
                {
                    SkinItem item = Items.Skin[_skinId.ToString()];
                    _skinMode = string.IsNullOrWhiteSpace(item.Mode) ? "layer" : item.Mode!;
                    foreach (string img in item.Image)
                        _skinImage.Add(OpenSkinBitmap(item, img));
                }

                RebuildImageCache();
            }
        }

        public string HatId
        {
            get => _hatId;
            set
            {
                _hatId = ValidateItem(value, Items.Hat);

                Bitmap? oldHat = _hatImage;
                if (_hatId == "0")
                {
                    _hatImage = null;
                    _hatOffset = new() { X = 30, Y = -70 };
                }
                else
                {
                    HatItem hat = Items.Hat[_hatId.ToString()];
                    _hatImage = new Bitmap(AssetLoader.Open(new Uri($"avares://{GlobalHelper.ProjectName}/Assets/hat/{hat.Image}")));
                    _hatOffset = GetHatOffsetByName(hat.Name);
                }

                // 旧帽子位图在被替换后再释放,避免与仍在显示的图像冲突
                oldHat?.Dispose();
            }
        }

        public Bitmap[] SkinImage
        {
            // 每次敲击都会读取该属性,直接返回缓存数组,避免高频敲击时反复分配
            get => _skinImageCache;
        }

        private Bitmap[] _skinImageCache = [];

        private void RebuildImageCache()
        {
            _skinImageCache = [.. _skinImage];
        }

        public string SkinMode
        {
            get => _skinMode;
        }

        public Bitmap? HatImage
        {
            get => _hatImage;
        }

        public Position HatOffset
        {
            get => _hatOffset;
        }

        private Position GetHatOffsetByName(string name)
        {
            HatOffsetJson hat = offset.FirstOrDefault(hat => hat.Name == name)!;
            return hat != null ? hat.Position : new Position { X = 30, Y = -70 };
        }

        /// <summary>
        /// 扫描外部皮肤目录并合并进列表。
        /// 目录结构：skins/&lt;皮肤目录&gt;/skin.json + 四帧 PNG + 图标。
        /// 任何单个皮肤出错都不影响其它皮肤和程序启动。
        /// </summary>
        private void LoadExternalSkins()
        {
            try
            {
                if (!Directory.Exists(ExternalSkinRoot))
                {
                    Directory.CreateDirectory(ExternalSkinRoot);
                    return;
                }

                foreach (string dir in Directory.GetDirectories(ExternalSkinRoot))
                {
                    string manifest = Path.Combine(dir, "skin.json");
                    if (!File.Exists(manifest))
                        continue;

                    try
                    {
                        SkinItem? item = JsonSerializer.Deserialize(
                            File.ReadAllText(manifest),
                            SkinJsonContext.Default.SkinItem);

                        if (item is null || item.Image is null || item.Image.Length != 4)
                            continue;

                        if (item.Image.Any(file => !File.Exists(Path.Combine(dir, file))))
                            continue;

                        item.ExternalDir = dir;
                        items.Skin["ext_" + Path.GetFileName(dir)] = item;
                    }
                    catch
                    {
                        // 单个 skin.json 写坏就跳过这一套
                    }
                }
            }
            catch
            {
                // 目录不可读时忽略，程序照常以内嵌皮肤运行
            }
        }

        /// <summary>外部皮肤读文件，内嵌皮肤读 avares 资源</summary>
        private static Bitmap OpenSkinBitmap(SkinItem item, string fileName)
        {
            if (!string.IsNullOrEmpty(item.ExternalDir))
            {
                using FileStream stream = File.OpenRead(Path.Combine(item.ExternalDir, fileName));
                return new Bitmap(stream);
            }

            return new Bitmap(AssetLoader.Open(new Uri($"avares://{GlobalHelper.ProjectName}/Assets/skin/{fileName}")));
        }

        public static IImmutableSolidColorBrush GetQuality(string tags)
        {
            if (tags.Contains("quality:unique"))
                return Brushes.Fuchsia;
            else if(tags.Contains("quality:legendary"))
                return Brushes.Orange;
            else if (tags.Contains("quality:epic"))
                return Brushes.DarkMagenta;
            else if (tags.Contains("quality:rare"))
                return Brushes.DeepSkyBlue;
            else if (tags.Contains("quality:uncommon"))
                return Brushes.Lime;
            else if (tags.Contains("quality:common"))
                return Brushes.Silver;

            return Brushes.Transparent;
        }

        private static string ValidateItem<T>(string value, IReadOnlyDictionary<string, T>? itemDict)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "0";
            return itemDict?.ContainsKey(value) == true ? value : "0";
        }

        private void DefaultImages()
        {
            _skinImage.Add(new Bitmap(AssetLoader.Open(new Uri($"avares://{GlobalHelper.ProjectName}/Assets/default/CatLeft.png"))));
            _skinImage.Add(new Bitmap(AssetLoader.Open(new Uri($"avares://{GlobalHelper.ProjectName}/Assets/default/CatLeftPunch.png"))));
            _skinImage.Add(new Bitmap(AssetLoader.Open(new Uri($"avares://{GlobalHelper.ProjectName}/Assets/default/CatRight.png"))));
            _skinImage.Add(new Bitmap(AssetLoader.Open(new Uri($"avares://{GlobalHelper.ProjectName}/Assets/default/CatRightPunch.png"))));
        }

        public string[] GetSkinIdList()
        {
            List<string> list = [];
            foreach (KeyValuePair<string, SkinItem> skinItem in items.Skin)
                list.Add(skinItem.Key);
            return [.. list];
        }

        public string[] GetHatIdList()
        {
            List<string> list = [];
            foreach (KeyValuePair<string, HatItem> hatItem in items.Hat)
                list.Add(hatItem.Key);
            return [.. list];
        }

        private string? _areaCacheKey;
        private RectArea _areaCache = new(0, 0, 0, 0);

        public RectArea GetImageArea()
        {
            // 同一套皮肤 + 帽子算出来的可见区域是固定的，换装后才会变。
            // 缓存一份，避免每次 SetWindowSize 都重新合成四帧再扫一遍。
            string key = SkinId + "|" + HatId;
            if (_areaCacheKey == key)
                return _areaCache;

            RectArea area = ComputeImageArea();
            _areaCacheKey = key;
            _areaCache = area;
            return area;
        }

        private RectArea ComputeImageArea()
        {
            using SKBitmap baseBitmap = ToSKBitmap(_skinImage[0]);
            using SKBitmap twoBitmap = ToSKBitmap(_skinImage[1]);
            using SKBitmap threeBitmap = ToSKBitmap(_skinImage[2]);
            using SKBitmap fourBitmap = ToSKBitmap(_skinImage[3]);

            RectArea rectArea = new(0, 0, 0, 0);
            Position hatOffset = GlobalHelper.CatSkin.HatOffset;

            using SKBitmap hatBitmap = _hatImage != null ? ToSKBitmap(_hatImage) : null!;

            if (hatBitmap != null)
            {
                rectArea.X = Math.Min(0, hatOffset.X);
                rectArea.Y = Math.Min(0, hatOffset.Y);
                rectArea.Right = Math.Max(baseBitmap.Width, hatOffset.X + hatBitmap.Width);
                rectArea.Bottom = Math.Max(baseBitmap.Height, hatOffset.Y + hatBitmap.Height);
                rectArea.Width = rectArea.Right - rectArea.Left;
                rectArea.Height = rectArea.Bottom - rectArea.Top;
            }
            else
            {
                rectArea.X = 0;
                rectArea.Y = 0;
                rectArea.Width = baseBitmap.Width;
                rectArea.Height = baseBitmap.Height;
            }

            SKImageInfo info = new(rectArea.Width, rectArea.Height, SKColorType.Rgba8888, SKAlphaType.Premul);

            using SKSurface surface = SKSurface.Create(info);
            SKCanvas canvas = surface.Canvas;
            canvas.Clear(SKColors.Transparent);

            int offsetX = -rectArea.Left;
            int offsetY = -rectArea.Top;
            canvas.DrawBitmap(baseBitmap, offsetX, offsetY);
            canvas.DrawBitmap(twoBitmap, offsetX, offsetY);
            canvas.DrawBitmap(threeBitmap, offsetX, offsetY);
            canvas.DrawBitmap(fourBitmap, offsetX, offsetY);

            if (hatBitmap != null)
                canvas.DrawBitmap(hatBitmap, offsetX + hatOffset.X, offsetY + hatOffset.Y);

            using SKImage image = surface.Snapshot();
            using SKBitmap bitmap = SKBitmap.FromImage(image);

            int width = bitmap.Width;
            int height = bitmap.Height;
            if (width <= 0 || height <= 0)
                return new RectArea(0, 0, 0, 0);

            IntPtr pixels = bitmap.GetPixels();
            if (pixels == IntPtr.Zero)
                return new RectArea(0, 0, 0, 0);

            // 一次性把像素拷进托管数组再扫。旧实现逐像素 GetPixel 是 65536 次 P/Invoke，
            // 这里是 65536 次纯内存访问，快两个数量级。
            int stride = width * 4;
            byte[] buffer = new byte[stride * height];
            Marshal.Copy(pixels, buffer, 0, buffer.Length);

            int minX = width;
            int minY = height;
            int maxX = -1;
            int maxY = -1;

            for (int y = 0; y < height; y++)
            {
                int rowStart = y * stride;
                for (int x = 0; x < width; x++)
                {
                    if (buffer[rowStart + x * 4 + 3] == 0)
                        continue;

                    if (x < minX) minX = x;
                    if (x > maxX) maxX = x;
                    if (y < minY) minY = y;
                    if (y > maxY) maxY = y;
                }
            }

            if (maxX < 0)
                return new RectArea(0, 0, 0, 0);

            return new RectArea(minX, minY, maxX - minX + 1, maxY - minY + 1);
        }

        /// <summary>
        /// Avalonia Bitmap → SKBitmap 直接拷像素。
        /// 旧实现用 MemoryStream 走了一次 PNG 编码 + 解码，单张 256×256 要几毫秒，
        /// GetImageArea 一次要转 5 张，换装时能明显感觉到停顿。
        /// </summary>
        private static SKBitmap ToSKBitmap(Bitmap bitmap)
        {
            Avalonia.PixelSize size = bitmap.PixelSize;
            if (size.Width <= 0 || size.Height <= 0)
                throw new ArgumentException("Bitmap has empty size", nameof(bitmap));

            SKColorType colorType = bitmap.Format == PixelFormat.Rgba8888
                ? SKColorType.Rgba8888
                : SKColorType.Bgra8888;

            SKImageInfo info = new(size.Width, size.Height, colorType, SKAlphaType.Premul);
            SKBitmap skBitmap = new(info);

            try
            {
                bitmap.CopyPixels(
                    new Avalonia.PixelRect(0, 0, size.Width, size.Height),
                    skBitmap.GetPixels(),
                    info.BytesSize,
                    info.RowBytes);
                return skBitmap;
            }
            catch (Exception ex)
            {
                skBitmap.Dispose();
                throw new Exception("Failed to copy pixels into SKBitmap", ex);
            }
        }
    }
}
