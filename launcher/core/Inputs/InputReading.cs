using System.Collections.Generic;

namespace QuietHome.Core.Inputs;

/// <summary>
/// The result of reading the TV's input list through <see cref="Platform.IInputSources.Read"/>:
/// the connected inputs and, when reading failed, why. <see cref="HomeController"/> keeps the
/// latest reading, builds the Home page's input cards from it and writes the error to <c>input_error=</c> in
/// the diagnostics file.
/// </summary>
/// <param name="Inputs">The connected inputs in the TV's order; empty when reading failed.</param>
/// <param name="Error">"" on success, "empty" when the TV returned no list, otherwise the error message.</param>
public sealed record InputReading(List<InputSource> Inputs, string Error);
