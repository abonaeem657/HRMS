# Architecture and module boundaries

One deployable ASP.NET Core MVC/API monolith targeting .NET 8, selected by the owner on 15 October 2026.

~~~mermaid
flowchart TD
 Web[Web: MVC, Razor, API, HTTP authorization] --> App[Application: use cases, validation, ports, DTOs]
 App --> Domain[Domain: entities and business invariants]
 Infra[Infrastructure: EF, SQL, Identity adapters, external HTTP] --> App
 Infra --> Domain
 Infra --> SQL[SQL Server]
~~~

EmployeeService, DepartmentService, PositionService, AttendanceService, LeaveService and UserAccountService are implemented in Application. They coordinate validation, domain operations, audit entries and transactions. Domain has no dependency on Application, EF, ASP.NET or Infrastructure. Controllers delegate state-changing operations to application contracts.

Infrastructure implements module-scoped data ports. Each module can mutate its own record sets. Foreign records are detached read queries; an accidental domain mutation cannot be saved through a foreign module's session. Identity changes employee links through IEmployeeAccountLinks instead of employee persistence.

IdentityStore and RoleStore adapt UserManager and RoleManager. Application receives account DTOs and neutral results; hashing, reset tokens and Identity storage remain in Infrastructure. AccountController retains HTTP sign-in/session concerns. ReportingService is an Infrastructure read-model adapter behind IReportingService, with no writes. RecordAccess supplies actor/relationship facts; audit storage is append-only.

## Query and transaction ports

Module interfaces live in their corresponding Application modules. Common contains only shared async execution/transaction contracts and neutral data-conflict exceptions. IQueryable expressions preserve SQL-side filtering, sorting, paging and projections. QueryDriver in Infrastructure executes them asynchronously. Application references neither EF, SqlClient, DbContext, Identity persistence types nor Web.

This adapter moves orchestration inward while preserving translated queries and atomic audit transactions. It is not an extra generic CRUD repository over another repository. Adapters share the scoped EF context/transaction. Provider exceptions become DataConcurrencyException or DataWriteException; Web maps conflicts to HTTP 409.

IQueryable is an intentional contract trade-off: a different provider must implement query execution and verify translation behavior. Unchanged queries are not guaranteed to work with every database.

Architecture tests enforce inward dependencies, service placement/constructor boundaries, read-only foreign ports, controller DbContext exclusion, model agreement and concurrency/filter metadata. SQL tests verify detached foreign mutations do not persist. Real Identity, employee, leave, attendance and browser tests verify behavior through these adapters.
## Database pagination and counts

PageRequest and PageResult in Application.Common describe read pagination. The DataQueries.ToPageAsync helper counts and applies Skip/Take to IQueryable before materialization through the module query port. The website only reads page parameters and builds navigation links; it no longer slices loaded lists in Razor. Reporting uses the equivalent EF read projection in Infrastructure, including separately paged dashboard groups. Account rows project user, employee and role in SQL; singleton lookup and dashboard counts do not load every account. Unpaged reference-data methods remain for authorized select controls and explicit all-results requests, rather than being used to implement normal list pagination.
