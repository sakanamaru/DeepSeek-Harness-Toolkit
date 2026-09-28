using System;
using System.Net;
using System.Net.Sockets;
using Dsht.Domain.Abstractions;

namespace Dsht.Platform.Windows
{
    /// <summary>端口探测：TcpClient 连接回环地址。逐条对齐 v2.x 的 IsPortOpen。</summary>
    public sealed class WindowsPortProbe : IPortProbe
    {
        public bool IsOpen(int port, int timeoutMs)
        {
            try
            {
                using (TcpClient c = new TcpClient())
                {
                    System.Threading.Tasks.Task t = c.ConnectAsync(IPAddress.Loopback, port);
                    return t.Wait(timeoutMs) && c.Connected;
                }
            }
            catch { return false; }
        }
    }

    /// <summary>HTTP 探测：GET，2xx/3xx 视为就绪。对齐 v2.x 的 HttpReady / HttpResponds。</summary>
    public sealed class WindowsHttpProbe : IHttpProbe
    {
        public bool IsReady(string url, int timeoutMs)
        {
            try
            {
                HttpWebRequest req = (HttpWebRequest)WebRequest.Create(url);
                req.Method = "GET";
                req.Timeout = timeoutMs;
                req.AllowAutoRedirect = true;
                using (HttpWebResponse resp = (HttpWebResponse)req.GetResponse())
                {
                    int code = (int)resp.StatusCode;
                    return code >= 200 && code < 400;
                }
            }
            catch { return false; }
        }

        /// <summary>任意状态码（含 401/403/404）也算有应答：服务活着但要求鉴权时用（对齐 v2.x 的 HttpResponds）。</summary>
        public bool Responds(string url, int timeoutMs)
        {
            try
            {
                HttpWebRequest req = (HttpWebRequest)WebRequest.Create(url);
                req.Method = "GET";
                req.Timeout = timeoutMs;
                req.AllowAutoRedirect = true;
                using (HttpWebResponse resp = (HttpWebResponse)req.GetResponse())
                {
                    return resp != null;
                }
            }
            catch (WebException wex) { return wex.Response != null; }
            catch { return false; }
        }
    }
}