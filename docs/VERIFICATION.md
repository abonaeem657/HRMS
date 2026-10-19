# Current verification — 19 October 2026

SDK 9.0.318 targeting net8.0; runtime/EF/Identity 8.0.31. The owner explicitly selected .NET 8. See ACCEPTANCE_AR.md for final counts. Obsolete raw logs and the former reflection-based runner were removed; use the TRX reports produced by the commands below.

## Reproduce

From the solution directory:

~~~powershell
powershell -ExecutionPolicy Bypass -File scripts/verify-published.ps1
~~~

This publishes Release to artifacts/publish and runs all test projects with LocalDB and installed Chrome. Override -TestSqlServer and -ChromePath if needed. SQL tests create and delete uniquely named databases; HRMSnew is not the test database.

## Evidence

- Unit tests cover domain rules, access policies, time-zone boundaries and external-provider behavior.
- Architecture tests check layer references, application service placement, scoped query contracts, controller dependencies and model/constraint metadata.
- Login tests verify the warning after the second failure, one-hour lockout after the fifth, rejection of the correct password during lockout and successful login after expiry.
- Results tests verify six rows, the next page, showing all 107 filtered employees and returning to six, with API/service page-size caps preserved. Browser acceptance exercises AJAX pagination.
- SQL/HTTP tests cover migrations, employee lifecycle/concurrency/filters, references, real Identity logins for every role, profile ownership, leave reservations/reviews, attendance sessions/correction and request limits.
- Browser acceptance starts the published app in Production using HTTPS on a random localhost port with a temporary self-signed certificate. Chrome ignores that certificate error only in the isolated test context; the certificate file is deleted afterward.
- Browser scenarios cover six roles, Arabic/English, desktop/mobile, drawer and AJAX navigation, own profile and role guide, account creation/linking, leave submission/approval, two attendance sessions and targeted correction.
- Hosting is exercised locally, not on public infrastructure or installed IIS. DNS, trusted certificates, service accounts, database grants, backup and monitoring belong to the eventual host.
- GitHub publication is deferred at the owner's request.

Test passwords are generated in memory and not included in evidence or screenshots. Existing HRMSnew passwords are unavailable locally and were not reset or tested. Use your own known credentials for those accounts.
## PDF alignment verification

The 15 October full gate passed 99 tests: 48 unit, 7 architecture and 44 integration/SQL/HTTP/browser, with zero failures and skips. PdfRequirementsTests captures actual SQL commands to verify OFFSET/FETCH before materialization for organization, accounts, leave types, requests and balances; account paging uses two read commands rather than a role lookup per user. HTTP tests verify localized API validation errors. Report tests cover database paging for dashboard/headcount and ascending/descending leave dates. No new migration or main database changes are involved.

See PDF_CHANGE_AUDIT_AR.md for the distinction between mandatory requirements, allowed UI choices and policy/delivery decisions still awaiting agreement. The current NuGet vulnerability check reported no vulnerable packages in the configured feeds; it is not an independent security assessment.

The detailed review in PDF_DETAILED_REVIEW_AR.md records unresolved policy, deletion-workflow and hosting/delivery interpretations explicitly. New unit tests cover application uniqueness/required references, overlap denial, unauthorized approval, audit generation and the same DashboardMetrics expressions executed by SQL. A SQL test preloads the same leave version in two contexts, commits approval in one, and verifies that a stale rejection cannot overwrite it or create a successful rejection audit. Organization sort tests execute ordering before SQL pagination. The final 99-test gate produced TRX files timestamped 22:45:44 and 22:45:49 on 15 October 2026, with the browser integration run finishing at 22:48:10.

## Trainer update verification — 19 October 2026
The final full published gate passed 103 tests: 50 unit, 7 architecture, 46 integration/SQL/HTTP/browser; zero failed and zero skipped. Final TRX timestamps: 11:12:51 and 11:12:54 local on 19 October; the last integration/browser gate completed at 11:14:22. A preceding complete 103-test run also passed. The final follow-up explicitly verified that report type filters also contain only the three supported types. New tests validate Open XML relationships, Arabic text, numeric cells and formula-safe text, genuine PDF bytes, full filtered results beyond six rows, manager scope despite departmentId tampering, employee-only report exports and browser download without workspace navigation. The three-type policy test verifies legacy deactivation and history preservation, rejection of a manually reactivated unsupported type, and prevention of adding a fourth type. Identity and browser tests confirm date-only leave forms; submitted obsolete time fields do not populate new records.
An earlier full run had a browser navigation timeout while opening Home/Guide; its other 102 tests passed. Diagnostic context was added to that failure path without increasing timeouts or skipping the test. The final complete rerun passed, including all role/language/viewport cases. This records the initial failure rather than calling it a confirmed product defect or hiding it.
The Development reference-data command was applied to HRMSnew. A separate read-only check confirmed Annual/Sick/Unpaid active, Emergency/Bereavement/Study inactive; existing allowances remained 14/14/0 for the supported types. Main database counts remained 24 employees, 6 accounts, 2 attendance rows and 2 leave requests. No schema migration or password change was made. The temporary policy-check console under ignored artifacts is not part of the application or solution.
PDF rendering requires a local Chrome/Edge installation on the host and is verified with installed Chrome here. Excel needs no Office installation. See TRAINER_UPDATE_2026_10_19_AR.md for usage, renderer configuration and the simple selected policies. Public hosting, production DB grants and GitHub remain deferred.
