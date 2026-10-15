namespace HRMS.Application.Common;
public interface IBusinessClock
{
    DateOnly Today { get; }
    DateTime? ToLocal(DateTime? utc);
    DateTime StartOfDayUtc(DateOnly date);
}
