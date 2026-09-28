namespace Dsht.Domain.Model
{
    /// <summary>服务三态。与 v2.x 的 ServiceState 语义对齐（V3-1 搬运时逐条核对）。</summary>
    public enum ServiceState
    {
        Down = 0,
        Starting = 1,
        Ready = 2
    }
}