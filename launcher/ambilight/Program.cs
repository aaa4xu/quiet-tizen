using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading;

// MAIN-only ambilight sender for the TV. It captures the raw video plane, samples the LED
// zones from the real video content (skipping letterbox bars), and streams the colours to
// WLED over DDP. There is no POST capture, no interleaving and no temporal smoothing: every
// frame is sent exactly as measured. Quiet Home starts and stops it through the packaged
// AmbilightLoop.dll; the same program can be run from a shell for experiments.
static class Program
{
    // MAIN plane: the raw video plane. The library pins this capture at 960x540.
    [DllImport("libvideo-capture.so", EntryPoint = "secvideo_api_capture_video_main")]
    static extern int CaptureMainPlane(int w, int h, IntPtr info);

    // Resets the capture state after a failed call.
    [DllImport("libvideo-capture.so", EntryPoint = "secvideo_api_capture_screen_unlock")]
    static extern int UnlockScreen();

    [DllImport("libdl.so.2")]
    static extern IntPtr dlopen(string file, int mode);

    [DllImport("libdl.so.2")]
    static extern IntPtr dlsym(IntPtr handle, string symbol);

    [DllImport("libdl.so.2")]
    static extern int dladdr(IntPtr addr, out DlInfo info);

    [DllImport("libc.so.6", EntryPoint = "mprotect")]
    static extern int Mprotect(IntPtr addr, IntPtr len, int prot);

    [StructLayout(LayoutKind.Sequential)]
    struct DlInfo
    {
        public IntPtr fname;
        public IntPtr fbase;
        public IntPtr sname;
        public IntPtr saddr;
    }

    // scaler_capture skips its 66 ms is_post_capture_fast gate when this local
    // static in libvideo-capture-impl-sec.so is non-zero. The capture itself
    // still costs ~50 ms, so this raises the loop from ~14 to ~18 fps.
    const int UNMANAGED_CAPTURE_OFFSET = 0x1b6e9;

    // The MAIN plane takes the library's cache path. is_input_capture_fast then
    // compares the last capture time to a 50/100 ms threshold and returns the
    // same buffer while it is fresh. Zeroing both thresholds forces a real
    // capture on every call, so the LEDs follow the video instead of a stale
    // buffer.
    const int THRESHOLD_SINGLE_OFFSET = 0x3f50;
    const int THRESHOLD_MULTI_OFFSET = 0x3f58;

    static int W = 960;
    static int H = 540;

    // Rows whose brightest pixel is at or below this level are treated as
    // letterbox bars. MAIN uses limited-range black (Y=16), so the threshold
    // must sit just above 16.
    const int BAR_THRESHOLD = 18;

    // The device-specific values: WLED address, LED count and the test-pattern
    // boundaries of the strip. The default layout was measured on the QE65S95F;
    // the address is a documentation placeholder. --config reads the owner's values.
    static class Config
    {
        public static string WledIp = "192.0.2.50";
        public static int DdpPort = 4048;
        public static int Count = 252;
        public static int BottomEnd = 81;
        public static int LeftEnd = 126;
        public static int TopEnd = 207;

        // Reads the JSON configuration, keeping every value that is valid and
        // falling back to the tested defaults for the rest.
        public static void Load(string path)
        {
            try
            {
                using var document = JsonDocument.Parse(File.ReadAllText(path));
                var root = document.RootElement;
                string ip = ReadString(root, "wled_ip", WledIp);
                if (IPAddress.TryParse(ip, out _))
                    WledIp = ip;
                DdpPort = ReadInt(root, "ddp_port", DdpPort);
                Count = ReadInt(root, "led_count", Count);
                BottomEnd = ReadInt(root, "bottom_end", BottomEnd);
                LeftEnd = ReadInt(root, "left_end", LeftEnd);
                TopEnd = ReadInt(root, "top_end", TopEnd);
                if (DdpPort < 1 || DdpPort > 65535 || Count < 4 ||
                    BottomEnd < 1 || LeftEnd <= BottomEnd ||
                    TopEnd <= LeftEnd || TopEnd >= Count)
                {
                    ResetLayout();
                }
                Log($"config: {WledIp}:{DdpPort} count={Count} ends={BottomEnd}/{LeftEnd}/{TopEnd}");
            }
            catch (Exception ex)
            {
                Log("config: " + ex.Message);
            }
        }

        // Returns a string setting, or the fallback when it is missing or not a string.
        static string ReadString(JsonElement root, string name, string fallback)
        {
            if (root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String)
                return value.GetString() ?? fallback;
            return fallback;
        }

