using System;
using AutoEDM.Diagnostics;

namespace AutoEDM.Assembly
{
    /// <summary>
    /// ⚠️ PREMISSA FALSA (confirmado em ~11 runs): <c>Occurrence.Activate = true</c> NÃO
    /// entra em edição in-place — é apenas a flag de ATIVAÇÃO/carga da ocorrência
    /// (gestão de memória de montagens grandes). Os sinais reais de edição in-place são
    /// <c>AssemblyDocument.ModelingInAssembly</c> e <c>.InPlaceActivated</c>. Por isso a
    /// Inter-Part Copy via COM está efetivamente BLOQUEADA (ver InterPartCopier) e o fluxo
    /// de produção NÃO usa este escopo — os eletrodos são criados como peça standalone +
    /// AddByFilename + PutOrigin (ver ElectrodeBuilder.CreateElectrodesWithBlank).
    ///
    /// Esta classe permanece só para os EXPERIMENTOS (InterPartCopyProbe). NÃO a use no
    /// caminho de produção até existir um gesto COM confirmado de edição in-place.
    /// </summary>
    public sealed class EditInPlaceScope : IDisposable
    {
        private readonly dynamic _occurrence;
        private bool _active;

        /// <summary>
        /// Documento ativo após a ativação. Em edição in-place, este é o documento
        /// da ocorrência (eletrodo), não a montagem.
        /// </summary>
        public dynamic ActiveDocument { get; private set; }

        public EditInPlaceScope(dynamic occurrence)
        {
            _occurrence = occurrence ?? throw new ArgumentNullException(nameof(occurrence));
            Activate();
        }

        private void Activate()
        {
            try
            {
                _occurrence.Activate = true;
                _active = true;
                ActiveDocument = _occurrence.OccurrenceDocument;
                Log.Info("Edição in-place ativada.");
            }
            catch (Exception ex)
            {
                Log.Warn($"Não foi possível ativar edição in-place: {ex.Message}. " +
                         "As operações de geometria podem falhar até isso ser validado.");
            }
        }

        private void Deactivate()
        {
            if (!_active) return;

            try
            {
                _occurrence.Activate = false;
                Log.Info("Edição in-place encerrada.");
            }
            catch (Exception ex)
            {
                Log.Warn($"Occurrence.Activate = false falhou: {ex.Message}");
            }
            finally
            {
                _active = false;
                ActiveDocument = null;
            }
        }

        public void Dispose() => Deactivate();
    }
}
