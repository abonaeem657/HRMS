# Creates HRMSnew, applies migrations, then permanently deletes HRMS on the same LocalDB instance.
$ErrorActionPreference = 'Stop'
$solutionPath = Split-Path $PSScriptRoot -Parent
$newConnection = 'Server=(localdb)\MSSQLLocalDB;Database=HRMSnew;Trusted_Connection=True;MultipleActiveResultSets=True;TrustServerCertificate=True'
$oldConnection = 'Server=(localdb)\MSSQLLocalDB;Database=HRMS;Trusted_Connection=True;MultipleActiveResultSets=True;TrustServerCertificate=True'
$names = @('ASPNETCORE_ENVIRONMENT', 'DOTNET_ENVIRONMENT', 'ConnectionStrings__HRMS')
$previous = @{}
foreach ($name in $names) { $previous[$name] = [Environment]::GetEnvironmentVariable($name, 'Process') }
Push-Location $solutionPath
try {
    $env:ASPNETCORE_ENVIRONMENT = 'Development'
    $env:DOTNET_ENVIRONMENT = 'Development'
    $env:ConnectionStrings__HRMS = $newConnection
    & dotnet ef database update --project HRMS.Infrastructure --startup-project HRMS
    if ($LASTEXITCODE -ne 0) { throw 'HRMSnew migration failed. HRMS has not been deleted.' }

    & dotnet user-secrets set 'ConnectionStrings:HRMS' $newConnection --project HRMS
    if ($LASTEXITCODE -ne 0) { throw 'Could not save the HRMSnew connection. HRMS has not been deleted.' }

    $env:ConnectionStrings__HRMS = $oldConnection
    & dotnet ef database drop --force --project HRMS.Infrastructure --startup-project HRMS --no-build
    if ($LASTEXITCODE -ne 0) { throw 'HRMSnew is ready, but deletion of HRMS failed.' }
    Write-Host 'HRMSnew is ready. The old HRMS database has been deleted.'
} finally {
    Pop-Location
    foreach ($name in $names) { [Environment]::SetEnvironmentVariable($name, $previous[$name], 'Process') }
}
