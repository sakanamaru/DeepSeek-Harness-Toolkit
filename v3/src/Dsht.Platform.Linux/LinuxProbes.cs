using System;
using System.Net;
using System.Net.Sockets;
using Dsht.Domain.Abstractions;

namespace Dsht.Platform.Linux
{
    /// <summary>端口探测（Linux）：与 Windows 实现同语义（TcpClient 连回环）。</summary>
    public sealed class LinuxPortProbe : IPortProbe
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

    /// <summary>HTTP 探测（Linux）：与 Windows 实现同语义（GET，2xx/3xx = 就绪）。</summary>
    public sealed class LinuxHttpProbe : IHttpProbe
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