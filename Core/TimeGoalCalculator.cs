using System;

namespace TimeFlow
{

    public static class TimeGoalCalculator
    {

        public static long ToSeconds(int years, int months, int days, int hours, int minutes, int seconds)
        {
            var baseDate = DateTime.Now;
            var targetDate = baseDate
                .AddYears(years)
                .AddMonths(months)
                .AddDays(days)
                .AddHours(hours)
                .AddMinutes(minutes)
                .AddSeconds(seconds);

            double totalSeconds = (targetDate - baseDate).TotalSeconds;
            return (long)Math.Round(totalSeconds);
        }

        public static (int years, int months, int days, int hours, int minutes, int seconds) FromSeconds(long totalSeconds)
        {
            var baseDate = DateTime.Now;
            var targetDate = baseDate.AddSeconds(totalSeconds);

            int years = targetDate.Year - baseDate.Year;
            if (targetDate.Month < baseDate.Month || 
                (targetDate.Month == baseDate.Month && targetDate.Day < baseDate.Day))
            {
                years--;
            }

            var dateAfterYears = baseDate.AddYears(years);
            int months = (targetDate.Year - dateAfterYears.Year) * 12 + 
                        (targetDate.Month - dateAfterYears.Month);
            
            if (targetDate.Day < dateAfterYears.Day)
            {
                months--;
            }

            var dateAfterMonths = dateAfterYears.AddMonths(months);
            int days = (targetDate.Date - dateAfterMonths.Date).Days;

            int hours = targetDate.Hour;
            int minutes = targetDate.Minute;
            int seconds = targetDate.Second;

            return (years, months, days, hours, minutes, seconds);
        }

        public static string FormatGoal(long totalSeconds)
        {
            if (totalSeconds <= 0)
                return "(Цель: 00:00:00)";

            var (years, months, days, hours, minutes, seconds) = FromSeconds(totalSeconds);

            string time = $"{hours:D2}:{minutes:D2}:{seconds:D2}";

            if (years > 0)
                return $"(Цель: {years:D2} г. {months:D2} мес. {days:D2} д. {time})";
            if (months > 0)
                return $"(Цель: {months:D2} мес. {days:D2} д. {time})";
            if (days > 0)
                return $"(Цель: {days:D2} д. {time})";
            
            return $"(Цель: {time})";
        }
    }
}