namespace BloodLink.Infrastructure.Services.Common;

internal static class WorkflowValidation
{
    public static void EnsureCanonicalEnum<TEnum>(TEnum value, string fieldName)
        where TEnum : struct, Enum
    {
        if (!Enum.IsDefined(value))
        {
            throw new ArgumentException($"{fieldName} is not valid.");
        }
    }

    public static void EnsurePositiveUnits(int units, string fieldName)
    {
        if (units <= 0)
        {
            throw new ArgumentException($"{fieldName} must be greater than zero.");
        }
    }

    public static void EnsureSafeNote(string? note)
    {
        if (string.IsNullOrWhiteSpace(note))
        {
            return;
        }

        var normalized = note.Trim().ToLowerInvariant();
        string[] disallowedClinicalSignals =
        [
            "patient",
            "diagnosis",
            "clinical history",
            "cross-match",
            "crossmatch",
            "medical record"
        ];

        if (disallowedClinicalSignals.Any(normalized.Contains))
        {
            throw new ArgumentException("Notes must not contain patient-identifying or clinical details.");
        }
    }
}
