#!/usr/bin/env bash
set -euo pipefail

# ===================================================================================
# build/test-package.sh
#
# PURPOSE:
#   Smoke-test the CycloneDDS.NET NuGet package (and, optionally, the DdsMonitor
#   global tool) from a local feed — whether the packages were built locally
#   (build/pack.ps1 or dotnet pack) or downloaded from a CI run (the
#   'nuget-packages' artifact). It always picks the NEWEST matching package by
#   modification time, so a cluttered artifacts/nuget with many historical builds
#   is fine.
#
#   It consumes the package as a real PackageReference via examples/PackageSmokeTest:
#   restore -> run the bundled code generator (idlc) -> publish/subscribe round-trip.
#
# USAGE:
#   build/test-package.sh [FEED_DIR]     # default FEED_DIR = artifacts/nuget
#   NO_DDSMON=1 build/test-package.sh    # skip the DdsMonitor tool check
# ===================================================================================

FEED="${1:-artifacts/nuget}"
SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
REPO_ROOT="$(dirname "$SCRIPT_DIR")"

if [ ! -d "$FEED" ]; then
    echo "ERROR: feed directory not found: $FEED" >&2
    exit 1
fi
FEED_ABS="$(cd "$FEED" && pwd)"

# Newest file matching a glob (by mtime), or empty.
newest() { ls -t "$FEED_ABS"/$1 2>/dev/null | head -n1 || true; }

RT_PKG="$(newest 'CycloneDDS.NET.[0-9]*.nupkg')"
if [ -z "$RT_PKG" ]; then
    echo "ERROR: no CycloneDDS.NET.<version>.nupkg found in $FEED_ABS" >&2
    exit 1
fi
RT_VER="$(basename "$RT_PKG" | sed -E 's/^CycloneDDS\.NET\.(.+)\.nupkg$/\1/')"

echo "============================================================"
echo "  Package smoke test"
echo "  feed:    $FEED_ABS"
echo "  package: $(basename "$RT_PKG")  (version $RT_VER)"
echo "============================================================"

# Force a fresh restore of exactly this build.
rm -rf "$HOME/.nuget/packages/cyclonedds.net" "$HOME/.nuget/packages/cyclonedds.net.ddsmonitor" 2>/dev/null || true
rm -rf "$REPO_ROOT/examples/PackageSmokeTest/bin" "$REPO_ROOT/examples/PackageSmokeTest/obj"

echo ""
echo "[1/2] Running examples/PackageSmokeTest against $RT_VER ..."
dotnet run --project "$REPO_ROOT/examples/PackageSmokeTest" -c Release \
    -p:SmokePkgVersion="$RT_VER" \
    -p:RestoreAdditionalSources="$FEED_ABS"
echo "  [+] runtime package smoke test PASSED"

if [ "${NO_DDSMON:-0}" = "1" ]; then
    echo ""
    echo "Skipping DdsMonitor tool check (NO_DDSMON=1)."
    echo "All good."
    exit 0
fi

MON_PKG="$(newest 'CycloneDDS.NET.DdsMonitor.*.nupkg')"
if [ -z "$MON_PKG" ]; then
    echo ""
    echo "No CycloneDDS.NET.DdsMonitor package in feed — skipping tool check."
    echo "All good."
    exit 0
fi
MON_VER="$(basename "$MON_PKG" | sed -E 's/^CycloneDDS\.NET\.DdsMonitor\.(.+)\.nupkg$/\1/')"

echo ""
echo "[2/2] Testing the ddsmonitor global tool $MON_VER ..."
dotnet tool uninstall --global CycloneDDS.NET.DdsMonitor >/dev/null 2>&1 || true
dotnet tool install --global --add-source "$FEED_ABS" --version "$MON_VER" CycloneDDS.NET.DdsMonitor >/dev/null

TOOL="$HOME/.dotnet/tools/ddsmonitor"
# The installed tool documents itself: --help must print the reference, including the recording workflow.
if "$TOOL" --help | grep -q 'RECORDING FOR SCRIPTS AND AI AGENTS'; then
    echo "  [+] ddsmonitor --help prints the reference ($("$TOOL" --version))"
else
    echo "  [-] ddsmonitor --help did not print the reference" >&2
    exit 1
fi
LOG="$(mktemp)"
"$TOOL" --NoBrowser true >"$LOG" 2>&1 &
TOOL_PID=$!

ok=0
for _ in $(seq 1 30); do
    if grep -q "Application started" "$LOG" 2>/dev/null; then ok=1; break; fi
    if ! kill -0 "$TOOL_PID" 2>/dev/null; then break; fi
    sleep 0.5
done

PORT="$(grep -oE 'Now listening on: http://127.0.0.1:[0-9]+' "$LOG" | grep -oE '[0-9]+$' | head -n1 || true)"
code=""
if [ -n "$PORT" ]; then
    code="$(curl -s -o /dev/null -w '%{http_code}' "http://127.0.0.1:$PORT/" 2>/dev/null || true)"
fi

kill "$TOOL_PID" 2>/dev/null || true
wait "$TOOL_PID" 2>/dev/null || true

