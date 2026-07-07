using System.Runtime.InteropServices;
using Microsoft.Windows.Widgets.Providers;

namespace GlobalTeamWidget.Widget;

/// <summary>
/// Registers the widget provider as a COM class factory and keeps the
/// registration alive until Dispose() is called. Must be created on an STA thread.
/// </summary>
internal sealed class ComServer : IDisposable
{
    private uint _cookie;
    private readonly ClassFactory _factory;

    public ComServer(GlobalTeamWidgetProvider provider)
    {
        _factory = new ClassFactory(provider);
        var clsid = new Guid("A1B2C3D4-E5F6-7890-ABCD-EF1234567890");
        int hr = CoRegisterClassObject(ref clsid, _factory, CLSCTX_LOCAL_SERVER, REGCLS_MULTIPLEUSE, out _cookie);
        Marshal.ThrowExceptionForHR(hr);
    }

    public void Dispose()
    {
        if (_cookie != 0) { CoRevokeClassObject(_cookie); _cookie = 0; }
    }

    private const uint CLSCTX_LOCAL_SERVER = 4;
    private const uint REGCLS_MULTIPLEUSE  = 1;

    [DllImport("ole32.dll")]
    private static extern int CoRegisterClassObject(
        ref Guid rclsid,
        [MarshalAs(UnmanagedType.IUnknown)] object pUnk,
        uint dwClsContext, uint flags, out uint lpdwRegister);

    [DllImport("ole32.dll")]
    private static extern int CoRevokeClassObject(uint dwRegister);

    [ComVisible(true)]
    private sealed class ClassFactory : IClassFactory
    {
        private readonly GlobalTeamWidgetProvider _provider;
        public ClassFactory(GlobalTeamWidgetProvider provider) => _provider = provider;

        public int CreateInstance(IntPtr pUnkOuter, ref Guid riid, out IntPtr ppvObject)
        {
            ppvObject = IntPtr.Zero;
            if (pUnkOuter != IntPtr.Zero)
                return unchecked((int)0x80040110);
            ppvObject = WinRT.MarshalInterface<IWidgetProvider>.FromManaged(_provider);
            return 0;
        }

        public int LockServer(bool fLock) => 0;
    }

    [ComImport]
    [Guid("00000001-0000-0000-C000-000000000046")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IClassFactory
    {
        [PreserveSig] int CreateInstance(IntPtr pUnkOuter, ref Guid riid, out IntPtr ppvObject);
        [PreserveSig] int LockServer(bool fLock);
    }
}
