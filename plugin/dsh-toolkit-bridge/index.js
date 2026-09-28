/**
 * dsh-toolkit-bridge —— cordis 插件入口（**只有这一层依赖宿主**）。
 *
 * 纯逻辑在 snapshot.js（零依赖、可用普通 node 自测）；这里只声明 cordis 插件并重新导出纯函数。
 *
 * 纪律（三条硬约束）：
 *   ① 只读：不写 dsh 状态、不发模型请求、不读会话正文、不联网；
 *   ② 不阻塞：定时器异步写，任何异常都吞掉（插件坏了不能影响 dsh）；
 *   ③ 可卸载：整行由 cordis.patch.yml 注册，`dsh plugin remove` 或工具箱的 `profilepatch --disable`
 *      都能一键还原；插件缺失时工具箱只用磁盘投影，功能降级但不报错。
 *
 * 诚实边界：ctx 服务名与调用形状依据 dsh 已发布包的 README 写成（`ctx.sessions.list()` /
 * `ctx.sessionQuery.listSessions()` / `ctx.sessionProjections.snapshot(session)`），**尚未在真实 dsh 上跑过**；
 * 所有取值都做了防御（服务缺失或形状不同 → 跳过该字段，绝不猜）。
 */
import z from "@deepseek-ai/schemastery";
import { apply, buildSnapshot, collectSessions, defaultOutFile, writeSnapshot, SNAPSHOT_FORMAT_VERSION } from "./snapshot.js";

export { apply, buildSnapshot, collectSessions, defaultOutFile, writeSnapshot, SNAPSHOT_FORMAT_VERSION };

/** cordis 插件名（与 cordis.patch.yml 里的 id 对应）。 */
export const name = "toolkit-bridge";

/** 依赖的 ctx 服务：会话注册表 + 会话投影注册表（cordis DI 会等它们就绪）。 */
export const inject = ["sessions", "sessionProjections"];

/** 配置 schema（schemastery，与 dsh 官方插件一致的做法）。 */
export const Config = z.object({
	enabled: z.boolean().default(true),
	outFile: z.string().default(""),
	intervalMs: z.natural().default(3000),
});
