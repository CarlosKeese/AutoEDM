using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace AutoEDM.Com
{
    /// <summary>
    /// O próprio <c>IDispatch</c> (IID 00020400-…-46) como interface .NET. Todo objeto COM da SE
    /// responde a ele, então o cast nunca dá E_NOINTERFACE — ao contrário dos tipos da PIA
    /// Interop.SolidEdge 219, cujos IIDs o SE 223 nem sempre expõe.
    /// </summary>
    [ComImport]
    [Guid("00020400-0000-0000-C000-000000000046")]
    [InterfaceType(ComInterfaceType.InterfaceIsIDispatch)]
    public interface IDispatchItem { }

    /// <summary>
    /// Monta um <c>SAFEARRAY(IDispatch)</c> com objetos COM de tipos QUAISQUER — o que métodos como
    /// <c>StitchSurfaces.Add(n, SurfaceArray)</c> pedem. Um <c>object[]</c> vira SAFEARRAY(VARIANT)
    /// (DISP_E_TYPEMISMATCH); um array de tipo da PIA exige tipos iguais e o IID da PIA (a
    /// costura de StitchSurface + 'Limite' do EE01 deu E_NOINTERFACE, 2026-10-19).
    /// </summary>
    public static class DispatchArray
    {
        public static IDispatchItem[] From(IEnumerable<object> items)
        {
            var list = new List<IDispatchItem>();
            foreach (object o in items)
            {
                if (o == null) continue;
                list.Add((IDispatchItem)o);
            }
            return list.ToArray();
        }
    }
}
