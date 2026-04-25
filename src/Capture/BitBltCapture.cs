using System.Runtime.InteropServices;
using OpenCvSharp;
using SleepRunner.Utils;

namespace SleepRunner.Capture;

/// <summary>
/// 使用 GDI BitBlt/PrintWindow 进行游戏窗口截屏
/// </summary>
public class BitBltCapture : IDisposable
{
    private IntPtr _hWnd;

    #region Win32 API

    [DllImport("user32.dll")]
    private static extern IntPtr GetDC(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

    [DllImport("user32.dll")]
    private static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll")]
    private static extern bool PrintWindow(IntPtr hWnd, IntPtr hdcBlt, uint nFlags);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateCompatibleDC(IntPtr hdc);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateCompatibleBitmap(IntPtr hdc, int nWidth, int nHeight);

    [DllImport("gdi32.dll")]
    private static extern IntPtr SelectObject(IntPtr hdc, IntPtr hgdiobj);

    [DllImport("gdi32.dll")]
    private static extern bool BitBlt(IntPtr hdcDest, int xDest, int yDest, int w, int h,
        IntPtr hdcSrc, int xSrc, int ySrc, uint rop);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteDC(IntPtr hdc);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr hObject);

    [DllImport("gdi32.dll")]
    private static extern int GetDIBits(IntPtr hdc, IntPtr hbmp, uint uStartScan, uint cScanLines,
        byte[] lpvBits, ref BITMAPINFO lpbi, uint uUsage);

    private const uint SRCCOPY = 0x00CC0020;
    private const uint PW_CLIENTONLY = 0x01;
    private const uint PW_RENDERFULLCONTENT = 0x02;

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int nIndex);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern bool EnumDisplaySettings(string? deviceName, int modeNum, ref DEVMODE devMode);

    private const int SM_CXSCREEN = 0;
    private const int ENUM_CURRENT_SETTINGS = -1;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct DEVMODE
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string dmDeviceName;
        public short dmSpecVersion, dmDriverVersion, dmSize, dmDriverExtra;
        public int dmFields, dmPositionX, dmPositionY, dmDisplayOrientation, dmDisplayFixedOutput;
        public short dmColor, dmDuplex, dmYResolution, dmTTOption, dmCollate;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string dmFormName;
        public short dmLogPixels;
        public int dmBitsPerPel, dmPelsWidth, dmPelsHeight, dmDisplayFlags, dmDisplayFrequency;
        public int dmICMMethod, dmICMIntent, dmMediaType, dmDitherType;
        public int dmReserved1, dmReserved2, dmPanningWidth, dmPanningHeight;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left, Top, Right, Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BITMAPINFOHEADER
    {
        public uint biSize;
        public int biWidth;
        public int biHeight;
        public ushort biPlanes;
        public ushort biBitCount;
        public uint biCompression;
        public uint biSizeImage;
        public int biXPelsPerMeter;
        public int biYPelsPerMeter;
        public uint biClrUsed;
        public uint biClrImportant;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BITMAPINFO
    {
        public BITMAPINFOHEADER bmiHeader;
    }

    #endregion

    /// <summary>
    /// 绑定目标窗口
    /// </summary>
    public void Start(IntPtr hWnd)
    {
        _hWnd = hWnd;
        Log.Log($"Capture bindded to window: 0x{hWnd:X}");
    }

    /// <summary>
    /// 截取一帧画面，返回 BGR 格式的 Mat
    /// </summary>
    public Mat? Capture()
    {
        if (_hWnd == IntPtr.Zero) return null;

        // 优先用 ClientRect，若为空则回退到 WindowRect
        if (!GetClientRect(_hWnd, out var rect) || (rect.Right - rect.Left) <= 0)
        {
            Log.Log("GetClientRect returned empty, falling back to GetWindowRect");
            if (!GetWindowRect(_hWnd, out rect))
            {
                Log.Log("GetWindowRect also failed");
                return null;
            }
            // WindowRect 是屏幕坐标，需要转为宽高
            int rw = rect.Right - rect.Left;
            int rh = rect.Bottom - rect.Top;
            rect.Left = 0; rect.Top = 0;
            rect.Right = rw; rect.Bottom = rh;
        }

        int logicalW = rect.Right - rect.Left;
        int logicalH = rect.Bottom - rect.Top;

        if (logicalW <= 0 || logicalH <= 0)
        {
            Log.Log($"Invalid rect: {logicalW}x{logicalH}");
            return null;
        }

        // DPI 补偿：逻辑尺寸 × DPI 因子 = 物理尺寸
        float dpi = GetDpiScale();
        int width = (int)(logicalW * dpi);
        int height = (int)(logicalH * dpi);
        Log.Log($"Capture size: logical={logicalW}x{logicalH}, dpi={dpi:F2}, physical={width}x{height}");

        IntPtr hdcWindow = GetDC(_hWnd);
        IntPtr hdcMem = CreateCompatibleDC(hdcWindow);
        IntPtr hBitmap = CreateCompatibleBitmap(hdcWindow, width, height);
        IntPtr hOld = SelectObject(hdcMem, hBitmap);

        // 优先用 PrintWindow（支持窗口被遮挡的情况），失败则回退 BitBlt
        bool ok = PrintWindow(_hWnd, hdcMem, PW_CLIENTONLY | PW_RENDERFULLCONTENT);
        if (!ok)
        {
            Log.Log("PrintWindow failed, fallback to BitBlt");
            BitBlt(hdcMem, 0, 0, width, height, hdcWindow, 0, 0, SRCCOPY);
        }

        // 使用 32 位 BGRA 避免行对齐问题
        var bmi = new BITMAPINFO();
        bmi.bmiHeader.biSize = (uint)Marshal.SizeOf<BITMAPINFOHEADER>();
        bmi.bmiHeader.biWidth = width;
        bmi.bmiHeader.biHeight = -height; // 负值 = 从上到下扫描
        bmi.bmiHeader.biPlanes = 1;
        bmi.bmiHeader.biBitCount = 32;
        bmi.bmiHeader.biCompression = 0;

        byte[] pixels = new byte[width * height * 4];
        GetDIBits(hdcMem, hBitmap, 0, (uint)height, pixels, ref bmi, 0);

        // BGRA → BGR
        var bgra = new Mat(height, width, MatType.CV_8UC4);
        Marshal.Copy(pixels, 0, bgra.Data, pixels.Length);

        var bgr = new Mat();
        Cv2.CvtColor(bgra, bgr, ColorConversionCodes.BGRA2BGR);
        bgra.Dispose();

        // 清理 GDI 资源
        SelectObject(hdcMem, hOld);
        DeleteObject(hBitmap);
        DeleteDC(hdcMem);
        ReleaseDC(_hWnd, hdcWindow);

        return bgr;
    }

    /// <summary>
    /// 检测 DPI 缩放因子（物理分辨率 / 逻辑分辨率）
    /// </summary>
    private static float GetDpiScale()
    {
        int logicalW = GetSystemMetrics(SM_CXSCREEN);
        var dm = new DEVMODE();
        dm.dmSize = (short)Marshal.SizeOf<DEVMODE>();
        EnumDisplaySettings(null, ENUM_CURRENT_SETTINGS, ref dm);
        int physicalW = dm.dmPelsWidth;

        if (logicalW > 0 && physicalW > logicalW)
            return (float)physicalW / logicalW;
        return 1.0f;
    }

    public void Dispose()
    {
        _hWnd = IntPtr.Zero;
    }
    private static readonly LogScope Log = new("Capture");
}
