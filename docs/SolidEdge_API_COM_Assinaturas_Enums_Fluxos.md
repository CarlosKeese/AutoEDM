# Siemens Solid Edge — API COM: Assinaturas, Enums e Fluxos Detalhados (2023-2026)

**TL;DR:** Este documento complementa o catálogo amplo com **assinaturas de métodos** (parâmetros, tipos, direções `[in]`/`[out]`/`[opt]`), **valores numéricos de enums** e **fluxos de trabalho passo a passo** para as operações mais críticas do projeto AutoEDM. Toda assinatura foi extraída da documentação oficial da Siemens [^74^][^99^][^66^][^97^][^39^][^89^] e deve ser validada contra o dump da typelib antes da implementação.

---

## 1. Enums — Valores Numéricos Completos

### 1.1 DocumentTypeConstants — Tipo de Documento

| Membro | Valor | Hex | Descrição |
|---|---|---|---|
| `igPartDocument` | **1** | 0x1 | Documento de peça (.par) |
| `igDraftDocument` | **2** | 0x2 | Documento de desenho (.dft) |
| `igAssemblyDocument` | **3** | 0x3 | Documento de montagem (.asm) |
| `igSheetMetalDocument` | **4** | 0x4 | Documento de chapa metálica (.psm) |
| `igUnknownDocument` | **5** | 0x5 | Tipo desconhecido |
| `igWeldmentDocument` | **6** | 0x6 | Documento de soldagem (.pwd) |
| `igWeldmentAssemblyDocument` | **7** | 0x7 | Montagem de soldagem |
| `igSyncPartDocument` | **8** | 0x8 | Peça síncrona (obsoleto ST3+) |
| `igSyncSheetMetalDocument` | **9** | 0x9 | Chapa metálica síncrona (obsoleto ST3+) |
| `igSyncAssemblyDocument` | **10** | 0xA | Montagem síncrona (obsoleto ST3+) |

Uso típico: `Application.GetDefaultTemplatePath(igPartDocument)` retorna o caminho do template padrão para peças [^39^]. A partir do ST3, os documentos "síncronos" foram integrados nos tipos tradicionais; a distinção agora é feita via `ModelingModeConstants` [^115^].

### 1.2 FeatureStatusConstants — Status de Feature

| Membro | Valor | Hex | Descrição |
|---|---|---|---|
| `igFeatureOK` | **1216476310** | 0x4877F5D6 | Feature válida |
| `igFeatureFailed` | **1216476311** | 0x4877F5D7 | Feature com erro |

Esses valores são **retornados pela propriedade `.Status`** de qualquer feature após sua criação. Sempre verifique `feature.Status == igFeatureOK` após chamar um método `Add` — o Solid Edge não lança exceções COM para features com erro, apenas retorna o status [^87^][^74^].

### 1.3 ModelingModeConstants — Modo de Modelagem

| Membro | Valor | Descrição |
|---|---|---|
| `seModelingModeSynchronous` | **1** | Modelagem síncrona (direct modeling) |
| `seModelingModeOrdered` | **2** | Modelagem ordenada (history-based) |

A propriedade `PartDocument.ModelingMode` (get/set) define o modo ativo. Cada feature possui a propriedade `ModelingModeType` (read-only) que indica em qual modo foi criada [^115^]. Chamar métodos inapropriados para o modo atual retorna `E_INVALID_MODELING_MODE`.

### 1.4 KeyPointType — Tipos de Ponto-Chave

| Membro | Valor | Hex | Descrição |
|---|---|---|---|
| `igKeyPointStart` | **1** | 0x1 | Ponto inicial |
| `igKeyPointEnd` | **2** | 0x2 | Ponto final |
| `igKeyPointCenter` | **4** | 0x4 | Centro |
| `igKeyPointMajorAxis` | **8** | 0x8 | Eixo maior |
| `igKeyPointMinorAxis` | **16** | 0x10 | Eixo menor |
| `igKeyPointMiddle` | **32** | 0x20 | Ponto médio |
| `igKeyPointPointOnly` | **64** | 0x40 | Apenas ponto |
| `igKeyPointHorizontalSilhouette` | **128** | 0x80 | Silhueta horizontal |
| `igKeyPointVerticalSilhouette` | **256** | 0x100 | Silhueta vertical |
| `igKeyPointInteriorNode` | **512** | 0x200 | Nó interior |
| `igKeyPointInteriorPole` | **1024** | 0x400 | Pólo interior |
| `igKeyPointNonDefining` | **16384** | 0x4000 | Não-definidor |
| `igKeyPointCallback` | **32768** | 0x8000 | Callback |

Usado em `Relations2d.AddKeypoint(Object1, Index1, Object2, Index2)` onde `Index1/Index2` são valores deste enum. `igLineEnd = 2` e `igLineStart = 1` são aliases comuns em código VB [^89^][^67^].

### 1.5 FeatureTopologyQueryTypeConstants — Filtro de Topologia

| Membro | Valor | Descrição |
|---|---|---|
| `igQueryAll` | **1** | Todas as faces/arestas |
| `igQueryRoundable` | **2** | Faces arredondáveis |
| `igQueryStraight` | **3** | Faces/arestas retas |
| `igQueryEllipse` | **4** | Faces/arestas elípticas |
| `igQuerySpline` | **5** | Faces/arestas spline |
| `igQueryPlane` | **6** | Faces planas |
| `igQueryCone` | **7** | Faces cônicas |
| `igQueryTorus` | **8** | Faces de toro |
| `igQuerySphere` | **9** | Faces esféricas |
| `igQueryCylinder` | **10** | Faces cilíndricas |

Usado como parâmetro ao acessar `Body.Faces(FaceType)` ou `Body.Edges(EdgeType)`. O valor mais comum é `igQueryAll = 1` para obter todas as faces de um corpo [^84^][^118^].

### 1.6 FeaturePropertyConstants — Constantes de Feature (seleção)

O enum `FeaturePropertyConstants` contém **centenas de valores** compartilhados entre múltiplos tipos de features. Os mais usados estão abaixo [^92^][^96^]:

| Membro | Descrição |
|---|---|
| `igLeft` | Projeto para esquerda / lado esquerdo |
| `igRight` | Projeto para direita / lado direito |
| `igSymmetric` | Extensão simétrica |
| `igThroughAll` | Através de tudo |
| `igInside` | Material interno |
| `igOutside` | Material externo |
| `igNone` | Nenhum |
| `igStart` | Ponto de início |
| `igAddRound` | Adicionar arredondamento |
| `igNoRound` | Sem arredondamento |
| `igCornerRound` | Arredondamento de canto |
| `igRegularHole` | Furo regular |
| `igCounterboreHole` | Furo escareado |
| `igCountersinkHole` | Furo com assentamento |
| `igPatternRectangular` | Padrão retangular |
| `igPatternCircular` | Padrão circular |
| `igPatternMirror` | Padrão espelhado |
| `igLinear` | Extensão linear |
| `igTangent` | Tangente |

