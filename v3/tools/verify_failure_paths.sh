#!/usr/bin/env bash
# ============================================================================
#  verify_failure_paths.sh —— 失败路径的可重复验证
#  ---------------------------------------------------------------------------
#  为什么有这个脚本：数据工具的可信度**不在于顺利时**，而在于**出错时**是否
#  诚实、是否破坏数据、是否留得下救回的路。这些结论本会话都在真机上验过，
#  但只留在会话记录里。这个脚本把它们固化成一条命令。
#
#  用法：  bash verify_failure_paths.sh /path/to/dsh-minato
#  退出码：0 = 全部通过；1 = 有失败
#
#  位置纪律：恢复目标放在 $HOME 的普通子目录（产品**接受**的位置 ✓）；
#            数据根与备份根放临时目录（它们不是工作区 ✓）。/tmp **被产品排除**
#            作为工作区 —— 恢复目标绝不能放那里。
# ============================================================================
set -u
CLI="${1:-}"
if [ -z "$CLI" ] || [ ! -x "$CLI" ]; then echo "用法: bash verify_failure_paths.sh /path/to/dsh-minato"; exit 2; fi
CLI=$(readlink -f "$CLI")
T="timeout 300"
pass=0; fail=0
ok(){ pass=$((pass+1)); echo "  [PASS] $1"; }
bad(){ fail=$((fail+1)); echo "  [FAIL] $1"; }

WORK=$(mktemp -d /tmp/vfp-XXXXXX)
BIN="$WORK/bin"; mkdir -p "$BIN"; cp "$CLI" "$BIN/dsh-minato"; chmod +x "$BIN/dsh-minato"; CLI="$BIN/dsh-minato"
BKROOT="$BIN/backup"   # 第一次备份必须显式给目录（D4 契约）→ 隔离备份根 ✓
cleanup(){ chmod -R u+rwX "$WORK" 2>/dev/null; rm -rf "$WORK" "$TG1" "$TG2" 2>/dev/null; }
TG1="$HOME/vfp-tg1-$$"; TG2="$HOME/vfp-tg2-$$"
trap cleanup EXIT

S="$WORK/src"; mkdir -p "$S/storages"; printf 'P1\n' > "$S/storages/p1.txt"; printf 'P2\n' > "$S/storages/p2.txt"
DSH_HOME="$S" $T $CLI backup --to "$BKROOT" >/dev/null 2>&1
P=$(DSH_HOME="$S" $T $CLI backup --to "$BKROOT" 2>&1 | tr -d '\r' | awk '/BACKUP_OK/{print $2}')
echo "== 失败路径验证 =="
echo "  CLI: $($CLI version 2>/dev/null || echo '(version 失败)')"
[ -n "$P" ] && ok "参照包已建（$(basename "$P") ✓）" || { bad "参照包未建 ✗"; echo "== 结果：PASS=$pass FAIL=$fail =="; exit 1; }

# ---- 1 只读目标：必须如实失败 + 不写入 + 告知锚点 ----
rm -rf "$TG1"; mkdir -p "$TG1/storages"; printf 'ORIG\n' > "$TG1/storages/keep.txt"
chmod 555 "$TG1" "$TG1/storages"
O=$(DSH_HOME="$TG1" $T $CLI restore --path "$P" --apply --yes 2>&1 | tr -d '\r')
echo "$O" | grep -qE '^RESTORE_FAIL' && ok "只读目标：如实失败 ✓" || bad "只读目标未如实失败 ✗"
# #40：断言原因是权限类（只断言 FAIL 会把"无效备份目录"也算通过）
echo "$O" | grep -qiE 'denied|Access|Permission|权限|拒绝' && ok "只读目标：原因是权限类 ✓✓" || bad "只读目标：原因不是权限类 ✗"
echo "$O" | grep -qE '^RESTORE_OK' && bad "只读目标竟报成功 ✗✗" || ok "只读目标：未谎报 ✓"
echo "$O" | grep -qE '^RESTORE_PRE_BACKUP ' && ok "只读目标：仍告知回滚锚点 ✓" || bad "只读目标：锚点未告知 ✗"
[ "$(cat "$TG1/storages/keep.txt" 2>/dev/null)" = "ORIG" ] && ok "只读目标：原有文件未被改 ✓" || bad "只读目标：原有文件被改 ✗"
[ -f "$TG1/storages/p1.txt" ] && bad "只读目标：竟写入了新文件 ✗✗" || ok "只读目标：未写入新文件 ✓"
chmod 755 "$TG1" "$TG1/storages"
O2=$(DSH_HOME="$TG1" $T $CLI restore --path "$P" --apply --yes 2>&1 | tr -d '\r')
echo "$O2" | grep -qE '^RESTORE_OK' && ok "改回可写后恢复成功 ✓（对照 ✓）" || bad "改回可写后仍失败 ✗"
[ -f "$TG1/storages/keep.txt" ] && ok "恢复后目标端独有文件仍在 ✓" || bad "目标端独有文件被删 ✗"

