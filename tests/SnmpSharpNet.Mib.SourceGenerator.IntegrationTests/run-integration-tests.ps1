[CmdletBinding()]
param(
    [switch] $Down
)

$composeFile = Join-Path $PSScriptRoot "docker-compose.yml"

if ($Down) {
    docker compose --file $composeFile down --volumes --remove-orphans
    exit $LASTEXITCODE
}

try {
    docker compose --file $composeFile up --build --abort-on-container-exit --exit-code-from integration-tests
    $exitCode = $LASTEXITCODE
}
finally {
    docker compose --file $composeFile down --volumes --remove-orphans
}
echo "Exit Code: $exitCode"
exit $exitCode
