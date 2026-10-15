# Historical implementation notes

The following notes predate the repairs. For current behavior, use POLICIES.md, REQUIREMENTS_TRACEABILITY.md and VERIFICATION.md.

# HRMS implementation notes

## Attendance policy currently used

The requirements do not define working hours, a weekly schedule, holidays, shift
lengths, or an overnight-shift policy. The current development defaults are
therefore explicit and configurable rather than treated as organizational
policy:

- Business dates and manual entry times use `Attendance:BusinessTimeZoneId`
  (`Jordan Standard Time` by default in Development).
- The configurable late threshold is `Attendance:LateAfterLocalTime`
  (`09:15`). A check-in exactly at the threshold is not late.
- A unique employee/date record prevents duplicate check-ins.
- Check-in and check-out timestamps are persisted in UTC. Manual entry accepts
  local `HH:mm` values and converts them to UTC. Invalid daylight-saving local
  times are rejected; ambiguous local times use the platform's standard-time
  conversion.
- Overnight automatic check-out is not supported. A check-out after the
  business date changes must be corrected by an authorized HR user.
- Worked hours are elapsed UTC hours rounded to two decimal places using
  .NET's default midpoint-to-even rounding.
- A past-day check-in without a check-out is displayed as missing check-out.
  The system does not synthesize absent days because the applicable workweek
  and holiday calendar have not been supplied.

The business owner should confirm these settings before production use.

## Leave balance convention

`DefaultDaysPerYear` is treated as the configured yearly allowance. Used days
are counted inclusively as calendar days for approved requests and prorated by
calendar-year overlap. Pending requests do not consume the displayed balance.
Weekend, holiday, partial-day, carryover, and accrual rules are not supplied,
so this display is informational and no balance is used to auto-approve or
reject a request.

## API access

The versioned API is under `/api/v1`. It uses the application's Identity
cookie, role authorization, JSON validation, and Problem Details-style error
responses. For state-changing API calls, first request
`GET /api/v1/antiforgery`, retain the antiforgery cookie, then send the returned
`requestToken` in the `X-CSRF-TOKEN` header. Public-holiday reference data is
retrieved through the Nager.Date public API; external errors are surfaced to
the caller and do not alter internal HR data.

Implemented endpoints:

- `GET /api/v1/employees` and `GET /api/v1/employees/{id}` (HR data readers)
- `POST`, `PUT`, and `DELETE /api/v1/employees[/{id}]` (employee managers;
  DELETE is a soft delete)
- `GET /api/v1/departments` (HR data readers)
- `GET /api/v1/leave-requests[/{id}]` and `POST /api/v1/leave-requests`
  (records are scoped to the linked employee or department manager)
- `GET /api/v1/public-holidays/{year}?countryCode=JO` (report readers)
