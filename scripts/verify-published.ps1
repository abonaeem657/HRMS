param(
    [string]$TestSqlServer = 'Server=(localdb)\MSSQLLocalDB;Integrated Security=True;TrustServerCertificate=True',
    [string]$ChromePath = 'C:\Program Files\Google\Chrome\Application\chrome.exe'
)
$ErrorActionPreference = 'Stop'
$solutionRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
if (!(Test-Path -LiteralPath $ChromePath -PathType Leaf)) { throw 'Install Chrome or specify -ChromePath for the browser acceptance gate.' }
$previousSql = $env:HRMS_TEST_SQLSERVER
$previousDll = $env:HRMS_PUBLISHED_WEB_DLL
$previousChrome = $env:HRMS_CHROME_PATH
Push-Location $solutionRoot
try {
    dotnet publish HRMS/HRMS.csproj -c Release -o artifacts/publish
    if ($LASTEXITCODE -ne 0) { throw 'Publish failed.' }
    $env:HRMS_TEST_SQLSERVER = $TestSqlServer
    $env:HRMS_CHROME_PATH = $ChromePath
    $env:HRMS_PUBLISHED_WEB_DLL = Join-Path $solutionRoot 'artifacts/publish/HRMS.dll'
    dotnet test HRMS.sln -c Release --no-restore --logger trx --results-directory artifacts/test-results
    if ($LASTEXITCODE -ne 0) { throw 'Acceptance failed. Review artifacts/test-results.' }
}
finally {
    $env:HRMS_TEST_SQLSERVER = $previousSql
    $env:HRMS_PUBLISHED_WEB_DLL = $previousDll
    $env:HRMS_CHROME_PATH = $previousChrome
    Pop-Location
}
