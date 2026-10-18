#!/usr/bin/env bash
set -euo pipefail

if (( $# != 1 )); then
    echo "Exactly one non-Workstation host is required." >&2
    exit 2
fi
host="$1"
case "$host" in
    core-api|admin-api|admin-bootstrap|db-migrator|web) ;;
    *) echo "Unsupported host: $host" >&2; exit 2 ;;
esac

repository_root="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$repository_root"
project=""
while IFS=$'\t' read -r alias candidate_project _; do
    if [[ "$alias" == "$host" ]]; then
        project="$candidate_project"
        break
    fi
done < eng/hosts.tsv
if [[ -z "$project" || ! -f "$project" ]]; then
    echo "Missing host project in eng/hosts.tsv: $host" >&2
    exit 1
fi

output="artifacts/publish/$host"
mkdir -p artifacts/host-verification
# A failed run must not leave an older publication available for collection.
rm -rf -- "$output"
{
    dotnet --info
    ./eng/verify-host.sh "$host"
    ./eng/build-host.sh "$host" --publish
    assembly="$(basename -- "${project%.csproj}")"
    for suffix in dll deps.json runtimeconfig.json; do
        if [[ ! -s "$output/$assembly.$suffix" ]]; then
            echo "Missing or empty publish output: $host/$assembly.$suffix" >&2
            exit 1
        fi
    done
    (
        cd "$output"
        find . -type f ! -path ./SHA256SUMS -print0 | sort -z | xargs -0 -r sha256sum > SHA256SUMS
    )
    echo "Verified and published $host from $project."
} 2>&1 | tee "artifacts/host-verification/$host.log"
