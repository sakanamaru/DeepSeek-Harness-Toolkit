using System;
using System.Collections.Generic;
using Dsht.Domain.Model;

namespace Dsht.Domain.Services
{
    /// <summary>一个 profile 的清单（来自 `profiles/&lt;name&gt;/package.json`，只取我们需要的字段）。</summary>
    public sealed class ProfileManifestInfo
    {
        /// <summary>是否解析成功。false = 格式不认（调用方必须诚实降级，绝不猜）。</summary>
        public bool Parsed;

        /// <summary>profile 名（目录名；manifest 里的 name 字段优先）。</summary>
        public string Name = "";

        /// <summary>`dsh.profile.bundles`（按 manifest 里的顺序；顺序有意义——后加载的覆盖前者）。</summary>
        public string[] Bundles = new string[0];

        /// <summary>由 bundles 推断出的**配置形态**（不是运行时形态）。</summary>
        public AppKind ConfiguredForm = AppKind.Unknown;

        /// <summary>@deepseek-ai/* 的官方包（base + app bundle + 官方插件）。</summary>
        public string[] OfficialBundles = new string[0];

        /// <summary>非 @deepseek-ai/* 的第三方插件（用户自己加的）。</summary>
        public string[] ThirdPartyPlugins = new string[0];
    }

    /// <summary>profile manifest 解析与形态推断（纯函数）。
    /// 依据（本机实测，dsh 0.1.5-rc.2）：`$DSH_HOME/profiles/&lt;name&gt;/package.json` 里有
    /// `dsh.profile.bundles`，直接列出该 profile 的组合包，例如
    /// `["@deepseek-ai/dsh-base", "@deepseek-ai/dsh-web-app", "example-search-plugin"]`。
    /// 形态由**哪个 app bundle 被启用**决定：web / headless / acp / sdk 各对应一个 bundle
    /// （dsh 官方文档：`--profile web|headless|acp|sdk`，SDK 与 ACP 都是 profile 而不是独立 bin）。
    /// **这是"配置形态"，不是"运行形态"**：必须与运行时事实（端口/进程）分开陈述。</summary>
    public static class ProfileManifest
    {
        public const string BundleBase = "@deepseek-ai/dsh-base";
        public const string BundleWeb = "@deepseek-ai/dsh-web-app";
        public const string BundleHeadless = "@deepseek-ai/dsh-headless";
        public const string BundleAcp = "@deepseek-ai/dsh-acp-app";
        public const string BundleSdk = "@deepseek-ai/dsh-sdk-app";

        /// <summary>解析 manifest 文本；格式不认 → Parsed=false（其余字段为空/Unknown）。</summary>
        public static ProfileManifestInfo Parse(string json)
        {
            ProfileManifestInfo info = new ProfileManifestInfo();
            JNode root = JsonLite.Parse(json);
            if (root == null || !root.IsObject) return info;          // 格式不认
            JNode bundlesNode = root.Path("dsh", "profile", "bundles");
            if (bundlesNode == null || !bundlesNode.IsArray) return info;
            info.Name = root.Get("name") == null ? "" : root.Get("name").AsString("");
            info.Bundles = bundlesNode.AsStringArray();
            info.ConfiguredForm = FromBundles(info.Bundles);
            List<string> official = new List<string>();
            List<string> third = new List<string>();
            for (int i = 0; i < info.Bundles.Length; i++)
            {
                string b = info.Bundles[i];
                if (b != null && b.StartsWith("@deepseek-ai/", StringComparison.Ordinal)) official.Add(b);
                else third.Add(b);
            }
            info.OfficialBundles = official.ToArray();
            info.ThirdPartyPlugins = third.ToArray();
            info.Parsed = true;
            return info;
        }

        /// <summary>bundles → 配置形态。按 manifest 顺序扫描，**最后一个被识别的 app bundle 生效**
        /// （dsh 的 bundle 是顺序叠加、后者按 id 覆盖前者）；没有 app bundle → Unknown。</summary>
        public static AppKind FromBundles(string[] bundles)
        {
            if (bundles == null) return AppKind.Unknown;
            AppKind kind = AppKind.Unknown;
            for (int i = 0; i < bundles.Length; i++)
            {
                AppKind k = KindOfBundle(bundles[i]);
                if (k != AppKind.Unknown) kind = k;
            }
            return kind;
        }

        /// <summary>单个 bundle → 形态（不认识 → Unknown）。</summary>
        public static AppKind KindOfBundle(string bundle)
        {
            if (string.IsNullOrEmpty(bundle)) return AppKind.Unknown;
            string b = bundle.Trim();
            if (string.Equals(b, BundleWeb, StringComparison.OrdinalIgnoreCase)) return AppKind.Web;
            if (string.Equals(b, BundleHeadless, StringComparison.OrdinalIgnoreCase)) return AppKind.Headless;
            if (string.Equals(b, BundleAcp, StringComparison.OrdinalIgnoreCase)) return AppKind.Acp;
            if (string.Equals(b, BundleSdk, StringComparison.OrdinalIgnoreCase)) return AppKind.Unknown;   // SDK 不是可管理形态
            return AppKind.Unknown;
        }

        /// <summary>形态 → 标记行里用的稳定短名。</summary>
        public static string FormName(AppKind k)
        {
            switch (k)
            {
                case AppKind.Web: return "web";
                case AppKind.Headless: return "headless";
                case AppKind.Acp: return "acp";
                case AppKind.Desktop: return "desktop";
                default: return "unknown";
            }
        }
    }
}
