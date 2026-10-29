using System;

namespace QuietHome.Core.Platform;

/// <summary>
/// Port to the TV's input preview: shows the live picture of the input on screen inside its
/// focused card on the Home page, or ends it, and mutes that input's sound under the home
/// screen. The TV host implements it with Samsung's TV service and a see-through hole in the card
/// (launcher/tizen/Platform/TizenInputPreview.cs); tests use a recording stand-in.
/// <see cref="HomeController"/> decides when a preview is shown; demo mode never shows one.
/// See docs/12-hdmi-preview.md.
/// </summary>
public interface IInputPreview
{
    /// <summary>
    /// Raised on the UI thread when the TV refused a preview <see cref="Show"/> asked for.
    /// The port has already put the picture back; the controller stops asking for the session.
    /// </summary>
    event Action? Failed;

    /// <summary>
    /// Asks for input <paramref name="sourceType"/> in the input card at <paramref name="slot"/>
    /// on the Home page drawn last. Returns at once; the picture may appear later, once the
    /// card has reached its focused size. A failure is reported through <see cref="Failed"/>.
    /// </summary>
    void Show(int sourceType, int slot);

    /// <summary>
    /// Ends the preview while Quiet Home stays in front: the card is drawn as usual again and
    /// the TV's picture goes back to full screen, since the TV keeps the preview's size
    /// otherwise. The controller calls it when the focus leaves the card or the page changes.
    /// </summary>
    void Hide();

    /// <summary>
    /// Quiet Home is giving the screen up (an input or app is opening, or it goes to the
    /// background): ends any preview at once and gives the TV its full-screen picture back.
    /// With <paramref name="sound"/> it also turns the input's sound back on if
    /// <see cref="Silence"/> muted it; the TV's viewer does not do that by itself. The
    /// controller leaves the sound muted while an app starts and gives it back when Quiet
    /// Home goes to the background.
    /// </summary>
    void Release(bool sound);

    /// <summary>
    /// Quiet Home is about to open an input in the TV's viewer: ends any preview at once and,
    /// once its hole is gone from the screen and the viewer below Quiet Home has paused, does
    /// what <see cref="Release"/> with sound does and runs <paramref name="open"/>, which
    /// switches the input and starts the viewer; the port may run it later on the UI thread.
    /// Going full screen only then keeps a stretched picture out of the hole. The viewer turns
    /// on its TV behaviour only when it resumes from a pause: on the QE65S95F, a viewer opened
    /// from under a launcher it could see through let the TV start its screen saver over the
    /// input after two minutes, as over an app.
    /// </summary>
    void HandToViewer(Action open);

    /// <summary>
    /// Mutes the sound of the input on screen while Quiet Home is in front, as Samsung's
    /// home does; <see cref="Release"/> turns it back on.
    /// </summary>
    void Silence();
}
