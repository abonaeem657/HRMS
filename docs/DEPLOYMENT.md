# Deployment on .NET 8

1. Confirm framework choice and HR policies; complete SQL/HTTP acceptance tests on an isolated database. Back up the target database and review an idempotent migration script. Do not seed production.
2. Install IIS with ASP.NET Core .NET 8 Hosting Bundle matching the retained framework. Create a dedicated application pool (No Managed Code), site, and HTTPS certificate binding.
3. `dotnet publish HRMS -c Release -o publish`. Deploy the publish output, not the source ZIP. Give the app identity read/execute permission, and write only to explicit log/data-protection locations.
4. Set `ASPNETCORE_ENVIRONMENT=Production`, `ConnectionStrings__HRMS` through approved secret configuration, `Identity__AllowInitialAdministratorSetup=false`, and the Attendance timezone/late threshold. Production SQL must use encrypted connections with a trusted server certificate; do not use the development TrustServerCertificate setting.
5. Apply reviewed migrations using a separate migration identity. Grant the runtime identity only required table operations; audit records need INSERT/SELECT, not UPDATE/DELETE. Preserve Identity/Data Protection keys securely across redeploys and instances.
6. Configure HTTPS, secure persistent key storage, application logs, backup/restore and health monitoring. Swagger is intentionally Development-only. Configure forwarded headers only for explicitly trusted proxies if one is introduced.
7. Smoke-test login, employee create/update/delete/restore, manager leave scope, overnight attendance, Arabic/English, audit visibility and external-provider failure. Verify cookies are Secure/HttpOnly on HTTPS. Review security/dependency scan findings.
8. Rollback: stop writes, deploy the known previous application plus a compatible database schema. Restore a tested backup if a schema/data rollback is required; do not run Down blindly on a live database.

The Release publish output has been started locally in Production over HTTPS on Kestrel, and tested with Chrome, real Identity and an isolated SQL database. The acceptance script creates and deletes its own localhost certificate; it does not install a trusted public certificate. IIS is not installed on this workstation and no public site was configured. The IIS steps above apply when a hosting target is selected. GitHub publication is deferred by the owner.


## Verified local publish and browser acceptance

Run scripts/verify-published.ps1 from the solution directory. It creates artifacts/publish, starts that exact build in Production on a random HTTPS localhost port, exercises the six roles in Chrome against temporary SQL databases, and stops the test host. It does not change HRMSnew, publish to a public host, install IIS or seed a production database. For a persistent host, provide ConnectionStrings__HRMS, ASPNETCORE_ENVIRONMENT=Production, an HTTPS binding/certificate, and a managed service identity as described above.

## PDF and Excel exports
Excel is generated as Open XML without Microsoft Office. PDF requires Chrome/Edge on the app host, an Arabic-capable system font and a private writable temporary directory. Set ReportExport__BrowserPath to override browser detection. Keep the browser patched and allow the application identity to execute it; the app does not disable the browser sandbox. Rendering is local, limited to two concurrent browser processes with a 45-second timeout. Missing renderer configuration returns a localized 503 and does not create a fake PDF. See TRAINER_UPDATE_2026_10_19_AR.md.
