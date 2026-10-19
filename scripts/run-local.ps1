param(
    [ValidateRange(1024, 65535)][int]$Port = 5014,
    [ValidatePattern('^[A-Za-z0-9_-]+$')][string]$DatabaseName = 'HRMSnew'
)
$ErrorActionPreference = 'Stop'
$solutionRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$projectPath = Join-Path $solutionRoot 'HRMS\HRMS.csproj'
if (!(Test-Path -LiteralPath $projectPath -PathType Leaf)) { throw 'Cannot locate HRMS.csproj beside this script.' }
$previousConnection = $env:ConnectionStrings__HRMS
$previousAspnetEnvironment = $env:ASPNETCORE_ENVIRONMENT
$previousDotnetEnvironment = $env:DOTNET_ENVIRONMENT
Push-Location $solutionRoot
try {
    # Process-local overrides: no global environment, secrets, schema or user data is changed.
    $env:ASPNETCORE_ENVIRONMENT = 'Development'
    $env:DOTNET_ENVIRONMENT = 'Development'
    $env:ConnectionStrings__HRMS = 'Server=(localdb)\MSSQLLocalDB;Database=' + $DatabaseName + ';Integrated Security=True;TrustServerCertificate=True'
    Write-Host ('Project: ' + $projectPath)
    Write-Host ('Local database: ' + $DatabaseName)
    Write-Host ('Login: http://localhost:' + $Port + '/Account/Login')
    dotnet run --project $projectPath --configuration Release --no-launch-profile --urls ('http://localhost:' + $Port)
    if ($LASTEXITCODE -ne 0) { throw 'HRMS stopped with an error. Check the startup output above.' }
}
finally {
    $env:ConnectionStrings__HRMS = $previousConnection
    $env:ASPNETCORE_ENVIRONMENT = $previousAspnetEnvironment
    $env:DOTNET_ENVIRONMENT = $previousDotnetEnvironment
    Pop-Location
}
