using System;
using AutoEDM.Diagnostics;

namespace AutoEDM.Assembly
{
    /// <summary>
    /// Ativa uma ocorrência para edição in-context (in-place) e garante a
    /// desativação ao sair do escopo — inclusive se houver exceção. Toda operação
    /// que MODIFICA a peça do eletrodo dentro da montagem (Inter-Part Copy, offset,
    /// blank, furos) deve rodar dentro deste escopo.
    ///
    /// No SE 2023/2026, <c>Occurrence.Activate</c> é uma propriedade booleana,
    /// não um método. Atribuir <c>true</c> entra em edição in-place; atribuir
    /// <c>false</c> retorna à montagem.
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
