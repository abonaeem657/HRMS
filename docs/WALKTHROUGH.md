# Acceptance walkthrough

Use the explicit development seed and accounts listed in README. Use a separate SQL test instance.

1. HR manager creates a department, position and employee. Attempt duplicate position code and invalid department/position. Confirm useful rejection.
2. Open an employee edit in two sessions. Save the first, then the stale second: expect 409. Check the audit record without contact values.
3. Soft-delete an employee; lists, leave queries and dashboards must exclude it. Restore through Employees/Deleted. Verify employee number was never reusable. Test an already signed-in linked account during deactivation/deletion.
4. Sign in as a department manager; confirm department assignment, report scope, and deny another department's leave URL. Confirm self-approval is denied; HR may process requests according to documented policy.
5. Submit overlapping leave requests and approve/reject repeatedly/concurrently; confirm only valid state changes persist, and audits match.
6. Correct attendance using a previous date: 22:00 check-in, 02:00 checkout with the next check-out date selected = 4 hours. Reject checkout without check-in, reversed times without the correct check-out date, future timestamps and intervals above 24 hours. Test concurrent check-ins and checkouts. Open a correction in two sessions and verify that saving the stale token is rejected.
7. Filter/sort/page reports; verify options survive paging. HR Officer must see no audit activity; Auditor is read-only.
8. Switch Arabic/English on full pages and modal forms, exercise loading/errors/save/status changes, keyboard navigation and accessible close buttons. Test narrow/mobile viewport and actual Arabic names/search collation.
9. Use Swagger in Development, cookie/CSRF correctly, validate missing HireDate=400, missing employee=404, stale version=409, unauthenticated=401 and forbidden=403. Simulate holiday service timeout=504 and HTTP errors=502.
10. Execute scripts/verify.ps1 with SQL configured and retain TRX output. Document policy approvals and any acceptance exceptions.
