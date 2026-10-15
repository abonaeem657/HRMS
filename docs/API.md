# API v1

Development OpenAPI: `/swagger/v1/swagger.json`; UI `/swagger`. Uses Identity cookie authentication. This is not a bearer-token API. A trusted same-origin client first signs in, then gets `/api/v1/antiforgery`, and sends `X-CSRF-TOKEN` plus its cookie on POST/PUT/DELETE. CSRF is not disabled for JSON.

API v1 has a fixed-window limit of 120 requests per minute per authenticated user (per remote IP before authentication), with no queue. Requests over the limit receive HTTP 429.

| Method | Route | Role / behavior |
|---|---|---|
| GET | /api/v1/employees | Admin/HR; search, departmentId, status, sortBy, sortDescending, page, pageSize; active default; `status=__all__` includes inactive employees |
| GET | /api/v1/employees/{id} | Admin/HR; 404 absent/deleted |
| POST | /api/v1/employees | Admin/HR; validated input; 201 |
| PUT | /api/v1/employees/{id} | Admin/HR; send base64 rowVersion from GET; 404 absent, 409 stale |
| DELETE | /api/v1/employees/{id} | Admin/HR; soft deletion, 204 / 404 |
| GET | /api/v1/departments | Admin/HR; search, isActive, page, pageSize |
| GET | /api/v1/leave-requests | HR/all or scoped manager/employee; status, leaveTypeId, year, page, pageSize |
| GET | /api/v1/leave-requests/{id} | Same role/record checks; inaccessible other employee => 403 |
| POST | /api/v1/leave-requests | Linked employee request; 201/400 |
| GET | /api/v1/public-holidays/{year} | Reports roles; countryCode defaults JO; 502 provider failure, 504 timeout |

Page sizes are capped at 100; departments/leave return a page array, employees return page metadata. Year range is 2000–2100. HireDate is required; dates use yyyy-MM-dd, tokens base64. Error bodies use ProblemDetails/ValidationProblemDetails where supplied; empty API errors, including cookie challenge/forbid and antiforgery failures, are completed by status-code middleware with ProblemDetails and a traceId.

Example employee body (for PUT additionally include rowVersion from GET):

```json
{"employeeNumber":"E-101","firstName":"Ahmad","lastName":"Example","workEmail":"ahmad@example.test","hireDate":"2026-01-01","employmentStatus":"Active","employmentType":"FullTime","departmentId":1,"positionId":1}
```

The holiday client validates inputs and response data, uses a 15-second timeout and propagates request cancellation. No external response can mutate HR data. Mock handler tests cover success, bad JSON/data, HTTP failure and cancellation; an HTTP endpoint test covers provider timeout.

## Web request limits and asynchronous navigation

The UI also has per-minute limits, partitioned by authenticated user ID (or remote IP for anonymous requests): 120 GET/HEAD requests and a separate 30 mutation requests. The API keeps its own 120/minute policy; login/setup POST endpoints keep their dedicated 10/minute policy. Static assets bypass the limiter. Rejections return 429, application/problem+json and Retry-After when available.

Signed-in internal pages, GET filters, paging links and regular business POST forms use fetch with same-origin credentials. Anti-forgery form fields remain included. The X-HRMS-Navigation header requests the complete HTML page, allowing the browser to replace only main content. Existing modal requests retain their X-Requested-With behavior. Login/logout, account password changes and language changes keep full navigation. There is no automatic retry of mutations after a network failure; users are told to inspect the record before submitting again. A 429 preserves the current page and form entries.

The sidebar is a Bootstrap offcanvas drawer, closed initially at every viewport width. Its button opens it; Close, Escape and the backdrop dismiss it. Focus handling and background scroll locking follow Bootstrap offcanvas behavior.