#!/usr/bin/env bash
set -euo pipefail
cd -- "$(dirname -- "${BASH_SOURCE[0]}")"
ROOT="$PWD"
if [[ -x "$ROOT/.tools/dotnet/dotnet" ]]; then
  export DOTNET_ROOT="$ROOT/.tools/dotnet"
  export PATH="$DOTNET_ROOT:$PATH"
fi
export DOTNET_CLI_HOME="$ROOT/.tools/cli"
export NUGET_PACKAGES="$ROOT/.tools/nuget"
export DOTNET_CLI_TELEMETRY_OPTOUT=1
GODOT="${GODOT_BIN:-$ROOT/.tools/Godot_v4.5.2-stable_mono_linux_x86_64/Godot_v4.5.2-stable_mono_linux.x86_64}"
if [[ ! -x "$GODOT" ]]; then
  GODOT="$(command -v godot || command -v godot4 || true)"
fi
if [[ -z "$GODOT" ]] || ! command -v dotnet >/dev/null; then
  echo "Potrzebne: Godot 4.5.2 .NET oraz SDK .NET 8. Zobacz README.txt."
  exit 1
fi
dotnet build Orbital.csproj --nologo --verbosity quiet
"$GODOT" --headless --editor --path "$ROOT" --import > "$ROOT/.tools/import.log" 2>&1
exec "$GODOT" --path "$ROOT" "$@"
