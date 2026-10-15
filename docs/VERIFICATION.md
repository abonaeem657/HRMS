# Current verification — 15 October 2026

SDK 9.0.318 targeting net8.0; runtime/EF/Identity 8.0.31. The owner explicitly selected .NET 8. See ACCEPTANCE_AR.md for final counts. Older raw historical logs do not describe this run.

## Reproduce

From the solution directory:

~~~powershell
powershell -ExecutionPolicy Bypass -File scripts/verify-published.ps1
~~~

This publishes Release to artifacts/publish and runs all test projects with LocalDB and installed Chrome. Override -TestSqlServer and -ChromePath if needed. SQL tests create and delete uniquely named databases; HRMSnew is not the test database.

## Evidence

- Unit tests cover domain rules, access policies, time-zone boundaries and external-provider behavior.
- Architecture tests check layer references, application service placement, scoped query contracts, controller dependencies and model/constraint metadata.
- SQL/HTTP tests cover migrations, employee lifecycle/concurrency/filters, references, real Identity logins for every role, profile ownership, leave reservations/reviews, attendance sessions/correction and request limits.
- Browser acceptance starts the published app in Production using HTTPS on a random localhost port with a temporary self-signed certificate. Chrome ignores that certificate error only in the isolated test context; the certificate file is deleted afterward.
- Browser scenarios cover six roles, Arabic/English, desktop/mobile, drawer and AJAX navigation, own profile and role guide, account creation/linking, leave submission/approval, two attendance sessions and targeted correction.
- Hosting is exercised locally, not on public infrastructure or installed IIS. DNS, trusted certificates, service accounts, database grants, backup and monitoring belong to the eventual host.
- GitHub publication is deferred at the owner's request.

Test passwords are generated in memory and not included in evidence or screenshots. Existing HRMSnew passwords are unavailable locally and were not reset or tested. Use your own known credentials for those accounts.