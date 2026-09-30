#!/bin/sh
# dsh-minato Linux 安装脚本
#
# 设计原则（与 Windows 安装器**完全一致** ✓）：
#   · **纯 POSIX sh** ✓ 零第三方依赖 ✓ 不需要 sudo ✓（装到用户目录 ✓）
#   · **绝不动用户数据** ✓ —— ~/.dsh 是 dsh 自己的 ✓ 本脚本只读它的位置并在卸载时告知 ✓
#   · **幂等** ✓ —— 重复运行不会重复添加、不会报错 ✓
#   · **逐项报告** ✓ —— 每一步都打印做了什么 ✓ 不静默 ✓
#   · **可回退** ✓ —— --uninstall 只删自己加的东西 ✓ 逐项列出 ✓
#
# 用法：
#   ./install.sh              安装（解压到 ~/.local/share/dsh-minato，链接到 ~/.local/bin）
#   ./install.sh --uninstall  卸载（**默认不删数据** ✓ 会在桌面留一份说明）
#   ./install.sh --prefix DIR 自定义安装位置
#   ./install.sh --no-desktop 不写 .desktop 菜单项
#
# 环境变量（可选）：
#   DSH_MINATO_PREFIX   安装位置（默认 ~/.local/share/dsh-minato）
#   DSH_MINATO_BINDIR   命令链接位置（默认 ~/.local/bin）

set -eu

APP="dsh-minato"
SRC_DIR=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
PREFIX="${DSH_MINATO_PREFIX:-$HOME/.local/share/$APP}"
BINDIR="${DSH_MINATO_BINDIR:-$HOME/.local/bin}"
DESKTOP_DIR="$HOME/.local/share/applications"
NOTE="$HOME/Desktop/$APP-卸载说明.txt"
[ -d "$HOME/Desktop" ] || NOTE="$HOME/${APP}-卸载说明.txt"
WRITE_DESKTOP=1
DO_UNINSTALL=0

# ---- 参数 ----
while [ $# -gt 0 ]; do
    case "$1" in
        --uninstall) DO_UNINSTALL=1 ;;
        --no-desktop) WRITE_DESKTOP=0 ;;
        --prefix) shift; [ $# -gt 0 ] || { echo "错误：--prefix 后面要跟目录" >&2; exit 2; }; PREFIX="$1" ;;
        --help|-h)
            sed -n '2,30p' "$0" | sed 's/^# \{0,1\}//'
            exit 0 ;;
        *) echo "未知参数：$1（用 --help 看用法）" >&2; exit 2 ;;
    esac
    shift
done

say() { printf '%s\n' "$*"; }
ok()  { printf '  ✓ %s\n' "$*"; }
warn(){ printf '  ! %s\n' "$*"; }
die() { printf '  ✗ %s\n' "$*" >&2; exit 1; }

# ================================================================ 卸载

if [ "$DO_UNINSTALL" -eq 1 ]; then
    say "== 卸载 $APP =="
    # **安全闸**：只有"看起来是本工具的安装目录"才允许删 ✓（与 Windows 安装器同一道闸 ✓）
    if [ ! -f "$PREFIX/$APP" ] && [ ! -f "$PREFIX/gui/dsht-gui" ]; then
        die "拒绝卸载：$PREFIX 里没有 $APP 的文件 → 它不像安装目录 ✓ **一个字节都不删** ✓"
    fi
    if [ -L "$BINDIR/$APP" ]; then rm -f "$BINDIR/$APP"; ok "已删命令链接 $BINDIR/$APP"; else warn "$BINDIR/$APP 不存在（跳过）"; fi
    if [ -f "$DESKTOP_DIR/$APP.desktop" ]; then rm -f "$DESKTOP_DIR/$APP.desktop"; ok "已删菜单项"; else warn "菜单项不存在（跳过）"; fi
    if [ -d "$PREFIX" ]; then
        # 先改名再删 ✓（有进程在跑时也能改名成功 ✓ 与 Windows 侧同一手法 ✓）
        MOVED="$PREFIX.removing.$$"
        if mv "$PREFIX" "$MOVED" 2>/dev/null; then rm -rf "$MOVED" 2>/dev/null || true; ok "已删安装目录"; else rm -rf "$PREFIX" 2>/dev/null || warn "有文件被占用，没能全删：$PREFIX"; fi
    fi
    command -v update-desktop-database >/dev/null 2>&1 && update-desktop-database "$DESKTOP_DIR" 2>/dev/null || true
    # **不删数据** ✓ 留一份说明 ✓
    DATA_DIR="$HOME/.dsh"
    {
        echo "$APP 已卸载。"
        echo ""
        echo "**你的数据没有被删除。**"
        echo ""
        echo "DeepSeek Harness 的会话、设置等数据在："
        echo "    $DATA_DIR"
        if [ -d "$DATA_DIR" ]; then echo "（这个目录现在还在 ✓）"; else echo "（这个目录当前不存在）"; fi
        echo ""
        echo "这些数据**不是 $APP 创建的** ✓ 是 DeepSeek Harness（dsh）自己的 ✓"
        echo "所以卸载 $APP **不会**、也**不应该**动它 ✓"
        echo ""
        echo "如果你确实想删掉这些数据："
        echo "  1. 先确认你不再需要这些会话记录（**删了无法恢复**）"
        echo "  2. 确认 dsh 本身也已卸载（否则它会重新生成）"
        echo "  3. 手动删除上面那个目录"
    } > "$NOTE" 2>/dev/null && ok "已写说明文档：$NOTE" || warn "写说明文档失败（不致命）"
    say "卸载完成 ✓（**你的数据没有被删除** ✓）"
    exit 0
