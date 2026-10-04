#!/usr/bin/env bash
set -euo pipefail

repository_root="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)"
test_project="$repository_root/tests/unit/Application.Orders.Tests"
output="$repository_root/artifacts/mutation-orders"

rm -rf -- "$output"
cd "$test_project"
dotnet tool restore
dotnet stryker \
    --mutate '**/OrderDraft.cs' \
    --output "$output" \
    --concurrency "${STRYKER_CONCURRENCY:-2}" \
    --reporter json \
    --reporter progress \
    --configuration Release

cd "$repository_root"
python3 eng/review-mutation.py
