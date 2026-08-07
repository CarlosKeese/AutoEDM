using System;
using System.Runtime.InteropServices;

namespace AutoEDM.Com
{
    /// <summary>
    /// Ciclo de vida de RCWs (Runtime Callable Wrapper) do Solid Edge (revisão 2026-07-23,
    /// docs/REVISAO-AutoEDM.md P1.1): cada acesso via <c>dynamic</c> a um objeto do Solid Edge
    /// cria um RCW que segura uma referência do lado do CAD. Num laço que percorre milhares de
    /// faces sem soltar essas referências, o processo do Solid Edge acumula memória/handles e
    /// pode não fechar direito numa sessão longa (RPC/"servidor ocupado" em operações demoradas).
    ///
    /// Regra prática: libere só o que você ENUMEROU e DESCARTOU dentro de um laço quente (faces/
    /// corpos/coleções temporárias que não sobrevivem além do laço). NUNCA libere um objeto que o
    /// chamador ainda vai usar (ex.: a face "vencedora" guardada em <c>SelectedFace.ComFace</c>,
    /// usada depois por cópia/offset) nem o Application/documento ativo — quem obtém, libera.
    /// </summary>
    public static class ComLifetime
    {
        /// <summary>Libera um RCW se for um objeto COM de verdade; nunca lança (best-effort).</summary>
        public static void Release(object o)
        {
            if (o == null) return;
            try { if (Marshal.IsComObject(o)) Marshal.ReleaseComObject(o); }
            catch { }
        }

        /// <summary>Wrapper descartável: <c>using (ComLifetime.Scoped(obj)) { ... }</c> libera ao
        /// sair do bloco — para objetos cuja vida útil é claramente um laço/bloco só.</summary>
        public static ScopedCom Scoped(object o) => new ScopedCom(o);
    }

    /// <summary>Ver <see cref="ComLifetime.Scoped"/>.</summary>
    public readonly struct ScopedCom : IDisposable
    {
        private readonly object _o;
        public ScopedCom(object o) { _o = o; }
        public void Dispose() => ComLifetime.Release(_o);
    }
}