### 1.7 GNTTypePropertyConstants — Tipos de Geometria

| Membro | Valor | Tipo de geometria |
|---|---|---|
| `igLine` | **3** | Linha reta |
| `igEllipse` | **4** | Elipse |
| `igPlane` | **6** | Plano |
| `igCone` | **7** | Cone |
| `igTorus` | **8** | Toro |
| `igSphere` | **9** | Esfera |
| `igCylinder` | **10** | Cilindro |

A propriedade `Face.Geometry.Type` ou `Edge.Geometry.Type` retorna um desses valores, permitindo identificar o tipo de superfície/curva subjacente [^26^].

### 1.8 SeObjectType — Tipos de Objeto COM (seleção)

| Membro | Valor | Descrição |
|---|---|---|
| `igApplication` | — | Aplicação |
| `igPartDocument` | — | PartDocument |
| `igAssemblyDocument` | — | AssemblyDocument |
| `igDraftDocument` | — | DraftDocument |
| `igSheetMetalDocument` | — | SheetMetalDocument |
| `igOccurrence` | — | Occurrence |
| `igOccurrences` | — | Occurrences |
| `igPlanarRelation3d` | — | PlanarRelation3d |
| `igAxialRelation3d` | — | AxialRelation3d |
| `igAngularRelation3d` | — | AngularRelation3d |
| `igGroundRelation3d` | — | GroundRelation3d |
| `igPointRelation3d` | — | PointRelation3d |
| `igFace` | — | Face |
| `igEdge` | — | Edge |
| `igVertex` | — | Vertex |
| `igBody` | — | Body |
| `igRefPlane` | — | RefPlane |
| `igProfile` | — | Profile |
| `igProfileSet` | — | ProfileSet |
| `igSketch` | — | Sketch |
| `igFeature` | — | Feature genérica |
| `igDimension` | — | Dimension |
| `igDrawingView` | — | DrawingView |
| `igSheet` | — | Sheet |
| `igSectionView` | — | SectionView |
| `igHole` | — | Hole |
| `igRound` | — | Round |
| `igChamfer` | — | Chamfer |
| `igPattern` | — | Pattern |
| `igMirrorCopy` | — | MirrorCopy |
| `igCoordinateSystem` | — | CoordinateSystem |
| `igFamilyMember` | — | FamilyMember |
| `igConnector` | — | Connector |
| `igGostWeldSymbol` | — | GostWeldSymbol |

Usado principalmente para verificação de tipo em eventos e para discriminação de objetos genéricos [^35^].

---

## 2. Assinaturas de Métodos — Framework

### 2.1 Application — Conexão e Controle

```vb
' VB6 assinatura (COM)
Public Property Get ActiveDocument() As SolidEdgeDocument
Public Property Get ActiveEnvironment() As String
Public Property Get Documents() As Documents
Public Property Get Environments() As Environments
Public Property Get Window() As Window
Public Property Get CommandBars() As CommandBars
Public Property Let Visible(ByVal RHS As Boolean)
Public Property Get Visible() As Boolean
Public Function GetDefaultTemplatePath(ByVal DocumentType As DocumentTypeConstants) As String
Public Function GetActiveEnvironment() As Environment
Public Sub Quit()
Public Property Let ScreenUpdating(ByVal RHS As Boolean)
Public Property Get ScreenUpdating() As Boolean
Public Property Let StatusBar(ByVal RHS As String)
Public Property Get StatusBar() As String
Public Property Get UserName() As String
Public Property Get ProcessID() As Long
```

| Método/Propriedade | Tipo Retorno | Parâmetros | Observação |
|---|---|---|---|
| `ActiveDocument` | `SolidEdgeDocument` | (propriedade) | Documento ativo ou `Nothing` |
| `ActiveEnvironment` | `String` | (propriedade) | Nome do ambiente: "Part", "Assembly", "Draft" |
| `Documents` | `Documents` | (propriedade) | Coleção de documentos abertos |
| `GetDefaultTemplatePath` | `String` | `[in] DocumentTypeConstants DocumentType` | Retorna caminho do template padrão |
| `GetActiveEnvironment` | `Environment` | (método) | Objeto Environment ativo |
| `Visible` | `Boolean` | `[in/out] Boolean` | Controla visibilidade da janela |
| `Quit` | `void` | (método) | Encerra Solid Edge |
| `ScreenUpdating` | `Boolean` | `[in/out] Boolean` | `False` para batch (performance) |

### 2.2 Documents — Gerenciamento de Documentos

```vb
Public Function Add(ByVal ProgID As String) As SolidEdgeDocument
Public Function Open(ByVal FileName As String) As SolidEdgeDocument
Public Function OpenWithTemplate(ByVal FileName As String, ByVal Template As String) As SolidEdgeDocument
Public Sub Close()
Public Property Get Count() As Long
Public Property Get Item(ByVal Index As Variant) As SolidEdgeDocument
```

| Método | Retorno | Parâmetros | Observação |
|---|---|---|---|
| `Add` | `SolidEdgeDocument` | `[in] String ProgID` | ProgID: "SolidEdge.PartDocument", "SolidEdge.AssemblyDocument", etc. |
| `Open` | `SolidEdgeDocument` | `[in] String FileName` | Abre arquivo existente |
| `OpenWithTemplate` | `SolidEdgeDocument` | `[in] String FileName, [in] String Template` | Abre arquivo não-nativo com template |
| `Close` | `void` | — | Fecha todos os documentos |
| `Count` | `Long` | (propriedade) | Número de documentos |
| `Item` | `SolidEdgeDocument` | `[in] Variant Index` | 1-based; aceita índice numérico ou nome |

### 2.3 SolidEdgeDocument — Propriedades Comuns

```vb
Public Property Get Name() As String
Public Property Get FullName() As String
Public Property Get Path() As String
Public Property Get Type() As DocumentTypeConstants
Public Property Get SelectSet() As SelectSet
Public Property Get PropertySets() As PropertySets
Public Property Get SummaryInfo() As SummaryInfo
Public Property Get AttributeSets() As AttributeSets
Public Property Get Windows() As Windows
Public Property Get Variables() As variable
Public Sub Save()
Public Sub SaveAs(ByVal NewFileName As String)
Public Sub Close(ByVal SaveChanges As Boolean)
```

