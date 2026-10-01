<p align="center">
  <img src="https://raw.githubusercontent.com/sakanamaru/dsh-minato/main/logo.png" alt="dsh-minato" width="180">
</p>

# dsh-minato 3.0.0-preview.3（首个 3.0 预览版 / first 3.0 preview）

> ⚠️ 非官方工具，由社区独立开发，与 DeepSeek 官方无关。本工具不是 dsh 插件：它是独立进程，不注入 dsh，dsh 没装也能用。

## v3.0.0-preview.3 — 2026-10-02

### Fixed / 修复（preview.2）

- 发布产物不再混入 v2 旧产物：v2 的 build job 原来对任何 tag 触发，v3 tag 上会产出 v2 的 exe 与 zip；现在只对 v2* tag 触发。
- 单文件产物开启 EnableCompressionInSingleFile（体积更小）。

## v3.0.0-preview.1 — 2026-10-02

### Added / 新增

- **V3 跨平台 CLI** —— Windows 与 Linux 同一套命令面，标记行与 v2.x **逐字一致**（`compare_markers.ps1` 21/21）。
- **V3 图形界面**（Avalonia，Windows / Linux 通用）—— 概览 · 看板 · 会话与 Token · 形态与插件 · 备份 · 体检 · 设置 · 更新 · 日志。
- **可选的只读桥接插件** `dsh-minato-bridge` —— 见下方专节。装不装**由你决定**，不装只是少一个字段。
  Optional read-only bridge plugin; see the section below. Without it, only one field falls back to `unknown`.
- 发布物附 **`hashes.txt`**（含 GPG 签名 `hashes.txt.asc`）；CLI 自带 `verify-install` 自校验。

### Fixed / 修复

- **备份/恢复引擎的 47 项缺陷**（本轮真机审查 + 三轮子代理复审逐条修）：
  - **恢复失败会自动回滚**（此前失败后数据留在半途，无路可退）
  - **符号链接 / junction 不再绕过恢复隔离闸门**（两端都解析真实路径；真机双向验证）
  - **Node 运行时下载后必须过官方 `SHASUMS256` 校验**，不匹配就拒绝解压（fail-closed）
  - **`..` 越界删除**：纯词法规范化，逃逸一律拒绝
  - **中断的备份会被识别为不完整**（完成标记 + 内容哈希），恢复时拒收并说明原因
  - 不再**写穿目标端的符号链接**；不再把**部分失败报成成功**
  - `wipe` 只打印手动删除路径，**不删也不备份**（本版有**行为断言**守住这条承诺）
- **Windows 停桌面端时的真实死锁**：异步排空两条输出流 + 超时如实报失败 + 复核进程真的没了。
- **状态目录探测竞态**：探测名唯一（pid + 随机），并发进程不再得出不同结论。

### Changed / 变更

- **CLI 拆分**：`Program.cs` **3595 → 1060 行**（-70.5%），拆成 7 个 partial 文件；调用点与行为未变。
- **备份引擎去重**：Windows 与 Linux 两端**逐字相同**的 6 个方法（约 110 行）提成共享基类，
  两端各减 111 行 —— 此前"一处修 bug 必须记得改两处"，而**已经漏过一次**。
- **删除无人调用的 `IPaths.BackupsRoot`**（接口 + 2 份实现 + 2 条断言，-78 行）——
  它曾把一次安全修复"骗"进死代码里。
- **本地就绪门槛从 11 项加到 13 项**：新增"CI 接线不变量"与"`wipe` 行为诚实性"，
  并**如实打印本地覆盖不到的检查**（本地全绿 ≠ CI 全绿）。

## 🧩 桥接插件（可选）—— 它解决什么问题

工具箱本体是**独立进程**：不注入 dsh，**dsh 没装也能用**。但有一个事实**只有 dsh 进程自己知道**：

> **当前有几个会话正在运行**（以及每个会话的实时 token / 上下文压力）。

这是**进程内的运行态**，dsh **不落盘** —— 所以磁盘投影里没有它，工具箱只能显示 `unknown`。

| | 不装插件 | 装了插件 |
|---|---|---|
| 会话清单 / token / 命中率 / 速度 | ✅（读磁盘投影） | ✅（读磁盘投影） |
| **「运行中」标记** | ❌ `unknown` | ✅ **实时** |

**它不做什么**（硬约束）：❌ 不发模型请求（不消耗 token）· ❌ 不写 dsh 状态 · ❌ 不读会话正文 ·
❌ 不联网 · ❌ 不阻塞（全 try/catch，**插件坏了不能影响 dsh**）。

### 安装

```bash
# 直接填仓库地址（仓库根已声明 dsh.bundle）
dsh plugin --profile web add "https://github.com/sakanamaru/dsh-minato"

# 或本地目录（最稳，不依赖网络）
dsh plugin --profile web add "<本仓库路径>/plugin/dsh-minato-bridge"
```

> ⚠️ `desktop` profile **由 dsh 桌面端独占管理**，命令行装不进去 —— 请在桌面端的
> 「添加插件」对话框里粘贴上面任一条。装完确认 desktop profile 的 `cordis.patch.yml`
> 里有 `shio-bridge` 行（缺了插件**不会加载**而 dsh **不报错**）。

## 校验

- `hashes.txt` —— 全部产物的 SHA-256（另有 `hashes.txt.asc` GPG 签名）
- CLI 自检：`dsh-minato verify-install --url <本页 hashes.txt 地址>`

## 许可与致谢

- [MIT License](LICENSE)。**代码是 MIT 的，图标不是** —— 图标来源与许可见仓库 `docs/ASSETS.md`。
- [DeepSeek Harness (dsh)](https://www.npmjs.com/package/@deepseek-ai/dsh)
- 图标：**新 logo 为生成式 AI 产出（工具：Kimi）**，**旧 logo 为 ChatGPT（OpenAI）协助产出**；提示词均由本项目维护者编写
- **v1 脚本协助：SOGR-Momono Dango（QwenPaw / DeepseekAPI-V4-Flash-0731）**
- **v2 重写与打包：DeepSeek DSH（DSH / DeepseekAPI-V4-Flash-0731）**
- GitHub：[@sakanamaru](https://github.com/sakanamaru)