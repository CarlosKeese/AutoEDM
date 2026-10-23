using System;
using System.Linq;
using System.Text;

namespace AutoEDM.Mold.MoldBase
{
    /// <summary>O plano do porta-molde em texto — o mesmo na janela do botão e na ferramenta MCP.</summary>
    public static class MoldBaseReport
    {
        public static string Text(MoldBasePlan plan, MoldBaseScan scan)
        {
            var sb = new StringBuilder();
            string nl = Environment.NewLine;
            if (scan?.Existing != null)
                sb.Append("Porta-molde atual: ").Append(scan.Existing.OrderCode ?? "(sem código)").Append(" — ").Append(scan.Existing.Path).Append(nl).Append(nl);
            if (plan == null) return sb.ToString();
            if (!plan.Ready) return sb.Append("✗ ").Append(plan.Problem).ToString();

            PolimoldSeries s = plan.Series;
            MoldBaseOptions o = plan.Options;
            bool poli = plan.Family == MoldBaseFamily.Polimold;
            if (poli)
            {
                sb.Append("Polimold série ").Append(s.Name).Append(" (").Append(F(s.A)).Append(" × ").Append(F(s.C)).Append(")   código ").Append(plan.OrderCode).Append(nl);
                sb.Append("Tipo ").Append(o.Type).Append(o.Type == 4 ? " (aba)" : " (rasgo)")
                  .Append(" · aço ").Append(o.Steel).Append(o.Steel == 3 ? " (P20 em P1/P2)" : " (SAE 1045)")
                  .Append(" · montagem ").Append(o.Mounting).Append(char.ToUpperInvariant(o.Guiding)).Append(nl);
                sb.Append("P1 ").Append(F(plan.P1)).Append(" · P2 ").Append(F(plan.P2)).Append(" (sub-série ").Append(plan.SubSeries < 0 ? "fora do padrão" : plan.SubSeries.ToString("00")).Append(")")
                  .Append(" · curso ").Append(plan.Stroke.Index).Append(" = ").Append(F(plan.Stroke.Stroke)).Append(" (espaçadores ").Append(F(plan.Stroke.SpacerHeight)).Append(")").Append(nl);
            }
            else
            {
                sb.Append(MoldBaseStandards.FamilyLabel(plan.Family)).Append(" — ").Append(F(s.A)).Append(" × ").Append(F(s.C)).Append(nl);
                if (plan.Family == MoldBaseFamily.Mud)
                    sb.Append("Placa fixa 100 = ").Append(F(plan.P1)).Append(" · placa móvel 200 = ").Append(F(plan.P2))
                      .Append(" + suporte 201 = ").Append(F(MoldBaseStandards.MudMovingThickness - plan.P2))
                      .Append(" · curso ").Append(F(plan.Stroke.Stroke)).Append(nl);
                else if (plan.Family == MoldBaseFamily.Zamak)
                    sb.Append("Fixo = ").Append(F(plan.P1)).Append(" · móvel = ").Append(F(plan.P2))
                      .Append(" · paralelos ").Append(F(2 * MoldBaseStandards.ZamakEjectorThickness + plan.Stroke.Stroke))
                      .Append(" · curso ").Append(F(plan.Stroke.Stroke)).Append(nl);
                else
                    sb.Append("Metade fixa 100 = ").Append(F(plan.P1)).Append(" · metade móvel 200 = ").Append(F(plan.P2))
                      .Append(" · curso ").Append(F(plan.Stroke.Stroke)).Append(nl);
            }
            sb.Append("Altura total ").Append(F(plan.TotalHeight)).Append(" · partição em ").Append(F(plan.PartingMm))
              .Append(" · centro (").Append(F(plan.Center1)).Append("; ").Append(F(plan.Center2)).Append(")")
              .Append(plan.Rotated ? " · girado 90°" : "").Append(o.FixedSideNegative ? " · lado fixo no sentido negativo" : "").Append(nl).Append(nl);

            foreach (PlanCheck c in plan.Checks) sb.Append(c).Append(nl);
            foreach (string n in plan.Notes) sb.Append("• ").Append(n).Append(nl);
            if (plan.Components.Count > 0)
            {
                sb.Append(nl).Append("Componentes (peça por código, posição no plano):").Append(nl);
                foreach (ComponentSpec c in plan.Components)
                    sb.Append("  ").Append(c.Kind.PadRight(16)).Append(c.Code.PadRight(18)).Append(c.Positions.Count).Append("×  Ø")
                      .Append(F(c.MaxDiameter)).Append(" × ").Append(F(c.MaxZ - c.MinZ)).Append(nl);
            }
            if (plan.LibraryParts.Count > 0)
            {
                sb.Append(nl).Append("Peças da biblioteca (por referência, na posição do padrão):").Append(nl);
                foreach (IGrouping<string, LibraryPart> g in plan.LibraryParts.GroupBy(p => p.FileName))
                    sb.Append("  ").Append(g.First().Label.PadRight(22)).Append(g.Key).Append(g.Count() > 1 ? $"  ×{g.Count()}" : "").Append(nl);
            }
            sb.Append(nl).Append("Placas (largura × comprimento × espessura, base a partir da partição):").Append(nl);
            foreach (PlateSpec p in plan.Plates)
            {
                sb.Append("  ").Append((p.Code ?? p.Plate.ToString()).PadRight(4)).Append(F(p.Width)).Append(" × ").Append(F(p.Length)).Append(" × ").Append(F(p.Thickness))
                  .Append(" em ").Append(F(p.BottomW)).Append(p.Count > 1 ? $"  ×{p.Count} (±{F(p.OffsetA)})" : "");
                if (p.Holes.Count > 0)
                    sb.Append("  — ").Append(string.Join(", ", p.Holes.Select(h =>
                        $"{h.Group} {h.Count}×Ø{F(h.Diameter)}{(h.Depth == HoleDepthKind.Through ? "" : " p" + F(h.DepthMm))}")));
                if (p.Pocket != null)
                    sb.Append("  — alojamento ").Append(F(p.Pocket.Width)).Append(" × ").Append(F(p.Pocket.Length)).Append(" × ")
                      .Append(p.Pocket.Depth >= p.Thickness - 1e-6 ? "passante" : F(p.Pocket.Depth));
                if (p.Boss != null) sb.Append("  — ressalto ").Append(F(p.Boss.Width)).Append(" × ").Append(F(p.Boss.Length)).Append(" × ").Append(F(p.Boss.Height));
                if (p.Corners != null) sb.Append(p.Corners.Recess ? "  — rebaixos de canto " : "  — travas de canto ").Append(F(p.Corners.Size)).Append(" × ").Append(F(p.Corners.Height));
                if (p.Groove != null) sb.Append("  — rasgos das guias ").Append(F(p.Groove.Depth)).Append(" em ").Append(F(p.Groove.Z0)).Append("..").Append(F(p.Groove.Z1));
                if (p.Step != null) sb.Append("  — degrau ").Append(F(p.Step.Inset)).Append(" por lado nos ").Append(F(p.Step.Depth)).Append(p.Step.FromBottom ? " de baixo" : " de cima");
                sb.Append(nl);
            }
            sb.Append(nl).Append(poli
                ? "Furos de parafuso no Ø nominal da rosca (referência de posição); colunas, buchas e pinos pelo catálogo de componentes Polimold. " +
                  "Placas extratoras com o comprimento C (não tabelado). Dados: catálogo Polimold CATPM2P (jun/2013) e o 15354 (3035) da biblioteca."
                : plan.Family == MoldBaseFamily.Mud
                    ? @"Padrão TIPO GAVETA (W:\01 - Biblioteca\Padrões\Moldes): 246 × 170, fixa 46, móvel 200 + 201 = 87, extratoras 184 × 108 × 12 dentro da 201."
                    : plan.Family == MoldBaseFamily.Zamak
                        ? @"Padrão ZAMAK\MINI (W:\01 - Biblioteca\Padrões\Moldes): 160 × 160, fixo 39, móvel 40, paralelos 45, extratoras 130 × 80 × 16, cavaletes 200 × 220 × 20."
                        : @"Padrão BABY PLAST (W:\01 - Biblioteca\Padrões\Moldes): metades 76 × 76 (75 × 75 nos 21 de trás) de 40 e 44, apoio 74 × 74 × 6, extratoras 70 × 70 × 12.");
            return sb.ToString();
        }

        private static string F(double v) => MoldBasePlanner.F(v);
    }
}
