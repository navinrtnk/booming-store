// Copyright (c) 2026 Navin Ratnayake (@navinrtnk). All rights reserved.
namespace BoomingStore;

/// <summary>Completed quarter-hours during a shift; nearest quarter at its end. No offline accrual.</summary>
public static class Payroll
{
    public const decimal HourlyRate = 100;
    public static long CompletedQuarters(double hours) => (long)Math.Floor(hours * 4 + 1e-9);
    public static long RoundedQuarters(double hours) => (long)Math.Floor(hours * 4 + .5 + 1e-9);
    public static void Advance(StoreState state, EmployeeData employee, double hours)
    {
        if (!employee.Summoned || hours <= 0 || !double.IsFinite(hours)) return;
        employee.ShiftHours += hours;
        Settle(state, employee, CompletedQuarters(employee.ShiftHours));
    }
    public static void EndShift(StoreState state, EmployeeData employee)
    {
        if (!employee.Summoned) return;
        Settle(state, employee, RoundedQuarters(employee.ShiftHours));
        employee.Summoned = false; employee.ClockOutRequested = false;
        employee.ShiftHours = 0; employee.PaidQuarters = 0;
    }
    private static void Settle(StoreState state, EmployeeData employee, long quarters)
    {
        var due = Math.Max(0, quarters - employee.PaidQuarters) * (HourlyRate / 4);
        employee.PaidQuarters = quarters;
        state.WageExpense += due; state.DayWages += due; state.WagesPayable += due;
        PayArrears(state);
    }
    public static void PayArrears(StoreState state)
    {
        var paid = Math.Min(state.Cash, state.WagesPayable);
        state.Cash -= paid; state.WagesPayable -= paid;
    }
}
