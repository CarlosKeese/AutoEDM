# Type Library `SolidEdgeGeometry` (reflexão do Interop.SolidEdge v219.0.0.0 — SE 2023)

> **Origem:** reflexão de `Interop.SolidEdge.dll` (não do dump da typelib — a lib de geometria não é capturada pelo walk). Tipos são a **projeção .NET**; `[out]`/`[ref]` marcam parâmetros de saída — em **late binding** eles precisam de `ParameterModifier` by-ref ou voltam vazios (ver [GUIA_SOLID_EDGE_COM.md](../GUIA_SOLID_EDGE_COM.md)). Geometria/modelagem usam **metros/radianos**; coleções são **1-based**.

- **Objetos/interfaces:** 33  ·  **Enums:** 4

## Enums / constantes

### `FeatureTopologyQueryTypeConstants`

| Constante | Valor |
|-----------|-------|
| `igQueryAll` | 1 |
| `igQueryRoundable` | 2 |
| `igQueryStraight` | 3 |
| `igQueryEllipse` | 4 |
| `igQuerySpline` | 5 |
| `igQueryPlane` | 6 |
| `igQueryCone` | 7 |
| `igQueryTorus` | 8 |
| `igQuerySphere` | 9 |
| `igQueryCylinder` | 10 |

### `GNTTypePropertyConstants`

| Constante | Valor |
|-----------|-------|
| `igFaces` | 167551073 |
| `igFace` | 167551075 |
| `igShells` | 167551078 |
| `igLoops` | 167551080 |
| `igEdges` | 167551084 |
| `igVertices` | 167551086 |
| `igShell` | 167551088 |
| `igBody` | 167551091 |
| `igEdge` | 167551093 |
| `igEdgeUses` | 167551095 |
| `igLoop` | 167551097 |
| `igEdgeUse` | 167551099 |
| `igVertex` | 167551101 |
| `igBSplineCurve` | 167551103 |
| `igCircle` | 167551105 |
| `igEllipse` | 167551107 |
| `igLine` | 167551109 |
| `igPLine` | 434530178 |
| `igBSplineSurface` | 1465959633 |
| `igMesh` | -2071771273 |
| `igPlane` | -1909484335 |
| `igParamBSplineCurve` | -1811952078 |
| `igCurveBody` | -1020639371 |
| `igCurvePath` | -1020639369 |
| `igCurve` | -1020639367 |
| `igCurveVertex` | -1020639365 |
| `igCone` | -114972031 |
| `igCylinder` | -114972029 |
| `igSphere` | -114972027 |
| `igTorus` | -114972025 |

### `SmartCollectionTypeConstants`

| Constante | Valor |
|-----------|-------|
| `seRoundableEdgesAtVertex` | 1 |
| `seRoundableSmoothEdgeChain` | 2 |
| `seRoundableEdgesOfFace` | 3 |
| `seRoundableEdgesOfLoop` | 4 |
| `seRoundableEdgesOfFeature` | 5 |
| `seRoundableConvexEdgesOfBody` | 6 |
| `seRoundableConcaveEdgesOfBody` | 7 |
| `seThinwallableFacesOfFeature` | 8 |
| `seThinwallableSmoothFaceChain` | 9 |
| `seDraftableSmoothFaceChain` | 10 |
| `seDraftableFacesOfALoop` | 11 |
| `seDraftableFacesOfBodyNormalToFaceOrPlane` | 12 |
| `seBendFacesOfBody` | 13 |
| `seBendFacesOfBodyWithBendCenterLineAttributes` | 14 |

### `TopologyCollectionTypeConstants`

| Constante | Valor |
|-----------|-------|
| `seFaceCollection` | 1 |
| `seEdgeCollection` | 2 |
| `seVertexCollection` | 3 |

## Objetos / interfaces

