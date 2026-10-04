using System;
using System.Runtime.InteropServices;

namespace VaporwaveToons;

/// <summary>
/// A window belongs to the virtual desktop it was created on, so after Win+Ctrl+Arrow the toons
/// would stay behind. The documented IVirtualDesktopManager can tell when that has happened;
/// the engine then recreates the toon windows, which puts them on the desktop now in view.
/// </summary>
internal sealed class VirtualDesktops
{
    [ComImport, Guid("a5cd92ff-29be-454c-8d04-d82879fb3f1b"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IVirtualDesktopManager
    {
        [PreserveSig] int IsWindowOnCurrentVirtualDesktop(IntPtr topLevelWindow, out int onCurrentDesktop);
        [PreserveSig] int GetWindowDesktopId(IntPtr topLevelWindow, out Guid desktopId);
        [PreserveSig] int MoveWindowToDesktop(IntPtr topLevelWindow, ref Guid desktopId);
    }

    [ComImport, Guid("aa509086-5ca9-4c25-8f95-589d3c07b48a")]
    private class VirtualDesktopManagerClass { }

    private IVirtualDesktopManager _vdm;

    public VirtualDesktops()
    {
        try { _vdm = (IVirtualDesktopManager)new VirtualDesktopManagerClass(); }
        catch (Exception) { _vdm = null; }   // older Windows, or the shell is not running
    }

    /// <summary>False when the window is on a virtual desktop other than the one being shown.</summary>
    public bool IsOnCurrentDesktop(IntPtr hwnd)
    {
        if (_vdm == null || hwnd == IntPtr.Zero) return true;
        try
        {
            return _vdm.IsWindowOnCurrentVirtualDesktop(hwnd, out int on) != 0 || on != 0;
        }
        catch (Exception)
        {
            _vdm = null;
            return true;
        }
    }
}
