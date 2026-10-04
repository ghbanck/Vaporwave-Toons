using System;
using System.Windows.Forms;
using static VaporwaveToons.Native;

namespace VaporwaveToons;

/// <summary>
/// One small per-pixel-alpha layered window per toon. Click-through by default;
/// in squish mode the opaque pixels take clicks (layered windows hit-test by alpha).
/// </summary>
internal sealed unsafe class ToonWindow : NativeWindow, IDisposable
{
    private IntPtr _dc, _dib, _oldBmp;
    private int* _bits;
    private int _dibW, _dibH;

    private Sheet _sheet;
    private int _frame = -1, _row = -1, _scale, _x = int.MinValue, _y = int.MinValue, _w, _h;
    private bool _visible, _disposed;

    public event Action Clicked;
    public bool Visible => _visible;

    public ToonWindow(int maxW, int maxH, bool clickable)
    {
        CreateHandle(new CreateParams
        {
            Caption = "Vaporwave Toon",
            X = -32000, Y = -32000, Width = 1, Height = 1,
            Style = WS_POPUP,
            ExStyle = WS_EX_LAYERED | WS_EX_TOOLWINDOW | WS_EX_TOPMOST | WS_EX_NOACTIVATE |
                      (clickable ? 0 : WS_EX_TRANSPARENT),
        });
        Allocate(maxW, maxH);
    }

    private void Allocate(int w, int h)
    {
        FreeDib();
        var bmi = new BITMAPINFOHEADER
        {
            biSize = 40, biWidth = w, biHeight = -h, biPlanes = 1, biBitCount = 32,
        };
        _dc = CreateCompatibleDC(IntPtr.Zero);
        _dib = CreateDIBSection(_dc, ref bmi, 0, out IntPtr bits, IntPtr.Zero, 0);
        if (_dib == IntPtr.Zero) throw new OutOfMemoryException("CreateDIBSection failed");
        _bits = (int*)bits;
        _oldBmp = SelectObject(_dc, _dib);
        _dibW = w;
        _dibH = h;
        _sheet = null;
    }

    public void SetClickable(bool on)
    {
        int ex = GetWindowLong(Handle, GWL_EXSTYLE);
        ex = on ? ex & ~WS_EX_TRANSPARENT : ex | WS_EX_TRANSPARENT;
        SetWindowLong(Handle, GWL_EXSTYLE, ex);
    }

    public void Draw(Sheet sheet, int frame, int row, int x, int y, int scale)
    {
        int w = sheet.FrameW * scale, h = sheet.FrameH * scale;
        if (w > _dibW || h > _dibH) Allocate(Math.Max(w, _dibW), Math.Max(h, _dibH));

        bool content = sheet != _sheet || frame != _frame || row != _row || scale != _scale;
        if (content)
        {
            Blit(sheet, frame, row, scale);
            _sheet = sheet;
            _frame = frame;
            _row = row;
            _scale = scale;
        }
        if (content || x != _x || y != _y || w != _w || h != _h)
        {
            var dst = new POINT(x, y);
            var size = new SIZE(w, h);
            var src = new POINT(0, 0);
            var blend = new BLENDFUNCTION
            {
                BlendOp = AC_SRC_OVER, SourceConstantAlpha = 255, AlphaFormat = AC_SRC_ALPHA,
            };
            UpdateLayeredWindow(Handle, IntPtr.Zero, ref dst, ref size, _dc, ref src, 0, ref blend, ULW_ALPHA);
            _x = x;
            _y = y;
            _w = w;
            _h = h;
        }
        if (!_visible)
        {
            ShowWindow(Handle, SW_SHOWNOACTIVATE);
            _visible = true;
        }
    }

    private void Blit(Sheet s, int frame, int row, int scale)
    {
        var px = s.Px;
        int stride = s.Stride;
        for (int sy = 0; sy < s.FrameH; sy++)
        {
            int src = (row * s.FrameH + sy) * stride + frame * s.FrameW;
            int* line = _bits + sy * scale * _dibW;
            int* d = line;
            for (int sx = 0; sx < s.FrameW; sx++)
            {
                int c = px[src + sx];
                for (int k = 0; k < scale; k++) *d++ = c;
            }
            int bytes = s.FrameW * scale * 4;
            for (int k = 1; k < scale; k++)
                Buffer.MemoryCopy(line, line + k * _dibW, bytes, bytes);
        }
    }

    public void Hide()
    {
        if (!_visible) return;
        ShowWindow(Handle, SW_HIDE);
        _visible = false;
    }

    protected override void WndProc(ref Message m)
    {
        switch (m.Msg)
        {
            case WM_MOUSEACTIVATE:
                m.Result = (IntPtr)MA_NOACTIVATE;
                return;
            case WM_SETCURSOR:
                SetCursor(LoadCursor(IntPtr.Zero, IDC_CROSS));
                m.Result = (IntPtr)1;
                return;
            case WM_LBUTTONDOWN:
                Clicked?.Invoke();
                return;
        }
        base.WndProc(ref m);
    }

    private void FreeDib()
    {
        if (_dc == IntPtr.Zero) return;
        SelectObject(_dc, _oldBmp);
        DeleteObject(_dib);
        DeleteDC(_dc);
        _dc = _dib = _oldBmp = IntPtr.Zero;
        _bits = null;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (Handle != IntPtr.Zero) DestroyHandle();
        FreeDib();
    }
}
