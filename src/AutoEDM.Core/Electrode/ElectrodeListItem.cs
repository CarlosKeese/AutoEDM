using System.Collections.Generic;

namespace AutoEDM.Electrode
{
    /// <summary>
    /// Uma linha da janela "Coordenadas" (Carlos, 2026-08-04): o usuário SELECIONA na
    /// montagem os eletrodos de interesse (ocorrências) e clica — nada de detecção
    /// automática por cor. A posição é a mesma que aparece em "Propriedades de
    /// Ocorrência" no SE (<c>Occurrence.GetTransform</c>, via
    /// <see cref="AutoEDM.Assembly.AssemblyContext.TryGetPlacement"/>); Gap/Ra vêm da
    /// peça do eletrodo (mesma fonte usada por "Duplicar eletrodo": variável
    /// <see cref="RaVariableStore"/> + feature <c>Model.FaceOffsets</c> nomeada
    /// "GAP: ... - Ra: ..."). SOMENTE-LEITURA.
    /// </summary>
    public sealed class ElectrodeListItem
    {
        /// <summary>Nome da ocorrência na montagem (ex.: "15142.200_EDM_EE01").</summary>
        public string Name { get; set; }

        /// <summary>Posição da ocorrência (mm, zero-máquina = origem da montagem).</summary>
        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }

        /// <summary>Rotação Z da ocorrência (graus) — mesmo valor de "Propriedades de Ocorrência".</summary>
        public double AzDeg { get; set; }

        /// <summary>True se a posição (GetTransform) foi lida com sucesso.</summary>
        public bool PositionKnown { get; set; }

        /// <summary>GAP aplicado (mm), se achado na peça (feature Model.FaceOffsets). Null = não achado.</summary>
        public double? GapMm { get; set; }

        /// <summary>Ra (µm) gravado na peça (variável ou nome da feature de GAP). Null = não achado.</summary>
        public double? Ra { get; set; }

        /// <summary>Avisos por linha (ex.: posição/Gap/Ra não encontrados).</summary>
        public List<string> Notes { get; } = new List<string>();
    }
}
