#!/usr/bin/env bash
set -euo pipefail

repository_root="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)"
configuration="${CONFIGURATION:-Release}"
host="${1:-}"

case "$host" in
    core-api) project="services/core-api/Application.CoreApi/Application.CoreApi.csproj" ;;
    admin-api) project="services/admin-api/Application.AdminApi/Application.AdminApi.csproj" ;;
    admin-bootstrap) project="services/admin-bootstrap/Application.AdminBootstrap/Application.AdminBootstrap.csproj" ;;
    db-migrator) project="services/db-migrator/Application.DatabaseMigrator/Application.DatabaseMigrator.csproj" ;;
    *)
        echo "Usage: ./eng/build-host.sh {core-api|admin-api|admin-bootstrap|db-migrator} [--publish]" >&2
        exit 2
        ;;
esac

if (( $# > 2 )) || [[ "${2:-}" != "" && "${2:-}" != "--publish" ]]; then
    echo "The only optional argument is --publish." >&2
    exit 2
fi

cd "$repository_root"
# Restoring a project follows only its ProjectReference graph, not Application.slnx.
dotnet restore "$project" --locked-mode
dotnet build "$project" --configuration "$configuration" --no-restore

if [[ "${2:-}" == "--publish" ]]; then
    dotnet publish "$project" --configuration "$configuration" --no-build --no-restore \
        --output "artifacts/publish/$host"
fi
