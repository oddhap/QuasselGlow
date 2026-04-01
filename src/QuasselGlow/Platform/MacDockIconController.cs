using System.Runtime.InteropServices;

namespace QuasselGlow.Platform;

internal static class MacDockIconController
{
    private static readonly IntPtr NSApplicationClass = objc_getClass("NSApplication");
    private static readonly IntPtr SharedApplicationSelector = sel_registerName("sharedApplication");
    private static readonly IntPtr SetActivationPolicySelector = sel_registerName("setActivationPolicy:");

    public static void SetDockIconVisible(bool isVisible)
    {
        if (!OperatingSystem.IsMacOS())
        {
            return;
        }

        try
        {
            var sharedApplication = GetSharedApplication();
            if (sharedApplication == IntPtr.Zero)
            {
                return;
            }

            var activationPolicy = isVisible
                ? (nint)NSApplicationActivationPolicy.Regular
                : (nint)NSApplicationActivationPolicy.Accessory;
            objc_msgSend_bool(sharedApplication, SetActivationPolicySelector, activationPolicy);
        }
        catch (DllNotFoundException)
        {
        }
        catch (EntryPointNotFoundException)
        {
        }
    }

    private static IntPtr GetSharedApplication()
    {
        if (NSApplicationClass == IntPtr.Zero)
        {
            return IntPtr.Zero;
        }

        return objc_msgSend(NSApplicationClass, SharedApplicationSelector);
    }

    private enum NSApplicationActivationPolicy : long
    {
        Regular = 0,
        Accessory = 1,
        Prohibited = 2
    }

    [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_getClass")]
    private static extern IntPtr objc_getClass(string name);

    [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "sel_registerName")]
    private static extern IntPtr sel_registerName(string selectorName);

    [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
    private static extern IntPtr objc_msgSend(IntPtr receiver, IntPtr selector);

    [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
    [return: MarshalAs(UnmanagedType.I1)]
    private static extern bool objc_msgSend_bool(IntPtr receiver, IntPtr selector, nint argument);
}