| Propriedade | Tipo | Descrição |
|---|---|---|
| `Name` | `String` | Nome do arquivo (com extensão) |
| `FullName` | `String` | Caminho completo do arquivo |
| `Path` | `String` | Diretório do arquivo |
| `Type` | `DocumentTypeConstants` | Tipo: 1=Part, 2=Draft, 3=Asm, 4=SheetMetal |
| `SelectSet` | `SelectSet` | Conjunto de seleção ativo |
| `PropertySets` | `PropertySets` | Propriedades do documento |
| `AttributeSets` | `AttributeSets` | Atributos definidos pelo usuário |
| `Variables` | `variable` | Tabela de variáveis |

---

## 3. Assinaturas de Métodos — Part (Modelagem de Peça)

### 3.1 Models — Criação de Features de Protrusão

```vb
' AddFiniteExtrudedProtrusion — cria protrusão extrudada
Public Function AddFiniteExtrudedProtrusion( _
    ByVal NumberOfProfiles As Long, _
    ByRef ProfileArray() As Profile, _
    ByVal ProfilePlaneSide As FeaturePropertyConstants, _
    ByVal ExtrusionDistance As Double _
) As Model
```

| Parâmetro | Direção | Tipo | Descrição |
|---|---|---|---|
| `NumberOfProfiles` | `[in]` | `Long` | Número de perfis no array |
| `ProfileArray` | `[in]` | `Profile[]` (SAFEARRAY) | Array de objetos Profile (1-based em VB) |
| `ProfilePlaneSide` | `[in]` | `FeaturePropertyConstants` | `igLeft`, `igRight`, `igSymmetric` |
| `ExtrusionDistance` | `[in]` | `Double` | Distância de extrusão em **metros** |
| **Retorno** | `[out]` | `Model` | Objeto Model criado |

```vb
' AddFiniteRevolvedProtrusion — cria protrusão revolucionada
Public Function AddFiniteRevolvedProtrusion( _
    ByVal NumberOfProfiles As Long, _
    ByRef ProfileArray() As Profile, _
    ByVal ReferenceAxis As RefAxis, _
    ByVal ProfilePlaneSide As FeaturePropertyConstants, _
    ByVal AngleOfRevolution As Double _
) As Model
```

| Parâmetro | Direção | Tipo | Descrição |
|---|---|---|---|
| `NumberOfProfiles` | `[in]` | `Long` | Número de perfis |
| `ProfileArray` | `[in]` | `Profile[]` | Array de perfis |
| `ReferenceAxis` | `[in]` | `RefAxis` | Eixo de revolução (criado via `Profile.SetAxisOfRevolution`) |
| `ProfilePlaneSide` | `[in]` | `FeaturePropertyConstants` | `igLeft`, `igRight`, `igSymmetric` |
| `AngleOfRevolution` | `[in]` | `Double` | Ângulo em **radianos** |

```vb
' ExtrudedCutouts.AddFinite — cria recorte extrudado
Public Function AddFinite( _
    ByVal Profile As Profile, _
    ByVal ProfileSide As FeaturePropertyConstants, _
    ByVal ProfilePlaneSide As FeaturePropertyConstants, _
    ByVal Depth As Double _
) As ExtrudedCutout
```

| Parâmetro | Direção | Tipo | Descrição |
|---|---|---|---|
| `Profile` | `[in]` | `Profile` | Perfil do recorte |
| `ProfileSide` | `[in]` | `FeaturePropertyConstants` | Lado do perfil: `igLeft`/`igRight` |
| `ProfilePlaneSide` | `[in]` | `FeaturePropertyConstants` | Lado do plano: `igLeft`/`igRight` |
| `Depth` | `[in]` | `Double` | Profundidade em **metros** |

```vb
' ExtrudedCutouts.AddFiniteMulti — recorte com múltiplos perfis
Public Function AddFiniteMulti( _
    ByVal NumberOfProfiles As Long, _
    ByRef ProfileArray() As Profile, _
    ByVal ProfilePlaneSide As FeaturePropertyConstants, _
    ByVal Depth As Double _
) As ExtrudedCutout
```

### 3.2 Holes — Furos

```vb
' Holes.AddFinite — furo com profundidade finita
Public Function AddFinite( _
    ByVal Profile As Profile, _
    ByVal ProfilePlaneSide As FeaturePropertyConstants, _
    ByVal FiniteDepth As Double, _
    ByVal Data As HoleData _
) As Hole

' Holes.AddThroughAll — furo através de tudo
Public Function AddThroughAll( _
    ByVal Profile As Profile, _
    ByVal ProfilePlaneSide As FeaturePropertyConstants, _
    ByVal Data As HoleData _
) As Hole

' Holes.AddFromTo — furo de superfície a superfície
Public Function AddFromTo( _
    ByVal Profile As Profile, _
    ByVal ProfilePlaneSide As FeaturePropertyConstants, _
    ByVal FromSurface As Object, _
    ByVal ToSurface As Object, _
    ByVal Data As HoleData _
) As Hole
```

| Parâmetro | Direção | Tipo | Descrição |
|---|---|---|---|
| `Profile` | `[in]` | `Profile` | Perfil com `Holes2d.Add(x,y)` |
| `ProfilePlaneSide` | `[in]` | `FeaturePropertyConstants` | Lado do plano |
| `FiniteDepth` | `[in]` | `Double` | Profundidade finita (metros) |
| `FromSurface` | `[in]` | `Object` | Superfície de início (Face ou RefPlane) |
| `ToSurface` | `[in]` | `Object` | Superfície de fim (Face ou RefPlane) |
| `Data` | `[in]` | `HoleData` | Dados do furo (diâmetro, tipo, etc.) |

```vb
' HoleDataCollection.Add — cria dados de furo
Public Function Add( _
    ByVal HoleType As FeaturePropertyConstants, _
    ByVal HoleDiameter As Double, _
    [Optional] ByVal BottomAngle As Double, _
    [Optional] ByVal CounterboreDiameter As Double, _
    [Optional] ByVal CounterboreDepth As Double, _
    [Optional] ByVal CountersinkDiameter As Double, _
    [Optional] ByVal CountersinkAngle As Double _
) As HoleData
```

### 3.3 RefPlanes — Planos de Referência

```vb
' RefPlanes.AddParallelByDistance — plano paralelo a uma distância
Public Function AddParallelByDistance( _
    ByVal ParentPlane As Object, _
    ByVal Distance As Double, _
    ByVal NormalSide As FeaturePropertyConstants, _
    [Optional] ByVal Pivot As Object, _
    [Optional] ByVal pivotorigin As KeyPointType, _
    [Optional] ByVal Local As Boolean = True _
) As RefPlane
```

