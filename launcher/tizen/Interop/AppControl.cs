using System;
using System.Runtime.InteropServices;

namespace QuietHome.TizenHost.Interop;

/// <summary>
/// One launch request through Tizen's app control API (<c>libcapi-appfw-app-control</c>):
/// create it, set the app and extras, send it, and dispose it. Each step runs only while
/// the earlier ones succeeded, so a caller can chain them and check <see cref="Result"/>
/// once. <see cref="Platform.TizenSystemLauncher"/> is the only user.
/// </summary>
sealed class AppControlRequest : IDisposable
{
    const string Library = "libcapi-appfw-app-control.so.0";

    /// <summary>The operation for a plain launch of an app.</summary>
    public const string DefaultOperation = "http://tizen.org/appcontrol/operation/default";

    IntPtr handle;

    /// <summary>True when the native request was created; when false nothing else can be done with it.</summary>
    public bool Created { get; }

    /// <summary>0 while every step so far succeeded, otherwise the error code of the first step that failed.</summary>
    public int Result { get; private set; }

    /// <summary>Creates the native request; <see cref="Created"/> tells whether that worked.</summary>
    public AppControlRequest()
    {
        Result = NativeCreate(out handle);
        Created = Result == 0;
    }

    /// <summary>Sets the ID of the app to launch.</summary>
    public void SetAppId(string appId) => Step(() => NativeSetAppId(handle, appId));

    /// <summary>Sets the operation, normally <see cref="DefaultOperation"/>.</summary>
    public void SetOperation(string operation) => Step(() => NativeSetOperation(handle, operation));

    /// <summary>Adds one string extra that the launched app reads.</summary>
    public void AddExtra(string key, string value) => Step(() => NativeAddExtraData(handle, key, value));

    /// <summary>Sends the request without waiting for a reply.</summary>
    public void Send() => Step(() => NativeSendLaunchRequest(handle, IntPtr.Zero, IntPtr.Zero));

    /// <summary>Frees the native request, if it was created.</summary>
    public void Dispose()
    {
        if (handle != IntPtr.Zero)
        {
            NativeDestroy(handle);
            handle = IntPtr.Zero;
        }
    }

    /// <summary>Runs one native step if all earlier steps succeeded and records its result.</summary>
    void Step(Func<int> call)
    {
        if (Result == 0)
            Result = call();
    }

    /// <summary>Creates a native request; returns 0 on success.</summary>
    [DllImport(Library, EntryPoint = "app_control_create")]
    static extern int NativeCreate(out IntPtr app);

    /// <summary>Sets the app ID of a native request; returns 0 on success.</summary>
    [DllImport(Library, EntryPoint = "app_control_set_app_id")]
    static extern int NativeSetAppId(IntPtr app, string id);

    /// <summary>Sets the operation of a native request; returns 0 on success.</summary>
    [DllImport(Library, EntryPoint = "app_control_set_operation")]
    static extern int NativeSetOperation(IntPtr app, string operation);

    /// <summary>Adds a string extra to a native request; returns 0 on success.</summary>
    [DllImport(Library, EntryPoint = "app_control_add_extra_data")]
    static extern int NativeAddExtraData(IntPtr app, string key, string value);

    /// <summary>Sends a native request; with no callback the TV does not report a reply. Returns 0 on success.</summary>
    [DllImport(Library, EntryPoint = "app_control_send_launch_request")]
    static extern int NativeSendLaunchRequest(IntPtr app, IntPtr callback, IntPtr data);

    /// <summary>Frees a native request; returns 0 on success.</summary>
    [DllImport(Library, EntryPoint = "app_control_destroy")]
    static extern int NativeDestroy(IntPtr app);
}
