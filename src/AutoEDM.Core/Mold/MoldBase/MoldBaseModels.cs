using System;
using System.Collections.Generic;

namespace AutoEDM.Mold.MoldBase
{
    /// <summary>As placas do porta-molde de 2 placas, de cima (lado fixo) para baixo.</summary>
    public enum MoldPlate { PBS, P1, P2, PS, ESP, CPE, PE, PBI }

    /// <summary>
    /// Os grupos de furos: os das coordenadas gerais do catálogo (p.6) e os rebaixos das cabeças dos componentes
    /// (coluna no topo da P1, bucha no fundo da P2, pino de retorno no fundo da porta-extratores).
    /// </summary>
    public enum HoleGroup
    {
        Colunas, ParafusosMolde, GuiasExtratora, ParafusosExtratora, PinosRetorno,
        ColunasCabeca, BuchasCabeca, PinosRetornoCabeca, PinosGuia,
        ParafusosMoldeCabeca, ParafusosExtratoraCabeca, ParafusosBase, ParafusosFixacao,
    }

    /// <summary>
    /// O tipo de porta-molde, cada um com o seu padrão (W:\01 - Biblioteca\Padrões\Moldes, 2026-10-23):
    /// Polimold = catálogo de 2 placas; MUD = o molde TIPO GAVETA, a unidade de 246 × 170 que desliza no quadro
    /// de troca rápida 7160 (o quadro é da biblioteca e entra por referência); Babyplast = metades 76 × 76 (a pasta
    /// BABY PLAST, a mais usada); Zamak = a matriz MINI 160 × 160 com cavaletes e paralelos.
    /// </summary>
    public enum MoldBaseFamily { Polimold = 1, Mud = 2, Babyplast = 3, Zamak = 4 }

    /// <summary>Um trecho cilíndrico de um componente redondo, entre duas alturas do eixo LOCAL dele (mm).</summary>
    public sealed class RoundSegment
    {
        public double Diameter { get; set; }
        public double Z0 { get; set; }
        public double Z1 { get; set; }
        public RoundSegment() { }
        public RoundSegment(double diameter, double z0, double z1) { Diameter = diameter; Z0 = Math.Min(z0, z1); Z1 = Math.Max(z0, z1); }
        public double Volume => Math.PI * Diameter * Diameter / 4 * (Z1 - Z0);
    }

    /// <summary>
    /// Coluna, bucha ou pino: cilindros empilhados no eixo Z local (e um furo de passagem, na bucha). O arquivo
    /// leva o CÓDIGO (ex.: "CG 24 046 046"), como peça comprada — série nova, código novo, peça nova.
    /// </summary>
    public sealed class ComponentSpec
    {
        public string Code { get; set; }
        public string Kind { get; set; }
        public List<RoundSegment> Solids { get; } = new List<RoundSegment>();
        public RoundSegment Bore { get; set; }
        /// <summary>Onde vai a origem local de cada ocorrência: (sentido de A, sentido de C, altura a partir da partição).</summary>
        public List<double[]> Positions { get; } = new List<double[]>();
        public string FileName => Code + ".par";
        public double MinZ { get { double z = double.MaxValue; foreach (RoundSegment s in Solids) z = Math.Min(z, s.Z0); return z; } }
        public double MaxZ { get { double z = double.MinValue; foreach (RoundSegment s in Solids) z = Math.Max(z, s.Z1); return z; } }
        public double MaxDiameter { get { double d = 0; foreach (RoundSegment s in Solids) d = Math.Max(d, s.Diameter); return d; } }
    }

    /// <summary>Bolsão retangular centrado, cortado a partir da face de baixo ou de cima (o alojamento da extratora no MUD).</summary>
    public sealed class PocketSpec
    {
        public double Width { get; set; }
        public double Length { get; set; }
        public double Depth { get; set; }
        public bool FromBottom { get; set; } = true;
    }

    /// <summary>
    /// Ressalto retangular centrado, saindo da face de baixo ou de cima — o encaixe da placa 200 da gaveta no
    /// alojamento da 201.
    /// </summary>
    public sealed class BossSpec
    {
        public double Width { get; set; }
        public double Length { get; set; }
        public double Height { get; set; }
        public bool OnBottom { get; set; } = true;
    }

    /// <summary>
    /// Travas dos 4 cantos (Babyplast): quadrados de lado <see cref="Size"/> encostados nas duas bordas, saindo
    /// da face (ressalto) ou entrando nela (rebaixo).
    /// </summary>
    public sealed class CornerSpec
    {
        public double Size { get; set; }
        public double Height { get; set; }
        public bool Recess { get; set; }
        public bool OnBottom { get; set; } = true;
    }