| Parâmetro | Direção | Tipo | Descrição |
|---|---|---|---|
| `ParentPlane` | `[in]` | `Object` | RefPlane ou Face plana de referência |
| `Distance` | `[in]` | `Double` | Distância em **metros** (sempre positiva) |
| `NormalSide` | `[in]` | `FeaturePropertyConstants` | `igLeft` ou `igRight` |
| `Pivot` | `[in, opt]` | `Object` | Aresta para pivotar o plano |
| `pivotorigin` | `[in, opt]` | `KeyPointType` | `igLineStart`(1) ou `igLineEnd`(2) |
| `Local` | `[in, opt]` | `Boolean` | `False` = plano global (nomeado) [^103^] |
| **Retorno** | `[out]` | `RefPlane` | Novo plano de referência |

```vb
' RefPlanes.AddAngularByAngle — plano por ângulo
Public Function AddAngularByAngle( _
    ByVal ParentPlane As Object, _
    ByVal Angle As Double, _
    ByVal NormalSide As FeaturePropertyConstants, _
    [Optional] ByVal Pivot As Object, _
    [Optional] ByVal pivotorigin As KeyPointType, _
    [Optional] ByVal Local As Boolean = True _
) As RefPlane

' RefPlanes.AddNormalToCurve — plano normal a uma curva
Public Function AddNormalToCurve( _
    ByVal Curve As Object, _
    ByVal KeyPoint As KeyPointType, _
    [Optional] ByVal Local As Boolean = True _
) As RefPlane

' RefPlanes.AddBy3Points — plano por três pontos
Public Function AddBy3Points( _
    ByVal Point1 As Object, _
    ByVal Point2 As Object, _
    ByVal Point3 As Object, _
    [Optional] ByVal Local As Boolean = True _
) As RefPlane
```

### 3.4 Profile — Criação e Validação de Perfil

```vb
' ProfileSets.Add — cria novo ProfileSet
Public Function Add() As ProfileSet

' Profiles.Add — cria novo Profile em um plano
Public Function Add( _
    ByVal pRefPlaneDisp As Object _
) As Profile
```

| Parâmetro | Direção | Tipo | Descrição |
|---|---|---|---|
| `pRefPlaneDisp` | `[in]` | `Object` | RefPlane ou Face plana onde desenhar o perfil |
| **Retorno** | `[out]` | `Profile` | Novo perfil pronto para desenho 2D |

```vb
' Profile.End — finaliza e valida o perfil
Public Function End( _
    ByVal ValidationCriteria As Long _
) As Long
```

| Parâmetro | Direção | Tipo | Descrição |
|---|---|---|---|
| `ValidationCriteria` | `[in]` | `Long` | Critério de validação: `igProfileClosed = 1` |
| **Retorno** | `[out]` | `Long` | **0** = sucesso; **≠0** = erro |

**Constantes de validação de perfil:**

| Constante | Valor | Descrição |
|---|---|---|
| `igProfileClosed` | **1** | Perfil deve estar fechado |
| `igProfileOpen` | — | Perfil pode estar aberto |
| `igProfileSelfIntersecting` | — | Permite auto-interseção |

```vb
' Profile.SetAxisOfRevolution — define eixo para revolução
Public Function SetAxisOfRevolution( _
    ByVal lineforaxis As Line2d _
) As RefAxis

' Profile.Visible — controla visibilidade do perfil
Public Property Let Visible(ByVal RHS As Boolean)
```

### 3.5 Geometry 2D em Profile (Lines2d, Circles2d, Arcs2d, Relations2d)

```vb
' Lines2d.AddBy2Points — adiciona linha por dois pontos
Public Function AddBy2Points( _
    ByVal x1 As Double, _
    ByVal y1 As Double, _
    ByVal x2 As Double, _
    ByVal y2 As Double _
) As Line2d

' Circles2d.AddByCenterRadius — adiciona círculo
Public Function AddByCenterRadius( _
    ByVal x As Double, _
    ByVal y As Double, _
    ByVal Radius As Double _
) As Circle2d

' Circles2d.AddBy3Points — círculo por três pontos
Public Function AddBy3Points( _
    ByVal x1 As Double, _
    ByVal y1 As Double, _
    ByVal x2 As Double, _
    ByVal y2 As Double, _
    ByVal x3 As Double, _
    ByVal y3 As Double _
) As Circle2d

' Holes2d.Add — adiciona ponto de furo
Public Function Add( _
    ByVal xCenter As Double, _
    ByVal yCenter As Double _
) As Hole2d

' Relations2d.AddKeypoint — relaciona pontos-chave
Public Sub AddKeypoint( _
    ByVal Object1 As Object, _
    ByVal Index1 As KeyPointType, _
    ByVal Object2 As Object, _
    ByVal Index2 As KeyPointType _
)

' Relations2d.AddHorizontal — relacionamento horizontal
Public Sub AddHorizontal(ByVal Object As Object)

' Relations2d.AddVertical — relacionamento vertical
Public Sub AddVertical(ByVal Object As Object)

' Relations2d.AddEqual — relacionamento de igualdade
Public Sub AddEqual(ByVal Object1 As Object, ByVal Object2 As Object)

' Relations2d.AddParallel — relacionamento paralelo
Public Sub AddParallel(ByVal Object1 As Object, ByVal Object2 As Object)

' Relations2d.AddPerpendicular — relacionamento perpendicular
Public Sub AddPerpendicular(ByVal Object1 As Object, ByVal Object2 As Object)

' Relations2d.AddCoincident — relacionamento coincidente
Public Sub AddCoincident(ByVal Object1 As Object, ByVal Object2 As Object)
```

---

## 4. Assinaturas de Métodos — Assembly (Montagem)

### 4.1 Occurrences — Ocorrências de Peças

```vb
' Occurrences.AddByFilename — insere peça existente (com ground relation)
Public Function AddByFilename( _
    ByVal FileName As String _
) As Occurrence

' Occurrences.AddByTemplate — cria nova peça em contexto (in-place)
Public Function AddByTemplate( _
    ByVal OccurrenceFileName As String, _
    [Optional] ByVal TemplateFileName As String _
) As Occurrence
```

| Método | Retorno | Parâmetros | Observação |
|---|---|---|---|
| `AddByFilename` | `Occurrence` | `[in] String FileName` | Insere arquivo existente; cria ground relation automaticamente [^8^] |
| `AddByTemplate` | `Occurrence` | `[in] String OccurrenceFileName, [in, opt] String TemplateFileName` | Cria arquivo novo a partir de template; **ESSENCIAL PARA ELETRODOS** [^62^] |

