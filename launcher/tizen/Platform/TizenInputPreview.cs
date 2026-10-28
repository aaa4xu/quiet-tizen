using System;
using System.Runtime.CompilerServices;
using QuietHome.Core.Platform;
using QuietHome.TizenHost.Views;

namespace QuietHome.TizenHost.Platform;

/// <summary>
/// Implements the core's <see cref="IInputPreview"/> port for the Quiet Home app: puts the
/// live picture of the input on screen into its focused card, mutes its sound while Quiet
/// Home is in front and gives picture and sound back when Quiet Home gives the screen up. The TV draws the picture on its main video plane below the window
/// (<see cref="TvVideoPlane"/>); <see cref="HomeView"/> cuts a hole in the card for it once
/// the card has finished widening. The TV may reset the plane to full screen by itself (on
/// the QE65S95F whenever the window's transparency changed, which <see cref="HomeView"/>
/// therefore sets once), so every reset it reports is undone at once, and after tying the
/// plane to the window the hole opens only once the size has held: the owner sees the
/// picture appear in its card, not a reset.
/// </summary>
sealed class TizenInputPreview : IInputPreview, IDisposable
{
    // A little longer than the view's focus animation, so the card has its final size.
    const int SettleMs = 200;

    // How long the size must hold after tying the plane, or after the TV's last reset,
    // before the hole opens.
    const int SteadyMs = 400;

    // Most resets undone for one preview, so a TV that keeps resetting cannot loop forever.
    const int MaxRepairs = 20;

    // The plane goes back to full screen this long after the hole closes, so no frame shows
    // the full-screen picture through a hole that is still open.
    const int CloseFrameMs = 40;

    readonly HomeView view;
    readonly Tizen.NUI.Timer timer = new(SettleMs);
    readonly Tizen.NUI.Timer steady = new(SteadyMs);
    readonly Tizen.NUI.Timer closing = new(CloseFrameMs);
    TvVideoPlane? plane;
    Box heldRect;
    int repairs;
    int wantedSlot = -1;
    bool shown;
    // Set once the plane was asked for anything, so a hide restores only what a preview changed.
    bool touched;
    // Set while the input's sound is muted by Quiet Home, so a release unmutes only what it muted.
    bool silenced;

    /// <summary>Creates the port for the cards drawn by <paramref name="view"/>.</summary>
    public TizenInputPreview(HomeView view)
    {
        this.view = view;
        timer.Tick += (s, e) =>
        {
            Apply();
            return false;
        };
        steady.Tick += (s, e) =>
        {
            Reveal();
            return false;
        };
        closing.Tick += (s, e) =>
        {
            Restore(false);
            return false;
        };
    }

    /// <summary>Raised when the TV refused the preview; the picture is already back to normal.</summary>
    public event Action? Failed;

    /// <summary>
    /// Asks for the input on screen in input card <paramref name="slot"/>; the TV shows that
    /// input, so <paramref name="sourceType"/> only names it. The TV is asked once the card has
    /// settled; a request for what is already shown or already on its way changes nothing.
    /// </summary>
    public void Show(int sourceType, int slot)
    {
        if (slot == wantedSlot && (timer.IsRunning() || steady.IsRunning() || view.HoleOpen(slot)))
            return;
        wantedSlot = slot;
        closing.Stop();
        steady.Stop();
        timer.Stop();
        timer.Start();
    }

    /// <summary>
    /// Ends the preview: no pending request and the hole closes at once; the TV's full-screen
    /// picture comes back a frame later (see <see cref="CloseFrameMs"/>).
    /// </summary>
    public void Hide()
    {
        Forget();
        if (!touched)
            return;
        touched = false;
        closing.Stop();
        closing.Start();
    }

    /// <summary>
    /// Quiet Home gives the screen up: the hole closes and the TV gets its full-screen
    /// picture back at once, before an input switch could keep the preview's size, and with
    /// <paramref name="sound"/> the input's sound if Quiet Home muted it.
    /// </summary>
    public void Release(bool sound)
    {
        Forget();
        if (touched || closing.IsRunning())
        {
            closing.Stop();
            touched = false;
            Restore(true);
        }
        if (!sound || !silenced)
            return;
        silenced = false;
        try
        {
            UnsilencePlane();
        }
        catch (Exception)
        {
            // Nothing more can be done here.
        }
    }

    /// <summary>Mutes the input's sound under the home screen; a TV without the TV service keeps it.</summary>
    public void Silence()
    {
        try
        {
            SilencePlane();
            silenced = true;
        }
        catch (Exception)
        {
            // The sound simply stays on.
        }
    }