| Tipo | Propriedades | Métodos |
|------|-------------|---------|
| [Body](#body) | 21 | 14 |
| [BSplineCurve](#bsplinecurve) | 5 | 7 |
| [BSplineSurface](#bsplinesurface) | 5 | 7 |
| [Circle](#circle) | 4 | 3 |
| [Cone](#cone) | 6 | 3 |
| [Curve](#curve) | 11 | 14 |
| [CurveBody](#curvebody) | 11 | 3 |
| [CurvePath](#curvepath) | 10 | 2 |
| [CurvePaths](#curvepaths) | 3 | 2 |
| [Curves](#curves) | 3 | 2 |
| [CurveVertex](#curvevertex) | 7 | 2 |
| [CurveVertices](#curvevertices) | 3 | 2 |
| [Cylinder](#cylinder) | 4 | 3 |
| [Edge](#edge) | 17 | 17 |
| [Edges](#edges) | 5 | 5 |
| [EdgeUse](#edgeuse) | 12 | 9 |
| [EdgeUses](#edgeuses) | 4 | 2 |
| [Ellipse](#ellipse) | 4 | 4 |
| [Face](#face) | 19 | 17 |
| [Faces](#faces) | 5 | 5 |
| [Line](#line) | 3 | 3 |
| [Loop](#loop) | 12 | 2 |
| [Loops](#loops) | 4 | 2 |
| [MeshSurface](#meshsurface) | 3 | 3 |
| [ParamBSplineCurve](#parambsplinecurve) | 4 | 7 |
| [Plane](#plane) | 3 | 4 |
| [PLine](#pline) | 3 | 4 |
| [Shell](#shell) | 13 | 3 |
| [Shells](#shells) | 4 | 2 |
| [Sphere](#sphere) | 4 | 2 |
| [Torus](#torus) | 5 | 3 |
| [Vertex](#vertex) | 12 | 2 |
| [Vertices](#vertices) | 5 | 5 |

### <a name="body"></a>`Body`

| Propriedade | Tipo | Acesso |
|-------------|------|--------|
| `Application` | `Object` | get |
| `DisplayName` | `String` | get/set |
| `Document` | `Object` | get |
| `Edges[EdgeType: FeatureTopologyQueryTypeConstants]` | `Object` | get |
| `Faces[FaceType: FeatureTopologyQueryTypeConstants]` | `Object` | get |
| `FacesByRay[Xorigin: Double, Yorigin: Double, Zorigin: Double, Xdir: Double, Ydir: Double, Zdir: Double]` | `Object` | get |
| `FacetCount[Tolerance: Double]` | `Int32` | get |
| `GeometryForm` | `Int32` | get |
| `IsOverriddenBody` | `Boolean` | get |
| `IsSolid` | `Boolean` | get |
| `Loops` | `Object` | get |
| `Parent` | `Object` | get |
| `Shells` | `Object` | get |
| `Style` | `FaceStyle` | get/set |
| `Tag` | `Int32` | get |
| `Type` | `GNTTypePropertyConstants` | get |
| `UpdatesCounter` | `Int32` | get |
| `Valid` | `Boolean` | get |
| `Vertices` | `Object` | get |
| `Visible` | `Boolean` | get/set |
| `Volume` | `Double` | get |

| Método | Retorno |
|--------|---------|
| `ClearOverrides()` | `void` |
| `ComputePhysicalProperties(Density: Double, Accuracy: Double, [out] Volume: Double, [out] Area: Double, [out] Mass: Double, [out] CenterOfGravity: Array, [out] CenterOfVolume: Array, [out] GlobalMomentsOfInteria: Array, [out] PrincipalMomentsOfInteria: Array, [out] PrincipalAxes: Array, [out] RadiiOfGyration: Array, [out] RelativeAccuracyAchieved: Double, [out] Status: Int32)` | `void` |
| `CreateCollection(Type: TopologyCollectionTypeConstants, [opt] NumberOfObjects: Object, [opt] ObjectArray: Object)` | `Object` |
| `CreateSmartCollection(SeedObject: Object, CollectionType: SmartCollectionTypeConstants, [opt] AdditionalObject: Object)` | `Object` |
| `GetEntityByID(EntityID: Int32)` | `Object` |
| `GetEntityType(Entity: Object)` | `Int32` |
| `GetExactRange([out] MinRangePoint: Array, [out] MaxRangePoint: Array)` | `void` |
| `GetExtremePoint(DirectionX: Double, DirectionY: Double, DirectionZ: Double, [out] ExtremeX: Double, [out] ExtremeY: Double, [out] ExtremeZ: Double)` | `void` |
| `GetFaceByFaceID(FaceID: Int32)` | `Object` |
| `GetFacetData(Tolerance: Double, [out] FacetCount: Int32, [out] Points: Array, [opt] [out] Normals: Object, [opt] [out] TextureCoords: Object, [opt] [out] StyleIDs: Object, [opt] [out] FaceIDs: Object, [opt] bHonourPrefs: Object)` | `void` |
| `GetIDFromEntity(Entity: Object)` | `Int32` |
| `GetRange([out] MinRangePoint: Array, [out] MaxRangePoint: Array)` | `void` |
| `GetReferenceKey([out] ReferenceKey: Array, [opt] [out] KeySize: Object)` | `void` |
| `SetFacesStyle(NumberOfFaces: Int32, [out] FacesArray: Array, Style: FaceStyle)` | `void` |

### <a name="bsplinecurve"></a>`BSplineCurve`

| Propriedade | Tipo | Acesso |
|-------------|------|--------|
| `Application` | `Object` | get |
| `IsPlanar` | `Boolean` | get |
| `IsRational` | `Boolean` | get |
| `Parent` | `Object` | get |
| `Type` | `GNTTypePropertyConstants` | get |

| Método | Retorno |
|--------|---------|
| `GetBSplineData(NumPoles: Int32, NumKnots: Int32, NumWeights: Int32, [out] Poles: Array, [out] Knots: Array, [out] Weights: Array)` | `void` |
| `GetBSplineInfo([out] Order: Int32, [out] NumPoles: Int32, [out] NumKnots: Int32, [out] Rational: Boolean, [out] Closed: Boolean, [out] Periodic: Boolean, [out] Planar: Boolean, [out] PlaneVector: Array)` | `void` |
| `GetIsClosed()` | `Boolean` |
| `GetIsPeriodic()` | `Boolean` |
| `GetNumKnots()` | `Int32` |
| `GetNumPoles()` | `Int32` |
| `GetOrder()` | `Int32` |

### <a name="bsplinesurface"></a>`BSplineSurface`

| Propriedade | Tipo | Acesso |
|-------------|------|--------|
| `Application` | `Object` | get |
| `IsPlanar` | `Boolean` | get |
| `IsRational` | `Boolean` | get |
| `Parent` | `Object` | get |
| `Type` | `GNTTypePropertyConstants` | get |

| Método | Retorno |
|--------|---------|
| `GetBSplineData(NumPoles: Int32, NumKnotsU: Int32, NumKnotsV: Int32, NumWeights: Int32, [out] Poles: Array, [out] KnotsU: Array, [out] KnotsV: Array, [out] Weights: Array)` | `void` |
| `GetBSplineInfo([out] Order: Array, [out] NumPoles: Array, [out] NumKnots: Array, [out] Rational: Boolean, [out] Closed: Array, [out] Periodic: Array, [out] Planar: Boolean)` | `void` |
| `GetIsClosed([out] Closed: Array)` | `void` |
| `GetIsPeriodic([out] Periodic: Array)` | `void` |
| `GetNumKnots([out] NumKnots: Array)` | `void` |
| `GetNumPoles([out] NumPoles: Array)` | `void` |
| `GetOrder([out] Order: Array)` | `void` |

### <a name="circle"></a>`Circle`

| Propriedade | Tipo | Acesso |
|-------------|------|--------|
| `Application` | `Object` | get |
| `Parent` | `Object` | get |
| `Radius` | `Double` | get |
| `Type` | `GNTTypePropertyConstants` | get |

| Método | Retorno |
|--------|---------|
| `GetAxisVector([out] AxisVector: Array)` | `void` |
| `GetCenterPoint([out] CenterPoint: Array)` | `void` |
| `GetCircleData([out] CenterPoint: Array, [out] AxisVector: Array, [out] Radius: Double)` | `void` |

### <a name="cone"></a>`Cone`

| Propriedade | Tipo | Acesso |
|-------------|------|--------|
| `Application` | `Object` | get |
| `Expanding` | `Boolean` | get |
| `HalfAngle` | `Double` | get |
| `Parent` | `Object` | get |
| `Radius` | `Double` | get |
| `Type` | `GNTTypePropertyConstants` | get |

| Método | Retorno |
|--------|---------|
| `GetAxisVector([out] AxisVector: Array)` | `void` |
| `GetBasePoint([out] BasePoint: Array)` | `void` |
| `GetConeData([out] BasePoint: Array, [out] AxisVector: Array, [out] Radius: Double, [out] HalfAngle: Double, [out] Expanding: Boolean)` | `void` |

### <a name="curve"></a>`Curve`

| Propriedade | Tipo | Acesso |
|-------------|------|--------|
| `Application` | `Application` | get |
| `Continuity` | `Int32` | get |
| `CurveBody` | `CurveBody` | get |
| `CurvePath` | `CurvePath` | get |
| `CurveVertices` | `CurveVertices` | get |
| `Geometry` | `Object` | get |
| `GeometryForm` | `Int32` | get |
| `IsParamReversed` | `Boolean` | get |
| `Parent` | `Object` | get |
| `Type` | `GNTTypePropertyConstants` | get |
| `UpdatesCounter` | `Int32` | get |

| Método | Retorno |
|--------|---------|
| `GetCurvature(NumParams: Int32, [out] Params: Array, [out] Directions: Array, [out] Curvatures: Array)` | `void` |
| `GetDerivatives(NumParams: Int32, [out] Params: Array, [out] FirstDerv: Array, [out] SecondDerv: Array, [out] ThirdDerv: Array)` | `void` |
| `GetEndPoints([out] StartPoint: Array, [out] EndPoint: Array)` | `void` |
| `GetExactRange([out] MinRangePoint: Array, [out] MaxRangePoint: Array)` | `void` |
| `GetLengthAtParam(FromParam: Double, ToParam: Double, [out] Length: Double)` | `void` |
| `GetParamAnomaly([out] Periodicity: Array, [out] Singular: Boolean)` | `void` |
| `GetParamAtLength(FromParam: Double, Length: Double, [out] Param: Double)` | `void` |
| `GetParamAtPoint(NumPoints: Int32, [out] Points: Array, [out] GuessParams: Array, [out] MaxDeviations: Array, [out] Params: Array, [out] Flags: Array)` | `void` |
| `GetParamAtPointEx(NumPoints: Int32, [out] Points: Array, [out] Params: Array, [opt] GuessParams: Object, [opt] [out] MaxDeviations: Object, [opt] [out] Flags: Object)` | `void` |
| `GetParamExtents([out] MinParam: Double, [out] MaxParam: Double)` | `void` |
| `GetPointAtParam(NumParams: Int32, [out] Params: Array, [out] Points: Array)` | `void` |
| `GetRange([out] MinRangePoint: Array, [out] MaxRangePoint: Array)` | `void` |
| `GetReferenceKey([out] ReferenceKey: Array, [opt] [out] KeySize: Object)` | `void` |
| `GetTangent(NumParams: Int32, [out] Params: Array, [out] Tangents: Array)` | `void` |

### <a name="curvebody"></a>`CurveBody`

| Propriedade | Tipo | Acesso |
|-------------|------|--------|
| `Application` | `Application` | get |
| `Area` | `Double` | get |
| `CurvePaths` | `CurvePaths` | get |
| `Curves` | `Curves` | get |
| `CurveVertices` | `CurveVertices` | get |
| `Document` | `Object` | get |
| `GeometryForm` | `Int32` | get |
| `HasArea` | `Boolean` | get |
| `Parent` | `Object` | get |
| `Type` | `GNTTypePropertyConstants` | get |
| `UpdatesCounter` | `Int32` | get |

| Método | Retorno |
|--------|---------|
| `GetExactRange([out] MinRangePoint: Array, [out] MaxRangePoint: Array)` | `void` |
| `GetRange([out] MinRangePoint: Array, [out] MaxRangePoint: Array)` | `void` |
| `GetReferenceKey([out] ReferenceKey: Array, [opt] [out] KeySize: Object)` | `void` |

### <a name="curvepath"></a>`CurvePath`

| Propriedade | Tipo | Acesso |
|-------------|------|--------|
| `Application` | `Application` | get |
| `Area` | `Double` | get |
| `CurveBody` | `CurveBody` | get |
| `Curves` | `Curves` | get |
| `IsClosed` | `Boolean` | get |
| `IsPlanar` | `Boolean` | get |
| `IsPointInside[[ref] Point: Array]` | `Boolean` | get |
| `IsVoid` | `Boolean` | get |
| `Parent` | `Object` | get |
| `Type` | `GNTTypePropertyConstants` | get |

| Método | Retorno |
|--------|---------|
| `GetExactRange([out] MinRangePoint: Array, [out] MaxRangePoint: Array)` | `void` |
| `GetRange([out] MinRangePoint: Array, [out] MaxRangePoint: Array)` | `void` |

### <a name="curvepaths"></a>`CurvePaths`

| Propriedade | Tipo | Acesso |
|-------------|------|--------|
| `Application` | `Application` | get |
| `Count` | `Int32` | get |
| `Parent` | `Object` | get |

| Método | Retorno |
|--------|---------|
| `GetEnumerator()` | `IEnumerator` |
| `Item(index: Object)` | `CurvePath` |

### <a name="curves"></a>`Curves`

| Propriedade | Tipo | Acesso |
|-------------|------|--------|
| `Application` | `Application` | get |
| `Count` | `Int32` | get |
| `Parent` | `Object` | get |

| Método | Retorno |
|--------|---------|
| `GetEnumerator()` | `IEnumerator` |
| `Item(index: Object)` | `Curve` |

### <a name="curvevertex"></a>`CurveVertex`

| Propriedade | Tipo | Acesso |
|-------------|------|--------|
| `Application` | `Object` | get |
| `Body` | `Object` | get |
| `ID` | `Int32` | get |
| `Parent` | `Object` | get |
| `Tag` | `Int32` | get |
| `Type` | `GNTTypePropertyConstants` | get |
| `UpdatesCounter` | `Int32` | get |

| Método | Retorno |
|--------|---------|
| `GetPointData([out] Point: Array)` | `void` |
| `GetReferenceKey([out] ReferenceKey: Array, [opt] [out] KeySize: Object)` | `void` |

### <a name="curvevertices"></a>`CurveVertices`

| Propriedade | Tipo | Acesso |
|-------------|------|--------|
| `Application` | `Object` | get |
| `Count` | `Int32` | get |
| `Parent` | `Object` | get |

| Método | Retorno |
|--------|---------|
| `GetEnumerator()` | `IEnumerator` |
| `Item(index: Object)` | `CurveVertex` |

### <a name="cylinder"></a>`Cylinder`

| Propriedade | Tipo | Acesso |
|-------------|------|--------|
| `Application` | `Object` | get |
| `Parent` | `Object` | get |
| `Radius` | `Double` | get |
| `Type` | `GNTTypePropertyConstants` | get |

| Método | Retorno |
|--------|---------|
| `GetAxisVector([out] AxisVect: Array)` | `void` |
| `GetBasePoint([out] BasePoint: Array)` | `void` |
| `GetCylinderData([out] BasePoint: Array, [out] AxisVector: Array, [out] Radius: Double)` | `void` |

### <a name="edge"></a>`Edge`

| Propriedade | Tipo | Acesso |
|-------------|------|--------|
| `Application` | `Object` | get |
| `Body` | `Object` | get |
| `Continuity` | `Int32` | get |
| `Document` | `Object` | get |
| `EndVertex` | `Object` | get |
| `Geometry` | `Object` | get |
| `GeometryForm` | `Int32` | get |
| `ID` | `Int32` | get |
| `IsClosed` | `Boolean` | get |
| `IsParamReversed` | `Boolean` | get |
| `Parent` | `Object` | get |
| `Shells` | `Object` | get |
| `StartVertex` | `Object` | get |
| `StrokeCount[Tolerance: Double]` | `Int32` | get |
| `Tag` | `Int32` | get |
| `Type` | `GNTTypePropertyConstants` | get |
| `UpdatesCounter` | `Int32` | get |

| Método | Retorno |
|--------|---------|
| `GetCurvature(NumParams: Int32, [out] Params: Array, [out] Directions: Array, [out] Curvatures: Array)` | `void` |
| `GetDerivatives(NumParams: Int32, [out] Params: Array, [out] FirstDerv: Array, [out] SecondDerv: Array, [out] ThirdDerv: Array)` | `void` |
| `GetEdgeUses([out] NumEdgeUses: Int32, [out] EdgeUses: Array)` | `void` |
| `GetEndPoints([out] StartPoint: Array, [out] EndPoint: Array)` | `void` |
| `GetExactRange([out] MinRangePoint: Array, [out] MaxRangePoint: Array)` | `void` |
| `GetFaces([out] NumFaces: Int32, [out] Faces: Array)` | `void` |
| `GetLengthAtParam(FromParam: Double, ToParam: Double, [out] Length: Double)` | `void` |
| `GetParamAnomaly([out] Periodicity: Array, [out] Singular: Boolean)` | `void` |
| `GetParamAtLength(FromParam: Double, Length: Double, [out] Param: Double)` | `void` |
| `GetParamAtPoint(NumPoints: Int32, [out] Points: Array, [out] GuessParams: Array, [out] MaxDeviations: Array, [out] Params: Array, [out] Flags: Array)` | `void` |
| `GetParamAtPointEx(NumPoints: Int32, [out] Points: Array, [out] Params: Array, [opt] GuessParams: Object, [opt] [out] MaxDeviations: Object, [opt] [out] Flags: Object)` | `void` |
| `GetParamExtents([out] MinParam: Double, [out] MaxParam: Double)` | `void` |
| `GetPointAtParam(NumParams: Int32, [out] Params: Array, [out] Points: Array)` | `void` |
| `GetRange([out] MinRangePoint: Array, [out] MaxRangePoint: Array)` | `void` |
| `GetReferenceKey([out] ReferenceKey: Array, [opt] [out] KeySize: Object)` | `void` |
| `GetStrokeData(Tolerance: Double, [out] StrokeCount: Int32, [out] Points: Array, [out] Params: Array)` | `void` |
| `GetTangent(NumParams: Int32, [out] Params: Array, [out] Tangents: Array)` | `void` |

### <a name="edges"></a>`Edges`

| Propriedade | Tipo | Acesso |
|-------------|------|--------|
| `Application` | `Object` | get |
| `Count` | `Int32` | get |
| `IsUserDefined` | `Boolean` | get |
| `Parent` | `Object` | get |
| `Type` | `GNTTypePropertyConstants` | get |

| Método | Retorno |
|--------|---------|
| `Add(Item: Object)` | `void` |
| `GetEnumerator()` | `IEnumerator` |
| `Item(index: Object)` | `Object` |
| `Remove(Item: Object)` | `void` |
| `RemoveAll()` | `void` |

### <a name="edgeuse"></a>`EdgeUse`

| Propriedade | Tipo | Acesso |
|-------------|------|--------|
| `Application` | `Object` | get |
| `Edge` | `Object` | get |
| `Geometry` | `Object` | get |
| `IsClosed` | `Boolean` | get |
| `IsOpposedToEdge` | `Boolean` | get |
| `IsParamReversed` | `Boolean` | get |
| `Loop` | `Object` | get |
| `Next` | `Object` | get |
| `Parent` | `Object` | get |
| `Partner` | `Object` | get |
| `Previous` | `Object` | get |
| `Type` | `GNTTypePropertyConstants` | get |

| Método | Retorno |
|--------|---------|
| `GetDerivatives(NumParams: Int32, [out] Params: Array, [out] FirstDerv: Array, [out] SecondDerv: Array, [out] ThirdDerv: Array)` | `void` |
| `GetEndPoints([out] StartPoint: Array, [out] EndPoint: Array)` | `void` |
| `GetExactRange([out] MinRangePoint: Array, [out] MaxRangePoint: Array)` | `void` |
| `GetParamAtPoint(NumPoints: Int32, [out] Points: Array, [out] GuessParams: Array, [out] MaxDeviations: Array, [out] Params: Array, [out] Flags: Array)` | `void` |
| `GetParamAtPointEx(NumPoints: Int32, [out] Points: Array, [out] Params: Array, [opt] GuessParams: Object, [opt] [out] MaxDeviations: Object, [opt] [out] Flags: Object)` | `void` |
| `GetParamExtents([out] MinParam: Double, [out] MaxParam: Double)` | `void` |
| `GetPointAtParam(NumParams: Int32, [out] Params: Array, [out] Points: Array)` | `void` |
| `GetRange([out] MinRangePoint: Array, [out] MaxRangePoint: Array)` | `void` |
| `GetTangent(NumParams: Int32, [out] Params: Array, [out] Tangents: Array)` | `void` |

### <a name="edgeuses"></a>`EdgeUses`

| Propriedade | Tipo | Acesso |
|-------------|------|--------|
| `Application` | `Object` | get |
| `Count` | `Int32` | get |
| `Parent` | `Object` | get |
| `Type` | `GNTTypePropertyConstants` | get |

| Método | Retorno |
|--------|---------|
| `GetEnumerator()` | `IEnumerator` |
| `Item(index: Object)` | `Object` |

### <a name="ellipse"></a>`Ellipse`

| Propriedade | Tipo | Acesso |
|-------------|------|--------|
| `Application` | `Object` | get |
| `MinorMajorRatio` | `Double` | get |
| `Parent` | `Object` | get |
| `Type` | `GNTTypePropertyConstants` | get |

| Método | Retorno |
|--------|---------|
| `GetAxisVector([out] AxisVector: Array)` | `void` |
| `GetCenterPoint([out] CenterPoint: Array)` | `void` |
| `GetEllipseData([out] CenterPoint: Array, [out] AxisVector: Array, [out] MajorAxis: Array, [out] MinorMajorRatio: Double)` | `void` |
| `GetMajorAxis([out] MajorAxis: Array)` | `void` |

### <a name="face"></a>`Face`

| Propriedade | Tipo | Acesso |
|-------------|------|--------|
| `Application` | `Object` | get |
| `Area` | `Double` | get |
| `Body` | `Object` | get |
| `Continuity` | `Int32` | get |
| `Document` | `Object` | get |
| `Edges` | `Object` | get |
| `FacetCount[Tolerance: Double]` | `Int32` | get |
| `Geometry` | `Object` | get |
| `GeometryForm` | `Int32` | get |
| `ID` | `Int32` | get |
| `IsParamReversed` | `Boolean` | get |
| `Loops` | `Object` | get |
| `Parent` | `Object` | get |
| `Shell` | `Object` | get |
| `Style` | `FaceStyle` | get/set |
| `Tag` | `Int32` | get |
| `Type` | `GNTTypePropertyConstants` | get |
| `UpdatesCounter` | `Int32` | get |
| `Vertices` | `Object` | get |

| Método | Retorno |
|--------|---------|
| `GetBendCenterLineAttributes([out] BendAtributesExist: Boolean, [opt] [out] BendCenterLineEndPoints: Object, [opt] [out] AttrbuteVersion: Object, [opt] [out] BendRadius: Object, [opt] [out] BendAngle: Object, [opt] [out] BendSweepAngle: Object, [opt] [out] BendOrientation: Object)` | `void` |
| `GetBendCenterLineAttributesEx([out] BendAtributesExist: Boolean, [opt] [out] BendCenterLineEndPoints: Object, [opt] [out] AttrbuteVersion: Object, [opt] [out] BendRadius: Object, [opt] [out] BendAngle: Object, [opt] [out] BendSweepAngle: Object, [opt] [out] BendOrientation: Object)` | `void` |
| `GetCurvatures(NumParams: Int32, [out] Params: Array, [out] MaxTangents: Array, [out] MaxCurvatures: Array, [out] MinCurvatures: Array)` | `void` |
| `GetDerivatives(NumParams: Int32, [out] Params: Array, [out] UPartials: Array, [out] VPartials: Array, [out] UUPartials: Array, [out] UVPartials: Array, [out] VVPartials: Array, [out] UUUPartials: Array, [out] VVVPartials: Array)` | `void` |
| `GetExactRange([out] MinRangePoint: Array, [out] MaxRangePoint: Array)` | `void` |
| `GetFacetData(Tolerance: Double, [out] FacetCount: Int32, [out] Points: Array, [opt] [out] Normals: Object, [opt] [out] TextureCoords: Object)` | `void` |
| `GetNormal(NumParams: Int32, [out] Params: Array, [out] Normals: Array)` | `void` |
| `GetParamAnomaly([out] PeriodicityU: Array, [out] PeriodicityV: Array, [out] EndSingularityU: Int32, [out] SingularityU: Array, [out] EndSingularityV: Int32, [out] SingularityV: Array)` | `void` |
| `GetParamAtPoint(NumPoints: Int32, [out] Points: Array, [out] GuessParams: Array, [out] MaxDeviations: Array, [out] Params: Array, [out] Flags: Array)` | `void` |
| `GetParamAtPointEx(NumPoints: Int32, [out] Points: Array, [out] Params: Array, [opt] GuessParams: Object, [opt] [out] MaxDeviations: Object, [opt] [out] Flags: Object)` | `void` |
| `GetParamOnFace(NumParams: Int32, [out] Params: Array, [out] OnFace: Array)` | `void` |
| `GetParamRange([out] MinParam: Array, [out] MaxParam: Array)` | `void` |
| `GetPointAtParam(NumParams: Int32, [out] Params: Array, [out] Points: Array)` | `void` |
| `GetRange([out] MinRangePoint: Array, [out] MaxRangePoint: Array)` | `void` |
| `GetReferenceKey([out] ReferenceKey: Array, [opt] [out] KeySize: Object)` | `void` |
| `GetRGBAVals([out] RVal: Double, [out] GVal: Double, [out] BVal: Double, [out] AVal: Double)` | `void` |
| `GetTangents(NumParams: Int32, [out] Params: Array, [out] UTangents: Array, [out] VTangents: Array)` | `void` |

### <a name="faces"></a>`Faces`

| Propriedade | Tipo | Acesso |
|-------------|------|--------|
| `Application` | `Object` | get |
| `Count` | `Int32` | get |
| `IsUserDefined` | `Boolean` | get |
| `Parent` | `Object` | get |
| `Type` | `GNTTypePropertyConstants` | get |

| Método | Retorno |
|--------|---------|
| `Add(Item: Object)` | `void` |
| `GetEnumerator()` | `IEnumerator` |
| `Item(index: Object)` | `Object` |
| `Remove(Item: Object)` | `void` |
| `RemoveAll()` | `void` |

### <a name="line"></a>`Line`

| Propriedade | Tipo | Acesso |
|-------------|------|--------|
| `Application` | `Object` | get |
| `Parent` | `Object` | get |
| `Type` | `GNTTypePropertyConstants` | get |

| Método | Retorno |
|--------|---------|
| `GetLineData([out] RootPoint: Array, [out] DirectionVector: Array)` | `void` |
| `GetRootPoint([out] RootPoint: Array)` | `void` |
| `GetVector([out] Vector: Array)` | `void` |

### <a name="loop"></a>`Loop`

| Propriedade | Tipo | Acesso |
|-------------|------|--------|
| `Application` | `Object` | get |
| `Body` | `Object` | get |
| `Document` | `Object` | get |
| `Edges` | `Object` | get |
| `EdgeUses` | `Object` | get |
| `Face` | `Object` | get |
| `ID` | `Int32` | get |
| `IsOuterLoop` | `Boolean` | get |
| `Parent` | `Object` | get |
| `Tag` | `Int32` | get |
| `Type` | `GNTTypePropertyConstants` | get |
| `Vertices` | `Object` | get |

| Método | Retorno |
|--------|---------|
| `GetExactRange([out] MinRangePoint: Array, [out] MaxRangePoint: Array)` | `void` |
| `GetRange([out] MinRangePoint: Array, [out] MaxRangePoint: Array)` | `void` |

### <a name="loops"></a>`Loops`

| Propriedade | Tipo | Acesso |
|-------------|------|--------|
| `Application` | `Object` | get |
| `Count` | `Int32` | get |
| `Parent` | `Object` | get |
| `Type` | `GNTTypePropertyConstants` | get |

| Método | Retorno |
|--------|---------|
| `GetEnumerator()` | `IEnumerator` |
| `Item(index: Object)` | `Object` |

### <a name="meshsurface"></a>`MeshSurface`

| Propriedade | Tipo | Acesso |
|-------------|------|--------|
| `Application` | `Object` | get |
| `Parent` | `Object` | get |
| `Type` | `GNTTypePropertyConstants` | get |

| Método | Retorno |
|--------|---------|
| `GetTriangleData([out] FacetCount: Int32, [out] Points: Array, [out] Normals: Array)` | `void` |
| `GetTriangleNormals([out] FacetCount: Int32, [out] Normals: Array)` | `void` |
| `GetTrianglePoints([out] FacetCount: Int32, [out] Points: Array)` | `void` |

### <a name="parambsplinecurve"></a>`ParamBSplineCurve`

| Propriedade | Tipo | Acesso |
|-------------|------|--------|
| `Application` | `Object` | get |
| `IsRational` | `Boolean` | get |
| `Parent` | `Object` | get |
| `Type` | `GNTTypePropertyConstants` | get |

| Método | Retorno |
|--------|---------|
| `GetBSpline2dData(NumPoles: Int32, NumKnots: Int32, NumWeights: Int32, [out] Poles: Array, [out] Knots: Array, [out] Weights: Array)` | `void` |
| `GetBSpline2dInfo([out] Order: Int32, [out] NumPoles: Int32, [out] NumKnots: Int32, [out] Rational: Boolean, [out] Closed: Boolean, [out] Periodic: Boolean)` | `void` |
| `GetIsClosed()` | `Boolean` |
| `GetIsPeriodic()` | `Boolean` |
| `GetNumKnots()` | `Int32` |
| `GetNumPoles()` | `Int32` |
| `GetOrder()` | `Int32` |

### <a name="plane"></a>`Plane`

| Propriedade | Tipo | Acesso |
|-------------|------|--------|
| `Application` | `Object` | get |
| `Parent` | `Object` | get |
| `Type` | `GNTTypePropertyConstants` | get |

| Método | Retorno |
|--------|---------|
| `GetNormalVector([out] NormalVector: Array)` | `void` |
| `GetPlaneData([out] RootPoint: Array, [out] NormalVector: Array)` | `void` |
| `GetPlaneProperties(ReqRelAccy: Double, [out] Area: Double, [out] RootPoint: Array, [out] Moments: Array, [out] PrincipalAxes: Array, [out] RelAccyAchieved: Double)` | `void` |
| `GetRootPoint([out] RootPoint: Array)` | `void` |

### <a name="pline"></a>`PLine`

| Propriedade | Tipo | Acesso |
|-------------|------|--------|
| `Application` | `Object` | get |
| `Parent` | `Object` | get |
| `Type` | `GNTTypePropertyConstants` | get |

| Método | Retorno |
|--------|---------|
| `GetBaseParameter([out] BaseParameter: Double)` | `void` |
| `GetClosed([out] Closed: Boolean)` | `void` |
| `GetPLineData([out] PointsCount: Int32, [out] Points: Array, [out] Closed: Boolean, [out] BaseParameter: Double)` | `void` |
| `GetPoints([out] PointsCount: Int32, [out] Points: Array)` | `void` |

### <a name="shell"></a>`Shell`

| Propriedade | Tipo | Acesso |
|-------------|------|--------|
| `Application` | `Object` | get |
| `Body` | `Object` | get |
| `Document` | `Object` | get |
| `Edges` | `Object` | get |
| `Faces` | `Object` | get |
| `ID` | `Int32` | get |
| `IsClosed` | `Boolean` | get |
| `IsPointInside[[ref] Point: Array]` | `Boolean` | get |
| `IsVoid` | `Boolean` | get |
| `Parent` | `Object` | get |
| `Tag` | `Int32` | get |
| `Type` | `GNTTypePropertyConstants` | get |
| `Volume` | `Double` | get |

| Método | Retorno |
|--------|---------|
| `GetExactRange([out] MinRangePoint: Array, [out] MaxRangePoint: Array)` | `void` |
| `GetNonStitchedEdges([out] NumEdgeCollections: Int32, [out] EdgeCollections: Array)` | `void` |
| `GetRange([out] MinRangePoint: Array, [out] MaxRangePoint: Array)` | `void` |

### <a name="shells"></a>`Shells`

| Propriedade | Tipo | Acesso |
|-------------|------|--------|
| `Application` | `Object` | get |
| `Count` | `Int32` | get |
| `Parent` | `Object` | get |
| `Type` | `GNTTypePropertyConstants` | get |

| Método | Retorno |
|--------|---------|
| `GetEnumerator()` | `IEnumerator` |
| `Item(index: Object)` | `Object` |

### <a name="sphere"></a>`Sphere`

| Propriedade | Tipo | Acesso |
|-------------|------|--------|
| `Application` | `Object` | get |
| `Parent` | `Object` | get |
| `Radius` | `Double` | get |
| `Type` | `GNTTypePropertyConstants` | get |

| Método | Retorno |
|--------|---------|
| `GetCenterPoint([out] CenterPoint: Array)` | `void` |
| `GetSphereData([out] CenterPoint: Array, [out] Radius: Double)` | `void` |

### <a name="torus"></a>`Torus`

| Propriedade | Tipo | Acesso |
|-------------|------|--------|
| `Application` | `Object` | get |
| `MajorRadius` | `Double` | get |
| `MinorRadius` | `Double` | get |
| `Parent` | `Object` | get |
| `Type` | `GNTTypePropertyConstants` | get |

| Método | Retorno |
|--------|---------|
| `GetAxisVector([out] AxisVector: Array)` | `void` |
| `GetCenterPoint([out] CenterPoint: Array)` | `void` |
| `GetTorusData([out] CenterPoint: Array, [out] AxisVector: Array, [out] MajorRadius: Double, [out] MinorRadius: Double)` | `void` |

### <a name="vertex"></a>`Vertex`

| Propriedade | Tipo | Acesso |
|-------------|------|--------|
| `Application` | `Object` | get |
| `Body` | `Object` | get |
| `Document` | `Object` | get |
| `Edges` | `Object` | get |
| `Faces` | `Object` | get |
| `ID` | `Int32` | get |
| `Loops` | `Object` | get |
| `Parent` | `Object` | get |
| `Shells` | `Object` | get |
| `Tag` | `Int32` | get |
| `Type` | `GNTTypePropertyConstants` | get |
| `UpdatesCounter` | `Int32` | get |

| Método | Retorno |
|--------|---------|
| `GetPointData([out] Point: Array)` | `void` |
| `GetReferenceKey([out] ReferenceKey: Array, [opt] [out] KeySize: Object)` | `void` |

### <a name="vertices"></a>`Vertices`

| Propriedade | Tipo | Acesso |
|-------------|------|--------|
| `Application` | `Object` | get |
| `Count` | `Int32` | get |
| `IsUserDefined` | `Boolean` | get |
| `Parent` | `Object` | get |
| `Type` | `GNTTypePropertyConstants` | get |

| Método | Retorno |
|--------|---------|
| `Add(Item: Object)` | `void` |
| `GetEnumerator()` | `IEnumerator` |
| `Item(index: Object)` | `Object` |
| `Remove(Item: Object)` | `void` |
| `RemoveAll()` | `void` |

