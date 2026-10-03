#!/usr/bin/env bash
set -euo pipefail

repository_root="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)"
configuration="${CONFIGURATION:-Release}"
host="${1:-}"

case "$host" in
    core-api)
        project="services/core-api/Application.CoreApi/Application.CoreApi.csproj"
        tests="tests/integration/Application.CoreApi.Tests/Application.CoreApi.Tests.csproj"
        ;;
    admin-api)
        project="services/admin-api/Application.AdminApi/Application.AdminApi.csproj"
        tests="tests/integration/Application.AdminApi.Tests/Application.AdminApi.Tests.csproj"
        ;;
    admin-bootstrap)
        project="services/admin-bootstrap/Application.AdminBootstrap/Application.AdminBootstrap.csproj"
        tests="tests/integration/Application.AdminBootstrap.Tests/Application.AdminBootstrap.Tests.csproj"
        ;;
    db-migrator)
        project="services/db-migrator/Application.DatabaseMigrator/Application.DatabaseMigrator.csproj"
        tests="tests/integration/Application.IdentityAccess.Postgres.Tests/Application.IdentityAccess.Postgres.Tests.csproj"
        ;;
    *)
        echo "Usage: ./eng/verify-host.sh {core-api|admin-api|admin-bootstrap|db-migrator}" >&2
        exit 2
        ;;
esac

if (( $# != 1 )); then
    echo "Exactly one host name is required." >&2
    exit 2
fi

cd "$repository_root"
./eng/build-host.sh "$host"
dotnet restore "$tests" --locked-mode
dotnet format "$project" --no-restore --verify-no-changes
dotnet format "$tests" --no-restore --verify-no-changes
dotnet test "$tests" --configuration "$configuration" --no-restore
