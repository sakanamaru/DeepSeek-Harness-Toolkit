using System;
using System.Collections.Generic;

namespace Dsht.Domain.Services
{
    /// <summary>补丁行编辑计划（纯函数产出）。</summary>
    public sealed class PatchPlan
    {
        /// <summary>无需改动（已经是我们想要的状态）。</summary>
        public bool Noop;
        /// <summary>计划可用（false 时看 Reason）。</summary>
        public bool Valid;
        /// <summary>原因键（与 v2.x 对齐：entry-not-found / not-disabled / empty-id 等）。</summary>
        public string Reason = "";
        /// <summary>目标行号（1 基，用于标记行）。</summary>
        public int Line;
        /// <summary>写回的新文本（Valid 且非 Noop 时有效）。</summary>
        public string NewText = "";
    }

    /// <summary>profile 补丁行的编辑（**纯函数**）：把"禁用 / 恢复某个条目"算成"新文本 + 行号 + 原因键"，
    /// 平台层只负责"备份 → 写盘 → 复检 → 失败回滚"。
    /// 语义与 v2.x 的 profilepatch 对齐（标记行可直接对照）：
    ///   · 已经 `disabled: true` → Noop；
    ///   · 条目不存在 → `entry-not-found`（免得写一条永远匹配不到的补丁）；
    ///   · 否则在**文件末尾追加**顶层行 `- id: &lt;id&gt;` + `  disabled: true`（dsh 的 PatchOptions.disabled 是一等字段，
    ///     dsh 自己关遥测行也这么写）。
    /// **恢复（enable）是 V3 新增的**：把该顶层行的 `disabled: true` 改成 `false`，**不删行** ——
    /// 用户自己写的行不该被我们删掉（宁可留一行显式的 false）。</summary>
    public static class PatchPlanner
    {
        public static PatchPlan PlanDisable(string text, string id)
        {
            PatchPlan p = new PatchPlan();
            if (string.IsNullOrEmpty(id) || id.Trim().Length == 0) { p.Reason = "empty-id"; return p; }
            string body = text == null ? "" : text;
            if (HasDisabled(body, id)) { p.Noop = true; p.Reason = "already-disabled"; return p; }
            if (!HasEntry(body, id)) { p.Reason = "entry-not-found"; return p; }
            // 已有该 id 的顶层补丁行（可能是我们先前写的，也可能是用户写的）→ **翻转它**，
            // 不再追加重复行 ✗ —— 否则每轮 disable/enable 都会堆一行（真机往返测试抓到的缺口）。
            PatchPlan flip = FlipRow(body, id, true);
            if (flip.Valid) return flip;

            string nl = body.IndexOf("\r\n", StringComparison.Ordinal) >= 0 ? "\r\n" : "\n";
            bool needNl = body.Length > 0 && !body.EndsWith(nl, StringComparison.Ordinal);
            string insert = (needNl ? nl : "") + "- id: " + id + nl + "  disabled: true" + nl;
            p.Valid = true;
            p.NewText = body + insert;
            // 行号 = 内容行数 + 1：末尾那个换行不算一行（否则会多 1 —— 单测抓到的）
            int contentLines = CountLines(body);
            if (contentLines > 0 && body.EndsWith(nl, StringComparison.Ordinal)) contentLines--;
            p.Line = contentLines + 1;
            return p;
        }

        public static PatchPlan PlanEnable(string text, string id)
        {
            PatchPlan p = new PatchPlan();
            if (string.IsNullOrEmpty(id) || id.Trim().Length == 0) { p.Reason = "empty-id"; return p; }
            string body = text == null ? "" : text;
            if (!HasEntry(body, id)) { p.Reason = "entry-not-found"; return p; }
            if (!HasDisabled(body, id)) { p.Noop = true; p.Reason = "not-disabled"; return p; }

            return FlipRow(body, id, false);
        }

        /// <summary>把该 id 所属顶层补丁行的 disabled 值翻成 toTrue；找不到返回 Invalid。</summary>
        private static PatchPlan FlipRow(string body, string id, bool toTrue)
        {
            PatchPlan p = new PatchPlan();
            string nl = body.IndexOf("\r\n", StringComparison.Ordinal) >= 0 ? "\r\n" : "\n";
            string[] lines = body.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                string t = lines[i].Trim();
                if (!t.StartsWith("disabled:", StringComparison.Ordinal)) continue;
                // 不按 true/false 过滤：翻转两个方向都要能找到这一行（PlanEnable 已先用 HasDisabled 排除了 Noop 情形）
                if (!OwnerIs(lines, i, id)) continue;
                string indent = lines[i].Substring(0, lines[i].Length - lines[i].TrimStart().Length);
                lines[i] = indent + (toTrue ? "disabled: true" : "disabled: false");
                p.Valid = true;
                p.Line = i + 1;
                p.NewText = string.Join(nl, lines);
                return p;
            }
            p.Reason = "not-disabled";
            return p;
        }

        /// <summary>该条目是否已被禁用（与 v2.x 的 PatchHasDisabled 同义）。</summary>
        public static bool HasDisabled(string text, string id)
        {
            if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(id)) return false;
            string[] lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                string t = lines[i].Trim();
                if (!t.StartsWith("disabled:", StringComparison.Ordinal)) continue;
                if (t.IndexOf("true", StringComparison.OrdinalIgnoreCase) < 0) continue;   // HasDisabled 只认 disabled: true ✓
                // 不按 true/false 过滤：翻转两个方向都要能找到这一行（PlanEnable 已先用 HasDisabled 排除了 Noop 情形）
                if (OwnerIs(lines, i, id)) return true;
            }
            return false;
        }

        /// <summary>文件里是否存在这个 id（任意形式的 `id: &lt;id&gt;`）。</summary>
        public static bool HasEntry(string text, string id)
        {
            if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(id)) return false;
            string[] lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                if (IdOf(lines[i]) == id) return true;
            }
            return false;
        }

        // ---- 内部 ----

        /// <summary>从某行取出 `id:` 的值（去引号）；不是 id 行返回 null。</summary>
        private static string IdOf(string line)
        {
            string t = line.Trim();
            if (t.StartsWith("- id:", StringComparison.Ordinal)) return t.Substring("- id:".Length).Trim().Trim('\'', '"');
            if (t.StartsWith("id:", StringComparison.Ordinal)) return t.Substring("id:".Length).Trim().Trim('\'', '"');
            return null;
        }

        /// <summary>某个 `disabled:` 行（下标 i）向上找最近的 id 行，判断是否属于 id。</summary>
        private static bool OwnerIs(string[] lines, int i, string id)
        {
            for (int k = i - 1; k >= 0 && k > i - 40; k--)
            {
                string t = lines[k].Trim();
                if (t.StartsWith("disabled:", StringComparison.Ordinal)) continue;
                string got = IdOf(lines[k]);
                if (got != null) return got == id;
            }
            return false;
        }

        private static int CountLines(string s)
        {
            if (string.IsNullOrEmpty(s)) return 0;
            int n = 1;
            for (int i = 0; i < s.Length; i++) if (s[i] == '\n') n++;
            return n;
        }
    }
}