# ---- 2 目标文件不可写（模拟被占用）：必须如实失败 + 锚点可用 ----
rm -rf "$TG2"; mkdir -p "$TG2/storages"; printf 'LOCKED-ORIG\n' > "$TG2/storages/p1.txt"
chmod 444 "$TG2/storages/p1.txt"
O3=$(DSH_HOME="$TG2" $T $CLI restore --path "$P" --apply --yes 2>&1 | tr -d '\r')
echo "$O3" | grep -qE '^RESTORE_FAIL' && ok "目标文件只读：如实失败 ✓" || bad "目标文件只读未失败 ✗"
# #40：同上
echo "$O3" | grep -qiE 'denied|Access|Permission|权限|拒绝' && ok "目标文件只读：原因是权限类 ✓✓" || bad "目标文件只读：原因不是权限类 ✗"
PB=$(echo "$O3" | grep -oP '^RESTORE_PRE_BACKUP \K\S+' | head -1)
[ -n "$PB" ] && ok "目标文件只读：锚点被告知 ✓" || bad "目标文件只读：锚点未告知 ✗"
chmod 644 "$TG2/storages/p1.txt"
if [ -n "$PB" ]; then
  R=$(DSH_HOME="$TG2" $T $CLI restore --path "$PB" --apply --yes --force 2>&1 | tr -d '\r')
  echo "$R" | grep -qE '^RESTORE_OK' && ok "锚点可用（失败后可回滚 ✓✓）" || bad "锚点不可用 ✗✗"
  [ "$(cat "$TG2/storages/p1.txt" 2>/dev/null)" = "LOCKED-ORIG" ] && ok "回滚救回了原始内容 ✓✓" || bad "回滚未救回 ✗"
fi

# ---- 3 并发备份：**两个不同源** ✓✓ + 路径必须不同 + 内容不得互混 ----
# 为什么用不同源：同源时即使撞名也**看不出损坏**（两边内容一样 ✓）——
# 第 93 轮我就是用同源复现，结果掩盖了真问题 ✗；换不同源后第 2 轮就暴露 ✓✓
C1="$WORK/c1"; C2="$WORK/c2"; mkdir -p "$C1/storages" "$C2/storages"
for i in $(seq 1 200); do printf 'c1-%d\n' "$i" > "$C1/storages/one-f$i.txt"; done
for i in $(seq 1 200); do printf 'c2-%d\n' "$i" > "$C2/storages/two-f$i.txt"; done
( DSH_HOME="$C1" $T $CLI backup --to "$BKROOT" > "$WORK/c-a.log" 2>&1 ) & PA=$!
( DSH_HOME="$C2" $T $CLI backup --to "$BKROOT" > "$WORK/c-b.log" 2>&1 ) & PB2=$!
wait $PA; wait $PB2
CA=$(tr -d '\r' < "$WORK/c-a.log" | awk '/BACKUP_OK/{print $2}')
CB=$(tr -d '\r' < "$WORK/c-b.log" | awk '/BACKUP_OK/{print $2}')
[ -n "$CA" ] && [ -n "$CB" ] && ok "并发备份：两个都成功 ✓" || bad "并发备份：有失败 ✗"
[ "$CA" != "$CB" ] && ok "并发备份：包路径不同（未互相覆盖 ✓）" || bad "并发备份：路径相同 ✗✗"
NA=$(find "$CA" -type f 2>/dev/null | wc -l); NB=$(find "$CB" -type f 2>/dev/null | wc -l)
[ "$NA" = "200" ] && [ "$NB" = "200" ] && ok "并发备份：两个都完整（各 200 ✓✓）" || bad "并发备份：不完整（$NA / $NB）✗✗"
# **内容不得互混** ✓✓（这才是"不同源"的价值所在 —— 撞名时两边内容会混进同一个包 ✗）
MIX=0
[ -n "$CA" ] && { [ -f "$CA/storages/two-f1.txt" ] && MIX=$((MIX+1)); [ -f "$CA/storages/one-f1.txt" ] || MIX=$((MIX+1)); }
[ -n "$CB" ] && { [ -f "$CB/storages/one-f1.txt" ] && MIX=$((MIX+1)); [ -f "$CB/storages/two-f1.txt" ] || MIX=$((MIX+1)); }
[ "$MIX" -eq 0 ] && ok "并发备份：**两个包内容互不混入** ✓✓" || bad "并发备份：**内容互混** $MIX 处 ✗✗"
VC=$(DSH_HOME="$C1" $T $CLI backup-list --verify 2>&1 | tr -d '\r')
echo "$VC" | grep -qE '^BACKUP_VERIFY .*mismatch' && bad "并发后出现 mismatch ✗✗" || ok "并发后无 mismatch ✓"

