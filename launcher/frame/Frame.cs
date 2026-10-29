using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Threading;

/// <summary>
/// A developer tool, not part of Quiet Home: it captures both layers of what the TV shows and
/// writes each as a PNG, so <c>tools/frame.py</c> can lay one over the other on the computer.
/// The UI layer comes from <c>efl_util_screenshot</c> with its alpha (zero where the picture shows
/// through, as in Quiet Home's preview hole); the video plane, which that screenshot leaves out,
/// comes from <c>libvideo-capture.so</c>, the call the WLED ambilight experiments used. The video
/// call worked only from the temporary root session on the QE65S95F, and its frame is small
/// (720x576 there, whatever size is asked for). See docs/04-launcher.md.
/// </summary>
public static class Frame
{
    [DllImport("libcapi-ui-efl-util.so.0")] static extern IntPtr efl_util_screenshot_initialize(int width, int height);
    [DllImport("libcapi-ui-efl-util.so.0")] static extern IntPtr efl_util_screenshot_take_tbm_surface(IntPtr screenshot);
    [DllImport("libcapi-ui-efl-util.so.0")] static extern int efl_util_screenshot_deinitialize(IntPtr screenshot);
    [DllImport("libtbm.so.1")] static extern int tbm_surface_map(IntPtr surface, int opt, IntPtr info);
    [DllImport("libtbm.so.1")] static extern int tbm_surface_unmap(IntPtr surface);
    [DllImport("libtbm.so.1")] static extern void tbm_surface_destroy(IntPtr surface);
    [DllImport("libvideo-capture.so")] static extern int secvideo_api_capture_screen_video_only(int width, int height, IntPtr info);
    [DllImport("libvideo-capture.so")] static extern int secvideo_api_capture_screen_unlock();

    // Asking for the full screen returns the plane's own size; a smaller request than that fails.
    const int ScreenWidth = 1920, ScreenHeight = 1080;

    // The video call refuses (-6) a capture that comes within about 50 ms of the previous one.
    const int VideoAttempts = 5, VideoRetryMs = 80;

    /// <summary>
    /// Writes <c>ui.png</c> and <c>video.png</c> into the directory in <paramref name="args"/>[0]
    /// and prints one line per layer; returns 0 when both layers were captured.
    /// </summary>
    public static int Main(string[] args)
    {
        if (args.Length != 1)
        {
            Console.WriteLine("usage: Frame.dll DIRECTORY");
            return 2;
        }
        bool ui = CaptureUi(Path.Combine(args[0], "ui.png"));
        bool video = CaptureVideo(Path.Combine(args[0], "video.png"));
        return ui && video ? 0 : 1;
    }

    /// <summary>Saves the UI layer as an RGBA PNG; returns false, after printing why, when it fails.</summary>
    static bool CaptureUi(string path)
    {
        IntPtr shot = efl_util_screenshot_initialize(ScreenWidth, ScreenHeight);
        if (shot == IntPtr.Zero)
        {
            Console.WriteLine("ui=init_failed");
            return false;
        }
        IntPtr surface = efl_util_screenshot_take_tbm_surface(shot);
        IntPtr info = Marshal.AllocHGlobal(256);
        try
        {
            if (surface == IntPtr.Zero)
            {
                Console.WriteLine("ui=take_failed");
                return false;
            }
            for (int i = 0; i < 256; i++)
                Marshal.WriteByte(info, i, 0);
            int result = tbm_surface_map(surface, 1, info);
            if (result != 0)
            {
                Console.WriteLine("ui=map_failed:" + result);
                return false;
            }
            // tbm_surface_info_s: width, height, format, bpp, size, num_planes, then planes
            // {ptr, size, offset, stride}. Each pixel is B, G, R, A in memory: the format says
            // XRGB8888, yet the fourth byte was 0 in Quiet Home's preview hole and 255 elsewhere.
            int width = Marshal.ReadInt32(info, 0), height = Marshal.ReadInt32(info, 4);
            uint format = (uint)Marshal.ReadInt32(info, 8);
            int stride = Marshal.ReadInt32(info, 36);
            IntPtr pixels = Marshal.ReadIntPtr(info, 24);
            var rgba = new byte[width * height * 4];
            var row = new byte[stride];
            for (int y = 0; y < height; y++)
            {
                Marshal.Copy(pixels + y * stride, row, 0, stride);
                for (int x = 0; x < width; x++)
                {
                    int to = (y * width + x) * 4;
                    rgba[to] = row[x * 4 + 2];
                    rgba[to + 1] = row[x * 4 + 1];
                    rgba[to + 2] = row[x * 4];
                    rgba[to + 3] = row[x * 4 + 3];
                }
            }
            tbm_surface_unmap(surface);
            WritePng(path, width, height, rgba, 4);
            Console.WriteLine($"ui={width}x{height} format={FourCc(format)}");
            return true;
        }
        finally
        {
            Marshal.FreeHGlobal(info);
            if (surface != IntPtr.Zero)
                tbm_surface_destroy(surface);
            efl_util_screenshot_deinitialize(shot);
        }
    }

