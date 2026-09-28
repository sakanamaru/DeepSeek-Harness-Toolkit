/**
 * dsh-shio-bridge —— cordis 插件入口。
 *
 * **零依赖**：本文件只 import 同目录的 snapshot.js，不 import 任何 dsh 包。
 *
 * 为什么（真机验证教训，2026-09-28）：最初这里 `import z from "@deepseek-ai/schemastery"` 想按 dsh 官方插件
 * 的做法声明配置 schema —— 但从**本地路径**安装时，该导入会从插件源码目录解析 → `ERR_MODULE_NOT_FOUND`
 * → **dsh 启动直接失败**（plugin tree failed to load ✗）。可选插件绝不该有这种能力，所以：
 *   · 不 import 任何 dsh 包（配置直接用 patch 行里的 config，不做 schema 校验）
 *   · 所有取值仍然防御式（见 snapshot.js）
 *
 * 纪律（三条硬约束）：
 *   ① 只读：不写 dsh 状态、不发模型请求、不读会话正文、不联网；
 *   ② 不阻塞：定时器异步写，任何异常都吞掉（插件坏了不能影响 dsh 的运行）；
 *   ③ 可卸载：整行由 cordis.patch.yml 注册，`dsh plugin remove` 或工具箱的 `profilepatch --disable`
 *      都能一键还原；插件缺失时工具箱只用磁盘投影，功能降级但不报错。
 *
 * 注意：**导入期错误会阻止 dsh 启动**（dsh 的行为，不是本插件能控制的）——
 * 所以先在临时 profile 里装一次确认能起来，再装到你日常用的 profile。
 */
import { apply, buildSnapshot, collectSessions, defaultOutFile, writeSnapshot, SNAPSHOT_FORMAT_VERSION } from "./snapshot.js";

export { apply, buildSnapshot, collectSessions, defaultOutFile, writeSnapshot, SNAPSHOT_FORMAT_VERSION };

/** cordis 插件名（与 cordis.patch.yml 里的 id 对应）。 */
export const name = "shio-bridge";

/** 依赖的 ctx 服务：会话注册表 + 会话投影注册表（cordis DI 会等它们就绪）。 */
export const inject = ["sessions", "sessionProjections"];
