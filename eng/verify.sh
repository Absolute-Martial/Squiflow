#!/usr/bin/env bash
set -euo pipefail

repository_root="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)"
configuration="${CONFIGURATION:-Release}"

cd "$repository_root"

dotnet restore Application.slnx
dotnet format Application.slnx --no-restore --verify-no-changes
dotnet build Application.slnx --configuration "$configuration" --no-restore
dotnet test Application.slnx --configuration "$configuration" --no-build --no-restore
