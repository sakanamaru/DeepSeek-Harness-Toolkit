namespace Dsht.Domain.Model
{
    /// <summary>启动失败堆栈的解析结果（纯数据）。Recognized=false 表示未识别到已知签名——此时不做任何猜测。</summary>
    public sealed class BootDiagResult
    {
        public bool Recognized;
        public string Kind = "unknown";
        public string Plugin = "";
        public string Entry = "";
        public string File = "";
        public int Line;
        public string Hint = "";
        public string FirstError = "";
    }
}