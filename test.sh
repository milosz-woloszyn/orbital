#!/usr/bin/env bash
set -euo pipefail
cd -- "$(dirname -- "${BASH_SOURCE[0]}")"
bash Domain/Tests/run.sh
if [[ "${1:-}" != "--ui" && "${1:-}" != "--game" ]]; then exit 0; fi
export DOTNET_ROOT="$PWD/.tools/dotnet"
export PATH="$DOTNET_ROOT:$PATH"
export DOTNET_CLI_HOME="$PWD/.tools/cli"
export NUGET_PACKAGES="$PWD/.tools/nuget"
export XDG_DATA_HOME="$PWD/.tools/test-user/data"
export XDG_CONFIG_HOME="$PWD/.tools/test-user/config"
export XDG_CACHE_HOME="$PWD/.tools/test-user/cache"
GODOT="${GODOT_BIN:-$PWD/.tools/Godot_v4.5.2-stable_mono_linux_x86_64/Godot_v4.5.2-stable_mono_linux.x86_64}"
dotnet build Orbital.csproj --nologo --verbosity quiet
mkdir -p artifacts
"$GODOT" --headless --editor --path . --import > artifacts/import.log 2>&1
scenarios=(smoke game-smoke)
if [[ "${1:-}" == "--game" ]]; then scenarios=(game-smoke); fi
for scenario in "${scenarios[@]}"; do
  result=0
  timeout 180s "$GODOT" --path . --audio-driver Dummy --position 40,40 -- "--$scenario" > "artifacts/$scenario.log" 2>&1 || result=$?
  cat "artifacts/$scenario.log"
  if (( result != 0 )); then exit "$result"; fi
  if rg -q 'ERROR:|ObjectDB instances leaked' "artifacts/$scenario.log"; then exit 1; fi
  rg -q 'SMOKE PASS' "artifacts/$scenario.log"
done
