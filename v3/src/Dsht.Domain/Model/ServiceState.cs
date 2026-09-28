namespace Dsht.Domain.Model
{
    /// <summary>服务三态。**与 v2.x 完全对齐**：Down / Listening（端口已开但服务未就绪）/ Ready。
    /// 标记行契约：Ready→STATUS_UP、Listening→STATUS_STARTING、Down→STATUS_DOWN。</summary>
    public enum ServiceState
    {
        Down = 0,
        Listening = 1,
        Ready = 2
    }
}