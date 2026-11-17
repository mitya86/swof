#!/usr/bin/env bash
# Cross-platform quality gate. Usage: tools/check.sh [fast|full] [scenario-filter]
#   fast     behavior scenarios + whitespace check (default)
#   full     fast + MonoGame host build + formatting verification
#   matches  100 unattended computer-versus-computer matches (about a minute);
#            tools/check.sh matches <count> <firstSeed> replays chosen seeds;
#            add "equal" to give every outfielder the same attributes;
#            add "home=half" or "away=half" to make that team wait at halfway,
#            "=up" to push its markers up, "home-up=12" for any distance,
#            "home-behind=up" / "home-ahead=half" to swap tactic by score and time;
#            100 or more matches must also meet the runner's pass marks
#   diff     the same matches on another commit (default main) and on this
#            working tree, compared line by line: tools/check.sh diff [base] [count]
#            [matches options...]. Any difference fails. A refactor, renderer or
#            docs change must show none; a gameplay change shows which seeds moved
set -euo pipefail
cd "$(dirname "$0")/.."

dotnet=(dotnet)
if ! dotnet --version >/dev/null 2>&1; then
    # The SDK may be installed through mise without being activated (a mise
    # shim can exist on PATH and still fail, so probe by running it).
    dotnet=(mise exec dotnet@10 -- dotnet)
fi

mode="${1:-fast}"
filter="${2:-}"

if [ "$mode" = matches ]; then
    exec "${dotnet[@]}" run --project Swof.Engine.Tests/Swof.Engine.Tests.csproj -- matches "${@:2}"
fi

if [ "$mode" = diff ]; then
    base="${2:-main}"
    count="${3:-100}"
    work="$(mktemp -d)"
    trap 'git worktree remove --force "$work/base" >/dev/null 2>&1 || true; rm -rf "$work"' EXIT
    git worktree add --detach --quiet "$work/base" "$base"
    # Only the per-seed lines: a failing match or a missed mark still prints one, so
    # the exit code of either run is not what is compared.
    seeds() {
        (cd "$1" && "${dotnet[@]}" run --project Swof.Engine.Tests/Swof.Engine.Tests.csproj -- matches "$count" 1 "${@:2}" || true) |
            grep -E '^(OK|FAIL) +seed' || true
    }
    seeds "$work/base" "${@:4}" >"$work/base.txt"
    seeds . "${@:4}" >"$work/head.txt"
    if [ ! -s "$work/head.txt" ]; then
        echo "diff: no per-seed lines from this working tree" >&2
        exit 1
    fi
    if diff "$work/base.txt" "$work/head.txt"; then
        echo "diff: all $(wc -l <"$work/head.txt" | tr -d ' ') matches identical to $base"
        exit 0
    fi
    echo "diff: matches differ from $base (< $base, > this working tree)" >&2
    exit 1
fi

"${dotnet[@]}" run --project Swof.Engine.Tests/Swof.Engine.Tests.csproj -- ${filter:+"$filter"}
git diff --check HEAD

if [ "$mode" = full ]; then
    "${dotnet[@]}" build Swof.Game/Swof.Game.csproj --nologo -v q
    "${dotnet[@]}" format Swof.Engine/Swof.Engine.csproj --verify-no-changes
    "${dotnet[@]}" format Swof.Game/Swof.Game.csproj --verify-no-changes
    "${dotnet[@]}" format Swof.Engine.Tests/Swof.Engine.Tests.csproj --verify-no-changes
fi
