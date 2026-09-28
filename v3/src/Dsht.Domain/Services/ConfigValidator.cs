using System;

namespace Dsht.Domain.Services
{
    /// <summary>config-set 校验（纯函数，与 v2.x 的 NIValidateConfigSet 逐条对齐）：
    /// 返回 null=通过；否则原因键 no-key / unknown-key / bad-value。ws 用注入的规范化委托判定。</summary>
    public static class ConfigValidator
    {
        public static string Validate(string key, string value, Func<string, string> canonicalizePath)
        {
            if (string.IsNullOrWhiteSpace(key)) return "no-key";
            string k = key.Trim().ToLowerInvariant();
            string v = (value == null ? "" : value).Trim();
            if (k == "lang") return (v == "zh" || v == "en" || v == "auto" || v == "") ? null : "bad-value";
            if (k == "host") return (v == "127.0.0.1" || v == "localhost") ? null : "bad-value";
            if (k == "ws")
            {
                if (v.Length == 0) return null;
                if (canonicalizePath == null) return null;
                try { canonicalizePath(v); return null; } catch { return "bad-value"; }
            }
            if (k == "keep_backups")
            {
                int n;
                return (int.TryParse(v, out n) && n >= 3) ? null : "bad-value";
            }
            if (k == "check_update" || k == "check_dsh_update") return (v == "on" || v == "off") ? null : "bad-value";
            if (k == "update_channel") return (v == "stable" || v == "rc") ? null : "bad-value";
            if (k == "close_action") return (v.Length == 0 || v == "ask" || v == "tray" || v == "exit") ? null : "bad-value";
            if (k == "auto_start") return (v == "on" || v == "off") ? null : "bad-value";
            return "unknown-key";
        }

        public static Dsht.Domain.Model.ToolkitConfig ApplyTo(Dsht.Domain.Model.ToolkitConfig c, string key, string value, Func<string, string> canonicalizePath)
        {
            if (c == null) c = new Dsht.Domain.Model.ToolkitConfig();
            string k = key.Trim().ToLowerInvariant();
            string v = (value == null ? "" : value).Trim();
            if (k == "lang") c.Lang = (v == "zh" || v == "en") ? v : "auto";
            else if (k == "host") c.Host = v;
            else if (k == "ws")
            {
                if (v.Length == 0) c.Workspace = null;
                else if (canonicalizePath != null) { try { c.Workspace = canonicalizePath(v); } catch { c.Workspace = null; } }
                else c.Workspace = v;
            }
            else if (k == "keep_backups") { int n; int.TryParse(v, out n); c.KeepBackups = n < 3 ? 3 : n; }
            else if (k == "check_update") c.CheckUpdate = v != "off";
            else if (k == "check_dsh_update") c.CheckDshUpdate = v != "off";
            else if (k == "update_channel") c.UpdateChannel = v == "rc" ? "rc" : "stable";
            else if (k == "close_action") c.CloseAction = (v == "tray" || v == "exit") ? v : (v == "ask" ? "ask" : "");
            else if (k == "auto_start") c.AutoStart = v != "off";
            return c;
        }
    }
}