using System;
using PocketMC.App.Navigation;
using Xunit;

namespace PocketMC.Tests;

public class ControlledNavigationStackTests
{
    [Fact]
    public void PushAndNavigateBack_RestoresPreviousEntry()
    {
        var stack = new ControlledNavigationStack();

        Assert.False(stack.CanNavigateBack);
        Assert.False(stack.CanNavigateForward);

        stack.Push("Page1", "Dashboard", DetailRouteKind.Dashboard, DetailBackNavigation.Dashboard);
        Assert.False(stack.CanNavigateBack);

        stack.Push("Page2", "Server Settings", DetailRouteKind.ServerSettings, DetailBackNavigation.PreviousDetail);
        Assert.True(stack.CanNavigateBack);
        Assert.False(stack.CanNavigateForward);

        var prev = stack.NavigateBack();
        Assert.NotNull(prev);
        Assert.Equal("Page1", prev.Page);
        Assert.Equal("Dashboard", prev.Breadcrumb);
        Assert.False(stack.CanNavigateBack);
        Assert.True(stack.CanNavigateForward);

        var forward = stack.NavigateForward();
        Assert.NotNull(forward);
        Assert.Equal("Page2", forward.Page);
        Assert.True(stack.CanNavigateBack);
        Assert.False(stack.CanNavigateForward);
    }
}
