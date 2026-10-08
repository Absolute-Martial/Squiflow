#!/usr/bin/env bash
set -euo pipefail

repository_root="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)"
configuration="${CONFIGURATION:-Release}"

cd "$repository_root"

# Test projects are split into two phases. Container-backed suites dominate the runtime, so
# running the fast projects first means an ordinary unit regression is reported in seconds
# instead of after the whole container fleet has finished. The split is derived from each
# project's own dependency on Testcontainers rather than from a maintained name list, so a
# new project is classified correctly without editing this script.
unit_projects=()
container_projects=()

while IFS= read -r project; do
    project_dir="$(dirname -- "$project")"
    if grep -qli -- "Testcontainers" "$project_dir"/*.csproj 2>/dev/null; then
        container_projects+=("$project")
    else
        unit_projects+=("$project")
    fi
done < <(grep -o 'Path="[^"]*\.csproj"' Application.slnx | sed 's/^Path="//; s/"$//' | grep '^tests/')

if [[ ${#unit_projects[@]} -eq 0 || ${#container_projects[@]} -eq 0 ]]; then
    echo "Test project classification found no projects in one phase; refusing to run a partial gate." >&2
    exit 1
fi

elapsed_since() { echo $(( $(date +%s) - $1 )); }

phase_start="$(date +%s)"
echo "==> restore (locked)"
dotnet restore Application.slnx --locked-mode

echo "==> format verification"
dotnet format Application.slnx --no-restore --verify-no-changes

echo "==> build ($configuration)"
dotnet build Application.slnx --configuration "$configuration" --no-restore
echo "    build phase took $(elapsed_since "$phase_start")s"

if [[ "${COLLECT_COVERAGE:-0}" == "1" ]]; then
    coverage_root="$repository_root/artifacts/coverage"
    rm -rf -- "$coverage_root"
    mkdir -p -- "$coverage_root/test-results"

    dotnet tool restore

    run_phase() {
        local label="$1"; shift
        local projects=("$@")
        if [[ ${#projects[@]} -eq 0 ]]; then
            return 0
        fi
        local started
        started="$(date +%s)"
        echo "==> tests: $label (${#projects[@]} project(s))"
        # dotnet test accepts a single project or a solution, never several projects, so each
        # project is invoked on its own. set -e stops the phase at the first failure.
        for project in "${projects[@]}"; do
            dotnet test "$project" \
                --configuration "$configuration" --no-build --no-restore \
                --collect:"XPlat Code Coverage" --results-directory "$coverage_root/test-results"
        done
        echo "    $label phase took $(elapsed_since "$started")s"
    }

    run_phase "unit" "${unit_projects[@]}"
    run_phase "container" "${container_projects[@]}"

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

    python3 eng/review-coverage.py
else
    run_phase() {
        local label="$1"; shift
        local projects=("$@")
        if [[ ${#projects[@]} -eq 0 ]]; then
            return 0
        fi
        local started
        started="$(date +%s)"
        echo "==> tests: $label (${#projects[@]} project(s))"
        # dotnet test accepts a single project or a solution, never several projects, so each
        # project is invoked on its own. set -e stops the phase at the first failure.
        for project in "${projects[@]}"; do
            dotnet test "$project" --configuration "$configuration" --no-build --no-restore
        done
        echo "    $label phase took $(elapsed_since "$started")s"
    }

    run_phase "unit" "${unit_projects[@]}"
    run_phase "container" "${container_projects[@]}"
fi

echo "==> gate phases completed in $(elapsed_since "$phase_start")s"