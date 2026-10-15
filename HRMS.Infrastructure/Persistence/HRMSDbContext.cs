using HRMS.Domain.Auditing;
using HRMS.Domain.Attendance;
using HRMS.Domain.Employees;
using HRMS.Domain.LeaveManagement;
using HRMS.Domain.Organization;
using HRMS.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace HRMS.Infrastructure.Persistence;

public class HRMSDbContext(DbContextOptions<HRMSDbContext> options)
    : IdentityDbContext<HRMSUser, IdentityRole, string>(options)
{
    public DbSet<Employee> Employees => Set<Employee>();

    public DbSet<Department> Departments => Set<Department>();

    public DbSet<Position> Positions => Set<Position>();

    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    public DbSet<AttendanceRecord> AttendanceRecords => Set<AttendanceRecord>();

    public DbSet<LeaveType> LeaveTypes => Set<LeaveType>();

    public DbSet<LeaveRequest> LeaveRequests => Set<LeaveRequest>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Department>(entity =>
        {
            entity.HasKey(department => department.DepartmentId);

            entity.HasMany(department => department.Employees)
                .WithOne(employee => employee.Department)
                .HasForeignKey(employee => employee.DepartmentId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(department => department.Manager)
                .WithMany()
                .HasForeignKey(department => department.ManagerEmployeeId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Position>(entity =>
        {
            entity.HasKey(position => position.PositionId);

            entity.Property(position => position.Code).HasMaxLength(50).IsRequired();
            entity.HasIndex(position => position.Code).IsUnique();
            entity.Property(position => position.Title).IsRequired();
            entity.Property(position => position.Description).HasMaxLength(2000);
            entity.Property(position => position.Grade).HasMaxLength(50);

            entity.HasOne(position => position.Department)
                .WithMany()
                .HasForeignKey(position => position.DepartmentId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(position => position.Employees)
                .WithOne(employee => employee.Position)
                .HasForeignKey(employee => employee.PositionId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Employee>(entity =>
        {
            entity.HasKey(employee => employee.EmployeeId);
            entity.Property(e => e.RowVersion).IsRowVersion();
            entity.HasQueryFilter(employee => !employee.IsDeleted);

            entity.Property(employee => employee.EmployeeNumber)
                .HasMaxLength(450)
                .IsRequired();

            entity.HasIndex(employee => employee.EmployeeNumber)
                .IsUnique();

            entity.HasOne(employee => employee.Manager)
                .WithMany(manager => manager.DirectReports)
                .HasForeignKey(employee => employee.ManagerEmployeeId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.Property(employee => employee.IdentityUserId)
                .HasMaxLength(450);

            entity.Property(employee => employee.DeletedByUserId)
                .HasMaxLength(450);

            entity.HasIndex(employee => employee.IdentityUserId)
                .IsUnique()
                .HasFilter("[IdentityUserId] IS NOT NULL");

            entity.HasOne<HRMSUser>()
                .WithMany()
                .HasForeignKey(employee => employee.IdentityUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<HRMSUser>(entity =>
        {
            entity.Property(user => user.IsActive)
                .HasDefaultValue(true);
        });

        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.HasKey(log => log.AuditLogId);

            entity.Property(log => log.Action)
                .HasMaxLength(100)
                .IsRequired();
            entity.Property(log => log.EntityName)
                .HasMaxLength(100)
                .IsRequired();
            entity.Property(log => log.EntityId).HasMaxLength(256);
            entity.Property(log => log.OccurredAtUtc).IsRequired();
            entity.Property(log => log.CorrelationId).HasMaxLength(128);
            entity.Property(log => log.ChangesJson).IsRequired();
            entity.Property(log => log.Outcome)
                .HasMaxLength(32)
                .IsRequired();
            entity.Property(log => log.Source).HasMaxLength(256);

            entity.HasOne<HRMSUser>()
                .WithMany()
                .HasForeignKey(log => log.ActorUserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(log => log.OccurredAtUtc);
            entity.HasIndex(log => new { log.ActorUserId, log.OccurredAtUtc });
            entity.HasIndex(log => new { log.EntityName, log.EntityId });
        });

        modelBuilder.Entity<AttendanceRecord>(entity =>
        {
            entity.HasKey(record => record.AttendanceId);
            entity.HasQueryFilter(record => !record.Employee!.IsDeleted);
            entity.Property(e => e.RowVersion).IsRowVersion();

            entity.Property(record => record.Status)
                .HasMaxLength(32)
                .IsRequired();

            entity.Property(record => record.Notes)
                .HasMaxLength(1000);

            entity.Property(record => record.TotalHours)
                .HasPrecision(8, 2);

            entity.HasOne(record => record.Employee)
                .WithMany(employee => employee.AttendanceRecords)
                .HasForeignKey(record => record.EmployeeId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(record => new { record.EmployeeId, record.WorkDate });

            entity.HasIndex(record => record.WorkDate);
            entity.HasIndex(record => record.EmployeeId).IsUnique()
                .HasDatabaseName("IX_AttendanceRecords_OpenShift")
                .HasFilter("[CheckInUtc] IS NOT NULL AND [CheckOutUtc] IS NULL");
        });

        modelBuilder.Entity<LeaveType>(entity =>
        {
            entity.HasKey(type => type.LeaveTypeId);

            entity.Property(type => type.Name)
                .HasMaxLength(100)
                .IsRequired();

            entity.Property(type => type.NameAr)
                .HasMaxLength(100)
                .IsRequired();

            entity.HasIndex(type => type.Name)
                .IsUnique();
            entity.HasIndex(type => type.NameAr)
                .IsUnique();
        });

        modelBuilder.Entity<LeaveRequest>(entity =>
        {
            entity.HasKey(request => request.LeaveRequestId);
            entity.HasQueryFilter(request => !request.Employee!.IsDeleted);

            entity.Property(request => request.Reason)
                .HasMaxLength(1000)
                .IsRequired();

            entity.Property(request => request.Status)
                .HasMaxLength(32)
                .IsRequired();

            entity.Property(request => request.ReviewNotes)
                .HasMaxLength(1000);
            entity.Property(request => request.RowVersion)
                .IsRowVersion();

            entity.HasOne(request => request.Employee)
                .WithMany(employee => employee.LeaveRequests)
                .HasForeignKey(request => request.EmployeeId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(request => request.LeaveType)
                .WithMany()
                .HasForeignKey(request => request.LeaveTypeId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<HRMSUser>()
                .WithMany()
                .HasForeignKey(request => request.ReviewedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(request => new { request.EmployeeId, request.StartDate, request.EndDate });
            entity.HasIndex(request => request.Status);
        });
    }

    public override int SaveChanges() => SaveChanges(acceptAllChangesOnSuccess: true);

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        EnsureAuditLogsAreAppendOnly();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        SaveChangesAsync(acceptAllChangesOnSuccess: true, cancellationToken);

    public override Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        EnsureAuditLogsAreAppendOnly();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void EnsureAuditLogsAreAppendOnly()
    {
        if (ChangeTracker.Entries<AuditLog>().Any(entry =>
                entry.State is EntityState.Modified or EntityState.Deleted))
        {
            throw new InvalidOperationException("Audit log records are append-only.");
        }
    }
}
