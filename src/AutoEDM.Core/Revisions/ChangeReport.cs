using System;
using System.Collections.Generic;
using System.Linq;

namespace AutoEDM.Revisions
{
    /// <summary>
    /// Uma caixa de seleção da folha MD (Carlos, 2026-09-17): o que a oficina precisa fazer com a
    /// peça alterada. O catálogo dos rótulos é do <see cref="ChangeTaskCatalog"/> — o modelo do SE
    /// não sabe nada disso, quem marca é o projetista na janela do relatório.
    /// </summary>
    public sealed class ChangeTask
    {
        /// <summary>Bloco da folha: "AÇÃO NECESSÁRIA", "NECESSIDADE DE SERVIÇO EXTERNO", "TAREFAS NECESSÁRIAS".</summary>
        public string Group { get; set; }

        /// <summary>Texto da linha, como está na folha (ex.: "SOLDA TIG / LASER").</summary>
        public string Label { get; set; }

        /// <summary>Marcada?</summary>
        public bool Checked { get; set; }

        /// <summary>Complemento digitado (ex.: a quantidade do "FABRICAR, QUANTIDADE:"). Null = nenhum.</summary>
        public string Detail { get; set; }

        public ChangeTask Clone() =>
            new ChangeTask { Group = Group, Label = Label, Checked = Checked, Detail = Detail };
    }

    /// <summary>Os blocos de caixas de seleção da folha MD, na ordem em que ela imprime.</summary>
    public static class ChangeTaskCatalog
    {
        public const string ActionGroup = "AÇÃO NECESSÁRIA";
        public const string ExternalGroup = "NECESSIDADE DE SERVIÇO EXTERNO";
        public const string TaskGroup = "TAREFAS NECESSÁRIAS";

        /// <summary>
        /// Cópia nova das caixas em branco (a folha MD-14309 conferida com o Carlos em 2026-09-17).
        /// É uma cópia: cada peça marca as suas sem mexer nas das outras.
        /// </summary>
        public static List<ChangeTask> NewSheet()
        {
            var sheet = new List<ChangeTask>();
            Add(sheet, ActionGroup, "FABRICAR, QUANTIDADE:", "RECUPERAR PEÇA NÃO CONFORME");
            Add(sheet, ExternalGroup,
                "SOLDA TIG / LASER",
                "GRAVAÇÃO / TEXTURIZAÇÃO",
                "EROSÃO / FURO RÁPIDO / FIO",
                "TRATAMENTO SUPERFICIAL",
                "TRATAMENTO TÉRMICO",
                "USINAGEM / FURAÇÃO PROFUNDA",
                "RETÍFICA PLANA / CILÍNDRICA");
            Add(sheet, TaskGroup,
                "VERIF. MATERIAL EM ESTOQUE / COMPRAR",
                "UTILIZAR O MESMO PROCESSO",
                "ATUALIZAR DESENHO DE PROCESSO / CAM",
                "DESENVOLVER NOVO PROCESSO / CAM",
                "DESENHAR ELETRODOS",
                "DESENHAR CORTE A FIO");
            return sheet;
        }

        /// <summary>Os blocos, na ordem da folha.</summary>
        public static IReadOnlyList<string> Groups => new[] { ActionGroup, ExternalGroup, TaskGroup };

        private static void Add(List<ChangeTask> sheet, string group, params string[] labels)
        {
            foreach (string label in labels) sheet.Add(new ChangeTask { Group = group, Label = label });
        }
    }

    /// <summary>
    /// Uma PEÇA alterada na revisão — uma "folha" do relatório. O que vem do modelo: arquivo,
    /// caminho e o RASCUNHO das operações lidas no grupo "Rev.N" da árvore ordenada. O que o
    /// projetista digita na janela: <see cref="Description"/>, <see cref="Actions"/> e as caixas.
    /// </summary>
    public sealed class PartChange
    {
        /// <summary>Nome do arquivo (ex.: "14309.205.par").</summary>
        public string FileName { get; set; }

        /// <summary>Caminho completo — vai na linha "LOCALIZAÇÃO DO ARQUIVO".</summary>
        public string FullPath { get; set; }

        /// <summary>Sai no relatório? A varredura traz tudo que tem revisão e o usuário desmarca o que não vale.</summary>
        public bool Include { get; set; } = true;

        /// <summary>
        /// Revisão que ESTA peça tem (o N do grupo "Rev.N" mais recente dela, ou o da PROPRIEDADE
        /// do arquivo quando a peça é nova). 0 = não lida. Pode ser menor que a do relatório: peça
        /// que parou na Rev.1 enquanto a montagem já vai na Rev.2 não foi alterada agora, e a
        /// varredura a traz desmarcada em vez de escondida.
        /// </summary>
        public int Revision { get; set; }

        /// <summary>
        /// PEÇA NOVA da revisão: a revisão veio da PROPRIEDADE do arquivo, não de um grupo na
        /// árvore (Carlos, 2026-09-18 — peça nova não tem recurso alterado para agrupar). Quem é
        /// nova já nasce com "FABRICAR, QUANTIDADE" marcado — ver <see cref="MarkAsNewPart"/>.
        /// </summary>
        public bool IsNew { get; set; }

        /// <summary>De onde saiu a revisão, para o log e para a janela explicarem ("grupo Rev.2",
        /// "propriedade 'Revision Number'"). Null = não lida.</summary>
        public string RevisionSource { get; set; }

        /// <summary>Quantas vezes o arquivo aparece na montagem — a QUANTIDADE a fabricar.</summary>
        public int Positions { get; set; } = 1;

