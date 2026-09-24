using Swipewalk.Desktop.Services;
using Foundation;
using ObjCRuntime;
using UIKit;

namespace Swipewalk.Desktop;

[Register("AppDelegate")]
public class AppDelegate : MauiUIApplicationDelegate
{
	protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();

	private static readonly UIKeyCommand[] TextSizeKeys =
	[
		UIKeyCommand.Create(new NSString("="), UIKeyModifierFlags.Command, new Selector("cfBiggerText:")),
		UIKeyCommand.Create(new NSString("-"), UIKeyModifierFlags.Command, new Selector("cfSmallerText:")),
		UIKeyCommand.Create(new NSString("0"), UIKeyModifierFlags.Command, new Selector("cfActualTextSize:")),
		UIKeyCommand.Create(UIKeyCommand.Escape, 0, new Selector("cfDismissModal:")),
	];

	/// <summary>
	/// ⌘=, ⌘- and ⌘0 change the text size (WCAG 1.4.4, 2.1.1); Escape dismisses the currently shown custom modal
	/// page (e.g. Pages/AppPickerPage), when one has registered with Services/ModalDismiss. This does not reach
	/// native alerts such as Services/PhysicalDeviceNotice's, which Mac Catalyst presents outside this app's own
	/// UIKit responder chain -- Escape still closes that alert, but through its own native Cancel-style key
	/// handling, not through this wiring (see PhysicalDeviceNotice.ShowMacAlertAsync). Registered as key
	/// commands rather than menu shortcuts: MAUI menu shortcuts only accept letters and digits, and changes to
	/// the system View menu are not kept. The Text menu (Services/AppMenus.cs) lists the text-size commands.
	/// </summary>
	public override UIKeyCommand[] KeyCommands => TextSizeKeys;

	/// <summary>Claim the text-size and Escape actions so UIKit delivers the key commands here. Escape is only
	/// claimed while something is registered to handle it, so it still reaches other controls otherwise.</summary>
	public override bool CanPerform(Selector action, NSObject? withSender) =>
		action.Name is "cfBiggerText:" or "cfSmallerText:" or "cfActualTextSize:"
		|| (action.Name == "cfDismissModal:" && ModalDismiss.Current is not null)
		|| base.CanPerform(action, withSender);

	[Export("cfBiggerText:")]
	public void BiggerText(NSObject sender) => Fonts.Bigger();

	[Export("cfSmallerText:")]
	public void SmallerText(NSObject sender) => Fonts.Smaller();

	[Export("cfActualTextSize:")]
	public void ActualTextSize(NSObject sender) => Fonts.Reset();

	[Export("cfDismissModal:")]
	public void DismissModal(NSObject sender) => ModalDismiss.Current?.Invoke();
}