    /// <summary>
    /// Rasgo das guias da gaveta: nas duas bordas do sentido de A, ao longo de todo o C, <see cref="Depth"/> para
    /// dentro e entre as alturas Z0..Z1 da placa (a partir da face de baixo; Z1 ≥ espessura = aberto em cima).
    /// </summary>
    public sealed class GrooveSpec
    {
        public double Depth { get; set; }
        public double Z0 { get; set; }
        public double Z1 { get; set; }
    }

    /// <summary>
    /// Degrau na borda: a placa fica <see cref="Inset"/> menor por lado nos últimos <see cref="Depth"/> mm a partir
    /// da face de baixo ou de cima (o Babyplast: 76 × 76 perto da partição, 75 × 75 atrás).
    /// </summary>
    public sealed class StepSpec
    {
        public double Inset { get; set; }
        public double Depth { get; set; }
        public bool FromBottom { get; set; }
    }

    /// <summary>
    /// Uma peça da BIBLIOTECA da casa posta por referência (quadro 7160, guias, puxadores, engates): o arquivo e a
    /// pose no referencial da submontagem — R linha a linha (p = R·p_local + t) e t em mm — copiada do padrão.
    /// </summary>
    public sealed class LibraryPart
    {
        public string Path { get; set; }
        public string Label { get; set; }
        public double[] R { get; set; }
        public double[] T { get; set; }
        public string FileName => System.IO.Path.GetFileName(Path);
    }

    /// <summary>Passante, ou cego entrando pela face de cima/de baixo da placa (no referencial do molde).</summary>
    public enum HoleDepthKind { Through, BlindFromTop, BlindFromBottom }

    public enum CheckLevel { Ok, Warning, Fail }

    /// <summary>Onde fica o centro do porta-molde na planta.</summary>
    public enum MoldBaseCentering { AssemblyOrigin, ContentCenter }

    /// <summary>
    /// Um grupo de furos numa placa: 4 simétricos; 2 sobre a linha de centro quando AcrossA = 0; ou só os 2 de
    /// UM lado (<see cref="Side"/> = +1/−1) — o caso do espaçador, cujo furo fica fora do centro da peça. Com
    /// <see cref="Middle"/>, mais 2 no meio dos lados de C (os 6 parafusos M8 da gaveta).
    /// </summary>
    public sealed class HoleSpec
    {
        public HoleGroup Group { get; set; }
        public double AcrossA { get; set; }
        public double AlongC { get; set; }
        public double Diameter { get; set; }
        public HoleDepthKind Depth { get; set; }
        public double DepthMm { get; set; }
        /// <summary>0 = simétrico; +1/−1 = só os dois furos do lado +A/−A (em ±AcrossA/2).</summary>
        public int Side { get; set; }
        /// <summary>Também no meio dos dois lados paralelos a C: (±AcrossA/2, 0); na linha de centro, (0, 0).</summary>
        public bool Middle { get; set; }
        /// <summary>Centros dados um a um (A, C) — o padrão com a furação fora de centro (anti-inversão do Zamak).</summary>
        public List<double[]> Points { get; set; }
        public bool OnCenterLine => Points == null && AcrossA < 1e-6;
        public int Count => Points != null ? Points.Count : OnCenterLine ? (Middle ? 3 : 2) : Side != 0 ? 2 : Middle ? 6 : 4;

        /// <summary>Prefixo das variáveis na peça e nome do recurso na árvore.</summary>
        public string Name => Group.ToString();
    }

    /// <summary>
    /// Uma placa do plano, no referencial do MOLDE: largura no sentido de A, comprimento no de C, e a altura
    /// medida a partir da linha de partição, positiva para o lado fixo. Os espaçadores são UMA peça em duas
    /// ocorrências (±OffsetA).
    /// </summary>
    public sealed class PlateSpec
    {
        public MoldPlate Plate { get; set; }
        public string Title { get; set; }
        public double Width { get; set; }
        public double Length { get; set; }
        public double Thickness { get; set; }
        public double BottomW { get; set; }
        public double OffsetA { get; set; }
        /// <summary>Ocorrências da mesma peça. Com 2 (espaçadores), a de −OffsetA vai GIRADA 180° na planta — por
        /// isso os furos fora de centro da peça caem certos dos dois lados.</summary>
        public int Count { get; set; } = 1;
        public List<HoleSpec> Holes { get; } = new List<HoleSpec>();
        public PocketSpec Pocket { get; set; }
        public BossSpec Boss { get; set; }
        public CornerSpec Corners { get; set; }
        public GrooveSpec Groove { get; set; }
        public StepSpec Step { get; set; }
        /// <summary>O número da casa no nome do arquivo (gaveta "201", Babyplast "101"); null = a sigla da placa.</summary>
        public string Code { get; set; }

