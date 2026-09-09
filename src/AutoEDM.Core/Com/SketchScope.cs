using System;
using System.Collections.Generic;
using AutoEDM.Diagnostics;

namespace AutoEDM.Com
{
    /// <summary>
    /// Dono de TUDO que uma operação de modelagem cria só para se apoiar: os esboços
    /// (<c>ProfileSets</c>) e os planos de referência temporários. No <see cref="Dispose"/>
    /// apaga cada um, CONFERE que sumiu de verdade e loga o que sobrou.
    ///
    /// POR QUE ISTO EXISTE (Carlos, 2026-09-08). O alojamento de O'ring deixava a peça cheia de
    /// esboços que o usuário não conseguia apagar. Três causas somadas:
    ///
    /// 1. <c>ProfileSets.Add()</c> cria um esboço ORDENADO mesmo numa peça SÍNCRONA. Consumido
    ///    por um recurso síncrono, ele fica no nó "Ordenado" do PathFinder sem dono — e a
    ///    interface do SE se recusa a apagar.
    /// 2. Cada corte cria até CINCO esboços: um por plano-base sondado (para descobrir onde o
    ///    plano está) mais o definitivo. Com "vários furos por vez", multiplique por furo. E não
    ///    dá para juntar as sondagens num esboço só: um <c>ProfileSet</c> aceita UM perfil, o
    ///    segundo <c>Profiles.Add</c> devolve E_FAIL (medido em peça real, log 083000). Se são
    ///    cinco mesmo, que sejam cinco CONFERIDOS.
    /// 3. Todos os <c>Delete()</c> de sondagem estavam em <c>try { } catch { }</c> MUDO. Quando
    ///    falhavam — que é o caso que interessa — não havia uma linha sequer no log; o problema
    ///    só aparecia na peça do usuário, sem rastro.
    ///
    /// A regra que este tipo impõe: <b>todo esboço criado por código nasce dentro de um escopo,
    /// e todo escopo é conferido no fim.</b> Um esboço que resistiu à exclusão é ERRO logado com
    /// nome e ambiente, nunca silêncio.
    /// </summary>
    public sealed class SketchScope : IDisposable
    {
        private readonly dynamic _doc;
        private readonly string _what;
        private readonly List<object> _profileSets = new List<object>();
        private readonly List<object> _tempPlanes = new List<object>();
        private bool _disposed;

        /// <summary>Quantos esboços resistiram à exclusão (0 = peça limpa). Só vale após o Dispose.</summary>
        public int LeftBehind { get; private set; }

        public SketchScope(dynamic doc, string what)
        {
            _doc = doc;
            _what = what ?? "operação";
        }

        /// <summary>Cria um esboço JÁ rastreado. Use sempre isto em vez de <c>doc.ProfileSets.Add()</c>.</summary>
        public dynamic AddProfileSet()
        {
            dynamic ps = _doc.ProfileSets.Add();
            if (ps != null) _profileSets.Add((object)ps);
            return ps;
        }

        /// <summary>Registra um plano de referência criado por código, para sumir no fim.</summary>
        public void TrackTempPlane(object plane)
        {
            if (plane != null) _tempPlanes.Add(plane);
        }

        /// <summary>Apaga um esboço ANTES da hora (tentativa que não vingou) — continua conferido e logado.</summary>
        public void DropProfileSet(object ps)
        {
            if (ps == null) return;
            _profileSets.Remove(ps);
            if (!DeleteProfileSet(ps)) LeftBehind++;
        }

        /// <summary>
        /// Tira um item do escopo: ele FICA na peça. Use quando um recurso passou a ser dono
        /// dele — em modelagem ordenada o esboço e o plano do corte pertencem ao recurso, e
        /// apagá-los mataria o próprio recurso que acabou de nascer.
        /// </summary>
        public void Release(object item)
        {
            if (item == null) return;
            _profileSets.Remove(item);
            _tempPlanes.Remove(item);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            foreach (object ps in _profileSets)
                if (!DeleteProfileSet(ps)) LeftBehind++;
            _profileSets.Clear();

            foreach (object plane in _tempPlanes) DeleteTempPlane(plane);
            _tempPlanes.Clear();

            if (LeftBehind > 0)
                Log.Error($"{_what}: {LeftBehind} esboço(s) do AutoEDM ficaram na peça e o Solid Edge não deixou " +
                          "apagar. Eles aparecem no nó 'Ordenado' do PathFinder e provavelmente NÃO podem ser " +
                          "removidos pela interface — desfaça (Ctrl+Z) até antes desta operação se quiser a peça limpa.");
        }

        private bool DeleteProfileSet(object ps) => DeleteVerified(_doc, ps, _what);

        /// <summary>
        /// Apaga um esboço e CONFERE pela contagem da coleção — para quem cria o esboço fora de
        /// um escopo (o <see cref="AutoEDM.Electrode.BlankModeler"/>, que constrói e consome o
        /// perfil na mesma linha reta). O <c>Delete()</c> do SE pode não lançar e mesmo assim
        /// não remover nada — esboço travado por um recurso do OUTRO ambiente. Por isso o
        /// try/catch sozinho não serve: tem de contar antes e depois.
        /// </summary>
        public static bool DeleteVerified(dynamic doc, object ps, string what)
        {
            if (ps == null) return true;
            int before = CountOf(doc);
            try { ((dynamic)ps).Delete(); }
            catch (Exception e)
            {
                Log.Warn($"{what}: ProfileSet.Delete() falhou — {e.GetBaseException().Message}");
                return false;
            }

            int after = CountOf(doc);
            if (before < 0 || after < 0) return true;   // sem contagem legível, confia no Delete que não lançou
            if (after < before) return true;

            Log.Warn($"{what}: ProfileSet.Delete() não lançou, mas a coleção continua com {after} esboço(s) " +
                     $"(era {before}) — o esboço ficou preso no ambiente ORDENADO e o usuário não vai " +
                     "conseguir apagá-lo pela interface do SE.");
            return false;
        }

        private static int CountOf(dynamic doc)
        {
            try { return (int)doc.ProfileSets.Count; } catch { return -1; }
        }

        /// <summary>
        /// Plano temporário que um recurso PASSOU A USAR: só esconder. Apagá-lo invalidaria o
        /// recurso que acabou de nascer, e um plano de referência escondido não atrapalha
        /// ninguém — some da tela e da árvore visível, e vai embora junto com o recurso.
        /// </summary>
        public void HideTempPlane(object plane)
        {
            if (plane == null) return;
            _tempPlanes.Remove(plane);
            try { ((dynamic)plane).Visible = false; }
            catch (Exception e) { Log.Warn($"{_what}: o plano do recurso ficou VISÍVEL na peça — {e.GetBaseException().Message}"); }
        }

        /// <summary>Plano temporário NÃO usado: apagar é o certo; se algo já o consumiu,
        /// esconder é o melhor possível.</summary>
        private void DeleteTempPlane(object plane)
        {
            try { ((dynamic)plane).Delete(); return; }
            catch (Exception e) { Log.Info($"{_what}: o plano temporário não pôde ser apagado ({e.GetBaseException().Message}) — escondendo."); }
            try { ((dynamic)plane).Visible = false; }
            catch (Exception e) { Log.Warn($"{_what}: o plano temporário ficou VISÍVEL na peça — {e.GetBaseException().Message}"); }
        }

    }
}
