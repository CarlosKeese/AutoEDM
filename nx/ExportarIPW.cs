// AutoEDM — Exportar IPW (journal do NX CAM 2212)
// 1. Põe o WCS no absoluto (Format > WCS > Set WCS to Absolute).
// 2. Pede o corpo de facetas (IPW) a exportar.
// 3. Grava <pasta da peça>\<nome da peça>_IPW.stl (File > Export > STL).
// Base: journal gravado no NX 2212 (WCS_ABSOLUTO-EXPORTAR_STL-JOURNAL.cs).
using System;
using System.IO;
using NXOpen;
using NXOpen.UF;

public class ExportarIPW
{
    const string Titulo = "AutoEDM — Exportar IPW";

    public static void Main(string[] args)
    {
        Session theSession = Session.GetSession();
        UI theUI = UI.GetUI();
        Part workPart = theSession.Parts.Work;

        if (workPart == null)
        {
            Erro(theUI, "Nenhuma peça aberta.");
            return;
        }

        string caminhoPeca = workPart.FullPath;
        if (string.IsNullOrEmpty(caminhoPeca) || !Path.IsPathRooted(caminhoPeca))
        {
            Erro(theUI, "Salve a peça antes de exportar: o STL vai para a pasta dela.");
            return;
        }

        // 1. WCS no absoluto (marca de desfazer antes, para o Ctrl+Z voltar o WCS)
        theSession.SetUndoMark(Session.MarkVisibility.Visible, "Exportar IPW");
        Matrix3x3 identidade = new Matrix3x3();
        identidade.Xx = 1.0; identidade.Xy = 0.0; identidade.Xz = 0.0;
        identidade.Yx = 0.0; identidade.Yy = 1.0; identidade.Yz = 0.0;
        identidade.Zx = 0.0; identidade.Zy = 0.0; identidade.Zz = 1.0;
        workPart.WCS.SetOriginAndMatrix(new Point3d(0.0, 0.0, 0.0), identidade);

        // 2. Seleção do corpo de facetas
        Selection.MaskTriple[] mascara =
        {
            new Selection.MaskTriple(UFConstants.UF_faceted_model_type, 0, 0)
        };
        TaggedObject selecionado;
        Point3d cursor;
        Selection.Response resp = theUI.SelectionManager.SelectTaggedObject(
            "Selecione o corpo de facetas (IPW) a exportar",
            Titulo,
            Selection.SelectionScope.WorkPartAndOccurrence,
            Selection.SelectionAction.ClearAndEnableSpecific,
            false, false, mascara, out selecionado, out cursor);

        if (resp != Selection.Response.ObjectSelected && resp != Selection.Response.ObjectSelectedByName)
            return; // cancelado

        NXOpen.Facet.FacetedBody corpo = selecionado as NXOpen.Facet.FacetedBody;
        if (corpo == null)
        {
            Erro(theUI, "O objeto selecionado não é um corpo de facetas.");
            return;
        }

        // 3. Nome do arquivo: mesma pasta da peça, <nome>_IPW.stl
        string arquivo = Path.Combine(
            Path.GetDirectoryName(caminhoPeca),
            Path.GetFileNameWithoutExtension(caminhoPeca) + "_IPW.stl");

        if (File.Exists(arquivo))
        {
            int r = theUI.NXMessageBox.Show(Titulo, NXMessageBox.DialogType.Question,
                "O arquivo já existe:\n" + arquivo + "\n\nSobrescrever?");
            if (r != 1) return; // 1 = Sim
            File.Delete(arquivo);
        }

        STLCreator stl = theSession.DexManager.CreateStlCreator();
        try
        {
            stl.AutoNormalGen = true;
            stl.ChordalTol = 0.01;
            stl.AdjacencyTol = 0.08;
            stl.ExportSelectionBlock.Add(corpo);
            stl.OutputFile = arquivo;
            stl.Commit();
        }
        catch (Exception ex)
        {
            Erro(theUI, "Falha ao exportar o STL:\n" + ex.Message);
            return;
        }
        finally
        {
            stl.Destroy();
        }

        theUI.NXMessageBox.Show(Titulo, NXMessageBox.DialogType.Information,
            "WCS no absoluto e IPW exportado:\n" + arquivo);
    }

    static void Erro(UI theUI, string msg)
    {
        theUI.NXMessageBox.Show(Titulo, NXMessageBox.DialogType.Error, msg);
    }

    public static int GetUnloadOption(string dummy)
    {
        return (int)Session.LibraryUnloadOption.Immediately;
    }
}
