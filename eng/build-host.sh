#!/usr/bin/env bash
set -euo pipefail

repository_root="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)"
configuration="${CONFIGURATION:-Release}"
host="${1:-}"
manifest="$repository_root/eng/hosts.tsv"
project=""

if (( $# > 2 )) || [[ "${2:-}" != "" && "${2:-}" != "--publish" ]]; then
    echo "The only optional argument is --publish." >&2
    exit 2
fi

while IFS=$'\t' read -r alias candidate_project _; do
    [[ -z "$alias" || "$alias" == \#* ]] && continue
    if [[ "$alias" == "$host" ]]; then
        project="$candidate_project"
        break
    fi
done < "$manifest"

if [[ -z "$project" ]]; then
    echo "Usage: ./eng/build-host.sh {core-api|admin-api|admin-bootstrap|db-migrator} [--publish]" >&2
    exit 2
fi

cd "$repository_root"
# Restoring a project follows only its ProjectReference graph, not Application.slnx.
dotnet restore "$project" --locked-mode
dotnet build "$project" --configuration "$configuration" --no-restore

if [[ "${2:-}" == "--publish" ]]; then
    output="artifacts/publish/$host"
    rm -rf -- "$output"
    dotnet publish "$project" --configuration "$configuration" --no-build --no-restore \
        --output "$output"
    test -f "$output/$(basename "${project%.csproj}").dll" || {
        echo "Publish completed without the expected host assembly for $host." >&2
        exit 1
    }
fi