if [ "$ok" != "1" ] || [ "$code" != "200" ]; then
    dotnet tool uninstall --global CycloneDDS.NET.DdsMonitor >/dev/null 2>&1 || true
    echo "  [-] ddsmonitor check FAILED (started=$ok, http=$code). Log:" >&2
    tail -n 20 "$LOG" >&2
    exit 1
fi
echo "  [+] ddsmonitor started (port $PORT) and served HTTP 200 — native loaded OK"

# ---------------------------------------------------------------------------------
# DDS round trip through the installed tool: one headless instance publishes its
# self-test topics, a second records them. Proves the packaged native library works
# for real pub/sub between processes, not just that it loads.
# Optionally the same with DDS Security on (packaged plugins + the system OpenSSL 3),
# plus an unsecured recorder that must receive nothing.
# ---------------------------------------------------------------------------------
WORK="$(mktemp -d)"
cleanup() { dotnet tool uninstall --global CycloneDDS.NET.DdsMonitor >/dev/null 2>&1 || true; rm -rf "$WORK"; }
trap cleanup EXIT

# run_mon <name> <domain> <CYCLONEDDS_URI or ""> <self-send true|false>  (background, ~12 s)
# Records the exit code: 124 means it was still running when the timeout stopped it.
run_mon() {
    ( cd "$WORK" && rc=0 && CYCLONEDDS_URI="$3" timeout 12 "$TOOL" --DdsSettings:DomainId="$2" \
        --AppSettings:ConfigFolder="$WORK/cfg-$1" \
        --DdsSettings:HeadlessMode=Record --DdsSettings:HeadlessFilePath="$WORK/$1.jsonl" \
        --DdsSettings:SelfSendEnabled="$4" > "$WORK/$1.log" 2>&1 || rc=$?; echo "$rc" > "$WORK/$1.exit" ) &
}
# True when every named instance ran until the timeout (none crashed or quit early).
all_ran() { local n; for n in "$@"; do [ "$(cat "$WORK/$n.exit" 2>/dev/null)" = "124" ] || return 1; done; }
# Samples recorded by an instance: complete lines of its JSON Lines capture.
recorded() { local n; n="$(wc -l < "$WORK/$1.jsonl" 2>/dev/null || true)"; echo "${n:-0}"; }

# Random domain per run, so concurrent runs on one machine do not see each other.
DOMAIN=$(( 100 + RANDOM % 100 ))
run_mon rec  "$DOMAIN" "" false
sleep 2
run_mon pub  "$DOMAIN" "" true
wait
if all_ran pub rec && [ "$(recorded rec)" -gt 0 ]; then
    echo "  [+] ddsmonitor DDS round trip between two processes: $(recorded rec) samples received"
else
    echo "  [-] ddsmonitor DDS round trip FAILED (received: $(recorded rec), exit codes pub/rec: $(cat "$WORK/pub.exit")/$(cat "$WORK/rec.exit"), 124 = ran to the end). Logs:" >&2
    tail -n 15 "$WORK/pub.log" "$WORK/rec.log" >&2
    exit 1
fi

SEC="$REPO_ROOT/tests/CycloneDDS.Runtime.Tests/Resources/Security"
# (Not "ldconfig -p | grep -q": with pipefail, grep's early exit fails the pipeline.)
if [ -f "$SEC/identity_cert.pem" ] && grep -q 'libcrypto\.so\.3' <(ldconfig -p 2>/dev/null); then
    cat > "$WORK/secure.xml" <<XML
<CycloneDDS><Domain id="any"><Security>
  <Authentication>
    <IdentityCertificate>file:$SEC/identity_cert.pem</IdentityCertificate>
    <IdentityCA>file:$SEC/ca_cert.pem</IdentityCA>
    <PrivateKey>file:$SEC/identity_key.pem</PrivateKey>
  </Authentication>
  <AccessControl>
    <PermissionsCA>file:$SEC/ca_cert.pem</PermissionsCA>
    <Governance>file:$SEC/governance.p7s</Governance>
    <Permissions>file:$SEC/permissions.p7s</Permissions>
  </AccessControl>
</Security></Domain></CycloneDDS>
XML
    DOMAIN=$(( DOMAIN + 1 ))
    run_mon srec   "$DOMAIN" "file://$WORK/secure.xml" false
    run_mon splain "$DOMAIN" "" false
    sleep 2
    run_mon spub   "$DOMAIN" "file://$WORK/secure.xml" true
    wait
    if all_ran spub srec splain && [ "$(recorded srec)" -gt 0 ] && [ "$(recorded splain)" -eq 0 ]; then
        echo "  [+] ddsmonitor DDS Security: secured recorder got $(recorded srec) samples, unsecured got none"
    else
        echo "  [-] ddsmonitor DDS Security check FAILED (secured: $(recorded srec), unsecured: $(recorded splain)). Logs:" >&2
        tail -n 15 "$WORK/spub.log" "$WORK/srec.log" >&2
        exit 1
    fi
else
    echo "  [ ] DDS Security check skipped (no test fixtures or no OpenSSL 3 libcrypto.so.3)"
fi

echo ""
echo "All good."
exit 0