        // Returns an integer setting, or the fallback when it is missing or not an integer.
        static int ReadInt(JsonElement root, string name, int fallback)
        {
            if (root.TryGetProperty(name, out var value) && value.TryGetInt32(out int result))
                return result;
            return fallback;
        }

        // Restores the tested values after an invalid combination.
        static void ResetLayout()
        {
            Count = 252;
            BottomEnd = 81;
            LeftEnd = 126;
            TopEnd = 207;
        }
    }

    static void Log(string s)
    {
        File.AppendAllText("/tmp/ambilight_loop.log", s + "\n");
    }

    static int Clamp(double v)
    {
        int i = (int)Math.Round(v);
        if (i < 0) return 0;
        if (i > 255) return 255;
        return i;
    }

    // Maps LED index i to a rectangle in frame coordinates. The strip runs
    // clockwise from bottom-right, so each side gets its own share of the LEDs.
    static void LedRect(int i, int w, int h, out int x0, out int y0, out int x1, out int y1)
    {
        if (i < Config.BottomEnd)
        {
            int j = i;
            x1 = w - (int)((long)w * j / Config.BottomEnd);
            x0 = w - (int)((long)w * (j + 1) / Config.BottomEnd);
            y0 = (int)(h * 0.90);
            y1 = h;
        }
        else if (i < Config.LeftEnd)
        {
            int j = i - Config.BottomEnd;
            int n = Config.LeftEnd - Config.BottomEnd;
            y1 = h - (int)((long)h * j / n);
            y0 = h - (int)((long)h * (j + 1) / n);
            x0 = 0;
            x1 = (int)(w * 0.10);
        }
        else if (i < Config.TopEnd)
        {
            int j = i - Config.LeftEnd;
            int n = Config.TopEnd - Config.LeftEnd;
            x0 = (int)((long)w * j / n);
            x1 = (int)((long)w * (j + 1) / n);
            y0 = 0;
            y1 = (int)(h * 0.10);
        }
        else
        {
            int j = i - Config.TopEnd;
            int n = Config.Count - Config.TopEnd;
            y0 = (int)((long)h * j / n);
            y1 = (int)((long)h * (j + 1) / n);
            x0 = (int)(w * 0.90);
            x1 = w;
        }
    }

    static void EnableUnmanagedCapture()
    {
        IntPtr handle = dlopen("libvideo-capture-impl-sec.so", 2);
        IntPtr fn = dlsym(handle, "_Z20is_post_capture_fastv");
        DlInfo info;
        if (fn != IntPtr.Zero && dladdr(fn, out info) != 0)
        {
            IntPtr flag = IntPtr.Add(info.fbase, UNMANAGED_CAPTURE_OFFSET);
            Log($"unmanaged_capture before={Marshal.ReadByte(flag)} base={info.fbase}");
            Marshal.WriteByte(flag, 1);
            Log($"unmanaged_capture after={Marshal.ReadByte(flag)}");
        }
        else
        {
            Log("unmanaged_capture: dladdr failed; the 66 ms gate stays in place");
        }
    }

    static void EnableFreshMainCapture()
    {
        IntPtr handle = dlopen("libvideo-capture-impl-sec.so", 2);
        IntPtr fn = dlsym(handle, "_Z20is_post_capture_fastv");
        DlInfo info;
        if (fn == IntPtr.Zero || dladdr(fn, out info) == 0)
        {
            Log("fresh_main: dladdr failed; the cache threshold stays in place");
            return;
        }
        IntPtr page = IntPtr.Add(info.fbase, THRESHOLD_SINGLE_OFFSET);
        page = (IntPtr)((long)page & ~0xfffL);
        // The thresholds sit in a literal pool inside .text, so keep PROT_EXEC.
        int rc = Mprotect(page, (IntPtr)0x2000, 7);
        Log($"fresh_main: mprotect rc={rc} " +
            $"single={Marshal.ReadInt64(IntPtr.Add(info.fbase, THRESHOLD_SINGLE_OFFSET))} " +
            $"multi={Marshal.ReadInt64(IntPtr.Add(info.fbase, THRESHOLD_MULTI_OFFSET))}");
        Marshal.WriteInt64(IntPtr.Add(info.fbase, THRESHOLD_SINGLE_OFFSET), 0);
        Marshal.WriteInt64(IntPtr.Add(info.fbase, THRESHOLD_MULTI_OFFSET), 0);
        Log("fresh_main: zeroed");
    }

