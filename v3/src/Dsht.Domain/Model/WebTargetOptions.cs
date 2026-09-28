namespace Dsht.Domain.Model
{
    /// <summary>web 形态的观测参数（纯数据，不含任何 IO）。</summary>
    public sealed class WebTargetOptions
    {
        public int Port { get; private set; }
        public string Url { get; private set; }
        public int PortTimeoutMs { get; private set; }
        public int HttpTimeoutMs { get; private set; }

        public WebTargetOptions(int port, string url, int portTimeoutMs, int httpTimeoutMs)
        {
            Port = port;
            Url = url == null ? "" : url;
            PortTimeoutMs = portTimeoutMs;
            HttpTimeoutMs = httpTimeoutMs;
        }
    }
}