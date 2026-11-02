using System;
using System.Diagnostics;
using System.IO;
using QuietHome.Core.Platform;

namespace QuietHome.TizenHost.Platform;

/// <summary>
/// Runs the packaged WLED ambilight sender on the TV. Implements the core's
/// <see cref="IAmbilight"/> port for the Quiet Home app: it starts
/// <c>AmbilightLoop.dll</c> through <c>/usr/bin/dotnet</c>, keeps its PID in the app's data
/// directory and kills that process to stop it. The sender is a separate process, so it
/// keeps streaming while Quiet Home is in the background. A small state file remembers the
/// owner's last choice, and <see cref="Restore"/> starts the sender again after a restart.
/// </summary>
sealed class TizenAmbilight : IAmbilight
{
    readonly string programPath;
    readonly string configPath;
    readonly string pidPath;
    readonly string statePath;
    Process? running;

    /// <summary>
    /// Creates the port for the program at <paramref name="programPath"/>, its JSON
    /// configuration at <paramref name="configPath"/> and the app's persistent data
    /// directory <paramref name="dataDirectory"/> for the PID and preference files.
    /// </summary>
    public TizenAmbilight(string programPath, string configPath, string dataDirectory)
    {
        this.programPath = programPath;
        this.configPath = configPath;
        pidPath = Path.Combine(dataDirectory, "ambilight.pid");
        statePath = Path.Combine(dataDirectory, "ambilight.state");
    }

    /// <summary>True when the packaged sender program exists.</summary>
    public bool Available => File.Exists(programPath);

    /// <summary>
    /// True when the process started by this app is alive, or when the saved PID names a
    /// live process whose command line contains the sender's name.
    /// </summary>
    public bool IsOn
    {
        get
        {
            if (running != null && !running.HasExited)
                return true;
            running = null;
            int pid = ReadPid();
            if (pid > 0 && IsOurProcess(pid))
                return true;
            if (pid > 0)
                DeletePid();
            return false;
        }
    }

    /// <summary>
    /// Starts the sender unless it is already running; returns false when the program is
    /// missing or the process could not be started. Remembers "on" for <see cref="Restore"/>.
    /// </summary>
    public bool Start()
    {
        if (!Available)
            return false;
        if (IsOn)
        {
            WriteState("on");
            return true;
        }
        EnsureSmackRule();
        try
        {
            var info = new ProcessStartInfo
            {
                FileName = "/usr/bin/dotnet",
                Arguments = "\"" + programPath + "\" --config \"" + configPath + "\" 0",
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            var process = Process.Start(info);
            if (process == null)
                return false;
            running = process;
            WritePid(process.Id);
            WriteState("on");
            return true;
        }
        catch (Exception ex)
        {
            Log("start: " + ex.Message);
            return false;
        }
    }

    /// <summary>
    /// Kills the running sender, forgets its PID and remembers "off". Returns false only when
    /// the process could not be killed; an already stopped sender counts as success.
    /// </summary>
    public bool Stop()
    {
        try
        {
            int pid = ReadPid();
            if (running != null)
            {
                Kill(running);
                running = null;
            }
            else if (pid > 0 && IsOurProcess(pid))
            {
                try
                {
                    using var process = Process.GetProcessById(pid);
                    Kill(process);
                }
                catch (ArgumentException)
                {
                    // The process ended between the check and the lookup.
                }
            }
            DeletePid();
            WriteState("off");
            return true;
        }
        catch (Exception ex)
        {
            Log("stop: " + ex.Message);
            return false;
        }
    }

    /// <summary>Starts the sender when the saved preference is "on"; a missing program leaves it off.</summary>
    public void Restore()
    {
        try
        {
            if (Available && ReadState() == "on" && !IsOn)
                Start();
        }
        catch (Exception ex)
        {
            Log("restore: " + ex.Message);
        }
    }

    /// <summary>Kills one process and waits briefly for it to exit; failures are ignored.</summary>
    static void Kill(Process process)
    {
        try
        {
            if (!process.HasExited)
                process.Kill();
            process.WaitForExit(2000);
        }
        catch (Exception)
        {
            // The process may already have exited.
        }
        process.Dispose();
    }

    /// <summary>Returns the saved PID, or 0 when the file is missing or unreadable.</summary>
    int ReadPid()
    {
        try
        {
            return int.TryParse(File.ReadAllText(pidPath).Trim(), out int pid) ? pid : 0;
        }
        catch (Exception)
        {
            return 0;
        }
    }

    /// <summary>Writes the process ID, replacing the file's text.</summary>
    void WritePid(int pid)
    {
        try
        {
            File.WriteAllText(pidPath, pid.ToString());
        }
        catch (Exception ex)
        {
            Log("pid: " + ex.Message);
        }
    }

    /// <summary>Deletes the PID file when it exists.</summary>
    void DeletePid()
    {
        try
        {
            File.Delete(pidPath);
        }
        catch (Exception ex)
        {
            Log("pid: " + ex.Message);
        }
    }

    /// <summary>Returns the saved preference, "on" or "off", or "" when it was never written.</summary>
    string ReadState()
    {
        try
        {
            return File.Exists(statePath) ? File.ReadAllText(statePath).Trim() : "";
        }
        catch (Exception)
        {
            return "";
        }
    }

    /// <summary>Writes the owner's last choice, "on" or "off".</summary>
    void WriteState(string value)
    {
        try
        {
            File.WriteAllText(statePath, value);
        }
        catch (Exception ex)
        {
            Log("state: " + ex.Message);
        }
    }

    /// <summary>True when the PID is alive and its command line names the sender program.</summary>
    static bool IsOurProcess(int pid)
    {
        try
        {
            string command = File.ReadAllText("/proc/" + pid + "/cmdline");
            return command.Contains("AmbilightLoop.dll");
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>
    /// Gives the app's SMACK label read and execute access to <c>System::Tools</c>, where
    /// <c>/usr/bin/dotnet</c> lives. The package's generated rules do not include it, and
    /// without it <see cref="Process.Start"/> fails with "Permission denied". The rule is
    /// runtime state; the app adds it again after every restart. <c>load2</c> is world
    /// writable, so this needs no root session.
    /// </summary>
    static void EnsureSmackRule()
    {
        try
        {
            string label = File.ReadAllText("/proc/self/attr/current").Trim();
            if (!label.StartsWith("User::Pkg::", StringComparison.Ordinal))
                return;
            string rule = label + " System::Tools rx";
            if (File.ReadAllText("/sys/fs/smackfs/load2").Contains(rule))
                return;
            File.AppendAllText("/sys/fs/smackfs/load2", rule + "\n");
            Log("smack: " + rule);
        }
        catch (Exception ex)
        {
            Log("smack: " + ex.Message);
        }
    }

    /// <summary>Appends a diagnostic line for the port to <c>/tmp/quiet-home-ambilight.log</c>.</summary>
    static void Log(string text)
    {
        try
        {
            File.AppendAllText("/tmp/quiet-home-ambilight.log", text + "\n");
        }
        catch (Exception)
        {
            // Diagnostics must never stop the app.
        }
    }
}