fi

# ================================================================ 安装

say "== 安装 $APP =="
say "  源目录: $SRC_DIR"
say "  安装到: $PREFIX"
say "  命令链接: $BINDIR/$APP"

# ---- 前置检查 ✓（失败要说清，不静默 ✓）----
[ -f "$SRC_DIR/$APP" ] || die "找不到 $SRC_DIR/$APP —— 请在**解压后的包目录里**运行本脚本 ✓"
[ -x "$SRC_DIR/$APP" ] || { chmod +x "$SRC_DIR/$APP" 2>/dev/null || true; }
[ -f "$SRC_DIR/gui/dsht-gui" ] || warn "包内没有 gui/dsht-gui（图形界面将不可用，CLI 仍可用）"

# ---- 复制（用暂存 + 改名 ✓ 与 Windows 侧同一套原子性做法 ✓）----
mkdir -p "$(dirname -- "$PREFIX")" || die "建不了 $PREFIX 的父目录"
STAGING="$(dirname -- "$PREFIX")/.$APP.staging.$$"
rm -rf "$STAGING" 2>/dev/null || true
mkdir -p "$STAGING" || die "建不了暂存目录"
say "  复制到暂存目录…"
# 排除脚本自己与常见垃圾 ✓
( cd "$SRC_DIR" && tar cf - --exclude='*.tar.gz' --exclude='*.sha256' . ) | ( cd "$STAGING" && tar xf - ) \
    || die "复制失败（tar 出错）"
ok "复制完成（$(find "$STAGING" -type f 2>/dev/null | wc -l | tr -d ' ') 个文件）"

chmod +x "$STAGING/$APP" 2>/dev/null || true
[ -f "$STAGING/gui/dsht-gui" ] && chmod +x "$STAGING/gui/dsht-gui" 2>/dev/null || true

# ---- 就位：先改名旧的，再改名新的 ✓ ----
if [ -d "$PREFIX" ]; then
    OLD="$PREFIX.old.$$"
    if mv "$PREFIX" "$OLD" 2>/dev/null; then rm -rf "$OLD" 2>/dev/null || true; ok "已替换旧版本"; else rm -rf "$PREFIX" 2>/dev/null || true; fi
fi
mv "$STAGING" "$PREFIX" || die "就位失败（暂存目录留在 $STAGING，可以手动看）"
ok "已安装到 $PREFIX"

# ---- 命令链接 ✓（放 bin/ ✓ 不污染 PATH 的其它位置 ✓）----
mkdir -p "$BINDIR" || die "建不了 $BINDIR"
ln -sf "$PREFIX/$APP" "$BINDIR/$APP"
ok "已链接 $BINDIR/$APP"
case ":$PATH:" in
    *":$BINDIR:"*) : ;;
    *) warn "$BINDIR 不在 PATH 里 —— 加一行到 ~/.profile 即可：export PATH=\"\$HOME/.local/bin:\$PATH\"" ;;
esac

# ---- 菜单项 ✓ ----
if [ "$WRITE_DESKTOP" -eq 1 ]; then
    mkdir -p "$DESKTOP_DIR"
    ICON="$PREFIX/icons/$APP.png"
    [ -f "$ICON" ] || ICON="$PREFIX/$APP"
    cat > "$DESKTOP_DIR/$APP.desktop" <<EOF
[Desktop Entry]
Type=Application
Name=$APP
Comment=DeepSeek Harness 的非官方工具箱（只读本地状态，不联网上传）
Exec=$PREFIX/gui/dsht-gui
Icon=$ICON
Terminal=false
Categories=Utility;
EOF
    command -v update-desktop-database >/dev/null 2>&1 && update-desktop-database "$DESKTOP_DIR" 2>/dev/null || true
    ok "已写菜单项 $DESKTOP_DIR/$APP.desktop"
fi

# ---- 指纹自检 ✓（与 Windows 侧同一机制 ✓）----
if [ -f "$PREFIX/hashes.txt" ]; then
    say "  自检安装后的文件指纹…"
    if command -v sha256sum >/dev/null 2>&1; then
        BAD=0
        while read -r want name; do
            case "$want" in ''|\#*) continue ;; esac
            f="$PREFIX/$name"
            [ -f "$f" ] || f="$PREFIX/gui/$name"
            [ -f "$f" ] || continue
            got=$(sha256sum "$f" | cut -d' ' -f1)
            [ "$got" = "$want" ] || { warn "指纹不符：$name"; BAD=$((BAD+1)); }
        done < "$PREFIX/hashes.txt"
        [ "$BAD" -eq 0 ] && ok "指纹全部一致 ✓" || warn "有 $BAD 个文件指纹不符 ✗（安装包可能被改动过）"
    else
        warn "没有 sha256sum，跳过指纹自检"
    fi
fi

# ---- 结束提示 ✓ ----
say ""
say "安装完成 ✓"
say "  · 启动图形界面：$BINDIR/$APP gui   或   $PREFIX/gui/dsht-gui"
say "  · 命令行：$BINDIR/$APP --help"
say "  · 卸载：$PREFIX/install.sh --uninstall   或   $SRC_DIR/install.sh --uninstall"
say "  · **本工具只读你的数据** ✓ 卸载也**不会删** ~/.dsh ✓"
say "  · 非官方工具，与 DeepSeek 官方无关 ✓（许可见 ASSETS.md，隐私见 PRIVACY.md）"
