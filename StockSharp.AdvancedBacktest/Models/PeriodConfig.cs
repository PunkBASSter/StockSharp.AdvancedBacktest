namespace StockSharp.AdvancedBacktest.Models
{
    public class PeriodConfig
    {
        public DateTimeOffset StartDate { get; set; }
        public DateTimeOffset EndDate { get; set; }

        public virtual bool IsValid() => StartDate < EndDate;
    }
}