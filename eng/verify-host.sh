#!/usr/bin/env bash
set -euo pipefail

repository_root="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)"
configuration="${CONFIGURATION:-Release}"
host="${1:-}"
manifest="$repository_root/eng/hosts.tsv"
project=""
test_list=""

if (( $# != 1 )); then
    echo "Exactly one host name is required." >&2
    exit 2
fi

while IFS=$'\t' read -r alias candidate_project candidate_tests; do
    [[ -z "$alias" || "$alias" == \#* ]] && continue
    if [[ "$alias" == "$host" ]]; then
        project="$candidate_project"
        test_list="$candidate_tests"
        break
    fi
done < "$manifest"

if [[ -z "$project" || -z "$test_list" ]]; then
    echo "Usage: ./eng/verify-host.sh {core-api|admin-api|admin-bootstrap|db-migrator}" >&2
    exit 2
fi

IFS=';' read -r -a test_projects <<< "$test_list"

cd "$repository_root"
./eng/build-host.sh "$host"
dotnet format "$project" --no-restore --verify-no-changes

for tests in "${test_projects[@]}"; do
    dotnet restore "$tests" --locked-mode
    dotnet format "$tests" --no-restore --verify-no-changes
    dotnet test "$tests" --configuration "$configuration" --no-restore
 done
