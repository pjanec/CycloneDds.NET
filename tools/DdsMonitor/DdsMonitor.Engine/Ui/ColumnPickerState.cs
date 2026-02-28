using System;
using System.Collections.Generic;
using System.Linq;

namespace DdsMonitor.Engine;

/// <summary>
/// Maintains available and selected column state for the column picker.
/// </summary>
public sealed class ColumnPickerState
{
    private readonly List<FieldMetadata> _available = new();
    private readonly List<FieldMetadata> _selected = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="ColumnPickerState"/> class.
    /// </summary>
    public ColumnPickerState(IEnumerable<FieldMetadata> availableFields, IEnumerable<FieldMetadata> selectedFields)
    {
        if (availableFields == null)
        {
            throw new ArgumentNullException(nameof(availableFields));
        }

        if (selectedFields == null)
        {
            throw new ArgumentNullException(nameof(selectedFields));
        }

        _selected.AddRange(selectedFields);
        var selectedSet = new HashSet<FieldMetadata>(_selected);

        foreach (var field in availableFields)
        {
            if (!selectedSet.Contains(field))
            {
                _available.Add(field);
            }
        }
    }

    /// <summary>
    /// Gets the available fields.
    /// </summary>
    public IReadOnlyList<FieldMetadata> AvailableFields => _available;

    /// <summary>
    /// Gets the selected fields.
    /// </summary>
    public IReadOnlyList<FieldMetadata> SelectedFields => _selected;

    /// <summary>
    /// Moves a field from available to selected.
    /// </summary>
    public void AddField(FieldMetadata field)
    {
        if (field == null)
        {
            throw new ArgumentNullException(nameof(field));
        }

        if (_available.Remove(field))
        {
            _selected.Add(field);
        }
    }

    /// <summary>
    /// Moves a field from selected to available.
    /// </summary>
    public void RemoveField(FieldMetadata field)
    {
        if (field == null)
        {
            throw new ArgumentNullException(nameof(field));
        }

        if (_selected.Remove(field))
        {
            _available.Add(field);
        }
    }

    /// <summary>
    /// Moves a selected field to a new index.
    /// </summary>
    public void MoveSelected(FieldMetadata field, int newIndex)
    {
        if (field == null)
        {
            throw new ArgumentNullException(nameof(field));
        }

        var currentIndex = _selected.IndexOf(field);
        if (currentIndex < 0)
        {
            return;
        }

        var clampedIndex = Math.Clamp(newIndex, 0, _selected.Count - 1);
        if (currentIndex == clampedIndex)
        {
            return;
        }

        _selected.RemoveAt(currentIndex);
        _selected.Insert(clampedIndex, field);
    }

    /// <summary>
    /// Returns the selected fields in their current order.
    /// </summary>
    public List<FieldMetadata> GetSelectedOrder()
    {
        return _selected.ToList();
    }
}
