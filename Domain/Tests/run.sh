#!/usr/bin/env bash
set -euo pipefail

test_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
project_root="$(cd -- "$test_dir/../.." && pwd)"
domain_dotnet="$project_root/.tools/dotnet/dotnet"
if [[ ! -x "$domain_dotnet" ]]; then
    domain_dotnet="$(command -v dotnet)"
fi
export DOTNET_CLI_HOME="$project_root/.tools/cli"
export DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1
export DOTNET_CLI_TELEMETRY_OPTOUT=1

{
    "$domain_dotnet" build "$test_dir/Orbital.Domain.Tests.csproj" -c Release --nologo
    # Bound runtime so regressions in tiny passive-cycle handling cannot hang the suite.
    timeout 45s "$domain_dotnet" run --project "$test_dir/Orbital.Domain.Tests.csproj" -c Release --no-build
} 2>&1 | tee "$test_dir/latest-output.txt"
