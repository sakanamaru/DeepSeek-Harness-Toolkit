namespace Dsht.Domain.Model
{
    /// <summary>dsh 的可管理形态。
    /// Unknown 是一等公民：形态识别只能依据可观测事实（端口/进程/命令行/管道），
    /// 拿不准时必须显式返回 Unknown 并提示"未识别形态"，**绝不假装 Ready**。</summary>
    public enum AppKind
    {
        Unknown = 0,
        Web = 1,        // dsh web：浏览器界面，通常监听本地端口
        Headless = 2,   // dsh headless：无界面
        Acp = 3,        // Agent Client Protocol：编辑器/IDE 嵌入
        Desktop = 4     // 桌面客户端（形态待观测，仅预留）
    }
}