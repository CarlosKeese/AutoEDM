# Constantes e Enums do Solid Edge (COM)

- **Total de enums:** 231
- **Fonte:** dump `SE_API_dump_223.00.13.05.txt` (SE 2023 `223.00.13.05`)
- **Nota:** os valores são os encontrados na instalação que gerou o dump. Verifique a versão do Solid Edge ao usar constantes não listadas aqui.

## Índice de enums

- [`AcceleratorTypeConstants`](#acceleratortypeconstants) (SolidEdgeFramework)
- [`AddBodyTypeConstants`](#addbodytypeconstants) (SolidEdgePart)
- [`AlignBodyFaceType`](#alignbodyfacetype) (SolidEdgePart)
- [`AlignBodyPointTypeOnFace`](#alignbodypointtypeonface) (SolidEdgePart)
- [`AnimationEventConstants`](#animationeventconstants) (SolidEdgeFramework)
- [`ApplicationActiveFrameSwitchingEvent`](#applicationactiveframeswitchingevent) (SolidEdgeFramework)
- [`ApplicationBeforeDocumentOpenEvent`](#applicationbeforedocumentopenevent) (SolidEdgeFramework)
- [`ApplicationDocumentLoadingEvent`](#applicationdocumentloadingevent) (SolidEdgeFramework)
- [`ApplicationGlobalConstants`](#applicationglobalconstants) (SolidEdgeFramework)
- [`ApplicationLicenseEvent`](#applicationlicenseevent) (SolidEdgeFramework)
- [`ApplicationReadyEvent`](#applicationreadyevent) (SolidEdgeFramework)
- [`ArrangeWindowsStyles`](#arrangewindowsstyles) (SolidEdgeFramework)
- [`AssemblyChangeEventsConstants`](#assemblychangeeventsconstants) (SolidEdgeFramework)
- [`AssemblyEventConstants`](#assemblyeventconstants) (SolidEdgeFramework)
- [`AssemblyWeldmentOccurrencesOptionsConstants`](#assemblyweldmentoccurrencesoptionsconstants) (SolidEdgePart)
- [`AttachedStatusConstants`](#attachedstatusconstants) (SolidEdgePart)
- [`AttributeTypeConstants`](#attributetypeconstants) (SolidEdgeFramework)
- [`BendFeatureConstants`](#bendfeatureconstants) (SolidEdgePart)
- [`BlendShapeConstants`](#blendshapeconstants) (SolidEdgePart)
- [`BlockLabelOriginLocationConstants`](#blocklabeloriginlocationconstants) (SolidEdgePart)
- [`BooleanFeatureConstants`](#booleanfeatureconstants) (SolidEdgePart)
- [`BulkMigrationTypeConstants`](#bulkmigrationtypeconstants) (SolidEdgeFramework)
- [`CageOffsetTypes`](#cageoffsettypes) (SolidEdgePart)
- [`CapturedRelationshipOffsetTypeConstants`](#capturedrelationshipoffsettypeconstants) (SolidEdgeFramework)
- [`CapturedRelationshipTypeConstants`](#capturedrelationshiptypeconstants) (SolidEdgeFramework)
- [`CheckInOptions`](#checkinoptions) (SolidEdgeFramework)
- [`CleanProfileOptions`](#cleanprofileoptions) (SolidEdgePart)
- [`CloseCornerFeatureConstants`](#closecornerfeatureconstants) (SolidEdgePart)
- [`CommandBarHeaderDialogControlIDs`](#commandbarheaderdialogcontrolids) (SolidEdgeFramework)
- [`ConfigForForeignFileType`](#configforforeignfiletype) (SolidEdgeFramework)
- [`ConfigResetType`](#configresettype) (SolidEdgeFramework)
- [`CookieDataToGet`](#cookiedatatoget) (SolidEdgeFramework)
- [`CoordinateSystemFeatureConstants`](#coordinatesystemfeatureconstants) (SolidEdgePart)
- [`CoordinateSystemOffsetTypeConstants`](#coordinatesystemoffsettypeconstants) (SolidEdgePart)
- [`CoordinateSystemRotationTypeConstants`](#coordinatesystemrotationtypeconstants) (SolidEdgePart)
- [`CoordinateSystemTypeConstants`](#coordinatesystemtypeconstants) (SolidEdgePart)
- [`CopySurfaceExternalBoundaryConstants`](#copysurfaceexternalboundaryconstants) (SolidEdgePart)
- [`CopySurfaceInternalBoundaryConstants`](#copysurfaceinternalboundaryconstants) (SolidEdgePart)
- [`DeleteFaceConstants`](#deletefaceconstants) (SolidEdgePart)
- [`DerivedCurveTypeConstants`](#derivedcurvetypeconstants) (SolidEdgePart)
- [`DimpleFeatureConstants`](#dimplefeatureconstants) (SolidEdgePart)
- [`DisplayTypeConstant`](#displaytypeconstant) (SolidEdgeFramework)
- [`DividedPartCutDirectionConstants`](#dividedpartcutdirectionconstants) (SolidEdgePart)
- [`DividedPartStatusConstants`](#dividedpartstatusconstants) (SolidEdgePart)
- [`DocumentAccess`](#documentaccess) (SolidEdgeFramework)
- [`DocumentDownloadLevel`](#documentdownloadlevel) (SolidEdgeFramework)
- [`DocumentStatus`](#documentstatus) (SolidEdgeFramework)
- [`DocumentTypeConstants`](#documenttypeconstants) (SolidEdgeFramework)
- [`DraftSideConstants`](#draftsideconstants) (SolidEdgePart)
- [`DrawnCutoutFeatureConstants`](#drawncutoutfeatureconstants) (SolidEdgePart)
- [`EnclosureTypeConstant`](#enclosuretypeconstant) (SolidEdgePart)
- [`ExtendSurfaceExtentTypeConstants`](#extendsurfaceextenttypeconstants) (SolidEdgePart)
- [`FaceMoveConstants`](#facemoveconstants) (SolidEdgePart)
- [`FaceOffsetConstants`](#faceoffsetconstants) (SolidEdgePart)
- [`FaceRotateConstants`](#facerotateconstants) (SolidEdgePart)
- [`FamilyMemberStatusConstants`](#familymemberstatusconstants) (SolidEdgePart)
- [`FeatureLoopType`](#featurelooptype) (SolidEdgePart)
- [`FeaturePropertyConstants`](#featurepropertyconstants) (SolidEdgePart)
- [`FeatureStatusConstants`](#featurestatusconstants) (SolidEdgePart)
- [`FeatureTypeConstants`](#featuretypeconstants) (SolidEdgePart)
- [`FileTranslationMode`](#filetranslationmode) (SolidEdgeFramework)
- [`FillHoleType`](#fillholetype) (SolidEdgePart)
- [`FillPatternMethodConstants`](#fillpatternmethodconstants) (SolidEdgePart)
- [`FilletWeldSetbackConstants`](#filletweldsetbackconstants) (SolidEdgePart)
- [`FlangeFeatureConstants`](#flangefeatureconstants) (SolidEdgePart)
- [`GenerateMasterImportListError`](#generatemasterimportlisterror) (SolidEdgeFramework)
- [`GenerateSourceImportListError`](#generatesourceimportlisterror) (SolidEdgeFramework)
- [`GussetConstants`](#gussetconstants) (SolidEdgePart)
- [`GussetPlateAlignmentConstants`](#gussetplatealignmentconstants) (SolidEdgePart)
- [`GussetPlateErrorCode`](#gussetplateerrorcode) (SolidEdgePart)
- [`GussetPlateThicknessDirConstants`](#gussetplatethicknessdirconstants) (SolidEdgePart)
- [`HatchElementType`](#hatchelementtype) (SolidEdgeFramework)
- [`HelicalCurveMethodType`](#helicalcurvemethodtype) (SolidEdgePart)
- [`HelicalCurveTaperByType`](#helicalcurvetaperbytype) (SolidEdgePart)
- [`HemFeatureConstants`](#hemfeatureconstants) (SolidEdgePart)
- [`HoleDataUnitsConstants`](#holedataunitsconstants) (SolidEdgePart)
- [`HoleToleranceTypeConstants`](#holetolerancetypeconstants) (SolidEdgePart)
- [`HoleTypeToDeleteConstants`](#holetypetodeleteconstants) (SolidEdgePart)
- [`InsightSPUserRights`](#insightspuserrights) (SolidEdgeFramework)
- [`InterDocumentUpdateMode`](#interdocumentupdatemode) (SolidEdgeFramework)
- [`IsoclineDirectionConstants`](#isoclinedirectionconstants) (SolidEdgePart)
- [`JogFeatureConstants`](#jogfeatureconstants) (SolidEdgePart)
- [`KeyPointExtentConstants`](#keypointextentconstants) (SolidEdgePart)
- [`KeyPointType`](#keypointtype) (SolidEdgeFramework)
- [`KeypointEndConditionConstants`](#keypointendconditionconstants) (SolidEdgePart)
- [`LinksUpdateOption`](#linksupdateoption) (SolidEdgeFramework)
- [`LiveRulesConstants`](#liverulesconstants) (SolidEdgePart)
- [`LoftedFlangeFeatureAutoReliefConstants`](#loftedflangefeatureautoreliefconstants) (SolidEdgePart)
- [`LoftedFlangeFeatureAutoReliefTrimConstants`](#loftedflangefeatureautorelieftrimconstants) (SolidEdgePart)
- [`LoftedFlangeFeatureBendingMethodConstants`](#loftedflangefeaturebendingmethodconstants) (SolidEdgePart)
- [`LoftedFlangeFeatureDivideBendConstants`](#loftedflangefeaturedividebendconstants) (SolidEdgePart)
- [`LouverFeatureConstants`](#louverfeatureconstants) (SolidEdgePart)
- [`MatTablePropIndexConstants`](#mattablepropindexconstants) (SolidEdgeFramework)
- [`MeasureDistanceTypeConstants`](#measuredistancetypeconstants) (SolidEdgePart)
- [`MirrorOptionConstants`](#mirroroptionconstants) (SolidEdgePart)
- [`ModelingModeConstants`](#modelingmodeconstants) (SolidEdgePart)
- [`MoveConnectedFaceTypes`](#moveconnectedfacetypes) (SolidEdgePart)
- [`MovePrecedenceConstants`](#moveprecedenceconstants) (SolidEdgePart)
- [`MultiBodyPublishStatusConstants`](#multibodypublishstatusconstants) (SolidEdgePart)
- [`NotifyOption`](#notifyoption) (SolidEdgeFramework)
- [`OLEInsertionTypeConstant`](#oleinsertiontypeconstant) (SolidEdgeFramework)
- [`OLEUpdateOptionConstant`](#oleupdateoptionconstant) (SolidEdgeFramework)
- [`ObjectType`](#objecttype) (SolidEdgeFramework)
- [`OffsetSideConstants`](#offsetsideconstants) (SolidEdgePart)
- [`OpenNonSolidEdgeFileContext`](#opennonsolidedgefilecontext) (SolidEdgeFramework)
- [`OverWriteFilesOption`](#overwritefilesoption) (SolidEdgeFramework)
- [`PMISectionDisplayModeConstants`](#pmisectiondisplaymodeconstants) (SolidEdgeFramework)
- [`PartBaseStylesConstants`](#partbasestylesconstants) (SolidEdgePart)
- [`PatternCurveAnchorSideConstants`](#patterncurveanchorsideconstants) (SolidEdgePart)
- [`PatternTransformRotateTypeConstants`](#patterntransformrotatetypeconstants) (SolidEdgePart)
- [`PatternTransformTypeConstants`](#patterntransformtypeconstants) (SolidEdgePart)
- [`PatternTypeConstants`](#patterntypeconstants) (SolidEdgePart)
- [`PhysicalPropertiesStatusConstants`](#physicalpropertiesstatusconstants) (SolidEdgePart)
- [`PhysicalThreadErrorCode`](#physicalthreaderrorcode) (SolidEdgePart)
- [`PointTypeConstants`](#pointtypeconstants) (SolidEdgePart)
- [`PredefineRelationGroupPolarityConstants`](#predefinerelationgrouppolarityconstants) (SolidEdgeFramework)
- [`Print3DFileType`](#print3dfiletype) (SolidEdgePart)
- [`ProfileValidationType`](#profilevalidationtype) (SolidEdgePart)
- [`PropertyFilterTypeConstants`](#propertyfiltertypeconstants) (SolidEdgePart)
- [`PropertyTableConstants`](#propertytableconstants) (SolidEdgePart)
- [`PropertyTypeConstants`](#propertytypeconstants) (SolidEdgePart)
- [`RadialHatchElementCenterLocation`](#radialhatchelementcenterlocation) (SolidEdgeFramework)
- [`RedefineFaceTangencyType`](#redefinefacetangencytype) (SolidEdgePart)
- [`ReferenceElementConstants`](#referenceelementconstants) (SolidEdgePart)
- [`ReferencePointTypeEnumForFromPointOption`](#referencepointtypeenumforfrompointoption) (SolidEdgePart)
- [`ReferencePointTypeEnumForToPointOption`](#referencepointtypeenumfortopointoption) (SolidEdgePart)
- [`RevisionRuleType`](#revisionruletype) (SolidEdgeFramework)
- [`RibbonBarControlSize`](#ribbonbarcontrolsize) (SolidEdgeFramework)
- [`RibbonBarControlText`](#ribbonbarcontroltext) (SolidEdgeFramework)
- [`RibbonBarInsertMode`](#ribbonbarinsertmode) (SolidEdgeFramework)
- [`RoundTypeConstants`](#roundtypeconstants) (SolidEdgePart)
- [`RouteStatus`](#routestatus) (SolidEdgeFramework)
- [`RouteType`](#routetype) (SolidEdgeFramework)
- [`RuledSurfaceDirectionConstants`](#ruledsurfacedirectionconstants) (SolidEdgePart)
- [`RuledSurfaceSideConstants`](#ruledsurfacesideconstants) (SolidEdgePart)
- [`RuledSurfaceTypeConstants`](#ruledsurfacetypeconstants) (SolidEdgePart)
- [`SEECOptions`](#seecoptions) (SolidEdgeFramework)
- [`SEFixedLengthConstraintDirection`](#sefixedlengthconstraintdirection) (SolidEdgePart)
- [`SEPatternRecognitionLevel`](#sepatternrecognitionlevel) (SolidEdgePart)
- [`SESubtractDirection`](#sesubtractdirection) (SolidEdgePart)
- [`SETargetConstructionBodyOption`](#setargetconstructionbodyoption) (SolidEdgePart)
- [`SETargetDesignBodyOption`](#setargetdesignbodyoption) (SolidEdgePart)
- [`SPServerType`](#spservertype) (SolidEdgeFramework)
- [`SaveAsFlatFileTypes`](#saveasflatfiletypes) (SolidEdgePart)
- [`SeAnalysisModeType`](#seanalysismodetype) (SolidEdgeFramework)
- [`SeAnalysisStateType`](#seanalysisstatetype) (SolidEdgeFramework)
- [`SeAntiAliasLevel`](#seantialiaslevel) (SolidEdgeFramework)
- [`SeBackgroundType`](#sebackgroundtype) (SolidEdgeFramework)
- [`SeBarPosition`](#sebarposition) (SolidEdgeFramework)
- [`SeBarType`](#sebartype) (SolidEdgeFramework)
- [`SeButtonState`](#sebuttonstate) (SolidEdgeFramework)
- [`SeButtonStyle`](#sebuttonstyle) (SolidEdgeFramework)
- [`SeConnectMode`](#seconnectmode) (SolidEdgeFramework)
- [`SeControlType`](#secontroltype) (SolidEdgeFramework)
- [`SeDisconnectMode`](#sedisconnectmode) (SolidEdgeFramework)
- [`SeFeatureAddFlag`](#sefeatureaddflag) (SolidEdgeFramework)
- [`SeFeatureDeleteFlag`](#sefeaturedeleteflag) (SolidEdgeFramework)
- [`SeFeatureModifyFlag`](#sefeaturemodifyflag) (SolidEdgeFramework)
- [`SeGradientType`](#segradienttype) (SolidEdgeFramework)
- [`SeHiddenLineMode`](#sehiddenlinemode) (SolidEdgeFramework)
- [`SeImageQualityType`](#seimagequalitytype) (SolidEdgeFramework)
- [`SeModifySketchFlag`](#semodifysketchflag) (SolidEdgeFramework)
- [`SeObjectType`](#seobjecttype) (SolidEdgeFramework)
- [`SeRenderFillMode`](#serenderfillmode) (SolidEdgeFramework)
- [`SeRenderMaterialGetMode`](#serendermaterialgetmode) (SolidEdgeFramework)
- [`SeRenderMaterialSetMode`](#serendermaterialsetmode) (SolidEdgeFramework)
- [`SeRenderModeType`](#serendermodetype) (SolidEdgeFramework)
- [`SeRenderShadeMode`](#serendershademode) (SolidEdgeFramework)
- [`SeRenderShapeType`](#serendershapetype) (SolidEdgeFramework)
- [`SeRenderSpaceType`](#serenderspacetype) (SolidEdgeFramework)
- [`SeSkyboxType`](#seskyboxtype) (SolidEdgeFramework)
- [`SectionSketchesErrorCode`](#sectionsketcheserrorcode) (SolidEdgePart)
- [`SectionSketchesPlanesDirection`](#sectionsketchesplanesdirection) (SolidEdgePart)
- [`SectionViewExtentSide`](#sectionviewextentside) (SolidEdgeFramework)
- [`SectionViewPlaneExtentTypeConstant`](#sectionviewplaneextenttypeconstant) (SolidEdgeFramework)
- [`SectionViewPlaneType`](#sectionviewplanetype) (SolidEdgeFramework)
- [`SectionViewProfileSide`](#sectionviewprofileside) (SolidEdgeFramework)
- [`SensorDisplayTypeConstants`](#sensordisplaytypeconstants) (SolidEdgeFramework)
- [`SensorOperatorConstants`](#sensoroperatorconstants) (SolidEdgeFramework)
- [`SensorStatusConstants`](#sensorstatusconstants) (SolidEdgeFramework)
- [`SensorTypeConstants`](#sensortypeconstants) (SolidEdgeFramework)
- [`SensorUpdateMechanismConstants`](#sensorupdatemechanismconstants) (SolidEdgeFramework)
- [`SheetMetalSensorFeatureTypeConstants`](#sheetmetalsensorfeaturetypeconstants) (SolidEdgeFramework)
- [`ShortCutMenuContextConstants`](#shortcutmenucontextconstants) (SolidEdgeFramework)
- [`SolidEdgeCommandConstants`](#solidedgecommandconstants) (SolidEdgeFramework)
- [`SpiralCurveMethodType`](#spiralcurvemethodtype) (SolidEdgePart)
- [`StitchWeldAnnotationFormat`](#stitchweldannotationformat) (SolidEdgePart)
- [`StitchWeldType`](#stitchweldtype) (SolidEdgePart)
- [`StyleUnitsConstant`](#styleunitsconstant) (SolidEdgeFramework)
- [`SubdivisionDragTypeConstants`](#subdivisiondragtypeconstants) (SolidEdgePart)
- [`SuppressRegionsConstants`](#suppressregionsconstants) (SolidEdgePart)
- [`SurfaceAreaSensorAreaTypeConstants`](#surfaceareasensorareatypeconstants) (SolidEdgeFramework)
- [`SurfaceAreaSensorSelectionTypeConstants`](#surfaceareasensorselectiontypeconstants) (SolidEdgeFramework)
- [`SurfaceByBoundaryConstants`](#surfacebyboundaryconstants) (SolidEdgePart)
- [`SurfaceByBoundaryFillPreference`](#surfacebyboundaryfillpreference) (SolidEdgePart)
- [`SurfaceByBoundaryInternalSmoothness`](#surfacebyboundaryinternalsmoothness) (SolidEdgePart)
- [`SurfaceByBoundaryPatchTopology`](#surfacebyboundarypatchtopology) (SolidEdgePart)
- [`SurfaceByBoundaryTangencyType`](#surfacebyboundarytangencytype) (SolidEdgePart)
- [`SyncOption`](#syncoption) (SolidEdgeFramework)
- [`TCESETypes`](#tcesetypes) (SolidEdgeFramework)
- [`TemplatesListType`](#templateslisttype) (SolidEdgeFramework)
- [`TextStyleNumberJustificationConstants`](#textstylenumberjustificationconstants) (SolidEdgeFramework)
- [`ThreadDiameterOptionConstants`](#threaddiameteroptionconstants) (SolidEdgePart)
- [`TreatmentCrownCurvatureSideConstants`](#treatmentcrowncurvaturesideconstants) (SolidEdgePart)
- [`TreatmentCrownSideConstants`](#treatmentcrownsideconstants) (SolidEdgePart)
- [`TreatmentCrownTypeConstants`](#treatmentcrowntypeconstants) (SolidEdgePart)
- [`TreatmentTypeConstants`](#treatmenttypeconstants) (SolidEdgePart)
- [`TrimExtendErrorCode`](#trimextenderrorcode) (SolidEdgePart)
- [`TrimSurfaceAreaSideConstants`](#trimsurfaceareasideconstants) (SolidEdgePart)
- [`UnitOfMeasureAngleReadoutConstants`](#unitofmeasureanglereadoutconstants) (SolidEdgePart)
- [`UnitOfMeasureLengthReadoutConstants`](#unitofmeasurelengthreadoutconstants) (SolidEdgePart)
- [`UnitTypeConstants`](#unittypeconstants) (SolidEdgeFramework)
- [`UploadType`](#uploadtype) (SolidEdgeFramework)
- [`VariableLimitValueConstant`](#variablelimitvalueconstant) (SolidEdgeFramework)
- [`VentDraftSideConstants`](#ventdraftsideconstants) (SolidEdgePart)
- [`VentExtentSideConstants`](#ventextentsideconstants) (SolidEdgePart)
- [`VentExtentTypeConstants`](#ventextenttypeconstants) (SolidEdgePart)
- [`WebNetworkFeatureConstants`](#webnetworkfeatureconstants) (SolidEdgePart)
- [`WeldmentGlobalConstants`](#weldmentglobalconstants) (SolidEdgePart)
- [`WeldmentLinkStatusConstants`](#weldmentlinkstatusconstants) (SolidEdgePart)
- [`WeldmentSectionTypeConstants`](#weldmentsectiontypeconstants) (SolidEdgePart)
- [`WorkflowAction`](#workflowaction) (SolidEdgeFramework)
- [`WorkflowType`](#workflowtype) (SolidEdgeFramework)
- [`eCPDMode`](#ecpdmode) (SolidEdgeFramework)
- [`seMovieFormatConstants`](#semovieformatconstants) (SolidEdgeFramework)
- [`seMovieStandardResolutionConstants`](#semoviestandardresolutionconstants) (SolidEdgeFramework)
- [`seSharpenLevelConstants`](#sesharpenlevelconstants) (SolidEdgeFramework)
- [`seSteeringWheelConstants`](#sesteeringwheelconstants) (SolidEdgeFramework)
- [`seStyleTypeConstants`](#sestyletypeconstants) (SolidEdgeFramework)
- [`seUnitsTypeConstants`](#seunitstypeconstants) (SolidEdgeFramework)
- [`seVariableTypeConstants`](#sevariabletypeconstants) (SolidEdgeFramework)

## <a name="acceleratortypeconstants"></a>`AcceleratorTypeConstants`

_Type library:_ `SolidEdgeFramework`  
_Membros:_ 7

| Constante | Valor |
|-----------|-------|
| `seExecutable` | 1 |
| `seEmbeded` | 2 |
| `seServerInPlace` | 3 |
| `seContainerInPlace` | 4 |
| `seMainFrame` | 5 |
| `seServerInPlaceLink` | 6 |
| `seContainerInPlaceLink` | 7 |

## <a name="addbodytypeconstants"></a>`AddBodyTypeConstants`

_Type library:_ `SolidEdgePart`  
_Membros:_ 7

| Constante | Valor |
|-----------|-------|
| `igPartType` | 1 |
| `igSheetMetalType` | 2 |
| `igSubdivisionType` | 3 |
| `igSubdivisionControlCageType` | 4 |
| `igConstructionPartType` | 5 |
| `igConstructionSheetMetalType` | 6 |
| `igConstructionSubdivisionType` | 7 |

## <a name="alignbodyfacetype"></a>`AlignBodyFaceType`

_Type library:_ `SolidEdgePart`  
_Membros:_ 6

| Constante | Valor |
|-----------|-------|
| `seAlignBodyFaceTypeFront` | 0 |
| `seAlignBodyFaceTypeBack` | 1 |
| `seAlignBodyFaceTypeTop` | 2 |
| `seAlignBodyFaceTypeBottom` | 3 |
| `seAlignBodyFaceTypeLeft` | 4 |
| `seAlignBodyFaceTypeRight` | 5 |

## <a name="alignbodypointtypeonface"></a>`AlignBodyPointTypeOnFace`

_Type library:_ `SolidEdgePart`  
_Membros:_ 9

| Constante | Valor |
|-----------|-------|
| `seAlignBodyPointTypeOnFaceLeftBottom` | 0 |
| `seAlignBodyPointTypeOnFaceLeftMiddle` | 1 |
| `seAlignBodyPointTypeOnFaceLeftTop` | 2 |
| `seAlignBodyPointTypeOnFaceCenterBottom` | 3 |
| `seAlignBodyPointTypeOnFaceCenterMiddle` | 4 |
| `seAlignBodyPointTypeOnFaceCenterTop` | 5 |
| `seAlignBodyPointTypeOnFaceRightBottom` | 6 |
| `seAlignBodyPointTypeOnFaceRightMiddle` | 7 |
| `seAlignBodyPointTypeOnFaceRightTop` | 8 |

## <a name="animationeventconstants"></a>`AnimationEventConstants`

_Type library:_ `SolidEdgeFramework`  
_Membros:_ 4

| Constante | Valor |
|-----------|-------|
| `BeforeTimelineFrameUpdate` | 1 |
| `AfterTimelineFrameUpdate` | 2 |
| `BeforeDragComponentFrameUpdate` | 3 |
| `AfterDragComponentFrameUpdate` | 4 |

## <a name="applicationactiveframeswitchingevent"></a>`ApplicationActiveFrameSwitchingEvent`

_Type library:_ `SolidEdgeFramework`  
_Membros:_ 2

| Constante | Valor |
|-----------|-------|
| `ApplicationSwitchingToMainFrame` | 1 |
| `ApplicationSwitchingToFloatingFrame` | 2 |

## <a name="applicationbeforedocumentopenevent"></a>`ApplicationBeforeDocumentOpenEvent`

_Type library:_ `SolidEdgeFramework`  
_Membros:_ 6

| Constante | Valor |
|-----------|-------|
| `OpenFromUnknown` | 1 |
| `OpenFromMRU` | 2 |
| `OpenDropTagetApplication` | 3 |
| `OpenDropTargetDocumentView` | 4 |
| `OpenFromAutomation` | 5 |
| `OpenFromClipboardForCopyPasted` | 6 |

## <a name="applicationdocumentloadingevent"></a>`ApplicationDocumentLoadingEvent`

_Type library:_ `SolidEdgeFramework`  
_Membros:_ 1

| Constante | Valor |
|-----------|-------|
| `ApplicationWaitingForNextLevel` | 1 |

## <a name="applicationglobalconstants"></a>`ApplicationGlobalConstants`

_Type library:_ `SolidEdgeFramework`  
_Membros:_ 491

| Constante | Valor |
|-----------|-------|
| `seApplicationGlobalDisplayQuality` | 0 |
| `seApplicationGlobalDisplayArcQuality` | 1 |
| `seApplicationGlobalColorActive` | 2 |
| `seApplicationGlobalColorBackground` | 3 |
| `seApplicationGlobalColorConstruction` | 4 |
| `seApplicationGlobalColorDisabled` | 5 |
| `seApplicationGlobalColorFailed` | 6 |
| `seApplicationGlobalColorHandle` | 7 |
| `seApplicationGlobalColorHighlight` | 8 |
| `seApplicationGlobalColorProfile` | 9 |
| `seApplicationGlobalColorSelected` | 10 |
| `seApplicationGlobalColorSheet` | 11 |
| `seApplicationGlobalAutomaticSave` | 12 |
| `seApplicationGlobalAutomaticSaveTime` | 13 |
| `seApplicationGlobalDisplayStatistics` | 14 |
| `seApplicationGlobalDisplaySectionCaps` | 15 |
| `seApplicationGlobalSoftwareVHL` | 16 |
| `seApplicationGlobalDynamicTransition` | 17 |
| `seApplicationGlobalApplicationDisplay` | 18 |
| `seApplicationGlobalDefaultSharpness` | 19 |
| `seApplicationGlobalCheckInOnClose` | 20 |
| `seApplicationGlobalLogFilesLocation` | 21 |
| `seApplicationGlobalInsightCacheLocation` | 22 |
| `seApplicationGlobalInsightFolderMappingFileLocation` | 23 |
| `seApplicationGlobalSearchScope` | 24 |
| `seApplicationGlobalOfflineMode` | 25 |
| `seApplicationGlobalLookAheadVersion` | 26 |
| `seApplicationGlobalUploadOnClose` | 27 |
| `seApplicationGlobalOverlayColor` | 28 |
| `seApplicationGlobalOverlayColorMode` | 29 |
| `seApplicationGlobalEnableThreadedDisplay` | 30 |
| `seApplicationGlobalColorRefPlane` | 31 |
| `seApplicationGlobalOpacityRefPlane` | 32 |
| `seApplicationGlobalRevisionDelimiter` | 33 |
| `seApplicationGlobalCurvatureCombDensity` | 34 |
| `seApplicationGlobalCurvatureCombMagnitude` | 35 |
| `seApplicationGlobalApplyStatusToLinks` | 36 |
| `seApplicationGlobalUpdateDraft` | 37 |
| `seApplicationGlobalMakeRevisionsObsolete` | 38 |
| `seApplicationGlobalAvailableRootFolder` | 39 |
| `seApplicationGlobalObsoleteRootFolder` | 40 |
| `seApplicationGlobalInWorkRootFolder` | 41 |
| `seApplicationGlobalReleasedRootFolder` | 42 |
| `seApplicationGlobalInReviewRootFolder` | 43 |
| `seApplicationGlobalBaselinedRootFolder` | 44 |
| `seApplicationGlobalEnableDynamicTolerance` | 45 |
| `seApplicationGlobalSystemInfo` | 46 |
| `seApplicationGlobalPrereleaseRootFolder` | 47 |
| `seApplicationGlobalPackagedCollaborationRootFolder` | 48 |
| `seApplicationGlobalECRRootFolder` | 49 |
| `seApplicationGlobalECORootFolder` | 50 |
| `seApplicationGlobalTemplateRootFolder` | 51 |
| `seApplicationGlobalUseDimensionStyleMapping` | 52 |
| `seApplicationGlobalAdminFileLocation` | 53 |
| `seApplicationGlobalColorManagerUseToolsOptionsColorSettings` | 54 |
| `seApplicationGlobalColorManagerUseIndividualPartStyles` | 55 |
| `seApplicationGlobalColorManagerShowPartFaceColors` | 56 |
| `seApplicationGlobalColorManagerShowAssemblyStyleOverrides` | 57 |
| `seApplicationGlobalColorManagerCopyIndividualFaceColors` | 58 |
| `seApplicationGlobalTeamCenterMode` | 59 |
| `seApplicationGlobalSessionDraftOpenInactive` | 60 |
| `seApplicationGlobalHoleSizeFile` | 61 |
| `seApplicationGlobalPipeThreadfsFile` | 62 |
| `seApplicationGlobalCustomSettingFile` | 63 |
| `seApplicationGlobalHideAllComponents` | 64 |
| `seApplicationGlobalApplyActivationOverridesToParts` | 65 |
| `seApplicationGlobalActivateAllParts` | 66 |
| `seApplicationGlobalApplySimplifyOverridesToParts` | 67 |
| `seApplicationGlobalUseAllSimplifiedParts` | 68 |
| `seApplicationGlobalApplySimplifyOverridesToSubAssemblies` | 69 |
| `seApplicationGlobalUseAllSimplifiedSubAssemblies` | 70 |
| `seApplicationGlobalStatusBarZoom` | 71 |
| `seApplicationGlobalStatusBarZoomArea` | 72 |
| `seApplicationGlobalStatusBarFit` | 73 |
| `seApplicationGlobalStatusBarPan` | 74 |
| `seApplicationGlobalStatusBarRotateView` | 75 |
| `seApplicationGlobalStatusBarSpinAbout` | 76 |
| `seApplicationGlobalStatusBarLookAtFace` | 77 |
| `seApplicationGlobalStatusBarCommonViews` | 78 |
| `seApplicationGlobalStatusBarPreviousView` | 79 |
| `seApplicationGlobalStatusBarNamedViews` | 80 |
| `seApplicationGlobalStatusBarViewStyles` | 81 |
| `seApplicationGlobalStatusBarZoomSlider` | 82 |
| `seApplicationGlobalStatusBarZoomTool` | 83 |
| `seApplicationGlobalStatusBarCmdFinder` | 84 |
| `seApplicationGlobalUserType` | 85 |
| `seApplicationGlobalStatusBarSketchView` | 86 |
| `seApplicationGlobalTeamcenterFormula` | 87 |
| `seApplicationGlobalDocumentNameFormula` | 88 |
| `seApplicationGlobalOpenAsReadOnly3DFile` | 89 |
| `seApplicationGlobalOpenAsReadOnlyDftFile` | 90 |
| `seApplicationGlobalColorLiveSectionEdge` | 91 |
| `seApplicationGlobalColorLiveSectionCenterline` | 92 |
| `seApplicationGlobalColorLiveSectionRegion` | 93 |
| `seApplicationGlobalColorLiveSectionOpacity` | 94 |
| `seApplicationGlobalOpenAsReadOnly3DFile_IndirectFiles` | 95 |
| `seApplicationGlobalOpenAsReadOnlyDftFile_IndirectFiles` | 96 |
| `seApplicationGlobalANSIInchToleranceFile` | 97 |
| `seApplicationGlobalANSIMetricToleranceFile` | 98 |
| `seApplicationGlobalISOToleranceFile` | 99 |
| `seApplicationGlobalUseISOToleranceTable` | 100 |
| `seApplicationGlobalUseDetailEnvelopeStandard` | 101 |
| `seApplicationGlobalUseDetailEnvelopeDisplayAsCircle` | 102 |
| `seApplicationGlobalUseDrawingViewShowCroppingEdges` | 103 |
| `seApplicationGlobalShowUnitsInValueFields` | 104 |
| `seApplicationGlobalCommandBarMode` | 105 |
| `seApplicationGlobalUseDrawingViewShowEdgesHiddenTangentEdgesSelfHidden` | 106 |
| `seApplicationGlobalUseDrawingViewShowEdgesHiddenTangentEdgesHiddenByOtherParts` | 107 |
| `seApplicationGlobalZebraFormula` | 108 |
| `seApplicationGlobalZebraFolderMapping` | 109 |
| `seApplicationGlobalAutoSharpenLevel` | 110 |
| `seApplicationGlobalUseDimensionStyleElementMapLinDim` | 111 |
| `seApplicationGlobalUseDimensionStyleElementMapRadialDim` | 112 |
| `seApplicationGlobalUseDimensionStyleElementMapRadialDiameterDim` | 113 |
| `seApplicationGlobalUseDimensionStyleElementMapCircularDiameterDim` | 114 |
| `seApplicationGlobalUseDimensionStyleElementMapSymmetricDiameterDim` | 115 |
| `seApplicationGlobalUseDimensionStyleElementMapCoordinateDim` | 116 |
| `seApplicationGlobalUseDimensionStyleElementMapAngularDim` | 117 |
| `seApplicationGlobalUseDimensionStyleElementMapAngularCoordinateDim` | 118 |
| `seApplicationGlobalUseDimensionStyleElementMapChamferDim` | 119 |
| `seApplicationGlobalUseDimensionStyleElementMapCenterline` | 120 |
| `seApplicationGlobalUseDimensionStyleElementMapCenterMark` | 121 |
| `seApplicationGlobalUseDimensionStyleElementMapBHC` | 122 |
| `seApplicationGlobalUseDimensionStyleElementMapBalloon` | 123 |
| `seApplicationGlobalUseDimensionStyleElementMapCallouts` | 124 |
| `seApplicationGlobalUseDimensionStyleElementMapLeaders` | 125 |
| `seApplicationGlobalUseDimensionStyleElementMapConnectors` | 126 |
| `seApplicationGlobalUseDimensionStyleElementMapSTSymbols` | 127 |
| `seApplicationGlobalUseDimensionStyleElementMapWeldSymbols` | 128 |
| `seApplicationGlobalUseDimensionStyleElementMapEdgeCondition` | 129 |
| `seApplicationGlobalUseDimensionStyleElementMapDatumFrames` | 130 |
| `seApplicationGlobalUseDimensionStyleElementMapDatumPoints` | 131 |
| `seApplicationGlobalUseDimensionStyleElementMapDatumTargets` | 132 |
| `seApplicationGlobalUseDimensionStyleElementMapFCF` | 133 |
| `seApplicationGlobalUseDimensionStyleElementMapBlockLabels` | 134 |
| `seApplicationGlobalSaveMultiCADDatasettoTC` | 135 |
| `seApplicationGlobalSheetTabDisplayInfo` | 136 |
| `seApplicationGlobalSheetTabDisplayInfoSeparator` | 137 |
| `seApplicationGlobalColorSheetTab1` | 138 |
| `seApplicationGlobalColorSheetTab2` | 139 |
| `seApplicationGlobalChangedPartActivation` | 140 |
| `seApplicationGlobalSectionCurvatureCombDensity` | 141 |
| `seApplicationGlobalSectionCurvatureCombMagnitude` | 142 |
| `seApplicationGlobalUseOnLineHelp` | 143 |
| `seApplicationGlobalOnLineHelpLocation` | 144 |
| `seApplicationGlobalStatusBarRecordVideo` | 145 |
| `seApplicationGlobalStatusBarUploadToYouTube` | 146 |
| `seApplicationGlobalGRID_Draft_ShowGrid` | 147 |
| `seApplicationGlobalGRID_Draft_SnapToGrid` | 148 |
| `seApplicationGlobalGRID_Draft_ShowReadout` | 149 |
| `seApplicationGlobalGRID_Draft_ShowAlignmentLines` | 150 |
| `seApplicationGlobalGRID_Draft_ShowKeyIns` | 151 |
| `seApplicationGlobalGRID_Draft_MajorLineColor` | 152 |
| `seApplicationGlobalGRID_Draft_MinorLineColor` | 153 |
| `seApplicationGlobalGRID_Sync_ShowGrid` | 154 |
| `seApplicationGlobalGRID_Sync_SnapToGrid` | 155 |
| `seApplicationGlobalGRID_Sync_ShowReadout` | 156 |
| `seApplicationGlobalGRID_Sync_ShowAlignmentLines` | 157 |
| `seApplicationGlobalGRID_Sync_ShowKeyIns` | 158 |
| `seApplicationGlobalGRID_Sync_MajorLineColor` | 159 |
| `seApplicationGlobalGRID_Sync_MinorLineColor` | 160 |
| `seApplicationGlobalGRID_Ordered_ShowGrid` | 161 |
| `seApplicationGlobalGRID_Ordered_SnapToGrid` | 162 |
| `seApplicationGlobalGRID_Ordered_ShowReadout` | 163 |
| `seApplicationGlobalGRID_Ordered_ShowAlignmentLines` | 164 |
| `seApplicationGlobalGRID_Ordered_ShowKeyIns` | 165 |
| `seApplicationGlobalGRID_Ordered_MajorLineColor` | 166 |
| `seApplicationGlobalGRID_Ordered_MinorLineColor` | 167 |
| `seApplicationGlobalDraftSaveAsPDFSaveAllColorsBlack` | 168 |
| `seApplicationGlobalDraftSaveAsPDFIncludeGridDisplay` | 169 |
| `seApplicationGlobalDraftSaveAsPDFTransparentDVBackgrounds` | 170 |
| `seApplicationGlobalDraftSaveAsPDFPrintQualityDPI` | 171 |
| `seApplicationGlobalDraftSaveAsPDFSheetOptions` | 172 |
| `seApplicationGlobalDraftSaveAsPDFSheetsRange` | 173 |
| `seApplicationGlobalDraftSaveAsPDFUseIndividualSheetSizes` | 174 |
| `seApplicationGlobalShowUnitsValueField` | 175 |
| `seApplicationGlobalPromptMatmodelDoc` | 176 |
| `seApplicationGlobalStoreGeomPart` | 177 |
| `seApplicationGlobalRecentlyUsedFilesBool` | 178 |
| `seApplicationGlobalReferencePlaneSize` | 179 |
| `seApplicationGlobalMaximumPrintFileSize` | 180 |
| `seApplicationGlobalFeatureOriginSize` | 181 |
| `seApplicationGlobalPartandAsmUndoSteps` | 182 |
| `seApplicationGlobalEnableDynamicEditProfiles` | 183 |
| `seApplicationGlobalRecomputeAssemblySketchEdit` | 184 |
| `seApplicationGlobalEnableValuechangeUsingMouseWheel` | 185 |
| `seApplicationGlobalProfileUndoSteps` | 186 |
| `seApplicationGlobalPaste2dBehaviourConstant` | 187 |
| `seApplicationGlobalIndicateUnderConstraintProfilesPF` | 188 |
| `seApplicationGlobalEnableUndoAllProfileSketch` | 189 |
| `seApplicationGlobalEnterProfileSketchCreateNewWindow` | 190 |
| `seApplicationGlobalOrientWindowSelectedPlane` | 191 |
| `seApplicationGlobalPropertyTextError` | 192 |
| `seApplicationGlobalRecentlyUsedFilesValue` | 193 |
| `seApplicationGlobalShowOrientationTriad` | 194 |
| `seApplicationGlobal3dInputdevice` | 195 |
| `seApplicationGlobalViewTransitionValue` | 196 |
| `seApplicationGlobalCullingBool` | 197 |
| `seApplicationGlobalAutomaticSelection` | 198 |
| `seApplicationGlobalAutoSharpen` | 200 |
| `seApplicationGlobalReferenceScale` | 201 |
| `seApplicationGlobalSteeringwheelSize` | 202 |
| `seApplicationGlobalArcSmoothness` | 203 |
| `seApplicationGlobalOrientXpressSize` | 204 |
| `seApplicationGlobalUseShadingSelection` | 205 |
| `seApplicationGlobalAutoPreserveDocBackup` | 206 |
| `seApplicationGlobalUseShadingRefplanes` | 207 |
| `seApplicationGlobalDimSurroundingComponents` | 208 |
| `seApplicationGlobalProcesshiddenedges` | 209 |
| `seApplicationGlobalDisplaydropshadows` | 210 |
| `seApplicationGlobalDisplayInterPartCopies` | 211 |
| `seApplicationGlobalShowtriangulationbendlines` | 212 |
| `seApplicationGlobalDynamicClipping` | 213 |
| `seApplicationGlobalDynamicPreviewFeaturecreation` | 214 |
| `seApplicationGlobalCullingValue` | 215 |
| `seApplicationGlobalSetBackGroundColor` | 216 |
| `seApplicationGlobalSetHighlightColor` | 217 |
| `seApplicationGlobalSetSelectedColor` | 218 |
| `seApplicationGlobalSetProfileSelectionColor` | 219 |
| `seApplicationGlobalSetGuidePathColor` | 220 |
| `seApplicationGlobalSetSketchColor` | 221 |
| `seApplicationGlobalSetPlaneColor` | 222 |
| `seApplicationGlobalSetPlaneEdgesColor` | 223 |
| `seApplicationGlobalSetPlaneFeatureColor` | 224 |
| `seApplicationGlobalSetChildFeatureColor` | 225 |
| `seApplicationGlobalSetHandle1Color` | 226 |
| `seApplicationGlobalSetHandle2Color` | 227 |
| `seApplicationGlobalSetHandle3Color` | 228 |
| `seApplicationGlobalSetDrivenColor` | 229 |
| `seApplicationGlobalSetFailedColor` | 230 |
| `seApplicationGlobalActivePartColor` | 231 |
| `seApplicationGlobalSetInactiveColor` | 232 |
| `seApplicationGlobalSetConstructionColor` | 233 |
| `seApplicationGlobalSetGeneralBodyColor` | 234 |
| `seApplicationGlobalSetFullyDefinedColor` | 235 |
| `seApplicationGlobalSetUnderDefinedColor` | 236 |
| `seApplicationGlobalSetOverDefinedColor` | 237 |
| `seApplicationGlobalSetInconsistantColor` | 238 |
| `seApplicationGlobalSetRegionColor` | 239 |
| `seApplicationGlobalSetRegionOpacity` | 240 |
| `seApplicationGlobalUseshading2DFence` | 241 |
| `seApplicationGlobalSet2DSelectionFenceOpacity` | 242 |
| `seApplicationGlobalInside` | 243 |
| `seApplicationGlobalInsideOutside` | 244 |
| `seApplicationGlobalAutoPreserveDoc` | 245 |
| `seApplicationGlobalAutoPreserveDocAutoSave` | 246 |
| `seApplicationGlobalAutoSaveMinutes` | 247 |
| `seApplicationGlobalBackupMinutes` | 248 |
| `seApplicationGlobalBackupModelfiles` | 249 |
| `seApplicationGlobalBackupDraftfiles` | 250 |
| `seApplicationGlobalPromptForfileProp` | 251 |
| `seApplicationGlobalUpdateAll` | 252 |
| `seApplicationGlobalAllowInterPart` | 253 |
| `seApplicationGlobalInterPartCopyCommand` | 254 |
| `seApplicationGlobalIncludeCommandPartAssemblySketches` | 255 |
| `seApplicationGlobalSketchRelationshipPeerEdges` | 256 |
| `seApplicationGlobalAssemblyReferencePlanefeature` | 257 |
| `seApplicationGlobalAssemblydrivenPartfeatures` | 258 |
| `seApplicationGlobalPasteLinkVariableTable` | 259 |
| `seApplicationGlobalLinkMgmt` | 260 |
| `seApplicationGlobalMacros` | 261 |
| `seApplicationGlobalPropSeedFile` | 262 |
| `seApplicationGlobalReports` | 263 |
| `seApplicationGlobalConfigNames` | 264 |
| `seApplicationGlobalMatTableFile` | 265 |
| `seApplicationGlobalSheetMetalGage` | 266 |
| `seApplicationGlobalMatTableFolder` | 267 |
| `seApplicationGlobalObsolute` | 270 |
| `seApplicationGlobalManagedStdParts` | 271 |
| `seApplicationGlobalExternalBom` | 272 |
| `seApplicationGlobalNXNAsternScratch` | 273 |
| `seApplicationGlobalAlwayaUploadToServerCheekedOutToMe` | 274 |
| `seApplicationGlobalAlwaysUploadToServerCheckDocIn` | 275 |
| `seApplicationGlobalAutomaticallyReviseDrawingSelected3Ddoc` | 276 |
| `seApplicationGlobalAutoMakeSiblingParentRevisionObsolute` | 277 |
| `seApplicationGlobalAutoSetDraftStatus` | 278 |
| `seApplicationGlobalStopLifeCycleProcess` | 279 |
| `seApplicationGlobalLimitSearchToSearchServicesScope` | 280 |
| `seApplicationGlobalEdgeCondition` | 282 |
| `seApplicationGlobalLimitsAndFits` | 283 |
| `seApplicationGlobalWeldSymbols` | 284 |
| `seApplicationGlobalStudyType` | 285 |
| `seApplicationGlobalGraphicsColorForce` | 286 |
| `seApplicationGlobalGraphicsColorPressure` | 287 |
| `seApplicationGlobalGraphicsColorGravity` | 288 |
| `seApplicationGlobalGraphicsColorTorqueMomnent` | 289 |
| `seApplicationGlobalGraphicsColorBearing` | 290 |
| `seApplicationGlobalGraphicsColorBeamCurve` | 291 |
| `seApplicationGlobalGraphicsColorNode` | 292 |
| `seApplicationGlobalGraphicsColorRigidLink` | 293 |
| `seApplicationGlobalGraphicsColorAngVelocity` | 294 |
| `seApplicationGlobalGraphicsColorAngAccleration` | 295 |
| `seApplicationGlobalGraphicsColorDisplacement` | 296 |
| `seApplicationGlobalGraphicsColorBodyTemp` | 297 |
| `seApplicationGlobalGraphicsColorConstraints` | 298 |
| `seApplicationGlobalGraphicsColorConnectorTarget` | 299 |
| `seApplicationGlobalGraphicsColorConnectorSource` | 300 |
| `seApplicationGlobalGraphicsColorHeatFlux` | 301 |
| `seApplicationGlobalGraphicsColorHeatGen` | 302 |
| `seApplicationGlobalGraphicsColorConvection` | 303 |
| `seApplicationGlobalGraphicsColorRadition` | 304 |
| `seApplicationGlobalGraphicsColorTemperature` | 305 |
| `seApplicationGlobalShowStartUpScreen` | 306 |
| `seApplicationGlobalStartUsingTemplate` | 307 |
| `seApplicationGlobalStartLastSaveDoc` | 308 |
| `seApplicationGlobalStartEnvironmentSyncOrOrdered` | 309 |
| `seApplicationGlobalShowCommandTips` | 310 |
| `seApplicationGlobalShowSensorIndicator` | 311 |
| `seApplicationGlobalApplicationColorScheme` | 312 |
| `seApplicationGlobalShowPFDocView` | 313 |
| `seApplicationGlobalPFAppereance` | 314 |
| `seApplicationGlobalIncreaseButtonsCommandRibbon2x` | 315 |
| `seApplicationGlobalCommandUserInterface` | 316 |
| `seApplicationGlobalUseGesters` | 317 |
| `seApplicationGlobalShowLiveRules` | 318 |
| `seApplicationGlobalMakeLiveRulePanelVertical` | 319 |
| `seApplicationGlobalUseWebBrowser` | 320 |
| `seApplicationGlobalDisperseAsmPlacePart` | 321 |
| `seApplicationGlobalDontCreateNewWndPlacePart` | 322 |
| `seApplicationGlobalFastlocateboxDisplayParts` | 323 |
| `seApplicationGlobalFastlocateBoxDisplayAsm` | 324 |
| `seApplicationGlobalFastlocateOverPathfinder` | 325 |
| `seApplicationGlobalUseformulaPlacementName` | 326 |
| `seApplicationGlobalUseDefaultPlacementName` | 327 |
| `seApplicationGlobalPatternedPartsInherit` | 328 |
| `seApplicationGlobalInactiveHiddenUnusedComponents` | 329 |
| `seApplicationGlobalMaintainRelationships` | 330 |
| `seApplicationGlobalShowPartReference` | 331 |
| `seApplicationGlobalUseSimplifiedModels` | 332 |
| `seApplicationGlobalAutoHideRelationshipPathfinder` | 333 |
| `seApplicationGlobalAsmOpenas` | 334 |
| `seApplicationGlobalUsewhenPlacingAsmPartslib` | 335 |
| `seApplicationGlobalPartActivationSmallAsm` | 336 |
| `seApplicationGlobalPartActivationMediumAsm` | 337 |
| `seApplicationGlobalPartActivationLargeAsm` | 338 |
| `seApplicationGlobalPartSimplificationSmallAsm` | 339 |
| `seApplicationGlobalPartSimplificationMedAsm` | 340 |
| `seApplicationGlobalPartSimplificationLargeAsm` | 341 |
| `seApplicationGlobalSubAsmSimplificationSmallAsm` | 342 |
| `seApplicationGlobalSubAsmSimplificationMedAsm` | 343 |
| `seApplicationGlobalSubAsmSimplificationLargeAsm` | 344 |
| `seApplicationGlobalMaintainItemNumbers` | 345 |
| `seApplicationGlobalUselevelbaseNumbers` | 346 |
| `seApplicationGlobalExpandWeldmentSubasms` | 347 |
| `seApplicationGlobalFramePipingUniquenessCutLength` | 348 |
| `seApplicationGlobalFramePipingUniquenessMass` | 349 |
| `seApplicationGlobalFramePipingUniquenessMiter` | 350 |
| `seApplicationGlobalAutoScrollASMPathfinder` | 351 |
| `seApplicationGlobalHideAllComponentsSmallAsm` | 352 |
| `seApplicationGlobalHideAllComponentsMedAsm` | 353 |
| `seApplicationGlobalHideAllComponentsLargeAsm` | 354 |
| `seApplicationGlobalActivateChangePartFileSmallAsm` | 355 |
| `seApplicationGlobalActivateChangedPartsFileMedAsm` | 356 |
| `seApplicationGlobalActivateChangedPartsFileLargeAsm` | 357 |
| `seApplicationGlobalIncludeDraftViewerData` | 358 |
| `seApplicationGlobalSaveColorsBlackWhite` | 359 |
| `seApplicationGlobalIncludeWorkingSheets` | 360 |
| `seApplicationGlobalInclude2DModelSheets` | 361 |
| `seApplicationGlobalIncludeBackgroundSheets` | 362 |
| `seApplicationGlobalUpdateLinkAutomatically` | 363 |
| `seApplicationGlobalDisplayUnitsMeasurement` | 364 |
| `seApplicationGlobalDoubleClickblock` | 365 |
| `seApplicationGlobalDoubleclickEmbeddedObjects` | 366 |
| `seApplicationGlobalCheckModelChanges` | 367 |
| `seApplicationGlobalAsmConfigurationChangesDrawingView` | 368 |
| `seApplicationGlobalUseConfigOrModelViewShow` | 369 |
| `seApplicationGlobalEnableSheetDistanceDimValue` | 370 |
| `seApplicationGlobalDimKeyInValueAuto` | 371 |
| `seApplicationGlobalHorizontalScrollBar` | 372 |
| `seApplicationGlobalVerticalScrollBar` | 373 |
| `seApplicationGlobalShowDragRectTimeValue` | 374 |
| `seApplicationGlobalZoomToolLeftClick` | 375 |
| `seApplicationGlobalZoomToolRightClick` | 376 |
| `seApplicationGlobalZoomToolLeftDrag` | 377 |
| `seApplicationGlobalZoomToolRightDrag` | 378 |
| `seApplicationGlobalSheetNoNameSeprator` | 379 |
| `seApplicationGlobalNumberSheetGroupSeparately` | 380 |
| `seApplicationGlobalDisplaySheet` | 381 |
| `seApplicationGlobalSheetColorDraft` | 382 |
| `seApplicationGlobalHighlightColorDraft` | 383 |
| `seApplicationGlobalSelElementColorDraft` | 384 |
| `seApplicationGlobalDisabledEleColorDraft` | 385 |
| `seApplicationGlobalHandle1ColorDraft` | 386 |
| `seApplicationGlobalHandle2ColorDraft` | 387 |
| `seApplicationGlobalHandle3ColorDraft` | 388 |
| `seApplicationGlobalUseShadingDraft` | 389 |
| `seApplicationGlobalOpacityColorDraft` | 390 |
| `seApplicationGlobalInsideColorDraft` | 391 |
| `seApplicationGlobalInsideOutsideColorDraft` | 392 |
| `seApplicationGlobalSheetTab1ColorDraft` | 393 |
| `seApplicationGlobalSheetTab2ColorDraft` | 394 |
| `seApplicationGlobalDeriveDisplayAsReference` | 395 |
| `seApplicationGlobalVisibleEdgesStyleFromAsm` | 396 |
| `seApplicationGlobalHiddenEdgeStyleFromAsm` | 397 |
| `seApplicationGlobalTangentEdgeStyleFromAsm` | 398 |
| `seApplicationGlobalVisibleEdgeStyle` | 399 |
| `seApplicationGlobalHiddenEdgeStyle` | 400 |
| `seApplicationGlobalTangentEdgeStyle` | 401 |
| `seApplicationGlobalOnlyGenerateEdgesInsideOverlappingCroppedBoundaries` | 402 |
| `seApplicationGlobalShowEdgesByCuttingPlane` | 403 |
| `seApplicationGlobalSimplifyBSplineEdges` | 404 |
| `seApplicationGlobalPartIntersections` | 405 |
| `seApplicationGlobalThreadDisplayMode` | 406 |
| `seApplicationGlobalProjectionAngleDraft` | 407 |
| `seApplicationGlobalEdgeConditionDraft` | 408 |
| `seApplicationGlobalWeldSymbolsDraft` | 409 |
| `seApplicationGlobalCutHardwareSectionViews` | 410 |
| `seApplicationGlobalHatchRibsSectionViews` | 411 |
| `seApplicationGlobalLimitsViewsDraft` | 412 |
| `seApplicationGlobalDetailEnvelope` | 413 |
| `seApplicationGlobalDisplayAsCircle` | 414 |
| `seApplicationGlobalExplodedFlowLineStyle` | 415 |
| `seApplicationGlobalBoundaryEdgesStyle` | 416 |
| `seApplicationGlobalShowSheetNumberParentAnnotation` | 417 |
| `seApplicationGlobalShowDrawingViewScale` | 418 |
| `seApplicationGlobalShowRotationAngle` | 419 |
| `seApplicationGlobalBendupCenterlineStyle` | 421 |
| `seApplicationGlobalBendDownCenterlineStyle` | 422 |
| `seApplicationGlobalOriginEdgeStyle` | 423 |
| `seApplicationGlobalProfileEdgeStyle` | 424 |
| `seApplicationGlobalUseDrawingViewWizardModelsDragged` | 425 |
| `seApplicationGlobalUseDrawingViewCommandBarDRWcommand` | 426 |
| `seApplicationGlobalPartDynamicDisplay` | 427 |
| `seApplicationGlobalSheetMetalDynamicDisplay` | 428 |
| `seApplicationGlobalSmallAsmDynamicDisplay` | 429 |
| `seApplicationGlobalMediumAsmDynamicDisplay` | 430 |
| `seApplicationGlobalLargeAsmDynamicDisplay` | 431 |
| `seApplicationGlobalSmallAsmOccurence` | 432 |
| `seApplicationGlobalLargeAsmOccurence` | 433 |
| `seApplicationGlobalLimitedUpdate` | 434 |
| `seApplicationGlobalLimitedSave` | 435 |
| `seApplicationGlobalStatusBarQuickViewCube` | 436 |
| `seApplicationGlobalLimitedUpdateEnable` | 437 |
| `seApplicationGlobalLimitedSaveEnable` | 438 |
| `seApplicationGlobalZone` | 439 |
| `seApplicationGlobalConfiguration` | 440 |
| `seApplicationGlobalEnableDotNet2GC` | 441 |
| `seApplicationGlobalEnableDotNet4GC` | 442 |
| `seApplicationGlobalTemplatePathFolder` | 443 |
| `seApplicationGlobalTextProfileText` | 444 |
| `seApplicationGlobalTextProfileSize` | 445 |
| `seApplicationGlobalTextProfileLetterSpacing` | 446 |
| `seApplicationGlobalTextProfileLineSpacing` | 447 |
| `seApplicationGlobalTextProfileLeftAlignment` | 448 |
| `seApplicationGlobalTextProfileCenterAlignment` | 449 |
| `seApplicationGlobalTextProfileRightAlignment` | 450 |
| `seApplicationGlobalTextProfileSmoothness` | 451 |
| `seApplicationGlobalTextProfileMargin` | 452 |
| `seApplicationGlobalTextProfileBoldState` | 453 |
| `seApplicationGlobalTextProfileItalicState` | 454 |
| `seApplicationGlobalTextProfileFontName` | 455 |
| `seApplicationGlobalTextProfileScript` | 456 |
| `seApplicationGlobalModelingStandard` | 457 |
| `seApplicationGlobalSTEPAdapterKey` | 458 |
| `seApplicationGlobalSimulationToggleSwitch` | 459 |
| `seApplicationGlobalAntiAliasLevel` | 460 |
| `seApplicationGlobalAntiAliasActiveLevel` | 461 |
| `seApplicationGlobalAntiAliasState` | 462 |
| `seApplicationGlobalCustomOccurrencePropertyFile` | 463 |
| `seApplicationGlobalPopupSwapping` | 464 |
| `seApplicationGlobalStatusBarGhostMode` | 465 |
| `seApplicationGlobalFloorReflectionIntensity` | 466 |
| `seApplicationGlobalEnableRegionsWithinSketches` | 467 |
| `seApplicationGlobalCheckOODMaterialOnFileOpen` | 468 |
| `seApplicationGlobalDefaultUserLangID` | 469 |
| `seApplicationGlobalDraftSaveAsPDFAutoRotateSheetsForTextReadability` | 470 |
| `seApplicationGlobalUseShadingHighlight` | 475 |
| `seApplicationGlobalOEMMode` | 476 |
| `seApplicationGlobalOEMName` | 477 |
| `seApplicationGlobalOEMApplicationName` | 478 |
| `seApplicationGlobalOEMMajorVersion` | 479 |
| `seApplicationGlobalOEMBuildVersion` | 480 |
| `seApplicationGlobalNewDocumentTabPosition` | 481 |
| `seApplicationGlobal_AutoScalePreference` | 482 |
| `seApplicationGlobalSEDMObsoletePreviousRevision` | 483 |
| `seApplicationGlobalSEDMSetDraftStatusAsPlacedPart` | 484 |
| `seApplicationGlobalOnLineHelpUseDefaultBrowser` | 485 |
| `seApplicationGlobalDraftCullingEnabled` | 486 |
| `seApplicationGlobalDraftCullingLevel` | 487 |
| `seApplicationGlobalConceptMode` | 489 |
| `seApplicationGlobalFramePipingUniquenessAngleOrientation` | 490 |
| `seApplicationGlobalStatusBarYouTube` | 491 |
| `seApplicationGlobalDraftIncludeWatermarkInPdf` | 492 |
| `seApplicationGlobalStoreGeometryInAssemblyForPreview` | 493 |
| `seApplicationGlobalUsePreviewForLargeAssemblies` | 494 |
| `seApplicationGlobalSWMigrationInProgress` | 495 |
| `seApplicationGlobalSWMigrationModeReq` | 496 |
| `seApplicationGlobalShadedSketches` | 497 |
| `seApplicationGlobalHolesDatabaseFolder` | 498 |
| `seApplicationGlobalLastPathRegValue` | 499 |
| `seApplicationGlobalLastFilterRegValue` | 500 |

## <a name="applicationlicenseevent"></a>`ApplicationLicenseEvent`

_Type library:_ `SolidEdgeFramework`  
_Membros:_ 2

| Constante | Valor |
|-----------|-------|
| `ApplicationLicenseCheckin` | 1 |
| `ApplicationLicenseCheckout` | 2 |

## <a name="applicationreadyevent"></a>`ApplicationReadyEvent`

_Type library:_ `SolidEdgeFramework`  
_Membros:_ 2

| Constante | Valor |
|-----------|-------|
| `ApplicationIsUIReady` | 1 |
| `ActiveDocumentIsUIReady` | 2 |

## <a name="arrangewindowsstyles"></a>`ArrangeWindowsStyles`

_Type library:_ `SolidEdgeFramework`  
_Membros:_ 4

| Constante | Valor |
|-----------|-------|
| `igWindowsTiled` | 1 |
| `igWindowsHorizontal` | 2 |
| `igWindowsVertical` | 4 |
| `igWindowsCascade` | 8 |

## <a name="assemblychangeeventsconstants"></a>`AssemblyChangeEventsConstants`

_Type library:_ `SolidEdgeFramework`  
_Membros:_ 12

| Constante | Valor |
|-----------|-------|
| `seAssemblyOccurrenceRename` | 1 |
| `seAssemblyFeatureRename` | 2 |
| `seAssemblyComponentShow` | 3 |
| `seAssemblyComponentHide` | 4 |
| `seAssemblyOccurrenceAdd` | 5 |
| `seAssemblyOccurrenceRemove` | 6 |
| `seAssemblyOccurrenceTransform` | 7 |
| `seAssemblySketchModify` | 8 |
| `seAssemblyFeatureModify` | 9 |
| `seAssemblyOccurrenceGeomModify` | 10 |
| `seAssemblyComponentSuppress` | 11 |
| `seAssemblyComponentUnSuppress` | 12 |

## <a name="assemblyeventconstants"></a>`AssemblyEventConstants`

_Type library:_ `SolidEdgeFramework`  
_Membros:_ 1

| Constante | Valor |
|-----------|-------|
| `seAssemblyOccurrenceReplace` | 1 |

## <a name="assemblyweldmentoccurrencesoptionsconstants"></a>`AssemblyWeldmentOccurrencesOptionsConstants`

_Type library:_ `SolidEdgePart`  
_Membros:_ 3

| Constante | Valor |
|-----------|-------|
| `seIncludeAllOccurrences` | 1 |
| `seIncludeInputOccurrences` | 2 |
| `seExcludeInputOccurrences` | 3 |

## <a name="attachedstatusconstants"></a>`AttachedStatusConstants`

_Type library:_ `SolidEdgePart`  
_Membros:_ 8

| Constante | Valor |
|-----------|-------|
| `seStatusOK` | 0 |
| `seStatusMissingPropertyObject` | 1 |
| `seStatusMissingPropertyTable` | 2 |
| `seStatusMissingAttachedEntites` | 3 |
| `seStatusDuplicateProperties` | 4 |
| `seStatusPropertyUpdateFailed` | 5 |
| `seStatusSickAttachedEntities` | 6 |
| `seAttachmentStatusUnknown` | 7 |

## <a name="attributetypeconstants"></a>`AttributeTypeConstants`

_Type library:_ `SolidEdgeFramework`  
_Membros:_ 11

| Constante | Valor |
|-----------|-------|
| `seByte` | 16 |
| `seInteger` | 2 |
| `seLong` | 3 |
| `seSingle` | 4 |
| `seDouble` | 5 |
| `seCurrency` | 6 |
| `seDate` | 7 |
| `seStringANSI` | 8 |
| `seStringUnicode` | 64 |
| `seBoolean` | 11 |
| `seByteArray` | 8209 |

## <a name="bendfeatureconstants"></a>`BendFeatureConstants`

_Type library:_ `SolidEdgePart`  
_Membros:_ 20

| Constante | Valor |
|-----------|-------|
| `seBendOnlyCornerRelief` | 1 |
| `seBendAndFaceCornerRelief` | 2 |
| `seBendExtendMoldlines` | 3 |
| `seBendNoExtendMoldines` | 4 |
| `seBendMoveRight` | 5 |
| `seBendMoveLeft` | 6 |
| `seBendNormal` | 7 |
| `seBendReverseNormal` | 8 |
| `seBendParamNFT` | 9 |
| `seBendParamEqn` | 10 |
| `seBendPZLInside` | 11 |
| `seBendPZLOutside` | 12 |
| `seBendPZLLeft` | 13 |
| `seBendPZLRight` | 14 |
| `seBendPZLSymmetric` | 15 |
| `seBendReliefRectangular` | 16 |
| `seBendReliefFillet` | 17 |
| `seBendStateFlat` | 18 |
| `seBendStateBent` | 19 |
| `seBendAndFaceChainRelief` | 20 |

## <a name="blendshapeconstants"></a>`BlendShapeConstants`

_Type library:_ `SolidEdgePart`  
_Membros:_ 6

| Constante | Valor |
|-----------|-------|
| `igBlendShapeConstantRadius` | 1 |
| `igBlendShapeConstantWidth` | 2 |
| `igBlendShapeChamfer` | 3 |
| `igBlendShapeRatioChamfer` | 4 |
| `igBlendShapeConic` | 5 |
| `igBlendShapeG2Continuous` | 6 |

## <a name="blocklabeloriginlocationconstants"></a>`BlockLabelOriginLocationConstants`

_Type library:_ `SolidEdgePart`  
_Membros:_ 12

| Constante | Valor |
|-----------|-------|
| `igBlockLabelTopLeft` | 0 |
| `igBlockLabelTopCenter` | 1 |
| `igBlockLabelTopRight` | 2 |
| `igBlockLabelMiddleLeft` | 3 |
| `igBlockLabelMiddleCenter` | 4 |
| `igBlockLabelMiddleRight` | 5 |
| `igBlockLabelBottomLeft` | 6 |
| `igBlockLabelBottomCenter` | 7 |
| `igBlockLabelBottomRight` | 8 |
| `igBlockLabelUnderLeft` | 9 |
| `igBlockLabelUnderCenter` | 10 |
| `igBlockLabelUnderRight` | 11 |

## <a name="booleanfeatureconstants"></a>`BooleanFeatureConstants`

_Type library:_ `SolidEdgePart`  
_Membros:_ 5

| Constante | Valor |
|-----------|-------|
| `seBooleanIntersect` | 1 |
| `seBooleanSubtract` | 2 |
| `seBooleanUnite` | 3 |
| `seBooleanPlaneFront` | 4 |
| `seBooleanPlaneBack` | 5 |

## <a name="bulkmigrationtypeconstants"></a>`BulkMigrationTypeConstants`

_Type library:_ `SolidEdgeFramework`  
_Membros:_ 6

| Constante | Valor |
|-----------|-------|
| `igNoBulkMigration` | 0 |
| `igTDMBulkMigration` | 1 |
| `igProEBulkMigration` | 2 |
| `igNX2DBulkMigration` | 3 |
| `igMDTBulkMigration` | 4 |
| `igSWBulkMigration` | 5 |

## <a name="cageoffsettypes"></a>`CageOffsetTypes`

_Type library:_ `SolidEdgePart`  
_Membros:_ 2

| Constante | Valor |
|-----------|-------|
| `CageOffsetTypeTip` | 1 |
| `CageOffsetTypeLift` | 2 |

## <a name="capturedrelationshipoffsettypeconstants"></a>`CapturedRelationshipOffsetTypeConstants`

_Type library:_ `SolidEdgeFramework`  
_Membros:_ 3

| Constante | Valor |
|-----------|-------|
| `seFixed` | 0 |
| `seFloating` | 1 |
| `seOffsetNotSupported` | 2 |

## <a name="capturedrelationshiptypeconstants"></a>`CapturedRelationshipTypeConstants`

_Type library:_ `SolidEdgeFramework`  
_Membros:_ 6

| Constante | Valor |
|-----------|-------|
| `seMate` | 0 |
| `sePlanarAlign` | 1 |
| `seAxialAlign` | 2 |
| `seTangent` | 3 |
| `seConnect` | 4 |
| `seParallel` | 5 |

## <a name="checkinoptions"></a>`CheckInOptions`

_Type library:_ `SolidEdgeFramework`  
_Membros:_ 2

| Constante | Valor |
|-----------|-------|
| `DoNotCheckInOption` | 0 |
| `UploadAndCheckInOption` | 1 |

## <a name="cleanprofileoptions"></a>`CleanProfileOptions`

_Type library:_ `SolidEdgePart`  
_Membros:_ 2

| Constante | Valor |
|-----------|-------|
| `igCleanProfileDelete` | 1 |
| `igCleanProfileMove` | 2 |

## <a name="closecornerfeatureconstants"></a>`CloseCornerFeatureConstants`

_Type library:_ `SolidEdgePart`  
_Membros:_ 9

| Constante | Valor |
|-----------|-------|
| `seCloseCornerCloseFaces` | 1 |
| `seCloseCornerOverlapFaces` | 2 |
| `seCloseCornerTreatmentOff` | 3 |
| `seCloseCornerTreatmentIntersect` | 4 |
| `seCloseCornerTreatmentCircularCutout` | 5 |
| `seCloseCornerTreatmentUCutout` | 6 |
| `seCloseCornerTreatmentVCutout` | 7 |
| `seCloseCornerTreatmentSquareCutout` | 8 |
| `seCloseCornerTreatmentMiter` | 9 |

## <a name="commandbarheaderdialogcontrolids"></a>`CommandBarHeaderDialogControlIDs`

_Type library:_ `SolidEdgeFramework`  
_Membros:_ 2

| Constante | Valor |
|-----------|-------|
| `CommandBarHeaderDoitButton` | 1073 |
| `CommandBarHeaderOptionsButton` | 1074 |

## <a name="configforforeignfiletype"></a>`ConfigForForeignFileType`

_Type library:_ `SolidEdgeFramework`  
_Membros:_ 1

| Constante | Valor |
|-----------|-------|
| `seAutoCADConfigFile` | 1067709598 |

## <a name="configresettype"></a>`ConfigResetType`

_Type library:_ `SolidEdgeFramework`  
_Membros:_ 2

| Constante | Valor |
|-----------|-------|
| `seResetAll` | -1801520595 |
| `seResetGroup` | -1957181463 |

## <a name="cookiedatatoget"></a>`CookieDataToGet`

_Type library:_ `SolidEdgeFramework`  
_Membros:_ 1

| Constante | Valor |
|-----------|-------|
| `GET_REVISION_RULE` | 0 |

## <a name="coordinatesystemfeatureconstants"></a>`CoordinateSystemFeatureConstants`

_Type library:_ `SolidEdgePart`  
_Membros:_ 6

| Constante | Valor |
|-----------|-------|
| `seCoordSysXAxis` | 1 |
| `seCoordSysYAxis` | 2 |
| `seCoordSysZAxis` | 3 |
| `seCoordSysXYPlane` | 4 |
| `seCoordSysYZPlane` | 5 |
| `seCoordSysZXPlane` | 6 |

## <a name="coordinatesystemoffsettypeconstants"></a>`CoordinateSystemOffsetTypeConstants`

_Type library:_ `SolidEdgePart`  
_Membros:_ 2

| Constante | Valor |
|-----------|-------|
| `seCoordSysOffsetGlobal` | 1 |
| `seCoordSysOffsetRelative` | 2 |

## <a name="coordinatesystemrotationtypeconstants"></a>`CoordinateSystemRotationTypeConstants`

_Type library:_ `SolidEdgePart`  
_Membros:_ 2

| Constante | Valor |
|-----------|-------|
| `seCoordSysRotateAboutSelf` | 1 |
| `seCoordSysRotateAboutParent` | 2 |

## <a name="coordinatesystemtypeconstants"></a>`CoordinateSystemTypeConstants`

_Type library:_ `SolidEdgePart`  
_Membros:_ 3

| Constante | Valor |
|-----------|-------|
| `seCoordSysGeometryBased` | 1 |
| `seCoordSysNonGeometryBased` | 2 |
| `seCoordSysNonGeometricRelativeTo` | 3 |

## <a name="copysurfaceexternalboundaryconstants"></a>`CopySurfaceExternalBoundaryConstants`

_Type library:_ `SolidEdgePart`  
_Membros:_ 2

| Constante | Valor |
|-----------|-------|
| `igCopySurfaceRemoveExternalBoundaries` | 1 |
| `igCopySurfaceCopyExternalBoundaries` | 2 |

## <a name="copysurfaceinternalboundaryconstants"></a>`CopySurfaceInternalBoundaryConstants`

_Type library:_ `SolidEdgePart`  
_Membros:_ 2

| Constante | Valor |
|-----------|-------|
| `igCopySurfaceRemoveInternalBoundaries` | 1 |
| `igCopySurfaceCopyInternalBoundaries` | 2 |

## <a name="deletefaceconstants"></a>`DeleteFaceConstants`

_Type library:_ `SolidEdgePart`  
_Membros:_ 2

| Constante | Valor |
|-----------|-------|
| `igDeleteFaceApplyHeal` | 1 |
| `igDeleteFaceApplyNoHeal` | 2 |

## <a name="derivedcurvetypeconstants"></a>`DerivedCurveTypeConstants`

_Type library:_ `SolidEdgePart`  
_Membros:_ 2

| Constante | Valor |
|-----------|-------|
| `igDCComposite` | 1 |
| `igDCCurve` | 2 |

## <a name="dimplefeatureconstants"></a>`DimpleFeatureConstants`

_Type library:_ `SolidEdgePart`  
_Membros:_ 12

| Constante | Valor |
|-----------|-------|
| `seDimpleDepthLeft` | 1 |
| `seDimpleDepthRight` | 2 |
| `seDimpleDimensionOffset` | 3 |
| `seDimpleDimensionFull` | 4 |
| `seDimpleProfileLeft` | 5 |
| `seDimpleProfileRight` | 6 |
| `seDimpleNoRoundCorners` | 7 |
| `seDimpleRoundCorners` | 8 |
| `seDimpleNoRoundEdges` | 9 |
| `seDimpleRoundEdges` | 10 |
| `seDimpleMaterialInside` | 11 |
| `seDimpleMaterialOutside` | 12 |

## <a name="displaytypeconstant"></a>`DisplayTypeConstant`

_Type library:_ `SolidEdgeFramework`  
_Membros:_ 3

| Constante | Valor |
|-----------|-------|
| `igNotSpecifiedDisplay` | -1 |
| `igContentsDisplay` | 0 |
| `igIconDisplay` | 1 |

## <a name="dividedpartcutdirectionconstants"></a>`DividedPartCutDirectionConstants`

_Type library:_ `SolidEdgePart`  
_Membros:_ 2

| Constante | Valor |
|-----------|-------|
| `seDividedPartCutNormal` | 0 |
| `seDividedPartCutReverseNormal` | 1 |

## <a name="dividedpartstatusconstants"></a>`DividedPartStatusConstants`

_Type library:_ `SolidEdgePart`  
_Membros:_ 4

| Constante | Valor |
|-----------|-------|
| `seDividedPartStatusNotCreated` | 0 |
| `seDividedStatusUpToDate` | 1 |
| `seDividedStatusOutOfDate` | 2 |
| `seDividedStatusLinkBroken` | 3 |

## <a name="documentaccess"></a>`DocumentAccess`

_Type library:_ `SolidEdgeFramework`  
_Membros:_ 3

| Constante | Valor |
|-----------|-------|
| `igReadWrite` | 0 |
| `igReadOnly` | 1 |
| `igReadExclusive` | 2 |

## <a name="documentdownloadlevel"></a>`DocumentDownloadLevel`

_Type library:_ `SolidEdgeFramework`  
_Membros:_ 3

| Constante | Valor |
|-----------|-------|
| `SEECDownloadAllLevel` | 0 |
| `SEECDownloadFirstLevel` | 1 |
| `SEECDownloadTopLevel` | 2 |

## <a name="documentstatus"></a>`DocumentStatus`

_Type library:_ `SolidEdgeFramework`  
_Membros:_ 7

| Constante | Valor |
|-----------|-------|
| `igStatusAvailable` | 0 |
| `igStatusInWork` | 1 |
| `igStatusInReview` | 2 |
| `igStatusReleased` | 3 |
| `igStatusBaselined` | 4 |
| `igStatusObsolete` | 5 |
| `igStatusUnknown` | 6 |

## <a name="documenttypeconstants"></a>`DocumentTypeConstants`

_Type library:_ `SolidEdgeFramework`  
_Membros:_ 13

| Constante | Valor |
|-----------|-------|
| `igPartDocument` | 1 |
| `igDraftDocument` | 2 |
| `igAssemblyDocument` | 3 |
| `igSheetMetalDocument` | 4 |
| `igUnknownDocument` | 5 |
| `igWeldmentDocument` | 6 |
| `igWeldmentAssemblyDocument` | 7 |
| `igSyncPartDocument` | 8 |
| `igSyncSheetMetalDocument` | 9 |
| `igSyncAssemblyDocument` | 10 |
| `igAssemblyViewerDocument` | 11 |
| `igPartViewerDocument` | 12 |
| `igDraftViewerDocument` | 13 |

## <a name="draftsideconstants"></a>`DraftSideConstants`

_Type library:_ `SolidEdgePart`  
_Membros:_ 3

| Constante | Valor |
|-----------|-------|
| `seDraftNone` | 44 |
| `seDraftInside` | 4 |
| `seDraftOutside` | 5 |

## <a name="drawncutoutfeatureconstants"></a>`DrawnCutoutFeatureConstants`

_Type library:_ `SolidEdgePart`  
_Membros:_ 10

| Constante | Valor |
|-----------|-------|
| `seDrawnCutoutDepthLeft` | 1 |
| `seDrawnCutoutDepthRight` | 2 |
| `seDrawnCutoutMaterialInside` | 3 |
| `seDrawnCutoutMaterialOutside` | 4 |
| `seDrawnCutoutProfileLeft` | 5 |
| `seDrawnCutoutProfileRight` | 6 |
| `seDrawnCutoutRoundEdges` | 7 |
| `seDrawnCutoutNoRoundEdges` | 8 |
| `seDrawnCutoutRoundCorners` | 9 |
| `seDrawnCutoutNoRoundCorners` | 10 |

## <a name="enclosuretypeconstant"></a>`EnclosureTypeConstant`

_Type library:_ `SolidEdgePart`  
_Membros:_ 3

| Constante | Valor |
|-----------|-------|
| `igEnclosureTypeBox` | 0 |
| `igEnclosureTypeInsideCylinder` | 1 |
| `igEnclosureTypeOutsideCylinder` | 2 |

## <a name="extendsurfaceextenttypeconstants"></a>`ExtendSurfaceExtentTypeConstants`

_Type library:_ `SolidEdgePart`  
_Membros:_ 6

| Constante | Valor |
|-----------|-------|
| `igESNatural` | 1 |
| `igESLinear` | 2 |
| `igESLinearTangentContinuous` | 3 |
| `igESLinearCurvatureContinuous` | 4 |
| `igESReflective` | 5 |
| `igESConstantRadiusArc` | 6 |

## <a name="facemoveconstants"></a>`FaceMoveConstants`

_Type library:_ `SolidEdgePart`  
_Membros:_ 13

| Constante | Valor |
|-----------|-------|
| `igFaceMoveNone` | 0 |
| `igFaceMoveAlong2PointVector` | 1 |
| `igFaceMoveAlongFaceNormal` | 2 |
| `igFaceMoveAlongEdge` | 3 |
| `igFaceMoveInPlane` | 4 |
| `igFaceMoveIgnoreBlends` | 5 |
| `igFaceMoveRecreateBlends` | 6 |
| `igFaceMoveAlongVector` | 7 |
| `igFaceMoveReverseVector` | 8 |
| `igFaceMoveOffsetAlongVector` | 9 |
| `igFaceMoveOffsetReverseVector` | 10 |
| `igFaceMoveSynchronousMoveLinear` | 11 |
| `igFaceMoveSynchronousMovePlanar` | 12 |

## <a name="faceoffsetconstants"></a>`FaceOffsetConstants`

_Type library:_ `SolidEdgePart`  
_Membros:_ 3

| Constante | Valor |
|-----------|-------|
| `igFaceOffsetNone` | 0 |
| `igFaceOffsetBySynchronousOffset` | 1 |
| `igFaceOffsetByOffset` | 2 |

## <a name="facerotateconstants"></a>`FaceRotateConstants`

_Type library:_ `SolidEdgePart`  
_Membros:_ 8

| Constante | Valor |
|-----------|-------|
| `igFaceRotateNone` | 0 |
| `igFaceRotateByPoints` | 1 |
| `igFaceRotateByGeometry` | 2 |
| `igFaceRotateAxisStart` | 3 |
| `igFaceRotateAxisEnd` | 4 |
| `igFaceRotateIgnoreBlends` | 5 |
| `igFaceRotateRecreateBlends` | 6 |
| `igFaceRotateBySynchronousRotate` | 7 |

## <a name="familymemberstatusconstants"></a>`FamilyMemberStatusConstants`

_Type library:_ `SolidEdgePart`  
_Membros:_ 6

| Constante | Valor |
|-----------|-------|
| `seStatusUnknown` | 0 |
| `seStatusNotCreated` | 1 |
| `seStatusUpToDate` | 2 |
| `seStatusOutOfDate` | 3 |
| `seStatusLinkBroken` | 4 |
| `seStatusFileReleased` | 7 |

## <a name="featurelooptype"></a>`FeatureLoopType`

_Type library:_ `SolidEdgePart`  
_Membros:_ 3

| Constante | Valor |
|-----------|-------|
| `eIncludeInternalLoop` | 1 |
| `eExcludeInternalLoop` | 2 |
| `eUseOnlyInternalLoop` | 3 |

## <a name="featurepropertyconstants"></a>`FeaturePropertyConstants`

_Type library:_ `SolidEdgePart`  
_Membros:_ 257

| Constante | Valor |
|-----------|-------|
| `igNullConstant` | 0 |
| `igLeft` | 1 |
| `igRight` | 2 |
| `igSymmetric` | 3 |
| `igInside` | 4 |
| `igOutside` | 5 |
| `igBoth` | 6 |
| `igNormalSideDummy` | 7 |
| `igReverseNormalSideDummy` | 8 |
| `igExtend` | 9 |
| `igNoExtend` | 10 |
| `igThkInProfilePlane` | 11 |
| `igThkNormalToProfilePlane` | 12 |
| `igFinite` | 13 |
| `igToNext` | 14 |
| `igToEndOfEdge` | 14 |
| `igFromTo` | 15 |
| `igThroughAll` | 16 |
| `igThreeHundredAndSixty` | 17 |
| `igParallelDummy` | 18 |
| `igAngularDummy` | 19 |
| `igNormal` | 20 |
| `igThroughAxis` | 21 |
| `igSingleEdge` | 22 |
| `igMultipleEdges` | 23 |
| `igEdgesByLoop` | 24 |
| `igEdgesByVertex` | 25 |
| `igAll` | 26 |
| `igConcave` | 27 |
| `igConvex` | 28 |
| `igStart` | 29 |
| `igEnd` | 30 |
| `igLinear` | 31 |
| `igRadial` | 32 |
| `igRegularHole` | 33 |
| `igCounterboreHole` | 34 |
| `igCountersinkHole` | 35 |
| `igCounterdrillHole` | 36 |
| `igTappedHole` | 37 |
| `igTaperedHole` | 38 |
| `igConstRadiusRound` | 39 |
| `igVarRadiusRound` | 40 |
| `igChamfer45degSetback` | 41 |
| `igChamferAngleSetback` | 42 |
| `igChamfer2Setbacks` | 43 |
| `igNone` | 44 |
| `igTaperByAngle` | 45 |
| `igTaperByRatio` | 46 |
| `igClosed` | 47 |
| `igProfileBasedCrossSection` | 48 |
| `igEdgeBasedCrossSection` | 49 |
| `igTangent` | 50 |
| `igRectangularBendRelief` | 51 |
| `igFilletBendRelief` | 52 |
| `igRipBendRelief` | 53 |
| `igBendOnlyCornerRelief` | 54 |
| `igBendAndFaceCornerRelief` | 55 |
| `igRipCornerRelief` | 56 |
| `igNFType` | 57 |
| `igEquationType` | 58 |
| `igPatternMirror` | 59 |
| `igPatternRectangular` | 60 |
| `igPatternCircular` | 61 |
| `igPatternUserDefined` | 62 |
| `igFromReferenceEnd` | 64 |
| `igFromNonReferenceEnd` | 65 |
| `igRndRollAcrossTangentEdgesOn` | 66 |
| `igRndRollAcrossTangentEdgesOff` | 67 |
| `igRndCapAcrossSharpEdges` | 68 |
| `igRndRollAcrossSharpEdges` | 69 |
| `igRndRollAlongBlendEdgesOn` | 70 |
| `igRndRollAlongBlendEdgesOff` | 71 |
| `igToKeyPoint` | 72 |
| `igFlatten` | 73 |
| `igAsConstruction` | 74 |
| `igOffset` | 75 |
| `igMitreParallelToThickness` | 76 |
| `igMitreNormalToThickness` | 77 |
| `igMitreByDist` | 78 |
| `igMitreByAngle` | 79 |
| `igMitreRegularCut` | 80 |
| `igMitreManufacturingCut` | 81 |
| `igProjectOptionProject` | 82 |
| `igProjectOptionWrap` | 83 |
| `igLip` | 84 |
| `igGroove` | 85 |
| `igPartingFromPlane` | 86 |
| `igPartingFromSurface` | 87 |
| `igPartingFromEdge` | 88 |
| `igPartingFromCurve` | 89 |
| `igSplitDraft` | 90 |
| `igSplitAngle1Right` | 91 |
| `igSplitAngle1Left` | 92 |
| `igLouverFormedEndType` | 93 |
| `igLouverLancedEndType` | 94 |
| `igLouverRound` | 95 |
| `igLouverRoundNone` | 96 |
| `igInsideDimension` | 97 |
| `igOutsideDimension` | 98 |
| `igFull` | 99 |
| `igBend` | 100 |
| `igAddRound` | 101 |
| `igNoRound` | 102 |
| `igCloseFaces` | 103 |
| `igOverlapFaces` | 104 |
| `igTreatmentOff` | 105 |
| `igTreatmentIntersect` | 106 |
| `igTreatmentCircleCutout` | 107 |
| `igStepDraft` | 108 |
| `igShowBoundaries` | 109 |
| `igRemoveBoundaries` | 110 |
| `igCornerRound` | 111 |
| `igNoCornerRound` | 112 |
| `igNatural` | 113 |
| `igPeriodic` | 114 |
| `igRoundAllVertexSetback` | 115 |
| `igRoundSingleVertexSetback` | 116 |
| `igRoundVertexEdgeSetback` | 117 |
| `igRoundSetbackIsAbsolute` | 118 |
| `igRoundSetbackIsRelative` | 119 |
| `igCircular` | 120 |
| `igUShaped` | 121 |
| `igVShaped` | 122 |
| `igPunchedEnd` | 123 |
| `igLancedEnd` | 124 |
| `igFormedEnd` | 125 |
| `igSweepAlignParallel` | 126 |
| `igSweepAlignNormal` | 127 |
| `igRoundStartVertexEdgeSetback` | 128 |
| `igRoundEndVertexEdgeSetback` | 129 |
| `igSubtract` | 130 |
| `igUnite` | 131 |
| `igIntersect` | 132 |
| `igContinuous` | 133 |
| `igFlangeFullEdge` | 134 |
| `igFlangeCenterOfEdge` | 135 |
| `igFlangeStartOnEndEdge` | 136 |
| `igFlangeEndOnEndEdge` | 137 |
| `igFlangeStartFromEndEdge` | 138 |
| `igFlangeEndFromEndEdge` | 139 |
| `igFlangeFromBothEndsOfEdge` | 140 |
| `igFlangeOffset` | 141 |
| `igChainedCornerRelief` | 142 |
| `igTangentInterior` | 143 |
| `igParallelToPlane` | 144 |
| `igVBottomDimToFlat` | 145 |
| `igVBottomDimToV` | 146 |
| `igTaperDimAtTop` | 147 |
| `igTaperDimAtBottom` | 148 |
| `igCounterboreProfileIsAtTop` | 149 |
| `igCounterboreProfileIsAtBottom` | 150 |
| `igTaperByRLRatio` | 151 |
| `igRndMiterAtCorner` | 152 |
| `igRndRollAroundCorner` | 153 |
| `igRndPreserveTopologyOn` | 154 |
| `igRndPreserveTopologyOff` | 155 |
| `igStepDraftPerpendicular` | 156 |
| `igExtendBendRelief` | 157 |
| `igEqualOffset` | 158 |
| `igUnequalOffset` | 159 |
| `igThickness` | 160 |
| `igFacesTouchingCurvesOnly` | 161 |
| `igCurveSetSeperator` | 162 |
| `igSideInfoSetSeperator` | 163 |
| `igRegularThread` | 164 |
| `igStraightPipeThread` | 165 |
| `igTaperedPipeThread` | 166 |
| `igRemoveInternalBoundaries` | 167 |
| `igRemoveExternalBoundaries` | 168 |
| `igDeleteFaceHeal` | 169 |
| `igEndCaps` | 170 |
| `igCurvatureContinuous` | 171 |
| `igNonSymmetric` | 172 |
| `igTreatmentDraft` | 173 |
| `igTreatmentCrown` | 174 |
| `igCloseCornerNone` | 175 |
| `igCloseCornerOpen` | 176 |
| `igCloseCornerClosed` | 177 |
| `igCloseCornerCircleCutout` | 178 |
| `igPatternAlongCurve` | 179 |
| `igPatternMountingBoss` | 180 |
| `igSMClearanceCutout` | 181 |
| `igSMMidPlaneCutout` | 182 |
| `igLinearTangentExtension` | 183 |
| `igLinearCurvatureContinuousExtension` | 184 |
| `igReflective` | 185 |
| `igMove` | 186 |
| `igCopy` | 187 |
| `igDelSMFaceNone` | 188 |
| `igDelZeroBendRadius` | 189 |
| `igDelSystemRelief` | 190 |
| `igDelSMFaceBoth` | 191 |
| `igReverseNormal` | 192 |
| `igRecreateBlends` | 193 |
| `igIgnoreBlends` | 194 |
| `igSweepAlignParametrically` | 195 |
| `igSweepAlignArcLength` | 196 |
| `igSweepMergeNone` | 197 |
| `igSweepMergeAlongPath` | 198 |
| `igSweepMergeAll` | 199 |
| `igSweepC1Continuity` | 200 |
| `igSweepC2Continuity` | 201 |
| `igWeldGrooveCapProject` | 202 |
| `igWeldGrooveCapSurface` | 203 |
| `igWeldGrooveCapSameAsTop` | 204 |
| `igSMFaceCutout` | 205 |
| `igFlangeMatchFace` | 206 |
| `igHemTypeClosed` | 207 |
| `igHemTypeOpen` | 208 |
| `igHemTypeSFlange` | 209 |
| `igHemTypeCurl` | 210 |
| `igHemTypeOpenLoop` | 211 |
| `igHemTypeClosedLoop` | 212 |
| `igHemTypeCenteredLoop` | 213 |
| `igSweepTwistNone` | 214 |
| `igSweepTwistTurns` | 215 |
| `igSweepTwistTurnsPerUnitLength` | 216 |
| `igSweepTwistStartAndEnd` | 217 |
| `igSweepScaleNone` | 218 |
| `igSweepScaleStartAndEnd` | 219 |
| `igTreatmentUCutout` | 220 |
| `igTreatmentVCutout` | 221 |
| `igTreatmentRectangularCutout` | 222 |
| `igTreatmentFormedFeatureDisplayAsModeled` | 223 |
| `igTreatmentFormedFeatureDisplayAsSketch` | 224 |
| `igTreatmentFormedFeatureDisplayAsCenterMark` | 225 |
| `igTreatmentFormedFeatureDisplayAsSketchAndCenterMark` | 226 |
| `igPatternFillRegion` | 227 |
| `igFlangeFromDefinedStartPoint` | 228 |
| `igTreatmentMiterRelief` | 232 |
| `igDevelopableSurface` | 233 |
| `igNeutralFactorFromExcel` | 234 |
| `igRegularSlot` | 235 |
| `igRecessedCounterboreSlot` | 236 |
| `igToggleToDesign` | 237 |
| `igToggleToConstruction` | 238 |
| `igSplit` | 239 |
| `igTaperedEnd` | 240 |
| `igRaisedCounterboreSlot` | 241 |
| `igConstantRadiusArc` | 242 |
| `igTaperedToPlane` | 243 |
| `igAlongAnAxis` | 244 |
| `igCurvatureContinuousInterior` | 245 |
| `igPatternByTable` | 246 |
| `igPatternDuplicate` | 247 |
| `igDinDimension` | 248 |
| `igSlotGroup` | 249 |
| `igMaterialInsideOML` | 250 |
| `igAutoReliefSpherical` | 251 |
| `igAutoReliefLinear` | 252 |
| `igAutoReliefTrimEndPlates` | 253 |
| `igAutoReliefTrimNone` | 254 |
| `igDivideBendByCount` | 255 |
| `igDivideBendByMaximumChordHeight` | 256 |
| `igDivideBendByMaximumSegmentLength` | 257 |
| `igDivideBendByMaximumSegmentAngle` | 258 |
| `igClampTypeProfile` | 259 |

## <a name="featurestatusconstants"></a>`FeatureStatusConstants`

_Type library:_ `SolidEdgePart`  
_Membros:_ 5

| Constante | Valor |
|-----------|-------|
| `igFeatureOK` | 1216476310 |
| `igFeatureFailed` | 1216476311 |
| `igFeatureWarned` | 1216476312 |
| `igFeatureSuppressed` | 1216476313 |
| `igFeatureRolledBack` | 1216476314 |

## <a name="featuretypeconstants"></a>`FeatureTypeConstants`

_Type library:_ `SolidEdgePart`  
_Membros:_ 128

| Constante | Valor |
|-----------|-------|
| `igExtrudedProtrusionFeatureObject` | 462094706 |
| `igRevolvedProtrusionFeatureObject` | 462094710 |
| `igExtrudedCutoutFeatureObject` | 462094714 |
| `igRevolvedCutoutFeatureObject` | 462094718 |
| `igHoleFeatureObject` | 462094722 |
| `igRibFeatureObject` | 462094730 |
| `igThinwallFeatureObject` | 462094734 |
| `igRoundFeatureObject` | 462094738 |
| `igChamferFeatureObject` | 462094742 |
| `igDraftFeatureObject` | 462094746 |
| `igPatternFeatureObject` | -416228998 |
| `igUserDefinedPatternFeatureObject` | -1468087919 |
| `igBodyFeatureObject` | -1489778563 |
| `igModelCopyFeatureObject` | -1132312974 |
| `igMirrorCopyFeatureObject` | 66247736 |
| `igTabFeatureObject` | -1470838992 |
| `igLoftedProtrusionFeatureObject` | 2057842144 |
| `igLoftedCutoutFeatureObject` | 2057842149 |
| `igSweptProtrusionFeatureObject` | -2101194894 |
| `igSweptCutoutFeatureObject` | -398746894 |
| `igFlangeFeatureObject` | -1752010637 |
| `igBreakCornerFeatureObject` | -615522252 |
| `igContourFlangeFeatureObject` | 281089316 |
| `igHelixProtrusionFeatureObject` | -1204891230 |
| `igHelixCutoutFeatureObject` | 1197717883 |
| `igUnbendFeatureObject` | 1323346977 |
| `igRebendFeatureObject` | -504033272 |
| `igCopiedPartFeatureObject` | -1871757644 |
| `igReplaceFaceFeatureObject` | 630099633 |
| `igWebNetworkFeatureObject` | 1718424353 |
| `igLoftedSurfaceFeatureObject` | 421475437 |
| `igSweptSurfaceFeatureObject` | 421475439 |
| `igDimpleFeatureObject` | 721904086 |
| `igCloseCornerFeatureObject` | 138029688 |
| `igLipFeatureObject` | -483645223 |
| `igJogFeatureObject` | 395285420 |
| `igLoftedFlangeFeatureObject` | 438063868 |
| `igExtrudedSurfaceObject` | 499918320 |
| `igOffsetSurfaceObject` | 1587872528 |
| `igRevolvedSurfaceObject` | -1934200528 |
| `igCopyConstructionObject` | 96955616 |
| `igIntersectionCurveObject` | 1501757264 |
| `igIntersectionPointObject` | -1444717528 |
| `igProjectCurveObject` | -1444717526 |
| `igLouverFeatureObject` | 316506019 |
| `igDrawnCutoutFeatureObject` | 611936790 |
| `igBeadFeatureObject` | -2099521208 |
| `igCoordinateSystemFeatureObject` | 1189339109 |
| `igBendFeatureObject` | -1906466896 |
| `igBooleanFeatureObject` | -848873917 |
| `igNormalCutoutFeatureObject` | -292547215 |
| `igMirrorPartFeatureObject` | 1908287958 |
| `igTubeFeatureObject` | -1199320480 |
| `igWireFeatureObject` | -818300041 |
| `igDeleteFaceFeatureObject` | 2113150981 |
| `igDeleteRegionFeatureObject` | 2113150980 |
| `igDeleteHoleFeatureObject` | -1078402089 |
| `igDeleteBlendFeatureObject` | 759182187 |
| `igStitchSurfaceObject` | -396962225 |
| `igCopySurfaceObject` | 1752082343 |
| `igNormalToFaceProtrusionObject` | 785064761 |
| `igNormalToFaceCutoutObject` | 785064758 |
| `igWeldBeadByExtrudedProtrusionFeatureObject` | 1036932736 |
| `igFilletWeldFeatureObject` | -461605375 |
| `igWeldBeadByRevolvedProtrusionFeatureObject` | -1105952133 |
| `igWeldBeadBySweptProtrusionFeatureObject` | 313074017 |
| `igWeldChamferFeatureObject` | 741566449 |
| `igWeldExtrudedCutoutFeatureObject` | -1472076592 |
| `igWeldHoleFeatureObject` | -573089291 |
| `igWeldMirrorFeatureObject` | -1625749114 |
| `igWeldPatternFeatureObject` | -1994967864 |
| `igWeldRevolvedCutoutFeatureObject` | -1033222194 |
| `igWeldRoundFeatureObject` | -1901787677 |
| `igLabelWeldFeatureObject` | -1072882524 |
| `igAssemblyWeldmentObject` | 2137685236 |
| `igTrimSurfaceObject` | 1917772600 |
| `igExtendSurfaceObject` | -1575914081 |
| `igDerivedCurveObject` | 97588292 |
| `igSplitCurveObject` | 276679016 |
| `igSurfaceByBoundaryObject` | -1483539936 |
| `igMirrorCopyGeometryObject` | 980322967 |
| `igBlueSurfFeatureObject` | -109105343 |
| `igThickenFeatureObject` | 990795800 |
| `igThinRegionFeatureObject` | 438630050 |
| `igPartingSplitFeatureObject` | -1223346452 |
| `igMidSurfaceObject` | -591258000 |
| `igPatternCopyGeometryObject` | -1106524128 |
| `igPatternPartFeatureObject` | 176240055 |
| `igVentFeatureObject` | -85880079 |
| `igPartingSurfaceFeatureObject` | 424701353 |
| `igSplitFaceObject` | -1385224025 |
| `igResizeHoleObject` | -1887900773 |
| `igResizeRoundObject` | 963690016 |
| `igResizeBendObject` | 1106163506 |
| `igFaceMoveObject` | 1100034230 |
| `igFaceRotateObject` | -1621393684 |
| `igFaceOffsetObject` | 1180468550 |
| `igWrapSketch` | 839887038 |
| `igInterpartConstructionObject` | -326994721 |
| `igGussetFeatureObject` | 758717329 |
| `igMatchFlangeFaceFeatureObject` | 1647712262 |
| `igLiveSectionObject` | -1431348508 |
| `igEtchFeatureObject` | 1786053696 |
| `igUnitedBodyObject` | -1974090952 |
| `igRecoveredBodyObject` | -1553526524 |
| `igSlotFeatureObject` | 1336556513 |
| `igDuplicateFeatureObject` | 1854650269 |
| `igIntersectSurfaceObject` | 841561903 |
| `igEmbossFeatureObject` | -2101998503 |
| `igRedefineFaceObject` | 2009702983 |
| `igBlankFeatureObject` | -1364719728 |
| `igBlankSurfaceFeatureObject` | 1484085415 |
| `igFeatureGroupObject` | -1042199833 |
| `igSlotGroupObject` | 339115113 |
| `seAsmExtrudedProtrusion` | 1541369857 |
| `seAsmRevolvedProtrusion` | 2079590812 |
| `seAsmSweptProtrusion` | 144813157 |
| `seAsmExtrudedCutout` | -1746885455 |
| `seAsmHole` | 1532784456 |
| `seAsmThread` | 483231468 |
| `seAsmPattern` | -869608965 |
| `seAsmMirror` | 45018492 |
| `seAsmRevolvedCutout` | 339062508 |
| `seAsmFilletWeld` | 731482635 |
| `seAsmGrooveWeld` | 1498218798 |
| `seAsmLabelWeld` | -489073541 |
| `seAsmStitchWeld` | -266365000 |
| `igReliefPatchFeature` | -313264769 |

## <a name="filetranslationmode"></a>`FileTranslationMode`

_Type library:_ `SolidEdgeFramework`  
_Membros:_ 2

| Constante | Valor |
|-----------|-------|
| `seImport` | 1493142125 |
| `seExport` | -1720541218 |

## <a name="fillholetype"></a>`FillHoleType`

_Type library:_ `SolidEdgePart`  
_Membros:_ 4

| Constante | Valor |
|-----------|-------|
| `seFillHoleTypeLinear` | 0 |
| `seFillHoleTypeRefined` | 1 |
| `seFillHoleTypeTangent` | 2 |
| `seFillHoleTypeCurvature` | 3 |

## <a name="fillpatternmethodconstants"></a>`FillPatternMethodConstants`

_Type library:_ `SolidEdgePart`  
_Membros:_ 6

| Constante | Valor |
|-----------|-------|
| `seRectangularFillMethod` | 1 |
| `seStaggerPolarFillMethod` | 2 |
| `seStaggerLinearOffsetFillMethod` | 3 |
| `seRadialTargetSpacingFillMethod` | 4 |
| `seRadialInstanceCountFillMethod` | 5 |
| `seStaggerComplexLinearOffsetFillMethod` | 6 |

## <a name="filletweldsetbackconstants"></a>`FilletWeldSetbackConstants`

_Type library:_ `SolidEdgePart`  
_Membros:_ 3

| Constante | Valor |
|-----------|-------|
| `seFilletWeldEqualSetback` | 0 |
| `seFilletWeldUnequalSetback` | 1 |
| `seFilletWeldThickness` | 2 |

## <a name="flangefeatureconstants"></a>`FlangeFeatureConstants`

_Type library:_ `SolidEdgePart`  
_Membros:_ 10

| Constante | Valor |
|-----------|-------|
| `seFlangeBendOnlyCornerRelief` | 1 |
| `seFlangeBendAndFaceCornerRelief` | 2 |
| `seFlangeBendAndFaceChainRelief` | 3 |
| `seFlangeMaterialInside` | 4 |
| `seFlangeMaterialOutside` | 5 |
| `seFlangeBendOutside` | 6 |
| `seFlangeBendReliefSquare` | 7 |
| `seFlangeBendReliefRound` | 8 |
| `seFlangeBendReliefNone` | 9 |
| `seFlangeCornerReliefNone` | 10 |

## <a name="generatemasterimportlisterror"></a>`GenerateMasterImportListError`

_Type library:_ `SolidEdgeFramework`  
_Membros:_ 1

| Constante | Valor |
|-----------|-------|
| `NoDocsFound` | 1 |

## <a name="generatesourceimportlisterror"></a>`GenerateSourceImportListError`

_Type library:_ `SolidEdgeFramework`  
_Membros:_ 1

| Constante | Valor |
|-----------|-------|
| `GenerateSourceImportListError_NoDocsFound` | 1 |

## <a name="gussetconstants"></a>`GussetConstants`

_Type library:_ `SolidEdgePart`  
_Membros:_ 9

| Constante | Valor |
|-----------|-------|
| `igGussetNone` | 0 |
| `igAutomaticProfile` | 1 |
| `igUserDrawnProfile` | 2 |
| `igRoundShape` | 3 |
| `igSquareShape` | 4 |
| `igIncludeRounding` | 5 |
| `igPatternFit` | 6 |
| `igPatternFixed` | 7 |
| `igPatternFill` | 8 |

## <a name="gussetplatealignmentconstants"></a>`GussetPlateAlignmentConstants`

_Type library:_ `SolidEdgePart`  
_Membros:_ 3

| Constante | Valor |
|-----------|-------|
| `GussetPlateAlignType_Center` | 0 |
| `GussetPlateAlignType_Right` | 1 |
| `GussetPlateAlignType_Left` | 2 |

## <a name="gussetplateerrorcode"></a>`GussetPlateErrorCode`

_Type library:_ `SolidEdgePart`  
_Membros:_ 6

| Constante | Valor |
|-----------|-------|
| `GussetPlateErrorCodeUnknownError` | -1 |
| `GussetPlateErrorCodeNoError` | 0 |
| `GussetPlateErrorCodeMissingParameter` | 1 |
| `GussetPlateErrorCodeInvalidParameter` | 2 |
| `GussetPlateErrorCodeNoReferencePlane` | 3 |
| `GussetPlateErrorCodeEditFailed` | 4 |

## <a name="gussetplatethicknessdirconstants"></a>`GussetPlateThicknessDirConstants`

_Type library:_ `SolidEdgePart`  
_Membros:_ 3

| Constante | Valor |
|-----------|-------|
| `GussetPlateDir_Center` | 0 |
| `GussetPlateDir_Right` | 1 |
| `GussetPlateDir_Left` | 2 |

## <a name="hatchelementtype"></a>`HatchElementType`

_Type library:_ `SolidEdgeFramework`  
_Membros:_ 3

| Constante | Valor |
|-----------|-------|
| `igHatchElementTypeUnknown` | 0 |
| `igHatchElementTypeLinear` | 1 |
| `igHatchElementTypeRadial` | 2 |

## <a name="helicalcurvemethodtype"></a>`HelicalCurveMethodType`

_Type library:_ `SolidEdgePart`  
_Membros:_ 3

| Constante | Valor |
|-----------|-------|
| `igHelicalCurveMethodPitchAndHeight` | 0 |
| `igHelicalCurveMethodPitchAndTurns` | 1 |
| `igHelicalCurveMethodHeightAndTurns` | 2 |

## <a name="helicalcurvetaperbytype"></a>`HelicalCurveTaperByType`

_Type library:_ `SolidEdgePart`  
_Membros:_ 3

| Constante | Valor |
|-----------|-------|
| `igHelicalCurveTaperNone` | 0 |
| `igHelicalCurveTaperByAngle` | 1 |
| `igHelicalCurveTaperByDiameter` | 2 |

## <a name="hemfeatureconstants"></a>`HemFeatureConstants`

_Type library:_ `SolidEdgePart`  
_Membros:_ 15

| Constante | Valor |
|-----------|-------|
| `seHemTypeClosed` | 1 |
| `seHemTypeOpen` | 2 |
| `seHemTypeSFlange` | 3 |
| `seHemTypeCurl` | 4 |
| `seHemTypeOpenLoop` | 5 |
| `seHemTypeClosedLoop` | 6 |
| `seHemTypeCenteredLoop` | 7 |
| `seHemSideMaterialInside` | 8 |
| `seHemSideMaterialOutside` | 9 |
| `seHemBendOutside` | 10 |
| `seHemNoBendRelief` | 11 |
| `seHemRectBendRelief` | 12 |
| `seHemRoundBendRelief` | 13 |
| `seHemNoMiter` | 14 |
| `seHemMiterCorner` | 15 |

## <a name="holedataunitsconstants"></a>`HoleDataUnitsConstants`

_Type library:_ `SolidEdgePart`  
_Membros:_ 2

| Constante | Valor |
|-----------|-------|
| `igHoleDataUnitsInches` | 0 |
| `igHoleDataUnitsMillimeters` | 1 |

## <a name="holetolerancetypeconstants"></a>`HoleToleranceTypeConstants`

_Type library:_ `SolidEdgePart`  
_Membros:_ 2

| Constante | Valor |
|-----------|-------|
| `seStandardFit_Tolerance` | 0 |
| `seUnit_Tolerance` | 1 |

## <a name="holetypetodeleteconstants"></a>`HoleTypeToDeleteConstants`

_Type library:_ `SolidEdgePart`  
_Membros:_ 3

| Constante | Valor |
|-----------|-------|
| `seHoleTypeToDeleteFeaturesOnly` | 0 |
| `seHoleTypeToDeleteCylindersAndConesOnly` | 1 |
| `seHoleTypeToDeleteAll` | 2 |

## <a name="insightspuserrights"></a>`InsightSPUserRights`

_Type library:_ `SolidEdgeFramework`  
_Membros:_ 23

| Constante | Valor |
|-----------|-------|
| `seAddAndCustomizePages` | 262144 |
| `seAddDelPrivateWebParts` | 268435456 |
| `seAddListItems` | 2 |
| `seApplyStyleSheets` | 1048576 |
| `seApplyThemeAndBorder` | 524288 |
| `seBrowseDirectories` | 67108864 |
| `seBrowseUserInfo` | 134217728 |
| `seCancelCheckout` | 256 |
| `seCreatePersonalGroups` | 16777216 |
| `seCreateSSCSite` | 4194304 |
| `seDeleteListItems` | 8 |
| `seEditListItems` | 4 |
| `seManageListPermissions` | 1024 |
| `seManageLists` | 2048 |
| `seManagePersonalViews` | 512 |
| `seManageRoles` | 33554432 |
| `seManageSubwebs` | 8388608 |
| `seManageWeb` | 1073741824 |
| `seOpenWeb` | 65536 |
| `seUpdatePersonalWebParts` | 536870912 |
| `seViewListItems` | 1 |
| `seViewPages` | 131072 |
| `seViewUsageData` | 2097152 |

## <a name="interdocumentupdatemode"></a>`InterDocumentUpdateMode`

_Type library:_ `SolidEdgeFramework`  
_Membros:_ 2

| Constante | Valor |
|-----------|-------|
| `seActiveLevel` | 0 |
| `seAllOpenDocuments` | 1 |

## <a name="isoclinedirectionconstants"></a>`IsoclineDirectionConstants`

_Type library:_ `SolidEdgePart`  
_Membros:_ 2

| Constante | Valor |
|-----------|-------|
| `igIsoclineleft` | 1 |
| `igIsoclineRight` | 2 |

## <a name="jogfeatureconstants"></a>`JogFeatureConstants`

_Type library:_ `SolidEdgePart`  
_Membros:_ 19

| Constante | Valor |
|-----------|-------|
| `seJogBendNFT` | 1 |
| `seJogBendEqn` | 2 |
| `seJogBRRectangular` | 3 |
| `seJogBRFillet` | 4 |
| `seJogBendOnlyCR` | 5 |
| `seJogBendAndFaceCR` | 6 |
| `seJogDimensionOffset` | 7 |
| `seJogDimensionFull` | 8 |
| `seJogExtendMoldlines` | 9 |
| `seJogNoExtendMoldines` | 10 |
| `seJogMoveLeft` | 11 |
| `seJogMoveRight` | 12 |
| `seJogMaterialInside` | 13 |
| `seJogMaterialOutside` | 14 |
| `seJogMaterialBendOutside` | 15 |
| `seJogNormal` | 16 |
| `seJogReverseNormal` | 17 |
| `seJogExtentFinite` | 18 |
| `seJogExtentFiniteByKeypoint` | 19 |

## <a name="keypointextentconstants"></a>`KeyPointExtentConstants`

_Type library:_ `SolidEdgePart`  
_Membros:_ 4

| Constante | Valor |
|-----------|-------|
| `igTangentNormal` | 1 |
| `igReverseTangentNormal` | 2 |
| `igInteriorTangentNormal` | 3 |
| `igInteriorReverseTangentNormal` | 4 |

## <a name="keypointtype"></a>`KeyPointType`

_Type library:_ `SolidEdgeFramework`  
_Membros:_ 13

| Constante | Valor |
|-----------|-------|
| `igKeyPointStart` | 1 |
| `igKeyPointEnd` | 2 |
| `igKeyPointCenter` | 4 |
| `igKeyPointMajorAxis` | 8 |
| `igKeyPointMinorAxis` | 16 |
| `igKeyPointMiddle` | 32 |
| `igKeyPointPointOnly` | 64 |
| `igKeyPointHorizontalSilhouette` | 128 |
| `igKeyPointVerticalSilhouette` | 256 |
| `igKeyPointInteriorNode` | 512 |
| `igKeyPointInteriorPole` | 1024 |
| `igKeyPointNonDefining` | 16384 |
| `igKeyPointCallback` | 32768 |

## <a name="keypointendconditionconstants"></a>`KeypointEndConditionConstants`

_Type library:_ `SolidEdgePart`  
_Membros:_ 5

| Constante | Valor |
|-----------|-------|
| `seKeypointEndConditionNatural` | 1 |
| `seKeypointEndConditionPeriodic` | 2 |
| `seKeypointEndConditionTangent` | 3 |
| `seKeypointEndConditionNormalToFace` | 4 |
| `seKeypointEndConditionCurvatureContinuous` | 5 |

## <a name="linksupdateoption"></a>`LinksUpdateOption`

_Type library:_ `SolidEdgeFramework`  
_Membros:_ 3

| Constante | Valor |
|-----------|-------|
| `igNoLinksUpdate` | 0 |
| `igLinksUpdateWithDefpath` | 1 |
| `igLinksUpdateWithAltPath` | 2 |

## <a name="liverulesconstants"></a>`LiveRulesConstants`

_Type library:_ `SolidEdgePart`  
_Membros:_ 18

| Constante | Valor |
|-----------|-------|
| `igConcentricLiveRule` | 1 |
| `igCoplanarLiveRule` | 2 |
| `igTangentEdgeLiveRule` | 3 |
| `igTangentTouchingLiveRule` | 4 |
| `igParallelLiveRule` | 5 |
| `igPerpendicularLiveRule` | 6 |
| `igSymmetricLiveRule` | 7 |
| `igSymmetricXYLiveRule` | 8 |
| `igSymmetricYZLiveRule` | 9 |
| `igSymmetricZXLiveRule` | 10 |
| `igMaintainRadiusLiveRule` | 11 |
| `igOrthoLockingLiveRule` | 12 |
| `igCoplanarAxesLiveRule` | 13 |
| `igCoplanarAxesAboutXLiveRule` | 14 |
| `igCoplanarAxesAboutYLiveRule` | 15 |
| `igCoplanarAxesAboutZLiveRule` | 16 |
| `igThicknessChainLiveRule` | 17 |
| `igOffsetLiveRule` | 18 |

## <a name="loftedflangefeatureautoreliefconstants"></a>`LoftedFlangeFeatureAutoReliefConstants`

_Type library:_ `SolidEdgePart`  
_Membros:_ 2

| Constante | Valor |
|-----------|-------|
| `igLoftedFlangeAutoReliefSpherical` | 251 |
| `igLoftedFlangeAutoReliefLinear` | 252 |

## <a name="loftedflangefeatureautorelieftrimconstants"></a>`LoftedFlangeFeatureAutoReliefTrimConstants`

_Type library:_ `SolidEdgePart`  
_Membros:_ 2

| Constante | Valor |
|-----------|-------|
| `igLoftedFlangeAutoReliefTrimEndPlates` | 253 |
| `igLoftedFlangeAutoReliefTrimNone` | 254 |

## <a name="loftedflangefeaturebendingmethodconstants"></a>`LoftedFlangeFeatureBendingMethodConstants`

_Type library:_ `SolidEdgePart`  
_Membros:_ 4

| Constante | Valor |
|-----------|-------|
| `igLoftedFlangeFeatureBendingMethodNone` | 1 |
| `igLoftedFlangeFeatureBendingMethodGraphicBends` | 2 |
| `igLoftedFlangeFeatureBendingMethodRealBends` | 3 |
| `igLoftedFlangeFeatureBendingMethodFormedBends` | 4 |

## <a name="loftedflangefeaturedividebendconstants"></a>`LoftedFlangeFeatureDivideBendConstants`

_Type library:_ `SolidEdgePart`  
_Membros:_ 4

| Constante | Valor |
|-----------|-------|
| `igLoftedFlangeDivideBendByCount` | 255 |
| `igLoftedFlangeDivideBendByMaximumChordHeight` | 256 |
| `igLoftedFlangeDivideBendByMaximumSegmentLength` | 257 |
| `igLoftedFlangeDivideBendByMaximumSegmentAngle` | 258 |

## <a name="louverfeatureconstants"></a>`LouverFeatureConstants`

_Type library:_ `SolidEdgePart`  
_Membros:_ 10

| Constante | Valor |
|-----------|-------|
| `seLouverDepthDirectionLeft` | 1 |
| `seLouverDepthDirectionRight` | 2 |
| `seLouverDimensionOffset` | 3 |
| `seLouverDimensionFull` | 4 |
| `seLouverFormedEnd` | 5 |
| `seLouverLancedEnd` | 6 |
| `seLouverHeightNormal` | 7 |
| `seLouverHeightReverseNormal` | 8 |
| `seLouverRound` | 9 |
| `seLouverNoRound` | 10 |

## <a name="mattablepropindexconstants"></a>`MatTablePropIndexConstants`

_Type library:_ `SolidEdgeFramework`  
_Membros:_ 13

| Constante | Valor |
|-----------|-------|
| `seMaterialName` | 3 |
| `seFaceStyle` | 20 |
| `seFillStyle` | 21 |
| `seVSPlusStyle` | 22 |
| `seDensity` | 23 |
| `seCoefOfThermalExpansion` | 24 |
| `seThermalConductivity` | 25 |
| `seSpecificHeat` | 26 |
| `seModulusElasticity` | 27 |
| `sePoissonRatio` | 28 |
| `seYieldStress` | 29 |
| `seUltimateStress` | 30 |
| `seElongation` | 31 |

## <a name="measuredistancetypeconstants"></a>`MeasureDistanceTypeConstants`

_Type library:_ `SolidEdgePart`  
_Membros:_ 3

| Constante | Valor |
|-----------|-------|
| `MeasureDistanceTypeConstants_MinimumDistance` | 1 |
| `MeasureDistanceTypeConstants_MaximumDistance` | 2 |
| `MeasureDistanceTypeConstants_SmartDistance` | 3 |

## <a name="mirroroptionconstants"></a>`MirrorOptionConstants`

_Type library:_ `SolidEdgePart`  
_Membros:_ 3

| Constante | Valor |
|-----------|-------|
| `MirrorOptionNormal` | 1 |
| `MirrorOptionDetach` | 2 |
| `MirrorOptionPersist` | 4 |

## <a name="modelingmodeconstants"></a>`ModelingModeConstants`

_Type library:_ `SolidEdgePart`  
_Membros:_ 2

| Constante | Valor |
|-----------|-------|
| `seModelingModeSynchronous` | 1 |
| `seModelingModeOrdered` | 2 |

## <a name="moveconnectedfacetypes"></a>`MoveConnectedFaceTypes`

_Type library:_ `SolidEdgePart`  
_Membros:_ 3

| Constante | Valor |
|-----------|-------|
| `seMoveConnectedFaceTypeExtendTrim` | 0 |
| `seMoveConnectedFaceTypeTip` | 1 |
| `seMoveConnectedFaceTypeLift` | 2 |

## <a name="moveprecedenceconstants"></a>`MovePrecedenceConstants`

_Type library:_ `SolidEdgePart`  
_Membros:_ 2

| Constante | Valor |
|-----------|-------|
| `igSelectSetMovePrecedence` | 1 |
| `igModelMovePredecence` | 2 |

## <a name="multibodypublishstatusconstants"></a>`MultiBodyPublishStatusConstants`

_Type library:_ `SolidEdgePart`  
_Membros:_ 7

| Constante | Valor |
|-----------|-------|
| `seMBPStatusUnknown` | 0 |
| `seMBPStatusNotCreated` | 1 |
| `seMBPStatusUpToDate` | 2 |
| `seMBPStatusOutOfDate` | 3 |
| `seMBPStatusFailed` | 4 |
| `seMBPStatusDeleted` | 5 |
| `seMBPStatusLinkBroken` | 6 |

## <a name="notifyoption"></a>`NotifyOption`

_Type library:_ `SolidEdgeFramework`  
_Membros:_ 5

| Constante | Valor |
|-----------|-------|
| `igNotifyWhenReadable` | 0 |
| `igNotifyWhenWriteable` | 1 |
| `igNotifyWhenAvailable` | 2 |
| `igNoNotify` | 3 |
| `igNotifyWhenExclusive` | 4 |

## <a name="oleinsertiontypeconstant"></a>`OLEInsertionTypeConstant`

_Type library:_ `SolidEdgeFramework`  
_Membros:_ 5

| Constante | Valor |
|-----------|-------|
| `igUseSymbolPreferences` | -1 |
| `igOLELinked` | 0 |
| `igOLEEmbedded` | 1 |
| `igOLENone` | 3 |
| `igOLESharedEmbedded` | 4 |

## <a name="oleupdateoptionconstant"></a>`OLEUpdateOptionConstant`

_Type library:_ `SolidEdgeFramework`  
_Membros:_ 3

| Constante | Valor |
|-----------|-------|
| `igOLEAutomatic` | 0 |
| `igOLEFrozen` | 1 |
| `igOLEManual` | 2 |

## <a name="objecttype"></a>`ObjectType`

_Type library:_ `SolidEdgeFramework`  
_Membros:_ 130

| Constante | Valor |
|-----------|-------|
| `igReference` | -768828720 |
| `igHorizontalRelation2d` | -280074960 |
| `igVerticalRelation2d` | -83892864 |
| `igPointOnRelation2d` | 273497200 |
| `igParallelRelation2d` | 463670656 |
| `igPerpendicularRelation2d` | 640124384 |
| `igKeyPointRelation2d` | 768508992 |
| `igIntersectRelation2d` | 1166881824 |
| `igSetRelation2d` | 769466240 |
| `igTangentRelation2d` | 709097856 |
| `igFixRelation2d` | -902087584 |
| `igHorizontalAlignRelation2d` | -401894992 |
| `igVerticalAlignRelation2d` | -1179755088 |
| `igConcentricRelation2d` | 1679388272 |
| `igSymmetricRelation2d` | -1337543808 |
| `igOffsetRelation2d` | 296913277 |
| `igEqualRelation2d` | -1337543803 |
| `igColinearRelation2d` | -1337543801 |
| `igFilletRelation2d` | -367594016 |
| `igChamferRelation2d` | 1650559520 |
| `igLinkRelation2d` | 1650559521 |
| `igSheetView` | -945616692 |
| `igDimension` | 488188096 |
| `igLeader` | 1421415312 |
| `igDatumFrame` | -1727514096 |
| `igFeatureContolFrame` | 77832960 |
| `igFeatureControlFrame` | 77832960 |
| `igSurfaceFinishTexture` | 1546072208 |
| `igWeldSymbol` | -42581280 |
| `igBalloon` | 384307874 |
| `igCenterMark` | 505332096 |
| `igCenterLine` | 505332097 |
| `igBoltHoleCircle` | -1157564964 |
| `igDatumTarget` | 1370023760 |
| `igDatumPoint` | -1927130447 |
| `igTextBox` | 2004510816 |
| `igTextProfile` | 18394125 |
| `igSmartFrame2d` | 1532309040 |
| `igArc2d` | -1654322688 |
| `igBsplineCurve2d` | -1803336960 |
| `igCircle2d` | -1876241792 |
| `igComplexString2d` | 928044128 |
| `igFittedCurve2d` | -822504112 |
| `igEllipticalArc2d` | -566911424 |
| `igEllipse2d` | -1555480560 |
| `igLine2d` | 760091584 |
| `igLineString2d` | -126503776 |
| `igPoint2d` | -1728529392 |
| `igBoundary2d` | 515045920 |
| `igSymbol2d` | 1906059870 |
| `igHole2d` | 82322336 |
| `igRectangularPattern2d` | -318384800 |
| `igCircularPattern2d` | 161169008 |
| `igImage2d` | 2018227021 |
| `igGroundRelation3d` | 1959028688 |
| `igAxialRelation3d` | 1472929712 |
| `igPlanarRelation3d` | -2058948880 |
| `igPointRelation3d` | -31813184 |
| `igAngularRelation3d` | 1290792304 |
| `igTangentRelation3d` | 918452310 |
| `igCamFollowerRelation3d` | -1356510324 |
| `igGearRelation3d` | -1244417873 |
| `igCenterPlaneRelation3d` | -890093169 |
| `igRigidSetRelation3d` | 1917428400 |
| `igPathRelation3d` | -1356510323 |
| `igCoordinateSystemRelation3d` | -1282956375 |
| `igPart` | -1879909117 |
| `igSubAssembly` | -1879909116 |
| `igDrawingView` | 1298446368 |
| `igViewPlane` | -1454771360 |
| `igCuttingPlane` | -987770656 |
| `igDetailEnvelope` | -1650313040 |
| `igGroup` | -637360432 |
| `igSubOccurrence` | -1020639365 |
| `igTopologyReference` | 1820054840 |
| `igDividedPart` | -1940558319 |
| `igFamilyMember` | 640059719 |
| `igTube` | 1453593158 |
| `igVariable` | 1984425411 |
| `igRefPlane` | 732824896 |
| `igRefAxis` | -961115504 |
| `igAsmRefPlane` | -1442041198 |
| `igSketch` | 1689979564 |
| `seDVLine2d` | 877758166 |
| `seDVArc2d` | -1804156302 |
| `seDVBSplineCurve2d` | 1550814640 |
| `seDVCircle2d` | -2074243498 |
| `seDVEllipse2d` | -429893932 |
| `seDVLineString2d` | 1720171476 |
| `seDVEllipticalArc2d` | -1799795820 |
| `seDVPoint2d` | 206852904 |
| `seLineSegment` | 1453593156 |
| `seArcSegment` | -1796011174 |
| `seSegmentDirectionRelation3d` | 1491687901 |
| `seSegmentDistanceRelation3d` | 1491687902 |
| `seSegmentRadiusRelation3d` | -1607610092 |
| `seSegmentAngularRelation3d` | -1887087796 |
| `seSegmentPointRelation3d` | 1491687900 |
| `seSegmentTangentRelation3d` | 1576341804 |
| `seOccurrences` | -825730197 |
| `seLayout` | 911078209 |
| `seSelectSet` | -586046304 |
| `seCurveSegment` | 1430040056 |
| `seVirtualComponent` | 1890705030 |
| `seVirtualComponentOccurrence` | -1170758697 |
| `igComponentImage2d` | -1362100758 |
| `igComponentSketch` | -1272438573 |
| `seComponentLayout` | 2038767277 |
| `seConfiguration` | 2079619202 |
| `seAssemblyBodies` | 1900166059 |
| `seAssemblyBody` | -1873862129 |
| `seSubassemblyBodies` | -794120783 |
| `seSubassemblyBody` | 1233590176 |
| `seAssemblyGroup` | 676108639 |
| `seAssemblyGroups` | -2004545918 |
| `igGostWeldSymbol` | -94114560 |
| `igConnector` | 1859111096 |
| `igProfile` | 1584866912 |
| `igProfileSet` | -1521484909 |
| `igOccurrence` | 1610764741 |
| `seSimplifiedAssembly` | -1840907999 |
| `igSketch3D` | -2054330988 |
| `igCircle3D` | -1495700349 |
| `igArc3D` | 2034152404 |
| `igLine3D` | 940686830 |
| `igBSplineCurve3D` | 1245532886 |
| `igCornerAnnotation` | 815208865 |
| `igPoint3D` | 1870596239 |
| `igMeasureVariable` | 922931924 |
| `igConic2d` | 1525194611 |

## <a name="offsetsideconstants"></a>`OffsetSideConstants`

_Type library:_ `SolidEdgePart`  
_Membros:_ 3

| Constante | Valor |
|-----------|-------|
| `seOffsetNone` | 44 |
| `seOffsetLeft` | 1 |
| `seOffsetRight` | 2 |

## <a name="opennonsolidedgefilecontext"></a>`OpenNonSolidEdgeFileContext`

_Type library:_ `SolidEdgeFramework`  
_Membros:_ 9

| Constante | Valor |
|-----------|-------|
| `OpenImage` | 1 |
| `OpenPointCloud` | 2 |
| `OpenDecal` | 3 |
| `OpenViewBackground` | 4 |
| `OpenViewReflection` | 5 |
| `OpenFaceStyleTexture` | 6 |
| `OpenFaceStyleBumpMap` | 7 |
| `OpenFaceStyleReflection` | 8 |
| `OpenStyleOrganizerStyle` | 9 |

## <a name="overwritefilesoption"></a>`OverWriteFilesOption`

_Type library:_ `SolidEdgeFramework`  
_Membros:_ 2

| Constante | Valor |
|-----------|-------|
| `NoToAll` | 0 |
| `YesToAll` | 1 |

## <a name="pmisectiondisplaymodeconstants"></a>`PMISectionDisplayModeConstants`

_Type library:_ `SolidEdgeFramework`  
_Membros:_ 4

| Constante | Valor |
|-----------|-------|
| `sePMISectionDisplayShowOnlyCutFaces` | 0 |
| `sePMISectionDisplayShowCutFacesAndCutBodies` | 1 |
| `sePMISectionDisplayShowCutFacesWithOriginalBodies` | 2 |
| `sePMISectionDisplayShowOnlyOriginalBodies` | 3 |

## <a name="partbasestylesconstants"></a>`PartBaseStylesConstants`

_Type library:_ `SolidEdgePart`  
_Membros:_ 6

| Constante | Valor |
|-----------|-------|
| `sePartBaseStyle` | 0 |
| `seConstructionBaseStyle` | 1 |
| `seThreadedCylindersBaseStyle` | 2 |
| `seCurveBaseStyle` | 3 |
| `seWeldBeadBaseStyle` | 4 |
| `seSectionedFaceBaseStyle` | 5 |

## <a name="patterncurveanchorsideconstants"></a>`PatternCurveAnchorSideConstants`

_Type library:_ `SolidEdgePart`  
_Membros:_ 2

| Constante | Valor |
|-----------|-------|
| `sePatternCurveLeftSide` | 1 |
| `sePatternCurveRightSide` | 2 |

## <a name="patterntransformrotatetypeconstants"></a>`PatternTransformRotateTypeConstants`

_Type library:_ `SolidEdgePart`  
_Membros:_ 2

| Constante | Valor |
|-----------|-------|
| `sePatternTransformRotateOnCurvePosition` | 0 |
| `sePatternTransformRotateOnFeaturePosition` | 1 |

## <a name="patterntransformtypeconstants"></a>`PatternTransformTypeConstants`

_Type library:_ `SolidEdgePart`  
_Membros:_ 4

| Constante | Valor |
|-----------|-------|
| `sePatternTransformLinear` | 0 |
| `sePatternTransformFullRotation` | 1 |
| `sePatternTransformProjectedRotation` | 2 |
| `sePatternTransformFullRotationFromSurface` | 3 |

## <a name="patterntypeconstants"></a>`PatternTypeConstants`

_Type library:_ `SolidEdgePart`  
_Membros:_ 2

| Constante | Valor |
|-----------|-------|
| `seSmartPattern` | 0 |
| `seFastPattern` | 1 |

## <a name="physicalpropertiesstatusconstants"></a>`PhysicalPropertiesStatusConstants`

_Type library:_ `SolidEdgePart`  
_Membros:_ 3

| Constante | Valor |
|-----------|-------|
| `sePhysicalPropertiesStatus_None` | 0 |
| `sePhysicalPropertiesStatus_Model` | 1 |
| `sePhysicalPropertiesStatus_User` | 2 |

## <a name="physicalthreaderrorcode"></a>`PhysicalThreadErrorCode`

_Type library:_ `SolidEdgePart`  
_Membros:_ 8

| Constante | Valor |
|-----------|-------|
| `sePhysicalThreadNoError` | 0 |
| `sePhysicalThreadUnknownError` | 1 |
| `sePhysicalThreadProfileCreationError` | 2 |
| `sePhysicalThreadHelixCreationError` | 3 |
| `sePhysicalThreadBooleanOperationError` | 4 |
| `sePhysicalThreadInvalidThreadTypeError` | 5 |
| `sePhysicalThreadInvalidPitchValueError` | 6 |
| `sePhysicalThreadDisabledByAdminError` | 7 |

## <a name="pointtypeconstants"></a>`PointTypeConstants`

_Type library:_ `SolidEdgePart`  
_Membros:_ 4

| Constante | Valor |
|-----------|-------|
| `igSpacePoint` | 0 |
| `igKeyPoint` | 1 |
| `igCylinderStartPoint` | 2 |
| `igCylinderEndPoint` | 3 |

## <a name="predefinerelationgrouppolarityconstants"></a>`PredefineRelationGroupPolarityConstants`

_Type library:_ `SolidEdgeFramework`  
_Membros:_ 4

| Constante | Valor |
|-----------|-------|
| `MagneticGroup` | 0 |
| `SPoleGroup` | 1 |
| `NPoleGroup` | 2 |
| `CaptureFitGroup` | 3 |

## <a name="print3dfiletype"></a>`Print3DFileType`

_Type library:_ `SolidEdgePart`  
_Membros:_ 2

| Constante | Valor |
|-----------|-------|
| `e3DPrint_STL` | 1 |
| `e3DPrint_3MF` | 2 |

## <a name="profilevalidationtype"></a>`ProfileValidationType`

_Type library:_ `SolidEdgePart`  
_Membros:_ 7

| Constante | Valor |
|-----------|-------|
| `igProfileClosed` | 1 |
| `igProfileSingle` | 4 |
| `igProfileNoSelfIntersect` | 8 |
| `igProfileRefAxisRequired` | 16 |
| `igProfileNoRefAxisIntersect` | 32 |
| `igProfileAllowNested` | 8192 |
| `igProfileAllowPointsAsProfiles` | 524288 |

## <a name="propertyfiltertypeconstants"></a>`PropertyFilterTypeConstants`

_Type library:_ `SolidEdgePart`  
_Membros:_ 7

| Constante | Valor |
|-----------|-------|
| `sePropertyFilterTypeFace` | 1 |
| `sePropertyFilterTypeFaceChain` | 2 |
| `sePropertyFilterTypeFeatureEdges` | 3 |
| `sePropertyFilterTypeFeatureFaces` | 4 |
| `sePropertyFilterTypeEdge` | 5 |
| `sePropertyFilterTypeEdgeChain` | 6 |
| `sePropertyFilterTypeVertex` | 7 |

## <a name="propertytableconstants"></a>`PropertyTableConstants`

_Type library:_ `SolidEdgePart`  
_Membros:_ 3

| Constante | Valor |
|-----------|-------|
| `seCustomPropertyQueryAllProperties` | 1 |
| `seCustomPropertyQueryByTable` | 2 |
| `seCustomPropertyQueryByNameAndValue` | 3 |

## <a name="propertytypeconstants"></a>`PropertyTypeConstants`

_Type library:_ `SolidEdgePart`  
_Membros:_ 3

| Constante | Valor |
|-----------|-------|
| `sePropertyTypeDouble` | 1 |
| `sePropertyTypeString` | 2 |
| `sePropertyTypeInteger` | 3 |

## <a name="radialhatchelementcenterlocation"></a>`RadialHatchElementCenterLocation`

_Type library:_ `SolidEdgeFramework`  
_Membros:_ 10

| Constante | Valor |
|-----------|-------|
| `igRadialHatchElementCenterUnknown` | 0 |
| `igRadialHatchElementCenterTopLeft` | 1 |
| `igRadialHatchElementCenterTopMid` | 2 |
| `igRadialHatchElementCenterTopRight` | 3 |
| `igRadialHatchElementCenterMidLeft` | 4 |
| `igRadialHatchElementCenterMidMid` | 5 |
| `igRadialHatchElementCenterMidRight` | 6 |
| `igRadialHatchElementCenterBottomLeft` | 7 |
| `igRadialHatchElementCenterBottomMid` | 8 |
| `igRadialHatchElementCenterBottomRight` | 9 |

## <a name="redefinefacetangencytype"></a>`RedefineFaceTangencyType`

_Type library:_ `SolidEdgePart`  
_Membros:_ 4

| Constante | Valor |
|-----------|-------|
| `igRedefineFaceNormalToPlane` | 20 |
| `igRedefineFaceTangentContinuous` | 50 |
| `igRedefineFaceNatural` | 113 |
| `igRedefineFaceCurvatureContinuous` | 171 |

## <a name="referenceelementconstants"></a>`ReferenceElementConstants`

_Type library:_ `SolidEdgePart`  
_Membros:_ 28

| Constante | Valor |
|-----------|-------|
| `igRefEleInit` | 0 |
| `igReverseNormalSide` | 1 |
| `igNormalSide` | 2 |
| `igPivotStart` | 3 |
| `igPivotEnd` | 4 |
| `igPlaneRotateLeft` | 5 |
| `igPlaneRotateRight` | 6 |
| `igPlaneFlipHorizontal` | 7 |
| `igPlaneFlipVertical` | 8 |
| `igPlaneAlignX` | 9 |
| `igPlaneAlignY` | 10 |
| `igParallel` | 11 |
| `igAngular` | 12 |
| `igNormalToCurve` | 13 |
| `igCurveStart` | 14 |
| `igCurveEnd` | 15 |
| `igNormalToCurveAtDistance` | 16 |
| `igTangentToFace` | 17 |
| `ig3PointPlane` | 18 |
| `igParallelThroughPoint` | 19 |
| `igNormalToCurveAtDistanceAlongCurve` | 20 |
| `igNormalToCurveAtArcLengthRatio` | 21 |
| `igNormalToCurveAtKeyPoint` | 22 |
| `igPlaneByMirror` | 23 |
| `igFlipNormal` | 24 |
| `igTangentToSurfaceAtAngle` | 25 |
| `igTangentToSurfaceAtKeypoint` | 26 |
| `igAtAngleToSurfaceAtKeypoint` | 27 |

## <a name="referencepointtypeenumforfrompointoption"></a>`ReferencePointTypeEnumForFromPointOption`

_Type library:_ `SolidEdgePart`  
_Membros:_ 2

| Constante | Valor |
|-----------|-------|
| `sePatternReferenceTypeFromCoOrdinateSystem` | 0 |
| `sePatternReferenceTypeFromKeyPoint` | 1 |

## <a name="referencepointtypeenumfortopointoption"></a>`ReferencePointTypeEnumForToPointOption`

_Type library:_ `SolidEdgePart`  
_Membros:_ 2

| Constante | Valor |
|-----------|-------|
| `sePatternReferenceTypeToKeyPoint` | 0 |
| `sePatternReferenceTypeToExcelFirstRow` | 1 |

## <a name="revisionruletype"></a>`RevisionRuleType`

_Type library:_ `SolidEdgeFramework`  
_Membros:_ 5

| Constante | Valor |
|-----------|-------|
| `LastSavedType` | 0 |
| `LatestReleasedRevision` | 1 |
| `LatestRevision` | 2 |
| `ExternalBOM` | 3 |
| `VersionFromCache` | 4 |

## <a name="ribbonbarcontrolsize"></a>`RibbonBarControlSize`

_Type library:_ `SolidEdgeFramework`  
_Membros:_ 3

| Constante | Valor |
|-----------|-------|
| `seRibbonBarControlSizeDefault` | 0 |
| `seRibbonBarControlSizeSmall` | 1 |
| `seRibbonBarControlSizeLarge` | 2 |

## <a name="ribbonbarcontroltext"></a>`RibbonBarControlText`

_Type library:_ `SolidEdgeFramework`  
_Membros:_ 3

| Constante | Valor |
|-----------|-------|
| `seRibbonBarControlTextDefault` | 0 |
| `seRibbonBarControlTextOn` | 1 |
| `seRibbonBarControlTextOff` | 2 |

## <a name="ribbonbarinsertmode"></a>`RibbonBarInsertMode`

_Type library:_ `SolidEdgeFramework`  
_Membros:_ 6

| Constante | Valor |
|-----------|-------|
| `seRibbonBarInsertCopy` | 0 |
| `seRibbonBarInsertMove` | 1 |
| `seRibbonBarInsertCreate` | 2 |
| `seRibbonBarInsertCreateButton` | 3 |
| `seRibbonBarInsertCreatePopup` | 4 |
| `seRibbonBarInsertCreateSplitButtonPopup` | 5 |

## <a name="roundtypeconstants"></a>`RoundTypeConstants`

_Type library:_ `SolidEdgePart`  
_Membros:_ 2

| Constante | Valor |
|-----------|-------|
| `igConstantRadius` | 1 |
| `igVariableRadius` | 2 |

## <a name="routestatus"></a>`RouteStatus`

_Type library:_ `SolidEdgeFramework`  
_Membros:_ 4

| Constante | Valor |
|-----------|-------|
| `igInvalidSlip` | 0 |
| `igRouteComplete` | 1 |
| `igNotYetRouted` | 2 |
| `igRouteInProgress` | 3 |

## <a name="routetype"></a>`RouteType`

_Type library:_ `SolidEdgeFramework`  
_Membros:_ 2

| Constante | Valor |
|-----------|-------|
| `igOneAfterAnother` | 0 |
| `igAllAtOnce` | 1 |

## <a name="ruledsurfacedirectionconstants"></a>`RuledSurfaceDirectionConstants`

_Type library:_ `SolidEdgePart`  
_Membros:_ 2

| Constante | Valor |
|-----------|-------|
| `igRuledSurfaceleft` | 1 |
| `igRuledSurfacRight` | 2 |

## <a name="ruledsurfacesideconstants"></a>`RuledSurfaceSideConstants`

_Type library:_ `SolidEdgePart`  
_Membros:_ 2

| Constante | Valor |
|-----------|-------|
| `igRuledSurfaceInside` | 1 |
| `igRuledSurfaceOutside` | 2 |

## <a name="ruledsurfacetypeconstants"></a>`RuledSurfaceTypeConstants`

_Type library:_ `SolidEdgePart`  
_Membros:_ 5

| Constante | Valor |
|-----------|-------|
| `igRuledTangentContinuous` | 1 |
| `igRuledNormalToPlane` | 2 |
| `igRuledNatural` | 3 |
| `igRuledTaperedToPlane` | 4 |
| `igRuledAlongAnAxis` | 5 |

## <a name="seecoptions"></a>`SEECOptions`

_Type library:_ `SolidEdgeFramework`  
_Membros:_ 2

| Constante | Valor |
|-----------|-------|
| `SEEC_eUnknownOption` | 0 |
| `SEEC_SearchLimit` | 1 |

## <a name="sefixedlengthconstraintdirection"></a>`SEFixedLengthConstraintDirection`

_Type library:_ `SolidEdgePart`  
_Membros:_ 6

| Constante | Valor |
|-----------|-------|
| `igConstraintDirectionNone` | 0 |
| `igConstraintDirectionNoAxis` | 1 |
| `igConstraintDirectionXAxis` | 2 |
| `igConstraintDirectionYAxis` | 3 |
| `igConstraintDirectionZAxis` | 4 |
| `igConstraintDirectionGeometry` | 5 |

## <a name="sepatternrecognitionlevel"></a>`SEPatternRecognitionLevel`

_Type library:_ `SolidEdgePart`  
_Membros:_ 3

| Constante | Valor |
|-----------|-------|
| `igFeaturesPattern` | 0 |
| `igLevelTwoPattern` | 1 |
| `igLevelThreePattern` | 2 |

## <a name="sesubtractdirection"></a>`SESubtractDirection`

_Type library:_ `SolidEdgePart`  
_Membros:_ 3

| Constante | Valor |
|-----------|-------|
| `igSubtractDirectionNone` | 0 |
| `igSubtractDirectionRight` | 1 |
| `igSubtractDirectionLeft` | 2 |

## <a name="setargetconstructionbodyoption"></a>`SETargetConstructionBodyOption`

_Type library:_ `SolidEdgePart`  
_Membros:_ 2

| Constante | Valor |
|-----------|-------|
| `igCreateMultipleConstructionBodiesOnNonManifoldOption` | 0 |
| `igCreateSingleConstructionGeneralBodyOnNonManifoldOption` | 1 |

## <a name="setargetdesignbodyoption"></a>`SETargetDesignBodyOption`

_Type library:_ `SolidEdgePart`  
_Membros:_ 3

| Constante | Valor |
|-----------|-------|
| `igCreateMultipleDesignBodiesOnNonManifoldOption` | 0 |
| `igFailOnNonManifoldOption` | 1 |
| `igCreateSingleDesignBodyOnNonManifoldOption` | 2 |

## <a name="spservertype"></a>`SPServerType`

_Type library:_ `SolidEdgeFramework`  
_Membros:_ 6

| Constante | Valor |
|-----------|-------|
| `SERVER_TYPE_NOT_SHAREPOINT` | 0 |
| `SHAREPOINT_V1_SERVER` | 1 |
| `SHAREPOINT_V2_SERVER` | 2 |
| `SHAREPOINT_V3_SERVER` | 3 |
| `SHAREPOINT_V4_SERVER` | 4 |
| `SHAREPOINT_V5_SERVER` | 5 |

## <a name="saveasflatfiletypes"></a>`SaveAsFlatFileTypes`

_Type library:_ `SolidEdgePart`  
_Membros:_ 3

| Constante | Valor |
|-----------|-------|
| `igAutoCAD` | 0 |
| `igPart_Document` | 1 |
| `igSheet_Metal_Document` | 2 |

## <a name="seanalysismodetype"></a>`SeAnalysisModeType`

_Type library:_ `SolidEdgeFramework`  
_Membros:_ 6

| Constante | Valor |
|-----------|-------|
| `seAnalysisModeDefault` | 0 |
| `seAnalysisModeZebraStripeLinear` | 1 |
| `seAnalysisModeZebraStripeSpherical` | 2 |
| `seAnalysisModeZebraStripeReflection` | 3 |
| `seAnalysisModeCurvatureColor` | 4 |
| `seAnalysisModeDraftAngle` | 5 |

## <a name="seanalysisstatetype"></a>`SeAnalysisStateType`

_Type library:_ `SolidEdgeFramework`  
_Membros:_ 3

| Constante | Valor |
|-----------|-------|
| `seAnalysisStateNone` | 0 |
| `seAnalysisStateGlobal` | 1 |
| `seAnalysisStateLocal` | 2 |

## <a name="seantialiaslevel"></a>`SeAntiAliasLevel`

_Type library:_ `SolidEdgeFramework`  
_Membros:_ 4

| Constante | Valor |
|-----------|-------|
| `seAntiAliasLevelNone` | 0 |
| `seAntiAliasLevelLow` | 2 |
| `seAntiAliasLevelMedium` | 4 |
| `seAntiAliasLevelHigh` | 8 |

## <a name="sebackgroundtype"></a>`SeBackgroundType`

_Type library:_ `SolidEdgeFramework`  
_Membros:_ 4

| Constante | Valor |
|-----------|-------|
| `seBackgroundTypeSolid` | 0 |
| `seBackgroundTypeGradient` | 1 |
| `seBackgroundTypeImage` | 2 |
| `seBackgroundTypeImageReference` | 3 |

## <a name="sebarposition"></a>`SeBarPosition`

_Type library:_ `SolidEdgeFramework`  
_Membros:_ 5

| Constante | Valor |
|-----------|-------|
| `seBarTop` | 1 |
| `seBarBottom` | 2 |
| `seBarLeft` | 3 |
| `seBarRight` | 4 |
| `seBarFloating` | 5 |

## <a name="sebartype"></a>`SeBarType`

_Type library:_ `SolidEdgeFramework`  
_Membros:_ 3

| Constante | Valor |
|-----------|-------|
| `seBarTypeMenuBar` | 1 |
| `seBarTypeNormal` | 2 |
| `seBarTypePopup` | 3 |

## <a name="sebuttonstate"></a>`SeButtonState`

_Type library:_ `SolidEdgeFramework`  
_Membros:_ 3

| Constante | Valor |
|-----------|-------|
| `seButtonDown` | 1 |
| `seButtonMixed` | 2 |
| `seButtonUp` | 3 |

## <a name="sebuttonstyle"></a>`SeButtonStyle`

_Type library:_ `SolidEdgeFramework`  
_Membros:_ 9

| Constante | Valor |
|-----------|-------|
| `seButtonAutomatic` | 1 |
| `seButtonCaption` | 2 |
| `seButtonIcon` | 3 |
| `seButtonIconAndCaption` | 4 |
| `seButtonIconAndCaptionBelow` | 5 |
| `seCheckButton` | 6 |
| `seCheckButtonAndIcon` | 7 |
| `seRadioButton` | 8 |
| `seNoExitBackstageButton` | 9 |

## <a name="seconnectmode"></a>`SeConnectMode`

_Type library:_ `SolidEdgeFramework`  
_Membros:_ 3

| Constante | Valor |
|-----------|-------|
| `seConnectAtStartup` | 1 |
| `seConnectByUser` | 2 |
| `seConnectExternally` | 3 |

## <a name="secontroltype"></a>`SeControlType`

_Type library:_ `SolidEdgeFramework`  
_Membros:_ 3

| Constante | Valor |
|-----------|-------|
| `seControlPopup` | 1 |
| `seControlButton` | 2 |
| `seControlSeparator` | 3 |

## <a name="sedisconnectmode"></a>`SeDisconnectMode`

_Type library:_ `SolidEdgeFramework`  
_Membros:_ 3

| Constante | Valor |
|-----------|-------|
| `seDisconnectAtShutdown` | 1 |
| `seDisconnectByUser` | 2 |
| `seDisconnectExternally` | 3 |

## <a name="sefeatureaddflag"></a>`SeFeatureAddFlag`

_Type library:_ `SolidEdgeFramework`  
_Membros:_ 5

| Constante | Valor |
|-----------|-------|
| `seNew` | 1 |
| `seUnSuppress` | 2 |
| `seUnSuppressUpTo` | 3 |
| `seNewPatternItem` | 4 |
| `seUnSuppressPatternItem` | 5 |

## <a name="sefeaturedeleteflag"></a>`SeFeatureDeleteFlag`

_Type library:_ `SolidEdgeFramework`  
_Membros:_ 5

| Constante | Valor |
|-----------|-------|
| `sePermanent` | 1 |
| `seSuppress` | 2 |
| `seSuppressDownTo` | 3 |
| `sePermanentPatternItem` | 4 |
| `seSuppressPatternItem` | 5 |

## <a name="sefeaturemodifyflag"></a>`SeFeatureModifyFlag`

_Type library:_ `SolidEdgeFramework`  
_Membros:_ 3

| Constante | Valor |
|-----------|-------|
| `seSchemaChanged` | 1 |
| `seDirectInputsChanged` | 2 |
| `seReordered` | 3 |

## <a name="segradienttype"></a>`SeGradientType`

_Type library:_ `SolidEdgeFramework`  
_Membros:_ 7

| Constante | Valor |
|-----------|-------|
| `seGradientTypeHorizontal` | 1 |
| `seGradientTypeVertical` | 2 |
| `seGradientTypeDiagonalUp` | 3 |
| `seGradientTypeDiagonalDown` | 4 |
| `seGradientTypeSquareSpot` | 5 |
| `seGradientTypeCircularSpot` | 6 |
| `seGradientTypeCustom` | 7 |

## <a name="sehiddenlinemode"></a>`SeHiddenLineMode`

_Type library:_ `SolidEdgeFramework`  
_Membros:_ 3

| Constante | Valor |
|-----------|-------|
| `seHiddenLineModeOff` | 0 |
| `seHiddenLineModeDim` | 1 |
| `seHiddenLineModeDashed` | 2 |

## <a name="seimagequalitytype"></a>`SeImageQualityType`

_Type library:_ `SolidEdgeFramework`  
_Membros:_ 3

| Constante | Valor |
|-----------|-------|
| `seImageQualityLow` | 1 |
| `seImageQualityMedium` | 2 |
| `seImageQualityHigh` | 3 |

## <a name="semodifysketchflag"></a>`SeModifySketchFlag`

_Type library:_ `SolidEdgeFramework`  
_Membros:_ 3

| Constante | Valor |
|-----------|-------|
| `seInsertEntity` | 1 |
| `seRemoveEntity` | 2 |
| `seModifyEntity` | 3 |

## <a name="seobjecttype"></a>`SeObjectType`

_Type library:_ `SolidEdgeFramework`  
_Membros:_ 3

| Constante | Valor |
|-----------|-------|
| `seObjectNamedViews` | 1 |
| `seObjectViewStyles` | 2 |
| `seObjectFaceStyles` | 3 |

## <a name="serenderfillmode"></a>`SeRenderFillMode`

_Type library:_ `SolidEdgeFramework`  
_Membros:_ 3

| Constante | Valor |
|-----------|-------|
| `seRenderFillSolid` | 1 |
| `seRenderFillBorder` | 2 |
| `seRenderFillSolidBorder` | 3 |

## <a name="serendermaterialgetmode"></a>`SeRenderMaterialGetMode`

_Type library:_ `SolidEdgeFramework`  
_Membros:_ 2

| Constante | Valor |
|-----------|-------|
| `seGetModeExisting` | 0 |
| `seGetModeCreateOnDemand` | 1 |

## <a name="serendermaterialsetmode"></a>`SeRenderMaterialSetMode`

_Type library:_ `SolidEdgeFramework`  
_Membros:_ 4

| Constante | Valor |
|-----------|-------|
| `seSetModeDetach` | 0 |
| `seSetModeAttach` | 1 |
| `seSetModeUpdate` | 2 |
| `seSetModeAttachAndUpdate` | 3 |

## <a name="serendermodetype"></a>`SeRenderModeType`

_Type library:_ `SolidEdgeFramework`  
_Membros:_ 12

| Constante | Valor |
|-----------|-------|
| `seRenderModeUndefined` | 0 |
| `seRenderModeWireframe` | 1 |
| `seRenderModeWiremesh` | 2 |
| `seRenderModeOutline` | 3 |
| `seRenderModeBoundary` | 4 |
| `seRenderModeVHL` | 6 |
| `seRenderModeSmooth` | 8 |
| `seRenderModeSmoothMesh` | 9 |
| `seRenderModeSmoothVHL` | 10 |
| `seRenderModeSmoothBoundary` | 11 |
| `seRenderModePhong` | 81 |
| `seRenderModeRayTraced` | 83 |

## <a name="serendershademode"></a>`SeRenderShadeMode`

_Type library:_ `SolidEdgeFramework`  
_Membros:_ 2

| Constante | Valor |
|-----------|-------|
| `seRenderShadeModeFlat` | 1 |
| `seRenderShadeModeSmooth` | 2 |

## <a name="serendershapetype"></a>`SeRenderShapeType`

_Type library:_ `SolidEdgeFramework`  
_Membros:_ 2

| Constante | Valor |
|-----------|-------|
| `seRenderShapeSquare` | 1 |
| `seRenderShapeRound` | 2 |

## <a name="serenderspacetype"></a>`SeRenderSpaceType`

_Type library:_ `SolidEdgeFramework`  
_Membros:_ 3

| Constante | Valor |
|-----------|-------|
| `seRenderSpaceDevice` | 0 |
| `seRenderSpacePaper` | 1 |
| `seRenderSpaceWorld` | 2 |

## <a name="seskyboxtype"></a>`SeSkyboxType`

_Type library:_ `SolidEdgeFramework`  
_Membros:_ 4

| Constante | Valor |
|-----------|-------|
| `seSkyboxTypeSkybox` | 0 |
| `seSkyboxTypeSingleImage` | 1 |
| `seSkyboxTypeSpheremap` | 2 |
| `seSkyboxTypeUndefined` | -1 |

## <a name="sectionsketcheserrorcode"></a>`SectionSketchesErrorCode`

_Type library:_ `SolidEdgePart`  
_Membros:_ 4

| Constante | Valor |
|-----------|-------|
| `seSectionSketchesUnknownError` | -1 |
| `seSectionSketchesNoError` | 0 |
| `seSectionSketchesNotPlaneIntersecting` | 1 |
| `seSectionSketchesSomePlaneIntersecting` | 2 |

## <a name="sectionsketchesplanesdirection"></a>`SectionSketchesPlanesDirection`

_Type library:_ `SolidEdgePart`  
_Membros:_ 3

| Constante | Valor |
|-----------|-------|
| `seSectionSketchesPlaneReverseNormalSide` | -1 |
| `seSectionSketchesPlaneNormalSide` | 0 |
| `seSectionSketchesPlaneBothSide` | 1 |

## <a name="sectionviewextentside"></a>`SectionViewExtentSide`

_Type library:_ `SolidEdgeFramework`  
_Membros:_ 6

| Constante | Valor |
|-----------|-------|
| `igLeftExtent` | 1 |
| `igRightExtent` | 2 |
| `igFiniteSymmetricExtent` | 3 |
| `igInfiniteLeftExtent` | 4 |
| `igInfiniteRightExtent` | 5 |
| `igThroughAllExtent` | 6 |

## <a name="sectionviewplaneextenttypeconstant"></a>`SectionViewPlaneExtentTypeConstant`

_Type library:_ `SolidEdgeFramework`  
_Membros:_ 2

| Constante | Valor |
|-----------|-------|
| `SectionViewPlaneExtentTypeConstant_Bounded` | 1 |
| `SectionViewPlaneExtentTypeConstant_UnBounded` | 2 |

## <a name="sectionviewplanetype"></a>`SectionViewPlaneType`

_Type library:_ `SolidEdgeFramework`  
_Membros:_ 2

| Constante | Valor |
|-----------|-------|
| `igDynamic` | 1 |
| `igAssociative` | 2 |

## <a name="sectionviewprofileside"></a>`SectionViewProfileSide`

_Type library:_ `SolidEdgeFramework`  
_Membros:_ 4

| Constante | Valor |
|-----------|-------|
| `igLeftProfileSide` | 1 |
| `igRightProfileSide` | 2 |
| `igInsideProfileSide` | 3 |
| `igOutsideProfileSide` | 4 |

## <a name="sensordisplaytypeconstants"></a>`SensorDisplayTypeConstants`

_Type library:_ `SolidEdgeFramework`  
_Membros:_ 3

| Constante | Valor |
|-----------|-------|
| `seSensorDisplayTypeInvalid` | 0 |
| `seSensorDisplayTypeHorizontalRange` | 1 |
| `seSensorDisplayTypeTrueFalse` | 2 |

## <a name="sensoroperatorconstants"></a>`SensorOperatorConstants`

_Type library:_ `SolidEdgeFramework`  
_Membros:_ 7

| Constante | Valor |
|-----------|-------|
| `seSensorOperatorInvalid` | 0 |
| `seSensorOperatorGreaterThan` | 1 |
| `seSensorOperatorLessThan` | 2 |
| `seSensorOperatorEqualTo` | 3 |
| `seSensorOperatorNotEqualTo` | 4 |
| `seSensorOperatorBetween` | 5 |
| `seSensorOperatorNotBetween` | 6 |

## <a name="sensorstatusconstants"></a>`SensorStatusConstants`

_Type library:_ `SolidEdgeFramework`  
_Membros:_ 3

| Constante | Valor |
|-----------|-------|
| `seSensorStatusUpToDate` | 0 |
| `seSensorStatusOutOfDate` | 1 |
| `seSensorStatusInError` | 2 |

## <a name="sensortypeconstants"></a>`SensorTypeConstants`

_Type library:_ `SolidEdgeFramework`  
_Membros:_ 4

| Constante | Valor |
|-----------|-------|
| `seSensorTypeInvalid` | 0 |
| `seSensorTypeVariable` | 1 |
| `seSensorTypeMinimumDistance` | 6 |
| `seSensorTypeUser` | 7 |

## <a name="sensorupdatemechanismconstants"></a>`SensorUpdateMechanismConstants`

_Type library:_ `SolidEdgeFramework`  
_Membros:_ 3

| Constante | Valor |
|-----------|-------|
| `seSensorUpdateMechanismInvalid` | 0 |
| `seSensorUpdateMechanismAutomatic` | 1 |
| `seSensorUpdateMechanismManual` | 2 |

## <a name="sheetmetalsensorfeaturetypeconstants"></a>`SheetMetalSensorFeatureTypeConstants`

_Type library:_ `SolidEdgeFramework`  
_Membros:_ 8

| Constante | Valor |
|-----------|-------|
| `seSheetMetalSensorFeatureTypeExteriorEdges` | 0 |
| `seSheetMetalSensorFeatureTypeInteriorEdges` | 1 |
| `seSheetMetalSensorFeatureTypeCutouts` | 2 |
| `seSheetMetalSensorFeatureTypeHoles` | 3 |
| `seSheetMetalSensorFeatureTypeDimples` | 4 |
| `seSheetMetalSensorFeatureTypeLouvers` | 5 |
| `seSheetMetalSensorFeatureTypeDrawnCutouts` | 6 |
| `seSheetMetalSensorFeatureTypeBeads` | 7 |

## <a name="shortcutmenucontextconstants"></a>`ShortCutMenuContextConstants`

_Type library:_ `SolidEdgeFramework`  
_Membros:_ 7

| Constante | Valor |
|-----------|-------|
| `seShortCutForGraphicLocate` | 1 |
| `seShortCutForView` | 2 |
| `seShortCutForFeaturePathFinder` | 3 |
| `seShortCutForFeaturePathFinderDocument` | 4 |
| `seShortCutNone` | 5 |
| `seShortCutSimulationPathfinder` | 6 |
| `seShortCutForPredictCommand` | 7 |

## <a name="solidedgecommandconstants"></a>`SolidEdgeCommandConstants`

_Type library:_ `SolidEdgeFramework`  
_Membros:_ 9

| Constante | Valor |
|-----------|-------|
| `seAssemblyPlacePartCommand` | 32791 |
| `sePartSelectCommand` | 45000 |
| `sePartInsertPartCommand` | 40254 |
| `seSheetMetalSelectCommand` | 45000 |
| `seAssemblySelectCommand` | 45000 |
| `seDraftSelectCommand` | 45000 |
| `seConvertCommand` | 10452 |
| `seRefreshViewCommand` | 32876 |
| `seSurfaceVisualCommand` | 11129 |

## <a name="spiralcurvemethodtype"></a>`SpiralCurveMethodType`

_Type library:_ `SolidEdgePart`  
_Membros:_ 3

| Constante | Valor |
|-----------|-------|
| `igSpiralCurveMethodEndDiameterAndTurns` | 0 |
| `igSpiralCurveMethodEndDiameterAndRadialPitch` | 1 |
| `igSpiralCurveMethodRadialPitchAndTurns` | 2 |

## <a name="stitchweldannotationformat"></a>`StitchWeldAnnotationFormat`

_Type library:_ `SolidEdgePart`  
_Membros:_ 3

| Constante | Valor |
|-----------|-------|
| `seLengthPitch` | 1 |
| `seNXL` | 2 |
| `seNXL_E` | 3 |

## <a name="stitchweldtype"></a>`StitchWeldType`

_Type library:_ `SolidEdgePart`  
_Membros:_ 3

| Constante | Valor |
|-----------|-------|
| `seStitchOnly` | 1 |
| `seStitchPlusOffsets` | 2 |
| `seOffsetsOnly` | 3 |

## <a name="styleunitsconstant"></a>`StyleUnitsConstant`

_Type library:_ `SolidEdgeFramework`  
_Membros:_ 3

| Constante | Valor |
|-----------|-------|
| `PAPER_STYLEUNITS` | 11 |
| `DESIGN_STYLEUNITS` | 12 |
| `VIEW_STYLEUNITS` | 13 |

## <a name="subdivisiondragtypeconstants"></a>`SubdivisionDragTypeConstants`

_Type library:_ `SolidEdgePart`  
_Membros:_ 2

| Constante | Valor |
|-----------|-------|
| `igLinearMoveType` | 1 |
| `igRotateType` | 2 |

## <a name="suppressregionsconstants"></a>`SuppressRegionsConstants`

_Type library:_ `SolidEdgePart`  
_Membros:_ 2

| Constante | Valor |
|-----------|-------|
| `seSuppressRegionInside` | 4 |
| `seSuppressRegionOutside` | 5 |

## <a name="surfaceareasensorareatypeconstants"></a>`SurfaceAreaSensorAreaTypeConstants`

_Type library:_ `SolidEdgeFramework`  
_Membros:_ 2

| Constante | Valor |
|-----------|-------|
| `seSurfaceAreaSensorAreaTypeNeg` | 0 |
| `seSurfaceAreaSensorAreaTypePos` | 1 |

## <a name="surfaceareasensorselectiontypeconstants"></a>`SurfaceAreaSensorSelectionTypeConstants`

_Type library:_ `SolidEdgeFramework`  
_Membros:_ 2

| Constante | Valor |
|-----------|-------|
| `seSurfaceAreaSensorSelectFace` | 0 |
| `seSurfaceAreaSensorSelectFaceChain` | 1 |

## <a name="surfacebyboundaryconstants"></a>`SurfaceByBoundaryConstants`

_Type library:_ `SolidEdgePart`  
_Membros:_ 2

| Constante | Valor |
|-----------|-------|
| `igSurfaceByBoundaryPreferPlanar` | 1 |
| `igSurfaceByBoundaryTangent` | 2 |

## <a name="surfacebyboundaryfillpreference"></a>`SurfaceByBoundaryFillPreference`

_Type library:_ `SolidEdgePart`  
_Membros:_ 4

| Constante | Valor |
|-----------|-------|
| `igSurfaceByBoundaryFillSmooth` | 1 |
| `igSurfaceByBoundaryFillNonSmooth` | 2 |
| `igSurfaceByBoundaryFillPreferPlane` | 3 |
| `igSurfaceByBoundaryFillPlaneOnly` | 4 |

## <a name="surfacebyboundaryinternalsmoothness"></a>`SurfaceByBoundaryInternalSmoothness`

_Type library:_ `SolidEdgePart`  
_Membros:_ 2

| Constante | Valor |
|-----------|-------|
| `igSurfaceByBoundarySharp` | 1 |
| `igSurfaceByBoundarySmooth` | 2 |

## <a name="surfacebyboundarypatchtopology"></a>`SurfaceByBoundaryPatchTopology`

_Type library:_ `SolidEdgePart`  
_Membros:_ 3

| Constante | Valor |
|-----------|-------|
| `igSurfaceByBoundaryMinimal` | 1 |
| `igSurfaceByBoundarySingle` | 2 |
| `igSurfaceByBoundaryMultiple` | 3 |

## <a name="surfacebyboundarytangencytype"></a>`SurfaceByBoundaryTangencyType`

_Type library:_ `SolidEdgePart`  
_Membros:_ 3

| Constante | Valor |
|-----------|-------|
| `igSurfaceByBoundaryNatural` | 113 |
| `igSurfaceByBoundaryTangential` | 50 |
| `igSurfaceByBoundaryCurvatureContinuous` | 171 |

## <a name="syncoption"></a>`SyncOption`

_Type library:_ `SolidEdgeFramework`  
_Membros:_ 2

| Constante | Valor |
|-----------|-------|
| `SEECSyncAll` | 0 |
| `SEECSyncOne` | 1 |

## <a name="tcesetypes"></a>`TCESETypes`

_Type library:_ `SolidEdgeFramework`  
_Membros:_ 5

| Constante | Valor |
|-----------|-------|
| `TCE_SEPart` | 0 |
| `TCE_SEAssembly` | 1 |
| `TCE_SEWeldment` | 2 |
| `TCE_SESheetmetal` | 3 |
| `TCE_SEDraft` | 4 |

## <a name="templateslisttype"></a>`TemplatesListType`

_Type library:_ `SolidEdgeFramework`  
_Membros:_ 4

| Constante | Valor |
|-----------|-------|
| `eUnknownTemplateList` | 0 |
| `eStandardTemplateList` | 1 |
| `eUserTemplateList` | 2 |
| `eCustomTemplateList` | 3 |

## <a name="textstylenumberjustificationconstants"></a>`TextStyleNumberJustificationConstants`

_Type library:_ `SolidEdgeFramework`  
_Membros:_ 3

| Constante | Valor |
|-----------|-------|
| `igLeftJustificationStyle` | 0 |
| `igCenterJustificationStyle` | 1 |
| `igRightJustificationStyle` | 2 |

## <a name="threaddiameteroptionconstants"></a>`ThreadDiameterOptionConstants`

_Type library:_ `SolidEdgePart`  
_Membros:_ 4

| Constante | Valor |
|-----------|-------|
| `seTapDrillDiameter` | 0 |
| `seInternalMinorDiameter` | 1 |
| `seNominalDiameter` | 2 |
| `seInsidePipeDiameter` | 3 |

## <a name="treatmentcrowncurvaturesideconstants"></a>`TreatmentCrownCurvatureSideConstants`

_Type library:_ `SolidEdgePart`  
_Membros:_ 3

| Constante | Valor |
|-----------|-------|
| `seTreatmentCrownCurvatureNone` | 44 |
| `seTreatmentCrownCurvatureInside` | 4 |
| `seTreatmentCrownCurvatureOutside` | 5 |

## <a name="treatmentcrownsideconstants"></a>`TreatmentCrownSideConstants`

_Type library:_ `SolidEdgePart`  
_Membros:_ 3

| Constante | Valor |
|-----------|-------|
| `seTreatmentCrownSideNone` | 44 |
| `seTreatmentCrownSideInside` | 4 |
| `seTreatmentCrownSideOutside` | 5 |

## <a name="treatmentcrowntypeconstants"></a>`TreatmentCrownTypeConstants`

_Type library:_ `SolidEdgePart`  
_Membros:_ 5

| Constante | Valor |
|-----------|-------|
| `seTreatmentCrownNone` | 0 |
| `seTreatmentCrownByRadius` | 1 |
| `seTreatmentCrownByRadiusAndTakeOffAngle` | 2 |
| `seTreatmentCrownByOffset` | 3 |
| `seTreatmentCrownByOffsetAndTakeOffAngle` | 4 |

## <a name="treatmenttypeconstants"></a>`TreatmentTypeConstants`

_Type library:_ `SolidEdgePart`  
_Membros:_ 3

| Constante | Valor |
|-----------|-------|
| `seTreatmentNone` | 44 |
| `seTreatmentDraft` | 173 |
| `seTreatmentCrown` | 174 |

## <a name="trimextenderrorcode"></a>`TrimExtendErrorCode`

_Type library:_ `SolidEdgePart`  
_Membros:_ 6

| Constante | Valor |
|-----------|-------|
| `TrimExtendErrorCodeUnknownError` | -1 |
| `TrimExtendErrorCodeNoError` | 0 |
| `TrimExtendErrorCodeMissingParameter` | 1 |
| `TrimExtendErrorCodeInvalidParameter` | 2 |
| `TrimExtendErrorCodeNoReferencePlane` | 3 |
| `TrimExtendErrorCodeEditFailed` | 4 |

## <a name="trimsurfaceareasideconstants"></a>`TrimSurfaceAreaSideConstants`

_Type library:_ `SolidEdgePart`  
_Membros:_ 2

| Constante | Valor |
|-----------|-------|
| `igTSLeft` | 1 |
| `igTSRight` | 2 |

## <a name="unitofmeasureanglereadoutconstants"></a>`UnitOfMeasureAngleReadoutConstants`

_Type library:_ `SolidEdgePart`  
_Membros:_ 10

| Constante | Valor |
|-----------|-------|
| `seAngleRadian` | 0 |
| `seAngleDegree` | 1 |
| `seAngleMinute` | 2 |
| `seAngleSecond` | 3 |
| `seAngleGradient` | 4 |
| `seAngleDegreeMinuteSecond` | 5 |
| `seAngleDegreeAbbr` | 6 |
| `seAngleMinuteAbbr` | 7 |
| `seAngleSecondAbbr` | 8 |
| `seAngleDegreeMinuteSecondAbbr` | 9 |

## <a name="unitofmeasurelengthreadoutconstants"></a>`UnitOfMeasureLengthReadoutConstants`

_Type library:_ `SolidEdgePart`  
_Membros:_ 20

| Constante | Valor |
|-----------|-------|
| `seLengthInch` | 0 |
| `seLengthFoot` | 1 |
| `seLengthInchAbbr` | 2 |
| `seLengthFootAbbr` | 3 |
| `seLengthFootInch` | 4 |
| `seLengthFootInchAbbr` | 5 |
| `seLengthYard` | 6 |
| `seLengthMile` | 7 |
| `seLengthTenth` | 8 |
| `seLengthHundredth` | 9 |
| `seLengthThousandth` | 10 |
| `seLengthRod` | 11 |
| `seLengthPole` | 12 |
| `seLengthChain` | 13 |
| `seLengthFurlong` | 14 |
| `seLengthMeter` | 15 |
| `seLengthCentimeter` | 16 |
| `seLengthMillimeter` | 17 |
| `seLengthKilometer` | 18 |
| `seLengthNanometer` | 19 |

## <a name="unittypeconstants"></a>`UnitTypeConstants`

_Type library:_ `SolidEdgeFramework`  
_Membros:_ 63

| Constante | Valor |
|-----------|-------|
| `igUnitDistance` | 1 |
| `igUnitAngle` | 2 |
| `igUnitMass` | 3 |
| `igUnitTime` | 4 |
| `igUnitTemperature` | 5 |
| `igUnitElectricCharge` | 6 |
| `igUnitLuminousIntensity` | 7 |
| `igUnitAmountOfSubstance` | 8 |
| `igUnitSolidAngle` | 9 |
| `igUnitAngularAcceleration` | 10 |
| `igUnitAngularMomentum` | 11 |
| `igUnitAngularVelocity` | 12 |
| `igUnitArea` | 13 |
| `igUnitBodyForce` | 14 |
| `igUnitCoefficientOfThermalExpansion` | 15 |
| `igUnitDensity` | 16 |
| `igUnitElectricalCapacitance` | 17 |
| `igUnitElectricalConductance` | 18 |
| `igUnitElectricalFieldStrength` | 19 |
| `igUnitElectricalInductance` | 20 |
| `igUnitElectricalPotential` | 21 |
| `igUnitElectricalResistance` | 22 |
| `igUnitEnergy` | 23 |
| `igUnitEntropy` | 24 |
| `igUnitFilmCoefficient` | 25 |
| `igUnitForce` | 26 |
| `igUnitForcePerArea` | 27 |
| `igUnitForcePerDistance` | 28 |
| `igUnitFrequency` | 29 |
| `igUnitHeatCapacity` | 30 |
| `igUnitHeatFluxPerArea` | 31 |
| `igUnitHeatFluxPerDistance` | 32 |
| `igUnitHeatSource` | 33 |
| `igUnitIlluminance` | 34 |
| `igUnitLinearAcceleration` | 35 |
| `igUnitLinearPerAngular` | 36 |
| `igUnitLinearVelocity` | 37 |
| `igUnitLuminousFlux` | 38 |
| `igUnitMagneticFieldStrength` | 39 |
| `igUnitMagneticFlux` | 40 |
| `igUnitMagneticFluxDensity` | 41 |
| `igUnitMassFlowRate` | 42 |
| `igUnitMassMomentOfInertia` | 43 |
| `igUnitMassPerArea` | 44 |
| `igUnitMassPerLength` | 45 |
| `igUnitMomentum` | 46 |
| `igUnitPerDistance` | 47 |
| `igUnitPower` | 48 |
| `igUnitQuantityOfElectricity` | 49 |
| `igUnitRadiantIntensity` | 50 |
| `igUnitRotationalStiffness` | 51 |
| `igUnitSecondMomentOfArea` | 52 |
| `igUnitThermalConductivity` | 53 |
| `igUnitDynamicViscosity` | 54 |
| `igUnitKinematicViscosity` | 55 |
| `igUnitVolume` | 56 |
| `igUnitVolumeFlowRate` | 57 |
| `igUnitScalar` | 58 |
| `igUnitTorque` | 59 |
| `igUnitEnergyDensity` | 60 |
| `igUnitPressure` | 61 |
| `igUnitHeatGeneration` | 62 |
| `igUnitTemperatureGradient` | 63 |

## <a name="uploadtype"></a>`UploadType`

_Type library:_ `SolidEdgeFramework`  
_Membros:_ 2

| Constante | Valor |
|-----------|-------|
| `DeepUploadType` | 0 |
| `ShallowUploadType` | 1 |

## <a name="variablelimitvalueconstant"></a>`VariableLimitValueConstant`

_Type library:_ `SolidEdgeFramework`  
_Membros:_ 3

| Constante | Valor |
|-----------|-------|
| `igVariableLimitNone` | 0 |
| `igDiscreteList` | 1 |
| `igMinMaxLimit` | 2 |

## <a name="ventdraftsideconstants"></a>`VentDraftSideConstants`

_Type library:_ `SolidEdgePart`  
_Membros:_ 2

| Constante | Valor |
|-----------|-------|
| `seVentDraftSideOutward` | 5 |
| `seVentDraftSideInward` | 4 |

## <a name="ventextentsideconstants"></a>`VentExtentSideConstants`

_Type library:_ `SolidEdgePart`  
_Membros:_ 2

| Constante | Valor |
|-----------|-------|
| `seVentSketchPlaneNormalSide` | 2 |
| `seVentReverseSketchPlaneNormalSide` | 1 |

## <a name="ventextenttypeconstants"></a>`VentExtentTypeConstants`

_Type library:_ `SolidEdgePart`  
_Membros:_ 4

| Constante | Valor |
|-----------|-------|
| `seVentExtentTypeThroughNext` | 14 |
| `seVentExtentTypeThroughAll` | 16 |
| `seVentExtentTypeFinite` | 13 |
| `seVentExtentToKeyPoint` | 72 |

## <a name="webnetworkfeatureconstants"></a>`WebNetworkFeatureConstants`

_Type library:_ `SolidEdgePart`  
_Membros:_ 6

| Constante | Valor |
|-----------|-------|
| `seWebNormal` | 1 |
| `seWebReverseNormal` | 2 |
| `seWebExtendToNext` | 3 |
| `seWebExtendFinite` | 4 |
| `seWebProfileExtend` | 5 |
| `seWebProfileNoExtend` | 6 |

## <a name="weldmentglobalconstants"></a>`WeldmentGlobalConstants`

_Type library:_ `SolidEdgePart`  
_Membros:_ 7

| Constante | Valor |
|-----------|-------|
| `seWeldmentGlobalDensity` | 1 |
| `seWeldmentGlobalAccuracyForDensity` | 2 |
| `seWeldmentGlobalBeadsDensity` | 3 |
| `seWeldmentGlobalMaterial` | 4 |
| `seWeldmentGlobalBeadMaterial` | 5 |
| `seWeldmentGlobalAutomaticUpdate` | 6 |
| `seWeldmentGlobalUpdatePhysicalPropertiesOnSave` | 7 |

## <a name="weldmentlinkstatusconstants"></a>`WeldmentLinkStatusConstants`

_Type library:_ `SolidEdgePart`  
_Membros:_ 3

| Constante | Valor |
|-----------|-------|
| `seLinkOK` | 1 |
| `seLinkOutOfDate` | 2 |
| `seLinkBroken` | 3 |

## <a name="weldmentsectiontypeconstants"></a>`WeldmentSectionTypeConstants`

_Type library:_ `SolidEdgePart`  
_Membros:_ 4

| Constante | Valor |
|-----------|-------|
| `seWeldmentSectionTypeComponent` | 0 |
| `seWeldmentSectionTypeSurfacePrep` | 1 |
| `seWeldmentSectionTypeBead` | 2 |
| `seWeldmentSectionTypeMachining` | 3 |

## <a name="workflowaction"></a>`WorkflowAction`

_Type library:_ `SolidEdgeFramework`  
_Membros:_ 4

| Constante | Valor |
|-----------|-------|
| `Initiate` | 0 |
| `Delegate` | 1 |
| `Accept` | 2 |
| `Reject` | 3 |

## <a name="workflowtype"></a>`WorkflowType`

_Type library:_ `SolidEdgeFramework`  
_Membros:_ 2

| Constante | Valor |
|-----------|-------|
| `OneStepRelease` | 0 |
| `QuickRelease` | 1 |

## <a name="ecpdmode"></a>`eCPDMode`

_Type library:_ `SolidEdgeFramework`  
_Membros:_ 4

| Constante | Valor |
|-----------|-------|
| `CPD_NEW_FILE` | 1 |
| `CPD_UPLOAD_FILE` | 2 |
| `CPD_SAVEAS_FILE` | 3 |
| `CPD_REVISE_FILE` | 4 |

## <a name="semovieformatconstants"></a>`seMovieFormatConstants`

_Type library:_ `SolidEdgeFramework`  
_Membros:_ 2

| Constante | Valor |
|-----------|-------|
| `seMovieFormatAVI` | 0 |
| `seMovieFormatWMV` | 1 |

## <a name="semoviestandardresolutionconstants"></a>`seMovieStandardResolutionConstants`

_Type library:_ `SolidEdgeFramework`  
_Membros:_ 5

| Constante | Valor |
|-----------|-------|
| `seMovieStandardResolutionNTSC` | 0 |
| `seMovieStandardResolutionPAL` | 1 |
| `seMovieStandardResolutionHD` | 2 |
| `seMovieStandardResolutionFullHD` | 3 |
| `seMovieStandardResolutionCurrentView` | 4 |

## <a name="sesharpenlevelconstants"></a>`seSharpenLevelConstants`

_Type library:_ `SolidEdgeFramework`  
_Membros:_ 9

| Constante | Valor |
|-----------|-------|
| `seSharpenDefault` | 0 |
| `seSharpenCoarse` | 1 |
| `seSharpenNormal` | 2 |
| `seSharpenFine` | 3 |
| `seSharpenExtraFine` | 4 |
| `seSharpenSuperFine` | 5 |
| `seSharpenIncrement` | 6 |
| `seSharpenDecrement` | 7 |
| `seResharpen` | 8 |

## <a name="sesteeringwheelconstants"></a>`seSteeringWheelConstants`

_Type library:_ `SolidEdgeFramework`  
_Membros:_ 3

| Constante | Valor |
|-----------|-------|
| `seSteeringWheelConstantsXAxis` | 1 |
| `seSteeringWheelConstantsYAxis` | 2 |
| `seSteeringWheelConstantsZAxis` | 3 |

## <a name="sestyletypeconstants"></a>`seStyleTypeConstants`

_Type library:_ `SolidEdgeFramework`  
_Membros:_ 7

| Constante | Valor |
|-----------|-------|
| `igDimensionStyle` | 0 |
| `igDrawingViewStyle` | 1 |
| `igFillStyle` | 2 |
| `igHatchStyle` | 3 |
| `igLineStyle` | 4 |
| `igTableStyle` | 5 |
| `igTextStyle` | 6 |

## <a name="seunitstypeconstants"></a>`seUnitsTypeConstants`

_Type library:_ `SolidEdgeFramework`  
_Membros:_ 2

| Constante | Valor |
|-----------|-------|
| `seUnitsType_DataBase` | -730794371 |
| `seUnitsType_Document` | 1886781498 |

## <a name="sevariabletypeconstants"></a>`seVariableTypeConstants`

_Type library:_ `SolidEdgeFramework`  
_Membros:_ 4

| Constante | Valor |
|-----------|-------|
| `seVariableType_Dimension` | 1661573600 |
| `seVariableType_UserDefined` | 1560616706 |
| `seVariableType_Simulation` | 215773802 |
| `seVariableType_Text` | -170730141 |
