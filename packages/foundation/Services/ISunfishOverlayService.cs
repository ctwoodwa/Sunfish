namespace Sunfish.Foundation.Services;

public interface ISunfishOverlayService
{
    event EventHandler? OverlayRequested;
    event EventHandler? OverlayDismissed;
    void RequestOverlay();
    void DismissOverlay();
}
