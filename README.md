# HRMS | Human Resources Management System

**English** | [العربية](README.ar.md)

HRMS is an ASP.NET Core MVC application for employee records, attendance, leave, reporting, and audit. It uses Entity Framework Core and SQL Server, supports Arabic and English (RTL and LTR), and includes six application roles.

![HRMS dashboard](docs/screenshots/dashboard.png)

## Screenshots

| Sign in | Employee management |
|---|---|
| ![Sign-in page](docs/screenshots/login.png) | ![Employee list](docs/screenshots/employees.png) |

![Attendance page](docs/screenshots/attendance.png)

> Screenshots use local demonstration data and contain no real employee data.

## Features

- Employee records and lifecycle, departments, and job positions.
- Attendance check-in/out, multiple completed sessions per day, and authorized corrections with an audit trail.
- Date-only annual, sick, and unpaid leave requests, balances, review, and approval.
- Dashboard, reports, and audit log with PDF and Excel downloads that respect filters and role permissions.
- Six roles and permissions with responsive Arabic and English interfaces.
- MVC pages and API endpoints. Swagger is available in Development only.

## Reports and leave

1. Open a report, choose the filters and sort order, then apply them.
2. Select **Download PDF** or **Download Excel**. The file includes all matching results within your permissions, not just the six rows on the current page.
3. Employees use **My leave report** to download their own requests and balances. Department managers can export only their department scope.

Leave types are fixed to **Annual**, **Sick**, and **Unpaid**. Requests contain start/end dates and a reason, without time fields or hourly permissions. Under **Leave → Leave types**, administrators and HR managers can edit the annual/sick allowance or enable/disable a type. Initial annual and sick allowances are 14 days each; existing configured values are preserved. Legacy types are disabled without deleting request history.

PDF generation uses a local browser on the server and supports Arabic/English. Set `ReportExport__BrowserPath` if the browser is installed elsewhere. Excel is a real `.xlsx` workbook. See the [trainer update and export setup](docs/TRAINER_UPDATE_2026_10_19_AR.md) for the selected training policies and limits.

## Requirements

- .NET SDK 8 or later and the .NET 8 runtime.
- SQL Server 2019 or later. LocalDB is suitable for Windows development.
- Chrome or Edge on the application host for PDF generation; Chrome for browser acceptance tests. Excel export does not require Microsoft Office.

The solution targets .NET 8 and uses ASP.NET Core, EF Core, and Identity 8.0.31. Review [verification and compatibility notes](docs/VERIFICATION.md) and [acceptance requirements and gaps](docs/ACCEPTANCE_AR.md) before deployment.

## Run locally

From the repository directory in PowerShell:

```powershell
dotnet tool restore
dotnet restore HRMS.sln
if (!(Test-Path .\HRMS\appsettings.Development.json)) {
    Copy-Item .\HRMS\appsettings.Development.example.json .\HRMS\appsettings.Development.json
}
$env:ASPNETCORE_ENVIRONMENT = "Development"
dotnet ef database update --project HRMS.Infrastructure --startup-project HRMS
dotnet run --project HRMS -- --seed-reference-data
dotnet run --project HRMS
```

The example configures a local database named `HRMSnew` and contains no passwords. Update the local `appsettings.Development.json` or provide `ConnectionStrings__HRMS` as an environment variable for another database. Do not commit real credentials.

To allow creation of the first administrator on an empty development database, enable the setup page in User Secrets:

```powershell
dotnet user-secrets set "Identity:AllowInitialAdministratorSetup" "true" --project .\HRMS\HRMS.csproj
```

Open `/Account/Setup` and create the account. Then turn the setting off:

```powershell
dotnet user-secrets set "Identity:AllowInitialAdministratorSetup" "false" --project .\HRMS\HRMS.csproj
```

ASP.NET Core User Secrets are stored locally outside the repository. Never enable initial administrator setup on a public deployment. For demo data and account recovery, see the [Arabic delivery guide](docs/DELIVERY_AR.md) and [user walkthrough](docs/USER_WALKTHROUGH_AR.md).

## Database

EF Core migrations are under `HRMS.Infrastructure/Persistence/Migrations`. Set `ConnectionStrings:HRMS` and apply them with the command above. See the [database guide](docs/DATABASE.md) for migration preflight and backups. Do not run demo seeding or SQL tests against a production database.

## Build and test

```powershell
dotnet build HRMS.sln
dotnet test HRMS.sln
```

Unit, architecture, and HTTP tests run without extra configuration. SQL and browser scenarios are skipped unless their prerequisites are configured; a skipped scenario is not a verified acceptance result. SQL and browser tests require a local SQL Server connection with permission to create temporary databases:

```powershell
$env:HRMS_TEST_SQLSERVER = "Server=(localdb)\MSSQLLocalDB;Integrated Security=True;TrustServerCertificate=True"
dotnet test HRMS.sln
Remove-Item Env:HRMS_TEST_SQLSERVER
```

Database tests use temporary `HRMS_Test_<guid>` databases and remove them after each test. See the [verification guide](docs/VERIFICATION.md) for details.

Latest complete verification (19 October 2026): **103 passed, 0 failed, 0 skipped** — 50 unit, 7 architecture, and 46 integration/SQL/HTTP/browser tests. To reproduce the published application gate with SQL Server and Chrome:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/verify-published.ps1
```

## Solution structure

| Project | Responsibility |
|---|---|
| `HRMS` | Web application, MVC, and API |
| `HRMS.Application` | HR services and use case rules |
| `HRMS.Domain` | Domain entities |
| `HRMS.Infrastructure` | EF Core, SQL Server, Identity, and integrations |
| `tests` | Unit, integration, architecture, and browser tests |

## Documentation

- [Architecture](docs/ARCHITECTURE.md) and [API](docs/API.md)
- [Deployment](docs/DEPLOYMENT.md) and [database](docs/DATABASE.md)
- [Roles and permissions](docs/ROLES_AND_ACCESS_AR.md)
- [Verification results](docs/VERIFICATION.md) and [acceptance requirements](docs/ACCEPTANCE_AR.md)
- [Arabic user guide](docs/HRMS_USER_GUIDE_AR.md) and [setup walkthrough](docs/USER_WALKTHROUGH_AR.md)
