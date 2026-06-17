namespace SalahBahazad.Domain.Enums;

/// <summary>
/// Staff roles, ordered by authority level (higher value = higher authority).
/// A Teacher cannot elevate another staff member beyond their own role (FR-PLAT-ROLE-002).
/// None = 0 satisfies the enum default-value convention (CA1008).
/// </summary>
public enum StaffRole
{
    None      = 0,
    Assistant = 1,
    Teacher   = 2,
}
