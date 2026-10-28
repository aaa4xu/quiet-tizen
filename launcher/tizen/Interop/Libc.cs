using System;
using System.Runtime.InteropServices;

namespace QuietHome.TizenHost.Interop;

/// <summary>
/// The C library, for freeing memory that a Samsung library allocated and handed over
/// (the input list from <see cref="SourceApi.GetSourceList"/>).
/// </summary>
static class Libc
{
    /// <summary>Frees memory allocated with malloc.</summary>
    [DllImport("libc.so.6", EntryPoint = "free")]
    public static extern void Free(IntPtr pointer);
}
