using Swipewalk.Collectors.Ios;

namespace Swipewalk.Core.Tests;

public class IosAccessibilityPermissionTests
{
    [Fact]
    public void IsTrusted_NeverThrows()
    {
        // Deliberately does not assert which way this comes back: a fresh macOS CI runner has never granted
        // Accessibility to whatever process runs tests, but a dev Mac can have it granted at any time (for
        // example to run a real Accessibility Inspector walk by hand -- see IosInspectorWalkTests), and
        // that's a legitimate, expected state for this test to see, not a bug. IsTrusted must simply never
        // throw, on either OS or either permission state.
        var exception = Record.Exception(() => IosAccessibilityPermission.IsTrusted());

        Assert.Null(exception);
    }
}
