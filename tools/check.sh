#!/usr/bin/env bash
# Cross-platform quality gate. Usage: tools/check.sh [fast|full] [scenario-filter]
#   fast  behavior scenarios + whitespace check (default)
#   full  fast + MonoGame host build + formatting verification
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

"${dotnet[@]}" run --project Swof.Engine.Tests/Swof.Engine.Tests.csproj -- ${filter:+"$filter"}
git diff --check HEAD

if [ "$mode" = full ]; then
    "${dotnet[@]}" build Swof.Game/Swof.Game.csproj --nologo -v q
    "${dotnet[@]}" format Swof.Engine/Swof.Engine.csproj --verify-no-changes
    "${dotnet[@]}" format Swof.Game/Swof.Game.csproj --verify-no-changes
    "${dotnet[@]}" format Swof.Engine.Tests/Swof.Engine.Tests.csproj --verify-no-changes
fi
