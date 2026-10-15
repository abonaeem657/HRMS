$ErrorActionPreference = 'Stop'
$projectPath = Join-Path $PSScriptRoot '..\HRMS\HRMS.csproj'
$names = @('ASPNETCORE_ENVIRONMENT', 'DOTNET_ENVIRONMENT', 'LocalAdmin__Email', 'LocalAdmin__Password')
$previous = @{}
foreach ($name in $names) { $previous[$name] = [Environment]::GetEnvironmentVariable($name, 'Process') }

$random = [Security.Cryptography.RandomNumberGenerator]::Create()
$bytes = New-Object byte[] 24
$random.GetBytes($bytes)
$password = 'Aa1!' + [Convert]::ToBase64String($bytes)
$email = 'admin.' + [Guid]::NewGuid().ToString('N').Substring(0, 12) + '@hrms.test'

try {
    $env:ASPNETCORE_ENVIRONMENT = 'Development'
    $env:DOTNET_ENVIRONMENT = 'Development'
    $env:LocalAdmin__Email = $email
    $env:LocalAdmin__Password = $password
    & dotnet run --project $projectPath --no-launch-profile -- --admin-create
    if ($LASTEXITCODE -ne 0) {
        throw 'Administrator creation failed. No login credentials will be displayed. Check the database connection and pending migrations.'
    }
    Write-Host ''
    Write-Host 'Local administrator created. Save these credentials:'
    Write-Host ('Email: ' + $email)
    Write-Host ('Password: ' + $password)
} finally {
    foreach ($name in $names) { [Environment]::SetEnvironmentVariable($name, $previous[$name], 'Process') }
    $random.Dispose()
    [Array]::Clear($bytes, 0, $bytes.Length)
    Remove-Variable password -ErrorAction SilentlyContinue
}
