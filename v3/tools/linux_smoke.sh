#!/usr/bin/env bash
# dsh-minato Linux 冒烟测试
# 纪律：只读命令用真实数据；写命令一律在**隔离根**里跑且**不带 --yes**（只打印计划）；
#       绝不触碰默认端口 3080 的实例；所有可能等待输入的地方都加 timeout 防挂。
set -u
BIN="${1:-./dsh-minato}"
ISO="$(mktemp -d /tmp/dsht-smoke.XXXXXX)"
T="timeout 20"
echo "===== dsh-minato Linux 冒烟 ====="
echo "--- 0 环境"; uname -a; echo "DSH_HOME=${DSH_HOME:-<未设置，用真实数据根>}"; ls -l "$BIN" | awk '{print $5, $9}'
echo "--- 1 about"; $T "$BIN" about
echo "--- 2 version"; $T "$BIN" version
echo "--- 3 status（真实数据，只读）"; $T "$BIN" status
echo "--- 4 doctor（只读）"; $T "$BIN" doctor 2>&1 | head -24
echo "--- 5 sessions（只读，前 3 行）"; $T "$BIN" sessions 2>&1 | head -3
echo "--- 6 profiles（只读，前 8 行）"; $T "$BIN" profiles 2>&1 | head -8
echo "--- 7 隔离根：backup"; DSH_HOME="$ISO" $T "$BIN" backup
echo "--- 8 隔离根：backup-list"; DSH_HOME="$ISO" $T "$BIN" backup-list
echo "--- 9 隔离根：restore 预览"; DSH_HOME="$ISO" $T "$BIN" restore --dry-run
echo "--- 10 隔离根：profilepatch 计划（不写盘）"; DSH_HOME="$ISO" $T "$BIN" profilepatch --profile web --id demo
echo "--- 11 安全：start 计划（不带 --yes，绝不启动）"; DSH_HOME="$ISO" $T "$BIN" start --port 3999
echo "--- 12 安全：stop 计划（不带 --yes，绝不停止）"; DSH_HOME="$ISO" $T "$BIN" stop --port 3999
echo "--- 13 shortcut 计划（不带 --yes）"; DSH_HOME="$ISO" $T "$BIN" shortcut
echo "--- 14 菜单（管道输入，EOF 即退出）"; printf '4\nq\n' | $T "$BIN" 2>&1 | head -8
echo "--- 15 默认端口是否仍被你的实例占用（应为监听中）"; (ss -ltnp 2>/dev/null || netstat -ltnp 2>/dev/null) | grep -E ':3080' || echo "3080 未监听"
rm -rf "$ISO"
echo "===== 完成：请把以上输出整段贴回 ====="