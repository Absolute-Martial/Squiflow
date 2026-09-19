#!/usr/bin/env bash
set -euo pipefail

repository_root="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)"
configuration="${CONFIGURATION:-Release}"

cd "$repository_root"

dotnet restore SquiFlow.slnx
dotnet build SquiFlow.slnx --configuration "$configuration" --no-restore
dotnet test SquiFlow.slnx --configuration "$configuration" --no-build --no-restore