    // Finds the vertical content range of a frame by trimming top and bottom
    // rows that are essentially black. Returns the full frame when no meaningful
    // bars are present, so a full-frame video is sampled normally.
    unsafe static void DetectContentRows(byte* yp, int w, int h, int[] rowMax,
                                         out int top, out int bottom)
    {
        for (int y = 0; y < h; y++)
        {
            int rm = 0;
            int rowBase = y * w;
            for (int x = 0; x < w; x++)
            {
                int v = yp[rowBase + x];
                if (v > rm) rm = v;
            }
            rowMax[y] = rm;
        }
        top = 0;
        while (top < h && rowMax[top] <= BAR_THRESHOLD) top++;
        bottom = h - 1;
        while (bottom >= top && rowMax[bottom] <= BAR_THRESHOLD) bottom--;
        if (top > bottom || (bottom - top + 1) < h / 2)
        {
            top = 0;
            bottom = h - 1;
        }
    }

    // Samples each LED zone from the content rectangle (cx0,cy0,cw,ch), mapping
    // the zone rectangle into that area. Top and bottom LEDs therefore read the
    // first and last real video pixels instead of the black bars.
    unsafe static void ComputeZonesInArea(byte* yp, byte* uvp, int w, int h,
                                          int cx0, int cy0, int cw, int ch,
                                          double[] zoneY, double[] zoneU, double[] zoneV)
    {
        for (int i = 0; i < Config.Count; i++)
        {
            int x0, y0, x1, y1;
            LedRect(i, w, h, out x0, out y0, out x1, out y1);
            int px0 = cx0 + x0 * cw / w;
            int py0 = cy0 + y0 * ch / h;
            int px1 = cx0 + x1 * cw / w;
            int py1 = cy0 + y1 * ch / h;
            if (px1 <= px0) px1 = px0 + 1;
            if (py1 <= py0) py1 = py0 + 1;
            long sy = 0, su = 0, sv = 0, n = 0;
            // A 2x2 subsample is plenty for a zone average.
            for (int yy = py0; yy < py1; yy += 2)
            {
                int yrow = yy * w;
                int uvrow = (yy >> 1) * w;
                for (int xx = px0; xx < px1; xx += 2)
                {
                    sy += yp[yrow + xx];
                    int o = uvrow + (xx >> 1) * 2;
                    su += uvp[o];
                    sv += uvp[o + 1];
                    n++;
                }
            }
            zoneY[i] = n > 0 ? (double)sy / n : 0;
            zoneU[i] = n > 0 ? (double)su / n : 128;
            zoneV[i] = n > 0 ? (double)sv / n : 128;
        }
    }

    // Converts per-LED YUV averages to RGB bytes with the BT.709 matrix.
    static void ZonesToRgb(double[] zoneY, double[] zoneU, double[] zoneV, byte[] rgb)
    {
        for (int i = 0; i < Config.Count; i++)
        {
            double Y = zoneY[i], U = zoneU[i], V = zoneV[i];
            double yv = 1.164 * (Y - 16), u = U - 128, v = V - 128;
            rgb[i * 3] = (byte)Clamp(yv + 1.793 * v);
            rgb[i * 3 + 1] = (byte)Clamp(yv - 0.213 * u - 0.533 * v);
            rgb[i * 3 + 2] = (byte)Clamp(yv + 2.112 * u);
        }
    }

    static void SendFrame(Socket sock, IPEndPoint ep, byte[] pkt, byte[] rgb, ref byte seq)
    {
        Buffer.BlockCopy(rgb, 0, pkt, 10, rgb.Length);
        pkt[1] = seq;
        seq = (byte)((seq + 1) & 0x0F);
        try { sock.SendTo(pkt, ep); }
        catch (Exception e) { Log("send failed: " + e.Message); }
    }

    unsafe static void Main(string[] args)
    {
        // Accept "--config PATH", an optional "dump" and an optional seconds
        // limit (0 runs until stopped, which is how Quiet Home starts it).
        string configPath = null;
        int seconds = 0;
        bool dump = false;
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--config" && i + 1 < args.Length)
                configPath = args[++i];
            else if (args[i] == "dump")
                dump = true;
            else if (args[i] != "main" && int.TryParse(args[i], out int limit))
                seconds = limit;
        }
        File.WriteAllText("/tmp/ambilight_loop.log", "");
        if (configPath != null)
            Config.Load(configPath);

        EnableFreshMainCapture();
        EnableUnmanagedCapture();

