using Swipewalk.Core.Model;

namespace Swipewalk.Core.Tests;

public class AccessibilityNodeTests
{
    [Fact]
    public void DescendantsAndSelf_WalksWholeTree_DepthFirst()
    {
        var tree = new AccessibilityNode
        {
            Role = "window",
            Children =
            [
                new AccessibilityNode
                {
                    Role = "group",
                    Children = [new AccessibilityNode { Role = "button", Label = "Pay" }],
                },
                new AccessibilityNode { Role = "image" },
            ],
        };

        var roles = tree.DescendantsAndSelf().Select(n => n.Role);

        Assert.Equal(["window", "group", "button", "image"], roles);
    }

    [Fact]
    public void DescendantsAndSelfWithPath_ReportsChildIndexPaths()
    {
        var tree = new AccessibilityNode
        {
            Role = "window",
            Children =
            [
                new AccessibilityNode
                {
                    Role = "group",
                    Children = [new AccessibilityNode { Role = "button" }],
                },
                new AccessibilityNode { Role = "image" },
            ],
        };

        var paths = tree.DescendantsAndSelfWithPath().Select(e => $"{e.Node.Role}:{e.Path}");

        Assert.Equal(["window:", "group:0", "button:0/0", "image:1"], paths);
    }
}