**Diferença crítica:** `AddByFilename` insere uma peça já existente e a fixa no espaço (grounded). `AddByTemplate` cria uma **nova peça em branco** a partir de um template, permitindo modelagem in-place dentro do contexto da montagem [^SKILL^].

```vb
' Occurrence.GetTransform — obtém posição e orientação
Public Sub GetTransform( _
    ByRef OriginX As Double, _
    ByRef OriginY As Double, _
    ByRef OriginZ As Double, _
    ByRef AngleX As Double, _
    ByRef AngleY As Double, _
    ByRef AngleZ As Double _
)

' Occurrence.PutTransform — define matriz de transformação
Public Sub PutTransform( _
    ByRef Matrix As Double _
)

' Occurrence.SetOrigin — define origem (grounded only)
Public Sub SetOrigin( _
    ByVal x As Double, _
    ByVal y As Double, _
    ByVal z As Double _
)

' Occurrence.Move — move ocorrência (grounded only)
Public Sub Move( _
    ByVal dx As Double, _
    ByVal dy As Double, _
    ByVal dz As Double _
)

' Occurrence.Rotate — rotaciona ocorrência (grounded only)
Public Sub Rotate( _
    ByVal x1 As Double, ByVal y1 As Double, ByVal z1 As Double, _
    ByVal x2 As Double, ByVal y2 As Double, ByVal z2 As Double, _
    ByVal angle As Double _
)

' Occurrence.Activate — ativa/desativa ocorrência para edição
Public Property Let Activate(ByVal RHS As Boolean)
Public Property Get Activate() As Boolean
```

**Unidades:** Todas as coordenadas estão em **metros** e ângulos em **radianos** [^SKILL^].

### 4.2 Relations3d — Relacionamentos de Montagem

```vb
' AssemblyDocument.CreateReference — cria referência a geometria de ocorrência
Public Function CreateReference( _
    ByVal Occurrence As Occurrence, _
    ByVal Entity As Object _
) As Reference
```

| Parâmetro | Direção | Tipo | Descrição |
|---|---|---|---|
| `Occurrence` | `[in]` | `Occurrence` | Ocorrência que contém a geometria |
| `Entity` | `[in]` | `Object` | Face, Edge ou RefPlane da ocorrência |
| **Retorno** | `[out]` | `Reference` | Objeto Reference para usar em relacionamentos |

```vb
' Relations3d.AddPlanar — relacionamento planar (Mate ou Align)
Public Function AddPlanar( _
    ByVal Plane1 As Reference, _
    ByVal Plane2 As Reference, _
    ByVal NormalsAligned As Boolean, _
    ByRef ConstrainingPoint1 As Double, _
    ByRef ConstrainingPoint2 As Double _
) As PlanarRelation3d
```

| Parâmetro | Direção | Tipo | Descrição |
|---|---|---|---|
| `Plane1` | `[in]` | `Reference` | Referência à face/plano da primeira ocorrência |
| `Plane2` | `[in]` | `Reference` | Referência à face/plano da segunda ocorrência |
| `NormalsAligned` | `[in]` | `Boolean` | `True` = Align (normais paralelas); `False` = Mate (normais opostas) [^10^] |
| `ConstrainingPoint1` | `[in]` | `Double[3]` (SAFEARRAY) | Ponto de restrição no plano 1 (X, Y, Z) |
| `ConstrainingPoint2` | `[in]` | `Double[3]` (SAFEARRAY) | Ponto de restrição no plano 2 (X, Y, Z) |
| **Retorno** | `[out]` | `PlanarRelation3d` | Relacionamento criado |

```vb
' Relations3d.AddAxial — relacionamento axial
Public Function AddAxial( _
    ByVal Cylinder1 As Reference, _
    ByVal Cylinder2 As Reference, _
    ByVal NormalsAligned As Boolean _
) As AxialRelation3d

' Relations3d.AddAngular — relacionamento angular
Public Function AddAngular( _
    ByVal Plane1 As Reference, _
    ByVal Plane2 As Reference, _
    ByVal Angle As Double _
) As AngularRelation3d

' Relations3d.AddGround — fixa ocorrência no espaço
Public Function AddGround( _
    ByVal Occurrence As Occurrence _
) As GroundRelation3d

' Relations3d.AddPoint — relacionamento por ponto
Public Function AddPoint( _
    ByVal PointGeometry As Reference, _
    ByVal PointKeyPoint As KeyPointType, _
    ByVal ConnectGeometry As Reference, _
    ByVal ConnectKeyPoint As KeyPointType _
) As PointRelation3d
```

| Método | Parâmetros Chave | Descrição |
|---|---|---|
| `AddAxial` | `Cylinder1, Cylinder2, NormalsAligned` | Alinha faces cilíndricas/cônicas |
| `AddAngular` | `Plane1, Plane2, Angle` | Define ângulo entre planos (radianos) |
| `AddGround` | `Occurrence` | Fixa ocorrência no espaço |
| `AddPoint` | `PointGeometry, PointKeyPoint, ConnectGeometry, ConnectKeyPoint` | Conecta pontos-chave [^99^] |

---

## 5. Assinaturas de Métodos — Geometry (Topologia)

### 5.1 Body — Corpo Sólido

```vb
' Body.Faces — acessa faces do corpo
Public Property Get Faces( _
    [Optional] ByVal FaceType As FeatureTopologyQueryTypeConstants = igQueryAll _
) As Faces

' Body.Edges — acessa arestas do corpo
Public Property Get Edges( _
    [Optional] ByVal EdgeType As FeatureTopologyQueryTypeConstants = igQueryAll _
) As Edges

' Body.Vertices — acessa vértices
Public Property Get Vertices() As Vertices

' Body.Shells — acessa shells
Public Property Get Shells() As Shells
```

### 5.2 Face — Faces do Modelo

```vb
' Face.GetRange — caixa delimitadora da face
Public Sub GetRange( _
    ByRef MinRangePoint As Double, _
    ByRef MaxRangePoint As Double _
)
```

| Parâmetro | Direção | Tipo | Descrição |
|---|---|---|---|
| `MinRangePoint` | `[out]` | `Double[]` (SAFEARRAY) | Ponto mínimo (X, Y, Z) da bounding box |
| `MaxRangePoint` | `[out]` | `Double[]` (SAFEARRAY) | Ponto máximo (X, Y, Z) da bounding box |

