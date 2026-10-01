<p align="center">
  <img src="https://raw.githubusercontent.com/sakanamaru/dsh-minato/main/logo.png" alt="dsh-minato" width="180">
</p>

<h3 align="center">dsh-minato · 3.0.0-preview.1</h3>
<p align="center">社区版 DeepSeek Harness (dsh) 本机部署运维套件<br>
安装 · 启动 · 监控 · 备份恢复 · 插件诊断与隔离</p>

---

> ⚠️ **非官方工具，与 DeepSeek 官方无关。** 只读写本机；会联网的只有 `check` / `update-info` /
> `update-center` / `doctor` / `install` / `update` / `verify-install --url`，其余命令不联网。

## 这个预览版里有什么

- **V3 跨平台 CLI**（Windows / Linux 同一套命令面，标记行与 v2.x 逐字一致）
- **V3 图形界面**（Avalonia，Windows / Linux 通用）：概览 · 看板 · 会话与 Token · 形态与插件 · 备份 · 体检 · 设置 · 更新 · 日志
- **备份/恢复引擎**：完成标记 + 内容哈希 + 恢复前自动锚点（可回滚）+ 截断包拒收
- 安装器带 **SHA-256 清单**与自校验；发布物附 `hashes.txt`

## 🧩 桥接插件（可选）—— 它解决什么问题

工具箱本体是**独立进程**：不注入 dsh，**dsh 没装也能用**。但有一个事实**只有 dsh 进程自己知道**：

> **当前有几个会话正在运行**（以及每个会话的实时 token / 上下文压力）。

这是**进程内的运行态**，dsh **不落盘** —— 所以磁盘投影里没有它，工具箱只能显示 `unknown`。

装上这个**只读桥接插件**后，工具箱就能显示真实的「运行中」：

| | 不装插件 | 装了插件 |
|---|---|---|
| 会话清单 / token / 命中率 / 速度 | ✅（读磁盘投影） | ✅（读磁盘投影） |
| **「运行中」标记** | ❌ `unknown` | ✅ **实时** |

**它不做什么**（硬约束）：❌ 不发模型请求（不消耗 token）· ❌ 不写 dsh 状态 · ❌ 不读会话正文 ·
❌ 不联网 · ❌ 不阻塞（全 try/catch，**插件坏了不能影响 dsh**）。

装它只为了这一个事实 —— 装不装**由你决定**，本工具不替你决定，也不假装它必需。

## 安装

**工具箱本体**：下载本页对应平台的产物（`dsh-minato-*-win-x64-setup.exe` / `dsh-minato-linux-x64.tar.gz`），
用附带的 `hashes.txt` 校验 SHA-256 后再运行。

**桥接插件**（可选，装完**重启 dsh** 生效）：

```bash
# 从本地目录（最稳，不依赖网络）
dsh plugin --profile web add "<本仓库路径>/plugin/dsh-minato-bridge"

# 或从仓库（注意要带子目录，只填仓库根不行 —— 根目录没有 package.json）
dsh plugin --profile web add "github:sakanamaru/dsh-minato#path:plugin/dsh-minato-bridge"
```

> ⚠️ `desktop` profile **由 dsh 桌面端独占管理**，命令行装不进去 —— 请在桌面端的
> 「添加插件」对话框里粘贴上面任一条路径。装完还要确认 desktop profile 的
> `cordis.patch.yml` 里有 `shio-bridge` 行（缺了插件**不会加载**而 dsh **不报错**）。

## 校验

- `hashes.txt` —— 全部产物的 SHA-256（另有 `hashes.txt.asc` GPG 签名）
- CLI 自检：`dsh-minato verify-install --url <本页 hashes.txt 地址>`

## 许可

MIT。图标与鲸鱼娘形象见仓库 `ASSETS.md`。