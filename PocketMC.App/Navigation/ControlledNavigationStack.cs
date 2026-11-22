using System;
using System.Collections.Generic;

namespace PocketMC.App.Navigation;

public sealed class ControlledNavigationStack
{
    private readonly List<NavigationEntry> _backStack = new();
    private readonly Stack<NavigationEntry> _forwardStack = new();
    private NavigationEntry? _current;

    public bool CanNavigateBack => _backStack.Count > 0;
    public bool CanNavigateForward => _forwardStack.Count > 0;

    public NavigationEntry? Current => _current;

    public void Push(object page, string breadcrumb, DetailRouteKind routeKind, DetailBackNavigation backNavigation, bool clearDetailStack = false)
    {
        var newEntry = new NavigationEntry(page, breadcrumb, routeKind, backNavigation);

        if (clearDetailStack)
        {
            _backStack.Clear();
            _forwardStack.Clear();
            _current = newEntry;
            return;
        }

        if (_current != null)
        {
            _backStack.Add(_current);
        }

        _forwardStack.Clear();
        _current = newEntry;
    }

    public NavigationEntry? NavigateBack()
    {
        if (!CanNavigateBack)
        {
            return null;
        }

        if (_current != null)
        {
            _forwardStack.Push(_current);
        }

        int lastIndex = _backStack.Count - 1;
        var previous = _backStack[lastIndex];
        _backStack.RemoveAt(lastIndex);

        _current = previous;
        return previous;
    }

    public NavigationEntry? NavigateForward()
    {
        if (!CanNavigateForward)
        {
            return null;
        }

        if (_current != null)
        {
            _backStack.Add(_current);
        }

        var next = _forwardStack.Pop();
        _current = next;
        return next;
    }

    public void Clear()
    {
        _backStack.Clear();
        _forwardStack.Clear();
        _current = null;
    }
}