        int ySize = W * H, uvSize = ySize / 2;
        IntPtr info = Marshal.AllocHGlobal(128);
        IntPtr y = Marshal.AllocHGlobal(ySize);
        IntPtr uv = Marshal.AllocHGlobal(uvSize);
        for (int i = 0; i < 128; i++) Marshal.WriteByte(info, i, 0);
        Marshal.WriteInt32(info, 0, ySize);
        Marshal.WriteInt32(info, 4, uvSize);
        Marshal.WriteInt32(info, 8, W);
        Marshal.WriteInt32(info, 12, H);
        Marshal.WriteIntPtr(info, 16, y);
        Marshal.WriteIntPtr(info, 20, uv);

        byte* yp = (byte*)y;
        byte* uvp = (byte*)uv;

        Socket sock = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        IPEndPoint ep = new IPEndPoint(IPAddress.Parse(Config.WledIp), Config.DdpPort);
        byte[] rgb = new byte[Config.Count * 3];
        byte[] pkt = new byte[10 + Config.Count * 3];
        // DDP v1 header: flags (0x40 version | 0x01 push), sequence, type RGB,
        // destination id, 32-bit offset (bytes 4-7), 16-bit length (bytes 8-9).
        pkt[0] = 0x41;
        pkt[2] = 0x01;
        int nbytes = Config.Count * 3;
        pkt[8] = (byte)(nbytes >> 8);
        pkt[9] = (byte)(nbytes & 0xFF);

        Log($"start: main {W}x{H}, {Config.Count} LEDs, DDP -> {Config.WledIp}:{Config.DdpPort}, " +
            $"seconds={seconds} dump={dump}");

        int[] rowMax = new int[H];
        double[] zy = new double[Config.Count], zu = new double[Config.Count], zv = new double[Config.Count];
        long frames = 0, fails = 0, minMs = long.MaxValue, maxMs = 0, sumMs = 0;
        long lastRc = 0;
        Stopwatch total = Stopwatch.StartNew();
        Stopwatch logTimer = Stopwatch.StartNew();
        byte seq = 0;
        bool dumped = false;

        while (true)
        {
            if (seconds > 0 && total.Elapsed.TotalSeconds >= seconds) break;

            Stopwatch sw = Stopwatch.StartNew();
            int rc = CaptureMainPlane(W, H, info);
            long capMs = sw.ElapsedMilliseconds;
            if (rc != 0)
            {
                fails++;
                lastRc = rc;
                if (fails <= 3) Log($"capture rc={rc}");
                UnlockScreen();
                Thread.Sleep(20);
                continue;
            }

            int ow = Marshal.ReadInt32(info, 8);
            int oh = Marshal.ReadInt32(info, 12);

            DetectContentRows(yp, ow, oh, rowMax, out int top, out int bottom);
            int ch = bottom - top + 1;
            ComputeZonesInArea(yp, uvp, ow, oh, 0, top, ow, ch, zy, zu, zv);
            ZonesToRgb(zy, zu, zv, rgb);
            long procMs = sw.ElapsedMilliseconds - capMs;

            if (dump && !dumped)
            {
                byte[] buf = new byte[ySize + uvSize];
                Marshal.Copy(y, buf, 0, ySize);
                Marshal.Copy(uv, buf, ySize, uvSize);
                File.WriteAllBytes("/tmp/ambilight_diag.yuv", buf);
                dumped = true;
                Log("dumped /tmp/ambilight_diag.yuv");
            }

            SendFrame(sock, ep, pkt, rgb, ref seq);

            long ms = sw.ElapsedMilliseconds;
            frames++;
            sumMs += ms;
            if (ms < minMs) minMs = ms;
            if (ms > maxMs) maxMs = ms;

            if (frames % 25 == 0)
                Log($"frame#{frames}: content={ow}x{ch}@{0},{top} cap={capMs} proc={procMs} total={ms}");

            if (logTimer.ElapsedMilliseconds >= 5000)
            {
                Log($"frames={frames} fails={fails} lastRc={lastRc} min={minMs} " +
                    $"avg={(frames > 0 ? sumMs / frames : -1)} max={maxMs} " +
                    $"fps={(frames * 1000.0 / Math.Max(1, total.ElapsedMilliseconds)):F1}");
                logTimer.Restart();
            }
        }

        Log($"stop: frames={frames} fails={fails} min={minMs} " +
            $"avg={(frames > 0 ? sumMs / frames : -1)} max={maxMs}");
        Log("done");
    }
}
