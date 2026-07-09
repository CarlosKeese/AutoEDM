using System;
using System.Runtime.InteropServices;

namespace AutoEDM.Com
{
    /// <summary>
    /// Standard single-threaded-apartment (STA) OLE message filter.
    ///
    /// Solid Edge is a busy, out-of-process COM server. When it is mid-operation
    /// (rebuilding, rendering, showing a modal dialog) it rejects incoming calls
    /// with RPC_E_CALL_REJECTED / RPC_E_SERVERCALL_RETRYLATER, which surfaces in
    /// .NET as a COMException (0x80010001 / 0x8001010A). Registering this filter
    /// tells COM to transparently retry those calls instead of throwing.
    ///
    /// Register once on the STA thread that drives Solid Edge:
    ///     OleMessageFilter.Register();
    /// and revoke on shutdown:
    ///     OleMessageFilter.Revoke();
    /// </summary>
    [ComImport, Guid("00000016-0000-0000-C000-000000000046"),
     InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IOleMessageFilter
    {
        [PreserveSig]
        int HandleInComingCall(int dwCallType, IntPtr hTaskCaller, int dwTickCount, IntPtr lpInterfaceInfo);

        [PreserveSig]
        int RetryRejectedCall(IntPtr hTaskCallee, int dwTickCount, int dwRejectType);

        [PreserveSig]
        int MessagePending(IntPtr hTaskCallee, int dwTickCount, int dwPendingType);
    }

    public class OleMessageFilter : IOleMessageFilter
    {
        // SERVERCALL_RETRYLATER == 2. Any reject type >= 2 is retryable.
        private const int ServerCallRetryLater = 2;

        [DllImport("Ole32.dll")]
        private static extern int CoRegisterMessageFilter(IOleMessageFilter newFilter, out IOleMessageFilter oldFilter);

        /// <summary>Install the filter on the current (STA) thread.</summary>
        public static void Register()
        {
            IOleMessageFilter dummy;
            int hr = CoRegisterMessageFilter(new OleMessageFilter(), out dummy);
            if (hr != 0)
                throw new COMException("Failed to register OLE message filter.", hr);
        }

        /// <summary>Remove the filter.</summary>
        public static void Revoke()
        {
            IOleMessageFilter dummy;
            CoRegisterMessageFilter(null, out dummy);
        }

        // Accept all incoming calls. SERVERCALL_ISHANDLED == 0.
        int IOleMessageFilter.HandleInComingCall(int dwCallType, IntPtr hTaskCaller, int dwTickCount, IntPtr lpInterfaceInfo)
            => 0;

        int IOleMessageFilter.RetryRejectedCall(IntPtr hTaskCallee, int dwTickCount, int dwRejectType)
        {
            // If the server is busy, retry after 250 ms. Return value >= 0 and
            // < 100 means "retry immediately"; here we ask COM to wait 250 ms.
            if (dwRejectType == ServerCallRetryLater)
                return 250;

            // Otherwise cancel the call (-1).
            return -1;
        }

        // PENDINGMSG_WAITDEFPROCESS == 2: keep waiting, let the message dispatch.
        int IOleMessageFilter.MessagePending(IntPtr hTaskCallee, int dwTickCount, int dwPendingType)
            => 2;
    }
}
