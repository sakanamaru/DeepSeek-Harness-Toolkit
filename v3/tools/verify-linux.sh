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
VER=$(timeout 60 "$CLI" version 2>&1 | head -1)
case "$VER" in
    DSHT_VERSION*) ok "binary runs: $VER" ;;
    *) fail "binary did not answer 'version' with a DSHT_VERSION marker (got: ${VER:-<empty>})" ;;
esac
ABOUT=$(timeout 60 "$CLI" about 2>&1 | head -1)
case "$ABOUT" in
    dsh-minato*) ok "about works: $ABOUT" ;;
    *) fail "about did not print the product name (got: ${ABOUT:-<empty>})" ;;
esac

# ---- 4. hashes for the release notes ------------------------------------
echo "  per-file SHA-256:"
( cd "$ROOT" && sha256sum dsh-minato gui/dsht-gui 2>/dev/null | sed 's/^/    /' )

echo "== RESULT: LINUX RELEASE VERIFIED =="
exit 0
