using System;
using System.Collections.Generic;

namespace Dsht.Gui.Avalonia.Markers
{
    /// <summary>一个 profile 卡片（来自 CLI `profiles` 的 PROFILE / BUNDLE 标记行）。</summary>
    public sealed class ProfileCard
    {
        public string Name = "";
        public string Form = "";            // web | headless | acp | unknown | unparsed
        public int Bundles;
        public int ThirdParty;
        public List<BundleItem> Items = new List<BundleItem>();
        /// <summary>被隔离（cordis.patch.yml 里 disabled: true）的条目 id。</summary>
        public List<string> Disabled = new List<string>();
        public string DisabledText { get { return Disabled.Count == 0 ? "" : "已隔离 " + Disabled.Count + " 项：" + string.Join("、", Disabled.ToArray()); } }

        public string FormText
        {
            get
            {
                if (Form == "web") return "Web · 浏览器界面";
                if (Form == "headless") return "Headless · 无界面（没有端口）";
                if (Form == "acp") return "ACP · 编辑器嵌入";
                if (Form == "unparsed") return "无法解析";
                return "未知形态";
            }
        }

        /// <summary>0=web 1=headless 2=acp 3=未知（颜色由界面层决定）。</summary>
        public int FormKind { get { return Form == "web" ? 0 : (Form == "headless" ? 1 : (Form == "acp" ? 2 : 3)); } }

        public string CountText { get { return Bundles + " 个组合包 · 第三方 " + ThirdParty + " 个"; } }
    }

    /// <summary>一个组合包/插件条目。</summary>
    public sealed class BundleItem
    {
        public string Id = "";
        public bool Official;
        public string Version = "";
        public string VersionText { get { return string.IsNullOrEmpty(Version) ? "" : "v" + Version; } }
        public string KindText { get { return Official ? "官方" : "第三方"; } }
    }

    public sealed class ProfilesSnapshot
    {
        public bool Ok;
        public string FailReason = "";
        public int Count;
        public List<ProfileCard> Profiles = new List<ProfileCard>();
    }

    /// <summary>解析 CLI `profiles` 的标记行（纯函数，**绝不抛**）。
    /// 契约：`PROFILES_OK n` / `PROFILE &lt;name&gt; form=… bundles=n thirdparty=m` /
    /// `BUNDLE &lt;profile&gt; &lt;bundle-id&gt; &lt;official|thirdparty&gt;` / `PROFILES_FAIL 原因`。</summary>
    public static class ProfilesMarkers
    {
        public static ProfilesSnapshot Parse(string output)
        {
            ProfilesSnapshot s = new ProfilesSnapshot();
            if (string.IsNullOrEmpty(output)) return s;
            string[] lines = output.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i] == null ? "" : lines[i].Trim();
                if (line.Length == 0) continue;
                try
                {
                    if (line.StartsWith("PROFILES_FAIL", StringComparison.Ordinal))
                    {
                        s.FailReason = line.Substring("PROFILES_FAIL".Length).Trim();
                        continue;
                    }
                    if (line.StartsWith("PROFILES_OK", StringComparison.Ordinal))
                    {
                        s.Count = (int)Num(line.Substring("PROFILES_OK".Length).Trim(), 0);
                        s.Ok = true;
                        continue;
                    }
                    if (line.StartsWith("PROFILE ", StringComparison.Ordinal))
                    {
                        string[] parts = line.Substring("PROFILE ".Length).Trim().Split(' ');
                        if (parts.Length == 0 || parts[0].Length == 0) continue;
                        ProfileCard c = new ProfileCard();
                        c.Name = parts[0];
                        for (int k = 1; k < parts.Length; k++)
                        {
                            int eq = parts[k].IndexOf('=');
                            if (eq <= 0) continue;
                            string key = parts[k].Substring(0, eq);
                            string val = parts[k].Substring(eq + 1);
                            if (key == "form") c.Form = val;
                            else if (key == "bundles") c.Bundles = (int)Num(val, 0);
                            else if (key == "thirdparty") c.ThirdParty = (int)Num(val, 0);
                        }
                        s.Profiles.Add(c);
                        continue;
                    }
                    if (line.StartsWith("DISABLED ", StringComparison.Ordinal))
                    {
                        string[] dp = line.Substring("DISABLED ".Length).Trim().Split(new char[] { ' ' }, 2);
                        if (dp.Length == 2) { ProfileCard dc = Find(s, dp[0]); if (dc != null) dc.Disabled.Add(dp[1].Trim()); }
                        continue;
                    }
                    if (line.StartsWith("BUNDLE ", StringComparison.Ordinal))
                    {
                        string[] parts = line.Substring("BUNDLE ".Length).Trim().Split(' ');
                        if (parts.Length < 3) continue;
                        ProfileCard c = Find(s, parts[0]);
                        if (c == null) continue;
                        BundleItem it = new BundleItem();
                        it.Id = parts[1];
                        it.Official = parts[2] == "official";
                        for (int k = 3; k < parts.Length; k++)
                        {
                            if (parts[k].StartsWith("version=", StringComparison.Ordinal)) it.Version = parts[k].Substring("version=".Length);
                        }
                        c.Items.Add(it);
                    }
                }
                catch
                {
                    /* 单行解析失败不影响其它行 */
                }
            }
            return s;
        }

        private static ProfileCard Find(ProfilesSnapshot s, string name)
        {
            for (int i = 0; i < s.Profiles.Count; i++) if (s.Profiles[i].Name == name) return s.Profiles[i];
            return null;
        }

        private static long Num(string v, long fallback)
        {
            long n;
            return long.TryParse(v, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out n) ? n : fallback;
        }
    }
}