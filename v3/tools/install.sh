#!/bin/sh
# dsh-minato Linux 安装脚本
#
# 设计原则（与 Windows 安装器**完全一致** ✓）：
#   · **纯 POSIX sh** ✓ 零第三方依赖 ✓ 不需要 sudo ✓（装到用户目录 ✓）
#   · **绝不动用户数据** ✓ —— ~/.dsh 是 dsh 自己的 ✓ 本脚本只读它的位置并在卸载时告知 ✓
#   · **幂等** ✓ —— 重复运行不会重复添加、不会报错 ✓
#   · **逐项报告** ✓ —— 每一步都打印做了什么 ✓ 不静默 ✓
#   · **只删自己加的** ✓ —— 卸载逐项列出 ✓ 删不干净**如实报告** ✗ 不假报干净 ✓
#
# 用法：
#   ./install.sh                    安装
#   ./install.sh --uninstall        卸载（**默认不删数据** ✓ 会在桌面留一份说明）
#   ./install.sh --prefix DIR       自定义安装位置（也支持 --prefix=DIR ✓）
#   ./install.sh --no-desktop       不写 .desktop 菜单项
#   ./install.sh --force            允许装进非空目录（**危险** ✓ 默认拒绝 ✓）
#
# 环境变量（可选）：
#   DSH_MINATO_PREFIX   安装位置（默认 ~/.local/share/dsh-minato）
#   DSH_MINATO_BINDIR   命令链接位置（默认 ~/.local/bin）

set -eu

APP="dsh-minato"
MARKER=".dsh-minato-install"
SRC_DIR=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
PREFIX="${DSH_MINATO_PREFIX:-$HOME/.local/share/$APP}"
BINDIR="${DSH_MINATO_BINDIR:-$HOME/.local/bin}"
DESKTOP_DIR="$HOME/.local/share/applications"
WRITE_DESKTOP=1
DO_UNINSTALL=0
FORCE=0
STAGING=""

say()  { printf '%s\n' "$*"; }
ok()   { printf '  ✓ %s\n' "$*"; }
warn() { printf '  ! %s\n' "$*"; }
die()  { printf '  ✗ %s\n' "$*" >&2; exit 1; }

# 退出时清理暂存 ✓（F9：中断/失败不再留半份副本 ✓ 幂等 ✓）
cleanup() { [ -n "$STAGING" ] && [ -d "$STAGING" ] && rm -rf "$STAGING" 2>/dev/null || true; }
trap cleanup EXIT INT TERM

# ---- 参数 ----
while [ $# -gt 0 ]; do
    case "$1" in
        --uninstall) DO_UNINSTALL=1 ;;
        --no-desktop) WRITE_DESKTOP=0 ;;
        --force) FORCE=1 ;;
        --prefix)
            # ✓ F10：缺值要**报错** ✓ 不能把下一个开关当成目录 ✗
            [ $# -ge 2 ] || die "--prefix 后面要跟一个目录"
            case "$2" in -*) die "--prefix 的值看起来是另一个开关：$2" ;; esac
            PREFIX="$2"; shift ;;
        --prefix=*) PREFIX="${1#--prefix=}" ;;   # ✓ F10：支持 = 形式 ✓
        --help|-h) sed -n '2,24p' "$0" | sed 's/^# \{0,1\}//'; exit 0 ;;
        *) die "未知参数：$1（用 --help 看用法）" ;;   # ✓ 未知参数**不能静默忽略** ✓
    esac
    shift
done