    /// <summary>Ends the preview and releases the TV's controls; the app is ending.</summary>
    public void Dispose()
    {
        Release(true);
        timer.Dispose();
        steady.Dispose();
        closing.Dispose();
        try
        {
            DisposePlane();
        }
        catch (Exception)
        {
            // The app is ending anyway.
        }
    }

    /// <summary>
    /// The card has settled: places the TV's picture over it. Right after tying the plane to
    /// the window the hole waits until the size holds (<see cref="Reveal"/>); otherwise it
    /// opens at once. Any failure, including missing TV assemblies, ends the preview and
    /// raises <see cref="Failed"/>.
    /// </summary>
    void Apply()
    {
        var rect = view.PreviewBox(wantedSlot);
        if (rect == null)
            return;
        try
        {
            // After another app the TV is not playing the input; the card keeps its icon.
            if (!PlanePlaying())
                return;
            touched = true;
            heldRect = rect.Value;
            repairs = 0;
            bool tied = ShowOnPlane(heldRect);
            shown = true;
            if (tied)
                Wait();
            else
                view.OpenHole(wantedSlot);
        }
        catch (Exception)
        {
            Fail();
        }
    }

    /// <summary>
    /// The TV reported the plane's size. A size other than the card's (the TV's own reset to
    /// full screen) is set back at once; before the hole is open it then waits
    /// <see cref="SteadyMs"/> after this reset. Reports while no preview is shown are ignored.
    /// </summary>
    void SizeReported(Box reported)
    {
        if (!shown || reported == heldRect || repairs >= MaxRepairs)
            return;
        repairs++;
        try
        {
            HoldPlane(heldRect);
        }
        catch (Exception)
        {
            Fail();
            return;
        }
        if (steady.IsRunning())
            Wait();
    }

    /// <summary>(Re)starts the wait before the hole opens.</summary>
    void Wait()
    {
        steady.Stop();
        steady.Start();
    }

    /// <summary>
    /// The size has held since the plane was tied: sets it once more (for TVs that send no
    /// size notices) and opens the hole, so the picture appears in its card.
    /// </summary>
    void Reveal()
    {
        if (!shown)
            return;
        try
        {
            HoldPlane(heldRect);
            view.OpenHole(wantedSlot);
        }
        catch (Exception)
        {
            Fail();
        }
    }

    /// <summary>Drops any pending request and closes the hole.</summary>
    void Forget()
    {
        timer.Stop();
        steady.Stop();
        wantedSlot = -1;
        shown = false;
        view.CloseHole();
    }

    /// <summary>
    /// Gives the TV its full-screen picture back; with <paramref name="confirm"/> it waits
    /// until the TV has untied the plane (see <see cref="TvVideoPlane.Reset"/>).
    /// </summary>
    void Restore(bool confirm)
    {
        try
        {
            ResetPlane(confirm);
        }
        catch (Exception)
        {
            // Reset tries every step itself; nothing more can be done here.
        }
    }

    /// <summary>The TV refused a call: ends the preview and tells the controller.</summary>
    void Fail()
    {
        Hide();
        Failed?.Invoke();
    }

    // The next methods are the only ones that touch the TV service's types. They are never
    // inlined, so a TV without those assemblies fails inside the callers' try blocks.

    /// <summary>Returns the video plane, opening it and listening to its size reports on first use.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    TvVideoPlane Plane()
    {
        if (plane == null)
        {
            plane = new TvVideoPlane();
            plane.SizeReported += SizeReported;
        }
        return plane;
    }

    /// <summary>Puts the input on screen into <paramref name="rect"/>; returns whether the plane was tied to the window just now.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    bool ShowOnPlane(Box rect) => Plane().Show(rect);

    /// <summary>Sets the plane's size again.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    void HoldPlane(Box rect) => plane?.Hold(rect);

    /// <summary>Gives the TV its full-screen picture back, waiting for the untie with <paramref name="confirm"/>.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    void ResetPlane(bool confirm) => plane?.Reset(confirm);

    /// <summary>Returns whether the TV is playing the input on screen.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    bool PlanePlaying() => Plane().Playing();

    /// <summary>Turns the input's sound back on.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    void UnsilencePlane() => plane?.Unsilence();

    /// <summary>Mutes the input's sound, opening the TV's controls on first use.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    void SilencePlane() => Plane().Silence();

    /// <summary>Releases the video plane's controls.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    void DisposePlane()
    {
        plane?.Dispose();
        plane = null;
    }
}