    /// <summary>
    /// Saves the video plane, at the size the TV returns, as an RGB PNG; returns false, after
    /// printing the last result code, when every attempt fails.
    /// </summary>
    static bool CaptureVideo(string path)
    {
        int ySize = ScreenWidth * ScreenHeight, uvSize = ySize / 2;
        // Info_t on 32-bit Tizen: buffer sizes, width, height, Y and UV pointers, format.
        IntPtr info = Marshal.AllocHGlobal(64), y = Marshal.AllocHGlobal(ySize), uv = Marshal.AllocHGlobal(uvSize);
        try
        {
            int result = -1;
            var watch = Stopwatch.StartNew();
            for (int attempt = 0; attempt < VideoAttempts && result != 0; attempt++)
            {
                if (attempt > 0)
                    Thread.Sleep(VideoRetryMs);
                for (int i = 0; i < 64; i++)
                    Marshal.WriteByte(info, i, 0);
                Marshal.WriteInt32(info, 0, ySize);
                Marshal.WriteInt32(info, 4, uvSize);
                Marshal.WriteInt32(info, 8, ScreenWidth);
                Marshal.WriteInt32(info, 12, ScreenHeight);
                Marshal.WriteIntPtr(info, 16, y);
                Marshal.WriteIntPtr(info, 20, uv);
                result = secvideo_api_capture_screen_video_only(ScreenWidth, ScreenHeight, info);
                secvideo_api_capture_screen_unlock();
            }
            if (result != 0)
            {
                Console.WriteLine("video=failed:" + result);
                return false;
            }
            int width = Marshal.ReadInt32(info, 8), height = Marshal.ReadInt32(info, 12);
            var luma = new byte[width * height];
            var chroma = new byte[width * height / 2];
            Marshal.Copy(y, luma, 0, luma.Length);
            Marshal.Copy(uv, chroma, 0, chroma.Length);
            WritePng(path, width, height, Nv12ToRgb(luma, chroma, width, height), 3);
            Console.WriteLine($"video={width}x{height} ms={watch.ElapsedMilliseconds}");
            return true;
        }
        finally
        {
            Marshal.FreeHGlobal(info);
            Marshal.FreeHGlobal(y);
            Marshal.FreeHGlobal(uv);
        }
    }

    /// <summary>
    /// Returns RGB bytes for an NV12 frame (a Y plane, then interleaved U and V at half size)
    /// in limited-range BT.709, which is what the captured HDMI frames were.
    /// </summary>
    static byte[] Nv12ToRgb(byte[] luma, byte[] chroma, int width, int height)
    {
        var rgb = new byte[width * height * 3];
        for (int row = 0; row < height; row++)
        {
            for (int column = 0; column < width; column++)
            {
                double l = 1.164 * (luma[row * width + column] - 16);
                int c = (row / 2) * width + (column & ~1);
                double u = chroma[c] - 128, v = chroma[c + 1] - 128;
                int to = (row * width + column) * 3;
                rgb[to] = Clamp(l + 1.793 * v);
                rgb[to + 1] = Clamp(l - 0.213 * u - 0.533 * v);
                rgb[to + 2] = Clamp(l + 2.112 * u);
            }
        }
        return rgb;
    }

    /// <summary>Returns the value rounded into a byte's range.</summary>
    static byte Clamp(double value) => (byte)Math.Clamp((int)Math.Round(value), 0, 255);

    /// <summary>Returns a DRM format code as its four letters, for example "AR24".</summary>
    static string FourCc(uint format) =>
        new string(new[] { (char)(format & 0xFF), (char)(format >> 8 & 0xFF), (char)(format >> 16 & 0xFF), (char)(format >> 24) });

    /// <summary>Writes 8-bit RGB (3 channels) or RGBA (4 channels) pixels as a PNG file.</summary>
    static void WritePng(string path, int width, int height, byte[] pixels, int channels)
    {
        var data = new MemoryStream();
        using (var z = new ZLibStream(data, CompressionLevel.Fastest, true))
        {
            int rowBytes = width * channels;
            for (int y = 0; y < height; y++)
            {
                z.WriteByte(0);
                z.Write(pixels, y * rowBytes, rowBytes);
            }
        }
        var header = new byte[13];
        BigEndian(width).CopyTo(header, 0);
        BigEndian(height).CopyTo(header, 4);
        header[8] = 8;
        header[9] = (byte)(channels == 4 ? 6 : 2);
        using var file = File.Create(path);
        file.Write(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
        Chunk(file, "IHDR", header);
        Chunk(file, "IDAT", data.ToArray());
        Chunk(file, "IEND", Array.Empty<byte>());
    }

    /// <summary>Returns the number's four bytes, most significant first.</summary>
    static byte[] BigEndian(int value)
    {
        var bytes = BitConverter.GetBytes(value);
        if (BitConverter.IsLittleEndian)
            Array.Reverse(bytes);
        return bytes;
    }

    /// <summary>Writes one PNG chunk: length, type, data and the CRC of type and data.</summary>
    static void Chunk(Stream output, string type, byte[] data)
    {
        var body = new byte[4 + data.Length];
        for (int i = 0; i < 4; i++)
            body[i] = (byte)type[i];
        data.CopyTo(body, 4);
        uint crc = 0xFFFFFFFF;
        foreach (byte b in body)
        {
            crc ^= b;
            for (int k = 0; k < 8; k++)
                crc = (crc & 1) != 0 ? 0xEDB88320 ^ (crc >> 1) : crc >> 1;
        }
        output.Write(BigEndian(data.Length));
        output.Write(body);
        output.Write(BigEndian((int)(crc ^ 0xFFFFFFFF)));
    }
}
