#!/usr/bin/env bash
set -euo pipefail

repository_root="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)"
output="$repository_root/artifacts/security/nuget-audit.txt"
mkdir -p -- "$(dirname -- "$output")"

cd "$repository_root"
{
    echo "NuGet dependency audit"
    echo "======================"
    echo "Locked restore with NuGetAuditMode=all; NU1901-NU1904 are errors for this gate."
    echo
    dotnet restore Application.slnx --locked-mode \
        -p:NuGetAudit=true \
        -p:NuGetAuditMode=all \
        '-p:WarningsAsErrors=NU1901%3BNU1902%3BNU1903%3BNU1904'
} 2>&1 | tee "$output"
