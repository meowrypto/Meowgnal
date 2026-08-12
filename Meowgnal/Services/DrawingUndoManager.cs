using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Meowgnal.Models;

namespace Meowgnal.Services;

/// <summary>
/// Manages undo/redo stacks for drawing operations.
/// Stores snapshots of the entire drawings list before each change.
/// </summary>
public class DrawingUndoManager
{
    private readonly Stack<string> _undoStack = new();
    private readonly Stack<string> _redoStack = new();
    private const int MaxStackSize = 50;

    /// <summary>
    /// Takes a snapshot of the current drawings list and pushes it onto the undo stack.
    /// Call this BEFORE any modification to the drawings list.
    /// </summary>
    public void PushSnapshot(List<Drawing> drawings)
    {
        var snapshot = JsonSerializer.Serialize(drawings);
        _undoStack.Push(snapshot);
        _redoStack.Clear(); // New action invalidates redo history

        // Limit stack size to prevent memory issues
        if (_undoStack.Count > MaxStackSize)
        {
            var temp = _undoStack.ToArray();
            _undoStack.Clear();
            for (var i = temp.Length - MaxStackSize; i < temp.Length; i++)
                _undoStack.Push(temp[i]);
        }
    }

    /// <summary>
    /// Undoes the last operation by restoring the previous snapshot.
    /// Returns the restored drawings list, or null if nothing to undo.
    /// </summary>
    public List<Drawing>? Undo(List<Drawing> currentDrawings)
    {
        if (_undoStack.Count == 0) return null;

        // Save current state to redo stack
        var currentSnapshot = JsonSerializer.Serialize(currentDrawings);
        _redoStack.Push(currentSnapshot);

        // Restore previous state
        var previousSnapshot = _undoStack.Pop();
        return JsonSerializer.Deserialize<List<Drawing>>(previousSnapshot) ?? new List<Drawing>();
    }

    /// <summary>
    /// Redoes the last undone operation by restoring the next snapshot.
    /// Returns the restored drawings list, or null if nothing to redo.
    /// </summary>
    public List<Drawing>? Redo(List<Drawing> currentDrawings)
    {
        if (_redoStack.Count == 0) return null;

        // Save current state to undo stack
        var currentSnapshot = JsonSerializer.Serialize(currentDrawings);
        _undoStack.Push(currentSnapshot);

        // Restore next state
        var nextSnapshot = _redoStack.Pop();
        return JsonSerializer.Deserialize<List<Drawing>>(nextSnapshot) ?? new List<Drawing>();
    }

    /// <summary>
    /// Returns true if there are operations that can be undone.
    /// </summary>
    public bool CanUndo => _undoStack.Count > 0;

    /// <summary>
    /// Returns true if there are operations that can be redone.
    /// </summary>
    public bool CanRedo => _redoStack.Count > 0;

    /// <summary>
    /// Clears both undo and redo stacks.
    /// </summary>
    public void Clear()
    {
        _undoStack.Clear();
        _redoStack.Clear();
    }
}