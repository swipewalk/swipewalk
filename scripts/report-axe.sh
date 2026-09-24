#!/bin/zsh
# Generates reports from the saved sample captures and checks them with axe-core (light and dark mode).
# Our report must have no axe violations. Needs Node and Google Chrome.
set -e
repo=${0:A:h:h}
cd "$repo"
out=${TMPDIR:-/tmp}/swipewalk-report-axe
(cd tools/report-axe && [[ -d node_modules ]] || npm install --silent)
dotnet build src/Swipewalk.Cli -v quiet -nologo | grep -E " error " || true
for platform in Android iOS; do
  dotnet run --project src/Swipewalk.Cli --no-build -- scan --platform ${platform:l} \
    --from tests/Swipewalk.Core.Tests/Fixtures/BuggyApp.$platform --standard ada-title-ii --framework maui \
    --out "$out/$platform" --no-history >/dev/null
  echo "== $platform report"
  node tools/report-axe/run.mjs "$out/$platform/report.html"
done
