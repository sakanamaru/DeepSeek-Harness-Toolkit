# dsh-toolkit-bridge（可选 · 只读桥接插件）

> ⚠️ 这是 **dsh 侧的可选插件**，不是工具箱本体。工具箱本体是**独立进程**（不注入 dsh、dsh 没装也能用）；
> 装了这个插件，工具箱的「会话 / token 面板」能多拿到**实时**信息（尤其是"**当前有几个会话在运行**"）。
> **不装也能用**——工具箱会退回到只读 dsh 落盘的会话投影，只是少了实时那部分。

## 它做什么

每 3 秒（可配）读一次 dsh 自己已经算好的东西，原子写一份小 JSON：

- `ctx.sessions` / `ctx.sessionQuery.listSessions()` → 会话清单，**含 `live` 标记**（这就是"正在运行"的可观测事实）
- `ctx.sessionProjections.snapshot(session)` → 每个会话的 `tokenUsage` / `sessionStats` / `contextPressure` / `sessionListMetadata` / `title`

写到：`<DSH_HOME>/toolkit-bridge/sessions.json`（格式见下）。

**它不做什么**（硬约束）：
- ❌ 不发模型请求（不消耗 token、不碰 API）
- ❌ 不写 dsh 状态（只读；快照写在工具箱自己的目录里）
- ❌ 不读会话正文（只读计数、时间与元数据：id/标题/cwd/用量）
- ❌ 不联网
- ❌ 不阻塞：定时器 + 全 try/catch，任何失败静默忽略（**插件坏了不能影响 dsh**）

## 安装 / 卸载（用 dsh 官方命令）

```bash
# 从本地目录安装（把 <repo> 换成本仓库路径）
dsh plugin --profile web add "<repo>/plugin/dsh-toolkit-bridge"

# 卸载：还原 cordis.patch.yml 里那一行
dsh plugin --profile web remove dsh-toolkit-bridge
```

也可以用工具箱自己的**手动隔离处方**把这一行关掉（不删包）：

```powershell
DeepSeek Harness Toolkit.exe profilepatch --disable toolkit-bridge --yes
```

## 快照格式（v2，工具箱侧 `SessionStats.ParseSnapshot` 按此解析）

```json
{
  "formatVersion": 2,
  "generatedAt": "2026-09-28T00:00:00.000Z",
  "sessions": [
    {
      "id": "…", "live": true, "title": "…", "cwd": "D:\\work",
      "createdAt": 1788517824758, "lastPromptAt": 1788517999999, "blank": false,
      "turns": 2, "steps": 9, "llmMs": 1000, "toolMs": 500, "ttftMs": 300,
      "decodeMs": 2000, "decodeTokens": 400,
      "uncachedInputTokens": 100, "outputTokens": 50, "cacheReadTokens": 900, "cacheWriteTokens": 10,
      "contextWindow": 1000, "pressureTokens": 250, "surfaceTokens": 500
    }
  ]
}
```

- 时间戳是 **epoch 毫秒**（与 dsh 投影一致）；工具箱会转成 UTC ISO 显示
- `formatVersion` 不匹配时工具箱**不解析**（诚实降级，不猜）

## 自测（零依赖，不需要 dsh）

```bash
cd plugin/dsh-toolkit-bridge
node test/snapshot.test.js
```

覆盖纯函数（字段映射、两种投影形状、防御式收集、原子写、`DSH_HOME` 语义、`apply` 首帧与 `enabled:false`）。

## 诚实边界（请务必知道）

- 本插件的 `ctx` 服务名与调用形状是**依据 dsh 已发布包的 README 写的**
  （`ctx.sessions.list()` / `ctx.sessionQuery.listSessions()` / `ctx.sessionProjections.snapshot(session)`），
  **尚未在真实 dsh 上跑过**——所有取值都做了防御（服务缺失或形状不同 → 跳过该字段，绝不猜）。
- 真实 dsh 上的验证方式：在**隔离的 `$DSH_HOME` + 从模板新建的临时 profile** 里装它跑一次，
  然后看 `<DSH_HOME>/toolkit-bridge/sessions.json` 是否出现、`live` 是否随会话启停变化。
- 它**不是**工具箱的必需组件；没有它，面板的数据来自磁盘投影（`storages/session_projcache/`）。

## 许可

MIT（与本仓库一致）。本插件不包含任何第三方代码，只使用 dsh 的公开插件接口。
