using System.Collections.Generic;

namespace Dsht.Domain.Model
{
    /// <summary>一个待扫描的 profile 文件（纯数据）：显示标签、绝对路径、文本内容。</summary>
    public sealed class ProfileFile
    {
        public string Label { get; private set; }
        public string Path { get; private set; }
        public string Text { get; private set; }

        public ProfileFile(string label, string path, string text)
        {
            Label = label == null ? "" : label;
            Path = path == null ? "" : path;
            Text = text == null ? "" : text;
        }
    }

    /// <summary>一次 profile 收集结果：文件列表 + 被跳过的 vendor 文件数（透明，不静默吞掉）。</summary>
    public sealed class ProfileCollection
    {
        public List<ProfileFile> Files { get; private set; }
        public int SkippedVendor { get; set; }

        public ProfileCollection()
        {
            Files = new List<ProfileFile>();
            SkippedVendor = 0;
        }
    }
}