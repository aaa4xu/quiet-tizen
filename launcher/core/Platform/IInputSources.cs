using System;
using QuietHome.Core.Inputs;

namespace QuietHome.Core.Platform;

/// <summary>
/// Port to the TV's inputs: the connected inputs, the one on screen, switching between them
/// and a notice when the list changes. The TV host implements it with Samsung's source
/// library (launcher/tizen/Platform/TizenInputSources.cs); demo mode wraps that in
/// <see cref="Demo.DemoInputSources"/>. The host reads the list through this port and draws
/// the input cards on the Home page from it.
/// </summary>
public interface IInputSources
{
    /// <summary>
    /// Raised on the UI thread when the TV reports that inputs were connected, disconnected
    /// or renamed. The handler rereads the list with <see cref="Read"/>.
    /// </summary>
    event Action? Changed;

    /// <summary>
    /// How subscribing to <see cref="Changed"/> went, written to <c>source_callback=</c> in the
    /// diagnostics file: 0 subscribed, -1 not tried (no UI thread to deliver it on), -2 the
    /// call threw, any other number is the TV's error code.
    /// </summary>
    int WatchStatus { get; }

    /// <summary>
    /// Reads the connected inputs in the TV's order. Never throws: a failure gives an empty
    /// list and the reason in <see cref="InputReading.Error"/>.
    /// </summary>
    InputReading Read();

    /// <summary>Returns the source type of the input on screen, or null when the TV did not say.</summary>
    int? Current();

    /// <summary>
    /// Switches the TV to input <paramref name="sourceType"/> and returns the TV's result:
    /// negative on failure. May throw when the TV's library is missing.
    /// </summary>
    int Switch(int sourceType);
}
