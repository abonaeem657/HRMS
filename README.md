# HRMS — repaired source delivery

ASP.NET Core MVC, SQL Server, EF Core, Identity, Arabic/English. Start with `docs/DELIVERY_AR.md` for the repair summary and limitations. Current acceptance evidence and deployment scope are documented in docs/ACCEPTANCE_AR.md.

## Prerequisites and setup

For the complete Arabic A–Z walkthrough of setup, all six roles, attendance, leave, reports and account recovery, read [USER_WALKTHROUGH_AR.md](docs/USER_WALKTHROUGH_AR.md). A shorter bilingual guide is available after sign-in under **Getting started / دليل الاستخدام** (`/Home/Guide`).

.NET 8 target, ASP.NET Core/EF/Identity 8.0.31, SQL Server 2019+ / LocalDB on Windows. The current build uses SDK 9.0.318 targeting net8.0 and runtime 8.0.31. The owner explicitly selected .NET 8 on 15 October 2026 under the SRS supported-alternative clause. No .NET 10 upgrade is required for this delivery. IIS requires the .NET 8 hosting bundle; the published application is also verified locally on Kestrel in Production over HTTPS.

From this directory:

```powershell
dotnet tool restore
dotnet restore HRMS.sln
dotnet build HRMS.sln
$env:ASPNETCORE_ENVIRONMENT = "Development"
# LocalDB development example; use an environment variable/secret store for a real SQL login.
$env:ConnectionStrings__HRMS = "Server=(localdb)\MSSQLLocalDB;Database=HRMSnew;Trusted_Connection=True;TrustServerCertificate=True"
dotnet ef database update --project HRMS.Infrastructure --startup-project HRMS
dotnet run --project HRMS
```

Visit the HTTPS address printed by the app. On an empty database use `/Account/Setup` if initial setup is enabled, or use the explicit demo seed below. Swagger is available at `/swagger` only in Development. Do not enable initial administrator setup on a public deployment.

## Local administrator creation and recovery

These commands work **only in Development** and connect to the same `ConnectionStrings:HRMS` as the web app. They do not run during a normal startup, do not apply migrations, and never change a stored password merely because a secret value changed. Use them from this solution directory after applying the migration to the intended database:

```powershell
$env:ASPNETCORE_ENVIRONMENT = "Development"
dotnet user-secrets set "LocalAdmin:Email" "your-admin@example.com" --project .\HRMS\HRMS.csproj
dotnet run --project HRMS --no-launch-profile -- --admin-status
```

Set the password locally without putting it in shell history or command arguments. It must have at least 10 characters including uppercase, lowercase, a digit and a symbol:

```powershell
$securePassword = Read-Host "Local administrator password" -AsSecureString
$passwordPointer = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($securePassword)
try {
    $passwordValue = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($passwordPointer)
    @{ LocalAdmin = @{ Password = $passwordValue } } |
        ConvertTo-Json -Compress |
        dotnet user-secrets set --project .\HRMS\HRMS.csproj
} finally {
    [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($passwordPointer)
    Remove-Variable passwordValue, securePassword -ErrorAction SilentlyContinue
}
```

Run the operation matching the status output:

```powershell
dotnet run --project HRMS --no-launch-profile -- --admin-create          # Create missing account and role; existing password stays unchanged
dotnet run --project HRMS --no-launch-profile -- --admin-check-password  # Compare the local secret with the stored Identity hash; no lockout increment
dotnet run --project HRMS --no-launch-profile -- --admin-reset-password  # Explicit Identity token-based reset
dotnet run --project HRMS --no-launch-profile -- --admin-unlock          # Explicitly clear lockout, if reported
dotnet run --project HRMS --no-launch-profile -- --admin-activate        # Explicitly activate account, only if linked employee permits sign-in
dotnet run --project HRMS --no-launch-profile -- --admin-status
```

`--admin-create` ensures all six application roles exist and assigns only `SystemAdministrator` to the selected account. This also makes the other roles available when creating users through the UI. A linked employee who is deleted or inactive still cannot sign in; restore/reactivate that employee through the authorized HR workflow. If the displayed database differs from the one you migrated or the database is unreachable, correct `ConnectionStrings:HRMS` first. Secrets are stored in the local ASP.NET Core User Secrets store, outside source control. After recovery, remove the password secret if no longer needed with `dotnet user-secrets remove "LocalAdmin:Password" --project .\HRMS\HRMS.csproj`.

## Optional demonstration data — empty development database only

For an existing Development database, `dotnet run --project HRMS --no-launch-profile -- --seed-reference-data` adds six starter leave types without creating users or changing existing definitions. Allowances are evaluation settings, not an approved employment policy. Administrators and HR managers can add/edit them at `/LeaveManagement/Types`. Re-running the command skips types matching an existing English or Arabic name; it does not run on normal startup.

```powershell
# Choose a unique development-only password satisfying Identity's complexity rules.
$env:HRMS_DEMO_PASSWORD = Read-Host "Development demo password"
dotnet run --project HRMS -- --seed-demo
Remove-Item Env:HRMS_DEMO_PASSWORD
```

This creates a department, position, annual/unpaid leave types and six linked users. Account names are `systemadministrator@example.test`, `hrmanager@example.test`, `hrofficer@example.test`, `departmentmanager@example.test`, `employee@example.test`, `auditor@example.test`. No password is shipped. Seed refuses non-Development environments and databases containing accounts or employees. The seed performs migrations; review them first for an existing database.

## Verification

```powershell
dotnet test HRMS.sln --logger trx
# SQL tests create/drop only a generated HRMS_Test_<guid> database.
# Supply a test SQL instance whose login has CREATE DATABASE permission.
$env:HRMS_TEST_SQLSERVER = "Server=(localdb)\MSSQLLocalDB;Integrated Security=True;TrustServerCertificate=True"
dotnet test HRMS.sln --logger trx
```

Without HRMS_TEST_SQLSERVER, SQL tests are explicitly skipped, not passed. The handoff requires the SQL gate. Use `scripts/verify.ps1` to reject a missing SQL connection before running acceptance checks.

An earlier sandbox could not launch VSTest. The current local repair ran `dotnet test` successfully with the five SQL tests explicitly skipped. `tools/VerificationRunner` remains a serial fallback that invokes the same xUnit test methods and InlineData rows, keeps class fixtures, reports skips, and returns nonzero for failures. It is not a replacement for VSTest in normal CI and supports only the patterns used in this solution:

```powershell
dotnet run --project tools/VerificationRunner
```

See `docs/VERIFICATION.md` for the actual method/results, `docs/REQUIREMENTS_TRACEABILITY.md` for acceptance gaps, `docs/DATABASE.md` for migration preflight, and `docs/DEPLOYMENT.md` for IIS deployment.


## Organization starter data and role permissions
See [Arabic role and access comparison](docs/ROLES_AND_ACCESS_AR.md). Development-only command: dotnet run --project HRMS --configuration Release --launch-profile http -- --seed-organization-data. It links existing users and fills departments, job titles and employees with equal department headcounts; it does not create passwords, attendance or leave requests.
