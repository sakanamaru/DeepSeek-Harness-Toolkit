# dsh-minato-bridge（可选 · 只读桥接插件）

> ⚠️ 这是 **dsh 侧的可选插件**，不是工具箱本体。工具箱本体是**独立进程**（不注入 dsh、dsh 没装也能用）；
> 装了这个插件，工具箱的「会话 / token 面板」能多拿到**实时**信息（尤其是"**当前有几个会话在运行**"）。
> **不装也能用**——工具箱会退回到只读 dsh 落盘的会话投影，只是少了实时那部分。

## 它做什么

每 3 秒（可配）读一次 dsh 自己已经算好的东西，原子写一份小 JSON：

- `ctx.sessions` / `ctx.sessionQuery.listSessions()` → 会话清单，**含 `live` 标记**（这就是"正在运行"的可观测事实）
- `ctx.sessionProjections.snapshot(session)` → 每个会话的 `tokenUsage` / `sessionStats` / `contextPressure` / `sessionListMetadata` / `title`

写到：`<DSH_HOME>/shio-bridge/sessions.json`（格式见下）。

**它不做什么**（硬约束）：
- ❌ 不发模型请求（不消耗 token、不碰 API）
- ❌ 不写 dsh 状态（只读；快照写在工具箱自己的目录里）
- ❌ 不读会话正文（只读计数、时间与元数据：id/标题/cwd/用量）
- ❌ 不联网
- ❌ 不阻塞：定时器 + 全 try/catch，任何失败静默忽略（**插件坏了不能影响 dsh**）

## 安装 / 卸载（用 dsh 官方命令）

```bash
# 从本地目录安装（把 <repo> 换成本仓库路径）
dsh plugin --profile web add "<repo>/plugin/dsh-minato-bridge"

# 卸载：还原 cordis.patch.yml 里那一行
dsh plugin --profile web remove dsh-minato-bridge
```

也可以用工具箱自己的**手动隔离处方**把这一行关掉（不删包）：

```powershell
DeepSeek Harness Toolkit.exe profilepatch --disable shio-bridge --yes
```

## 快照格式（v2，工具箱侧 `SessionStats.ParseSnapshot` 按此解析）

```json
{
  "formatVersion": 2,
  "generatedAt": "2026-09-28T00:00:00.000Z",
  "sessions": [
    {
      "id": "…", "live": true, "title": "…", "cwd": "D:\\work",
      "createdAt": "2026-09-10T00:26:40.000Z", "lastPromptAt": "2026-09-10T00:27:40.000Z", "blank": false,
      "turns": 2, "steps": 9, "llmMs": 1000, "toolMs": 500, "ttftMs": 300,
      "decodeMs": 2000, "decodeTokens": 400,
      "uncachedInputTokens": 100, "outputTokens": 50, "cacheReadTokens": 900, "cacheWriteTokens": 10,
      "contextWindow": 1000, "pressureTokens": 250, "surfaceTokens": 500
    }
  ]
}
```

- 时间戳是 **ISO 8601 字符串**（如 `2026-09-10T00:26:40.000Z`）—— **不是数字** ✗
  （C# 侧的 `Str()` **只认字符串** ✓ 写数字会被丢成空串 ✓ → 面板显示 `unknown` ✓ 且默认排序退化 ✓✓）
- `formatVersion` 不匹配时工具箱**不解析**（诚实降级，不猜）

## 自测（零依赖，不需要 dsh）

```bash
cd plugin/dsh-minato-bridge
node test/snapshot.test.js
```

覆盖纯函数（字段映射、两种投影形状、防御式收集、原子写、`DSH_HOME` 语义、`apply` 首帧与 `enabled:false`）。

## 诚实边界（请务必知道）

- 本插件的 `ctx` 服务名与调用形状是**依据 dsh 已发布包的 README 写的**
  （`ctx.sessions.list()` / `ctx.sessionQuery.listSessions()` / `ctx.sessionProjections.snapshot(session)`），
  **尚未在真实 dsh 上跑过**——所有取值都做了防御（服务缺失或形状不同 → 跳过该字段，绝不猜）。
- 真实 dsh 上的验证方式：在**隔离的 `$DSH_HOME` + 从模板新建的临时 profile** 里装它跑一次，
  然后看 `<DSH_HOME>/shio-bridge/sessions.json` 是否出现、`live` 是否随会话启停变化。
- 它**不是**工具箱的必需组件；没有它，面板的数据来自磁盘投影（`storages/session_projcache/`）。

## 许可

MIT（与本仓库一致）。本插件不包含任何第三方代码，只使用 dsh 的公开插件接口。

## 真机验证记录（2026-09-28，隔离 `$DSH_HOME` + 临时 profile + 端口 3999）

**第一次：失败 ✗（真实缺陷，已修）**
```
dsh: plugin tree failed to load: failed to import loader entry shio-bridge (dsh-minato-bridge):
Cannot find package '@deepseek-ai/schemastery' imported from .../plugin/dsh-minato-bridge/index.js
```
原因：`index.js` 里 `import z from "@deepseek-ai/schemastery"`（想按 dsh 官方插件做法声明配置 schema）——
从**本地路径**安装时该导入从插件源码目录解析 → `ERR_MODULE_NOT_FOUND` → **dsh 启动直接失败**。
**教训：可选插件绝不能因为一个未解析的导入就拖垮 dsh 的启动** → 已改为**零依赖**（不 import 任何 dsh 包，配置直接取 patch 行里的值，不做 schema 校验）。

**第二次：通过 ✓**
```
index.js 语法检查 ✓ · 自测 19/19 ✓ · index.js 不再含任何 @deepseek-ai 导入 ✓
dsh --profile bridgetest --port 3999 启动成功（3999 监听）✓ · 输出无 "plugin tree failed" ✓
用户正在跑的 3080 实例（PID 16748）全程未被触碰 ✓
```

**仍未验证的部分（诚实说）**：**快照是否真的写出**——因为隔离根里一个会话都没有
（`storages/session_projcache/sessions` 为空），而插件设计上**只在有会话时才写快照**
（避免用空数据覆盖上一次的好数据）。要验证这一步，需要在这个隔离实例里**真的发一条消息**
（打开 `http://127.0.0.1:3999` 聊一句），然后看 `<DSH_HOME>/shio-bridge/sessions.json`。

**给你的复现命令**（隔离根已经建好并装好插件了，直接复用）：
```bash
export DSH_HOME=/tmp/dsht_plug_iso        # Windows: $env:DSH_HOME="$env:TEMP\dsht_plug_iso"
dsh --profile bridgetest --port 3999
# 浏览器打开 http://127.0.0.1:3999 发一条消息，然后：
cat "$DSH_HOME/shio-bridge/sessions.json"     # 应出现快照，live 会随会话启停变化
```