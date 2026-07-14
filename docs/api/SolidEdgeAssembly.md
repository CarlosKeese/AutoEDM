# Type Library `SolidEdgeAssembly` (reflexão do Interop.SolidEdge v219.0.0.0 — SE 2023)

> **Origem:** reflexão de `Interop.SolidEdge.dll` (não do dump da typelib — a lib de geometria não é capturada pelo walk). Tipos são a **projeção .NET**; `[out]`/`[ref]` marcam parâmetros de saída — em **late binding** eles precisam de `ParameterModifier` by-ref ou voltam vazios (ver [GUIA_SOLID_EDGE_COM.md](../GUIA_SOLID_EDGE_COM.md)). Geometria/modelagem usam **metros/radianos**; coleções são **1-based**.

- **Objetos/interfaces:** 132  ·  **Enums:** 56

## Enums / constantes

### `AlternateAssemblyTypeConstants`

| Constante | Valor |
|-----------|-------|
| `seAlternateAssemblyType_Family` | 1 |
| `seAlternateAssemblyType_AlternatePosition` | 2 |

### `AssemblyBaseStylesConstants`

| Constante | Valor |
|-----------|-------|
| `seAssemblyConstructionStyle` | 1 |
| `seAssemblyThreadedCylindersStyle` | 2 |
| `seAssemblyCurveStyle` | 3 |
| `seAssemblyWeldBeadStyle` | 4 |

### `AssemblyCopyActionConstants`

| Constante | Valor |
|-----------|-------|
| `seAssemblyCopyActionInclude` | 0 |
| `seAssemblyCopyActionExclude` | 1 |
| `seAssemblyCopyActionPending` | 2 |
| `seAssemblyCopyActionMirror` | 3 |
| `seAssemblyCopyActionRotate` | 4 |
| `seAssemblyCopyActionDefault` | 5 |

### `AssemblyCopyComponentConstants`

| Constante | Valor |
|-----------|-------|
| `seAssemblyCopyComponentsIncludeAll` | 0 |
| `seAssemblyCopyComponentsExcludeAll` | 1 |
| `seAssemblyCopyComponentsIncludeSpecified` | 2 |
| `seAssemblyCopyComponentsExcludeSpecified` | 3 |

### `AssemblyCopyStatusConstants`

| Constante | Valor |
|-----------|-------|
| `seAssemblyCopyStatusOK` | 0 |
| `seAssemblyCopyStatusOutOfDate` | 1 |
| `seAssemblyCopyStatusFrozen` | 2 |
| `seAssemblyCopyStatusPending` | 3 |
| `seAssemblyCopyStatusMirrorPlaneMissing` | 4 |
| `seAssemblyCopyStatusCoordinateSystemMissing` | 5 |
| `seAssemblyCopyStatusFailed` | 6 |

### `AssemblyCopyTypeConstants`

| Constante | Valor |
|-----------|-------|
| `seAssemblyCopyTypeDefault` | 0 |
| `seAssemblyCopyTypeMirror` | 1 |
| `seAssemblyCopyTypeMultiBodyPart` | 2 |

### `AssemblyFamilyMemberPropertyConstants`

| Constante | Valor |
|-----------|-------|
| `seAssemblyFamilyMemberPropertyDocumentNumber` | 0 |
| `seAssemblyFamilyMemberPropertyRevisionNumber` | 1 |
| `seAssemblyFamilyMemberPropertyProjectName` | 2 |

### `AssemblyGlobalConstants`

| Constante | Valor |
|-----------|-------|
| `seAssemblyGlobalTubeWallThickness` | 1 |
| `seAssemblyGlobalTubeBendRadius` | 2 |
| `seAssemblyGlobalTubeOuterDiameter` | 3 |
| `seAssemblyGlobalTubeMinimumFlatLength` | 4 |
| `seAssemblyGlobalTubeEndTreatmentOutsideDiameter` | 5 |
| `seAssemblyGlobalTubeEndTreatmentInsideDiameter` | 6 |
| `seAssemblyGlobalTubeEndTreatmentDepth` | 7 |
| `seAssemblyGlobalTubeEndTreatmentAngle` | 8 |
| `seAssemblyGlobalTubeEndTreatmentRadius` | 9 |
| `seAssemblyGlobalDefaultPartDensity` | 10 |
| `seAssemblyGlobalDefaultAccuracyForPartDensity` | 11 |
| `seAssemblyGlobalWireHarnessDefaultSlackCompensation` | 12 |
| `seAssemblyGlobalWireHarnessDefaultHoleClearance` | 13 |
| `seAssemblyGlobalWireHarnessDefaultBundleClearance` | 14 |
| `seAssemblyGlobalWireHarnessDefaultWireAdder` | 15 |
| `seAssemblyGlobalWireHarnessDefaultCableAdder` | 16 |
| `seAssemblyGlobalWireHarnessDefaultBundleAdder` | 17 |
| `seAssemblyGlobalUpdatePhysicalPropertiesOnSave` | 18 |
| `seAssemblyGlobalAutomaticUpdate` | 19 |

### `AssemblyPathfinderUpdateConstants`

| Constante | Valor |
|-----------|-------|
| `seUpdate` | 1 |
| `seRebuild` | 2 |
| `seSuspend` | 3 |
| `seResume` | 4 |
| `seExpandAll` | 5 |
| `seCollapse` | 6 |

### `AssemblyPatternTypeConstants`

| Constante | Valor |
|-----------|-------|
| `seAssemblyPatternType` | 1 |
| `seAssemblyDuplicatePatternType` | 2 |
| `seAssemblyPatternAlongCurveType` | 3 |

### `AssemblyReportTypeConstants`

| Constante | Valor |
|-----------|-------|
| `seAssemblyWireReportAtomic` | 0 |
| `seAssemblyWireReportExpanded` | 1 |

### `AssemblyWireHarnessReportTypeConstants`

| Constante | Valor |
|-----------|-------|
| `seAssemblyWireHarnessReportComponents` | 1 |
| `seAssemblyWireHarnessReportConnections` | 2 |

### `CloneComponentOptions`

| Constante | Valor |
|-----------|-------|
| `seRepairUnsatisfiedRelationships` | 0 |
| `seDoNotCreateRelationships` | 2 |
| `seCreateGroundRelationships` | 3 |

### `CloneMatchTypeOptions`

| Constante | Valor |
|-----------|-------|
| `CloneMatchTypeAutomatic` | 0 |
| `CloneMatchTypeExact` | 1 |

### `ConfigurationTypeConstants`

| Constante | Valor |
|-----------|-------|
| `seConfigurationType_Display` | 0 |
| `seConfigurationType_Explode` | 1 |

### `ConstraintReplacementConstants`

| Constante | Valor |
|-----------|-------|
| `seConstraintReplacementNone` | 0 |
| `seConstraintReplacementSuppress` | 1 |
| `seConstraintReplacementDelete` | 2 |

### `CurveSegmentPathAdditionStatusConstants`

| Constante | Valor |
|-----------|-------|
| `seCurveSegmentPathAdditionStatusSucceeded` | 0 |
| `seCurveSegmentPathAdditionStatusFailedUnknownReason` | 1 |
| `seCurveSegmentPathAdditionStatusFailedBreak` | 2 |
| `seCurveSegmentPathAdditionStatusFailedDuplicate` | 3 |
| `seCurveSegmentPathAdditionStatusFailedFork` | 4 |
| `seCurveSegmentPathAdditionStatusFailedIntersection` | 5 |
| `seCurveSegmentPathAdditionStatusFailedLoop` | 6 |
| `seCurveSegmentPathAdditionStatusFailedUnknownSegment` | 7 |
| `seCurveSegmentPathAdditionStatusFailedLength` | 8 |

### `CurveSegmentPathRemovalStatusConstants`

| Constante | Valor |
|-----------|-------|
| `seCurveSegmentPathRemovalStatusSucceeded` | 0 |
| `seCurveSegmentPathRemovalStatusFailedUnknownReason` | 1 |
| `seCurveSegmentPathRemovalStatusFailedBreak` | 2 |
| `seCurveSegmentPathRemovalStatusFailedNotInPath` | 3 |
| `seCurveSegmentPathRemovalStatusFailedSingle` | 4 |
| `seCurveSegmentPathRemovalStatusFailedEmpty` | 5 |

### `CurveSegmentValidationConstants`

| Constante | Valor |
|-----------|-------|
| `seCurveSegmentValidation_valid` | 0 |
| `seCurveSegmentValidation_break` | 1 |
| `seCurveSegmentValidation_angle` | 2 |
| `seCurveSegmentValidation_length` | 4 |
| `seCurveSegmentValidation_intersection` | 8 |
| `seCurveSegmentValidation_fork` | 16 |
| `seCurveSegmentValidation_duplicate_segment` | 32 |
| `seCurveSegmentValidation_loop` | 64 |
| `seCurveSegmentValidation_unknown_segment` | 128 |
| `seCurveSegmentValidation_unknown` | 256 |
| `seCurveSegmentValidation_empty` | 512 |
| `seCurveSegmentValidation_single_segment` | 1024 |
| `seCurveSegmentValidation_connectivity` | 2048 |
| `seCurveSegmentValidation_calculation` | 4096 |
| `seCurveSegmentValidation_status` | 8192 |

### `CurveSegmentWhichKeypointsConstants`

| Constante | Valor |
|-----------|-------|
| `seCurveSegmentWhichKeypoints_mid_points` | 1 |
| `seCurveSegmentWhichKeypoints_end_points` | 2 |
| `seCurveSegmentWhichKeypoints_all_points` | 3 |

### `HarnessSaveAsEcadStatusConstants`

| Constante | Valor |
|-----------|-------|
| `seHarnessSaveAsEcadStatus_Success` | 0 |
| `seHarnessSaveAsEcadStatus_Failed` | 1 |
| `seHarnessSaveAsEcadStatus_FailedBadArgs` | 2 |
| `seHarnessSaveAsEcadStatus_FailedNoComps` | 3 |
| `seHarnessSaveAsEcadStatus_FailedNoConns` | 4 |
| `seHarnessSaveAsEcadStatus_FailedDupComps` | 5 |
| `seHarnessSaveAsEcadStatus_FailedDupConns` | 6 |
| `seHarnessSaveAsEcadStatus_FailedComps` | 7 |
| `seHarnessSaveAsEcadStatus_FailedConns` | 8 |
| `seHarnessSaveAsEcadStatus_FailedBoth` | 9 |
| `seHarnessSaveAsEcadStatus_FailedBadConfig` | 10 |

### `HarnessTypeConstants`

| Constante | Valor |
|-----------|-------|
| `seHarnessType_Wire` | 1 |
| `seHarnessType_Cable` | 2 |
| `seHarnessType_Bundle` | 3 |
| `seHarnessType_Wires` | 4 |
| `seHarnessType_Cables` | 5 |
| `seHarnessType_Bundles` | 6 |
| `seHarnessType_Splice` | 7 |
| `seHarnessType_Splices` | 8 |

### `InterferenceStatusConstants`

| Constante | Valor |
|-----------|-------|
| `seInterferenceStatusNoInterference` | 1 |
| `seInterferenceStatusConfirmedInterference` | 2 |
| `seInterferenceStatusProbableInterference` | 3 |
| `seInterferenceStatusConfirmedAndProbableInterference` | 4 |
| `seInterferenceStatusIncompleteAnalysis` | 5 |

### `ItemNumberModeConstants`

| Constante | Valor |
|-----------|-------|
| `seItemNumber_None` | 0 |
| `seItemNumber_Toplevel` | 1 |
| `seItemNumber_Atomic` | 2 |
| `seItemNumber_Exploded` | 3 |
| `seItemNumber_LevelBased` | 4 |

### `MoveMultipleMoveTypeConstants`

| Constante | Valor |
|-----------|-------|
| `seMoveMultipleMove` | 1 |
| `seMoveMultipleCopy` | 2 |

### `MoveMultipleRelationshipConstants`

| Constante | Valor |
|-----------|-------|
| `seMoveMultipleMaintainInternalRelationships` | 1 |
| `seMoveMultipleDropInternalRelationships` | 2 |
| `seMoveMultipleDropInternalRelationshipsAndGround` | 3 |

### `OccurrenceStatusConstants`

| Constante | Valor |
|-----------|-------|
| `seOccurrenceStatusWellDefined` | 1 |
| `seOccurrenceStatusFixed` | 2 |
| `seOccurrenceStatusUnderDefined` | 4 |
| `seOccurrenceStatusOverDefined` | 32776 |
| `seOccurrenceStatusNotConsistent` | 32784 |
| `seOccurrenceStatusNotChanged` | 32800 |
| `seOccurrenceStatusNonAlgebraic` | 32832 |
| `seOccurrenceStatusUnknown` | 32896 |

### `PartStatusConstants`

| Constante | Valor |
|-----------|-------|
| `igPartStatusWellDefined` | 1 |
| `igPartStatusFixed` | 2 |
| `igPartStatusUnderDefined` | 4 |
| `igPartStatusOverDefined` | 32776 |
| `igPartStatusNotConsistent` | 32784 |
| `igPartStatusNotChanged` | 32800 |
| `igPartStatusNonAlgebraic` | 32832 |
| `igPartStatusUnknown` | 32896 |

### `PatternOffsetTypeConstants`

| Constante | Valor |
|-----------|-------|
| `sePatternFitOffset` | 0 |
| `sePatternFillOffset` | 1 |
| `sePatternFixedOffset` | 2 |
| `sePatternChordLengthOffset` | 3 |

### `PipeFittingEndTreatmentConstants`

| Constante | Valor |
|-----------|-------|
| `sePipeFittingEndTreatmentNone` | 0 |
| `sePipeFittingEndTreatmentSocketWeld` | 1 |
| `sePipeFittingEndTreatmentButtWeld` | 2 |
| `sePipeFittingEndTreatmentFlange` | 3 |
| `sePipeFittingEndTreatmentThread` | 4 |
| `sePipeFittingEndTreatmentPipePenetration` | 5 |

### `PipeFittingTypeConstants`

| Constante | Valor |
|-----------|-------|
| `sePipeFittingTypeNone` | 0 |
| `sePipeFittingTypeElbow` | 1 |
| `sePipeFittingTypeY` | 2 |
| `sePipeFittingTypeTee` | 3 |
| `sePipeFittingTypeCoupling` | 4 |
| `sePipeFittingTypeReducer` | 5 |
| `sePipeFittingTypeCross` | 6 |
| `sePipeFittingTypePlug` | 7 |
| `sePipeFittingTypeCompanionFlange` | 8 |
| `sePipeFittingTypeReturn180` | 9 |

### `QueryConditionConstants`

| Constante | Valor |
|-----------|-------|
| `seQueryConditionContains` | 0 |
| `seQueryConditionIs` | 1 |
| `seQueryConditionIsNot` | 2 |

### `QueryPropertyConstants`

| Constante | Valor |
|-----------|-------|
| `seQueryPropertyName` | 0 |
| `seQueryPropertyTitle` | 1 |
| `seQueryPropertySubject` | 2 |
| `seQueryPropertyAuthor` | 3 |
| `seQueryPropertyManager` | 4 |
| `seQueryPropertyCompany` | 5 |
| `seQueryPropertyCategory` | 6 |
| `seQueryPropertyKeywords` | 7 |
| `seQueryPropertyComments` | 8 |
| `seQueryPropertyDocumentNumber` | 9 |
| `seQueryPropertyRevisionNumber` | 10 |
| `seQueryPropertyProject` | 11 |
| `seQueryPropertyMaterial` | 12 |
| `seQueryPropertyStatus` | 13 |
| `seQueryPropertyReference` | 14 |
| `seQueryPropertyCustom` | 15 |
| `seQueryPropertyCustomOccurrence` | 16 |

### `QueryScopeConstants`

| Constante | Valor |
|-----------|-------|
| `seQueryScopeAllParts` | 0 |
| `seQueryScopeShownParts` | 1 |
| `seQueryScopeHiddenParts` | 2 |
| `seQueryScopeSelectedParts` | 3 |

### `Relation3dDetailedStatusConstants`

| Constante | Valor |
|-----------|-------|
| `igRelation3dDetailedStatusUnknown` | 0 |
| `igRelation3dDetailedStatusSolved` | 1 |
| `igRelation3dDetailedStatusSuppressed` | 2 |
| `igRelation3dDetailedStatusBetweenSetMembers` | 3 |
| `igRelation3dDetailedStatusBetweenFixed` | 4 |
| `igRelation3dDetailedStatusUnsatisfied` | 5 |
| `igRelation3dDetailedStatusMissingGeometry` | 6 |

### `Relation3dGearRatioTypeConstants`

| Constante | Valor |
|-----------|-------|
| `igRelation3dGearRatioTypeNumberOfTurns` | 0 |
| `igRelation3dGearRatioTypeNumberOfTeeth` | 1 |

### `Relation3dGearTypeConstants`

| Constante | Valor |
|-----------|-------|
| `igRelation3dGearTypeRotaryRotary` | 0 |
| `igRelation3dGearTypeRotaryLinear` | 1 |
| `igRelation3dGearTypeLinearLinear` | 2 |

### `Relation3dGeometryConstants`

| Constante | Valor |
|-----------|-------|
| `igRelation3dGeometryPlane` | 1 |
| `igRelation3dGeometryLine` | 2 |
| `igRelation3dGeometryPoint` | 3 |
| `igRelation3dStartPoint` | 4 |
| `igRelation3dMidPoint` | 5 |
| `igRelation3dEndPoint` | 6 |
| `igRelation3dCenterPoint` | 7 |
| `igRelation3dPointUnknown` | 8 |
| `igRelation3dGeometrySphere` | 9 |
| `igRelation3dGeometryCone` | 10 |
| `igRelation3dGeometrySurface` | 11 |
| `igRelation3dGeometrySweepSurface` | 12 |

### `Relation3dOrientationConstants`

| Constante | Valor |
|-----------|-------|
| `igRelation3dOrientationNotspecified` | 0 |
| `igRelation3dOrientationAlign` | 1 |
| `igRelation3dOrientationAntialign` | 2 |

### `Relation3dStatusConstants`

| Constante | Valor |
|-----------|-------|
| `igRelation3dStatusUnsolved` | 0 |
| `igRelation3dStatusSolved` | 1 |

### `seAssemblyBodyTypeConstants`

| Constante | Valor |
|-----------|-------|
| `seAssemblyBodyType_WeldBeadBody` | 1 |
| `seAssemblyBodyType_HarnessBody` | 2 |
| `seAssemblyBodyType_GenericAssemblyBody` | 3 |

### `SegmentRelation3dDirectionConstants`

| Constante | Valor |
|-----------|-------|
| `seSegmentRelation3dDirectionParallel` | 0 |
| `seSegmentRelation3dDirectionPerpendicular` | 1 |
| `seSegmentRelation3dDirectionCoincident` | 2 |

### `SegmentRelation3dDistanceConstants`

| Constante | Valor |
|-----------|-------|
| `seSegmentRelation3dDistanceNormal` | 0 |
| `seSegmentRelation3dDistanceReverse` | 1 |
| `seSegmentRelation3dDistanceTrueLength` | 2 |

### `SegmentRelation3dGeometryConstants`

| Constante | Valor |
|-----------|-------|
| `seSegmentRelation3dStartPoint` | 1 |
| `seSegmentRelation3dEndPoint` | 2 |
| `seSegmentRelation3dUnbounded` | 3 |
| `seSegmentRelation3dArcCenter` | 4 |
| `seSegmentRelation3dEllipseCenter` | 5 |
| `seSegmentRelation3dLineStartPoint` | 6 |
| `seSegmentRelation3dLineEndPoint` | 7 |
| `seSegmentRelation3dRefPlane` | 8 |
| `seSegmentRelation3dArc` | 9 |

### `SegmentRelation3dStatusConstants`

| Constante | Valor |
|-----------|-------|
| `seSegmentRelation3dStatusUnsolved` | 0 |
| `seSegmentRelation3dStatusSolved` | 1 |

### `SimplifiedAssemblyMode`

| Constante | Valor |
|-----------|-------|
| `seSimplifiedAssemblyModeUnknown` | 0 |
| `seSimplifiedAssemblyModeModeled` | 1 |
| `seSimplifiedAssemblyModeVisibleFaces` | 2 |

### `StructuralFrameEndConditionConstants`

| Constante | Valor |
|-----------|-------|
| `seMiter` | 0 |
| `seButt1` | 1 |
| `seButt2` | 2 |
| `seNone` | 3 |
| `seRadius` | 4 |
| `seExtend` | 5 |
| `seMiterXY` | 6 |
| `seMiterYZ` | 7 |
| `seMiterXZ` | 8 |
| `seButtX` | 9 |
| `seButtY` | 10 |
| `seButtZ` | 11 |

### `TubePropertyPidConstants`

| Constante | Valor |
|-----------|-------|
| `seTubePropertyPid_TubeBendRadius` | 1508 |
| `seTubePropertyPid_TubeOuterDiameter` | 1509 |
| `seTubePropertyPid_TubeMinimumFlatLength` | 1510 |
| `seTubePropertyPid_TubeWallThickness` | 1511 |
| `seTubePropertyPid_TubeFlatLength` | 1512 |
| `seTubePropertyPid_TubeAreaInsideDiameter` | 1513 |
| `seTubePropertyPid_TubeVolumeInsideDiameter` | 1514 |
| `seTubePropertyPid_TubeEndTreatmentTypeEnd1` | 1515 |
| `seTubePropertyPid_TubeEndTreatmentTypeEnd2` | 1516 |

### `TubeSegmentAdditionStatusConstants`

| Constante | Valor |
|-----------|-------|
| `seTubeSegmentAdditionStatusSucceeded` | 1 |
| `seTubeSegmentAdditionStatusFailedSplit` | 2 |
| `seTubeSegmentAdditionStatusFailedDisjoint` | 3 |
| `seTubeSegmentAdditionStatusFailedUnknownReason` | 4 |

### `TubeSegmentRemovalStatusConstants`

| Constante | Valor |
|-----------|-------|
| `seTubeSegmentRemovalStatusSucceeded` | 1 |
| `seTubeSegmentRemovalStatusFailedNotPartOfTube` | 2 |
| `seTubeSegmentRemovalStatusFailedDueToDisjoint` | 3 |
| `seTubeSegmentRemovalStatusFailedUnknownReason` | 4 |

### `UpdateStructureCacheConstants`

| Constante | Valor |
|-----------|-------|
| `seUseOpenDocuments` | 1 |
| `seWalkFilesOnDisk` | 2 |

### `VirtualComponentPublishConstants`

| Constante | Valor |
|-----------|-------|
| `seVCPublishOn_FrontView` | 1 |
| `seVCPublishOn_TopView` | 2 |
| `seVCPublishOn_RightView` | 3 |
| `seVCPublishOn_SketchView` | 4 |

### `VirtualComponentStatusConstants`

| Constante | Valor |
|-----------|-------|
| `seVCStatus_Success` | 1 |
| `seVCStatus_Fail` | 2 |
| `seVCStatus_AddUnManagedToManaged` | 3 |
| `seVCStatus_AddManagedToUnManaged` | 4 |
| `seVCStatus_ReplaceConflictWithVirtualComponent` | 5 |
| `seVCStatus_SourceFileNotFound` | 6 |
| `seVCStatus_NameConflictWithVirtualComponent` | 7 |
| `seVCStatus_NameConflictWithPreDefinedComponent` | 8 |
| `seVCStatus_DuplicateNameConflictWithVirtualComponent` | 9 |
| `seVCStatus_CannotAddToComponent` | 10 |
| `seVCStatus_ComponentSketchMissing` | 11 |

### `VirtualComponentTypeConstants`

| Constante | Valor |
|-----------|-------|
| `seVirtualComponentType_Unknown` | 1 |
| `seVirtualComponentType_Assembly` | 2 |
| `seVirtualComponentType_Part` | 3 |
| `seVirtualComponentType_Sheetmetal` | 4 |

### `VisibilityBasedSimplifiedAssemblyCopyType`

| Constante | Valor |
|-----------|-------|
| `seVisibilityBasedSimplifiedAssemblyCopyTypeFaces` | 0 |
| `seVisibilityBasedSimplifiedAssemblyCopyTypeBodies` | 1 |

### `WirePathConstants`

| Constante | Valor |
|-----------|-------|
| `seSingleWirePath` | 0 |
| `seCableWirePathMaster` | 1 |
| `seCableWirePathMember` | 2 |

## Objetos / interfaces

