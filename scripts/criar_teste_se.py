import os
import win32com.client

SE = win32com.client.GetActiveObject("SolidEdge.Application")
SE.Visible = True
SE.DisplayAlerts = False

base = os.path.dirname(os.path.abspath(__file__))
cav_path = os.path.join(base, "teste_cav.par")
asm_path = os.path.join(base, "teste_molde.asm")

# Criar peça com box 20x20x20 mm
part = SE.Documents.Add("SolidEdge.PartDocument")
models = part.Models
plane = part.RefPlanes.Item(1)

# AddBoxByTwoPoints: pontos em metros (0,0,0) -> (0.020,0.020,0), profundidade 0.020
box = models.AddBoxByTwoPoints(
    0.0, 0.0, 0.0,
    0.020, 0.020, 0.0,
    0.0, 0.020, plane,
    1, False, None, 0
)

# Criar estilo vermelho (valores de cor em 0..1 no SE)
styles = part.FaceStyles
red_style = styles.Add("AutoEDM_Red", "")
red_style.SetDiffuse(1.0, 0.0, 0.0)

# Pintar algumas faces de vermelho
body = box.Body
faces = body.Faces(1)
for i in range(1, min(4, faces.Count) + 1):
    face = faces.Item(i)
    face.Style = red_style

part.SaveAs(cav_path)
print(f"Cavidade salva: {cav_path}")

# Criar montagem e adicionar a peça
asm = SE.Documents.Add("SolidEdge.AssemblyDocument")
occ = asm.Occurrences.AddByFilename(cav_path, False)
asm.SaveAs(asm_path)
print(f"Montagem salva: {asm_path}")