        public string FileName => (Code ?? Plate.ToString()) + " - " + Title + ".par";
        public double TopW => BottomW + Thickness;
    }

    public sealed class PlanCheck
    {
        public string Name { get; set; }
        public CheckLevel Level { get; set; }
        public string Detail { get; set; }

        public PlanCheck(string name, CheckLevel level, string detail) { Name = name; Level = level; Detail = detail; }

        public override string ToString() =>
            (Level == CheckLevel.Ok ? "✓ " : Level == CheckLevel.Warning ? "⚠ " : "✗ ") + Name + ": " + Detail;
    }

    /// <summary>
    /// O que já está na montagem, no referencial de PLANTA (os dois eixos que não são a altura, na ordem da
    /// montagem) e com a altura medida a partir da linha de partição, positiva para o lado fixo.
    /// </summary>
    public sealed class MoldBaseContent
    {
        public double Min1 { get; set; }
        public double Max1 { get; set; }
        public double Min2 { get; set; }
        public double Max2 { get; set; }
        public double MinW { get; set; }
        public double MaxW { get; set; }
        public int Count { get; set; }

        /// <summary>Altura a extrair (mm), das peças marcadas como PRODUTO; 0 = não identificado.</summary>
        public double ProductHeight { get; set; }
        /// <summary>Tamanho da caixa dos produtos nos dois eixos de planta (mm); 0 = sem produto marcado.</summary>
        public double ProductSize1 { get; set; }
        public double ProductSize2 { get; set; }

        /// <summary>Extensão dos produtos no sentido de C (o comprimento do porta-molde), ou null.</summary>
        public double? ProductAlongC(MoldBasePlan plan)
        {
            double v = plan.Rotated ? ProductSize1 : ProductSize2;
            return v > 0 ? v : (double?)null;
        }
    }

    /// <summary>Escolhas da janela. Tudo em mm; os padrões são da casa e ficam gravados no config.json.</summary>
    public sealed class MoldBaseOptions
    {
        /// <summary>Margem entre as peças e a borda da área útil X×Y, por lado — o espaço da refrigeração em volta.</summary>
        public double SideMarginMm { get; set; } = 20;
        /// <summary>Fundo mínimo de aço entre as peças e a face de trás de P1/P2.</summary>
        public double FloorMm { get; set; } = 20;
        /// <summary>Folga somada à altura a extrair para chegar ao curso.</summary>
        public double StrokeClearanceMm { get; set; } = 10;
        /// <summary>Altura a extrair digitada (mm); 0 = a medida nas peças.</summary>
        public double ProductHeightMm { get; set; }
        public double CoolingDiameterMm { get; set; } = 8;
        /// <summary>Aço mínimo entre o canal e qualquer furo ou as peças (≥ 4–5 mm dos extratores — H. Neto / Thomazi).</summary>
        public double CoolingClearanceMm { get; set; } = 5;
        /// <summary>Distância mínima do canal à face externa da placa (≥ 15 mm — H. Neto / Thomazi).</summary>
        public double CoolingEdgeMm { get; set; } = 15;
        /// <summary>Pressão na cavidade para a flexão da placa suporte (40 MPa ≈ 400 kgf/cm², MISUMI #017).</summary>
        public double CavityPressureMpa { get; set; } = 40;
        /// <summary>Flexão admissível da placa suporte (0,05 mm — fonte secundária).</summary>
        public double MaxSupportDeflectionMm { get; set; } = 0.05;

        /// <summary>Linha de partição: coordenada da MONTAGEM no eixo de altura (mm). null = automática.</summary>
        public double? PartingMm { get; set; }
        /// <summary>Lado fixo (P1, PBS) para o sentido NEGATIVO do eixo de altura.</summary>
        public bool FixedSideNegative { get; set; }

        public MoldBaseFamily Family { get; set; } = MoldBaseFamily.Polimold;
        /// <summary>Polimold: colunas e buchas como peças. MUD e Babyplast: as peças de BIBLIOTECA do padrão (quadro
        /// 7160, guias, puxadores, batente / engate de extração).</summary>
        public bool WithGuides { get; set; } = true;
        /// <summary>Gerar os pinos de retorno como peças (só Polimold — os padrões da gaveta e do Babyplast não têm).</summary>
        public bool WithReturnPins { get; set; } = true;

