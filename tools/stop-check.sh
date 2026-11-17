#!/usr/bin/env bash
# Claude Code Stop hook: runs the fast gate before an agent ends its turn with
# changed code, and sends it back to work when the gate fails. A tree that has
# already passed is not checked again.
set -uo pipefail
cd "$(dirname "$0")/.."

# A second stop after this hook blocked once: let it through rather than loop.
grep -q '"stop_hook_active": *true' && exit 0

# Only code and the gates themselves, changed in the working tree or committed on
# this branch; a docs-only change needs no build.
base="$(git merge-base HEAD main 2>/dev/null || git rev-parse HEAD)"
[ -z "$(git status --porcelain -- 'Swof.*' tools)" ] &&
    git diff --quiet "$base" HEAD -- 'Swof.*' tools && exit 0
command -v dotnet >/dev/null || command -v mise >/dev/null || exit 0

# The whole working tree, untracked files included, as one tree hash.
index="$(mktemp)"
trap 'rm -f "$index"' EXIT
cp "$(git rev-parse --git-dir)/index" "$index"
tree="$(GIT_INDEX_FILE="$index" git add -A . && GIT_INDEX_FILE="$index" git write-tree)"
passed="$(git rev-parse --git-dir)/swof-stop-check-passed"
[ "$(cat "$passed" 2>/dev/null)" = "$tree" ] && exit 0

if output="$(tools/check.sh fast 2>&1)"; then
    echo "$tree" >"$passed"
    exit 0
fi
{
    echo "tools/check.sh fast fails on this working tree. Fix it before stopping, or say plainly that it fails and why:"
    echo "$output" | grep -E 'FAIL|error|Error|failed' | head -40
    echo "$output" | tail -5
} >&2
exit 2
