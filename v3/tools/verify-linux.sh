#!/usr/bin/env bash
# verify-linux.sh -- verify a dsh-minato Linux release asset set.
#
# Why this exists: verify.ps1 is the project's release verifier, but it is Windows-only (it hardcodes
# Git-for-Windows bash and .exe names), and it does not know about Linux artifacts at all -- a Linux
# tarball would be silently ignored rather than failing verification. verify.ps1 is also locked by a
# switchover invariant, so the Linux side gets its own verifier instead of editing that one.
#
# Usage:  ./verify-linux.sh <dsh-minato-linux-x64.tar.gz> [<tarball.sha256>]
# Exit:   0 = verified, 1 = verification failed, 2 = usage/setup problem
#
# Checks, in order:
#   1. the .sha256 file matches the tarball (when given)
#   2. the tarball lists exactly the expected payload (no Windows executables smuggled in)
#   3. the CLI binary inside is executable and answers "version" with a DSHT_VERSION marker
#   4. per-file SHA-256 for the shipped binaries, printed for the release notes
#
# Safety: extraction happens into a private temp directory, the CLI is only asked for read-only commands,
# and no test ever touches the default data root or the default port.

set -u

TARBALL="${1:-}"
SHAFILE="${2:-}"

if [ -z "$TARBALL" ]; then
    echo "usage: verify-linux.sh <dsh-minato-linux-x64.tar.gz> [<tarball.sha256>]" >&2
    exit 2
fi
if [ ! -f "$TARBALL" ]; then
    echo "FAIL: tarball not found: $TARBALL" >&2
    exit 1
fi

fail() { echo "FAIL: $*" >&2; exit 1; }
ok()   { echo "  [ok] $*"; }

echo "== dsh-minato Linux release verification =="
echo "tarball: $TARBALL ($(wc -c < "$TARBALL") bytes)"

# ---- 1. checksum ----------------------------------------------------------
if [ -n "$SHAFILE" ]; then
    [ -f "$SHAFILE" ] || fail "sha256 file not found: $SHAFILE"
    EXPECTED=$(awk '{print $1}' "$SHAFILE" | head -1)
    ACTUAL=$(sha256sum "$TARBALL" | awk '{print $1}')
    [ -n "$EXPECTED" ] || fail "could not read a hash from $SHAFILE"
    if [ "$EXPECTED" != "$ACTUAL" ]; then
        fail "sha256 mismatch: manifest=$EXPECTED actual=$ACTUAL"
    fi
    ok "sha256 matches the manifest ($ACTUAL)"
else
    echo "  [--] no .sha256 given, skipping the checksum check"
fi

# ---- 2. payload ----------------------------------------------------------
WORK=$(mktemp -d /tmp/dsht-verify.XXXXXX) || fail "cannot create a temp directory"
cleanup() { rm -rf "$WORK"; }
trap cleanup EXIT INT TERM

tar -xzf "$TARBALL" -C "$WORK" || fail "tarball does not extract"
ROOT=$(find "$WORK" -maxdepth 1 -mindepth 1 -type d | head -1)
[ -n "$ROOT" ] || fail "tarball has no top-level directory"

for want in dsh-minato gui/dsht-gui dsh-minato.desktop linux_smoke.sh; do
    [ -e "$ROOT/$want" ] || fail "expected payload missing: $want"
done
ok "payload present: dsh-minato, gui/dsht-gui, dsh-minato.desktop, linux_smoke.sh"

# A Linux package must not carry Windows executables (the Windows build ships those separately).
if find "$ROOT" -name '*.exe' -o -name '*.lnk' | grep -q .; then
    find "$ROOT" \( -name '*.exe' -o -name '*.lnk' \) | sed 's/^/    unexpected: /'
    fail "Linux package contains Windows-only artifacts"
fi
ok "no .exe/.lnk smuggled into the Linux package"

# The GUI publish also drops a small framework-dependent variant next to the CLI; that is expected.
[ -d "$ROOT/gui" ] || fail "gui directory missing"

# ---- 3. the binary actually runs ----------------------------------------
CLI="$ROOT/dsh-minato"
[ -x "$CLI" ] || { chmod +x "$CLI" 2>/dev/null || true; }
[ -x "$CLI" ] || fail "dsh-minato is not executable"
# ★★★ **CI 红修复（2026-10-01）—— 不要把 stderr 混进被断言的内容** ✓✓
#   ✗ 原来 `... version 2>&1 | head -1` ✗ —— 而 CLI 会往 **stderr** 打一条**正常诊断**
#     （`INTEGRITY_SKIPPED 旁无 hashes.txt …`：源码编译/单独复制的 exe 旁没有清单 ✓ 完全正常 ✓）
#     → **`$VER` 以 `INTEGRITY_SKIPPED` 开头** ✗ → `case DSHT_VERSION*` 不匹配 → **误报失败** ✗✗
#   · 这与当初 `compare_markers` 的假 FAIL 是**同一个根因** ✓（`2>&1` 把诊断混进比对/断言 ✓）
#   ✓ 现在：**只看 stdout** ✓✓（诊断照旧打到终端 ✓ 可见 ✓ 但不参与断言 ✓）
VER=$(timeout 60 "$CLI" version 2>/dev/null | head -1)
case "$VER" in
    DSHT_VERSION*) ok "binary runs: $VER" ;;
    *) fail "binary did not answer 'version' with a DSHT_VERSION marker (got: ${VER:-<empty>})" ;;
esac
# ★★★ **CI 红修复（2026-10-01，第二处）** ✓✓
#   ✗ 原来只检查 `about` 的**第一行**是不是 `dsh-minato*` ✗
#     → 而 `about` 现在**第一行是 CREDITS**（鲸鱼娘/生成式图标/非官方声明 ✓ 有意的 ✓）
#     → 产品名在后面 ✓ → **断言误报失败** ✗✗
#   ✓ 现在：**在整份输出里找产品名** ✓✓（只看事实"产品名出现过" ✓ 不绑行号 ✓）
ABOUT=$(timeout 60 "$CLI" about 2>/dev/null)
case "$ABOUT" in
    *dsh-minato*) ok "about works: $(echo "$ABOUT" | head -1)" ;;
    *) fail "about did not print the product name (got: $(echo "$ABOUT" | head -1))" ;;
esac

# ---- 4. hashes for the release notes ------------------------------------
echo "  per-file SHA-256:"
( cd "$ROOT" && sha256sum dsh-minato gui/dsht-gui 2>/dev/null | sed 's/^/    /' )

echo "== RESULT: LINUX RELEASE VERIFIED =="
exit 0
