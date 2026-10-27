#!/usr/bin/env bash
set -euo pipefail

# ===================================================================================
# build/native-linux.sh
#
# PURPOSE:
#   Compiles the native (C/C++) Cyclone DDS submodule for Linux and copies the
#   resulting binaries (.so libraries + idlc) to the local 'artifacts' directory.
#   This is a prerequisite for running managed tests or packing the NuGet package
#   with linux-x64 support. It is the Linux counterpart of build/native-win.ps1.
#
# USAGE:
#   ./build/native-linux.sh [Release|Debug]
#   Default: Release
# ===================================================================================

CONFIG="${1:-Release}"
SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
REPO_ROOT="$(dirname "$SCRIPT_DIR")"
SOURCE_DIR="$REPO_ROOT/cyclonedds"
BUILD_DIR="$REPO_ROOT/build/native-linux"
INSTALL_DIR="$REPO_ROOT/artifacts/native-install-linux"
ARTIFACTS_DIR="$REPO_ROOT/artifacts/native/linux-x64"

echo "============================================================"
echo "  Building Native CycloneDDS for Linux ($CONFIG)"
echo "============================================================"

# ----------------------------------------------------------------
# Prerequisites
# ----------------------------------------------------------------
if ! command -v cmake &> /dev/null; then
    echo "ERROR: cmake is not installed or not in PATH." >&2
    exit 1
fi

if ! command -v gcc &> /dev/null && ! command -v cc &> /dev/null; then
    echo "ERROR: no C compiler (gcc/cc) found in PATH." >&2
    exit 1
fi

if [ ! -d "$SOURCE_DIR" ] || [ -z "$(ls -A "$SOURCE_DIR" 2>/dev/null)" ]; then
    echo "ERROR: Native source directory is missing or empty: $SOURCE_DIR" >&2
    echo "       Run: git submodule update --init --recursive" >&2
    exit 1
fi

mkdir -p "$BUILD_DIR" "$INSTALL_DIR" "$ARTIFACTS_DIR"

# ----------------------------------------------------------------
# [1/3] CMake Configure
# ----------------------------------------------------------------
echo ""
echo "[1/3] Configuring CMake..."

cmake -S "$SOURCE_DIR" -B "$BUILD_DIR" \
    -DCMAKE_INSTALL_PREFIX="$INSTALL_DIR" \
    -DCMAKE_BUILD_TYPE="$CONFIG" \
    -DBUILD_IDLC=ON \
    -DBUILD_TESTING=OFF \
    -DBUILD_EXAMPLES=OFF \
    -DENABLE_SSL=OFF \
    -DENABLE_SHM=OFF \
    -DENABLE_SECURITY=OFF

# ----------------------------------------------------------------
# [2/3] Build & Install
# ----------------------------------------------------------------
echo ""
echo "[2/3] Building & Installing..."

NPROC="${NPROC:-$(nproc 2>/dev/null || echo 4)}"
cmake --build "$BUILD_DIR" --config "$CONFIG" -j "$NPROC"
cmake --install "$BUILD_DIR" --config "$CONFIG"

# ----------------------------------------------------------------
# [3/3] Copy Artifacts & Fix RPATH
# ----------------------------------------------------------------
echo ""
echo "[3/3] Copying artifacts to $ARTIFACTS_DIR..."

LIB_DIR="$INSTALL_DIR/lib"
BIN_DIR="$INSTALL_DIR/bin"
# Some distros install shared libs to lib64.
[ -d "$LIB_DIR" ] || LIB_DIR="$INSTALL_DIR/lib64"

# Copy a shared library, resolving the SONAME symlink chain to the real file so
# we don't have to hard-code the version suffix. The file is staged as both
# <base>.so (linker/convention name) and under its SONAME (e.g. <base>.so.0 or
# <base>.so.11), which is the name dependent binaries (idlc) load at runtime.
copy_lib() {
    local base="$1"
    local real=""
    if [ -e "$LIB_DIR/${base}.so" ]; then
        real="$(readlink -f "$LIB_DIR/${base}.so")"
    elif [ -e "$LIB_DIR/${base}.so.0" ]; then
        real="$(readlink -f "$LIB_DIR/${base}.so.0")"
    else
        # Last resort: pick the most specific versioned file available.
        real="$(ls -1 "$LIB_DIR/${base}.so".* 2>/dev/null | sort | tail -n1 || true)"
    fi

    if [ -z "$real" ] || [ ! -e "$real" ]; then
        echo "  [-] Missing ${base}.so* in $LIB_DIR" >&2
        return 1
    fi

    local soname
    soname="$(readelf -d "$real" 2>/dev/null | sed -n 's/.*(SONAME).*\[\(.*\)\].*/\1/p')"
    [ -n "$soname" ] || soname="${base}.so.0"

    cp -f "$real" "$ARTIFACTS_DIR/${base}.so"
    cp -f "$real" "$ARTIFACTS_DIR/${soname}"
    echo "  [+] ${base}.so / ${soname}"
}

# Runtime library and IDL-compiler support libraries.
copy_lib libddsc
copy_lib libcycloneddsidl
copy_lib libcycloneddsidlc
copy_lib libcycloneddsidljson

# IDL compiler executable.
if [ -f "$BIN_DIR/idlc" ]; then
    cp -f "$BIN_DIR/idlc" "$ARTIFACTS_DIR/"
    chmod +x "$ARTIFACTS_DIR/idlc"
    echo "  [+] idlc"
else
    echo "  [-] Missing idlc in $BIN_DIR" >&2
    exit 1
fi

# Fix RPATH: CMake sets RPATH to $ORIGIN/../lib (bin/ -> lib/), but in the flat
# NuGet tools/ directory every file sits side by side. Rewrite RPATH to $ORIGIN/
# so the dynamic linker finds the .so dependencies next to the executable.
if command -v patchelf &> /dev/null; then
    echo ""
    echo "  [+] Fixing RPATH to \$ORIGIN/..."
    chmod +w "$ARTIFACTS_DIR/"*.so* "$ARTIFACTS_DIR/idlc" 2>/dev/null || true
    for f in "$ARTIFACTS_DIR/"*.so* "$ARTIFACTS_DIR/idlc"; do
        [ -e "$f" ] || continue
        patchelf --set-rpath '$ORIGIN/' "$f" 2>/dev/null || true
    done
    echo "  [+] RPATH fixed."
else
    echo "  [!] patchelf not found. Run: sudo apt-get install patchelf"
    echo "  [!] Without patchelf, LD_LIBRARY_PATH must point at the idlc directory at runtime."
    echo "  [!] (IdlcRunner sets this automatically; DllImport('ddsc') resolves via the co-located .so.)"
fi

echo ""
echo "Native build complete."
echo "Artifacts staged at: $ARTIFACTS_DIR"
ls -la "$ARTIFACTS_DIR"