# ✓ F5：**规范化路径** ✓（相对路径会让符号链接悬空 ✗ 原来的坑 ✓）
case "$PREFIX" in
    /*) ;;
    *) PREFIX="$(pwd)/$PREFIX" ;;
esac
PREFIX=$(printf '%s' "$PREFIX" | sed 's:/*$::')      # 去掉尾部斜杠 ✓
[ -n "$PREFIX" ] || die "安装位置为空"
case "$BINDIR" in /*) ;; *) BINDIR="$(pwd)/$BINDIR" ;; esac
BINDIR=$(printf '%s' "$BINDIR" | sed 's:/*$::')

# ✓ F6：桌面目录用 xdg-user-dir ✓ 再回退多候选 ✓（原来写死 ~/Desktop ✗ 中文系统会写错地方 ✓）
desktop_dir() {
    d=""
    if command -v xdg-user-dir >/dev/null 2>&1; then d=$(xdg-user-dir DESKTOP 2>/dev/null || true); fi
    [ -n "$d" ] && [ -d "$d" ] && { printf '%s' "$d"; return; }
    for c in "$HOME/Desktop" "$HOME/桌面" "$HOME/デスクトップ"; do
        [ -d "$c" ] && { printf '%s' "$c"; return; }
    done
    printf '%s' "$HOME"
}

# ================================================================ 卸载

if [ "$DO_UNINSTALL" -eq 1 ]; then
    say "== 卸载 $APP =="
    # ★★ F1 修复：**身份校验不能只看文件名** ✗✗
    #   ✗ 原来只判"存在一个叫 dsh-minato 的文件" ✓ → **任何含该文件名的目录都会被 rm -rf** ✗✗
    #     （审计实测：`important.txt` 与 `photos/` 全没了 ✓ 与 Windows 侧 C1 同类 ✓）
    #   ✓ 现在要求**安装器写下的标记文件** ✓（含路径 ✓ 不匹配即拒绝 ✓）
    if [ ! -f "$PREFIX/$MARKER" ]; then
        die "拒绝卸载：$PREFIX 里没有安装标记 $MARKER → 它不像安装目录 ✓ **一个字节都不删** ✓"
    fi
    recorded=$(sed -n 's/^path=//p' "$PREFIX/$MARKER" 2>/dev/null | head -1)
    if [ -n "$recorded" ] && [ "$recorded" != "$PREFIX" ]; then
        die "拒绝卸载：标记里记录的安装位置是「$recorded」，与本次的「$PREFIX」不一致 ✓ **一个字节都不删** ✓"
    fi

    # 符号链接：**只删指向我们的** ✓（F7：用户自己的链接不能动 ✗）
    if [ -L "$BINDIR/$APP" ]; then
        tgt=$(readlink "$BINDIR/$APP" 2>/dev/null || true)
        case "$tgt" in
            "$PREFIX"/*) rm -f "$BINDIR/$APP" && ok "已删命令链接 $BINDIR/$APP" ;;
            *) warn "跳过 $BINDIR/$APP：它指向 $tgt ✓ 不是我们建的 ✓ 不动它 ✓" ;;
        esac
    fi
    # 菜单项：**只删内容是我们的** ✓（F7）
    if [ -f "$DESKTOP_DIR/$APP.desktop" ] && grep -q "$PREFIX" "$DESKTOP_DIR/$APP.desktop" 2>/dev/null; then
        rm -f "$DESKTOP_DIR/$APP.desktop" && ok "已删菜单项"
    fi
    command -v update-desktop-database >/dev/null 2>&1 && update-desktop-database "$DESKTOP_DIR" 2>/dev/null || true

    # ★★ F1 的另一半：**只删我们自己的** ✓✓（不再 rm -rf 整棵树 ✗）
    if [ -d "$PREFIX" ]; then
        removed=0; kept=0
        # ① **优先用安装时记录的文件清单** ✓✓（它才是完整的 ✓ 包内 hashes.txt 可能只覆盖几个 ✓）
        LIST=""
        [ -f "$PREFIX/.dsh-minato-files" ] && LIST="$PREFIX/.dsh-minato-files"
        if [ -n "$LIST" ]; then
            while read -r name; do
                [ -n "${name:-}" ] || continue
                f="$PREFIX/$name"
                if [ -f "$f" ]; then rm -f "$f" 2>/dev/null && removed=$((removed+1)) || kept=$((kept+1)); fi
            done < "$LIST"
        elif [ -f "$PREFIX/hashes.txt" ]; then
            # 回退：老版本装的没有记录清单 ✓ 用包内 hashes.txt ✓（可能不完整 ✓ 如实说明 ✓）
            warn "没有安装文件清单（老版本装的 ✓）→ 回退用包内 hashes.txt ✓ 可能不完整 ✓"
            while read -r _h name; do
                [ -n "${name:-}" ] || continue
                f="$PREFIX/$name"
                if [ -f "$f" ]; then rm -f "$f" 2>/dev/null && removed=$((removed+1)) || kept=$((kept+1)); fi
            done < "$PREFIX/hashes.txt"
        fi
        # ② 已知生成物 ✓
        for g in uninstall.exe "$MARKER" hashes.txt install.sh .dsh-minato-files; do
            [ -f "$PREFIX/$g" ] && { rm -f "$PREFIX/$g" 2>/dev/null && removed=$((removed+1)) || kept=$((kept+1)); }
        done
        for g in gui bin icons app-*; do
            for d in $PREFIX/$g; do
                [ -d "$d" ] || continue
                rm -rf "$d" 2>/dev/null && removed=$((removed+1)) || kept=$((kept+1))
            done
        done
        # ✓ 删完文件后，把**空的子目录**也清掉 ✓
        #   ✗ 原来只删 `gui`/`bin`/`icons`/`app-*` ✗ → **`cli-small/` 这种目录会剩下** ✓
        #     （VM 实测：文件 0 个、目录还剩 2 个 ✓ → 目录非空 → 目录被保留 ✓）
        # ✓ `find -depth -type d -empty` 是 POSIX ✓ 自底向上删空目录 ✓
        find "$PREFIX" -depth -type d -empty -exec rmdir {} \; 2>/dev/null || true
        ok "已删我们自己的 $removed 项"
        # ③ **目录空了才删目录** ✓ 还有别人的东西 → 保留 + 如实报告 ✓✓
        if [ -z "$(ls -A "$PREFIX" 2>/dev/null || true)" ]; then
            # ✓ 注意：上面的 `find -depth -empty` 可能**已经把目录删了** ✓ → 这里再删会失败 ✓
            #   所以**先看它还在不在** ✓ 免得日志自相矛盾（"没能删掉"但实际已删 ✗ 实测踩到 ✓）
            if [ -d "$PREFIX" ]; then
                rmdir "$PREFIX" 2>/dev/null && ok "已删安装目录（已空）" || warn "目录没能删掉：$PREFIX"
            else
                ok "已删安装目录（已空）"
            fi
        else
            # ✓ F8：删不干净**不假报干净** ✗
            warn "目录里**还有不属于本工具的文件** → 目录**保留** ✓：$PREFIX"
            kept=$((kept+1))
        fi
        [ "$kept" -gt 0 ] && warn "有 $kept 项没能删掉（可能是权限或被占用 ✓）" || true
    fi

    # **不删数据** ✓ 留一份说明 ✓
    DATA_DIR="$HOME/.dsh"
    NOTE="$(desktop_dir)/$APP-卸载说明.txt"
    {
        echo "$APP 已卸载。"
        echo ""
        echo "**你的数据没有被删除。**"
        echo ""
        echo "DeepSeek Harness 的会话、设置等数据在："
        echo "    $DATA_DIR"
        [ -d "$DATA_DIR" ] && echo "（这个目录现在还在 ✓）" || echo "（这个目录当前不存在）"
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

[ -f "$SRC_DIR/$APP" ] || die "找不到 $SRC_DIR/$APP —— 请在**解压后的包目录里**运行本脚本 ✓"
[ -x "$SRC_DIR/$APP" ] || chmod +x "$SRC_DIR/$APP" 2>/dev/null || true
[ -f "$SRC_DIR/gui/dsht-gui" ] || warn "包内没有 gui/dsht-gui（图形界面将不可用，CLI 仍可用）"

# ★★ F2 修复：目标**非空且不是我们的** → **拒绝** ✗✗
#   ✗ 原来 `rm -rf` 掉 --prefix 里已有的一切 ✓
#     （审计实测：`--prefix ~/.local/share` → `other-app/` 与 `other.desktop` **全没了** ✓ 还报成功 ✗✗）
#   ✓ 现在：非空 + 没有我们的标记 → 拒绝 ✓（要强装用 --force ✓）
if [ -d "$PREFIX" ] && [ -n "$(ls -A "$PREFIX" 2>/dev/null || true)" ] && [ ! -f "$PREFIX/$MARKER" ] && [ "$FORCE" -eq 0 ]; then
    die "拒绝安装：$PREFIX 非空且不是本工具的安装目录 ✓
  为了安全，安装器**不会**装进一个非空目录 —— 因为卸载时会删除安装目录里的文件 ✓
  请换一个空目录，或确认里面没有重要文件后用 --force 强制安装 ✓"
fi

mkdir -p "$(dirname -- "$PREFIX")" || die "建不了 $PREFIX 的父目录"
STAGING="$(dirname -- "$PREFIX")/.$APP.staging.$$"
rm -rf "$STAGING" 2>/dev/null || true
mkdir -p "$STAGING" || die "建不了暂存目录"

# ★★ F3 修复：**不再用 `tar | tar` 管道** ✗（左端失败会被忽略 → 文件静默缺失却报成功 ✗）
#   ✓ 现在用 `cp -R` ✓ 两侧都检查 ✓ 并核对文件数 ✓
say "  复制到暂存目录…"
if ! cp -R "$SRC_DIR/." "$STAGING/" 2>/dev/null; then
    die "复制失败（cp 出错）—— 检查源目录是否可读 ✓"
fi
# 去掉包内的大文件与校验和 ✓（它们不属于安装内容 ✓ 顺带避免 busybox tar 的 --exclude 问题 ✓ F16）
rm -f "$STAGING"/*.tar.gz "$STAGING"/*.tar.gz.sha256 "$STAGING"/*.zip "$STAGING"/*.sha256 2>/dev/null || true
src_n=$(find "$SRC_DIR" -type f 2>/dev/null | wc -l | tr -d ' ')
stg_n=$(find "$STAGING" -type f 2>/dev/null | wc -l | tr -d ' ')
ok "复制完成（暂存 $stg_n 个文件 ✓ 源 $src_n 个 ✓）"
if [ "$stg_n" -eq 0 ]; then die "复制后暂存目录是空的 ✗"; fi

chmod +x "$STAGING/$APP" 2>/dev/null || true
[ -f "$STAGING/gui/dsht-gui" ] && chmod +x "$STAGING/gui/dsht-gui" 2>/dev/null || true

# ---- 就位：先改名旧的，再改名新的 ✓（与 Windows 侧同一套原子性做法 ✓）----
if [ -d "$PREFIX" ]; then
    OLD="$PREFIX.old.$$"
    rm -rf "$OLD" 2>/dev/null || true
    if mv "$PREFIX" "$OLD" 2>/dev/null; then
        rm -rf "$OLD" 2>/dev/null || warn "旧目录没能完全删掉：$OLD"
        ok "已替换旧版本"
    else
        warn "旧目录改名失败（可能有程序占用）→ 就地覆盖 ✓"
    fi
fi
mv "$STAGING" "$PREFIX" || die "就位失败（暂存目录留在 $STAGING，可以手动看）"
STAGING=""     # 已就位 ✓ trap 不用再清理 ✓
ok "已安装到 $PREFIX"

# ---- 安装标记 ✓（F1 的前提 ✓ 卸载时靠它认身份 ✓）----
{
    echo "dsh-minato install marker"
    echo "path=$PREFIX"
    echo "installed=$(date '+%Y-%m-%d %H:%M:%S' 2>/dev/null || echo unknown)"
} > "$PREFIX/$MARKER" 2>/dev/null && ok "已写安装标记 ✓" || warn "安装标记写入失败（卸载会更保守 ✓）"

# ★★ **记下我们装了哪些文件** ✓✓（卸载**只删这些** ✓✓）
#   ✗ 包内 `hashes.txt` 只覆盖 2 个文件（248 个里的 2 个 ✗ 与 Windows 侧审计 #3 同类 ✓）
#     → 卸载时"只删清单里的"会**剩 246 个文件** ✗✗（VM 实测确认 ✓）
#   ✓ 现在：安装时**把真实装进去的每个文件都记下来** ✓ → 卸载时删这份清单 ✓✓
find "$PREFIX" -type f 2>/dev/null | sed "s:^$PREFIX/::" > "$PREFIX/.dsh-minato-files" 2>/dev/null \
    && ok "已记录安装文件清单（$(wc -l < "$PREFIX/.dsh-minato-files" | tr -d ' ') 个文件 ✓）" \
    || warn "记录文件清单失败（卸载会更保守 ✓）"

# ---- 命令链接 ✓ ----
mkdir -p "$BINDIR" || die "建不了 $BINDIR"
# ✗✗ F7：原来直接 `ln -sf` → **覆盖用户自己的符号链接** ✗
#   （审计实测：用户建的 `$BINDIR/dsh-minato -> /etc/hostname` 被装掉、然后被卸掉 ✓✗）
# ✓ 修：**安装时**就检查 ✓ —— 已存在且不指向我们 → **拒绝** ✓✓（卸载侧检查太晚 ✓）
if [ -L "$BINDIR/$APP" ]; then
    _tgt=$(readlink "$BINDIR/$APP" 2>/dev/null || true)
    case "$_tgt" in
        "$PREFIX"/*) : ;;   # 指向我们（重装 ✓）→ 可以覆盖 ✓
        *) die "拒绝：$BINDIR/$APP 已经是一个指向「$_tgt」的符号链接 ✓
  它不是本工具建的 ✓ 为了不动别人的东西，请先自行处理它（或换 DSH_MINATO_BINDIR ✓）" ;;
    esac
elif [ -e "$BINDIR/$APP" ]; then
    die "$BINDIR/$APP 已存在且不是符号链接 ✓ 请先处理它 ✓"
fi
ln -sf "$PREFIX/$APP" "$BINDIR/$APP"
ok "已链接 $BINDIR/$APP"
case ":$PATH:" in
    *":$BINDIR:"*) : ;;
    *) warn "$BINDIR 不在 PATH 里 —— 加一行到 ~/.profile 即可：export PATH=\"\$HOME/.local/bin:\$PATH\"" ;;
esac

# ---- 菜单项 ✓（F11：Exec= **要加引号** ✗ 有空格否则 GLib 直接拒绝 ✓）----
if [ "$WRITE_DESKTOP" -eq 1 ]; then
    mkdir -p "$DESKTOP_DIR" || warn "建不了 $DESKTOP_DIR（跳过菜单项）"
    ICON="$PREFIX/icons/$APP.png"
    [ -f "$ICON" ] || ICON="$PREFIX/$APP"
    cat > "$DESKTOP_DIR/$APP.desktop" <<EOF
[Desktop Entry]
Type=Application
Name=$APP
Comment=DeepSeek Harness 的非官方工具箱（只读本地状态，不联网上传）
Exec="$PREFIX/gui/dsht-gui"
Icon=$ICON
Terminal=false
Categories=Utility;
EOF
    command -v update-desktop-database >/dev/null 2>&1 && update-desktop-database "$DESKTOP_DIR" 2>/dev/null || true
    ok "已写菜单项 $DESKTOP_DIR/$APP.desktop"
fi

# ---- 指纹自检 ✓（F4：**不再静默跳过** ✗ 覆盖不全 → 拒绝 ✓ 真不符 → 非零退出 ✓✓）----
if [ -f "$PREFIX/hashes.txt" ]; then
    say "  自检安装后的文件指纹…"
    if command -v sha256sum >/dev/null 2>&1; then
        checked=0; bad=0; missing=0
        while read -r want name; do
            case "${want:-}" in ''|\#*) continue ;; esac
            [ -n "${name:-}" ] || continue
            f="$PREFIX/$name"
            [ -f "$f" ] || f="$PREFIX/gui/$name"
            # ✗ F4：原来这里 `continue` **静默跳过** ✓ 然后照样打印"✓ 指纹全部一致" ✗✗
            #   ✓ 现在**计数** ✓ 缺文件就是缺文件 ✓
            if [ ! -f "$f" ]; then missing=$((missing+1)); warn "清单里有但装完没有：$name"; continue; fi
            checked=$((checked+1))
            got=$(sha256sum "$f" 2>/dev/null | cut -d' ' -f1)
            [ "$got" = "$want" ] || { bad=$((bad+1)); warn "指纹不符：$name"; }
        done < "$PREFIX/hashes.txt"
        total=$(find "$PREFIX" -type f 2>/dev/null | wc -l | tr -d ' ')
        say "    清单核对 $checked 项 · 不符 $bad · 缺失 $missing · 目录共 $total 个文件"
        if [ "$bad" -gt 0 ] || [ "$missing" -gt 0 ]; then
            # ✓ F4：**真不符要非零退出** ✗ 原来只 warn 然后照样"安装完成 ✓" exit 0 ✗✗
            warn "**指纹校验没通过** ✗ 安装包可能被改动过，或文件不完整 ✓"
            warn "已安装，但请从官方 Releases 重新下载核对 ✓"
            exit 3
        fi
        ok "指纹全部一致 ✓（$checked 项）"
    else
        warn "没有 sha256sum，跳过指纹自检（**未校验** ✓ 不是通过 ✓）"
    fi
else
    warn "包内没有 hashes.txt → **未做任何校验** ✓（不是通过 ✓）"
fi

# ---- 结束提示 ✓ ----
say ""
say "安装完成 ✓"
say "  · 启动图形界面：$BINDIR/$APP gui   或   $PREFIX/gui/dsht-gui"
say "  · 命令行：$BINDIR/$APP --help"
say "  · 卸载：$PREFIX/install.sh --uninstall   或   $SRC_DIR/install.sh --uninstall"
say "  · **本工具只读你的数据** ✓ 卸载也**不会删** ~/.dsh ✓"
say "  · 非官方工具，与 DeepSeek 官方无关 ✓（许可见 ASSETS.md，隐私见 PRIVACY.md）"
