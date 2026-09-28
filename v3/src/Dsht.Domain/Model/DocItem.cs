namespace Dsht.Domain.Model
{
    /// <summary>诊断条目：类别 + 级别（0=OK 1=WARN 2=ERROR）+ 描述。对齐 v2.x 的 DocItem。</summary>
    public sealed class DocItem
    {
        public string Cat { get; private set; }
        public int Level { get; private set; }
        public string Text { get; private set; }

        public DocItem(string cat, int level, string text)
        {
            Cat = cat == null ? "" : cat;
            Level = level;
            Text = text == null ? "" : text;
        }
    }
}