        public MoldBaseCentering Centering { get; set; } = MoldBaseCentering.AssemblyOrigin;
        public bool AllowRotation { get; set; } = true;

        /// <summary>2 = rasgo de fixação (placas base com largura A); 4 = aba (largura B).</summary>
        public int Type { get; set; } = 4;
        /// <summary>1 = SAE 1045 (postiços); 3 = P20 em P1/P2.</summary>
        public int Steel { get; set; } = 1;
        /// <summary>1–6: guias da extratora e pinos de retorno (p.4).</summary>
        public int Mounting { get; set; } = 5;
        /// <summary>'A' cavidades, 'B' parcial (+ PBS e PS), 'C' completo.</summary>
        public char Guiding { get; set; } = 'B';

        // Escolhas forçadas (null/0 = automático).
        public string ForcedSeries { get; set; }
        public double ForcedP1 { get; set; }
        public double ForcedP2 { get; set; }
        public int ForcedStroke { get; set; }

        /// <summary>Peças (arquivo) fora / dentro da caixa e marcadas como produto / não produto na última rodada —
        /// lembradas para o projeto; peça nova segue a detecção.</summary>
        public List<string> ExcludedFiles { get; set; } = new List<string>();
        public List<string> IncludedFiles { get; set; } = new List<string>();
        public List<string> ProductFiles { get; set; } = new List<string>();
        public List<string> NonProductFiles { get; set; } = new List<string>();

        public MoldBaseOptions Clone()
        {
            var c = (MoldBaseOptions)MemberwiseClone();
            c.ExcludedFiles = new List<string>(ExcludedFiles ?? new List<string>());
            c.IncludedFiles = new List<string>(IncludedFiles ?? new List<string>());
            c.ProductFiles = new List<string>(ProductFiles ?? new List<string>());
            c.NonProductFiles = new List<string>(NonProductFiles ?? new List<string>());
            return c;
        }
    }

    /// <summary>O porta-molde escolhido, as placas e as conferências.</summary>
    public sealed class MoldBasePlan
    {
        public PolimoldSeries Series { get; set; }
        /// <summary>true = A corre no 2º eixo de planta (o porta-molde girado 90° na planta).</summary>
        public bool Rotated { get; set; }
        public double Center1 { get; set; }
        public double Center2 { get; set; }
        /// <summary>Partição usada, na coordenada da montagem (mm) — preenchida pelo construtor.</summary>
        public double PartingMm { get; set; }
        public double P1 { get; set; }
        public double P2 { get; set; }
        public int SubSeries { get; set; }
        public StrokeOption Stroke { get; set; }
        public double NeededStroke { get; set; }
        public double NeededX { get; set; }
        public double NeededY { get; set; }
        public MoldBaseOptions Options { get; set; }
        public MoldBaseFamily Family => Options?.Family ?? MoldBaseFamily.Polimold;
        public List<PlateSpec> Plates { get; } = new List<PlateSpec>();
        public List<ComponentSpec> Components { get; } = new List<ComponentSpec>();
        public List<LibraryPart> LibraryParts { get; } = new List<LibraryPart>();
        public List<PlanCheck> Checks { get; } = new List<PlanCheck>();
        public List<string> Notes { get; } = new List<string>();
        public string Problem { get; set; }

        public bool Ready => Problem == null && Series != null;
        public bool HasFailures => Checks.Exists(c => c.Level == CheckLevel.Fail);

        /// <summary>Código de pedido Polimold, ex.: "3035 4 15 1 2 5B" (série, tipo, sub-série, aço, curso, montagem);
        /// nos outros tipos, a descrição da unidade (ex.: "MUD 246×170 · 56/87").</summary>
        public string OrderCode
        {
            get
            {
                if (Series == null || Stroke == null) return "";
                if (Family != MoldBaseFamily.Polimold)
                    return $"{Series.Name} · {MoldBasePlanner.F(P1)}/{MoldBasePlanner.F(P2)}";
                return $"{Series.Code} {Options.Type} {(SubSeries < 0 ? "??" : SubSeries.ToString("00"))} {Options.Steel} {Stroke.Index} {Options.Mounting}{char.ToUpperInvariant(Options.Guiding)}";
            }
        }

        public double TotalHeight
        {
            get
            {
                double top = double.MinValue, bottom = double.MaxValue;
                foreach (PlateSpec p in Plates) { top = Math.Max(top, p.TopW); bottom = Math.Min(bottom, p.BottomW); }
                return Plates.Count == 0 ? 0 : top - bottom;
            }
        }
    }
}
