using HRMS.Domain.Attendance;
using HRMS.Domain.Employees;
using HRMS.Domain.LeaveManagement;
using HRMS.Application.Identity;
using HRMS.Application.Common;
using System.Globalization;
namespace HRMS.UnitTests;
public class BusinessRulesTests
{
    private static DateTime Start => new(2026, 1, 1, 22, 0, 0, DateTimeKind.Utc);
    [Fact] public void OvernightShiftCalculatesHours() { var r = AttendanceRecord.CreateCheckIn(1, new(2026,1,1), Start); r.RecordCheckOut(Start.AddHours(4)); Assert.Equal(4m,r.TotalHours); }
    [Fact] public void CheckoutRequiresCheckin() => Assert.Throws<ArgumentException>(() => new AttendanceRecord(1,new(2026,1,1),null,Start,"Present"));
    [Fact] public void ReversedIntervalRejected() => Assert.Throws<ArgumentException>(() => new AttendanceRecord(1,new(2026,1,1),Start,Start.AddMinutes(-1),"Present"));
    [Fact] public void ExcessiveShiftRejected() { var r = AttendanceRecord.CreateCheckIn(1,new(2026,1,1),Start); Assert.Throws<ArgumentException>(()=>r.RecordCheckOut(Start.AddHours(25))); }
    [Fact] public void DuplicateCheckoutRejected() { var r = AttendanceRecord.CreateCheckIn(1,new(2026,1,1),Start); r.RecordCheckOut(Start.AddHours(2)); Assert.Throws<InvalidOperationException>(()=>r.RecordCheckOut(Start.AddHours(3))); }
    [Fact] public void MissingCheckoutPreserved() { var r = new AttendanceRecord(1,new(2026,1,1),Start,null,"Present"); Assert.Null(r.CheckOutUtc); Assert.Equal(0,r.TotalHours); }
    [Fact] public void HoursRoundedToTwoDecimals() { var r=AttendanceRecord.CreateCheckIn(1,new(2026,1,1),Start);r.RecordCheckOut(Start.AddMinutes(61));Assert.Equal(1.02m,r.TotalHours); }
    [Fact] public void NonUtcRejected() => Assert.Throws<ArgumentException>(()=>AttendanceRecord.CreateCheckIn(1,new(2026,1,1),DateTime.SpecifyKind(Start,DateTimeKind.Local)));
    [Fact] public void InvalidLeaveDatesRejected() => Assert.Throws<ArgumentException>(()=>new LeaveRequest(1,1,new(2026,1,2),new(2026,1,1),"Annual"));
    [Fact] public void RepeatedApprovalRejected() { var r=new LeaveRequest(1,1,new(2026,1,1),new(2026,1,2),"Annual");r.Approve("manager");Assert.Throws<InvalidOperationException>(()=>r.Approve("manager")); }
    [Fact] public void RejectionStoresReason() { var r=new LeaveRequest(1,1,new(2026,1,1),new(2026,1,2),"Annual");r.Reject("manager","Coverage required");Assert.Equal("Rejected",r.Status);Assert.Equal("Coverage required",r.ReviewNotes); }
    [Fact] public void RejectionRequiresReason() { var r=new LeaveRequest(1,1,new(2026,1,1),new(2026,1,2),"Annual");Assert.Throws<ArgumentException>(()=>r.Reject("manager"," ")); }
    [Fact] public void InclusiveLeaveDays() => Assert.Equal(2,new LeaveRequest(1,1,new(2026,1,1),new(2026,1,2),"Annual").TotalDays);
    [Fact] public void EmployeeRequiresNumber() => Assert.Throws<ArgumentException>(()=>new Employee(" ","A","B","a@example.test",new(2026,1,1),"Active",1,1,"FullTime"));
    [Fact] public void EmployeeRequiresHireDate() => Assert.Throws<ArgumentOutOfRangeException>(()=>new Employee("1","A","B","a@example.test",default,"Active",1,1,"FullTime"));
    [Fact] public void EmployeeRejectsInvalidEmail() => Assert.Throws<ArgumentException>(()=>new Employee("1","A","B","invalid",new(2026,1,1),"Active",1,1,"FullTime"));
    [Theory]
    [InlineData(false,false,false,false,false,true,false)]
    [InlineData(true,false,false,true,false,false,true)]
    [InlineData(true,false,false,false,false,false,false)]
    [InlineData(true,false,true,false,true,true,true)]
    [InlineData(true,false,true,false,false,true,false)]
    [InlineData(true,false,true,true,true,true,false)]
    [InlineData(true,true,false,false,false,true,true)]
    public void RecordAuthorization(bool authenticated,bool hr,bool manager,bool owns,bool manages,bool review,bool expected)
        => Assert.Equal(expected,LeaveAccessPolicy.Allows(authenticated,hr,manager,owns,manages,review));
    [Theory][InlineData("en-US","Employee not found.")][InlineData("ar-JO","الموظف غير موجود.")]
    public void BusinessMessagesLocalized(string culture,string expected)
    { var before=CultureInfo.CurrentUICulture;try{CultureInfo.CurrentUICulture=CultureInfo.GetCultureInfo(culture);Assert.Equal(expected,Messages.Get("Business016"));}finally{CultureInfo.CurrentUICulture=before;} }
}
