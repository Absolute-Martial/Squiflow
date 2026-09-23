#!/usr/bin/env bash
set -euo pipefail

repository_root="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)"
configuration="${CONFIGURATION:-Release}"

cd "$repository_root"

dotnet restore Application.slnx --locked-mode
dotnet format Application.slnx --no-restore --verify-no-changes
dotnet build Application.slnx --configuration "$configuration" --no-restore

if [[ "${COLLECT_COVERAGE:-0}" == "1" ]]; then
    coverage_root="$repository_root/artifacts/coverage"
    rm -rf -- "$coverage_root"
    mkdir -p -- "$coverage_root/test-results"

    dotnet tool restore
    dotnet test Application.slnx --configuration "$configuration" --no-build --no-restore \
        --collect:"XPlat Code Coverage" --results-directory "$coverage_root/test-results"

    shopt -s globstar nullglob
    reports=("$coverage_root"/test-results/**/coverage.cobertura.xml)
    if (( ${#reports[@]} == 0 )); then
        echo "Coverage was requested, but no Cobertura reports were produced." >&2
        exit 1
    fi

    dotnet reportgenerator \
        "-reports:$coverage_root/test-results/**/coverage.cobertura.xml" \
        "-targetdir:$coverage_root/report" \
        "-filefilters:-*/obj/*" \
        "-reporttypes:HtmlSummary;Cobertura;TextSummary"
else
    dotnet test Application.slnx --configuration "$configuration" --no-build --no-restore
fi