| Tipo | Propriedades | Métodos |
|------|-------------|---------|
| [AdjustablePart](#adjustablepart) | 6 | 4 |
| [AFGrooveWeld](#afgrooveweld) | 10 | 8 |
| [AFGrooveWelds](#afgroovewelds) | 3 | 2 |
| [AngularRelation3d](#angularrelation3d) | 17 | 11 |
| [ArcSegment](#arcsegment) | 7 | 4 |
| [ArcSegments](#arcsegments) | 3 | 3 |
| [AsmRefPlane](#asmrefplane) | 10 | 6 |
| [AsmRefPlanes](#asmrefplanes) | 4 | 8 |
| [AssemblyBodies](#assemblybodies) | 6 | 2 |
| [AssemblyBody](#assemblybody) | 12 | 3 |
| [AssemblyCopies](#assemblycopies) | 3 | 4 |
| [AssemblyCopy](#assemblycopy) | 13 | 5 |
| [AssemblyDocument](#assemblydocument) | 111 | 80 |
| [AssemblyDrivenPartFeatures](#assemblydrivenpartfeatures) | 5 | 0 |
| [AssemblyDrivenPartFeaturesExtrudedCutout](#assemblydrivenpartfeaturesextrudedcutout) | 8 | 20 |
| [AssemblyDrivenPartFeaturesExtrudedCutouts](#assemblydrivenpartfeaturesextrudedcutouts) | 3 | 3 |
| [AssemblyDrivenPartFeaturesHole](#assemblydrivenpartfeatureshole) | 8 | 20 |
| [AssemblyDrivenPartFeaturesHoles](#assemblydrivenpartfeaturesholes) | 3 | 3 |
| [AssemblyDrivenPartFeaturesRevolvedCutout](#assemblydrivenpartfeaturesrevolvedcutout) | 8 | 18 |
| [AssemblyDrivenPartFeaturesRevolvedCutouts](#assemblydrivenpartfeaturesrevolvedcutouts) | 3 | 3 |
| [AssemblyFamilyMember](#assemblyfamilymember) | 5 | 7 |
| [AssemblyFamilyMembers](#assemblyfamilymembers) | 8 | 11 |
| [AssemblyFeatures](#assemblyfeatures) | 15 | 1 |
| [AssemblyFeaturesExtrudedCutout](#assemblyfeaturesextrudedcutout) | 12 | 23 |
| [AssemblyFeaturesExtrudedCutouts](#assemblyfeaturesextrudedcutouts) | 3 | 3 |
| [AssemblyFeaturesExtrudedProtrusion](#assemblyfeaturesextrudedprotrusion) | 10 | 21 |
| [AssemblyFeaturesExtrudedProtrusions](#assemblyfeaturesextrudedprotrusions) | 3 | 3 |
| [AssemblyFeaturesHole](#assemblyfeatureshole) | 12 | 23 |
| [AssemblyFeaturesHoles](#assemblyfeaturesholes) | 3 | 3 |
| [AssemblyFeaturesMirror](#assemblyfeaturesmirror) | 12 | 13 |
| [AssemblyFeaturesMirrors](#assemblyfeaturesmirrors) | 3 | 3 |
| [AssemblyFeaturesPattern](#assemblyfeaturespattern) | 12 | 13 |
| [AssemblyFeaturesPatterns](#assemblyfeaturespatterns) | 3 | 3 |
| [AssemblyFeaturesRevolvedCutout](#assemblyfeaturesrevolvedcutout) | 12 | 21 |
| [AssemblyFeaturesRevolvedCutouts](#assemblyfeaturesrevolvedcutouts) | 3 | 3 |
| [AssemblyFeaturesRevolvedProtrusion](#assemblyfeaturesrevolvedprotrusion) | 10 | 19 |
| [AssemblyFeaturesRevolvedProtrusions](#assemblyfeaturesrevolvedprotrusions) | 3 | 3 |
| [AssemblyFeaturesSweptProtrusion](#assemblyfeaturessweptprotrusion) | 10 | 7 |
| [AssemblyFeaturesSweptProtrusions](#assemblyfeaturessweptprotrusions) | 3 | 3 |
| [AssemblyFilletWeld](#assemblyfilletweld) | 16 | 9 |
| [AssemblyFilletWelds](#assemblyfilletwelds) | 3 | 3 |
| [AssemblyGroup](#assemblygroup) | 6 | 5 |
| [AssemblyGroups](#assemblygroups) | 4 | 3 |
| [AssemblyLabelWeld](#assemblylabelweld) | 9 | 8 |
| [AssemblyLabelWelds](#assemblylabelwelds) | 5 | 3 |
| [AssemblyMirror](#assemblymirror) | 5 | 1 |
| [AssemblyMirrors](#assemblymirrors) | 3 | 1 |
| [AssemblyPattern](#assemblypattern) | 5 | 10 |
| [AssemblyPatternOccurrence](#assemblypatternoccurrence) | 5 | 2 |
| [AssemblyPatterns](#assemblypatterns) | 3 | 9 |
| [AssemblyProperties](#assemblyproperties) | 4 | 5 |
| [AssemblyProperty](#assemblyproperty) | 4 | 8 |
| [AssemblyStitchWeld](#assemblystitchweld) | 15 | 7 |
| [AssemblyStitchWelds](#assemblystitchwelds) | 3 | 3 |
| [AssemblyThread](#assemblythread) | 11 | 8 |
| [AssemblyThreads](#assemblythreads) | 3 | 3 |
| [AxialRelation3d](#axialrelation3d) | 21 | 11 |
| [Bundle](#bundle) | 18 | 13 |
| [Bundles](#bundles) | 4 | 3 |
| [Cable](#cable) | 27 | 13 |
| [Cables](#cables) | 4 | 3 |
| [CamFollowerRelation3d](#camfollowerrelation3d) | 11 | 6 |
| [CenterPlaneRelation3d](#centerplanerelation3d) | 9 | 7 |
| [ComponentLayout](#componentlayout) | 8 | 1 |
| [ComponentLayouts](#componentlayouts) | 3 | 3 |
| [Configuration](#configuration) | 5 | 4 |
| [Configurations](#configurations) | 4 | 6 |
| [CurveSegment](#curvesegment) | 10 | 6 |
| [CurveSegments](#curvesegments) | 3 | 3 |
| [DefaultCustomOccurrenceProperties](#defaultcustomoccurrenceproperties) | 3 | 2 |
| [FastenerSystem](#fastenersystem) | 4 | 3 |
| [FastenerSystems](#fastenersystems) | 3 | 4 |
| [GearRelation3d](#gearrelation3d) | 13 | 11 |
| [GroundRelation3d](#groundrelation3d) | 11 | 6 |
| [Harness](#harness) | 6 | 4 |
| [Harnesses](#harnesses) | 3 | 4 |
| [ItemNumbers](#itemnumbers) | 3 | 5 |
| [Layout](#layout) | 11 | 1 |
| [Layouts](#layouts) | 4 | 4 |
| [LineSegment](#linesegment) | 5 | 5 |
| [LineSegments](#linesegments) | 3 | 3 |
| [Occurrence](#occurrence) | 71 | 51 |
| [Occurrences](#occurrences) | 6 | 14 |
| [Part](#part) | 23 | 12 |
| [Parts](#parts) | 3 | 5 |
| [Path](#path) | 10 | 12 |
| [PathRelation3d](#pathrelation3d) | 11 | 6 |
| [Paths](#paths) | 3 | 3 |
| [PhysicalProperties](#physicalproperties) | 9 | 9 |
| [Pipe](#pipe) | 7 | 5 |
| [Pipes](#pipes) | 3 | 3 |
| [PlanarRelation3d](#planarrelation3d) | 19 | 10 |
| [PointRelation3d](#pointrelation3d) | 16 | 10 |
| [Queries](#queries) | 5 | 7 |
| [Query](#query) | 8 | 5 |
| [Relations3d](#relations3d) | 3 | 16 |
| [RigidSetRelation3d](#rigidsetrelation3d) | 12 | 8 |
| [SegmentAngularRelation3d](#segmentangularrelation3d) | 12 | 4 |
| [SegmentDirectionRelation3d](#segmentdirectionrelation3d) | 8 | 4 |
| [SegmentDistanceRelation3d](#segmentdistancerelation3d) | 10 | 5 |
| [SegmentPointRelation3d](#segmentpointrelation3d) | 7 | 4 |
| [SegmentRadiusRelation3d](#segmentradiusrelation3d) | 10 | 3 |
| [SegmentRelations3d](#segmentrelations3d) | 3 | 8 |
| [Segments](#segments) | 3 | 2 |
| [SegmentTangentRelation3d](#segmenttangentrelation3d) | 7 | 4 |
| [SimplifiedAssemblies](#simplifiedassemblies) | 5 | 2 |
| [SimplifiedAssembly](#simplifiedassembly) | 6 | 5 |
| [Splice](#splice) | 15 | 10 |
| [Splices](#splices) | 4 | 3 |
| [StructuralFrame](#structuralframe) | 8 | 25 |
| [StructuralFrames](#structuralframes) | 3 | 8 |
| [SubassemblyBodies](#subassemblybodies) | 5 | 2 |
| [SubassemblyBody](#subassemblybody) | 12 | 2 |
| [SubOccurrence](#suboccurrence) | 47 | 24 |
| [SubOccurrences](#suboccurrences) | 3 | 2 |
| [TangentRelation3d](#tangentrelation3d) | 16 | 10 |
| [TopologyReference](#topologyreference) | 4 | 3 |
| [Tube](#tube) | 20 | 17 |
| [VirtualComponent](#virtualcomponent) | 11 | 5 |
| [VirtualComponentOccurrence](#virtualcomponentoccurrence) | 10 | 16 |
| [VirtualComponentOccurrences](#virtualcomponentoccurrences) | 3 | 5 |
| [Wire](#wire) | 26 | 12 |
| [WirePath](#wirepath) | 16 | 6 |
| [WirePathCableMembers](#wirepathcablemembers) | 3 | 2 |
| [WirePaths](#wirepaths) | 3 | 4 |
| [WirePathSegments](#wirepathsegments) | 3 | 2 |
| [WireRun](#wirerun) | 6 | 7 |
| [WireRunPaths](#wirerunpaths) | 3 | 2 |
| [WireRuns](#wireruns) | 3 | 4 |
| [Wires](#wires) | 4 | 3 |
| [Zone](#zone) | 4 | 11 |
| [Zones](#zones) | 3 | 10 |

### <a name="adjustablepart"></a>`AdjustablePart`

| Propriedade | Tipo | Acesso |
|-------------|------|--------|
| `AdjustToFit` | `Boolean` | get/set |
| `Application` | `Application` | get |
| `AttributeSets` | `Object` | get |
| `IsAttributeSetPresent[Name: String]` | `Boolean` | get |
| `Occurrence` | `Occurrence` | get |
| `Parent` | `Object` | get |

| Método | Retorno |
|--------|---------|
| `Delete()` | `void` |
| `GetAdjustables(NumberOfEntries: Int32, [out] PartVariables: Array, [out] AsmVariables: Array)` | `void` |
| `GetTube()` | `Tube` |
| `SetAdjustables(NumberOfEntries: Int32, [out] PartVariables: Array, [out] AsmVariables: Array)` | `void` |

### <a name="afgrooveweld"></a>`AFGrooveWeld`

| Propriedade | Tipo | Acesso |
|-------------|------|--------|
| `Application` | `Application` | get |
| `AssemblyBody` | `AssemblyBody` | get |
| `AttributeSets` | `Object` | get |
| `FeatureType` | `FeatureTypeConstants` | get |
| `IsAttributeSetPresent[Name: String]` | `Boolean` | get |
| `Name` | `String` | get/set |
| `Parent` | `Object` | get |
| `Status[[opt] [out] Description: Object]` | `FeatureStatusConstants` | get |
| `Suppress` | `Boolean` | get/set |
| `Visible` | `Boolean` | get/set |

| Método | Retorno |
|--------|---------|
| `AddSuppressionVariable()` | `SuppressVariable` |
| `Delete()` | `void` |
| `DeleteSuppressionVariable()` | `void` |
| `GetLabelWeldData([out] Object: Object)` | `void` |
| `GetSuppressionVariable()` | `SuppressVariable` |
| `GoTo()` | `void` |
| `HasSuppressionVariable()` | `Boolean` |
| `Recompute()` | `void` |

### <a name="afgroovewelds"></a>`AFGrooveWelds`

| Propriedade | Tipo | Acesso |
|-------------|------|--------|
| `Application` | `Application` | get |
| `Count` | `Int32` | get |
| `Parent` | `Object` | get |

| Método | Retorno |
|--------|---------|
| `GetEnumerator()` | `IEnumerator` |
| `Item(Index: Object)` | `AFGrooveWeld` |

### <a name="angularrelation3d"></a>`AngularRelation3d`

| Propriedade | Tipo | Acesso |
|-------------|------|--------|
| `Angle` | `Double` | get/set |
| `Application` | `Object` | get |
| `AttributeSets` | `Object` | get |
| `DetailedStatus` | `Relation3dDetailedStatusConstants` | get |
| `Index` | `Int32` | get |
| `IsAttributeSetPresent[Name: String]` | `Boolean` | get |
| `Occurrence1` | `Occurrence` | get |
| `Occurrence2` | `Occurrence` | get |
| `Parent` | `Object` | get |
| `Part1` | `Part` | get |
| `Part2` | `Part` | get |
| `RangedAngle` | `Boolean` | get/set |
| `RangeHigh` | `Double` | get/set |
| `RangeLow` | `Double` | get/set |
| `Status` | `Relation3dStatusConstants` | get |
| `Suppress` | `Boolean` | get/set |
| `Type` | `ObjectType` | get |

| Método | Retorno |
|--------|---------|
| `AddSuppressionVariable()` | `SuppressVariable` |
| `Delete()` | `void` |
| `DeleteSuppressionVariable()` | `void` |
| `GetDefinition([out] ToPlane1PositiveSide: Boolean, [out] FromPlane2PositiveSide: Boolean, [out] CounterClockWise: Boolean, [out] VectorX: Double, [out] VectorY: Double, [out] VectorZ: Double)` | `void` |
| `GetElement1([out] IsTopologyReference: Boolean)` | `Object` |
| `GetElement2([out] IsTopologyReference: Boolean)` | `Object` |
| `GetGeometry1([out] GeometryType: Int32, [out] PointX: Double, [out] PointY: Double, [out] PointZ: Double, [out] VectorX: Double, [out] VectorY: Double, [out] VectorZ: Double)` | `void` |
| `GetGeometry2([out] GeometryType: Int32, [out] PointX: Double, [out] PointY: Double, [out] PointZ: Double, [out] VectorX: Double, [out] VectorY: Double, [out] VectorZ: Double)` | `void` |
| `GetSuppressionVariable()` | `SuppressVariable` |
| `HasSuppressionVariable()` | `Boolean` |
| `Select(Replace: Boolean)` | `void` |

### <a name="arcsegment"></a>`ArcSegment`

| Propriedade | Tipo | Acesso |
|-------------|------|--------|
| `Application` | `Application` | get |
| `AttributeSets` | `Object` | get |
| `IsAttributeSetPresent[Name: String]` | `Boolean` | get |
| `Parent` | `Object` | get |
| `Radius` | `Double` | get/set |
| `SweepAngle` | `Double` | get/set |
| `Type` | `ObjectType` | get |

| Método | Retorno |
|--------|---------|
| `GetEndPoint([out] x: Double, [out] y: Double, [out] z: Double)` | `void` |
| `GetStartPoint([out] x: Double, [out] y: Double, [out] z: Double)` | `void` |
| `SetEndPoint(x: Double, y: Double, z: Double)` | `void` |
| `SetStartPoint(x: Double, y: Double, z: Double)` | `void` |

### <a name="arcsegments"></a>`ArcSegments`

| Propriedade | Tipo | Acesso |
|-------------|------|--------|
| `Application` | `Application` | get |
| `Count` | `Int32` | get |
| `Parent` | `Object` | get |

| Método | Retorno |
|--------|---------|
| `Add([ref] StartPoint: Array, [ref] EndPoint: Array, [ref] PlanePoint: Array)` | `ArcSegment` |
| `GetEnumerator()` | `IEnumerator` |
| `Item(Index: Object)` | `ArcSegment` |

### <a name="asmrefplane"></a>`AsmRefPlane`

| Propriedade | Tipo | Acesso |
|-------------|------|--------|
| `Application` | `Application` | get |
| `AttributeSets` | `Object` | get |
| `Global` | `Boolean` | get |
| `IsAttributeSetPresent[Name: String]` | `Boolean` | get |
| `Name` | `String` | get/set |
| `Parent` | `Object` | get |
| `ParentRelations` | `Array` | get |
| `Status[[opt] [out] Description: Object]` | `FeatureStatusConstants` | get |
| `Type` | `ObjectType` | get |
| `Visible` | `Boolean` | get/set |

| Método | Retorno |
|--------|---------|
| `AutoResizeRefPlane()` | `void` |
| `Delete()` | `void` |
| `GetNormal([out] Normal: Array)` | `void` |
| `GetReferenceDirection([out] RefDir: Array)` | `void` |
| `GetRootPoint([out] RootPoint: Array)` | `void` |
| `ResizeRefPlaneByDirDist(bAlongX: Boolean, idx: Int32, dist: Double)` | `void` |

### <a name="asmrefplanes"></a>`AsmRefPlanes`

| Propriedade | Tipo | Acesso |
|-------------|------|--------|
| `Application` | `Application` | get |
| `Count` | `Int32` | get |
| `Parent` | `Object` | get |
| `Visible` | `Boolean` | get/set |

| Método | Retorno |
|--------|---------|
| `AddAngularByAngle(ParentPlane: Object, Angle: Double, Pivot: Object, PivotOrigin: ReferenceElementConstants, [opt] NormalSide: Object, [opt] Local: Object)` | `AsmRefPlane` |
| `AddBy3Points(RootPoint: Object, SecondXAxisPoint: Object, ThirdPoint: Object)` | `AsmRefPlane` |
| `AddNormalToCurveAtArcLengthRatio(Parent: Object, OrientationPlane: Object, ArcLengthRatio: Double, XAxisRotation: Double, normalOrientation: ReferenceElementConstants, arcLengthRatioOrigin: ReferenceElementConstants)` | `AsmRefPlane` |
| `AddNormalToCurveAtDistanceAlongCurve(Parent: Object, OrientationPlane: Object, Distance: Double, XAxisRotation: Double, normalOrientation: ReferenceElementConstants, distanceOrigin: ReferenceElementConstants)` | `AsmRefPlane` |
| `AddNormalToCurveAtKeyPoint(Parent: Object, OrientationPlane: Object, Keypoint: Object, XAxisRotation: Double, normalOrientation: ReferenceElementConstants, selectedCurveEnd: ReferenceElementConstants)` | `AsmRefPlane` |
| `AddParallelByDistance(ParentPlane: Object, Distance: Double, [opt] NormalSide: Object, [opt] Pivot: Object, [opt] PivotOrigin: Object, [opt] Local: Object)` | `AsmRefPlane` |
| `GetEnumerator()` | `IEnumerator` |
| `Item(Index: Object)` | `AsmRefPlane` |

### <a name="assemblybodies"></a>`AssemblyBodies`

| Propriedade | Tipo | Acesso |
|-------------|------|--------|
| `Application` | `Application` | get |
| `AttributeSets` | `Object` | get |
| `Count` | `Int32` | get |
| `IsAttributeSetPresent[Name: String]` | `Boolean` | get |
| `Parent` | `Object` | get |
| `Type` | `ObjectType` | get |

| Método | Retorno |
|--------|---------|
| `GetEnumerator()` | `IEnumerator` |
| `Item(Index: Object)` | `AssemblyBody` |

### <a name="assemblybody"></a>`AssemblyBody`

| Propriedade | Tipo | Acesso |
|-------------|------|--------|
| `Application` | `Application` | get |
| `AssemblyBodyType` | `seAssemblyBodyTypeConstants` | get |
| `AttributeSets` | `Object` | get |
| `Body` | `Object` | get |
| `FaceStyle` | `Object` | get/set |
| `IsAttributeSetPresent[Name: String]` | `Boolean` | get |
| `Name` | `String` | get |
| `Parent` | `Object` | get |
| `Style` | `String` | get/set |
| `TopLevelDocument` | `AssemblyDocument` | get |
| `Type` | `ObjectType` | get |
| `Visible` | `Boolean` | get/set |

| Método | Retorno |
|--------|---------|
| `GetFaceStyle2(vbHonourPrefs: Boolean)` | `Object` |
| `GetReferenceKey([out] ReferenceKey: Array, [opt] [out] KeySize: Object)` | `void` |
| `Mirror(pPlane: Object)` | `void` |

### <a name="assemblycopies"></a>`AssemblyCopies`

| Propriedade | Tipo | Acesso |
|-------------|------|--------|
| `Application` | `Application` | get |
| `Count` | `Int32` | get |
| `Parent` | `Object` | get |

| Método | Retorno |
|--------|---------|
| `Add(FileName: String, SpecifiedComponentSelection: AssemblyCopyComponentConstants, [opt] MirrorPlane: Object, [opt] IncludeAF: Object, [opt] AddNewComponentsOnUpdate: Object, [opt] NumSpecifiedComponents: Object, [opt] SpecifiedComponents: Object, [opt] CoordinateSystem: Object)` | `AssemblyCopy` |
| `GetEnumerator()` | `IEnumerator` |
| `Item(Index: Object)` | `AssemblyCopy` |
| `ItemEx(Index: Object)` | `AssemblyCopy` |

### <a name="assemblycopy"></a>`AssemblyCopy`

| Propriedade | Tipo | Acesso |
|-------------|------|--------|
| `AddNewComponentsOnUpdate` | `Boolean` | get/set |
| `Application` | `Application` | get |
| `AssemblyCopyType` | `AssemblyCopyTypeConstants` | get |
| `CoordinateSystem` | `Object` | get/set |
| `IncludeAssemblyFeatures` | `Boolean` | get/set |
| `IsFrozen` | `Boolean` | get/set |
| `IsOutOfDate` | `Boolean` | get |
| `MirrorPlane` | `Object` | get/set |
| `Name` | `String` | get/set |
| `Parent` | `Object` | get |
| `SourceDocument` | `Object` | get |
| `SourceFilename` | `String` | get |
| `Status` | `AssemblyCopyStatusConstants` | get |

| Método | Retorno |
|--------|---------|
| `ChangeSource(FileName: String, SpecifiedComponentSelection: AssemblyCopyComponentConstants, [opt] NumSpecifiedComponents: Object, [opt] SpecifiedComponents: Object)` | `void` |
| `ComponentSettings(NumComponents: Int32, [out] Components: Array, [out] ActionConstants: Array, [opt] PlaneConstants: Object)` | `void` |
| `GetDefinition([out] inputOccs: Array, [out] outputOccs: Array, [opt] [out] actionEnums: Object, [opt] [out] planeEnums: Object, [opt] [out] userEnums: Object)` | `void` |
| `GetMultibodyDefinition([out] inputFileNames: Array, [out] inputReferences: Array, [out] outputOccs: Array)` | `void` |
| `Update()` | `void` |

### <a name="assemblydocument"></a>`AssemblyDocument`

| Propriedade | Tipo | Acesso |
|-------------|------|--------|
| `ActivateSimplifiedAssembly` | `Boolean` | get/set |
| `ActiveConfiguration` | `String` | get |
| `ActiveSketch` | `Object` | get |
| `AddInsStorage[Name: String, grfMode: Int32]` | `Object` | get |
| `Application` | `Application` | get |
| `ArcSegments` | `ArcSegments` | get |
| `AsmRefPlanes` | `AsmRefPlanes` | get |
| `AssemblyBodies` | `AssemblyBodies` | get |
| `AssemblyChangeEvents` | `Object` | get |
| `AssemblyConfigurationChangeEvents` | `Object` | get |
| `AssemblyCopies` | `AssemblyCopies` | get |
| `AssemblyDrivenPartFeatures` | `AssemblyDrivenPartFeatures` | get |
| `AssemblyFamilyEvents` | `Object` | get |
| `AssemblyFamilyEvents2` | `Object` | get |
| `AssemblyFamilyMembers` | `AssemblyFamilyMembers` | get |
| `AssemblyFeatures` | `AssemblyFeatures` | get |
| `AssemblyGroups` | `AssemblyGroups` | get |
| `AssemblyMirrors` | `AssemblyMirrors` | get |
| `AssemblyPatterns` | `AssemblyPatterns` | get |
| `AssemblyPhysicalPropertiesChangeEvents` | `Object` | get |
| `AssemblyProperties` | `AssemblyProperties` | get |
| `AssemblyRecomputeEvents` | `Object` | get |
| `AttributeQuery` | `AttributeQuery` | get |
| `Blocks` | `SketchBlocks` | get |
| `CapturedRelationshipCount` | `Int32` | get |
| `ComponentLayouts` | `ComponentLayouts` | get |
| `Configurations` | `Configurations` | get |
| `Constructions` | `Constructions` | get |
| `CoordinateSystems` | `CoordinateSystems` | get |
| `CreatedVersion` | `String` | get |
| `CurveSegments` | `CurveSegments` | get |
| `CutawaysCount` | `Int32` | get |
| `DimensionStyles` | `Object` | get |
| `Dirty` | `Boolean` | get/set |
| `DisplayName` | `String` | get |
| `DocumentEvents` | `Object` | get |
| `ExplosionsCount` | `Int32` | get |
| `FaceStyles` | `Object` | get |
| `FastenerSystems` | `FastenerSystems` | get |
| `FillStyles` | `Object` | get |
| `FullName` | `String` | get |
| `GeometricVersion` | `Int32` | get |
| `Harnesses` | `Harnesses` | get |
| `HasCapturedRelationships` | `Boolean` | get |
| `HatchPatternStyles` | `Object` | get |
| `HidePreviousLevel` | `Boolean` | get/set |
| `HighlightSets` | `HighlightSets` | get |
| `HoleDataCollection` | `HoleDataCollection` | get |
| `InPlaceActivated` | `Boolean` | get |
| `InterDocumentUpdate` | `InterDocumentUpdate` | get |
| `InterpartLinks` | `Object` | get |
| `IsAssemblySimplified` | `Boolean` | get |
| `IsFileAlternatePositionByDocument` | `Boolean` | get |
| `IsFileFamilyByDocument` | `Boolean` | get |
| `IsGeometricVersionDirty` | `Boolean` | get |
| `IsInsightFile` | `Boolean` | get |
| `IsMultiCADDriven` | `Boolean` | get |
| `IsSimplifyStateActive` | `Boolean` | get |
| `IsTemplate` | `Boolean` | get/set |
| `ItemNumbers` | `ItemNumbers` | get |
| `LabelWeldDataCollection` | `LabelWeldDataCollection` | get |
| `LastSavedVersion` | `String` | get |
| `Layers` | `Layers` | get |
| `Layouts` | `Layouts` | get |
| `LinearStyles` | `Object` | get |
| `LineSegments` | `LineSegments` | get |
| `Name` | `String` | get/set |
| `NamedViews` | `NamedViews` | get |
| `Occurrences` | `Occurrences` | get |
| `Parent` | `Application` | get |
| `Parts` | `Parts` | get |
| `Path` | `String` | get |
| `Paths` | `Paths` | get |
| `PhysicalProperties` | `PhysicalProperties` | get |
| `Pipes` | `Pipes` | get |
| `PMI` | `Object` | get |
| `ProfileUndoSteps` | `Int32` | get/set |
| `Properties` | `Object` | get |
| `Queries` | `Queries` | get |
| `ReadOnly` | `Boolean` | get/set |
| `Relations3d` | `Relations3d` | get |
| `RelationshipsSelectSet` | `SelectSet` | get |
| `RootStorage` | `Object` | get |
| `RoutingSlip` | `Object` | get |
| `SectionViews` | `SectionViews` | get |
| `SegmentRelations3d` | `SegmentRelations3d` | get |
| `Segments` | `Segments` | get |
| `SelectSet` | `SelectSet` | get |
| `Sensors` | `Sensors` | get |
| `SimplifiedAssemblies` | `SimplifiedAssemblies` | get |
| `Sketches3D` | `Sketches3D` | get |
| `Status` | `DocumentStatus` | get/set |
| `SteeringWheel` | `SteeringWheel` | get |
| `StructuralFrames` | `StructuralFrames` | get |
| `StudyOwner` | `StudyOwner` | get |
| `SummaryInfo` | `Object` | get |
| `TextCharStyles` | `Object` | get |
| `TextStyles` | `Object` | get |
| `Type` | `DocumentTypeConstants` | get |
| `UndoSteps` | `Int32` | get/set |
| `UnitsOfMeasure` | `UnitsOfMeasure` | get |
| `Variables` | `Object` | get |
| `ViewStyles` | `Object` | get |
| `VirtualComponentOccurrences` | `VirtualComponentOccurrences` | get |
| `WeldmentAssembly` | `Boolean` | get/set |
| `WeldmentAssemblyBeadDensity` | `Double` | get/set |
| `WeldmentAssemblyBeadMaterial` | `String` | get/set |
| `Windows` | `Windows` | get |
| `WirePaths` | `WirePaths` | get |
| `WireRuns` | `WireRuns` | get |
| `Zones` | `Zones` | get |

| Método | Retorno |
|--------|---------|
| `Activate()` | `void` |
| `ActivateAll()` | `void` |
| `AddMeasureVariable(Type: MeasureVariableTypeConstants, ValueType: MeasureVariableValueConstants, Geom1: Object, Geom2: Object, [opt] Geom3: Object)` | `MeasureVariable` |
| `BindKeyToObject([out] ReferenceKey: Array, [out] Object: Object)` | `void` |
| `BreakAllInterpartLinks()` | `void` |
| `BundleOccurrences([ref] OccurrencesToTransfer: Array, OccurrenceFileName: String, [opt] TemplateFileName: Object, [opt] Subassembly: Object)` | `Occurrence` |
| `CheckInterference(NumElementsSet1: Int32, [ref] Set1: Array, [out] Status: InterferenceStatusConstants, [opt] ComparisonMethod: Object, [opt] NumElementsSet2: Object, [opt] Set2: Object, [opt] AddInterferenceAsOccurrence: Object, [opt] ReportFilename: Object, [opt] ReportType: Object, [opt] [out] NumInterferences: Object, [opt] [out] InterferingPartsSet1: Object, [opt] [out] InterferingPartsOtherSet: Object, [opt] [out] ConfirmedInterference: Object, [opt] [out] InterferenceOccurrence: Object, [opt] IgnoreThreadInterferences: Object)` | `void` |
| `CheckInterference2(NumElementsSet1: Int32, [ref] Set1: Array, [out] Status: InterferenceStatusConstants, [opt] ComparisonMethod: Object, [opt] NumElementsSet2: Object, [opt] Set2: Object, [opt] AddInterferenceAsOccurrence: Object, [opt] ReportFilename: Object, [opt] ReportType: Object, [opt] [out] NumInterferences: Object, [opt] [out] InterferingPartsSet1: Object, [opt] [out] InterferingPartsOtherSet: Object, [opt] [out] ConfirmedInterference: Object, [opt] [out] InterferenceOccurrence: Object, [opt] IgnoreSameNominalDiaConstant: Object, [opt] IgnoreNonThreadVsThreadConstant: Object)` | `void` |
| `CheckInterference3(NumElementsSet1: Int32, [ref] Set1: Array, [out] Status: InterferenceStatusConstants, [opt] ComparisonMethod: Object, [opt] NumElementsSet2: Object, [opt] Set2: Object, [opt] AddInterferenceAsOccurrence: Object, [opt] ReportFilename: Object, [opt] ReportType: Object, [opt] [out] NumInterferences: Object, [opt] [out] InterferingPartsSet1: Object, [opt] [out] InterferingPartsOtherSet: Object, [opt] [out] ConfirmedInterference: Object, [opt] [out] InterferenceOccurrence: Object, [opt] IgnoreSameNominalDiaConstant: Object, [opt] IgnoreNonThreadVsThreadConstant: Object, [opt] bAllowConstructions: Boolean)` | `void` |
| `Close([opt] SaveChanges: Object, [opt] FileName: Object, [opt] RouteWorkbook: Object)` | `void` |
| `CopySketch(SourceLayoutOrSketch: Object, TargetPartOrAssembly: Object, bAssociativeCopy: Boolean, [opt] [out] pvCopySketchErrorStatus: Object)` | `void` |
| `CreateCloneComponents([ref] ComponentsToClone: Array, [ref] ReferenceGeometryFaces: Array, [ref] CloneEnviornment: Array, CloneOptions: CloneComponentOptions, bCreateGroup: Boolean, CloneMatchType: CloneMatchTypeOptions, [out] ErrorStatus: Int32)` | `void` |
| `CreateFamilyFile(FirstMemberName: String, SecondMemberName: String, bCreateAlternatePosition: Boolean)` | `void` |
| `CreateOrEditSimplifiedAssembly(NumOccurrenceExclude: Int32, [ref] OccurrenceExclude: Array, dExcludeRangeRatio: Double, NumOccurrenceInclude: Int32, [ref] OccurrenceInclude: Array)` | `void` |
| `CreatePreview()` | `void` |
| `CreateReference(Occurrence: Occurrence, Entity: Object)` | `Object` |
| `CreateReference2(Object: Object, Entity: Object)` | `Object` |
| `DeleteSimplifiedAssembly()` | `void` |
| `DeleteStructureCache()` | `void` |
| `DisperseSubassembly(Subassembly: Object, bAllOccurrences: Boolean)` | `void` |
| `EditProperties()` | `void` |
| `FreezeAllInterpartLinks()` | `void` |
| `GenerateWireHarnessReport(ReportType: AssemblyWireHarnessReportTypeConstants, [opt] FileNameToSaveReport: Object, [opt] Selection: Object, [opt] Window: Object, [opt] NumberOfReportProperties: Object, [opt] AssemblyReportProperties: Object, [opt] NumberOfSortItems: Object, [opt] SortProperties: Object, [opt] SortOrder: Object, [opt] Justification: Object, [opt] UpdateTemplate: Object)` | `void` |
| `GenerateWireReport(ReportType: AssemblyReportTypeConstants, FileNameToSaveReport: Object, FieldsToGenerate: Int32)` | `void` |
| `Get3dPrintInfo([out] iNumberOfTriangles: Int32, [out] iNumberOfPoints: Int32, [out] iNumberOfEdges: Int32, [out] dMeshSurfaceArea: Double, [out] dMeshVolume: Double, [out] dFileSize: Double, [out] dExportToleranceValue: Double, [out] dSurfacePlaneAngTol: Double, [opt] Type: Print3DFileType)` | `void` |
| `GetBaseStyle(BaseStyleType: AssemblyBaseStylesConstants, [out] BaseStyle: FaceStyle)` | `void` |
| `GetCapturedRelationshipInformation([out] RelationshipTypes: Array, [out] OffsetTypes: Array, [out] Offsets: Array, [out] Faces: Object)` | `void` |
| `GetContainerDocumentAndMatrixOfIPADoc([out] ContainerDocument: Object, [out] Matrix: Array)` | `void` |
| `GetContainerDocumentAndOccurrenceOfIPADoc([out] ContainerDocument: Object, [out] IPAOccurrence: Object)` | `void` |
| `GetDrivenDrivingInfo(Element: Object, [out] DrivenOccurrencesArray: Object, [out] DrivingOccurrencesArray: Object)` | `void` |
| `GetGlobalParameter(Parameter: AssemblyGlobalConstants, [out] Value: Object)` | `void` |
| `GetInContextAssemblyNameForInterpartLinks([ref] pbstrAssemblyName: String)` | `void` |
| `GetRegisteredCustomPropertiesBiDM([out] varPropInfo: Object)` | `void` |
| `GetSimplifiedAssemblyInputs([out] NumOccurrenceExclude: Int32, [out] OccurrenceExclude: Array, [out] dExcludeRangeRatio: Double, [out] NumOccurrenceInclude: Int32, [out] OccurrenceInclude: Array)` | `void` |
| `GetTopDocumentAndSubOccurrenceOfIPADoc([out] TopDocument: Object, [out] IPASubOccurrence: Object)` | `void` |
| `GoalSeek(NameTargetVariable: String, NameVariableToChange: String, dTargetValue: Double, dTimeLimitInSecs: Double, NumIterationsLimit: Int32, [out] dTimeElapsed: Double, [out] NumIterations: Int32, [out] TimeLimitExceeded: Boolean, [out] IterationsLimitExceeded: Boolean)` | `void` |
| `HasInterpartLinks([ref] pbHasInterpartLinks: Boolean)` | `void` |
| `ImportStyles(FileName: String, [opt] Overwrite: Object)` | `void` |
| `ImportStyles2(StyleType: seStyleTypeConstants, bReplace: Boolean, pSrcDocument: Object)` | `void` |
| `InquireElement(Element: Object, [ref] InPoint: Array, CoordinateSystem: Object, [out] Point: Array, [out] SurfaceArea: Double, [out] Volume: Double, [out] Length: Double)` | `void` |
| `LoadUOMPreferences(UpdateUomGlobals: Boolean)` | `void` |
| `MeasureAngle(Element1: Object, Element2: Object, [out] TrueAngle: Double, [out] ApparentAngle: Double, [opt] Element3: Object)` | `void` |
| `MeasureAngleEx(Element1: Object, Element2: Object, Element3: Object, [out] Angle1: Double, [out] Angle2: Double, [out] Angle3: Double, [out] Angle4: Double)` | `void` |
| `MeasureDistance(Element1: Object, Element2: Object, DistanceType: MeasureDistanceTypeConstants, [out] Distance: Double, [out] DX: Double, [out] DY: Double, [out] DZ: Double, [out] Point1: Array, [out] Point2: Array)` | `void` |
| `MinimumDistance(Element1: Object, Element2: Object, [out] Distance: Double, [out] Point1: Array, [out] Point2: Array)` | `void` |
| `NewNavigatorWindow()` | `void` |
| `NewWindow([opt] NewWindowOptions: Object, [opt] Environment: Object)` | `Object` |
| `NormalDistance(Element1: Object, Element2: Object, [out] TrueDistance: Double, [out] ApparentDistance: Double, [out] DeltaX: Double, [out] DeltaY: Double, [out] DeltaZ: Double, [opt] CoordinateSystem: Object)` | `void` |
| `PathfinderScrollToSelection()` | `void` |
| `PMI_ByModelState([out] PMIObj: Object, [opt] PMIModelState: PMIModelStateConstants)` | `void` |
| `PrintOut([opt] Printer: Object, [opt] NumCopies: Object, [opt] Orientation: Object, [opt] PaperSize: Object, [opt] Scale: Object, [opt] PrintToFile: Object, [opt] OutputFileName: Object, [opt] PrintRange: Object, [opt] Sheets: Object, [opt] ColorAsBlack: Object, [opt] Collate: Object)` | `void` |
| `PublishVirtualComponents()` | `void` |
| `PublishVirtualComponentsBIDM(ListOfDocumentNumbers: Object, ListOfRevisionIDs: Object, [opt] ListOfTitles: Object)` | `void` |
| `Range([out] x_min: Double, [out] y_min: Double, [out] z_min: Double, [out] x_max: Double, [out] y_max: Double, [out] z_max: Double)` | `void` |
| `ReplaceComponents([ref] TargetComponents: Array, ReplacementFilePath: String, ConstraintReplacementOption: ConstraintReplacementConstants)` | `void` |
| `ReviseBIDM(FilePath: String, Revision: String, Title: String)` | `String` |
| `ReviseWithCustomPropertiesBIDM(FilePath: String, Revision: String, Title: String, varPropInfo: Object)` | `String` |
| `RotateMultipleOccurrences(lNumberOfOccurrences: Int32, [ref] Occurrences: Array, MoveType: MoveMultipleMoveTypeConstants, RelationshipMaintenance: MoveMultipleRelationshipConstants, AxisX: Double, AxisY: Double, AxisZ: Double, Angle: Double, [out] MovedOrCopiedOccurrences: Object)` | `void` |
| `Save()` | `void` |
| `SaveAs(NewName: String, [opt] IsATemplate: Object, [opt] FileFormat: Object, [opt] ReadOnlyEnforced: Object, [opt] ReadOnlyRecommended: Object, [opt] NewStatus: Object, [opt] CreateBackup: Object, [opt] UpdateLinkInContainer: Object, [opt] UpdateAllLinksInContainer: Object)` | `void` |
| `SaveAsBIDM(FilePath: String, DocumentNumber: String, Revision: String, Title: String)` | `String` |
| `SaveAsJT(NewName: String, [opt] Include_PreciseGeom: Object, [opt] Prod_Structure_Option: Object, [opt] Export_PMI: Object, [opt] Export_CoordinateSystem: Object, [opt] Export_3DBodies: Object, [opt] NumberofLODs: Object, [opt] JTFileUnit: Object, [opt] Write_Which_Files: Object, [opt] Use_Simplified_TopAsm: Object, [opt] Use_Simplified_SubAsm: Object, [opt] Use_Simplified_Part: Object, [opt] EnableDefaultOutputPath: Object, [opt] IncludeSEProperties: Object, [opt] Export_VisiblePartsOnly: Object, [opt] Export_VisibleConstructionsOnly: Object, [opt] RemoveUnsafeCharacters: Object, [opt] ExportSEPartFileAsSingleJTFile: Object)` | `void` |
| `SaveAsWithCustomPropertiesBIDM(FilePath: String, DocumentNumber: String, Revision: String, Title: String, varPropInfo: Object)` | `String` |
| `SaveCopyAs(Name: String)` | `void` |
| `SaveModelAs(Occurrence: Object, SaveFileName: String)` | `void` |
| `SaveModelAsBiDM(Occurrence: Object, FilePath: String, DocumentNumber: String, Revision: String, Title: String)` | `String` |
| `SaveSimplifiedAssemblyAs(SaveFileName: String)` | `void` |
| `SeekReadOnlyAccess([out] ReadOnlyAccess: Boolean)` | `void` |
| `SeekWriteAccess([out] WriteAccess: Boolean)` | `void` |
| `SendMail([opt] Recipients: Object, [opt] Subject: Object, [opt] ReturnReceipt: Object)` | `void` |
| `SetBaseStyle(BaseStyleType: AssemblyBaseStylesConstants, BaseStyle: FaceStyle)` | `void` |
| `SetGlobalParameter(Parameter: AssemblyGlobalConstants, Value: Object)` | `void` |
| `ThawAllInterpartLinks()` | `void` |
| `TransferOccurrences([ref] OccurrencesToTransfer: Array, [opt] Subassembly: Object)` | `void` |
| `TranslateMultipleOccurrences(lNumberOfOccurrences: Int32, [ref] Occurrences: Array, MoveType: MoveMultipleMoveTypeConstants, RelationshipMaintenance: MoveMultipleRelationshipConstants, FromX: Double, FromY: Double, FromZ: Double, ToX: Double, ToY: Double, ToZ: Double, [out] MovedOrCopiedOccurrences: Object)` | `void` |
| `UpdateAll()` | `void` |
| `UpdateDocument([opt] FutureUse1: Object, [opt] FutureUse2: Object)` | `void` |
| `UpdatePathfinder(UpdateType: AssemblyPathfinderUpdateConstants)` | `void` |
| `UpdateSimplifiedAssembly()` | `void` |
| `UpdateStructureCache(UpdateType: UpdateStructureCacheConstants)` | `void` |

### <a name="assemblydrivenpartfeatures"></a>`AssemblyDrivenPartFeatures`

| Propriedade | Tipo | Acesso |
|-------------|------|--------|
| `Application` | `Application` | get |
| `AssemblyDrivenPartFeaturesExtrudedCutouts` | `AssemblyDrivenPartFeaturesExtrudedCutouts` | get |
| `AssemblyDrivenPartFeaturesHoles` | `AssemblyDrivenPartFeaturesHoles` | get |
| `AssemblyDrivenPartFeaturesRevolvedCutouts` | `AssemblyDrivenPartFeaturesRevolvedCutouts` | get |
| `Parent` | `Object` | get |

### <a name="assemblydrivenpartfeaturesextrudedcutout"></a>`AssemblyDrivenPartFeaturesExtrudedCutout`

| Propriedade | Tipo | Acesso |
|-------------|------|--------|
| `Application` | `Application` | get |
| `Edges[EdgeType: FeatureTopologyQueryTypeConstants]` | `Object` | get |
| `Faces[FaceType: FeatureTopologyQueryTypeConstants]` | `Object` | get |
| `FacesByRay[Xorigin: Double, Yorigin: Double, Zorigin: Double, Xdir: Double, Ydir: Double, Zdir: Double]` | `Object` | get |
| `Name` | `String` | get/set |
| `Parent` | `Object` | get |
| `Status[[opt] [out] Description: Object]` | `FeatureStatusConstants` | get |
| `Suppress` | `Boolean` | get/set |

| Método | Retorno |
|--------|---------|
| `Delete()` | `void` |
| `GetDepth()` | `Double` |
| `GetExtentSide()` | `FeaturePropertyConstants` |
| `GetExtentType()` | `FeaturePropertyConstants` |
| `GetFromPlane()` | `Object` |
| `GetPartFeatures([out] NumFeatures: Int32, [out] ppFeatures: Array)` | `void` |
| `GetProfiles([out] NumProfiles: Int32, [out] ppProfiles: Array)` | `void` |
| `GetProfileSide()` | `FeaturePropertyConstants` |
| `GetScopeParts([out] NumScopeParts: Int32, [out] pScopeParts: Array)` | `void` |
| `GetToPlane()` | `Object` |
| `Range([out] X1: Double, [out] Y1: Double, [out] Z1: Double, [out] X2: Double, [out] Y2: Double, [out] Z2: Double)` | `void` |
| `RollToFeature()` | `void` |
| `SetDepth(nDepth: Double)` | `void` |
| `SetExtentSide(ExtentSide: FeaturePropertyConstants)` | `void` |
| `SetExtentType(ExtentType: FeaturePropertyConstants)` | `void` |
| `SetFromPlane(FromPlane: Object)` | `void` |
| `SetProfiles(NumProfiles: Int32, pProfiles: Array)` | `void` |
| `SetProfileSide(profileSide: FeaturePropertyConstants)` | `void` |
| `SetScopeParts(NumScopeParts: Int32, pScopeParts: Array)` | `void` |
| `SetToPlane(ToPlane: Object)` | `void` |

### <a name="assemblydrivenpartfeaturesextrudedcutouts"></a>`AssemblyDrivenPartFeaturesExtrudedCutouts`

| Propriedade | Tipo | Acesso |
|-------------|------|--------|
| `Application` | `Application` | get |
| `Count` | `Int32` | get |
| `Parent` | `Object` | get |

| Método | Retorno |
|--------|---------|
| `Add(nNumScopeParts: Int32, [ref] pScopeParts: Array, nNumProfiles: Int32, [ref] pProfiles: Array, ExtentType: FeaturePropertyConstants, [ref] pExtentSide: FeaturePropertyConstants, profileSide: FeaturePropertyConstants, [ref] pdDistance: Double, pKeyPoint: Object, [ref] pKeyPointFlags: KeyPointExtentConstants, pFromSurfOrPlane: Object, pToSurfOrPlane: Object)` | `AssemblyDrivenPartFeaturesExtrudedCutout` |
| `GetEnumerator()` | `IEnumerator` |
| `Item(Index: Object)` | `AssemblyDrivenPartFeaturesExtrudedCutout` |

### <a name="assemblydrivenpartfeatureshole"></a>`AssemblyDrivenPartFeaturesHole`

| Propriedade | Tipo | Acesso |
|-------------|------|--------|
| `Application` | `Application` | get |
| `Edges[EdgeType: FeatureTopologyQueryTypeConstants]` | `Object` | get |
| `Faces[FaceType: FeatureTopologyQueryTypeConstants]` | `Object` | get |
| `FacesByRay[Xorigin: Double, Yorigin: Double, Zorigin: Double, Xdir: Double, Ydir: Double, Zdir: Double]` | `Object` | get |
| `Name` | `String` | get/set |
| `Parent` | `Object` | get |
| `Status[[opt] [out] Description: Object]` | `FeatureStatusConstants` | get |
| `Suppress` | `Boolean` | get/set |

| Método | Retorno |
|--------|---------|
| `Delete()` | `void` |
| `GetDepth()` | `Double` |
| `GetExtentSide()` | `FeaturePropertyConstants` |
| `GetExtentType()` | `FeaturePropertyConstants` |
| `GetFromPlane()` | `Object` |
| `GetHoleData()` | `Object` |
| `GetPartFeatures([out] NumFeatures: Int32, [out] ppFeatures: Array)` | `void` |
| `GetProfiles([out] NumProfiles: Int32, [out] ppProfiles: Array)` | `void` |
| `GetScopeParts([out] NumScopeParts: Int32, [out] pScopeParts: Array)` | `void` |
| `GetToPlane()` | `Object` |
| `Range([out] X1: Double, [out] Y1: Double, [out] Z1: Double, [out] X2: Double, [out] Y2: Double, [out] Z2: Double)` | `void` |
| `RollToFeature()` | `void` |
| `SetDepth(nDepth: Double)` | `void` |
| `SetExtentSide(ExtentSide: FeaturePropertyConstants)` | `void` |
| `SetExtentType(ExtentType: FeaturePropertyConstants)` | `void` |
| `SetFromPlane(FromPlane: Object)` | `void` |
| `SetHoleData(Holedata: Object)` | `void` |
| `SetProfiles(NumProfiles: Int32, pProfiles: Array)` | `void` |
| `SetScopeParts(NumScopeParts: Int32, pScopeParts: Array)` | `void` |
| `SetToPlane(ToPlane: Object)` | `void` |

### <a name="assemblydrivenpartfeaturesholes"></a>`AssemblyDrivenPartFeaturesHoles`

| Propriedade | Tipo | Acesso |
|-------------|------|--------|
| `Application` | `Application` | get |
| `Count` | `Int32` | get |
| `Parent` | `Object` | get |

| Método | Retorno |
|--------|---------|
| `Add(nNumScopeParts: UInt32, [ref] pScopeParts: Array, nNumProfiles: UInt32, [ref] pProfiles: Array, [ref] pExtentSide: FeaturePropertyConstants, pHoledata: Object, ExtentType: FeaturePropertyConstants, [ref] pHoleDepth: Double, pFromSurfOrPlane: Object, pToSurfOrPlane: Object, pKeyPoint: Object, [ref] pKeyPointFlags: KeyPointExtentConstants)` | `AssemblyDrivenPartFeaturesHole` |
| `GetEnumerator()` | `IEnumerator` |
| `Item(Index: Object)` | `AssemblyDrivenPartFeaturesHole` |

### <a name="assemblydrivenpartfeaturesrevolvedcutout"></a>`AssemblyDrivenPartFeaturesRevolvedCutout`

| Propriedade | Tipo | Acesso |
|-------------|------|--------|
| `Application` | `Application` | get |
| `Edges[EdgeType: FeatureTopologyQueryTypeConstants]` | `Object` | get |
| `Faces[FaceType: FeatureTopologyQueryTypeConstants]` | `Object` | get |
| `FacesByRay[Xorigin: Double, Yorigin: Double, Zorigin: Double, Xdir: Double, Ydir: Double, Zdir: Double]` | `Object` | get |
| `Name` | `String` | get/set |
| `Parent` | `Object` | get |
| `Status[[opt] [out] Description: Object]` | `FeatureStatusConstants` | get |
| `Suppress` | `Boolean` | get/set |

| Método | Retorno |
|--------|---------|
| `Delete()` | `void` |
| `GetAngle()` | `Double` |
| `GetExtentSide()` | `FeaturePropertyConstants` |
| `GetExtentType()` | `FeaturePropertyConstants` |
| `GetPartFeatures([out] NumFeatures: Int32, [out] ppFeatures: Array)` | `void` |
| `GetProfiles([out] NumProfiles: Int32, [out] ppProfiles: Array)` | `void` |
| `GetProfileSide()` | `FeaturePropertyConstants` |
| `GetReferenceAxis()` | `Object` |
| `GetScopeParts([out] NumScopeParts: Int32, [out] pScopeParts: Array)` | `void` |
| `Range([out] X1: Double, [out] Y1: Double, [out] Z1: Double, [out] X2: Double, [out] Y2: Double, [out] Z2: Double)` | `void` |
| `RollToFeature()` | `void` |
| `SetAngle(nAngle: Double)` | `void` |
| `SetExtentSide(ExtentSide: FeaturePropertyConstants)` | `void` |
| `SetExtentType(ExtentType: FeaturePropertyConstants)` | `void` |
| `SetProfiles(NumProfiles: Int32, pProfiles: Array)` | `void` |
| `SetProfileSide(profileSide: FeaturePropertyConstants)` | `void` |
| `SetReferenceAxis(ReferenceAxis: Object)` | `void` |
| `SetScopeParts(NumScopeParts: Int32, pScopeParts: Array)` | `void` |

### <a name="assemblydrivenpartfeaturesrevolvedcutouts"></a>`AssemblyDrivenPartFeaturesRevolvedCutouts`

| Propriedade | Tipo | Acesso |
|-------------|------|--------|
| `Application` | `Application` | get |
| `Count` | `Int32` | get |
| `Parent` | `Object` | get |

| Método | Retorno |
|--------|---------|
| `Add(nNumScopeParts: Int32, [ref] pScopeParts: Array, nNumProfiles: Int32, [ref] pProfiles: Array, pRefAxis: Object, ExtentType: FeaturePropertyConstants, ExtentSide: FeaturePropertyConstants, profileSide: FeaturePropertyConstants, [ref] pdAngle: Double, KeyPointOrTangentFace: Object, [ref] KeyPointFlags: KeyPointExtentConstants, pFromSurface: Object, pToSurface: Object)` | `AssemblyDrivenPartFeaturesRevolvedCutout` |
| `GetEnumerator()` | `IEnumerator` |
| `Item(Index: Object)` | `AssemblyDrivenPartFeaturesRevolvedCutout` |

### <a name="assemblyfamilymember"></a>`AssemblyFamilyMember`

| Propriedade | Tipo | Acesso |
|-------------|------|--------|
| `Application` | `Application` | get |
| `MemberName` | `String` | get |
| `MemberProperties[PropertyID: AssemblyFamilyMemberPropertyConstants]` | `String` | get |
| `Parent` | `Object` | get |
| `Properties` | `Object` | get |

| Método | Retorno |
|--------|---------|
| `AddOccurrence(Occurrence: Object, MemberName: String)` | `void` |
| `ExcludeObject(ObjectToExclude: Object)` | `void` |
| `IncludeObject(ObjectToInclude: Object)` | `void` |
| `RemoveExcludedAttachment(AttachmentToRemove: Object)` | `void` |
| `RemoveExcludedRelationship(ConstraintToRemove: Object)` | `void` |
| `SetMemberProperties(PropertyID: AssemblyFamilyMemberPropertyConstants, PropertyString: String)` | `void` |
| `SetVariableValue(Variable: Object, ValueToSet: Double)` | `void` |

### <a name="assemblyfamilymembers"></a>`AssemblyFamilyMembers`

| Propriedade | Tipo | Acesso |
|-------------|------|--------|
| `ActiveMember` | `String` | get |
| `AlternateAssemblyType` | `AlternateAssemblyTypeConstants` | get |
| `Application` | `Application` | get |
| `Count` | `Int32` | get |
| `GlobalEditMode` | `Boolean` | get/set |
| `Parent` | `Object` | get |
| `Variable[Index: Int32]` | `Object` | get |
| `VariableCount` | `Int32` | get |

| Método | Retorno |
|--------|---------|
| `ActivateMember(MemberName: String)` | `void` |
| `AddVariable(Variable: Object)` | `void` |
| `DeleteMember(MemberName: String)` | `void` |
| `GetEnumerator()` | `IEnumerator` |
| `Item(Index: Object)` | `AssemblyFamilyMember` |
| `NewMember(MemberName: String)` | `AssemblyFamilyMember` |
| `RemoveVariable(Variable: Object)` | `void` |
| `RenameMember(CurrentMemberName: String, NewMemberName: String)` | `void` |
| `SaveAllMembers(ChangeGeometricVersion: Boolean)` | `void` |
| `SaveMember(MemberName: String, FileNameWithPath: String)` | `void` |
| `SaveMemberBiDM(MemberName: String, FileNameWithPath: String, DocumentNumber: String, Revision: String, Title: String)` | `String` |

### <a name="assemblyfeatures"></a>`AssemblyFeatures`

| Propriedade | Tipo | Acesso |
|-------------|------|--------|
| `Application` | `Application` | get |
| `AssemblyFeaturesExtrudedCutouts` | `AssemblyFeaturesExtrudedCutouts` | get |
| `AssemblyFeaturesHoles` | `AssemblyFeaturesHoles` | get |
| `AssemblyFeaturesMirrors` | `AssemblyFeaturesMirrors` | get |
| `AssemblyFeaturesPatterns` | `AssemblyFeaturesPatterns` | get |
| `AssemblyFeaturesRevolvedCutouts` | `AssemblyFeaturesRevolvedCutouts` | get |
| `AssemblyFeaturesSweptProtrusions` | `AssemblyFeaturesSweptProtrusions` | get |
| `ExtrudedProtrusions` | `AssemblyFeaturesExtrudedProtrusions` | get |
| `FilletWelds` | `AssemblyFilletWelds` | get |
| `GrooveWelds` | `AFGrooveWelds` | get |
| `LabelWelds` | `AssemblyLabelWelds` | get |
| `Parent` | `Object` | get |
| `RevolvedProtrusions` | `AssemblyFeaturesRevolvedProtrusions` | get |
| `StitchWelds` | `AssemblyStitchWelds` | get |
| `Threads` | `AssemblyThreads` | get |

| Método | Retorno |
|--------|---------|
| `Recompute(options: Int32)` | `void` |

### <a name="assemblyfeaturesextrudedcutout"></a>`AssemblyFeaturesExtrudedCutout`

| Propriedade | Tipo | Acesso |
|-------------|------|--------|
| `Application` | `Application` | get |
| `Edges[EdgeType: FeatureTopologyQueryTypeConstants]` | `Object` | get |
| `EdgesForScopePart[pScopePart: Object, EdgeType: FeatureTopologyQueryTypeConstants]` | `Object` | get |
| `Faces[FaceType: FeatureTopologyQueryTypeConstants]` | `Object` | get |
| `FacesByRay[Xorigin: Double, Yorigin: Double, Zorigin: Double, Xdir: Double, Ydir: Double, Zdir: Double]` | `Object` | get |
| `FacesByRayForScopePart[pScopePart: Object, Xorigin: Double, Yorigin: Double, Zorigin: Double, Xdir: Double, Ydir: Double, Zdir: Double]` | `Object` | get |
| `FacesForScopePart[ScopePart: Object, FaceType: FeatureTopologyQueryTypeConstants]` | `Object` | get |
| `FeatureType` | `FeatureTypeConstants` | get |
| `Name` | `String` | get/set |
| `Parent` | `Object` | get |
| `Status[[opt] [out] Description: Object]` | `FeatureStatusConstants` | get |
| `Suppress` | `Boolean` | get/set |

| Método | Retorno |
|--------|---------|
| `AddSuppressionVariable()` | `SuppressVariable` |
| `Delete()` | `void` |
| `DeleteSuppressionVariable()` | `void` |
| `GetDepth()` | `Double` |
| `GetExtentSide()` | `FeaturePropertyConstants` |
| `GetExtentType()` | `FeaturePropertyConstants` |
| `GetFromPlane()` | `Object` |
| `GetProfiles([out] NumProfiles: Int32, [out] ppProfiles: Array)` | `void` |
| `GetProfileSide()` | `FeaturePropertyConstants` |
| `GetScopeParts([out] NumScopeParts: Int32, [out] pScopeParts: Array)` | `void` |
| `GetSuppressionVariable()` | `SuppressVariable` |
| `GetToPlane()` | `Object` |
| `HasSuppressionVariable()` | `Boolean` |
| `Range([out] X1: Double, [out] Y1: Double, [out] Z1: Double, [out] X2: Double, [out] Y2: Double, [out] Z2: Double)` | `void` |
| `RollToFeature()` | `void` |
| `SetDepth(nDepth: Double)` | `void` |
| `SetExtentSide(ExtentSide: FeaturePropertyConstants)` | `void` |
| `SetExtentType(ExtentType: FeaturePropertyConstants)` | `void` |
| `SetFromPlane(FromPlane: Object)` | `void` |
| `SetProfiles(NumProfiles: Int32, pProfiles: Array)` | `void` |
| `SetProfileSide(profileSide: FeaturePropertyConstants)` | `void` |
| `SetScopeParts(NumScopeParts: Int32, pScopeParts: Array)` | `void` |
| `SetToPlane(ToPlane: Object)` | `void` |

### <a name="assemblyfeaturesextrudedcutouts"></a>`AssemblyFeaturesExtrudedCutouts`

| Propriedade | Tipo | Acesso |
|-------------|------|--------|
| `Application` | `Application` | get |
| `Count` | `Int32` | get |
| `Parent` | `Object` | get |

| Método | Retorno |
|--------|---------|
| `Add(nNumScopeParts: Int32, [ref] pScopeParts: Array, nNumProfiles: Int32, [ref] pProfiles: Array, ExtentType: FeaturePropertyConstants, [ref] pExtentSide: FeaturePropertyConstants, profileSide: FeaturePropertyConstants, [ref] pdDistance: Double, pKeyPoint: Object, [ref] pKeyPointFlags: KeyPointExtentConstants, pFromSurfOrPlane: Object, pToSurfOrPlane: Object)` | `AssemblyFeaturesExtrudedCutout` |
| `GetEnumerator()` | `IEnumerator` |
| `Item(Index: Object)` | `AssemblyFeaturesExtrudedCutout` |

### <a name="assemblyfeaturesextrudedprotrusion"></a>`AssemblyFeaturesExtrudedProtrusion`

| Propriedade | Tipo | Acesso |
|-------------|------|--------|
| `Application` | `Application` | get |
| `AssemblyBody` | `AssemblyBody` | get |
| `Edges[EdgeType: FeatureTopologyQueryTypeConstants]` | `Object` | get |
| `Faces[FaceType: FeatureTopologyQueryTypeConstants]` | `Object` | get |
| `FacesByRay[Xorigin: Double, Yorigin: Double, Zorigin: Double, Xdir: Double, Ydir: Double, Zdir: Double]` | `Object` | get |
| `FeatureType` | `FeatureTypeConstants` | get |
| `Name` | `String` | get/set |
| `Parent` | `Object` | get |
| `Status[[opt] [out] Description: Object]` | `FeatureStatusConstants` | get |
| `Suppress` | `Boolean` | get/set |

| Método | Retorno |
|--------|---------|
| `AddSuppressionVariable()` | `SuppressVariable` |
| `Delete()` | `void` |
| `DeleteSuppressionVariable()` | `void` |
| `GetDepth()` | `Double` |
| `GetExtentSide()` | `FeaturePropertyConstants` |
| `GetExtentType()` | `FeaturePropertyConstants` |
| `GetFromPlane()` | `Object` |
| `GetProfiles([out] NumProfiles: Int32, [out] ppProfiles: Array)` | `void` |
| `GetProfileSide()` | `FeaturePropertyConstants` |
| `GetSuppressionVariable()` | `SuppressVariable` |
| `GetToPlane()` | `Object` |
| `HasSuppressionVariable()` | `Boolean` |
| `Range([out] X1: Double, [out] Y1: Double, [out] Z1: Double, [out] X2: Double, [out] Y2: Double, [out] Z2: Double)` | `void` |
| `RollToFeature()` | `void` |
| `SetDepth(nDepth: Double)` | `void` |
| `SetExtentSide(ExtentSide: FeaturePropertyConstants)` | `void` |
| `SetExtentType(ExtentType: FeaturePropertyConstants)` | `void` |
| `SetFromPlane(FromPlane: Object)` | `void` |
| `SetProfiles(NumProfiles: Int32, pProfiles: Array)` | `void` |
| `SetProfileSide(profileSide: FeaturePropertyConstants)` | `void` |
| `SetToPlane(ToPlane: Object)` | `void` |

### <a name="assemblyfeaturesextrudedprotrusions"></a>`AssemblyFeaturesExtrudedProtrusions`

| Propriedade | Tipo | Acesso |
|-------------|------|--------|
| `Application` | `Application` | get |
| `Count` | `Int32` | get |
| `Parent` | `Object` | get |

| Método | Retorno |
|--------|---------|
| `Add(nNumProfiles: Int32, [ref] pProfiles: Array, ExtentType: FeaturePropertyConstants, [ref] pExtentSide: FeaturePropertyConstants, profileSide: FeaturePropertyConstants, [ref] pdDistance: Double, pKeyPoint: Object, [ref] pKeyPointFlags: KeyPointExtentConstants, pFromSurfOrPlane: Object, pToSurfOrPlane: Object)` | `AssemblyFeaturesExtrudedProtrusion` |
| `GetEnumerator()` | `IEnumerator` |
| `Item(Index: Object)` | `AssemblyFeaturesExtrudedProtrusion` |

### <a name="assemblyfeatureshole"></a>`AssemblyFeaturesHole`

| Propriedade | Tipo | Acesso |
|-------------|------|--------|
| `Application` | `Application` | get |
| `Edges[EdgeType: FeatureTopologyQueryTypeConstants]` | `Object` | get |
| `EdgesForScopePart[ScopePart: Object, EdgeType: FeatureTopologyQueryTypeConstants]` | `Object` | get |
| `Faces[FaceType: FeatureTopologyQueryTypeConstants]` | `Object` | get |
| `FacesByRay[Xorigin: Double, Yorigin: Double, Zorigin: Double, Xdir: Double, Ydir: Double, Zdir: Double]` | `Object` | get |
| `FacesByRayForScopePart[ScopePart: Object, Xorigin: Double, Yorigin: Double, Zorigin: Double, Xdir: Double, Ydir: Double, Zdir: Double]` | `Object` | get |
| `FacesForScopePart[ScopePart: Object, FaceType: FeatureTopologyQueryTypeConstants]` | `Object` | get |
| `FeatureType` | `FeatureTypeConstants` | get |
| `Name` | `String` | get/set |
| `Parent` | `Object` | get |
| `Status[[opt] [out] Description: Object]` | `FeatureStatusConstants` | get |
| `Suppress` | `Boolean` | get/set |

| Método | Retorno |
|--------|---------|
| `AddSuppressionVariable()` | `SuppressVariable` |
| `Delete()` | `void` |
| `DeleteSuppressionVariable()` | `void` |
| `GetDepth()` | `Double` |
| `GetExtentSide()` | `FeaturePropertyConstants` |
| `GetExtentType()` | `FeaturePropertyConstants` |
| `GetFromPlane()` | `Object` |
| `GetHoleData()` | `Object` |
| `GetProfiles([out] NumProfiles: Int32, [out] ppProfiles: Array)` | `void` |
| `GetScopeParts([out] NumScopeParts: Int32, [out] pScopeParts: Array)` | `void` |
| `GetSuppressionVariable()` | `SuppressVariable` |
| `GetToPlane()` | `Object` |
| `HasSuppressionVariable()` | `Boolean` |
| `Range([out] X1: Double, [out] Y1: Double, [out] Z1: Double, [out] X2: Double, [out] Y2: Double, [out] Z2: Double)` | `void` |
| `RollToFeature()` | `void` |
| `SetDepth(nDepth: Double)` | `void` |
| `SetExtentSide(ExtentSide: FeaturePropertyConstants)` | `void` |
| `SetExtentType(ExtentType: FeaturePropertyConstants)` | `void` |
| `SetFromPlane(FromPlane: Object)` | `void` |
| `SetHoleData(Holedata: Object)` | `void` |
| `SetProfiles(NumProfiles: Int32, pProfiles: Array)` | `void` |
| `SetScopeParts(NumScopeParts: Int32, pScopeParts: Array)` | `void` |
| `SetToPlane(ToPlane: Object)` | `void` |

### <a name="assemblyfeaturesholes"></a>`AssemblyFeaturesHoles`

| Propriedade | Tipo | Acesso |
|-------------|------|--------|
| `Application` | `Application` | get |
| `Count` | `Int32` | get |
| `Parent` | `Object` | get |

| Método | Retorno |
|--------|---------|
| `Add(nNumScopeParts: UInt32, [ref] pScopeParts: Array, nNumProfiles: UInt32, [ref] pProfiles: Array, [ref] pExtentSide: FeaturePropertyConstants, pHoledata: Object, ExtentType: FeaturePropertyConstants, [ref] pHoleDepth: Double, pFromSurfOrPlane: Object, pToSurfOrPlane: Object, pKeyPoint: Object, [ref] pKeyPointFlags: KeyPointExtentConstants)` | `AssemblyFeaturesHole` |
| `GetEnumerator()` | `IEnumerator` |
| `Item(Index: Object)` | `AssemblyFeaturesHole` |

### <a name="assemblyfeaturesmirror"></a>`AssemblyFeaturesMirror`

| Propriedade | Tipo | Acesso |
|-------------|------|--------|
| `Application` | `Application` | get |
| `Edges[EdgeType: FeatureTopologyQueryTypeConstants]` | `Object` | get |
| `EdgesForScopePart[ScopePart: Object, EdgeType: FeatureTopologyQueryTypeConstants]` | `Object` | get |
| `Faces[FaceType: FeatureTopologyQueryTypeConstants]` | `Object` | get |
| `FacesByRay[Xorigin: Double, Yorigin: Double, Zorigin: Double, Xdir: Double, Ydir: Double, Zdir: Double]` | `Object` | get |
| `FacesByRayForScopePart[ScopePart: Object, Xorigin: Double, Yorigin: Double, Zorigin: Double, Xdir: Double, Ydir: Double, Zdir: Double]` | `Object` | get |
| `FacesForScopePart[ScopePart: Object, FaceType: FeatureTopologyQueryTypeConstants]` | `Object` | get |
| `FeatureType` | `FeatureTypeConstants` | get |
| `Name` | `String` | get/set |
| `Parent` | `Object` | get |
| `Status[[opt] [out] Description: Object]` | `FeatureStatusConstants` | get |
| `Suppress` | `Boolean` | get/set |

| Método | Retorno |
|--------|---------|
| `AddSuppressionVariable()` | `SuppressVariable` |
| `Delete()` | `void` |
| `DeleteSuppressionVariable()` | `void` |
| `GetMirrorType()` | `FeaturePropertyConstants` |
| `GetParentFeatures([out] NumParentFeatures: Int32, [out] ppParentFeatures: Array)` | `void` |
| `GetPlane()` | `Object` |
| `GetSuppressionVariable()` | `SuppressVariable` |
| `HasSuppressionVariable()` | `Boolean` |
| `Range([out] X1: Double, [out] Y1: Double, [out] Z1: Double, [out] X2: Double, [out] Y2: Double, [out] Z2: Double)` | `void` |
| `RollToFeature()` | `void` |
| `SetMirrorType(MirrorType: FeaturePropertyConstants)` | `void` |
| `SetParentFeatures(NumParentFeatures: Int32, pParentFeatures: Array)` | `void` |
| `SetPlane(Plane: Object)` | `void` |

### <a name="assemblyfeaturesmirrors"></a>`AssemblyFeaturesMirrors`

| Propriedade | Tipo | Acesso |
|-------------|------|--------|
| `Application` | `Application` | get |
| `Count` | `Int32` | get |
| `Parent` | `Object` | get |

| Método | Retorno |
|--------|---------|
| `Add(NumberOfFeatures: Int32, [ref] ppFeaturesArray: Array, pMirrorPlane: Object, MirrorType: FeaturePropertyConstants)` | `AssemblyFeaturesMirror` |
| `GetEnumerator()` | `IEnumerator` |
| `Item(Index: Object)` | `AssemblyFeaturesMirror` |

### <a name="assemblyfeaturespattern"></a>`AssemblyFeaturesPattern`

| Propriedade | Tipo | Acesso |
|-------------|------|--------|
| `Application` | `Application` | get |
| `Edges[EdgeType: FeatureTopologyQueryTypeConstants]` | `Object` | get |
| `EdgesForScopePart[ScopePart: Object, EdgeType: FeatureTopologyQueryTypeConstants]` | `Object` | get |
| `Faces[FaceType: FeatureTopologyQueryTypeConstants]` | `Object` | get |
| `FacesByRay[Xorigin: Double, Yorigin: Double, Zorigin: Double, Xdir: Double, Ydir: Double, Zdir: Double]` | `Object` | get |
| `FacesByRayForScopePart[ScopePart: Object, Xorigin: Double, Yorigin: Double, Zorigin: Double, Xdir: Double, Ydir: Double, Zdir: Double]` | `Object` | get |
| `FacesForScopePart[ScopePart: Object, FaceType: FeatureTopologyQueryTypeConstants]` | `Object` | get |
| `FeatureType` | `FeatureTypeConstants` | get |
| `Name` | `String` | get/set |
| `Parent` | `Object` | get |
| `Status[[opt] [out] Description: Object]` | `FeatureStatusConstants` | get |
| `Suppress` | `Boolean` | get/set |

| Método | Retorno |
|--------|---------|
| `AddSuppressionVariable()` | `SuppressVariable` |
| `Delete()` | `void` |
| `DeleteSuppressionVariable()` | `void` |
| `GetParentFeatures([out] NumParentFeatures: Int32, [out] ppParentFeatures: Array)` | `void` |
| `GetPatternType()` | `FeaturePropertyConstants` |
| `GetProfile()` | `Object` |
| `GetSuppressionVariable()` | `SuppressVariable` |
| `HasSuppressionVariable()` | `Boolean` |
| `Range([out] X1: Double, [out] Y1: Double, [out] Z1: Double, [out] X2: Double, [out] Y2: Double, [out] Z2: Double)` | `void` |
| `RollToFeature()` | `void` |
| `SetParentFeatures(NumParentFeatures: Int32, pParentFeatures: Array)` | `void` |
| `SetPatternType(PatternType: FeaturePropertyConstants)` | `void` |
| `SetProfile(pProfile: Object)` | `void` |

### <a name="assemblyfeaturespatterns"></a>`AssemblyFeaturesPatterns`

| Propriedade | Tipo | Acesso |
|-------------|------|--------|
| `Application` | `Application` | get |
| `Count` | `Int32` | get |
| `Parent` | `Object` | get |

| Método | Retorno |
|--------|---------|
| `Add(NumberOfFeatures: Int32, [ref] ppFeaturesArray: Array, Profile: Object, PatternType: FeaturePropertyConstants)` | `AssemblyFeaturesPattern` |
| `GetEnumerator()` | `IEnumerator` |
| `Item(Index: Object)` | `AssemblyFeaturesPattern` |

### <a name="assemblyfeaturesrevolvedcutout"></a>`AssemblyFeaturesRevolvedCutout`

| Propriedade | Tipo | Acesso |
|-------------|------|--------|
| `Application` | `Application` | get |
| `Edges[EdgeType: FeatureTopologyQueryTypeConstants]` | `Object` | get |
| `EdgesForScopePart[ScopePart: Object, EdgeType: FeatureTopologyQueryTypeConstants]` | `Object` | get |
| `Faces[FaceType: FeatureTopologyQueryTypeConstants]` | `Object` | get |
| `FacesByRay[Xorigin: Double, Yorigin: Double, Zorigin: Double, Xdir: Double, Ydir: Double, Zdir: Double]` | `Object` | get |
| `FacesByRayForScopePart[ScopePart: Object, Xorigin: Double, Yorigin: Double, Zorigin: Double, Xdir: Double, Ydir: Double, Zdir: Double]` | `Object` | get |
| `FacesForScopePart[ScopePart: Object, FaceType: FeatureTopologyQueryTypeConstants]` | `Object` | get |
| `FeatureType` | `FeatureTypeConstants` | get |
| `Name` | `String` | get/set |
| `Parent` | `Object` | get |
| `Status[[opt] [out] Description: Object]` | `FeatureStatusConstants` | get |
| `Suppress` | `Boolean` | get/set |

| Método | Retorno |
|--------|---------|
| `AddSuppressionVariable()` | `SuppressVariable` |
| `Delete()` | `void` |
| `DeleteSuppressionVariable()` | `void` |
| `GetAngle()` | `Double` |
| `GetExtentSide()` | `FeaturePropertyConstants` |
| `GetExtentType()` | `FeaturePropertyConstants` |
| `GetProfiles([out] NumProfiles: Int32, [out] ppProfiles: Array)` | `void` |
| `GetProfileSide()` | `FeaturePropertyConstants` |
| `GetReferenceAxis()` | `Object` |
| `GetScopeParts([out] NumScopeParts: Int32, [out] pScopeParts: Array)` | `void` |
| `GetSuppressionVariable()` | `SuppressVariable` |
| `HasSuppressionVariable()` | `Boolean` |
| `Range([out] X1: Double, [out] Y1: Double, [out] Z1: Double, [out] X2: Double, [out] Y2: Double, [out] Z2: Double)` | `void` |
| `RollToFeature()` | `void` |
| `SetAngle(nAngle: Double)` | `void` |
| `SetExtentSide(ExtentSide: FeaturePropertyConstants)` | `void` |
| `SetExtentType(ExtentType: FeaturePropertyConstants)` | `void` |
| `SetProfiles(NumProfiles: Int32, pProfiles: Array)` | `void` |
| `SetProfileSide(profileSide: FeaturePropertyConstants)` | `void` |
| `SetReferenceAxis(ReferenceAxis: Object)` | `void` |
| `SetScopeParts(NumScopeParts: Int32, pScopeParts: Array)` | `void` |

### <a name="assemblyfeaturesrevolvedcutouts"></a>`AssemblyFeaturesRevolvedCutouts`

| Propriedade | Tipo | Acesso |
|-------------|------|--------|
| `Application` | `Application` | get |
| `Count` | `Int32` | get |
| `Parent` | `Object` | get |

| Método | Retorno |
|--------|---------|
| `Add(nNumScopeParts: Int32, [ref] pScopeParts: Array, nNumProfiles: Int32, [ref] pProfiles: Array, pRefAxis: Object, ExtentType: FeaturePropertyConstants, ExtentSide: FeaturePropertyConstants, profileSide: FeaturePropertyConstants, [ref] pdAngle: Double, KeyPointOrTangentFace: Object, [ref] KeyPointFlags: KeyPointExtentConstants, pFromSurface: Object, pToSurface: Object)` | `AssemblyFeaturesRevolvedCutout` |
| `GetEnumerator()` | `IEnumerator` |
| `Item(Index: Object)` | `AssemblyFeaturesRevolvedCutout` |

### <a name="assemblyfeaturesrevolvedprotrusion"></a>`AssemblyFeaturesRevolvedProtrusion`

| Propriedade | Tipo | Acesso |
|-------------|------|--------|
| `Application` | `Application` | get |
| `AssemblyBody` | `AssemblyBody` | get |
| `Edges[EdgeType: FeatureTopologyQueryTypeConstants]` | `Object` | get |
| `Faces[FaceType: FeatureTopologyQueryTypeConstants]` | `Object` | get |
| `FacesByRay[Xorigin: Double, Yorigin: Double, Zorigin: Double, Xdir: Double, Ydir: Double, Zdir: Double]` | `Object` | get |
| `FeatureType` | `FeatureTypeConstants` | get |
| `Name` | `String` | get/set |
| `Parent` | `Object` | get |
| `Status[[opt] [out] Description: Object]` | `FeatureStatusConstants` | get |
| `Suppress` | `Boolean` | get/set |

| Método | Retorno |
|--------|---------|
| `AddSuppressionVariable()` | `SuppressVariable` |
| `Delete()` | `void` |
| `DeleteSuppressionVariable()` | `void` |
| `GetAngle()` | `Double` |
| `GetExtentSide()` | `FeaturePropertyConstants` |
| `GetExtentType()` | `FeaturePropertyConstants` |
| `GetProfiles([out] NumProfiles: Int32, [out] ppProfiles: Array)` | `void` |
| `GetProfileSide()` | `FeaturePropertyConstants` |
| `GetReferenceAxis()` | `Object` |
| `GetSuppressionVariable()` | `SuppressVariable` |
| `HasSuppressionVariable()` | `Boolean` |
| `Range([out] X1: Double, [out] Y1: Double, [out] Z1: Double, [out] X2: Double, [out] Y2: Double, [out] Z2: Double)` | `void` |
| `RollToFeature()` | `void` |
| `SetAngle(nAngle: Double)` | `void` |
| `SetExtentSide(ExtentSide: FeaturePropertyConstants)` | `void` |
| `SetExtentType(ExtentType: FeaturePropertyConstants)` | `void` |
| `SetProfiles(NumProfiles: Int32, pProfiles: Array)` | `void` |
| `SetProfileSide(profileSide: FeaturePropertyConstants)` | `void` |
| `SetReferenceAxis(ReferenceAxis: Object)` | `void` |

### <a name="assemblyfeaturesrevolvedprotrusions"></a>`AssemblyFeaturesRevolvedProtrusions`

| Propriedade | Tipo | Acesso |
|-------------|------|--------|
| `Application` | `Application` | get |
| `Count` | `Int32` | get |
| `Parent` | `Object` | get |

| Método | Retorno |
|--------|---------|
| `Add(nNumProfiles: Int32, [ref] pProfiles: Array, pRefAxis: Object, ExtentType: FeaturePropertyConstants, ExtentSide: FeaturePropertyConstants, profileSide: FeaturePropertyConstants, [ref] pdAngle: Double, KeyPointOrTangentFace: Object, [ref] KeyPointFlags: KeyPointExtentConstants, pFromSurface: Object, pToSurface: Object)` | `AssemblyFeaturesRevolvedProtrusion` |
| `GetEnumerator()` | `IEnumerator` |
| `Item(Index: Object)` | `AssemblyFeaturesRevolvedProtrusion` |

### <a name="assemblyfeaturessweptprotrusion"></a>`AssemblyFeaturesSweptProtrusion`

| Propriedade | Tipo | Acesso |
|-------------|------|--------|
| `Application` | `Application` | get |
| `AssemblyBody` | `AssemblyBody` | get |
| `Edges[EdgeType: FeatureTopologyQueryTypeConstants]` | `Object` | get |
| `Faces[FaceType: FeatureTopologyQueryTypeConstants]` | `Object` | get |
| `FacesByRay[Xorigin: Double, Yorigin: Double, Zorigin: Double, Xdir: Double, Ydir: Double, Zdir: Double]` | `Object` | get |
| `FeatureType` | `FeatureTypeConstants` | get |
| `Name` | `String` | get/set |
| `Parent` | `Object` | get |
| `Status[[opt] [out] Description: Object]` | `FeatureStatusConstants` | get |
| `Suppress` | `Boolean` | get/set |

| Método | Retorno |
|--------|---------|
| `AddSuppressionVariable()` | `SuppressVariable` |
| `Delete()` | `void` |
| `DeleteSuppressionVariable()` | `void` |
| `GetSuppressionVariable()` | `SuppressVariable` |
| `HasSuppressionVariable()` | `Boolean` |
| `Range([out] X1: Double, [out] Y1: Double, [out] Z1: Double, [out] X2: Double, [out] Y2: Double, [out] Z2: Double)` | `void` |
| `RollToFeature()` | `void` |

### <a name="assemblyfeaturessweptprotrusions"></a>`AssemblyFeaturesSweptProtrusions`

| Propriedade | Tipo | Acesso |
|-------------|------|--------|
| `Application` | `Application` | get |
| `Count` | `Int32` | get |
| `Parent` | `Object` | get |

| Método | Retorno |
|--------|---------|
| `Add(NumCurves: Int32, [ref] TraceCurves: Array, NumSections: Int32, [ref] CrossSections: Array, [ref] Origins: Array)` | `AssemblyFeaturesSweptProtrusion` |
| `GetEnumerator()` | `IEnumerator` |
| `Item(Index: Object)` | `AssemblyFeaturesSweptProtrusion` |

### <a name="assemblyfilletweld"></a>`AssemblyFilletWeld`

| Propriedade | Tipo | Acesso |
|-------------|------|--------|
| `Application` | `Application` | get |
| `AssemblyBody` | `AssemblyBody` | get |
| `AttributeSets` | `Object` | get |
| `BaseThickness` | `Double` | get/set |
| `Edges[EdgeType: FeatureTopologyQueryTypeConstants]` | `Object` | get |
| `Faces[FaceType: FeatureTopologyQueryTypeConstants]` | `Object` | get |
| `FacesByRay[Xorigin: Double, Yorigin: Double, Zorigin: Double, Xdir: Double, Ydir: Double, Zdir: Double]` | `Object` | get |
| `FeatureType` | `FeatureTypeConstants` | get |
| `IsAttributeSetPresent[Name: String]` | `Boolean` | get |
| `Name` | `String` | get/set |
| `Parent` | `Object` | get |
| `SetBackType` | `FilletWeldSetbackConstants` | get/set |
| `Status[[opt] [out] Description: Object]` | `FeatureStatusConstants` | get |
| `Suppress` | `Boolean` | get/set |
| `TargetThickness` | `Double` | get/set |
| `Visible` | `Boolean` | get/set |

| Método | Retorno |
|--------|---------|
| `AddSuppressionVariable()` | `SuppressVariable` |
| `Delete()` | `void` |
| `DeleteSuppressionVariable()` | `void` |
| `GetLabelWeldData([out] Object: Object)` | `void` |
| `GetSuppressionVariable()` | `SuppressVariable` |
| `GoTo()` | `void` |
| `HasSuppressionVariable()` | `Boolean` |
| `Range([out] X1: Double, [out] Y1: Double, [out] Z1: Double, [out] X2: Double, [out] Y2: Double, [out] Z2: Double)` | `void` |
| `Recompute()` | `void` |

### <a name="assemblyfilletwelds"></a>`AssemblyFilletWelds`

| Propriedade | Tipo | Acesso |
|-------------|------|--------|
| `Application` | `Application` | get |
| `Count` | `Int32` | get |
| `Parent` | `Object` | get |

| Método | Retorno |
|--------|---------|
| `Add(NumberOfBaseFaces: Int32, [ref] BaseFaces: Array, NumberOfTargetFaces: Int32, [ref] TargetFaces: Array, pLabelWeldDataObject: Object, eSetbackType: FilletWeldSetbackConstants, dBaseThickness: Double, dTargetThickness: Double)` | `AssemblyFilletWeld` |
| `GetEnumerator()` | `IEnumerator` |
| `Item(Index: Object)` | `AssemblyFilletWeld` |

### <a name="assemblygroup"></a>`AssemblyGroup`

| Propriedade | Tipo | Acesso |
|-------------|------|--------|
| `Application` | `Application` | get |
| `Name` | `String` | get/set |
| `NumComponents` | `Int32` | get |
| `Parent` | `Object` | get |
| `Type` | `ObjectType` | get |
| `Visible` | `Boolean` | set |

| Método | Retorno |
|--------|---------|
| `AddToGroup(NumComponents: Int32, [out] Components: Array)` | `void` |
| `Delete()` | `void` |
| `GetComponents([out] Components: Array)` | `void` |
| `RemoveFromGroup(NumComponents: Int32, [out] Components: Array)` | `void` |
| `UnGroup()` | `void` |

### <a name="assemblygroups"></a>`AssemblyGroups`

| Propriedade | Tipo | Acesso |
|-------------|------|--------|
| `Application` | `Application` | get |
| `Count` | `Int32` | get |
| `Parent` | `Object` | get |
| `Type` | `ObjectType` | get |

| Método | Retorno |
|--------|---------|
| `Add(NumComponents: Int32, [out] Components: Array)` | `AssemblyGroup` |
| `GetEnumerator()` | `IEnumerator` |
| `Item(Index: Object)` | `AssemblyGroup` |

### <a name="assemblylabelweld"></a>`AssemblyLabelWeld`

| Propriedade | Tipo | Acesso |
|-------------|------|--------|
| `Application` | `Application` | get |
| `AttributeSets` | `Object` | get |
| `FeatureType` | `FeatureTypeConstants` | get |
| `IsAttributeSetPresent[Name: String]` | `Boolean` | get |
| `Name` | `String` | get/set |
| `Parent` | `Object` | get |
| `Status[[opt] [out] Description: Object]` | `FeatureStatusConstants` | get |
| `Suppress` | `Boolean` | get/set |
| `Visible` | `Boolean` | get/set |

| Método | Retorno |
|--------|---------|
| `AddSuppressionVariable()` | `SuppressVariable` |
| `Delete()` | `void` |
| `DeleteSuppressionVariable()` | `void` |
| `GetLabelWeldData([out] Object: Object)` | `void` |
| `GetSuppressionVariable()` | `SuppressVariable` |
| `GoTo()` | `void` |
| `HasSuppressionVariable()` | `Boolean` |
| `Recompute()` | `void` |

### <a name="assemblylabelwelds"></a>`AssemblyLabelWelds`

| Propriedade | Tipo | Acesso |
|-------------|------|--------|
| `Application` | `Application` | get |
| `AttributeSets` | `Object` | get |
| `Count` | `Int32` | get |
| `IsAttributeSetPresent[Name: String]` | `Boolean` | get |
| `Parent` | `Object` | get |

| Método | Retorno |
|--------|---------|
| `Add(NumberOfEdges: Int32, [ref] Edges: Array, pLabelWeldDataObject: Object)` | `AssemblyLabelWeld` |
| `GetEnumerator()` | `IEnumerator` |
| `Item(Index: Object)` | `AssemblyLabelWeld` |

### <a name="assemblymirror"></a>`AssemblyMirror`

| Propriedade | Tipo | Acesso |
|-------------|------|--------|
| `Application` | `Application` | get |
| `MirrorPlane` | `Object` | get |
| `Name` | `String` | get |
| `Parent` | `Object` | get |
| `Status` | `AssemblyCopyStatusConstants` | get |

| Método | Retorno |
|--------|---------|
| `GetDefinition([out] inputOccs: Array, [out] outputOccs: Array, [opt] [out] actionEnums: Object, [opt] [out] planeEnums: Object, [opt] [out] userEnums: Object)` | `void` |

### <a name="assemblymirrors"></a>`AssemblyMirrors`

| Propriedade | Tipo | Acesso |
|-------------|------|--------|
| `Application` | `Application` | get |
| `Count` | `Int32` | get |
| `Parent` | `Object` | get |

| Método | Retorno |
|--------|---------|
| `Item(Index: Object)` | `AssemblyMirror` |

### <a name="assemblypattern"></a>`AssemblyPattern`

| Propriedade | Tipo | Acesso |
|-------------|------|--------|
| `Application` | `Application` | get |
| `Count` | `Int32` | get |
| `Name` | `String` | get/set |
| `Parent` | `Object` | get |
| `Visible` | `Boolean` | get/set |

| Método | Retorno |
|--------|---------|
| `Edit([ref] MasterOccurrences: Array, FeaturePattern: Object, [opt] ReferenceFeature: Object, [opt] Name: String)` | `void` |
| `EditAlongCurve(NumberOfMasters: Int32, [ref] MasterOccs: Array, NumberOfCurves1: Int32, [ref] Curves1: Array, AnchorPointForCurves1: Object, AnchorSideForCurves1: PatternCurveAnchorSideConstants, AnchorAtDistanceForCurves1: Double, PatternOffsetTypeForCurves1: PatternOffsetTypeConstants, NumberOfOccurrencesForCurves1: Int32, OccurrenceSpacingForCurves1: Double, NumberOfCurves2: Int32, [ref] Curves2: Array, AnchorPointForCurves2: Object, AnchorSideForCurves2: PatternCurveAnchorSideConstants, AnchorAtDistanceForCurves2: Double, PatternOffsetTypeForCurves2: PatternOffsetTypeConstants, NumberOfOccurrencesForCurves2: Int32, OccurrenceSpacingForCurves2: Double, TransformType: PatternTransformTypeConstants, TransformRotateType: PatternTransformRotateTypeConstants, TransformPlaneOrSurface: Object, Name: String, [opt] FutureUse1: Object, [opt] FutureUse2: Object, [opt] FutureUse3: Object)` | `void` |
| `EditAlongCurveEx(NumberOfMasters: Int32, [ref] MasterOccs: Array, NumberOfCurves1: Int32, [ref] Curves1: Array, AnchorPointForCurves1: Object, AnchorSideForCurves1: PatternCurveAnchorSideConstants, AnchorAtDistanceForCurves1: Double, PatternOffsetTypeForCurves1: PatternOffsetTypeConstants, NumberOfOccurrencesForCurves1: Int32, OccurrenceSpacingForCurves1: Double, NumberOfCurves2: Int32, [ref] Curves2: Array, AnchorPointForCurves2: Object, AnchorSideForCurves2: PatternCurveAnchorSideConstants, AnchorAtDistanceForCurves2: Double, PatternOffsetTypeForCurves2: PatternOffsetTypeConstants, NumberOfOccurrencesForCurves2: Int32, OccurrenceSpacingForCurves2: Double, TransformType: PatternTransformTypeConstants, TransformRotateType: PatternTransformRotateTypeConstants, TransformPlaneOrSurface: Object, Name: String, lSkipCount: Int32, [opt] FutureUse2: Object, [opt] FutureUse3: Object)` | `void` |
| `EditDuplicate(NumberOfMasters: Int32, [ref] MasterOccurrences: Array, FromOccurrence: Object, NumberOfToOccurrences: Int32, [ref] ToOccurrences: Array, [opt] Name: String)` | `void` |
| `GetEnumerator()` | `IEnumerator` |
| `GetInputs([out] MasterOccurrences: Array, [out] FeaturePattern: Object, [out] ReferenceFeatures: Array)` | `void` |
| `GetOccurrences([out] Occurrences: Array)` | `void` |
| `GetPatternData([out] NumberOfMasters: Int32, [out] MasterOccs: Array, [out] NumberOfCurves1: Int32, [out] Curves1: Array, [out] AnchorPointForCurves1: Object, [out] AnchorSideForCurves1: PatternCurveAnchorSideConstants, [out] AnchorAtDistanceForCurves1: Double, [out] PatternOffsetTypeForCurves1: PatternOffsetTypeConstants, [out] NumberOfOccurrencesForCurves1: Int32, [out] OccurrenceSpacingForCurves1: Double, [out] NumberOfCurves2: Int32, [out] Curves2: Array, [out] AnchorPointForCurves2: Object, [out] AnchorSideForCurves2: PatternCurveAnchorSideConstants, [out] AnchorAtDistanceForCurves2: Double, [out] PatternOffsetTypeForCurves2: PatternOffsetTypeConstants, [out] NumberOfOccurrencesForCurves2: Int32, [out] OccurrenceSpacingForCurves2: Double, [out] TransformType: PatternTransformTypeConstants, [out] TransformRotateType: PatternTransformRotateTypeConstants, [out] TransformPlaneOrSurface: Object, [out] Name: String, [out] SkipCount: Int32)` | `void` |
| `Item(Index: Object)` | `AssemblyPatternOccurrence` |
| `PatternType()` | `AssemblyPatternTypeConstants` |

### <a name="assemblypatternoccurrence"></a>`AssemblyPatternOccurrence`

| Propriedade | Tipo | Acesso |
|-------------|------|--------|
| `Application` | `Application` | get |
| `Name` | `String` | get/set |
| `Parent` | `Object` | get |
| `Suppress` | `Boolean` | get/set |
| `Visible` | `Boolean` | get/set |

| Método | Retorno |
|--------|---------|
| `GetOccurrences([out] Occurrences: Array)` | `void` |
| `GetPosition([out] Row: Int32, [out] Col: Int32)` | `void` |

### <a name="assemblypatterns"></a>`AssemblyPatterns`

| Propriedade | Tipo | Acesso |
|-------------|------|--------|
| `Application` | `Application` | get |
| `Count` | `Int32` | get |
| `Parent` | `Object` | get |

| Método | Retorno |
|--------|---------|
| `Create(PatternName: String, [ref] MasterOccurrences: Array, FeaturePattern: Object, ReferenceFeature: Object)` | `AssemblyPattern` |
| `CreateAlongCurve(NumberOfMasters: Int32, [ref] MasterOccs: Array, NumberOfCurves1: Int32, [ref] Curves1: Array, AnchorPointForCurves1: Object, AnchorSideForCurves1: PatternCurveAnchorSideConstants, AnchorAtDistanceForCurves1: Double, PatternOffsetTypeForCurves1: PatternOffsetTypeConstants, NumberOfOccurrencesForCurves1: Int32, OccurrenceSpacingForCurves1: Double, NumberOfCurves2: Int32, [ref] Curves2: Array, AnchorPointForCurves2: Object, AnchorSideForCurves2: PatternCurveAnchorSideConstants, AnchorAtDistanceForCurves2: Double, PatternOffsetTypeForCurves2: PatternOffsetTypeConstants, NumberOfOccurrencesForCurves2: Int32, OccurrenceSpacingForCurves2: Double, TransformType: PatternTransformTypeConstants, TransformRotateType: PatternTransformRotateTypeConstants, TransformPlaneOrSurface: Object, bstrName: String, [opt] FutureUse1: Object, [opt] FutureUse2: Object, [opt] FutureUse3: Object)` | `AssemblyPattern` |
| `CreateAlongCurveEx(NumberOfMasters: Int32, [ref] MasterOccs: Array, NumberOfCurves1: Int32, [ref] Curves1: Array, AnchorPointForCurves1: Object, AnchorSideForCurves1: PatternCurveAnchorSideConstants, AnchorAtDistanceForCurves1: Double, PatternOffsetTypeForCurves1: PatternOffsetTypeConstants, NumberOfOccurrencesForCurves1: Int32, OccurrenceSpacingForCurves1: Double, NumberOfCurves2: Int32, [ref] Curves2: Array, AnchorPointForCurves2: Object, AnchorSideForCurves2: PatternCurveAnchorSideConstants, AnchorAtDistanceForCurves2: Double, PatternOffsetTypeForCurves2: PatternOffsetTypeConstants, NumberOfOccurrencesForCurves2: Int32, OccurrenceSpacingForCurves2: Double, TransformType: PatternTransformTypeConstants, TransformRotateType: PatternTransformRotateTypeConstants, TransformPlaneOrSurface: Object, bstrName: String, lSkipCount: Int32, [opt] FutureUse2: Object, [opt] FutureUse3: Object)` | `AssemblyPattern` |
| `CreateDuplicate(PatternName: String, NumberOfMasters: Int32, [ref] MasterOccurrences: Array, FromOccurrence: Object, NumberOfToOccurrences: Int32, [ref] ToOccurrences: Array)` | `AssemblyPattern` |
| `Delete(PatternName: String)` | `void` |
| `Drop(PatternName: String)` | `void` |
| `Find(PatternName: String)` | `AssemblyPattern` |
| `GetEnumerator()` | `IEnumerator` |
| `Item(Index: Object)` | `AssemblyPattern` |

### <a name="assemblyproperties"></a>`AssemblyProperties`

| Propriedade | Tipo | Acesso |
|-------------|------|--------|
| `Application` | `Application` | get |
| `Count` | `Int32` | get |
| `DefaultCustomOccurrenceProperties` | `DefaultCustomOccurrenceProperties` | get |
| `Parent` | `Object` | get |

| Método | Retorno |
|--------|---------|
| `AddProperty(MemberName: String)` | `AssemblyProperty` |
| `DeleteCustomOccurrenceProperties([ref] CustomOccurrencePropertyNames: Array)` | `void` |
| `GetEnumerator()` | `IEnumerator` |
| `Item(Index: Object)` | `AssemblyProperty` |
| `LoadCustomOccurrenceProperties()` | `void` |

### <a name="assemblyproperty"></a>`AssemblyProperty`

| Propriedade | Tipo | Acesso |
|-------------|------|--------|
| `Application` | `Application` | get |
| `Count` | `Int32` | get |
| `Name` | `String` | get/set |
| `Parent` | `Object` | get |

| Método | Retorno |
|--------|---------|
| `AddPropertyItem(ItemName: String, ItemType: PropertyTypeConstants, ItemValue: Object)` | `void` |
| `DeleteAllItems()` | `void` |
| `DeletePropertyItem(ItemName: String)` | `void` |
| `GetDefaultValues([out] NumberOfEntries: Int32, [out] DefaultValues: Array)` | `void` |
| `GetItemType(ItemName: String)` | `PropertyTypeConstants` |
| `GetItemValue(ItemName: String)` | `Object` |
| `GetNameByIndex(IndexOfItem: Int32)` | `String` |
| `UpdateItemValue(CurrentItemName: String, CurrentItemType: PropertyTypeConstants, NewItemValue: Object)` | `void` |

### <a name="assemblystitchweld"></a>`AssemblyStitchWeld`

| Propriedade | Tipo | Acesso |
|-------------|------|--------|
| `AnnotationFormat` | `StitchWeldAnnotationFormat` | get/set |
| `Application` | `Application` | get |
| `AttributeSets` | `Object` | get |
| `BeadLength` | `Double` | get/set |
| `EndOffset` | `Double` | get/set |
| `FeatureType` | `FeatureTypeConstants` | get |
| `GapLength` | `Double` | get/set |
| `IsAttributeSetPresent[Name: String]` | `Boolean` | get |
| `Name` | `String` | get/set |
| `Parent` | `Object` | get |
| `StartOffset` | `Double` | get/set |
| `Status[[opt] [out] Description: Object]` | `FeatureStatusConstants` | get |
| `Suppress` | `Boolean` | get/set |
| `Type` | `StitchWeldType` | get/set |
| `Visible` | `Boolean` | get/set |

| Método | Retorno |
|--------|---------|
| `AddSuppressionVariable()` | `SuppressVariable` |
| `Delete()` | `void` |
| `DeleteSuppressionVariable()` | `void` |
| `GetSuppressionVariable()` | `SuppressVariable` |
| `GoTo()` | `void` |
| `HasSuppressionVariable()` | `Boolean` |
| `Recompute()` | `void` |

### <a name="assemblystitchwelds"></a>`AssemblyStitchWelds`

| Propriedade | Tipo | Acesso |
|-------------|------|--------|
| `Application` | `Application` | get |
| `Count` | `Int32` | get |
| `Parent` | `Object` | get |

| Método | Retorno |
|--------|---------|
| `Add(NumberOfStitchPaths: Int32, [ref] StitchWeldPaths: Array, [ref] StartVertices: Array, [ref] DirectionEdgeForClosedStitchWeldPaths: Array, [ref] eStitchDirections: Array, eWeldType: StitchWeldType, eAnnotationFormat: StitchWeldAnnotationFormat, dStartOffsetLength: Double, dEndOffsetLength: Double, dBeadLength: Double, dGapLength: Double, bsStyleName: String)` | `AssemblyStitchWeld` |
| `GetEnumerator()` | `IEnumerator` |
| `Item(Index: Object)` | `AssemblyStitchWeld` |

### <a name="assemblythread"></a>`AssemblyThread`

| Propriedade | Tipo | Acesso |
|-------------|------|--------|
| `Application` | `Application` | get |
| `Edges[EdgeType: FeatureTopologyQueryTypeConstants]` | `Object` | get |
| `Faces[FaceType: FeatureTopologyQueryTypeConstants]` | `Object` | get |
| `FacesByRay[Xorigin: Double, Yorigin: Double, Zorigin: Double, Xdir: Double, Ydir: Double, Zdir: Double]` | `Object` | get |
| `FeatureType` | `FeatureTypeConstants` | get |
| `Holedata` | `Object` | get |
| `Name` | `String` | get/set |
| `Parent` | `Object` | get |
| `Status[[opt] [out] Description: Object]` | `FeatureStatusConstants` | get |
| `Suppress` | `Boolean` | get/set |
| `Visible` | `Boolean` | get/set |

| Método | Retorno |
|--------|---------|
| `AddSuppressionVariable()` | `SuppressVariable` |
| `Delete()` | `void` |
| `DeleteSuppressionVariable()` | `void` |
| `GetSuppressionVariable()` | `SuppressVariable` |
| `GoTo()` | `void` |
| `HasSuppressionVariable()` | `Boolean` |
| `Range([out] X1: Double, [out] Y1: Double, [out] Z1: Double, [out] X2: Double, [out] Y2: Double, [out] Z2: Double)` | `void` |
| `Recompute()` | `void` |

### <a name="assemblythreads"></a>`AssemblyThreads`

| Propriedade | Tipo | Acesso |
|-------------|------|--------|
| `Application` | `Application` | get |
| `Count` | `Int32` | get |
| `Parent` | `Object` | get |

| Método | Retorno |
|--------|---------|
| `Add(Holedata: Object, NumberOfCylinders: Int32, [ref] CylinderArray: Array, [ref] CylinderEndArray: Array, [ref] CylinderEndTypes: Array, [opt] Reserved2: Object)` | `AssemblyThread` |
| `GetEnumerator()` | `IEnumerator` |
| `Item(Index: Object)` | `AssemblyThread` |

### <a name="axialrelation3d"></a>`AxialRelation3d`

| Propriedade | Tipo | Acesso |
|-------------|------|--------|
| `Application` | `Object` | get |
| `AttributeSets` | `Object` | get |
| `DetailedStatus` | `Relation3dDetailedStatusConstants` | get |
| `FixedParallelOffset` | `Boolean` | get/set |
| `FixedRotate` | `Boolean` | get/set |
| `Index` | `Int32` | get |
| `IsAttributeSetPresent[Name: String]` | `Boolean` | get |
| `Occurrence1` | `Occurrence` | get |
| `Occurrence2` | `Occurrence` | get |
| `Offset` | `Double` | get/set |
| `Orientation` | `Relation3dOrientationConstants` | get/set |
| `ParallelOffset` | `Boolean` | get/set |
| `Parent` | `Object` | get |
| `Part1` | `Part` | get |
| `Part2` | `Part` | get |
| `RangedOffset` | `Boolean` | get/set |
| `RangeHigh` | `Double` | get/set |
| `RangeLow` | `Double` | get/set |
| `Status` | `Relation3dStatusConstants` | get |
| `Suppress` | `Boolean` | get/set |
| `Type` | `ObjectType` | get |

| Método | Retorno |
|--------|---------|
| `AddSuppressionVariable()` | `SuppressVariable` |
| `Delete()` | `void` |
| `DeleteSuppressionVariable()` | `void` |
| `Flip()` | `void` |
| `GetElement1([out] IsTopologyReference: Boolean)` | `Object` |
| `GetElement2([out] IsTopologyReference: Boolean)` | `Object` |
| `GetGeometry1([out] GeometryType: Int32, [out] PointX: Double, [out] PointY: Double, [out] PointZ: Double, [out] VectorX: Double, [out] VectorY: Double, [out] VectorZ: Double)` | `void` |
| `GetGeometry2([out] GeometryType: Int32, [out] PointX: Double, [out] PointY: Double, [out] PointZ: Double, [out] VectorX: Double, [out] VectorY: Double, [out] VectorZ: Double)` | `void` |
| `GetSuppressionVariable()` | `SuppressVariable` |
| `HasSuppressionVariable()` | `Boolean` |
| `Select(Replace: Boolean)` | `void` |

### <a name="bundle"></a>`Bundle`

| Propriedade | Tipo | Acesso |
|-------------|------|--------|
| `Application` | `Application` | get |
| `AssemblyProperty` | `AssemblyProperty` | get |
| `AttributeSets` | `Object` | get |
| `Color` | `Int32` | get/set |
| `CutLength` | `Double` | get |
| `Description` | `String` | get/set |
| `IsAttributeSetPresent[Name: String]` | `Boolean` | get |
| `LinearDensity` | `Double` | get/set |
| `Mass` | `Double` | get |
| `MinimumBendRadius` | `Double` | get/set |
| `Name` | `String` | get/set |
| `OuterDiameter` | `Double` | get/set |
| `Parent` | `Object` | get |
| `PartNumber` | `String` | get/set |
| `TrueLength` | `Double` | get |
| `Type` | `HarnessTypeConstants` | get |
| `UseGlobalAdders` | `Boolean` | get/set |
| `Visible` | `Boolean` | get/set |

| Método | Retorno |
|--------|---------|
| `CreatePhysicalConductor()` | `void` |
| `Delete()` | `void` |
| `DeletePhysicalConductor()` | `void` |
| `Edit(NumberOfPaths: Int32, [ref] PathArray: Array, [ref] PathDirectionArray: Array, NumberOfConductors: Int32, [ref] ConductorArray: Array, [ref] SplitPathArray: Array, [ref] SplitPathDirectionArray: Array)` | `void` |
| `GetAdders([out] dSlackAdder: Double, [out] dPureAdder: Double, [out] dHoleDiameterAdder: Double, [out] dBundleDiameterAdder: Double)` | `void` |
| `GetAllPaths([out] NumberOfPaths: Int32, [out] PathArray: Array)` | `void` |
| `GetConductors([out] NumberOfConductors: Int32, [out] ConductorArray: Array)` | `void` |
| `GetEndPoints([out] dStartX: Double, [out] dStartY: Double, [out] dStartZ: Double, [out] dEndX: Double, [out] dEndY: Double, [out] dEndZ: Double)` | `void` |
| `GetOuterConductors([out] NumberOfConductors: Int32, [out] ConductorArray: Array)` | `void` |
| `GetPaths([out] NumberOfPaths: Int32, [out] PathArray: Array, [out] PathDirectionArray: Array)` | `void` |
| `GetTopMostConductors([out] NumberOfConductors: Int32, [out] ConductorArray: Array)` | `void` |
| `Remove(NumberOfConductors: Int32, [ref] ConductorArray: Array)` | `void` |
| `SetAdders(dSlackAdder: Double, dPureAdder: Double, dHoleDiameterAdder: Double, dBundleDiameterAdder: Double)` | `void` |

### <a name="bundles"></a>`Bundles`

| Propriedade | Tipo | Acesso |
|-------------|------|--------|
| `Application` | `Application` | get |
| `Count` | `Int32` | get |
| `Parent` | `Object` | get |
| `Type` | `HarnessTypeConstants` | get |

| Método | Retorno |
|--------|---------|
| `Add(NumberOfPaths: Int32, [ref] PathArray: Array, [ref] PathDirectionArray: Array, NumberOfConductors: Int32, [ref] ConductorArray: Array, [ref] SplitPathArray: Array, [ref] SplitPathDirectionArray: Array, ConductorDescription: String)` | `Bundle` |
| `GetEnumerator()` | `IEnumerator` |
| `Item(Index: Object)` | `Bundle` |

### <a name="cable"></a>`Cable`

| Propriedade | Tipo | Acesso |
|-------------|------|--------|
| `Application` | `Application` | get |
| `AssemblyProperty` | `AssemblyProperty` | get |
| `AttributeSets` | `Object` | get |
| `Color` | `Int32` | get/set |
| `CutLength` | `Double` | get |
| `Description` | `String` | get/set |
| `Diameter` | `Double` | get/set |
| `FromComponentName` | `String` | get/set |
| `FromTerminalName` | `String` | get/set |
| `Gage` | `String` | get/set |
| `IsAttributeSetPresent[Name: String]` | `Boolean` | get |
| `LinearDensity` | `Double` | get/set |
| `Mass` | `Double` | get |
| `Material` | `String` | get/set |
| `MaterialType` | `String` | get/set |
| `MinimumBendRadius` | `Double` | get/set |
| `Name` | `String` | get/set |
| `NumberOfConductors` | `Int32` | get/set |
| `OuterDiameter` | `Double` | get/set |
| `Parent` | `Object` | get |
| `PartNumber` | `String` | get/set |
| `ToComponentName` | `String` | get/set |
| `ToTerminalName` | `String` | get/set |
| `TrueLength` | `Double` | get |
| `Type` | `HarnessTypeConstants` | get |
| `UseGlobalAdders` | `Boolean` | get/set |
| `Visible` | `Boolean` | get/set |

| Método | Retorno |
|--------|---------|
| `CreatePhysicalConductor()` | `void` |
| `Delete()` | `void` |
| `DeletePhysicalConductor()` | `void` |
| `Edit(NumberOfPaths: Int32, [ref] PathArray: Array, [ref] PathDirectionArray: Array, NumberOfWires: Int32, [ref] WireArray: Array, [ref] SplitPathArray: Array, [ref] SplitPathDirectionArray: Array)` | `void` |
| `GetAdders([out] dSlackAdder: Double, [out] dPureAdder: Double, [out] dHoleDiameterAdder: Double, [out] dBundleDiameterAdder: Double)` | `void` |
| `GetAllPaths([out] NumberOfPaths: Int32, [out] PathArray: Array)` | `void` |
| `GetEndPoints([out] dStartX: Double, [out] dStartY: Double, [out] dStartZ: Double, [out] dEndX: Double, [out] dEndY: Double, [out] dEndZ: Double)` | `void` |
| `GetOuterConductors([out] NumberOfConductors: Int32, [out] ConductorArray: Array)` | `void` |
| `GetPaths([out] NumberOfPaths: Int32, [out] PathArray: Array, [out] PathDirectionArray: Array)` | `void` |
| `GetTopMostConductors([out] NumberOfConductors: Int32, [out] ConductorArray: Array)` | `void` |
| `GetWires([out] NumberOfWires: Int32, [out] WireArray: Array)` | `void` |
| `Remove(NumberOfConductors: Int32, [ref] ConductorArray: Array)` | `void` |
| `SetAdders(dSlackAdder: Double, dPureAdder: Double, dHoleDiameterAdder: Double, dBundleDiameterAdder: Double)` | `void` |

### <a name="cables"></a>`Cables`

| Propriedade | Tipo | Acesso |
|-------------|------|--------|
| `Application` | `Application` | get |
| `Count` | `Int32` | get |
| `Parent` | `Object` | get |
| `Type` | `HarnessTypeConstants` | get |

| Método | Retorno |
|--------|---------|
| `Add(NumberOfPaths: Int32, [ref] PathArray: Array, [ref] PathDirectionArray: Array, NumberOfWires: Int32, [ref] WireArray: Array, [ref] SplitPathArray: Array, [ref] SplitPathDirectionArray: Array, ConductorDescription: String)` | `Cable` |
| `GetEnumerator()` | `IEnumerator` |
| `Item(Index: Object)` | `Cable` |

### <a name="camfollowerrelation3d"></a>`CamFollowerRelation3d`

| Propriedade | Tipo | Acesso |
|-------------|------|--------|
| `Application` | `Object` | get |
| `AttributeSets` | `Object` | get |
| `DetailedStatus` | `Relation3dDetailedStatusConstants` | get |
| `Index` | `Int32` | get |
| `IsAttributeSetPresent[Name: String]` | `Boolean` | get |
| `Occurrence1` | `Occurrence` | get |
| `Occurrence2` | `Occurrence` | get |
| `Parent` | `Object` | get |
| `Status` | `Relation3dStatusConstants` | get |
| `Suppress` | `Boolean` | get/set |
| `Type` | `ObjectType` | get |

| Método | Retorno |
|--------|---------|
| `AddSuppressionVariable()` | `SuppressVariable` |
| `Delete()` | `void` |
| `DeleteSuppressionVariable()` | `void` |
| `GetSuppressionVariable()` | `SuppressVariable` |
| `HasSuppressionVariable()` | `Boolean` |
| `Select(Replace: Boolean)` | `void` |

### <a name="centerplanerelation3d"></a>`CenterPlaneRelation3d`

| Propriedade | Tipo | Acesso |
|-------------|------|--------|
| `Application` | `Object` | get |
| `AttributeSets` | `Object` | get |
| `DetailedStatus` | `Relation3dDetailedStatusConstants` | get |
| `Index` | `Int32` | get |
| `IsAttributeSetPresent[Name: String]` | `Boolean` | get |
| `Parent` | `Object` | get |
| `Status` | `Relation3dStatusConstants` | get |
| `Suppress` | `Boolean` | get/set |
| `Type` | `ObjectType` | get |

| Método | Retorno |
|--------|---------|
| `AddSuppressionVariable()` | `SuppressVariable` |
| `Delete()` | `void` |
| `DeleteSuppressionVariable()` | `void` |
| `Flip()` | `void` |
| `GetSuppressionVariable()` | `SuppressVariable` |
| `HasSuppressionVariable()` | `Boolean` |
| `Select(Replace: Boolean)` | `void` |

### <a name="componentlayout"></a>`ComponentLayout`

| Propriedade | Tipo | Acesso |
|-------------|------|--------|
| `Application` | `Application` | get |
| `AttributeSets` | `Object` | get |
| `IsAttributeSetPresent[Name: String]` | `Boolean` | get |
| `Name` | `String` | get/set |
| `Parent` | `Object` | get |
| `Profile` | `Object` | get |
| `RefPlane` | `AsmRefPlane` | get |
| `Type` | `ObjectType` | get |

| Método | Retorno |
|--------|---------|
| `Delete()` | `void` |

### <a name="componentlayouts"></a>`ComponentLayouts`

| Propriedade | Tipo | Acesso |
|-------------|------|--------|
| `Application` | `Application` | get |
| `Count` | `Int32` | get |
| `Parent` | `Object` | get |

| Método | Retorno |
|--------|---------|
| `Add(AsmRefPlane: AsmRefPlane, [opt] ReturnExisting: Object, [opt] [out] Status: Object)` | `ComponentLayout` |
| `GetEnumerator()` | `IEnumerator` |
| `Item(Index: Object)` | `ComponentLayout` |

### <a name="configuration"></a>`Configuration`

| Propriedade | Tipo | Acesso |
|-------------|------|--------|
| `Application` | `Application` | get |
| `ConfigurationType` | `ConfigurationTypeConstants` | get |
| `Name` | `String` | get |
| `Parent` | `Object` | get |
| `Type` | `ObjectType` | get |

| Método | Retorno |
|--------|---------|
| `Apply([opt] ActivationOverride: Object, [opt] SimplifyOverride: Object)` | `void` |
| `Delete()` | `void` |
| `Save()` | `void` |
| `Update()` | `void` |

### <a name="configurations"></a>`Configurations`

| Propriedade | Tipo | Acesso |
|-------------|------|--------|
| `Application` | `Application` | get |
| `Count` | `Int32` | get |
| `Dirty` | `Boolean` | get |
| `Parent` | `Object` | get |

| Método | Retorno |
|--------|---------|
| `Add(Name: String)` | `Configuration` |
| `AddDerivedConfig(nConfig: Int32, nZones: Int32, nQueries: Int32, [ref] ppsaConfigList: Object, [ref] ppsaZoneList: Object, [ref] ppsaQueryList: Object, bstrNewConfigName: String)` | `Configuration` |
| `Apply(Index: Object, [opt] ActivationOverride: Object, [opt] SimplifyOverride: Object)` | `void` |
| `GetEnumerator()` | `IEnumerator` |
| `Item(Index: Object)` | `Configuration` |
| `Save()` | `void` |

### <a name="curvesegment"></a>`CurveSegment`

| Propriedade | Tipo | Acesso |
|-------------|------|--------|
| `Application` | `Application` | get |
| `AttributeSets` | `Object` | get |
| `EndDerivativeMagnitude` | `Double` | get/set |
| `IsAttributeSetPresent[Name: String]` | `Boolean` | get |
| `Parent` | `Object` | get |
| `PathSegmentsCount` | `Int32` | get |
| `StartDerivativeMagnitude` | `Double` | get/set |
| `Type` | `ObjectType` | get |
| `Validation` | `CurveSegmentValidationConstants` | get |
| `WhichKeypoints` | `CurveSegmentWhichKeypointsConstants` | get/set |

| Método | Retorno |
|--------|---------|
| `AddPathSegment(pPathSegment: Object, [out] AdditionStatus: CurveSegmentPathAdditionStatusConstants)` | `void` |
| `Delete()` | `void` |
| `GetPathSegment(Index: Object)` | `Object` |
| `HidePathSegments()` | `void` |
| `RemovePathSegment(pPathSegment: Object, [out] RemovalStatus: CurveSegmentPathRemovalStatusConstants)` | `void` |
| `ShowPathSegments()` | `void` |

### <a name="curvesegments"></a>`CurveSegments`

| Propriedade | Tipo | Acesso |
|-------------|------|--------|
| `Application` | `Application` | get |
| `Count` | `Int32` | get |
| `Parent` | `Object` | get |

| Método | Retorno |
|--------|---------|
| `Add(PathSegmentsCount: Int32, [ref] PathSegments: Array, [out] Validation: CurveSegmentValidationConstants)` | `CurveSegment` |
| `GetEnumerator()` | `IEnumerator` |
| `Item(Index: Object)` | `CurveSegment` |

### <a name="defaultcustomoccurrenceproperties"></a>`DefaultCustomOccurrenceProperties`

| Propriedade | Tipo | Acesso |
|-------------|------|--------|
| `Application` | `Application` | get |
| `Count` | `Int32` | get |
| `Parent` | `Object` | get |

| Método | Retorno |
|--------|---------|
| `GetEnumerator()` | `IEnumerator` |
| `Item(Index: Object)` | `AssemblyProperty` |

### <a name="fastenersystem"></a>`FastenerSystem`

| Propriedade | Tipo | Acesso |
|-------------|------|--------|
| `Application` | `Application` | get |
| `Name` | `String` | get/set |
| `Parent` | `Object` | get |
| `Visible` | `Boolean` | get/set |

| Método | Retorno |
|--------|---------|
| `Delete()` | `void` |
| `Flip()` | `void` |
| `GetComponents([out] NoOfcomp: Int32, [out] Occurrences: Array)` | `void` |

### <a name="fastenersystems"></a>`FastenerSystems`

| Propriedade | Tipo | Acesso |
|-------------|------|--------|
| `Application` | `Application` | get |
| `Count` | `Int32` | get |
| `Parent` | `Object` | get |

| Método | Retorno |
|--------|---------|
| `GetComponents(master: Object, [out] NoOfcomp: Int32, [out] Occurrences: Array)` | `void` |
| `GetEnumerator()` | `IEnumerator` |
| `GetOccurrences([out] NumOfMasters: Int32, [out] Occurrences: Array)` | `void` |
| `Item(Index: Object)` | `FastenerSystem` |

### <a name="gearrelation3d"></a>`GearRelation3d`

| Propriedade | Tipo | Acesso |
|-------------|------|--------|
| `Application` | `Object` | get |
| `AttributeSets` | `Object` | get |
| `DetailedStatus` | `Relation3dDetailedStatusConstants` | get |
| `Index` | `Int32` | get |
| `IsAttributeSetPresent[Name: String]` | `Boolean` | get |
| `Occurrence1` | `Occurrence` | get |
| `Occurrence2` | `Occurrence` | get |
| `Parent` | `Object` | get |
| `RatioValue1` | `Double` | get/set |
| `RatioValue2` | `Double` | get/set |
| `Status` | `Relation3dStatusConstants` | get |
| `Suppress` | `Boolean` | get/set |
| `Type` | `ObjectType` | get |

| Método | Retorno |
|--------|---------|
| `AddSuppressionVariable()` | `SuppressVariable` |
| `Delete()` | `void` |
| `DeleteSuppressionVariable()` | `void` |
| `Flip()` | `void` |
| `GetElement1([out] IsTopologyReference: Boolean)` | `Object` |
| `GetElement2([out] IsTopologyReference: Boolean)` | `Object` |
| `GetGeometry1([out] GeometryType: Int32, [out] PointX: Double, [out] PointY: Double, [out] PointZ: Double, [out] VectorX: Double, [out] VectorY: Double, [out] VectorZ: Double)` | `void` |
| `GetGeometry2([out] GeometryType: Int32, [out] PointX: Double, [out] PointY: Double, [out] PointZ: Double, [out] VectorX: Double, [out] VectorY: Double, [out] VectorZ: Double)` | `void` |
| `GetSuppressionVariable()` | `SuppressVariable` |
| `HasSuppressionVariable()` | `Boolean` |
| `Select(Replace: Boolean)` | `void` |

### <a name="groundrelation3d"></a>`GroundRelation3d`

| Propriedade | Tipo | Acesso |
|-------------|------|--------|
| `Application` | `Object` | get |
| `AttributeSets` | `Object` | get |
| `DetailedStatus` | `Relation3dDetailedStatusConstants` | get |
| `Index` | `Int32` | get |
| `IsAttributeSetPresent[Name: String]` | `Boolean` | get |
| `Occurrence` | `Occurrence` | get |
| `Parent` | `Object` | get |
| `Part` | `Part` | get |
| `Status` | `Relation3dStatusConstants` | get |
| `Suppress` | `Boolean` | get/set |
| `Type` | `ObjectType` | get |

| Método | Retorno |
|--------|---------|
| `AddSuppressionVariable()` | `SuppressVariable` |
| `Delete()` | `void` |
| `DeleteSuppressionVariable()` | `void` |
| `GetSuppressionVariable()` | `SuppressVariable` |
| `HasSuppressionVariable()` | `Boolean` |
| `Select(Replace: Boolean)` | `void` |

### <a name="harness"></a>`Harness`

| Propriedade | Tipo | Acesso |
|-------------|------|--------|
| `Application` | `Application` | get |
| `Bundles` | `Bundles` | get |
| `Cables` | `Cables` | get |
| `Parent` | `Object` | get |
| `Splices` | `Splices` | get |
| `Wires` | `Wires` | get |

| Método | Retorno |
|--------|---------|
| `CreatePhysicalConductor()` | `void` |
| `DeletePhysicalConductor()` | `void` |
| `GetTopMostConductors([out] NumberOfConductors: Int32, [out] ConductorArray: Array)` | `void` |
| `SaveAsEcad([out] HarnessSaveAsEcadStatus: HarnessSaveAsEcadStatusConstants, CompanyName: String, [opt] ComponentFilePath: Object, [opt] ConnectionFilePath: Object)` | `void` |

### <a name="harnesses"></a>`Harnesses`

| Propriedade | Tipo | Acesso |
|-------------|------|--------|
| `Application` | `Application` | get |
| `Count` | `Int32` | get |
| `Parent` | `Object` | get |

| Método | Retorno |
|--------|---------|
| `Add()` | `Harness` |
| `CreateFromNetlistImporter(CompanyName: String, ComponentFilePath: String, ConnectionFilePath: String, [opt] vbSaveHarnessImportOnFailure: Object)` | `void` |
| `GetEnumerator()` | `IEnumerator` |
| `Item(Index: Object)` | `Harness` |

### <a name="itemnumbers"></a>`ItemNumbers`

| Propriedade | Tipo | Acesso |
|-------------|------|--------|
| `Application` | `Application` | get |
| `NextAvailableItemNumber` | `Int32` | get |
| `Parent` | `Object` | get |

| Método | Retorno |
|--------|---------|
| `GetItemNumberPreferences([out] Mode: ItemNumberModeConstants, [out] ExpandWeldmentSubassemblies: Boolean, [out] StartNumber: Int32, [out] IncrementBy: Int32)` | `void` |
| `IsInUse(ItemNumber: Int32, [out] ItemNumberInUse: Boolean)` | `void` |
| `Regenerate()` | `void` |
| `RemoveUnusedItemNumbers()` | `void` |
| `SetItemNumberPreferences(Mode: ItemNumberModeConstants, ExpandWeldmentSubassemblies: Boolean, StartNumber: Int32, IncrementBy: Int32)` | `void` |

### <a name="layout"></a>`Layout`

| Propriedade | Tipo | Acesso |
|-------------|------|--------|
| `Application` | `Application` | get |
| `AttributeSets` | `Object` | get |
| `IsAttributeSetPresent[Name: String]` | `Boolean` | get |
| `Name` | `String` | get/set |
| `Parent` | `Object` | get |
| `Profile` | `Object` | get |
| `RefPlane` | `AsmRefPlane` | get |
| `ShowSketchColors` | `Boolean` | get/set |
| `Status[[opt] [out] Description: Object]` | `FeatureStatusConstants` | get |
| `Style` | `String` | get/set |
| `Type` | `ObjectType` | get |

| Método | Retorno |
|--------|---------|
| `Delete()` | `void` |

### <a name="layouts"></a>`Layouts`

| Propriedade | Tipo | Acesso |
|-------------|------|--------|
| `Application` | `Application` | get |
| `Count` | `Int32` | get |
| `Parent` | `Object` | get |
| `Visible` | `Boolean` | get/set |

| Método | Retorno |
|--------|---------|
| `Add(AsmRefPlane: AsmRefPlane, [opt] ReturnExisting: Object, [opt] [out] Status: Object)` | `Layout` |
| `AddByTearOff([ref] ProfileCurveBodyEdges: Array, TearOffSketchPlane: Object, bAssociative: Boolean, bCopy: Boolean, [opt] [out] Status: Object)` | `Layout` |
| `GetEnumerator()` | `IEnumerator` |
| `Item(Index: Object)` | `Layout` |

### <a name="linesegment"></a>`LineSegment`

| Propriedade | Tipo | Acesso |
|-------------|------|--------|
| `Application` | `Application` | get |
| `AttributeSets` | `Object` | get |
| `IsAttributeSetPresent[Name: String]` | `Boolean` | get |
| `Parent` | `Object` | get |
| `Type` | `ObjectType` | get |

| Método | Retorno |
|--------|---------|
| `GetEndPoint([out] x: Double, [out] y: Double, [out] z: Double)` | `void` |
| `GetStartPoint([out] x: Double, [out] y: Double, [out] z: Double)` | `void` |
| `SetEndPoint([ref] x: Double, [ref] y: Double, [ref] z: Double)` | `void` |
| `SetStartPoint([ref] x: Double, [ref] y: Double, [ref] z: Double)` | `void` |
| `SplitAtPoint(x: Double, y: Double, z: Double, [opt] [out] OtherSplitElement: Object)` | `void` |

### <a name="linesegments"></a>`LineSegments`

| Propriedade | Tipo | Acesso |
|-------------|------|--------|
| `Application` | `Application` | get |
| `Count` | `Int32` | get |
| `Parent` | `Object` | get |

| Método | Retorno |
|--------|---------|
| `Add([ref] StartPoint: Array, [ref] EndPoint: Array)` | `LineSegment` |
| `GetEnumerator()` | `IEnumerator` |
| `Item(Index: Object)` | `LineSegment` |

### <a name="occurrence"></a>`Occurrence`

| Propriedade | Tipo | Acesso |
|-------------|------|--------|
| `Activate` | `Boolean` | get/set |
| `Adjustable` | `Boolean` | get/set |
| `Application` | `Object` | get |
| `AttributeSets` | `Object` | get |
| `Body` | `Object` | get |
| `CoordinateSystemsVisible` | `Boolean` | get/set |
| `CustomPropertyValue[CustomPropertyName: String]` | `String` | get/set |
| `DisplayAnnotations` | `Boolean` | get/set |
| `DisplayAsLastSaved` | `Boolean` | get/set |
| `DisplayAsReference` | `Boolean` | get/set |
| `DisplayCenterline` | `Boolean` | get/set |
| `DisplayConstrCurves` | `Boolean` | get/set |
| `DisplayConstructions` | `Boolean` | get/set |
| `DisplayCoordinateSystems` | `Boolean` | get/set |
| `DisplayDesignBody` | `Boolean` | get/set |
| `DisplayDimensions` | `Boolean` | get/set |
| `DisplayInDrawings` | `Boolean` | get/set |
| `DisplayInSubAssembly` | `Boolean` | get/set |
| `DisplayLiveSections` | `Boolean` | get/set |
| `DisplayReferenceAxes` | `Boolean` | get/set |
| `DisplayReferencePlanes` | `Boolean` | get/set |
| `DisplaySketches` | `Boolean` | get/set |
| `FaceStyle` | `Object` | get/set |
| `HasBodyOverride` | `Boolean` | get |
| `HasNongraphicQuantity` | `Boolean` | get |
| `HasUserDefinedName` | `Boolean` | get |
| `IncludeInBom` | `Boolean` | get/set |
| `IncludeInInterference` | `Boolean` | get/set |
| `IncludeInPhysicalProperties` | `Boolean` | get/set |
| `Index` | `Int32` | get |
| `IsAdjustablePart` | `Boolean` | get |
| `IsAttributeSetPresent[Name: String]` | `Boolean` | get |
| `IsCopy` | `Boolean` | get |
| `IsFamilyOfAssembly` | `Boolean` | get |
| `IsFamilyOfParts` | `Boolean` | get |
| `IsFastenerSystemItem` | `Boolean` | get |
| `IsForeign` | `Boolean` | get |
| `IsNongraphic` | `Boolean` | get |
| `IsPatterned` | `Boolean` | get |
| `IsPatternItem` | `Boolean` | get |
| `IsPipeFitting` | `Boolean` | get |
| `IsPipeSegment` | `Boolean` | get |
| `IsStructuralFrameItem` | `Boolean` | get |
| `IsWire` | `Boolean` | get |
| `ItemNumber` | `Int32` | get/set |
| `Locatable` | `Boolean` | get/set |
| `Name` | `String` | get/set |
| `NodeType` | `ObjectType` | get |
| `NongraphicDescription` | `String` | get |
| `NongraphicPrecision` | `Int32` | get |
| `NongraphicQuantity` | `Double` | get/set |
| `OccurrenceDocument` | `Object` | get |
| `OccurrenceFileName` | `String` | get |
| `OccurrenceID` | `Int32` | get |
| `Parent` | `Object` | get |
| `PartDocument` | `Object` | get |
| `PartFileName` | `String` | get |
| `Quantity` | `Int32` | get/set |
| `ReferenceOnly` | `Boolean` | get/set |
| `ReferencePlanesVisible` | `Boolean` | get/set |
| `Relations3d` | `Object` | get |
| `SketchesVisible` | `Boolean` | get/set |
| `Status` | `OccurrenceStatusConstants` | get |
| `Style` | `String` | get/set |
| `Subassembly` | `Boolean` | get |
| `SubassemblyBodies` | `SubassemblyBodies` | get |
| `SubOccurrences` | `SubOccurrences` | get |
| `TopLevelDocument` | `AssemblyDocument` | get |
| `Type` | `ObjectType` | get |
| `UseSimplified` | `Boolean` | get/set |
| `Visible` | `Boolean` | get/set |

| Método | Retorno |
|--------|---------|
| `AddAlternateComponent(AlternateComponentFileName: String)` | `void` |
| `BindKeyToTopology(BodyOverride: Boolean, [ref] ReferenceKey: Array)` | `Object` |
| `CaptureRelationships(RelationshipCount: Int32, [ref] RelationshipsToCapture: Array)` | `void` |
| `ClearCapturedRelationships()` | `void` |
| `CreateTopologyReference([ref] ReferenceKey: Array, [out] TopologyReference: TopologyReference)` | `void` |
| `CreateTopologyReferenceToBodyOverride([ref] ReferenceKey: Array, [out] TopologyReference: TopologyReference)` | `void` |
| `Delete()` | `void` |
| `DeleteHoleLocation()` | `void` |
| `FileMissing()` | `Boolean` |
| `FrameSaveAs(FileName: String)` | `void` |
| `FrameSaveAsBiDM(FilePath: String, DocumentNumber: String, Revision: String, Title: String)` | `String` |
| `FrameSaveAsTranslated(FileName: String)` | `void` |
| `GetAdjustablePart()` | `AdjustablePart` |
| `GetAllAlternateComponents([out] AlternateComponentCount: Int32, [out] AlternateComponentFileNames: Array)` | `void` |
| `GetBodyInversionMatrix([out] InvMatrix: Array)` | `void` |
| `GetExplodeMatrix([out] Matrix: Array)` | `void` |
| `GetFaceStyle2(vbHonourPrefs: Boolean)` | `Object` |
| `GetInterpartDrivenOccurrences([out] NumDrivenOccurrences: Int32, [out] DrivenOccurrences: Array)` | `void` |
| `GetInterpartDrivingOccurrences([out] NumDrivingOccurrences: Int32, [out] DrivingOccurrences: Array)` | `void` |
| `GetMatrix([out] Matrix: Array)` | `void` |
| `GetOrigin([out] OriginX: Double, [out] OriginY: Double, [out] OriginZ: Double)` | `void` |
| `GetRangeBox([out] MinRangePoint: Array, [out] MaxRangePoint: Array)` | `void` |
| `GetReferenceKey([out] ReferenceKey: Array, [opt] [out] KeySize: Object)` | `void` |
| `GetSimplifiedBodies([out] NumBodies: Int32, [out] SimplifiedBodies: Array)` | `void` |
| `GetSimplifiedBodies2([out] NumBodies: Int32, [out] SimplifiedBodies: Array, [out] Transforms: Array)` | `void` |
| `GetStyleNone()` | `Boolean` |
| `GetStyleUsePartStyle()` | `Boolean` |
| `GetTransform([out] OriginX: Double, [out] OriginY: Double, [out] OriginZ: Double, [out] AngleX: Double, [out] AngleY: Double, [out] AngleZ: Double)` | `void` |
| `GetTube()` | `Tube` |
| `IsAlternateComponent()` | `Boolean` |
| `IsTube()` | `Boolean` |
| `MakeAdjustablePart()` | `AdjustablePart` |
| `MakeWritable()` | `void` |
| `Mirror(pPlane: Object)` | `void` |
| `Move(DeltaX: Double, DeltaY: Double, DeltaZ: Double)` | `void` |
| `PutMatrix([ref] Matrix: Array, Replace: Boolean)` | `void` |
| `PutOrigin(OriginX: Double, OriginY: Double, OriginZ: Double)` | `void` |
| `PutStyleNone()` | `void` |
| `PutStyleUsePartStyle()` | `void` |
| `PutTransform(OriginX: Double, OriginY: Double, OriginZ: Double, AngleX: Double, AngleY: Double, AngleZ: Double)` | `void` |
| `Range([out] x_min: Double, [out] y_min: Double, [out] z_min: Double, [out] x_max: Double, [out] y_max: Double, [out] z_max: Double)` | `void` |
| `RecheckMissingFile()` | `Boolean` |
| `RemoveAllAlternateComponents()` | `void` |
| `RemoveAlternateComponent(AlternateComponentFileName: String)` | `void` |
| `RemoveOverrideBody()` | `void` |
| `Replace(NewOccurrenceFileName: String, ReplaceAll: Boolean, [opt] NewFamilyMemberName: Object)` | `void` |
| `ResetName()` | `void` |
| `RetrieveHoleLocation()` | `void` |
| `Rotate(AxisX1: Double, AxisY1: Double, AxisZ1: Double, AxisX2: Double, AxisY2: Double, AxisZ2: Double, Angle: Double)` | `void` |
| `Select(Replace: Boolean)` | `void` |
| `SwapFamilyMember(MemberName: String, SwapAllOccurrences: Boolean)` | `void` |

### <a name="occurrences"></a>`Occurrences`

| Propriedade | Tipo | Acesso |
|-------------|------|--------|
| `Application` | `Object` | get |
| `AttributeSets` | `Object` | get |
| `Count` | `Int32` | get |
| `IsAttributeSetPresent[Name: String]` | `Boolean` | get |
| `Parent` | `Object` | get |
| `Type` | `ObjectType` | get |

| Método | Retorno |
|--------|---------|
| `AddAsAdjustablePart(OccurrenceFileName: String)` | `Occurrence` |
| `AddByFilename(OccurrenceFileName: String, [opt] UseSimplifiedPart: Object)` | `Occurrence` |
| `AddByTemplate(OccurrenceFileName: String, [opt] TemplateFileName: Object)` | `Occurrence` |
| `AddFamilyByFilename(OccurrenceFileName: String, MemberName: String)` | `Occurrence` |
| `AddFamilyWithMatrix(OccurrenceFileName: String, [ref] Matrix: Array, MemberName: String)` | `Occurrence` |
| `AddFamilyWithTransform(OccurrenceFileName: String, OriginX: Double, OriginY: Double, OriginZ: Double, AngleX: Double, AngleY: Double, AngleZ: Double, MemberName: String)` | `Occurrence` |
| `AddTube([ref] TubeSegments: Array, [ref] PartFileName: String, [opt] TemplateFileName: Object, [opt] IsSolid: Object, [opt] Material: Object, [opt] BendRadius: Object, [opt] OuterDiameter: Object, [opt] MinimumFlatLength: Object, [opt] WallThickness: Object, [opt] ExtendStart: Object, [opt] ExtendEnd: Object, [opt] AddConnectRelations: Object, [opt] StartEndTreatmentType: Object, [opt] StartEndTreatmentInsideDiameter: Object, [opt] StartEndTreatmentOutsideDiameter: Object, [opt] StartEndTreatmentRadius: Object, [opt] StartEndTreatmentDepth: Object, [opt] StartEndTreatmentAngle: Object, [opt] EndEndTreatmentType: Object, [opt] EndEndTreatmentInsideDiameter: Object, [opt] EndEndTreatmentOutsideDiameter: Object, [opt] EndEndTreatmentRadius: Object, [opt] EndEndTreatmentDepth: Object, [opt] EndEndTreatmentAngle: Object)` | `Occurrence` |
| `AddTubeBIDM([ref] TubeSegments: Array, FilePath: String, DocumentNumber: String, RevisionID: String, Title: String, [out] NewFileName: String, [opt] TemplateFileName: Object, [opt] IsSolid: Object, [opt] Material: Object, [opt] BendRadius: Object, [opt] OuterDiameter: Object, [opt] MinimumFlatLength: Object, [opt] WallThickness: Object, [opt] ExtendStart: Object, [opt] ExtendEnd: Object, [opt] AddConnectRelations: Object, [opt] StartEndTreatmentType: Object, [opt] StartEndTreatmentInsideDiameter: Object, [opt] StartEndTreatmentOutsideDiameter: Object, [opt] StartEndTreatmentRadius: Object, [opt] StartEndTreatmentDepth: Object, [opt] StartEndTreatmentAngle: Object, [opt] EndEndTreatmentType: Object, [opt] EndEndTreatmentInsideDiameter: Object, [opt] EndEndTreatmentOutsideDiameter: Object, [opt] EndEndTreatmentRadius: Object, [opt] EndEndTreatmentDepth: Object, [opt] EndEndTreatmentAngle: Object)` | `Occurrence` |
| `AddWithMatrix(OccurrenceFileName: String, [ref] Matrix: Array)` | `Occurrence` |
| `AddWithTransform(OccurrenceFileName: String, OriginX: Double, OriginY: Double, OriginZ: Double, AngleX: Double, AngleY: Double, AngleZ: Double)` | `Occurrence` |
| `GetEnumerator()` | `IEnumerator` |
| `GetOccurrence(ID: Int32)` | `Occurrence` |
| `Item(Index: Object)` | `Occurrence` |
| `ReorderOccurrence(OccurrenceToReorder: Object, TargetOccurrence: Object, AfterTarget: Boolean)` | `void` |

### <a name="part"></a>`Part`

| Propriedade | Tipo | Acesso |
|-------------|------|--------|
| `Application` | `Object` | get |
| `AttributeSets` | `Object` | get |
| `DisplayInDrawings` | `Boolean` | get/set |
| `DisplayInSubAssembly` | `Boolean` | get/set |
| `FaceStyle` | `Object` | get/set |
| `IncludeInBom` | `Boolean` | get/set |
| `IncludeInPhysicalProperties` | `Boolean` | get/set |
| `Index` | `Int32` | get |
| `IsAttributeSetPresent[Name: String]` | `Boolean` | get |
| `IsForeign` | `Boolean` | get |
| `Locatable` | `Boolean` | get/set |
| `Name` | `String` | get/set |
| `Parent` | `Object` | get |
| `PartDocument` | `Object` | get |
| `PartFileName` | `String` | get |
| `Quantity` | `Int32` | get/set |
| `ReferenceOnly` | `Boolean` | get/set |
| `Relations3d` | `Object` | get |
| `Status` | `PartStatusConstants` | get |
| `Style` | `String` | get/set |
| `Subassembly` | `Boolean` | get |
| `Type` | `ObjectType` | get |
| `Visible` | `Boolean` | get/set |

| Método | Retorno |
|--------|---------|
| `Delete()` | `void` |
| `GetMatrix([out] Matrix: Array)` | `void` |
| `GetOrigin([out] OriginX: Double, [out] OriginY: Double, [out] OriginZ: Double)` | `void` |
| `GetTransform([out] OriginX: Double, [out] OriginY: Double, [out] OriginZ: Double, [out] AngleX: Double, [out] AngleY: Double, [out] AngleZ: Double)` | `void` |
| `MakeWritable()` | `void` |
| `Move(DeltaX: Double, DeltaY: Double, DeltaZ: Double)` | `void` |
| `PutMatrix([ref] Matrix: Array, Replace: Boolean)` | `void` |
| `PutOrigin(OriginX: Double, OriginY: Double, OriginZ: Double)` | `void` |
| `PutTransform(OriginX: Double, OriginY: Double, OriginZ: Double, AngleX: Double, AngleY: Double, AngleZ: Double)` | `void` |
| `Replace(NewPartFileName: String, ReplaceAll: Boolean)` | `void` |
| `Rotate(AxisX1: Double, AxisY1: Double, AxisZ1: Double, AxisX2: Double, AxisY2: Double, AxisZ2: Double, Angle: Double)` | `void` |
| `Select(Replace: Boolean)` | `void` |

### <a name="parts"></a>`Parts`

| Propriedade | Tipo | Acesso |
|-------------|------|--------|
| `Application` | `Object` | get |
| `Count` | `Int32` | get |
| `Parent` | `Object` | get |

| Método | Retorno |
|--------|---------|
| `AddByFilename(PartFileName: String)` | `Part` |
| `AddWithMatrix(PartFileName: String, [ref] Matrix: Array)` | `Part` |
| `AddWithTransform(PartFileName: String, OriginX: Double, OriginY: Double, OriginZ: Double, AngleX: Double, AngleY: Double, AngleZ: Double)` | `Part` |
| `GetEnumerator()` | `IEnumerator` |
| `Item(Index: Object)` | `Part` |

### <a name="path"></a>`Path`

| Propriedade | Tipo | Acesso |
|-------------|------|--------|
| `Application` | `Application` | get |
| `EndParentEdge` | `Object` | get |
| `EndpointEndCondition` | `KeypointEndConditionConstants` | get |
| `EndpointMagnitude` | `Double` | get |
| `Name` | `String` | get |
| `Parent` | `Object` | get |
| `StartParentEdge` | `Object` | get |
| `StartpointEndCondition` | `KeypointEndConditionConstants` | get |
| `StartpointMagnitude` | `Double` | get |
| `Status` | `Int32` | get |

| Método | Retorno |
|--------|---------|
| `Delete()` | `void` |
| `EditSpacePoint(KeypointIndexToEdit: Int32, XPos: Double, YPos: Double, ZPos: Double)` | `void` |
| `GetAllPoints([out] NumPoints: Int32, [out] PointPositions: Array)` | `void` |
| `GetEndPoints([out] EndPointPositions: Array)` | `void` |
| `GetFixedLength([out] pdCurveLength: Double, [out] pFixedLengthConstraintDirection: SEFixedLengthConstraintDirection, [out] pConstraintGeometry: Object)` | `void` |
| `GetLength([out] PathLength: Double)` | `void` |
| `InsertKeyPoint(EdgeToInsert: Object, KeypointConstant: KeyPointType, KeypointIndexToInsertBefore: Int32)` | `void` |
| `InsertSpacePoint(XPos: Double, YPos: Double, ZPos: Double, KeypointIndexToInsertBefore: Int32)` | `void` |
| `InserttCylinderAxis(CylinderFace: Object, CylinderEdge: Object, KeypointIndexToInsertBefore: Int32)` | `void` |
| `RemovePoint(KeypointIndex: Int32)` | `void` |
| `SetFixedLength(dCurveLength: Double, FixedLengthConstraintDirection: SEFixedLengthConstraintDirection, ConstraintGeometry: Object)` | `void` |
| `SplitPath(XPos: Double, YPos: Double, ZPos: Double, [out] pNewPath: Object)` | `void` |

### <a name="pathrelation3d"></a>`PathRelation3d`

| Propriedade | Tipo | Acesso |
|-------------|------|--------|
| `Application` | `Object` | get |
| `AttributeSets` | `Object` | get |
| `DetailedStatus` | `Relation3dDetailedStatusConstants` | get |
| `Index` | `Int32` | get |
| `IsAttributeSetPresent[Name: String]` | `Boolean` | get |
| `Occurrence1` | `Occurrence` | get |
| `Occurrence2` | `Occurrence` | get |
| `Parent` | `Object` | get |
| `Status` | `Relation3dStatusConstants` | get |
| `Suppress` | `Boolean` | get/set |
| `Type` | `ObjectType` | get |

| Método | Retorno |
|--------|---------|
| `AddSuppressionVariable()` | `SuppressVariable` |
| `Delete()` | `void` |
| `DeleteSuppressionVariable()` | `void` |
| `GetSuppressionVariable()` | `SuppressVariable` |
| `HasSuppressionVariable()` | `Boolean` |
| `Select(Replace: Boolean)` | `void` |

### <a name="paths"></a>`Paths`

| Propriedade | Tipo | Acesso |
|-------------|------|--------|
| `Application` | `Application` | get |
| `Count` | `Int32` | get |
| `Parent` | `Object` | get |

| Método | Retorno |
|--------|---------|
| `Add(NumPoints: Int32, [ref] PointTypeConstants: Array, [ref] EdgeSet: Array, [ref] KeyPointTypeConstants: Array, [ref] Array: Array, StartpointEndType: KeypointEndConditionConstants, [opt] EndpointEndType: Object)` | `Path` |
| `GetEnumerator()` | `IEnumerator` |
| `Item(Index: Object)` | `Path` |

### <a name="physicalproperties"></a>`PhysicalProperties`

| Propriedade | Tipo | Acesso |
|-------------|------|--------|
| `Application` | `Object` | get |
| `DisplayCenterOfMass` | `Boolean` | get/set |
| `DisplayCenterOfVolume` | `Boolean` | get/set |
| `DisplayPrincipalAxes` | `Boolean` | get/set |
| `IsUpToDate` | `Boolean` | get |
| `Parent` | `Object` | get |
| `UpdateOnFileSaveStatus` | `Boolean` | get/set |
| `UseQuantityOverrideMass` | `Boolean` | get/set |
| `UserDefinedPropertiesStatus` | `Boolean` | get/set |

| Método | Retorno |
|--------|---------|
| `GetAsmPhysicalPropsRelativeToCoordSystem(CoordinateSystem: Object, [out] Mass: Double, [out] Volume: Double, [out] Area: Double, [out] CenterOfMass: Array, [out] CenterOfVolume: Array, [out] GlobalMoments: Array, [out] PrincipalAxis1: Array, [out] PrincipalAxis2: Array, [out] PrincipalAxis3: Array, [out] PrincipalMoments: Array, [out] RadiiOfGyration: Array, [out] IsSick: Boolean, [out] UpdateStatus: Boolean)` | `void` |
| `GetAssemblyPhysicalProperties([out] Mass: Double, [out] Volume: Double, [out] Area: Double, [out] CenterOfMass: Array, [out] CenterOfVolume: Array, [out] GlobalMoments: Array, [out] PrincipalAxis1: Array, [out] PrincipalAxis2: Array, [out] PrincipalAxis3: Array, [out] PrincipalMoments: Array, [out] RadiiOfGyration: Array, [out] IsSick: Boolean, [out] UpdateStatus: Boolean)` | `void` |
| `GetAssemblyPhysicalProperties1([out] Mass: Double, [out] Volume: Double, [out] Area: Double, [out] CenterOfMass: Array, [out] CenterOfVolume: Array, [out] GlobalMoments: Array, [out] PrincipalAxis1: Array, [out] PrincipalAxis2: Array, [out] PrincipalAxis3: Array, [out] PrincipalMoments: Array, [out] RadiiOfGyration: Array, [out] IsSick: Boolean, [out] UpdateStatus: Boolean, [out] OverrideMass: Double)` | `void` |
| `GetSelectSetPhysicalProperties([out] Mass: Double, [out] Volume: Double, [out] Area: Double, [out] CenterOfMass: Array, [out] CenterOfVolume: Array, [out] GlobalMoments: Array, [out] PrincipalAxis1: Array, [out] PrincipalAxis2: Array, [out] PrincipalAxis3: Array, [out] PrincipalMoments: Array, [out] RadiiOfGyration: Array)` | `void` |
| `GetSelectSetPhysicalPropsRelativeToCoordSystem(CoordinateSystem: Object, [out] Mass: Double, [out] Volume: Double, [out] Area: Double, [out] CenterOfMass: Array, [out] CenterOfVolume: Array, [out] GlobalMoments: Array, [out] PrincipalAxis1: Array, [out] PrincipalAxis2: Array, [out] PrincipalAxis3: Array, [out] PrincipalMoments: Array, [out] RadiiOfGyration: Array)` | `void` |
| `SetUserDefinedAssemblyPhysicalProperties([ref] Volume: Double, [ref] Area: Double, [ref] Mass: Double, [ref] CenterOfMass: Array, [ref] CenterOfVolume: Array, [ref] GlobalMoments: Array, [ref] PrincipalMoments: Array, [ref] PrincipalAxis1: Array, [ref] PrincipalAxis2: Array, [ref] PrincipalAxis3: Array, [ref] RadiiOfGyration: Array, [ref] OverrideMass: Double)` | `void` |
| `Update()` | `void` |
| `UpdateV2([out] ParFileNamesWithoutDensity: Array)` | `void` |
| `WriteToFile(FileName: String)` | `void` |

### <a name="pipe"></a>`Pipe`

| Propriedade | Tipo | Acesso |
|-------------|------|--------|
| `Application` | `Application` | get |
| `DefaultEndTreatment` | `PipeFittingEndTreatmentConstants` | get/set |
| `Name` | `String` | get/set |
| `Parent` | `Object` | get |
| `PipeFittingStandard` | `String` | get/set |
| `PipeSegmentStandard` | `String` | get/set |
| `Visible` | `Boolean` | get/set |

| Método | Retorno |
|--------|---------|
| `Delete()` | `void` |
| `GetPipeFittings([out] NumPipeFittings: Int32, [out] PipeFittingOccurrences: Array, [out] FittingTypes: Array)` | `void` |
| `GetPipeLengths([out] NumPipeSegments: Int32, [out] PipeSegmentOccurrences: Array, [out] LineSegments: Array, [out] PipeSegmentLength: Array)` | `void` |
| `GetPipeSegments([out] NumPipeSegments: Int32, [out] PipeSegmentOccurrences: Array)` | `void` |
| `RotateFittings(PipeFittingOccurrences: Array, dValue: Double)` | `void` |

### <a name="pipes"></a>`Pipes`

| Propriedade | Tipo | Acesso |
|-------------|------|--------|
| `Application` | `Application` | get |
| `Count` | `Int32` | get |
| `Parent` | `Object` | get |

| Método | Retorno |
|--------|---------|
| `Add(dPipeDiameter: Double, NumPaths: Int32, [ref] Path: Array, bAllowGradient: Boolean, dGradientValue: Double, PipeFileName: String, ElbowFileName: String, TeeFileName: String, CrossFileName: String, CouplingFileName: String)` | `Pipe` |
| `GetEnumerator()` | `IEnumerator` |
| `Item(Index: Object)` | `Pipe` |

### <a name="planarrelation3d"></a>`PlanarRelation3d`

| Propriedade | Tipo | Acesso |
|-------------|------|--------|
| `Application` | `Object` | get |
| `AttributeSets` | `Object` | get |
| `DetailedStatus` | `Relation3dDetailedStatusConstants` | get |
| `FixedOffset` | `Boolean` | get/set |
| `Index` | `Int32` | get |
| `IsAttributeSetPresent[Name: String]` | `Boolean` | get |
| `NormalsAligned` | `Boolean` | get/set |
| `Occurrence1` | `Occurrence` | get |
| `Occurrence2` | `Occurrence` | get |
| `Offset` | `Double` | get/set |
| `Parent` | `Object` | get |
| `Part1` | `Part` | get |
| `Part2` | `Part` | get |
| `RangedOffset` | `Boolean` | get/set |
| `RangeHigh` | `Double` | get/set |
| `RangeLow` | `Double` | get/set |
| `Status` | `Relation3dStatusConstants` | get |
| `Suppress` | `Boolean` | get/set |
| `Type` | `ObjectType` | get |

| Método | Retorno |
|--------|---------|
| `AddSuppressionVariable()` | `SuppressVariable` |
| `Delete()` | `void` |
| `DeleteSuppressionVariable()` | `void` |
| `GetElement1([out] IsTopologyReference: Boolean)` | `Object` |
| `GetElement2([out] IsTopologyReference: Boolean)` | `Object` |
| `GetGeometry1([out] GeometryType: Int32, [out] PointX: Double, [out] PointY: Double, [out] PointZ: Double, [out] VectorX: Double, [out] VectorY: Double, [out] VectorZ: Double)` | `void` |
| `GetGeometry2([out] GeometryType: Int32, [out] PointX: Double, [out] PointY: Double, [out] PointZ: Double, [out] VectorX: Double, [out] VectorY: Double, [out] VectorZ: Double)` | `void` |
| `GetSuppressionVariable()` | `SuppressVariable` |
| `HasSuppressionVariable()` | `Boolean` |
| `Select(Replace: Boolean)` | `void` |

### <a name="pointrelation3d"></a>`PointRelation3d`

| Propriedade | Tipo | Acesso |
|-------------|------|--------|
| `Application` | `Object` | get |
| `AttributeSets` | `Object` | get |
| `DetailedStatus` | `Relation3dDetailedStatusConstants` | get |
| `Index` | `Int32` | get |
| `IsAttributeSetPresent[Name: String]` | `Boolean` | get |
| `Occurrence1` | `Occurrence` | get |
| `Occurrence2` | `Occurrence` | get |
| `Parent` | `Object` | get |
| `Part1` | `Part` | get |
| `Part2` | `Part` | get |
| `RangedOffset` | `Boolean` | get/set |
| `RangeHigh` | `Double` | get/set |
| `RangeLow` | `Double` | get/set |
| `Status` | `Relation3dStatusConstants` | get |
| `Suppress` | `Boolean` | get/set |
| `Type` | `ObjectType` | get |

| Método | Retorno |
|--------|---------|
| `AddSuppressionVariable()` | `SuppressVariable` |
| `Delete()` | `void` |
| `DeleteSuppressionVariable()` | `void` |
| `GetElement1([out] IsTopologyReference: Boolean)` | `Object` |
| `GetElement2([out] IsTopologyReference: Boolean)` | `Object` |
| `GetGeometry1([out] GeometryType: Int32, [out] PointX: Double, [out] PointY: Double, [out] PointZ: Double, [out] VectorX: Double, [out] VectorY: Double, [out] VectorZ: Double)` | `void` |
| `GetGeometry2([out] GeometryType: Int32, [out] PointX: Double, [out] PointY: Double, [out] PointZ: Double, [out] VectorX: Double, [out] VectorY: Double, [out] VectorZ: Double)` | `void` |
| `GetSuppressionVariable()` | `SuppressVariable` |
| `HasSuppressionVariable()` | `Boolean` |
| `Select(Replace: Boolean)` | `void` |

### <a name="queries"></a>`Queries`

| Propriedade | Tipo | Acesso |
|-------------|------|--------|
| `Application` | `Application` | get |
| `Count` | `Int32` | get |
| `Parent` | `Object` | get |
| `QueryName[Query: Query]` | `String` | get |
| `QuickQuery` | `Query` | get |

| Método | Retorno |
|--------|---------|
| `Add(QueryName: String)` | `Query` |
| `Cut(QueryName: String)` | `void` |
| `GetEnumerator()` | `IEnumerator` |
| `Item(Index: Object)` | `Query` |
| `Paste(QueryName: String)` | `Query` |
| `Remove(QueryName: String)` | `void` |
| `Rename(OldQueryName: String, NewQueryName: String)` | `void` |

### <a name="query"></a>`Query`

| Propriedade | Tipo | Acesso |
|-------------|------|--------|
| `Application` | `Application` | get |
| `Criteria` | `Object` | get |
| `CriteriaCount` | `Int32` | get |
| `LoadSubassemblies` | `Boolean` | get/set |
| `MatchesCount` | `Int32` | get |
| `Parent` | `Object` | get |
| `Scope` | `QueryScopeConstants` | get/set |
| `SearchSubassemblies` | `Boolean` | get/set |

| Método | Retorno |
|--------|---------|
| `AddCriteria(QueryProperty: QueryPropertyConstants, CustomPropertyName: String, QueryCondition: QueryConditionConstants, Value: Object)` | `void` |
| `CopyToClipboard()` | `void` |
| `PopulateFromClipboard()` | `void` |
| `RemoveAllCriteria()` | `void` |
| `RemoveCriteria(QueryProperty: QueryPropertyConstants, CustomPropertyName: String, QueryCondition: QueryConditionConstants, Value: Object)` | `void` |

### <a name="relations3d"></a>`Relations3d`

| Propriedade | Tipo | Acesso |
|-------------|------|--------|
| `Application` | `Object` | get |
| `Count` | `Int32` | get |
| `Parent` | `Object` | get |

| Método | Retorno |
|--------|---------|
| `AddAngular(Element1: Object, Element2: Object, ReverseElement1Direction: Boolean, ReverseElement2Direction: Boolean, MeasureElement1: Object, MeasureElement2: Object, Angle: Double, MeasureToPositiveSide: Boolean, MeasureFromPositiveSide: Boolean, MeasureCCW: Boolean)` | `AngularRelation3d` |
| `AddAxial(Axis1: Object, Axis2: Object, NormalsAligned: Boolean)` | `AxialRelation3d` |
| `AddAxialWithParallelOffset(Axis1: Object, Axis2: Object, FixedOffset: Boolean, Offset: Double)` | `AxialRelation3d` |
| `AddBarrelCam(ConstrainingFollowerElement: Object, [ref] ConstrainingCamEdges: Array)` | `CamFollowerRelation3d` |
| `AddCenterPlane(PlacementElement1: Object, PlacementElement2: Object, TargetElement1: Object, TargetElement2: Object, [ref] PlacementConstrainingPoint1: Array, [ref] PlacementConstrainingPoint2: Array, [ref] TargetConstrainingPoint1: Array, [ref] TargetConstrainingPoint2: Array)` | `CenterPlaneRelation3d` |
| `AddGear(Element1: Object, Element2: Object, GearType: Relation3dGearTypeConstants, RatioType: Relation3dGearRatioTypeConstants, GearRatio1: Double, GearRatio2: Double, Flip: Boolean)` | `GearRelation3d` |
| `AddGround(Occurrence: Occurrence)` | `Object` |
| `AddPath(ConstrainingFollowerElement: Object, [ref] ConstrainingCamEdges: Array)` | `PathRelation3d` |
| `AddPlanar(Plane1: Object, Plane2: Object, NormalsAligned: Boolean, [ref] ConstrainingPoint1: Array, [ref] ConstrainingPoint2: Array)` | `PlanarRelation3d` |
| `AddPlanarCam(ConstrainingFollowerElement: Object, [ref] ConstrainingCamFaces: Array)` | `CamFollowerRelation3d` |
| `AddPoint(PointGeometry: Object, PointKeyPoint: Relation3dGeometryConstants, ConnectGeometry: Object, [opt] ConnectKeyPoint: Object)` | `PointRelation3d` |
| `AddPointWithInferredGeometry(PointGeometry: Object, SketchPoint: Object)` | `PointRelation3d` |
| `AddRigidSet(OccurrenceCount: Int32, [ref] Occurrences: Array)` | `RigidSetRelation3d` |
| `AddTangent(Element1: Object, Element2: Object, [ref] ConstrainingPoint1: Array, [ref] ConstrainingPoint2: Array, Offset: Double, IsHalfSpacePositive: Boolean)` | `TangentRelation3d` |
| `GetEnumerator()` | `IEnumerator` |
| `Item(Index: Object)` | `Object` |

### <a name="rigidsetrelation3d"></a>`RigidSetRelation3d`

| Propriedade | Tipo | Acesso |
|-------------|------|--------|
| `Application` | `Object` | get |
| `AttributeSets` | `Object` | get |
| `DetailedStatus` | `Relation3dDetailedStatusConstants` | get |
| `Index` | `Int32` | get |
| `IsAttributeSetPresent[Name: String]` | `Boolean` | get |
| `IsMember[Occurrence: Object]` | `Boolean` | get |
| `OccurrenceCount` | `Int32` | get |
| `Occurrences` | `Array` | get |
| `Parent` | `Object` | get |
| `Status` | `Relation3dStatusConstants` | get |
| `Suppress` | `Boolean` | get/set |
| `Type` | `ObjectType` | get |

| Método | Retorno |
|--------|---------|
| `AddOccurrence(Occurrence: Object)` | `void` |
| `AddSuppressionVariable()` | `SuppressVariable` |
| `Delete()` | `void` |
| `DeleteSuppressionVariable()` | `void` |
| `GetSuppressionVariable()` | `SuppressVariable` |
| `HasSuppressionVariable()` | `Boolean` |
| `RemoveOccurrence(Occurrence: Object)` | `void` |
| `Select(Replace: Boolean)` | `void` |

### <a name="segmentangularrelation3d"></a>`SegmentAngularRelation3d`

| Propriedade | Tipo | Acesso |
|-------------|------|--------|
| `Angle` | `Double` | get/set |
| `AngleCounterclockwise` | `Boolean` | get |
| `AngleFromPositiveDirection` | `Boolean` | get |
| `AngleToPositiveDirection` | `Boolean` | get |
| `Application` | `Object` | get |
| `AttributeSets` | `Object` | get |
| `Dimension` | `Object` | get |
| `Index` | `Int32` | get |
| `IsAttributeSetPresent[Name: String]` | `Boolean` | get |
| `Parent` | `Object` | get |
| `Status` | `SegmentRelation3dStatusConstants` | get |
| `Type` | `ObjectType` | get |

| Método | Retorno |
|--------|---------|
| `Delete()` | `void` |
| `GetElement1([out] Element1: Object, [out] Type: ObjectType, [out] GeometryType: SegmentRelation3dGeometryConstants)` | `void` |
| `GetElement2([out] Element2: Object, [out] Type: ObjectType, [out] GeometryType: SegmentRelation3dGeometryConstants)` | `void` |
| `Select(Replace: Boolean)` | `void` |

### <a name="segmentdirectionrelation3d"></a>`SegmentDirectionRelation3d`

| Propriedade | Tipo | Acesso |
|-------------|------|--------|
| `Application` | `Object` | get |
| `AttributeSets` | `Object` | get |
| `DirectionType` | `SegmentRelation3dDirectionConstants` | get |
| `Index` | `Int32` | get |
| `IsAttributeSetPresent[Name: String]` | `Boolean` | get |
| `Parent` | `Object` | get |
| `Status` | `SegmentRelation3dStatusConstants` | get |
| `Type` | `ObjectType` | get |

| Método | Retorno |
|--------|---------|
| `Delete()` | `void` |
| `GetElement1([out] Element1: Object, [out] Type: ObjectType, [out] GeometryType: SegmentRelation3dGeometryConstants)` | `void` |
| `GetElement2([out] Element2: Object, [out] Type: ObjectType, [out] GeometryType: SegmentRelation3dGeometryConstants)` | `void` |
| `Select(Replace: Boolean)` | `void` |

### <a name="segmentdistancerelation3d"></a>`SegmentDistanceRelation3d`

| Propriedade | Tipo | Acesso |
|-------------|------|--------|
| `Application` | `Object` | get |
| `AttributeSets` | `Object` | get |
| `Dimension` | `Object` | get |
| `Distance` | `Double` | get/set |
| `DistanceType` | `SegmentRelation3dDistanceConstants` | get |
| `Index` | `Int32` | get |
| `IsAttributeSetPresent[Name: String]` | `Boolean` | get |
| `Parent` | `Object` | get |
| `Status` | `SegmentRelation3dStatusConstants` | get |
| `Type` | `ObjectType` | get |

| Método | Retorno |
|--------|---------|
| `Delete()` | `void` |
| `GetElement1([out] Element1: Object, [out] Type: ObjectType, [out] GeometryType: SegmentRelation3dGeometryConstants)` | `void` |
| `GetElement2([out] Element2: Object, [out] Type: ObjectType, [out] GeometryType: SegmentRelation3dGeometryConstants)` | `void` |
| `GetElement3([out] Element3: Object, [out] Type: ObjectType, [out] GeometryType: SegmentRelation3dGeometryConstants)` | `void` |
| `Select(Replace: Boolean)` | `void` |

### <a name="segmentpointrelation3d"></a>`SegmentPointRelation3d`

| Propriedade | Tipo | Acesso |
|-------------|------|--------|
| `Application` | `Object` | get |
| `AttributeSets` | `Object` | get |
| `Index` | `Int32` | get |
| `IsAttributeSetPresent[Name: String]` | `Boolean` | get |
| `Parent` | `Object` | get |
| `Status` | `SegmentRelation3dStatusConstants` | get |
| `Type` | `ObjectType` | get |

| Método | Retorno |
|--------|---------|
| `Delete()` | `void` |
| `GetElement1([out] Element1: Object, [out] Type: ObjectType, [out] GeometryType: SegmentRelation3dGeometryConstants)` | `void` |
| `GetElement2([out] Element2: Object, [out] Type: ObjectType, [out] GeometryType: SegmentRelation3dGeometryConstants)` | `void` |
| `Select(Replace: Boolean)` | `void` |

### <a name="segmentradiusrelation3d"></a>`SegmentRadiusRelation3d`

| Propriedade | Tipo | Acesso |
|-------------|------|--------|
| `Application` | `Object` | get |
| `ArcSegment` | `ArcSegment` | get |
| `AttributeSets` | `Object` | get |
| `Dimension` | `Object` | get |
| `Index` | `Int32` | get |
| `IsAttributeSetPresent[Name: String]` | `Boolean` | get |
| `Parent` | `Object` | get |
| `Radius` | `Double` | get/set |
| `Status` | `SegmentRelation3dStatusConstants` | get |
| `Type` | `ObjectType` | get |

| Método | Retorno |
|--------|---------|
| `Delete()` | `void` |
| `GetElement1([out] Element1: Object, [out] Type: ObjectType, [out] GeometryType: SegmentRelation3dGeometryConstants)` | `void` |
| `Select(Replace: Boolean)` | `void` |

### <a name="segmentrelations3d"></a>`SegmentRelations3d`

| Propriedade | Tipo | Acesso |
|-------------|------|--------|
| `Application` | `Object` | get |
| `Count` | `Int32` | get |
| `Parent` | `Object` | get |

| Método | Retorno |
|--------|---------|
| `AddSegmentAngular(Element1: Object, SegmentGeometryType1: SegmentRelation3dGeometryConstants, Element2: Object, SegmentGeometryType2: SegmentRelation3dGeometryConstants, Angle: Double, vbSegAngleFromPositiveDir: Boolean, vbSegAngleToPositiveDir: Boolean, vbSegAngleCCW: Boolean)` | `SegmentAngularRelation3d` |
| `AddSegmentDirection(Element1: Object, SegmentGeometryType1: SegmentRelation3dGeometryConstants, Element2: Object, SegmentGeometryType2: SegmentRelation3dGeometryConstants, SegmentDirectionType: SegmentRelation3dDirectionConstants)` | `SegmentDirectionRelation3d` |
| `AddSegmentDistance(Element1: Object, SegmentGeometryType1: SegmentRelation3dGeometryConstants, Element2: Object, SegmentGeometryType2: SegmentRelation3dGeometryConstants, RefPlane: Object, SegmentDistanceType: SegmentRelation3dDistanceConstants, Distance: Double)` | `SegmentDistanceRelation3d` |
| `AddSegmentPoint(Element1: Object, SegmentGeometryType1: SegmentRelation3dGeometryConstants, Element2: Object, SegmentGeometryType2: SegmentRelation3dGeometryConstants)` | `SegmentPointRelation3d` |
| `AddSegmentRadius(Element: Object, Radius: Double)` | `SegmentRadiusRelation3d` |
| `AddSegmentTangent(Element1: Object, Element2: Object)` | `SegmentTangentRelation3d` |
| `GetEnumerator()` | `IEnumerator` |
| `Item(Index: Object)` | `Object` |

### <a name="segments"></a>`Segments`

| Propriedade | Tipo | Acesso |
|-------------|------|--------|
| `Application` | `Application` | get |
| `Count` | `Int32` | get |
| `Parent` | `Object` | get |

| Método | Retorno |
|--------|---------|
| `GetEnumerator()` | `IEnumerator` |
| `Item(Index: Object)` | `Object` |

### <a name="segmenttangentrelation3d"></a>`SegmentTangentRelation3d`

| Propriedade | Tipo | Acesso |
|-------------|------|--------|
| `Application` | `Object` | get |
| `AttributeSets` | `Object` | get |
| `Index` | `Int32` | get |
| `IsAttributeSetPresent[Name: String]` | `Boolean` | get |
| `Parent` | `Object` | get |
| `Status` | `SegmentRelation3dStatusConstants` | get |
| `Type` | `ObjectType` | get |

| Método | Retorno |
|--------|---------|
| `Delete()` | `void` |
| `GetElement1([out] Element1: Object, [out] Type: ObjectType, [out] GeometryType: SegmentRelation3dGeometryConstants)` | `void` |
| `GetElement2([out] Element2: Object, [out] Type: ObjectType, [out] GeometryType: SegmentRelation3dGeometryConstants)` | `void` |
| `Select(Replace: Boolean)` | `void` |

### <a name="simplifiedassemblies"></a>`SimplifiedAssemblies`

| Propriedade | Tipo | Acesso |
|-------------|------|--------|
| `AddModel` | `SimplifiedAssembly` | get |
| `AddVisible[NumOccurrenceExclude: Int32, [ref] OccurrenceExclude: Array, dExcludeRangeRatio: Double, NumOccurrenceInclude: Int32, [ref] OccurrenceInclude: Array, CopyType: VisibilityBasedSimplifiedAssemblyCopyType]` | `SimplifiedAssembly` | get |
| `Application` | `Application` | get |
| `Count` | `Int32` | get |
| `Parent` | `Object` | get |

| Método | Retorno |
|--------|---------|
| `GetEnumerator()` | `IEnumerator` |
| `Item(Index: Object)` | `SimplifiedAssembly` |

### <a name="simplifiedassembly"></a>`SimplifiedAssembly`

| Propriedade | Tipo | Acesso |
|-------------|------|--------|
| `ActivateSimplifiedAssembly` | `Boolean` | get/set |
| `Application` | `Application` | get |
| `Parent` | `Object` | get |
| `SimplificationType` | `SimplifiedAssemblyMode` | get |
| `SimplifiedModel` | `Object` | get |
| `Type` | `ObjectType` | get |

| Método | Retorno |
|--------|---------|
| `DeleteSimplifiedAssembly()` | `void` |
| `EditVisibleFaces(NumOccurrenceExclude: Int32, [ref] OccurrenceExclude: Array, dExcludeRangeRatio: Double, NumOccurrenceInclude: Int32, [ref] OccurrenceInclude: Array, CopyType: VisibilityBasedSimplifiedAssemblyCopyType)` | `void` |
| `GetVisibleFaceInputs([out] NumOccurrenceExclude: Int32, [out] OccurrenceExclude: Array, [out] dExcludeRangeRatio: Double, [out] NumOccurrenceInclude: Int32, [out] OccurrenceInclude: Array, [out] CopyType: VisibilityBasedSimplifiedAssemblyCopyType)` | `void` |
| `SaveSimplifiedAssemblyAs(SaveFileName: String)` | `void` |
| `UpdateSimplifiedAssembly()` | `void` |

### <a name="splice"></a>`Splice`

| Propriedade | Tipo | Acesso |
|-------------|------|--------|
| `Application` | `Application` | get |
| `AssemblyProperty` | `AssemblyProperty` | get |
| `AttributeSets` | `Object` | get |
| `Color` | `Int32` | get/set |
| `Description` | `String` | get/set |
| `Gage` | `String` | get/set |
| `IsAttributeSetPresent[Name: String]` | `Boolean` | get |
| `Material` | `String` | get/set |
| `MaterialType` | `String` | get/set |
| `Name` | `String` | get/set |
| `OuterDiameter` | `Double` | get/set |
| `Parent` | `Object` | get |
| `PartNumber` | `String` | get/set |
| `Type` | `HarnessTypeConstants` | get |
| `Visible` | `Boolean` | get/set |

| Método | Retorno |
|--------|---------|
| `AddConductors(NumberOfConductors: Int32, [ref] ConductorArray: Array)` | `void` |
| `CreatePhysicalConductor()` | `void` |
| `Delete()` | `void` |
| `DeletePhysicalConductor()` | `void` |
| `Edit(dSpliceX: Double, dSpliceY: Double, dSpliceZ: Double, NumberOfConductors: Int32, [ref] ConductorArray: Array)` | `void` |
| `GetConductors([out] NumberOfConductors: Int32, [out] ConductorArray: Array)` | `void` |
| `GetOuterConductors([out] NumberOfConductors: Int32, [out] ConductorArray: Array)` | `void` |
| `GetSplicePosition([out] dSpliceX: Double, [out] dSpliceY: Double, [out] dSpliceZ: Double)` | `void` |
| `GetTopMostConductors([out] NumberOfConductors: Int32, [out] ConductorArray: Array)` | `void` |
| `Remove(NumberOfConductors: Int32, [ref] ConductorArray: Array)` | `void` |

### <a name="splices"></a>`Splices`

| Propriedade | Tipo | Acesso |
|-------------|------|--------|
| `Application` | `Application` | get |
| `Count` | `Int32` | get |
| `Parent` | `Object` | get |
| `Type` | `HarnessTypeConstants` | get |

| Método | Retorno |
|--------|---------|
| `Add(dSpliceX: Double, dSpliceY: Double, dSpliceZ: Double, NumberOfConductors: Int32, [ref] ConductorArray: Array, ConductorDescription: String)` | `Splice` |
| `GetEnumerator()` | `IEnumerator` |
| `Item(Index: Object)` | `Splice` |

### <a name="structuralframe"></a>`StructuralFrame`

| Propriedade | Tipo | Acesso |
|-------------|------|--------|
| `Application` | `Application` | get |
| `AutoPosition` | `Boolean` | get/set |
| `GlobalDocument` | `String` | get/set |
| `Name` | `String` | get/set |
| `Parent` | `Object` | get |
| `SingleFrameForCollinearPaths` | `Boolean` | get/set |
| `SingleFrameForTangentialPaths` | `Boolean` | get/set |
| `Visible` | `Boolean` | get/set |

| Método | Retorno |
|--------|---------|
| `AssociateLegacyFrameToCrossSection()` | `void` |
| `CutLength([out] dCutLength: Double, [opt] [out] bExactLength: Object)` | `void` |
| `DeleteHoleLocation()` | `void` |
| `FrameSaveAs(FileName: String)` | `void` |
| `FrameSaveAsBiDM(FilePath: String, DocumentNumber: String, Revision: String, Title: String)` | `String` |
| `FrameSaveAsTranslated(FileName: String)` | `void` |
| `GetFramePath([out] NumPathSegments: Int32, [out] PathSegments: Array)` | `void` |
| `GetFramePathObjects([out] NumPathSegments: Int32, [out] PathSegments: Array)` | `void` |
| `GetGlobalEndCondition([out] EndCondition: StructuralFrameEndConditionConstants, [opt] [out] dValue: Object)` | `void` |
| `GetOverrideEndCondition(NumSegments: Int32, SegmentIds: Array, [out] EndCondition: StructuralFrameEndConditionConstants, [opt] [out] dValue: Object)` | `void` |
| `GetPlaneOrientationForGivenSectionID(SectionId: Int32, [opt] [out] XOffset: Object, [opt] [out] YOffset: Object, [opt] [out] ZOffset: Object, [opt] [out] XRotation: Object, [opt] [out] YRotation: Object, [opt] [out] ZRotation: Object)` | `void` |
| `GetRedefineSectionDocument(SectionId: Int32, [out] PartFileDocument: String)` | `void` |
| `GetRedefineSectionPlacement(SectionId: Int32, [opt] [out] Rotation: Object, [opt] [out] XDistance: Object, [opt] [out] YDistance: Object)` | `void` |
| `GetSections([out] NumSections: Int32, [out] Sections: Array)` | `void` |
| `GetSetionIDObject(SectionId: Int32, [out] ProfileObject: Object)` | `void` |
| `MiterCut(SectionId: Int32, [out] MiterCut1: Double, [out] MiterCut2: Double)` | `void` |
| `RetrieveHoleLocation()` | `void` |
| `ReturnOccurrenceForGivenSectionID(SectionId: Int32, [opt] [out] OccurrenceID: Object, [opt] [out] OccurrenceAsObject: Object)` | `void` |
| `ReturnPathForGivenSectionID(SectionId: Int32, [opt] [out] PathId: Object, [opt] [out] PathAsObject: Object)` | `void` |
| `SegmentCutLength(SectionId: Int32, [out] dCutLength: Double, [opt] [out] bExactLength: Object)` | `void` |
| `SetFramePath(NumPathSegments: Int32, [out] PathSegments: Array)` | `void` |
| `SetGlobalEndCondition(EndCondition: StructuralFrameEndConditionConstants, [opt] dValue: Object)` | `void` |
| `SetOverrideEndCondition(NumSegments: Int32, SegmentIds: Array, EndCondition: StructuralFrameEndConditionConstants, [opt] dValue: Object)` | `void` |
| `SetRedefineSectionDocument(SectionId: Int32, PartFileDocument: String)` | `void` |
| `SetRedefineSectionPlacement(SectionId: Int32, [opt] Rotation: Object, [opt] XDistance: Object, [opt] YDistance: Object)` | `void` |

### <a name="structuralframes"></a>`StructuralFrames`

| Propriedade | Tipo | Acesso |
|-------------|------|--------|
| `Application` | `Application` | get |
| `Count` | `Int32` | get |
| `Parent` | `Object` | get |

| Método | Retorno |
|--------|---------|
| `Add(PartFileName: String, NumPaths: Int32, [ref] Path: Array, [opt] GlobalEndConditions: Object, [opt] GlobalEndConditionValue: Object, [opt] AutoPosition: Object)` | `StructuralFrame` |
| `AddByOrientation(PartFileName: String, CoOrdinateSystemName: String, NumPaths: Int32, [ref] Path: Array, [opt] PreferredOrientationPlane: Object, [opt] GlobalEndConditions: Object, [opt] GlobalEndConditionValue: Object, [opt] AutoPosition: Object)` | `StructuralFrame` |
| `Delete(StructuralFrame: StructuralFrame)` | `void` |
| `FrameSaveAs(FileName: String)` | `void` |
| `FrameSaveAsBiDM(FilePath: String, DocumentNumber: String, Revision: String, Title: String)` | `String` |
| `FrameSaveAsTranslated(FileName: String)` | `void` |
| `GetEnumerator()` | `IEnumerator` |
| `Item(Index: Object)` | `StructuralFrame` |

### <a name="subassemblybodies"></a>`SubassemblyBodies`

| Propriedade | Tipo | Acesso |
|-------------|------|--------|
| `Application` | `Application` | get |
| `AttributeSets` | `Object` | get |
| `Count` | `Int32` | get |
| `IsAttributeSetPresent[Name: String]` | `Boolean` | get |
| `Parent` | `Object` | get |

| Método | Retorno |
|--------|---------|
| `GetEnumerator()` | `IEnumerator` |
| `Item(Index: Object)` | `SubassemblyBody` |

### <a name="subassemblybody"></a>`SubassemblyBody`

| Propriedade | Tipo | Acesso |
|-------------|------|--------|
| `Application` | `Application` | get |
| `AssemblyBodyType` | `seAssemblyBodyTypeConstants` | get |
| `AttributeSets` | `Object` | get |
| `Body` | `Object` | get |
| `IsAttributeSetPresent[Name: String]` | `Boolean` | get |
| `Name` | `String` | get |
| `Parent` | `Object` | get |
| `Reference` | `Reference` | get |
| `ThisAsAssemblyBody` | `AssemblyBody` | get |
| `TopLevelDocument` | `AssemblyDocument` | get |
| `Type` | `ObjectType` | get |
| `Visible` | `Boolean` | get/set |

| Método | Retorno |
|--------|---------|
| `GetMaterial([out] DiffuseColor: Array, [out] AmbientColor: Array, [out] SpecularColor: Array, [out] EmissionColor: Array, [out] Shininess: Double, [out] Opacity: Double)` | `void` |
| `GetReferenceKey([out] ReferenceKey: Array, [opt] [out] KeySize: Object)` | `void` |

### <a name="suboccurrence"></a>`SubOccurrence`

| Propriedade | Tipo | Acesso |
|-------------|------|--------|
| `Adjustable` | `Boolean` | get/set |
| `Application` | `Application` | get |
| `Body` | `Object` | get |
| `CoordinateSystemsVisible` | `Boolean` | get/set |
| `CustomPropertyValue[CustomPropertyName: String]` | `String` | get/set |
| `DisplayAnnotations` | `Boolean` | get/set |
| `DisplayAsDraftReference` | `Boolean` | get/set |
| `DisplayAsLastSaved` | `Boolean` | get/set |
| `DisplayCenterline` | `Boolean` | get/set |
| `DisplayConstrCurves` | `Boolean` | get/set |
| `DisplayConstructions` | `Boolean` | get/set |
| `DisplayCoordinateSystems` | `Boolean` | get/set |
| `DisplayDesignBody` | `Boolean` | get/set |
| `DisplayDimensions` | `Boolean` | get/set |
| `DisplayLiveSections` | `Boolean` | get/set |
| `DisplayReferenceAxes` | `Boolean` | get/set |
| `DisplayReferencePlanes` | `Boolean` | get/set |
| `DisplaySketches` | `Boolean` | get/set |
| `ExcludeFromInterference` | `Boolean` | get/set |
| `ExcludeFromPhysicalProperties` | `Boolean` | get/set |
| `ExcludeFromReports` | `Boolean` | get/set |
| `FaceStyle` | `Object` | get/set |
| `HasBodyOverride` | `Boolean` | get |
| `HideInDrawing` | `Boolean` | get/set |
| `HideInSubassembly` | `Boolean` | get/set |
| `IsAdjustablePart` | `Boolean` | get |
| `IsPatterned` | `Boolean` | get |
| `IsPatternItem` | `Boolean` | get |
| `ItemNumber` | `Int32` | get/set |
| `Locatable` | `Boolean` | get/set |
| `Name` | `String` | get |
| `NodeType` | `ObjectType` | get |
| `Parent` | `Object` | get |
| `Reference` | `Reference` | get |
| `ReferencePlanesVisible` | `Boolean` | get/set |
| `SketchesVisible` | `Boolean` | get/set |
| `Style` | `String` | get/set |
| `Subassembly` | `Boolean` | get |
| `SubassemblyBodies` | `SubassemblyBodies` | get |
| `SubOccurrenceDocument` | `Object` | get |
| `SubOccurrenceFileName` | `String` | get |
| `SubOccurrences` | `SubOccurrences` | get |
| `ThisAsOccurrence` | `Occurrence` | get |
| `TopLevelDocument` | `AssemblyDocument` | get |
| `Type` | `ObjectType` | get |
| `UseSimplified` | `Boolean` | get/set |
| `Visible` | `Boolean` | get/set |

| Método | Retorno |
|--------|---------|
| `BindKeyToTopology(BodyOverride: Boolean, [ref] ReferenceKey: Array)` | `Object` |
| `CreateTopologyReference([ref] ReferenceKey: Array, [out] TopologyReference: TopologyReference)` | `void` |
| `CreateTopologyReferenceToBodyOverride([ref] ReferenceKey: Array, [out] TopologyReference: TopologyReference)` | `void` |
| `FileMissing()` | `Boolean` |
| `GetAdjustablePart()` | `AdjustablePart` |
| `GetBodyInversionMatrix([out] InvMatrix: Array)` | `void` |
| `GetExplodeMatrix([out] Matrix: Array)` | `void` |
| `GetFaceStyle2(vbHonourPrefs: Boolean)` | `Object` |
| `GetInterpartDrivenOccurrences([out] NumDrivenOccurrences: Int32, [out] DrivenOccurrences: Array)` | `void` |
| `GetInterpartDrivingOccurrences([out] NumDrivingOccurrences: Int32, [out] DrivingOccurrences: Array)` | `void` |
| `GetMaterial([out] DiffuseColor: Array, [out] AmbientColor: Array, [out] SpecularColor: Array, [out] EmissionColor: Array, [out] Shininess: Double, [out] Opacity: Double)` | `void` |
| `GetMatrix([out] Matrix: Array)` | `void` |
| `GetRangeBox([out] MinRangePoint: Array, [out] MaxRangePoint: Array)` | `void` |
| `GetReferenceKey([out] ReferenceKey: Array, [opt] [out] KeySize: Object)` | `void` |
| `GetSimplifiedBodies([out] NumBodies: Int32, [out] SimplifiedBodies: Array)` | `void` |
| `GetStyleNone()` | `Boolean` |
| `GetStyleUsePartStyle()` | `Boolean` |
| `MakeAdjustablePart()` | `AdjustablePart` |
| `PutMatrix([ref] Matrix: Array, Replace: Boolean)` | `void` |
| `PutStyleNone()` | `void` |
| `PutStyleUsePartStyle()` | `void` |
| `Range([out] x_min: Double, [out] y_min: Double, [out] z_min: Double, [out] x_max: Double, [out] y_max: Double, [out] z_max: Double)` | `void` |
| `RecheckMissingFile()` | `Boolean` |
| `RemoveOverrideBody()` | `void` |

### <a name="suboccurrences"></a>`SubOccurrences`

| Propriedade | Tipo | Acesso |
|-------------|------|--------|
| `Application` | `Application` | get |
| `Count` | `Int32` | get |
| `Parent` | `Object` | get |

| Método | Retorno |
|--------|---------|
| `GetEnumerator()` | `IEnumerator` |
| `Item(Index: Object)` | `SubOccurrence` |

### <a name="tangentrelation3d"></a>`TangentRelation3d`

| Propriedade | Tipo | Acesso |
|-------------|------|--------|
| `Application` | `Object` | get |
| `AttributeSets` | `Object` | get |
| `DetailedStatus` | `Relation3dDetailedStatusConstants` | get |
| `HalfSpacePositive` | `Boolean` | get |
| `Index` | `Int32` | get |
| `IsAttributeSetPresent[Name: String]` | `Boolean` | get |
| `Occurrence1` | `Occurrence` | get |
| `Occurrence2` | `Occurrence` | get |
| `Offset` | `Double` | get/set |
| `Parent` | `Object` | get |
| `RangedOffset` | `Boolean` | get/set |
| `RangeHigh` | `Double` | get/set |
| `RangeLow` | `Double` | get/set |
| `Status` | `Relation3dStatusConstants` | get |
| `Suppress` | `Boolean` | get/set |
| `Type` | `ObjectType` | get |

| Método | Retorno |
|--------|---------|
| `AddSuppressionVariable()` | `SuppressVariable` |
| `Delete()` | `void` |
| `DeleteSuppressionVariable()` | `void` |
| `GetElement1([out] IsTopologyReference: Boolean)` | `Object` |
| `GetElement2([out] IsTopologyReference: Boolean)` | `Object` |
| `GetGeometry1([out] GeometryType: Int32, [out] PointX: Double, [out] PointY: Double, [out] PointZ: Double, [out] VectorX: Double, [out] VectorY: Double, [out] VectorZ: Double)` | `void` |
| `GetGeometry2([out] GeometryType: Int32, [out] PointX: Double, [out] PointY: Double, [out] PointZ: Double, [out] VectorX: Double, [out] VectorY: Double, [out] VectorZ: Double)` | `void` |
| `GetSuppressionVariable()` | `SuppressVariable` |
| `HasSuppressionVariable()` | `Boolean` |
| `Select(Replace: Boolean)` | `void` |

### <a name="topologyreference"></a>`TopologyReference`

| Propriedade | Tipo | Acesso |
|-------------|------|--------|
| `Application` | `Application` | get |
| `Object` | `Object` | get |
| `Parent` | `Object` | get |
| `Type` | `ObjectType` | get |

| Método | Retorno |
|--------|---------|
| `GetMatrix([out] Matrix: Array)` | `void` |
| `GetOccurrencesInPath([out] TopOccurrence: Object, [out] NumSubOccurrencesInPath: Int32, [out] NumBoundSubOccurrencesInPath: Int32, [out] BoundSubOccurrencesInPath: Array)` | `void` |
| `GetReferenceKey([out] ReferenceKey: Array, [opt] [out] KeySize: Object)` | `void` |

### <a name="tube"></a>`Tube`

| Propriedade | Tipo | Acesso |
|-------------|------|--------|
| `Application` | `Application` | get |
| `AttributeSets` | `Object` | get |
| `DefaultBendRadius` | `Double` | get/set |
| `ExtentEnd` | `Double` | get/set |
| `ExtentStart` | `Double` | get/set |
| `InsideArea` | `Double` | get |
| `InsideVolume` | `Double` | get |
| `IsAttributeSetPresent[Name: String]` | `Boolean` | get |
| `IsSolid` | `Boolean` | get/set |
| `Material` | `FaceStyle` | get/set |
| `MinimumFlatLength` | `Double` | get/set |
| `OuterDiameter` | `Double` | get/set |
| `Parent` | `Object` | get |
| `PartFileName` | `String` | get |
| `TubeMaterial` | `String` | get/set |
| `UsesGlobalBendRadius` | `Boolean` | get/set |
| `UsesGlobalMinimumFlatLength` | `Boolean` | get/set |
| `UsesGlobalOuterDiameter` | `Boolean` | get/set |
| `UsesGlobalWallThickness` | `Boolean` | get/set |
| `WallThickness` | `Double` | get/set |

| Método | Retorno |
|--------|---------|
| `AddSegment(TubeSegment: Object, [out] Status: TubeSegmentAdditionStatusConstants)` | `void` |
| `BendTable([opt] [out] CutLength: Object, [opt] [out] NumOfBends: Object, [opt] [out] FeedLength: Object, [opt] [out] RotationAngle: Object, [opt] [out] BendRadius: Object, [opt] [ref] ReverseBendOrder: Object, [opt] SaveToFileName: Object, [opt] [out] BendAngle: Object)` | `void` |
| `BreakLink()` | `void` |
| `Freeze()` | `void` |
| `GetBendRadius(Segment1: Object, Segment2: Object, [out] BendRadius: Double)` | `void` |
| `GetEndTreatment(StartOrEnd: Boolean, [opt] [out] EndTreatmentType: Object, [opt] [out] InsideDiameter: Object, [opt] [out] OutsideDiameter: Object, [opt] [out] Depth: Object, [opt] [out] Radius: Object, [opt] [out] Angle: Object)` | `void` |
| `GetPathSegmentsRelations([out] NumSegmentRelations: Int32, [opt] [out] SegmentRelations: Object)` | `void` |
| `GetTubePropertyNameAndValueByPid(TubePropertyPid: TubePropertyPidConstants, [out] TubePropertyName: String, [out] TubePropertyValue: String)` | `void` |
| `IsBendRadiusOverridden(Segment1: Object, Segment2: Object, [out] IsOverridden: Boolean)` | `void` |
| `IsFrozen([out] bFrozen: Boolean)` | `void` |
| `IsLinked([out] bLinked: Boolean)` | `void` |
| `Path([out] NumElementsInPath: Int32, [opt] [out] Path: Object, [opt] [out] Reversed: Object)` | `void` |
| `RemoveSegment(TubeSegment: Object, [out] Status: TubeSegmentRemovalStatusConstants)` | `void` |
| `SetBendRadius(Segment1: Object, Segment2: Object, BendRadius: Double)` | `void` |
| `SetEndTreatment(StartOrEnd: Boolean, [opt] EndTreatmentType: Object, [opt] InsideDiameter: Object, [opt] OutsideDiameter: Object, [opt] Depth: Object, [opt] Radius: Object, [opt] Angle: Object)` | `void` |
| `Thaw()` | `void` |
| `UpdateTubeFileProperties()` | `void` |

### <a name="virtualcomponent"></a>`VirtualComponent`

| Propriedade | Tipo | Acesso |
|-------------|------|--------|
| `Application` | `Application` | get |
| `AttributeSets` | `Object` | get |
| `IsAttributeSetPresent[Name: String]` | `Boolean` | get |
| `Name` | `String` | get/set |
| `Parent` | `Object` | get |
| `Properties` | `Object` | get |
| `PublishPath` | `String` | get/set |
| `TemplateName` | `String` | get/set |
| `Type` | `ObjectType` | get |
| `VirtualComponentOccurrences` | `VirtualComponentOccurrences` | get |
| `VirtualComponentType` | `VirtualComponentTypeConstants` | get |

| Método | Retorno |
|--------|---------|
| `Delete()` | `void` |
| `GetGeometry([out] NumElements: Int32, [out] GeomElements: Array)` | `void` |
| `RemoveGeometry(NumElements: Int32, [out] GeomElements: Array)` | `void` |
| `SetGeometry(NumElements: Int32, [out] GeomElements: Array)` | `void` |
| `UpdateComponent([opt] [out] ComponentStatusConstants: Object)` | `void` |

### <a name="virtualcomponentoccurrence"></a>`VirtualComponentOccurrence`

| Propriedade | Tipo | Acesso |
|-------------|------|--------|
| `Angle` | `Double` | get/set |
| `Application` | `Application` | get |
| `AttributeSets` | `Object` | get |
| `IsAttributeSetPresent[Name: String]` | `Boolean` | get |
| `IsPositioned` | `Boolean` | get |
| `IsPreDefined` | `Boolean` | get |
| `Name` | `String` | get |
| `Parent` | `Object` | get |
| `Type` | `ObjectType` | get |
| `VirtualComponent` | `VirtualComponent` | get |

| Método | Retorno |
|--------|---------|
| `Delete()` | `void` |
| `GetOrigin([out] x: Double, [out] y: Double)` | `void` |
| `GetOriginByKeypoints([out] Object1: Object, [out] KeyPointTypeConstant1: KeyPointType, [out] Object2: Object, [out] KeyPointTypeConstant2: KeyPointType)` | `void` |
| `GetViewDefinition([out] PublishOn: VirtualComponentPublishConstants, [opt] [out] SketchName: Object)` | `void` |
| `Hide()` | `void` |
| `HideAll()` | `void` |
| `IsMaster([out] IsMaster: Boolean, [opt] [out] ppMasterOccurrence: Object)` | `void` |
| `IsVisible()` | `Boolean` |
| `MoveTo(VirtualComponent: VirtualComponent)` | `void` |
| `PositionComponent(pProfile: Object, x: Double, y: Double, [opt] Angle: Object, [opt] [out] ComponentStatusConstants: Object)` | `void` |
| `ReplaceComponent(FileName: String, ReplaceAll: Boolean, [opt] [out] ComponentStatusConstants: Object)` | `void` |
| `SetOrigin(x: Double, y: Double)` | `void` |
| `SetOriginByKeypoints(Object1: Object, KeyPointTypeConstant1: KeyPointType, Object2: Object, KeyPointTypeConstant2: KeyPointType)` | `void` |
| `SetViewDefinition(PublishOn: VirtualComponentPublishConstants, [opt] SketchName: Object)` | `void` |
| `Show()` | `void` |
| `ShowAll()` | `void` |

### <a name="virtualcomponentoccurrences"></a>`VirtualComponentOccurrences`

| Propriedade | Tipo | Acesso |
|-------------|------|--------|
| `Application` | `Application` | get |
| `Count` | `Int32` | get |
| `Parent` | `Object` | get |

| Método | Retorno |
|--------|---------|
| `Add(VirtualComponentName: String, VirtualComponentType: VirtualComponentTypeConstants, [out] ComponentStatusConstants: VirtualComponentStatusConstants)` | `VirtualComponentOccurrence` |
| `AddAsPreDefined(FileName: String, [out] ComponentStatusConstants: VirtualComponentStatusConstants)` | `VirtualComponentOccurrence` |
| `AddBIDM(bstrDocumentNumber: String, bstrRevisionID: String, VirtualComponentType: VirtualComponentTypeConstants, [out] ComponentStatusConstants: VirtualComponentStatusConstants)` | `VirtualComponentOccurrence` |
| `GetEnumerator()` | `IEnumerator` |
| `Item(Index: Object)` | `VirtualComponentOccurrence` |

### <a name="wire"></a>`Wire`

| Propriedade | Tipo | Acesso |
|-------------|------|--------|
| `Application` | `Application` | get |
| `AssemblyProperty` | `AssemblyProperty` | get |
| `AttributeSets` | `Object` | get |
| `Color` | `Int32` | get/set |
| `CutLength` | `Double` | get |
| `Description` | `String` | get/set |
| `Diameter` | `Double` | get/set |
| `FromComponentName` | `String` | get/set |
| `FromTerminalName` | `String` | get/set |
| `Gage` | `String` | get/set |
| `IsAttributeSetPresent[Name: String]` | `Boolean` | get |
| `LinearDensity` | `Double` | get/set |
| `Mass` | `Double` | get |
| `Material` | `String` | get/set |
| `MaterialType` | `String` | get/set |
| `MinimumBendRadius` | `Double` | get/set |
| `Name` | `String` | get/set |
| `OuterDiameter` | `Double` | get/set |
| `Parent` | `Object` | get |
| `PartNumber` | `String` | get/set |
| `ToComponentName` | `String` | get/set |
| `ToTerminalName` | `String` | get/set |
| `TrueLength` | `Double` | get |
| `Type` | `HarnessTypeConstants` | get |
| `UseGlobalAdders` | `Boolean` | get/set |
| `Visible` | `Boolean` | get/set |

| Método | Retorno |
|--------|---------|
| `CreatePhysicalConductor()` | `void` |
| `Delete()` | `void` |
| `DeletePhysicalConductor()` | `void` |
| `Edit(NumberOfPaths: Int32, [ref] PathArray: Array, [ref] PathDirectionArray: Array)` | `void` |
| `GetAdders([out] dSlackAdder: Double, [out] dPureAdder: Double, [out] dHoleDiameterAdder: Double, [out] dBundleDiameterAdder: Double)` | `void` |
| `GetAllPaths([out] NumberOfPaths: Int32, [out] PathArray: Array)` | `void` |
| `GetEndPoints([out] dStartX: Double, [out] dStartY: Double, [out] dStartZ: Double, [out] dEndX: Double, [out] dEndY: Double, [out] dEndZ: Double)` | `void` |
| `GetOuterConductors([out] NumberOfConductors: Int32, [out] ConductorArray: Array)` | `void` |
| `GetPaths([out] NumberOfPaths: Int32, [out] PathArray: Array, [out] PathDirectionArray: Array)` | `void` |
| `GetTopMostConductors([out] NumberOfConductors: Int32, [out] ConductorArray: Array)` | `void` |
| `Remove(NumberOfConductors: Int32, [ref] ConductorArray: Array)` | `void` |
| `SetAdders(dSlackAdder: Double, dPureAdder: Double, dHoleDiameterAdder: Double, dBundleDiameterAdder: Double)` | `void` |

### <a name="wirepath"></a>`WirePath`

| Propriedade | Tipo | Acesso |
|-------------|------|--------|
| `Application` | `Application` | get |
| `AssemblyProperty` | `AssemblyProperty` | get |
| `CableMaster` | `WirePath` | get |
| `CableMembers` | `WirePathCableMembers` | get |
| `Color` | `Int32` | get/set |
| `ColorName` | `String` | get/set |
| `Diameter` | `Double` | get/set |
| `EndConnector` | `String` | get/set |
| `Length` | `Double` | get/set |
| `Material` | `String` | get/set |
| `Name` | `String` | get/set |
| `Parent` | `Object` | get |
| `Segments` | `WirePathSegments` | get |
| `StartConnector` | `String` | get/set |
| `Type` | `WirePathConstants` | get/set |
| `Visible` | `Boolean` | get/set |

| Método | Retorno |
|--------|---------|
| `AddCableMember(WirePath: WirePath)` | `void` |
| `AddSegment(Segment: Object, [out] Status: TubeSegmentAdditionStatusConstants)` | `void` |
| `GetCableMemberIndex(WirePath: WirePath, [out] Index: Int32)` | `void` |
| `RemoveCableMember(WirePath: WirePath)` | `void` |
| `RemoveSegment(Segment: Object, [out] Status: TubeSegmentRemovalStatusConstants)` | `void` |
| `SetCableMemberIndex(WirePath: WirePath, Index: Int32)` | `void` |

### <a name="wirepathcablemembers"></a>`WirePathCableMembers`

| Propriedade | Tipo | Acesso |
|-------------|------|--------|
| `Application` | `Application` | get |
| `Count` | `Int32` | get |
| `Parent` | `Object` | get |

| Método | Retorno |
|--------|---------|
| `GetEnumerator()` | `IEnumerator` |
| `Item(Index: Object)` | `WirePath` |

### <a name="wirepaths"></a>`WirePaths`

| Propriedade | Tipo | Acesso |
|-------------|------|--------|
| `Application` | `Application` | get |
| `Count` | `Int32` | get |
| `Parent` | `Object` | get |

| Método | Retorno |
|--------|---------|
| `Add(Name: String)` | `WirePath` |
| `Delete(WirePath: WirePath)` | `void` |
| `GetEnumerator()` | `IEnumerator` |
| `Item(Index: Object)` | `WirePath` |

### <a name="wirepathsegments"></a>`WirePathSegments`

| Propriedade | Tipo | Acesso |
|-------------|------|--------|
| `Application` | `Application` | get |
| `Count` | `Int32` | get |
| `Parent` | `Object` | get |

| Método | Retorno |
|--------|---------|
| `GetEnumerator()` | `IEnumerator` |
| `Item(Index: Object)` | `Object` |

### <a name="wirerun"></a>`WireRun`

| Propriedade | Tipo | Acesso |
|-------------|------|--------|
| `Application` | `Application` | get |
| `Name` | `String` | get/set |
| `Occurrence` | `Occurrence` | get |
| `Parent` | `Object` | get |
| `Visible` | `Boolean` | get/set |
| `WirePaths` | `WireRunPaths` | get |

| Método | Retorno |
|--------|---------|
| `AddWirePath(WirePath: WirePath)` | `void` |
| `BreakLink()` | `void` |
| `Freeze()` | `void` |
| `IsFrozen([out] bFrozen: Boolean)` | `void` |
| `IsLinked([out] bLinked: Boolean)` | `void` |
| `RemoveWirePath(WirePath: WirePath)` | `void` |
| `Thaw()` | `void` |

### <a name="wirerunpaths"></a>`WireRunPaths`

| Propriedade | Tipo | Acesso |
|-------------|------|--------|
| `Application` | `Application` | get |
| `Count` | `Int32` | get |
| `Parent` | `Object` | get |

| Método | Retorno |
|--------|---------|
| `GetEnumerator()` | `IEnumerator` |
| `Item(Index: Object)` | `WirePath` |

### <a name="wireruns"></a>`WireRuns`

| Propriedade | Tipo | Acesso |
|-------------|------|--------|
| `Application` | `Application` | get |
| `Count` | `Int32` | get |
| `Parent` | `Object` | get |

| Método | Retorno |
|--------|---------|
| `Add(PartFileName: String, [opt] PartTemplateFileName: Object, [opt] PartFileDirectory: Object)` | `WireRun` |
| `Delete(WireRun: WireRun)` | `void` |
| `GetEnumerator()` | `IEnumerator` |
| `Item(Index: Object)` | `WireRun` |

### <a name="wires"></a>`Wires`

| Propriedade | Tipo | Acesso |
|-------------|------|--------|
| `Application` | `Application` | get |
| `Count` | `Int32` | get |
| `Parent` | `Object` | get |
| `Type` | `HarnessTypeConstants` | get |

| Método | Retorno |
|--------|---------|
| `Add(NumberOfPaths: Int32, [ref] PathArray: Array, [ref] PathDirectionArray: Array, ConductorDescription: String)` | `Wire` |
| `GetEnumerator()` | `IEnumerator` |
| `Item(Index: Object)` | `Wire` |

### <a name="zone"></a>`Zone`

| Propriedade | Tipo | Acesso |
|-------------|------|--------|
| `Application` | `Application` | get |
| `Name` | `String` | get/set |
| `Overlap` | `Boolean` | get/set |
| `Parent` | `Object` | get |

| Método | Retorno |
|--------|---------|
| `AddPartsToSelectSet()` | `void` |
| `CopyToClipboard()` | `void` |
| `GetPoints([out] Point1x: Double, [out] Point1y: Double, [out] Point1z: Double, [out] Point2x: Double, [out] Point2y: Double, [out] Point2z: Double)` | `void` |
| `Hide()` | `void` |
| `HideParts()` | `void` |
| `IsVisible()` | `Boolean` |
| `PopulateFromClipboard()` | `void` |
| `SetPoints(Point1x: Double, Point1y: Double, Point1z: Double, Point2x: Double, Point2y: Double, Point2z: Double)` | `void` |
| `Show()` | `void` |
| `ShowOnlyParts()` | `void` |
| `ShowParts()` | `void` |

### <a name="zones"></a>`Zones`

| Propriedade | Tipo | Acesso |
|-------------|------|--------|
| `Application` | `Application` | get |
| `Count` | `Int32` | get |
| `Parent` | `Object` | get |

| Método | Retorno |
|--------|---------|
| `Add(Point1x: Double, Point1y: Double, Point1z: Double, Point2x: Double, Point2y: Double, Point2z: Double)` | `Zone` |
| `Cut(Name: String)` | `void` |
| `Delete(Name: String)` | `void` |
| `GetEnumerator()` | `IEnumerator` |
| `Hide()` | `void` |
| `IsVisible()` | `Boolean` |
| `Item(Index: Object)` | `Zone` |
| `Paste()` | `Zone` |
| `Save()` | `void` |
| `Show()` | `void` |