**⚠️ CRÍTICO para late binding:** Os parâmetros `[out]` são `SAFEARRAY(double)`. Em C# com `dynamic`, use `new double[0]` + `ParameterModifier(2)` by-ref + `CultureInfo.InvariantCulture` para evitar `DISP_E_TYPEMISMATCH` [^SKILL^][^66^].

```vb
' Face.GetParamRange — extensão paramétrica UV
Public Sub GetParamRange( _
    ByRef UMin As Double, _
    ByRef UMax As Double, _
    ByRef VMin As Double, _
    ByRef VMax As Double _
)

' Face.GetPointAtParam — ponto 3D dado parâmetros UV
Public Sub GetPointAtParam( _
    ByVal u As Double, _
    ByVal v As Double, _
    ByRef Point As Double _
)

' Face.GetNormalAtPoint — normal no ponto
Public Sub GetNormalAtPoint( _
    ByVal x As Double, _
    ByVal y As Double, _
    ByVal z As Double, _
    ByRef Normal As Double _
)

' Face.GetArea — área da face
Public Function GetArea() As Double

' Face.Style — estilo visual (cor)
Public Property Get Style() As FaceStyle
```

| Propriedade | Tipo | Descrição |
|---|---|---|
| `Style.DiffuseRed` | `Double` (0..1) | Componente vermelho da cor difusa |
| `Style.DiffuseGreen` | `Double` (0..1) | Componente verde da cor difusa |
| `Style.DiffuseBlue` | `Double` (0..1) | Componente azul da cor difusa |
| `Style.AmbientRed/Green/Blue` | `Double` (0..1) | Componente ambiente |
| `Style.SpecularRed/Green/Blue` | `Double` (0..1) | Componente especular |

Multiplicar por **255** para obter valores RGB de 0-255. Usado no AutoEDM para identificar Ra (rugosidade) das faces de queima [^SKILL^].

### 5.3 Edge — Arestas do Modelo

```vb
' Edge.GetEndPoints — pontos inicial e final
Public Sub GetEndPoints( _
    ByRef StartPoint As Double, _
    ByRef EndPoint As Double _
)

' Edge.GetRange — caixa delimitadora
Public Sub GetRange( _
    ByRef MinRangePoint As Double, _
    ByRef MaxRangePoint As Double _
)

' Edge.GetParamExtents — extensão paramétrica
Public Sub GetParamExtents( _
    ByRef UMin As Double, _
    ByRef UMax As Double _
)

' Edge.GetPointAtParam — ponto dado parâmetro
Public Sub GetPointAtParam( _
    ByVal Param As Double, _
    ByRef Point As Double _
)

' Edge.GetTangent — vetor tangente
Public Sub GetTangent( _
    ByVal Param As Double, _
    ByRef Tangent As Double _
)

' Edge.GetLength — comprimento da aresta
Public Function GetLength() As Double

' Edge.GetFaces — faces adjacentes
Public Function GetFaces() As Faces
```

---

## 6. Assinaturas — CopySurfaces e Inter-Part Copy

### 6.1 CopySurfaces.Add

```vb
' CopySurfaces.Add — cópia de superfícies entre peças
Public Function Add( _
    ByVal NumberOfFaces As Long, _
    ByRef FaceArray() As Face, _
    [Optional] ByVal InternalBoundary As Object, _
    [Optional] ByVal ExternalBoundary As Object _
) As CopySurface
```

| Parâmetro | Direção | Tipo | Descrição |
|---|---|---|---|
| `NumberOfFaces` | `[in]` | `Long` | Número de faces no array |
| `FaceArray` | `[in]` | `Face[]` (SAFEARRAY de IDispatch) | Array tipado de objetos Face |
| `InternalBoundary` | `[in, opt]` | `Object` | Borda interna (Edge ou Loop) |
| `ExternalBoundary` | `[in, opt]` | `Object` | Borda externa (Edge ou Loop) |
| **Retorno** | `[out]` | `CopySurface` | Feature de cópia criada |

**⚠️ RESTRIÇÃO CRÍTICA:** O parâmetro `FaceArray` deve ser um array tipado **`Face[]`** que se marshala como `SAFEARRAY(IDispatch)`. Um array `object[]` vira `SAFEARRAY(VARIANT)` e é rejeitado com `DISP_E_TYPEMISMATCH` [^SKILL^].

### 6.2 InterpartConstructions

```vb
' InterpartConstructions.Add — adiciona construção inter-parte
Public Function Add( _
    ByVal ??? As ??? _
) As InterpartConstruction

' InterpartConstructions.Add2 — versão estendida (ST2 MP1+)
Public Function Add2( _
    ByVal ??? As ??? _
) As InterpartConstruction
```

> **Nota:** As assinaturas completas de `InterpartConstructions.Add` e `Add2` não foram encontradas na documentação pública. Esses métodos foram implementados no ST2 MP1 (ver PR 2129884) [^116^]. **Extrair do dump da typelib do SE 2023.**

---

## 7. Fluxos de Trabalho Detalhados

### 7.1 Fluxo 1: Criar uma Feature Extrudada (Protrusion)

Este é o padrão fundamental de modelagem no Solid Edge. Todo fluxo de criação de feature baseada em perfil segue este padrão [^74^][^67^][^8^].

**Passo 1 — Obter aplicação e documento:**
```
Application = Marshal.GetActiveObject("SolidEdge.Application")
PartDocument = Application.ActiveDocument
' ou: PartDocument = Application.Documents.Add("SolidEdge.PartDocument")
```

**Passo 2 — Criar ProfileSet e Profile:**
```
ProfileSet = PartDocument.ProfileSets.Add()
Profile = ProfileSet.Profiles.Add(pRefPlaneDisp:=PartDocument.RefPlanes(1))
' RefPlanes(1) = xy, RefPlanes(2) = yz, RefPlanes(3) = xz
```

**Passo 3 — Desenhar geometria 2D no perfil:**
```
Lines2d = Profile.Lines2d
Line1 = Lines2d.AddBy2Points(x1:=0, y1:=0, x2:=0.04, y2:=0)
Line2 = Lines2d.AddBy2Points(x1:=0.04, y1:=0, x2:=0.04, y2:=0.04)
Line3 = Lines2d.AddBy2Points(x1:=0.04, y1:=0.04, x2:=0, y2:=0.04)
Line4 = Lines2d.AddBy2Points(x1:=0, y1:=0.04, x2:=0, y2:=0)
```

