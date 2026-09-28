namespace Dsht.Domain.Model
{
    /// <summary>profile 诊断的一条发现（纯数据）。
    /// Missing="maxDepth" 可自动修（对应 v2.x 的 PROFILECHK_FIX 行）；Missing="command" 只报不修。</summary>
    public sealed class ProfileFinding
    {
        public string File { get; set; }      // 文件标签（调用方提供的显示名）
        public int Line { get; set; }         // 1 基行号
        public string Id { get; set; }
        public string Missing { get; set; }   // "maxDepth" | "command"
        public string Hint { get; set; }

        /// <summary>是否可自动修（v2.x：只有 maxDepth 给 FIX 行，command 类只报不修）。</summary>
        public bool AutoFixable { get { return Missing == "maxDepth"; } }

        public override string ToString()
        {
            return File + ":" + Line + " " + Id + " missing=" + Missing;
        }
    }
}