# ---- 4 备份进行中发起恢复：两者都必须成功（含时序证明 ✓）----
R1="$WORK/r1"; mkdir -p "$R1/storages"
for i in $(seq 1 2000); do printf 'r%d\n' "$i" > "$R1/storages/g$i.txt"; done
DSH_HOME="$R1" $T $CLI backup --to "$BKROOT" >/dev/null 2>&1
RP=$(DSH_HOME="$R1" $T $CLI backup --to "$BKROOT" 2>&1 | tr -d '\r' | awk '/BACKUP_OK/{print $2}')
RT="$HOME/vfp-rt-$$"; rm -rf "$RT"; mkdir -p "$RT"
S1=$(date +%s%N)
( DSH_HOME="$R1" $T $CLI backup --to "$BKROOT" > "$WORK/r-b.log" 2>&1 ) & PR=$!
sleep 0.15
S2=$(date +%s%N)
( cd "$RT" && DSH_HOME="$WORK/r-data" $T $CLI restore --path "$RP" --apply --yes > "$WORK/r-r.log" 2>&1 )
E2=$(date +%s%N)
wait $PR; E1=$(date +%s%N)
if [ "$E2" -lt "$E1" ]; then ok "并发证明：恢复在备份结束前完成（两者同时在飞 ✓✓）"; else bad "并发证明不成立（未真正并发 ✗）"; fi
grep -q 'BACKUP_OK' "$WORK/r-b.log" && ok "并发期间备份成功 ✓" || bad "并发期间备份失败 ✗"
grep -qE '^RESTORE_OK' "$WORK/r-r.log" && ok "备份进行中恢复成功 ✓" || bad "备份进行中恢复失败 ✗"
RN=$(find "$WORK/r-b.log" >/dev/null 2>&1; tr -d '\r' < "$WORK/r-b.log" | awk '/BACKUP_OK/{print $2}')
[ "$(find "$RN" -type f 2>/dev/null | wc -l)" = "2000" ] && ok "并发期间备份仍完整（2000 ✓✓）" || bad "并发期间备份不完整 ✗"
rm -rf "$RT"

echo ""
echo "== 结果：PASS=$pass FAIL=$fail =="
# #43：**项数下限**断言 ✓✓ —— 否则"删掉一条检查"脚本仍报 PASS 但项数变少 ✗（与 chain 脚本同一做法 ✓）
EXPECTED_MIN=23
echo "== 结果：PASS=$pass FAIL=$fail（声明最少 $EXPECTED_MIN 项）=="
if [ "$pass" -lt "$EXPECTED_MIN" ]; then
  echo "  [FAIL] 项数不足：实跑 $pass < 声明 $EXPECTED_MIN（有检查被删掉或没执行到）"
  fail=$((fail+1))
fi
[ "$fail" -eq 0 ] && exit 0 || exit 1