        /// <summary>Linha da peça na "LISTA DE PEÇAS ALTERADAS" (ex.: "Adicionado postiço para reparar machos").</summary>
        public string Description { get; set; }

        /// <summary>"AÇÕES INDICADAS", numeradas 1..n na folha — o que a oficina tem de fazer.</summary>
        public List<string> Actions { get; } = new List<string>();

        /// <summary>Caixas de seleção desta peça (cópia do <see cref="ChangeTaskCatalog.NewSheet"/>).</summary>
        public List<ChangeTask> Tasks { get; set; } = ChangeTaskCatalog.NewSheet();

        /// <summary>
        /// Nomes das operações lidas no grupo da revisão (ex.: "Recorte 12", "Furo 3"). NÃO sai na
        /// folha: serve de rascunho na janela, para lembrar o que foi mexido sem abrir a peça.
        /// </summary>
        public List<string> Features { get; } = new List<string>();

        /// <summary>Arquivo da miniatura isométrica exportada (PNG ao lado do relatório). Null = sem imagem.</summary>
        public string ThumbnailFile { get; set; }

        /// <summary>
        /// Documento COM da peça e o GRUPO da revisão nela — vivos enquanto a montagem estiver
        /// aberta (a janela é modal, então está). Guardados na varredura para a miniatura não ter
        /// de procurar a ocorrência de novo nem reler a árvore.
        /// </summary>
        public object PartDocument { get; set; }
        public object RevisionFeature { get; set; }

        /// <summary>
        /// As miniaturas da peça, na ordem em que entram na planilha: vista de CIMA (Z+) e de
        /// BAIXO (Z−). Duas porque a alteração pode estar de qualquer lado — e é justamente o lado
        /// escondido que o desenho de bancada costuma esconder (Carlos, 2026-09-18).
        /// </summary>
        public List<byte[]> Thumbnails { get; } = new List<byte[]>();

        /// <summary>Marcadas nesta peça, na ordem da folha.</summary>
        public IEnumerable<ChangeTask> CheckedTasks =>
            (Tasks ?? new List<ChangeTask>()).Where(t => t.Checked);

        /// <summary>
        /// Peça nova: marca "FABRICAR, QUANTIDADE" com a quantidade (as posições na montagem). É o
        /// único preenchimento automático de caixa que o AutoEDM faz — vale porque peça que não
        /// existia só pode ser fabricada, e a quantidade está na montagem, não na cabeça de
        /// ninguém (Carlos, 2026-09-18). O que ficar gravado no .json depois manda sobre isto.
        /// </summary>
        public void MarkAsNewPart(int positions)
        {
            IsNew = true;
            Positions = positions > 0 ? positions : 1;

            ChangeTask fabricar = Task("FABRICAR");
            if (fabricar != null)
            {
                fabricar.Checked = true;
                fabricar.Detail = Positions.ToString(System.Globalization.CultureInfo.InvariantCulture);
            }
            // Quem fabrica precisa do material antes (Carlos, 2026-09-18).
            ChangeTask material = Task("VERIF. MATERIAL");
            if (material != null) material.Checked = true;
        }

        /// <summary>
        /// Peça ALTERADA: marca "RECUPERAR PEÇA NÃO CONFORME". A regra do Carlos (2026-09-18) é
        /// binária — o que não é fabricar é recuperar: a peça já existe e volta para a bancada.
        /// O que ficar gravado no .json depois manda sobre isto.
        /// </summary>
        public void MarkAsChangedPart()
        {
            IsNew = false;
            ChangeTask recuperar = Task("RECUPERAR");
            if (recuperar != null) recuperar.Checked = true;
        }

        private ChangeTask Task(string labelStart) => (Tasks ?? new List<ChangeTask>())
            .FirstOrDefault(t => t.Label != null &&
                                 t.Label.StartsWith(labelStart, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// O relatório inteiro: o cabeçalho do projeto (que o <see cref="ProjectFolder"/> preenche pelo
    /// caminho da montagem) e uma folha por peça alterada. Espelha a planilha
    /// "REGISTRO DE REVISÕES E AÇÕES EM PROJETOS DE MOLDES" (Carlos, 2026-09-17).
    /// </summary>
    public sealed class ChangeReport
    {
        public const string Title = "REGISTRO DE REVISÕES E AÇÕES EM PROJETOS DE MOLDES";

        /// <summary>Número da revisão (o N do grupo "Rev.N" mais recente da montagem).</summary>
        public int Revision { get; set; }

        public DateTime When { get; set; } = DateTime.Now;

        /// <summary>Nome do arquivo da montagem.</summary>
        public string AssemblyName { get; set; }

        /// <summary>Códigos do projeto — lidos da pasta, editáveis na janela.</summary>
        public string ProductCode { get; set; }     // PA
        public string PartNumbers { get; set; }     // PN
        public string MoldBaseCode { get; set; }    // PM (porta molde)
        public string MoldCode { get; set; }        // MD
        public string Rvpa { get; set; }
        public string ProjectDirectory { get; set; }

        /// <summary>Quem assina a folha (a linha de nomes do rodapé da planilha).</summary>
        public List<string> Responsibles { get; } = new List<string>();

        public List<PartChange> Parts { get; } = new List<PartChange>();

        /// <summary>As peças que realmente saem no relatório, na ordem da lista.</summary>
        public IEnumerable<PartChange> IncludedParts => Parts.Where(p => p.Include);
    }
}