**Passo 4 — Adicionar relações 2D para fechar o perfil:**
```
Relations2d = Profile.Relations2d
Relations2d.AddKeypoint(Object1:=Line1, Index1:=igKeyPointEnd,    Object2:=Line2, Index2:=igKeyPointStart)
Relations2d.AddKeypoint(Object1:=Line2, Index1:=igKeyPointEnd,    Object2:=Line3, Index2:=igKeyPointStart)
Relations2d.AddKeypoint(Object1:=Line3, Index1:=igKeyPointEnd,    Object2:=Line4, Index2:=igKeyPointStart)
Relations2d.AddKeypoint(Object1:=Line4, Index1:=igKeyPointEnd,    Object2:=Line1, Index2:=igKeyPointStart)
' igKeyPointEnd = 2, igKeyPointStart = 1
```

**Passo 5 — Validar e finalizar o perfil:**
```
Status = Profile.End(ValidationCriteria:=igProfileClosed)
' Retorno: 0 = sucesso, ≠0 = erro
If Status <> 0 Then
    ' Tratar erro de perfil não fechado
End If
```

**Passo 6 — Criar array de perfis e gerar a feature:**
```
ProfileArray(1) = Profile
Model = PartDocument.Models.AddFiniteExtrudedProtrusion( _
    NumberOfProfiles:=1, _
    ProfileArray:=ProfileArray, _
    ProfilePlaneSide:=igRight, _
    ExtrusionDistance:=0.04)
' Unidades: metros (0.04 = 40 mm)
```

**Passo 7 — Verificar status e ocultar perfil:**
```
If Model.ExtrudedProtrusions(1).Status <> igFeatureOK Then
    ' Tratar erro de feature
End If
Profile.Visible = False
```

### 7.2 Fluxo 2: Criar Eletrodo em Contexto de Montagem (AutoEDM)

Este é o fluxo principal do projeto AutoEDM para criação de eletrodos [^SKILL^][^62^][^10^].

**Passo 1 — Abrir montagem e obter referências:**
```
Application = Marshal.GetActiveObject("SolidEdge.Application")
AssemblyDoc = Application.ActiveDocument  ' .asm aberto
' Verificar: AssemblyDoc.Type == igAssemblyDocument (3)
```

**Passo 2 — Obter template padrão para peça:**
```
TemplatePath = Application.GetDefaultTemplatePath(igPartDocument)
' igPartDocument = 1
' Retorna algo como: "C:\Program Files\Siemens\Solid Edge 2023\Template\NormENG.par"
```

**Passo 3 — Criar ocorrência em contexto (in-place):**
```
Occurrence = AssemblyDoc.Occurrences.AddByTemplate( _
    OccurrenceFileName:="Electrode_001.par", _
    TemplateFileName:=TemplatePath)
' Cria peça nova em branco dentro da montagem
```

**Passo 4 — Ativar ocorrência para edição:**
```
Occurrence.Activate = True
' ⚠️ Ativar ≠ garantir edição in-place
' Sinais reais: AssemblyDoc.ModelingInAssembly / InPlaceActivated
```

**Passo 5 — Obter faces de queima da cavidade (peça fonte):**
```
' Acessar ocorrência da cavidade (já existente na montagem)
CavityOccurrence = AssemblyDoc.Occurrences.Item(1)
CavityDoc = CavityOccurrence.OccurrenceDocument
CavityModel = CavityDoc.Models(1)
CavityBody = CavityModel.Body

' Query todas as faces
Faces = CavityBody.Faces(FaceType:=igQueryAll)  ' igQueryAll = 1

' Filtrar faces de queima por cor (Ra)
BurnFaces = []
For i = 1 To Faces.Count
    Face = Faces.Item(i)
    Style = Face.Style
    R = Style.DiffuseRed * 255
    G = Style.DiffuseGreen * 255
    B = Style.DiffuseBlue * 255
    ' Classificar por cor: vermelho = queima, etc.
    If IsBurnColor(R, G, B) Then
        BurnFaces.Add(Face)
    End If
Next
```

**Passo 6 — Copiar faces para o eletrodo (Inter-Part Copy):**
```
' Acessar documento do eletrodo
ElectrodeDoc = Occurrence.OccurrenceDocument
ElectrodeConstructions = ElectrodeDoc.Constructions
CopySurfaces = ElectrodeConstructions.CopySurfaces

' Criar array tipado Face[] (NÃO object[])
FaceArray = new Face[BurnFaces.Count]
For i = 0 To BurnFaces.Count - 1
    FaceArray[i] = BurnFaces[i]
Next

' Copiar superfícies
CopySurface = CopySurfaces.Add( _
    NumberOfFaces:=BurnFaces.Count, _
    FaceArray:=FaceArray)
```

**⚠️ Se E_FAIL em CopySurfaces.Add:**
- Confirmar que peça foi criada via `AddByTemplate` (não `AddByFilename`)
- Confirmar `Occurrence.Activate = true`
- Verificar `AssemblyDoc.ModelingInAssembly` / `InPlaceActivated`
- **Próxima hipótese:** usar `InterpartConstructions.Add` / `CreateTopologyReference`

### 7.3 Fluxo 3: Criar Relacionamentos 3D em Montagem

Este fluxo posiciona peças relativamente umas às outras na montagem [^99^][^10^].

**Passo 1 — Inserir ocorrências:**
```
Occ1 = AssemblyDoc.Occurrences.AddByFilename("part1.par")
Occ2 = AssemblyDoc.Occurrences.AddByFilename("part2.par")
' Ambas ficam com GroundRelation3d automaticamente
```

**Passo 2 — Remover ground relation da segunda ocorrência:**
```
Occ2.Relations3d(1).Delete()
' A primeira relation é sempre o ground
```

**Passo 3 — Obter geometria e criar referências:**
```
' Face planar da primeira ocorrência
Face1 = Occ1.PartDocument.Models(1).ExtrudedProtrusions(1).TopCap
Ref1 = AssemblyDoc.CreateReference(Occ1, Face1)

' Face planar da segunda ocorrência
Face2 = Occ2.PartDocument.Models(1).ExtrudedProtrusions(1).BottomCap
Ref2 = AssemblyDoc.CreateReference(Occ2, Face2)
```

**Passo 4 — Obter pontos de restrição (centro das faces):**
```
Face1.GetParamRange(UMin, UMax, VMin, VMax)
u = (UMin + UMax) / 2
v = (VMin + VMax) / 2
Face1.GetPointAtParam(u, v, XYZPoints1)

Face2.GetParamRange(UMin, UMax, VMin, VMax)
u = (UMin + UMax) / 2
v = (VMin + VMax) / 2
Face2.GetPointAtParam(u, v, XYZPoints2)
```

