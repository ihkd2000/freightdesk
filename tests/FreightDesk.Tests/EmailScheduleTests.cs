using FreightDesk.Infra;
using Microsoft.Extensions.Configuration;

namespace FreightDesk.Tests;

public class EmailScheduleTests
{
    private static readonly DateTime Today = new(2026, 11, 1);
    private static readonly int[] Offsets = { 5, 2 };

    private static IReadOnlyList<PlannedEmail> Plan(DateTime? arrival) =>
        EmailSchedule.Plan(arrival, Today, 10, Offsets);

    [Fact]
    public void No_arrival_date_means_nothing_is_scheduled()
    {
        Assert.Empty(Plan(null));
    }

    [Fact]
    public void Past_arrival_means_nothing_is_scheduled()
    {
        Assert.Empty(Plan(Today.AddDays(-1)));
    }

    [Fact]
    public void Far_arrival_schedules_notice_and_each_reminder_on_its_own_day()
    {
        var plan = Plan(Today.AddDays(30));

        Assert.Equal(
            new[]
            {
                (EmailKinds.ArrivalNotice, Today.AddDays(20)),
                (EmailKinds.PaymentReminder, Today.AddDays(25)),
                (EmailKinds.PaymentReminder, Today.AddDays(28))
            },
            plan.Select(p => (p.Kind, p.Date)));
    }

    [Fact]
    public void Inside_the_windows_the_notice_and_due_reminders_go_out_today_as_one_each()
    {
        // 3 days out: the 5-day stage is already due, the 2-day stage is still ahead.
        var plan = Plan(Today.AddDays(3));

        Assert.Equal(
            new[]
            {
                (EmailKinds.ArrivalNotice, Today),
                (EmailKinds.PaymentReminder, Today),
                (EmailKinds.PaymentReminder, Today.AddDays(1))
            },
            plan.Select(p => (p.Kind, p.Date)));
    }

    [Fact]
    public void Arrival_today_still_gets_notice_and_one_reminder()
    {
        var plan = Plan(Today.AddHours(15));

        Assert.Equal(new[] { EmailKinds.ArrivalNotice, EmailKinds.PaymentReminder }, plan.Select(p => p.Kind));
        Assert.All(plan, p => Assert.Equal(Today, p.Date));
    }

    [Fact]
    public void Settings_default_and_are_cleaned_up()
    {
        var empty = new ConfigurationBuilder().Build();
        Assert.Equal(10, EmailSchedule.ArrivalDaysBefore(empty));
        Assert.Equal(new[] { 5, 2 }, EmailSchedule.ReminderOffsets(empty));

        var custom = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ArrivalNotifications:DaysBefore"] = "0",
            ["PaymentReminders:DaysBefore:0"] = "1",
            ["PaymentReminders:DaysBefore:1"] = "7",
            ["PaymentReminders:DaysBefore:2"] = "-3",
            ["PaymentReminders:DaysBefore:3"] = "7"
        }).Build();
        Assert.Equal(1, EmailSchedule.ArrivalDaysBefore(custom));
        Assert.Equal(new[] { 7, 1 }, EmailSchedule.ReminderOffsets(custom));
    }
}
