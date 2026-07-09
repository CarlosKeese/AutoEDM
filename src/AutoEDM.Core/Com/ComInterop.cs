using System;
using System.Runtime.InteropServices;

namespace AutoEDM.Com
{
    /// <summary>
    /// Helpers de COM que funcionam tanto no .NET Framework 4.7.2 quanto no .NET
    /// moderno (.NET 5+/10, Windows).
    ///
    /// Motivo: <c>Marshal.GetActiveObject</c> foi REMOVIDO do .NET Core/5+. Aqui ele
    /// é reimplementado via P/Invoke (CLSIDFromProgID em ole32 + GetActiveObject em
    /// oleaut32), APIs presentes em qualquer versão do Windows. Com isso o
    /// <see cref="SolidEdgeConnector"/> não depende do runtime: para migrar para
    /// net10.0-windows basta trocar o TargetFramework e adicionar o pacote
    /// System.Drawing.Common (única outra dependência que muda entre runtimes).
    /// </summary>
    internal static class ComInterop
    {
        [DllImport("ole32.dll", PreserveSig = true)]
        private static extern int CLSIDFromProgID(
            [MarshalAs(UnmanagedType.LPWStr)] string progId, out Guid clsid);

        // PreserveSig=false: um HRESULT de falha (ex.: MK_E_UNAVAILABLE quando não
        // há instância em execução) é convertido automaticamente em COMException.
        [DllImport("oleaut32.dll", PreserveSig = false)]
        private static extern void GetActiveObject(
            ref Guid rclsid,
            IntPtr pvReserved,
            [MarshalAs(UnmanagedType.IUnknown)] out object ppunk);

        /// <summary>
        /// Equivalente portável a <c>Marshal.GetActiveObject(progId)</c>: devolve a
        /// instância COM já em execução registrada na Running Object Table, ou lança
        /// <see cref="COMException"/> (0x800401E3 MK_E_UNAVAILABLE) se não houver.
        /// </summary>
        public static object GetActiveObject(string progId)
        {
            if (progId == null) throw new ArgumentNullException(nameof(progId));

            int hr = CLSIDFromProgID(progId, out Guid clsid);
            if (hr < 0) Marshal.ThrowExceptionForHR(hr);

            GetActiveObject(ref clsid, IntPtr.Zero, out object result);
            return result;
        }
    }
}
