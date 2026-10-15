$ErrorActionPreference = "Stop"
if ([string]::IsNullOrWhiteSpace($env:HRMS_TEST_SQLSERVER)) { throw "Set HRMS_TEST_SQLSERVER to an isolated SQL Server instance. Skipped database tests do not satisfy acceptance." }
dotnet restore HRMS.sln
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
dotnet build HRMS.sln --no-restore
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
dotnet test HRMS.sln --no-build --logger trx
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
