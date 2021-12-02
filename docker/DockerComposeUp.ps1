$platform = $args[0]
if (!$platform) {
    Write-Host "Missing Platform: windows | linux"
    exit(1)
}

Write-Host "Run the CI environment"

docker-compose -f docker-compose.ci.$platform.db.yml -p nesci up --detach

Write-Host "Creating Databases"

Start-Sleep -Seconds 20

if ($platform -eq "linux") {
docker exec nesci-sqlexpress-1 /opt/mssql-tools/bin/sqlcmd -l 60 -S localhost -U sa -P Password1 -Q "CREATE DATABASE NEventStore"
}

if ($platform -eq "windows") {
docker exec nesci-sqlexpress-1 sqlcmd -l 60 -S localhost -U sa -P Password1 -Q "CREATE DATABASE NEventStore"
}