**Passo 5 — Criar relacionamento planar (Mate):**
```
PlanarRelation = AssemblyDoc.Relations3d.AddPlanar( _
    Plane1:=Ref1, _
    Plane2:=Ref2, _
    NormalsAligned:=False, _   ' False = Mate (normais opostas)
    ConstrainingPoint1:=XYZPoints1, _
    ConstrainingPoint2:=XYZPoints2)
```

**Passo 6 — Adicionar relacionamento axial (opcional, para inserção):**
```
CylFace1 = ...  ' Face cilíndrica da ocorrência 1
CylFace2 = ...  ' Face cilíndrica da ocorrência 2
CylRef1 = AssemblyDoc.CreateReference(Occ1, CylFace1)
CylRef2 = AssemblyDoc.CreateReference(Occ2, CylFace2)

AxialRelation = AssemblyDoc.Relations3d.AddAxial( _
    Cylinder1:=CylRef1, _
    Cylinder2:=CylRef2, _
    NormalsAligned:=True)
```

**Passo 7 — Travar rotação axial (se necessário):**
```
AxialRelation.FixedRotate = True
```

### 7.4 Fluxo 4: Query de Faces por Tipo e Bounding Box

Usado para análise geométrica e identificação de regiões [^66^][^84^].

**Passo 1 — Obter corpo e faces:**
```
Model = PartDocument.Models(1)
Body = Model.Body
Faces = Body.Faces(FaceType:=igQueryAll)  ' Todas as faces
```

**Passo 2 — Iterar faces e obter bounding box:**
```
For i = 1 To Faces.Count
    Face = Faces.Item(i)
    
    ' Bounding box (em late binding)
    Dim MinPt(0) As Double  ' placeholder [out]
    Dim MaxPt(0) As Double  ' placeholder [out]
    Face.GetRange(MinRangePoint:=MinPt, MaxRangePoint:=MaxPt)
    
    ' MinPt(0..2) = Xmin, Ymin, Zmin
    ' MaxPt(0..2) = Xmax, Ymax, Zmax
    
    ' Dimensões da face
    Width = MaxPt(0) - MinPt(0)
    Height = MaxPt(1) - MinPt(1)
    Depth = MaxPt(2) - MinPt(2)
    
    ' Tipo de geometria
    Geometry = Face.Geometry
    GeoType = Geometry.Type
    ' GeoType: 6=Plane, 10=Cylinder, 7=Cone, 9=Sphere, 8=Torus
Next
```

**Passo 3 — Query por tipo específico:**
```
' Apenas faces planas
PlanarFaces = Body.Faces(FaceType:=igQueryPlane)  ' igQueryPlane = 6

' Apenas faces cilíndricas
CylindricalFaces = Body.Faces(FaceType:=igQueryCylinder)  ' igQueryCylinder = 10
```

---

## 8. Tabela de Conversão: COM → C# Late Binding

Quando se usa `dynamic` (late binding) no projeto AutoEDM, os tipos COM precisam de tratamento especial [^SKILL^]:

| Tipo COM | Tipo C# Early Binding | Tipo C# Late Binding (`dynamic`) | Observação |
|---|---|---|---|
| `BSTR` | `string` | `string` | Direto |
| `LONG` | `int` / `long` | `int` / `long` | Direto |
| `DOUBLE` | `double` | `double` | Direto |
| `VARIANT_BOOL` | `bool` | `bool` | Direto |
| `IDispatch*` | Interface tipada | `dynamic` | Via `GetActiveObject` |
| `SAFEARRAY(IDispatch)` | `Object[]` / tipado[] | **Array tipado** (`Face[]`, `Edge[]`) | `object[]` → `DISP_E_TYPEMISMATCH` |
| `SAFEARRAY(VARIANT)` | `object[]` | `object[]` | Aceito em alguns métodos |
| `SAFEARRAY(double)` `[out]` | `double[]` | `new double[0]` + `ParameterModifier` | Placeholder para `[out]` |
| `SAFEARRAY(double)` `[in]` | `double[]` | `double[]` | Direto |
| `[optional]` | `Type.Missing` | `Type.Missing` | Mesmo em late binding |
| `HRESULT` | Exceção COM | Exceção COM | `try/catch` para `COMException` |

---

## 9. HRESULTs e Diagnóstico

| HRESULT | Valor Hex | Nome | Causa Provável | Próxima Ação |
|---|---|---|---|---|
| `S_OK` | `0x00000000` | Sucesso | — | — |
| `E_FAIL` | `0x80004005` | Falha não especificada | Contexto inválido, objeto não inicializado | Verificar sequência de operações |
| `E_POINTER` | `0x80004003` | Ponteiro nulo | Objeto `null`/`Nothing` | Verificar inicialização |
| `E_INVALIDARG` | `0x80070057` | Argumento inválido | Parâmetro fora de faixa ou tipo errado | Verificar valores passados |
| `DISP_E_TYPEMISMATCH` | `0x80020005` | Tipo incompatível | VARIANT errado | Usar array tipado; verificar `[out]` |
| `DISP_E_MEMBERNOTFOUND` | `0x80020003` | Membro não encontrado | Nome de método/propriedade incorreto | Verificar no dump da typelib |
| `DISP_E_BADPARAMCOUNT` | `0x8002000E` | Número errado de parâmetros | Método chamado com argumentos errados | Verificar assinatura |
| `RPC_E_CALL_REJECTED` | `0x80010001` | Chamada rejeitada | Diálogo modal / servidor ocupado | Implementar `OleMessageFilter` |
| `RPC_E_SERVER_DIED` | `0x80010007` | Servidor morreu | Solid Edge crashou | Reiniciar aplicação |
| `REGDB_E_CLASSNOTREG` | `0x80040154` | Classe não registrada | SE não instalado / DLL não registrada | Verificar instalação |
| `MK_E_UNAVAILABLE` | `0x800401E3` | Objeto não disponível | Arquivo não encontrado | Verificar caminho |
| `CO_E_NOT_SUPPORTED` | `0x80004021` | Não suportado | Operação inapropriada para modo de modelagem | Verificar `ModelingModeType` |
| `E_INVALID_MODELING_MODE` | — | Modo de modelagem inválido | API não suportada no modo síncrono/ordenado | Mudar `ModelingMode` |

---

*Documento gerado para o projeto AutoEDM. Toda assinatura deve ser validada contra o dump da typelib (`SE_API_dump_*.txt`) antes da implementação. Se um método não está no dump, ele não existe com aquele nome — sinalize para obter um novo dump completo. Os valores hexadecimais de `FeatureStatusConstants` (`0x4877F5D6`, `0x4877F5D7`) são literais de erro específicos do Solid Edge e devem ser comparados como inteiros de 32 bits sem sinal.*
