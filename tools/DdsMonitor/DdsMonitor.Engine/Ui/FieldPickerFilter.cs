using System;
using System.Collections.Generic;
using System.Linq;

namespace DdsMonitor.Engine;

/// <summary>
/// Filters fields for incremental search pickers.
/// </summary>
public static class FieldPickerFilter
{
    public static IReadOnlyList<FieldMetadata> FilterFields(IEnumerable<FieldMetadata> fields, string query)
    {
        if (fields == null)
        {
            throw new ArgumentNullException(nameof(fields));
        }

        if (string.IsNullOrWhiteSpace(query))
        {
            return fields.ToList();
        }

        var trimmed = query.Trim();
        return fields.Where(field => Matches(field, trimmed)).ToList();
    }

    public static bool Matches(FieldMetadata field, string query)
    {
        if (field == null)
        {
            throw new ArgumentNullException(nameof(field));
        }

        if (string.IsNullOrWhiteSpace(query))
        {
            return true;
        }

        var fullPath = (field.IsWrapperField ? "Sample." : "Payload.") + field.StructuredName;
        return fullPath.Contains(query, StringComparison.OrdinalIgnoreCase) ||
               field.DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase);
    }
}
