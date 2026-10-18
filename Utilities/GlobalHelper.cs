using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace BongoCatNotice.Utilities
{
    public class GlobalHelper
    {
        public static string ProjectName { get; } = "BongoCatNotice";
        public static string Name { get; } = "BongoCatNotice";
        public static string Version { get; } = "0.1.0.5";
        public static string Url { get; } = "https://github.com/HBrOcean/BongoCatNotice";
        public static int WindowWidth { get; } = 256;
        public static int WindowHeight { get; } = 256;
        public static SkinManager CatSkin { get; set; } = SkinManager.Instance;
        public static AppConfig Config = ConfigManager.LoadConfig();

        public static double GetScaling(int index)
        {
            return index switch
            {
                0 => 0.25,
                1 => 0.375,
                2 => 0.5,
                3 => 0.625,
                4 => 0.75,
                5 => 1,
                6 => 1.1,
                _ => 1
            };
        }
    }

    public class ItemsJson
    {
        [JsonPropertyName("skin")]
        public required Dictionary<string, SkinItem> Skin { get; set; }

        [JsonPropertyName("hat")]
        public required Dictionary<string, HatItem> Hat { get; set; }
    }

    public class ItemBase
    {
        [JsonPropertyName("name")]
        public required string Name { get; set; }

        [JsonPropertyName("description")]
        public required string Description { get; set; }

        [JsonPropertyName("modified")]
        public required string Modified { get; set; }

        [JsonPropertyName("created")]
        public required string Created { get; set; }

        [JsonPropertyName("icon")]
        public required string Icon { get; set; }

        [JsonPropertyName("tags")]
        public required string Tags { get; set; }
    }

    public class SkinItem : ItemBase
    {
        [JsonPropertyName("image")]
        public required string[] Image { get; set; }

        // "layer"(默认,四帧分层) 或 "full"(整猫帧:[静止, 左击, 右击, 占位])
        [JsonPropertyName("mode")]
        public string? Mode { get; set; }

        /// <summary>
        /// 外部皮肤所在目录(热加载)。内嵌皮肤为 null。
        /// 不参与序列化，仅运行时用来定位图片文件。
        /// </summary>
        [JsonIgnore]
        public string? ExternalDir { get; set; }
    }

    public class HatItem : ItemBase
    {
        [JsonPropertyName("image")]
        public required string Image { get; set; }
    }

    [JsonSerializable(typeof(ItemsJson))]
    public partial class ItemsJsonContext : JsonSerializerContext { }

    /// <summary>外部皮肤清单解析（热加载用）</summary>
    [JsonSerializable(typeof(SkinItem))]
    [JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true)]
    public partial class SkinJsonContext : JsonSerializerContext { }

    public class HatOffsetJson
    {
        [JsonPropertyName("name")]
        public required string Name { get; set; }
        [JsonPropertyName("position")]
        public required Position Position { get; set; }
    }

    public class Position
    {
        [JsonPropertyName("x")]
        public int X { get; set; }
        [JsonPropertyName("y")]
        public int Y { get; set; }
    }

    [JsonSerializable(typeof(HatOffsetJson[]))]
    public partial class HatOffsetJsonContext : JsonSerializerContext { }
}
