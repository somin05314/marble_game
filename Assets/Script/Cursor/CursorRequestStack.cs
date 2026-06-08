using System.Collections.Generic;
using UnityEngine;

public static class CursorRequestStack
{
    static readonly List<(object owner, CursorManager.CursorState state)> _stack = new();

    public static void Push(object owner, CursorManager.CursorState state)
    {
        // 중복 push 방지
        for (int i = _stack.Count - 1; i >= 0; i--)
            if (ReferenceEquals(_stack[i].owner, owner))
                _stack.RemoveAt(i);

        _stack.Add((owner, state));
    }

    public static void Pop(object owner)
    {
        for (int i = _stack.Count - 1; i >= 0; i--)
            if (ReferenceEquals(_stack[i].owner, owner))
                _stack.RemoveAt(i);
    }

    public static bool TryGetTop(out CursorManager.CursorState state)
    {
        if (_stack.Count > 0)
        {
            state = _stack[_stack.Count - 1].state;
            return true;
        }
        state = CursorManager.CursorState.Default;
        return false;
    }
}