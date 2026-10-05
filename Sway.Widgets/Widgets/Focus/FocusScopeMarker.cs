namespace Sway.Widgets;

sealed class FocusScopeMarker(FocusNode node, bool hasFocus, Widget child) : InheritedWidget(child)
{
    public FocusNode Node => node;
    public bool HasFocus => hasFocus;
    public override bool UpdateShouldNotify(InheritedWidget old) => ((FocusScopeMarker)old).HasFocus != HasFocus || ((FocusScopeMarker)old).Node != Node;
}
