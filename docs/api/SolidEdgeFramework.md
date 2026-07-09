# Type Library `SolidEdgeFramework`

- **Tipos documentados:** 408 (declarados na type library: 461)
- **Origem:** dump `SE_API_dump_223.00.13.05.txt` gerado por `ComDiagnostics.DumpTypeLibraries`
- **Nota:** o contador da type library pode incluir tipos internos/aliases não listados no dump.

## Sumário por categoria

- **DISPATCH**: 124 tipo(s)
- **ENUM**: 110 tipo(s)
- **INTERFACE**: 172 tipo(s)
- **RECORD**: 2 tipo(s)

## DISPATCH

| Tipo | Membros |
|------|---------|
| [SelectSet](#selectset) | 21 |
| [Application](#application) | 180 |
| [Documents](#documents) | 20 |
| [TemplateManager](#templatemanager) | 6 |
| [Environments](#environments) | 8 |
| [Environment](#environment) | 16 |
| [CommandBars](#commandbars) | 15 |
| [CommandBar](#commandbar) | 30 |
| [CommandBarControls](#commandbarcontrols) | 9 |
| [CommandBarControl](#commandbarcontrol) | 45 |
| [Accelerators](#accelerators) | 7 |
| [Accelerator](#accelerator) | 13 |
| [KeyBinding](#keybinding) | 8 |
| [CommandCategories](#commandcategories) | 7 |
| [CommandCategory](#commandcategory) | 7 |
| [CommandInfo](#commandinfo) | 10 |
| [Windows](#windows) | 8 |
| [DISEApplicationEvents](#diseapplicationevents) | 16 |
| [DISEApplicationWindowEvents](#diseapplicationwindowevents) | 1 |
| [DISEFileUIEvents](#disefileuievents) | 6 |
| [DISEBeforeFileSaveAsEvents](#disebeforefilesaveasevents) | 1 |
| [DISECommand](#disecommand) | 12 |
| [DISEMouse](#disemouse) | 51 |
| [DISEMouseEvents](#disemouseevents) | 6 |
| [DISECommandWindowEvents](#disecommandwindowevents) | 1 |
| [DISECommandEvents](#disecommandevents) | 7 |
| [AddIns](#addins) | 8 |
| [AddIn](#addin) | 20 |
| [DISEAddInEvents](#diseaddinevents) | 3 |
| [CommandBarButton](#commandbarbutton) | 50 |
| [DISECommandBarButtonEvents](#disecommandbarbuttonevents) | 3 |
| [Insight](#insight) | 60 |
| [DISEFeatureSelectedFromPFEvents](#disefeatureselectedfrompfevents) | 4 |
| [MatTable](#mattable) | 62 |
| [SolidEdgeTCE](#solidedgetce) | 104 |
| [SolidEdgeInsightXT](#solidedgeinsightxt) | 53 |
| [Customization](#customization) | 10 |
| [RibbonBarThemes](#ribbonbarthemes) | 12 |
| [RibbonBarTheme](#ribbonbartheme) | 13 |
| [RibbonBars](#ribbonbars) | 8 |
| [RibbonBar](#ribbonbar) | 7 |
| [RibbonBarTabs](#ribbonbartabs) | 10 |
| [RibbonBarTab](#ribbonbartab) | 12 |
| [RibbonBarGroups](#ribbonbargroups) | 10 |
| [RibbonBarGroup](#ribbonbargroup) | 11 |
| [RibbonBarControls](#ribbonbarcontrols) | 11 |
| [RibbonBarControl](#ribbonbarcontrol) | 16 |
| [RadialMenu](#radialmenu) | 18 |
| [SwitchWindowCust](#switchwindowcust) | 14 |
| [DynamicVisualization](#dynamicvisualization) | 4 |
| [View](#view) | 121 |
| [Window](#window) | 42 |
| [DISEViewEvents](#diseviewevents) | 3 |
| [DISEhDCDisplayEvents](#disehdcdisplayevents) | 4 |
| [DISEAnimationEvents](#diseanimationevents) | 4 |
| [NamedView](#namedview) | 11 |
| [UnitOfMeasure](#unitofmeasure) | 8 |
| [CommandBarPopup](#commandbarpopup) | 47 |
| [DISEBendTableEvents](#disebendtableevents) | 4 |
| [DISEAssemblyChangeEvents](#diseassemblychangeevents) | 5 |
| [DISEAssemblyConfigurationChangeEvents](#diseassemblyconfigurationchangeevents) | 5 |
| [DISEAssemblyRecomputeEvents](#diseassemblyrecomputeevents) | 8 |
| [DISEDocumentEvents](#disedocumentevents) | 4 |
| [DISEAssemblyPhysicalPropertiesChangeEvents](#diseassemblyphysicalpropertieschangeevents) | 5 |
| [DISEAddInEventsEx](#diseaddineventsex) | 4 |
| [DISEAddInEventsEx2](#diseaddineventsex2) | 4 |
| [DISEAssemblyFamilyEvents](#diseassemblyfamilyevents) | 9 |
| [DISEAssemblyFamilyEvents2](#diseassemblyfamilyevents2) | 11 |
| [DISESketchRecomputeEvents](#disesketchrecomputeevents) | 7 |
| [Layer](#layer) | 32 |
| [LinearStyle](#linearstyle) | 21 |
| [FillStyle](#fillstyle) | 27 |
| [HatchPatternStyle](#hatchpatternstyle) | 55 |
| [DashStyle](#dashstyle) | 16 |
| [FaceStyle](#facestyle) | 185 |
| [TextStyle](#textstyle) | 28 |
| [TextCharStyle](#textcharstyle) | 28 |
| [Symbol2d](#symbol2d) | 69 |
| [ViewStyle](#viewstyle) | 108 |
| [Properties](#properties) | 12 |
| [Property](#property) | 9 |
| [AttributeSet](#attributeset) | 9 |
| [Attribute](#attribute) | 7 |
| [QueryObjects](#queryobjects) | 7 |
| [HighlightSet](#highlightset) | 15 |
| [NamedViews](#namedviews) | 11 |
| [UnitsOfMeasure](#unitsofmeasure) | 10 |
| [variable](#variable) | 62 |
| [VariableList](#variablelist) | 8 |
| [Variables](#variables) | 20 |
| [InterpartLink](#interpartlink) | 8 |
| [InterpartLinks](#interpartlinks) | 7 |
| [Sensor](#sensor) | 29 |
| [Sensors](#sensors) | 11 |
| [SheetMetalSensors](#sheetmetalsensors) | 12 |
| [Layers](#layers) | 10 |
| [DashStyles](#dashstyles) | 10 |
| [LinearStyles](#linearstyles) | 12 |
| [FillStyles](#fillstyles) | 12 |
| [HatchPatternStyles](#hatchpatternstyles) | 10 |
| [FaceStyles](#facestyles) | 11 |
| [TextStyles](#textstyles) | 12 |
| [TextCharStyles](#textcharstyles) | 10 |
| [Symbols](#symbols) | 10 |
| [ViewStyles](#viewstyles) | 12 |
| [Reference](#reference) | 14 |
| [RoutingSlip](#routingslip) | 29 |
| [SymbolProperties](#symbolproperties) | 8 |
| [PropertySets](#propertysets) | 9 |
| [PropertyEx](#propertyex) | 10 |
| [SummaryInfo](#summaryinfo) | 41 |
| [AttributeQuery](#attributequery) | 6 |
| [HighlightSets](#highlightsets) | 9 |
| [SEGenericCollection](#segenericcollection) | 8 |
| [AttributeSets](#attributesets) | 8 |
| [SolidEdgeDocument](#solidedgedocument) | 62 |
| [PredefineRelationProducer](#predefinerelationproducer) | 28 |
| [CPDInitializerInsightXT](#cpdinitializerinsightxt) | 9 |
| [CPDInitializer](#cpdinitializer) | 9 |
| [SectionView](#sectionview) | 33 |
| [SectionViews](#sectionviews) | 11 |
| [InterDocumentUpdate](#interdocumentupdate) | 8 |
| [SteeringWheel](#steeringwheel) | 8 |
| [CPDInitializerBiDM](#cpdinitializerbidm) | 7 |

### <a name="selectset"></a>`SelectSet`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `QueryInterface` | riid: GUID*, [out] ppvObj: void** | void |
| `AddRef` | — | uint |
| `Release` | — | uint |
| `get Application` | — | Application* |
| `get Parent` | — | IDispatch |
| `get Count` | — | int |
| `Add` | Dispatch: IDispatch | void |
| `Remove` | Index: VARIANT | void |
| `RemoveAll` | — | void |
| `Item` | Index: VARIANT | IDispatch |
| `get _NewEnum` | — | IUnknown |
| `Copy` | — | void |
| `Cut` | — | void |
| `Delete` | — | void |
| `AddAll` | — | void |
| `get Type` | — | ObjectType |
| `CopyProfile` | — | void |
| `CutProfile` | — | void |
| `SuspendDisplay` | — | void |
| `ResumeDisplay` | — | void |
| `RefreshDisplay` | — | void |

### <a name="application"></a>`Application`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `QueryInterface` | riid: GUID*, [out] ppvObj: void** | void |
| `AddRef` | — | uint |
| `Release` | — | uint |
| `Activate` | — | void |
| `get ActiveDocument` | — | IDispatch |
| `get ActiveEnvironment` | — | BSTR |
| `get ActivePrinter` | — | BSTR |
| `get ActiveSelectSet` | — | SelectSet* |
| `get ActiveStatusBarPart` | — | int |
| `put ActiveStatusBarPart` | p0: int | void |
| `get ActiveWindow` | — | IDispatch |
| `get Application` | — | Application* |
| `get Caption` | — | BSTR |
| `put Caption` | p0: BSTR | void |
| `get DefaultFilePath` | — | BSTR |
| `put DefaultFilePath` | p0: BSTR | void |
| `put DelayCompute` | p0: bool | void |
| `get DelayCompute` | — | bool |
| `get DisplayAlerts` | — | bool |
| `put DisplayAlerts` | p0: bool | void |
| `get DisplayFullScreen` | — | bool |
| `put DisplayFullScreen` | p0: bool | void |
| `get DisplayRecentFiles` | — | bool |
| `put DisplayRecentFiles` | p0: bool | void |
| `get DisplayRecentFilesCount` | — | int |
| `put DisplayRecentFilesCount` | p0: int | void |
| `get Documents` | — | Documents* |
| `get Environments` | — | Environments* |
| `GetOpenFileName` | [out] LinksUpdate: LinksUpdateOption*, [out] AltLinkPath: BSTR*, [out] DocAccess: DocumentAccess*, [out] OptNotify: NotifyOption*, [out] DocRelationAutoServer: IDispatch*, [opt]FileFilter: VARIANT, [opt]FilterIndex: VARIANT, [opt]Title: VARIANT, [opt]IgnoreWarnings: VARIANT | VARIANT |
| `SearchDocuments` | bUseSearchScope: bool, bstrFolders: BSTR, bIncludeSubFolders: bool, [out] ListOfFoundDocuments: VARIANT*, [out] iNumDocsFound: int*, [opt]varFileFilterOrText: VARIANT, [opt]PropertyList: VARIANT, [opt]ConditionList: VARIANT, [opt]PropertyValueList: VARIANT, [opt]varNumProps: VARIANT, [opt]varCheckModified: VARIANT, [opt]varNumberOfDays: VARIANT, [opt][out] ListOfTitles: VARIANT*, [opt][out] ListOfSubjects: VARIANT*, [opt][out] ListOfModifiedDates: VARIANT* | int |
| `GetSaveAsFileName` | [out] LinkSaveOption: int*, [out] SelectedFilter: int*, [opt]InitialFilename: VARIANT, [opt]FileFilter: VARIANT, [opt]FilterIndex: VARIANT, [opt]Title: VARIANT, [opt]IsTemplate: VARIANT | VARIANT |
| `FindFile` | — | VARIANT |
| `GetDirectoryName` | — | VARIANT |
| `get Height` | — | int |
| `put Height` | p0: int | void |
| `get hWnd` | — | int |
| `get Interactive` | — | bool |
| `put Interactive` | p0: bool | void |
| `get Left` | — | int |
| `put Left` | p0: int | void |
| `MailLogoff` | — | void |
| `MailLogon` | [opt]Name: VARIANT, [opt]Password: VARIANT, [opt]DownloadNewMail: VARIANT | void |
| `get MailSession` | — | int |
| `get Name` | — | BSTR |
| `get Parent` | — | Application* |
| `Quit` | — | void |
| `get ScreenUpdating` | — | bool |
| `put ScreenUpdating` | p0: bool | void |
| `get StatusBar` | — | BSTR |
| `put StatusBar` | p0: BSTR | void |
| `get StatusBarDelayUpdate` | — | bool |
| `put StatusBarDelayUpdate` | p0: bool | void |
| `get StatusBarHeight` | — | int |
| `get StatusBarPartCount` | — | int |
| `put StatusBarPartCount` | p0: int | void |
| `get StatusBarPartWidth` | — | int |
| `put StatusBarPartWidth` | p0: int | void |
| `get StatusBarVisible` | — | bool |
| `put StatusBarVisible` | p0: bool | void |
| `get Top` | — | int |
| `put Top` | p0: int | void |
| `get UsableHeight` | — | int |
| `get UsableWidth` | — | int |
| `get UserName` | — | BSTR |
| `put UserName` | p0: BSTR | void |
| `get Value` | — | BSTR |
| `get Version` | — | BSTR |
| `get Visible` | — | bool |
| `put Visible` | p0: bool | void |
| `get Width` | — | int |
| `put Width` | p0: int | void |
| `get Windows` | — | Windows* |
| `get WindowState` | — | int |
| `put WindowState` | p0: int | void |
| `get ApplicationEvents` | — | ApplicationEvents* |
| `get ApplicationWindowEvents` | — | ApplicationWindowEvents* |
| `get ActiveDocumentType` | — | DocumentTypeConstants |
| `get FileUIEvents` | — | FileUIEvents* |
| `get BeforeFileSaveAsEvents` | — | BeforeFileSaveAsEvents* |
| `StartCommand` | CommandID: SolidEdgeCommandConstants | void |
| `CommandEnabled` | CommandID: int, strEnvironment: BSTR, [out] bLicensed: bool*, [out] bUnknownCmd: bool* | bool |
| `CreateCommand` | CmdFlags: int | Command* |
| `ReplaceReference` | [opt]FromReference: VARIANT, [opt]ToReference: VARIANT, [opt]Scope: VARIANT, [opt]Recursive: VARIANT | void |
| `RunMacro` | Filename: BSTR | void |
| `get AddIns` | — | AddIns* |
| `get EnableStereo` | — | bool |
| `put EnableStereo` | p0: bool | void |
| `get EdgeBarVisible` | — | bool |
| `put EdgeBarVisible` | p0: bool | void |
| `get FeatureLibraryEvents` | — | FeatureLibraryEvents* |
| `GetGlobalParameter` | Parameter: ApplicationGlobalConstants, [in,out] Value: VARIANT* | void |
| `SetGlobalParameter` | Parameter: ApplicationGlobalConstants, Value: VARIANT | void |
| `get ActiveObject` | Type: SeObjectType | IDispatch |
| `get Insight` | — | Insight* |
| `get ApplicationV8AfterDocumentOpenEvent` | — | ApplicationV8DocumentOpenEvent* |
| `SetOLERequestPendingTimeout` | [opt]SetOLERequestPendingTimeout: VARIANT | void |
| `SetOLEServerBusyTimeout` | [opt]SetOLEServerBusyTimeout: VARIANT | void |
| `get FeatureSelectedFromPFEvents` | — | FeatureSelectedFromPFEvents* |
| `CreateSEDocumentFromTDMAuto` | bstrHostName: BSTR, bstrServerName: BSTR, bstrFolderLocation: BSTR, bstrProject: BSTR, bstrLibrary: BSTR, bstrItemGUID: BSTR, bstrVersionGUID: BSTR, bstrVersionNumber: BSTR, SEDocType: DocumentTypeConstants, bstrAssemblyTemplate: BSTR, bstrPartTemplate: BSTR | void |
| `CreateSEDraftDocFromDXFAuto` | bstrDxfFileName: BSTR, bstrDraftFileLocation: BSTR, bstrDraftTemplateFile: BSTR, bstrclsidDoc: BSTR | void |
| `CreateSEDocumentFromForeignFile` | bstrForeignFilePath: BSTR, bstrSEFileLocation: BSTR, bstrTemplatePath: BSTR, bstrClsid: BSTR, MigrationType: BulkMigrationTypeConstants | void |
| `GetTemplateFileName` | [out] DocType: DocumentTypeConstants*, [opt]FileFilter: VARIANT | BSTR |
| `GetDefaultTemplatePath` | DocType: DocumentTypeConstants | BSTR |
| `SetDefaultTemplatePath` | DocType: DocumentTypeConstants, TemplatePath: BSTR | void |
| `DoIdle` | — | void |
| `GetMaterialTable` | — | MatTable* |
| `get NewFileUIEvents` | — | NewFileUIEvents* |
| `SEAdminUpdate` | — | void |
| `get ShortcutMenuEvents` | — | ShortcutMenuEvents* |
| `get ApprenticeMode` | — | bool |
| `put ApprenticeMode` | p0: bool | void |
| `get ShowStartupScreen` | — | bool |
| `put ShowStartupScreen` | p0: bool | void |
| `get SolidEdgeTCE` | — | SolidEdgeTCE* |
| `get SolidEdgeInsightXT` | — | SolidEdgeInsightXT* |
| `get IsIdling` | MilliSec: int | bool |
| `get ResolveLink` | — | bool |
| `put ResolveLink` | p0: bool | void |
| `DisableEventsForGivenAddIn` | bstrClsid: BSTR | void |
| `SetAddInInterfaces` | bstrClsid: BSTR, pSaUnknownPtrs: SAFEARRAY(IUnknown)* | void |
| `EnableEventsForGivenAddIn` | bstrClsid: BSTR | void |
| `ShowCommand` | nCmdID: int, Highlight: bool | void |
| `get ProcessID` | — | int |
| `get SEECEvents` | — | SEECEvents* |
| `get SESPEvents` | — | SESPEvents* |
| `get BiDMEvents` | — | BiDMEvents* |
| `WriteDocumentFormulaIntoXML` | outputXMLPath: BSTR, knownResXMLPath: BSTR, [opt]bDeepTree: bool | void |
| `SetBuiltInATPRunningFlagAndATPID` | bRunningFlag: bool, strATPID: BSTR | void |
| `SetValuesForBIDMCPD` | pvarListOfValues: VARIANT* | void |
| `SetMessageForBIDMCPD` | pvarListOfMessages: VARIANT* | void |
| `SetBIDMATPInfo` | bstrATPClassName: BSTR, bstrATPName: BSTR, ATPId: int | void |
| `GetCountOfOpenModelsInFemap` | — | int |
| `get Customization` | — | Customization* |
| `GetDraftPrintUtility` | — | IDispatch |
| `ArrangeWindows` | Style: ArrangeWindowsStyles | void |
| `GetOpenFileNameWithOptions` | dwFlagForOpen: uint, [out] LinksUpdate: LinksUpdateOption*, [out] AltLinkPath: BSTR*, [out] DocAccess: DocumentAccess*, [out] OptNotify: NotifyOption*, [out] DocRelationAutoServer: IDispatch*, [opt]FileFilter: VARIANT, [opt]FilterIndex: VARIANT, [opt]Title: VARIANT, [opt]IgnoreWarnings: VARIANT | VARIANT |
| `SEGetFileVersionInfo` | Filename: BSTR, [out] DocType: DocumentTypeConstants*, [out] CreatedVersion: BSTR*, [out] LastSavedVersion: BSTR*, [out] GeometricVersion: uint* | void |
| `GenerateMasterImportListForDataPrep` | psalistOfFilesFolders: SAFEARRAY(VARIANT)*, IncludeSubFolders: bool, FileTypes: uint, TimeStamp: BSTR, WorkingFolderLocation: BSTR, [out] OrderedCSVFilePath: BSTR*, [out] UnOrderedCSVFilePath: BSTR*, [out] BrokenLinkXMLFilePath: BSTR*, [out] iNumberOfBrokenLinks: int*, [out] LinkReportFilePath: BSTR*, [out] ErrorMsg: BSTR*, [out] ErrCode: GenerateMasterImportListError* | void |
| `FindWhereUsedDocuments` | DocumentPathName: VARIANT, psalistOfDirectories: SAFEARRAY(VARIANT)*, IncludeSubFolders: bool, psaFilterList: SAFEARRAY(VARIANT)* | VARIANT |
| `QuerySystemInformation` | Search: BSTR | VARIANT |
| `DisableBuilInDataMgmt` | bDisableBuiltInDM: bool | void |
| `get RegistryPath` | — | BSTR |
| `get AppDataFolder` | — | BSTR |
| `GetRevisionLinkInfo` | bstrFilePath: BSTR, [out] pVarRevisionRoot: VARIANT*, [out] pVarRevisedFrom: VARIANT* | void |
| `GetRevisionsHistory` | PathName: BSTR, psaScope: SAFEARRAY(VARIANT)*, [out] psaRevHistoryFileNameList: VARIANT*, [out] psaRevHistoryRevisionFromList: VARIANT* | void |
| `OpenDraft` | — | void |
| `GetLatestRevision` | PathName: BSTR, psaScope: SAFEARRAY(VARIANT)*, [out] bLatestRevPath: BSTR*, [out] bLatestReleasedRevPath: BSTR* | void |
| `GetTopLevelAssemblyFileNames` | FileNames: SAFEARRAY(BSTR)*, [out] TopLevelAssemblyFileNames: SAFEARRAY(BSTR)* | void |
| `FindSEDocumentsContainingText` | text_to_search: BSTR, psaScope: SAFEARRAY(VARIANT)*, file_types: BSTR, bIncludeSubFolders: bool, [out] FilesFoundInSearch: SAFEARRAY(BSTR)* | void |
| `ResetConfigFile` | eResetType: ConfigResetType, eConfigFileType: ConfigForForeignFileType, eTranslationMode: FileTranslationMode, [opt]GroupName: BSTR, [opt]pFile: VARIANT*, [opt]pTemplateName: VARIANT* | void |
| `GetNextDocumentNumbers` | countOfFiles: int, [out] pVarPrefix: VARIANT*, [out] pVarDocNumbs: VARIANT* | int |
| `Get_Set_UseBiDM_SEOption` | bGet: bool, [out] iValue: bool* | void |
| `Get_Set_FileNamingRule` | bGet: bool, [out] bValue: bool* | void |
| `GetDocNameFormulaForFile` | bFilename: BSTR | BSTR |
| `BiDM_RegisterCustomProps` | bProcessCustomPropsFromPropSeed: bool, bProcessCustomPropsFromTemplates: bool | void |
| `PerformSolidEdgeWorkflow` | bstrFilePath: BSTR, [in,out] pSEWorkflowInfo: SolidEdgeWorkflowInfo* | void |
| `GetSolidEdgeWorkflowInformation` | bstrFilePath: BSTR, [out] pSEWorkflowQueryInfo: SolidEdgeWorkflowQueryInfo* | void |
| `SuspendMRU` | — | void |
| `ResumeMRU` | — | void |
| `ClearMRU` | — | void |
| `AbortCommand` | AbortAllCommands: bool | void |
| `Publish3DPDF` | bstrInputFileOrFolderPath: BSTR, bstr3DPDFTemplateFile: BSTR, [opt]bIncludeSubFolders: bool, [opt]bstrOutputFolderPath: BSTR, [opt]bstr3DPDFFileName: BSTR, [opt]bOpenPDFAfterPublish: bool, [opt]bPublishHTML: bool, [opt]bAddNextPrevButtons: bool, [opt]bAddFileCustomPropsToPDF: bool, [opt]bSelectAllPMIModelViewsForPDF: bool, [opt]bstrDefaultModelView: BSTR, [opt]bGenAndAttachSTEPAP242: bool, [opt]bGenAndAttachJT: bool, [opt]ListOfAttachments: VARIANT | bool |
| `ConvertByFilePath` | InputFileOrFolderPath: BSTR, OutputFileOrFolderPath: BSTR | bool |
| `get CommandPredictionLearningMode` | — | bool |
| `put CommandPredictionLearningMode` | p0: bool | void |
| `get SoldToID` | — | BSTR |
| `GetListOfTopLevelAssembliesFromFolder` | FolderPath: BSTR, [out] TopAssembliesList: SAFEARRAY(BSTR)* | void |
| `get LicenseType` | — | BSTR |
| `GenerateSourceImportListForDataPrep` | psalistOfFilesFolders: SAFEARRAY(VARIANT)*, IncludeSubFolders: bool, FileTypes: uint, TimeStamp: BSTR, WorkingFolderLocation: BSTR, [out] OrderedCSVFilePath: BSTR*, [out] UnOrderedCSVFilePath: BSTR*, [out] BrokenLinkXMLFilePath: BSTR*, [out] iNumberOfBrokenLinks: int*, [out] LinkReportFilePath: BSTR*, [out] ErrorMsg: BSTR*, [out] ErrCode: GenerateSourceImportListError* | void |
| `get ActiveFramehWnd` | — | int |
| `get DynamicVisualization` | — | DynamicVisualization* |
| `get LicenseHandle` | — | vt20 |
| `OpenNoteLibrary` | — | void |
| `CloseNoteLibrary` | — | void |
| `GetSavedNoteList` | [out] saSavedNote: SAFEARRAY(BSTR)* | void |
| `GetSavedNote` | bstrNoteName: BSTR | IUnknown |
| `AddNote` | bstrNoteName: BSTR, bstrText: BSTR, bNoteOverWrite: bool | void |
| `Publish3DPDFEx` | bstrInputFileOrFolderPath: BSTR, bstr3DPDFTemplateFile: BSTR, [opt]bIncludeSubFolders: bool, [opt]bstrOutputFolderPath: BSTR, [opt]bstr3DPDFFileName: BSTR, [opt]bOpenPDFAfterPublish: bool, [opt]bPublishHTML: bool, [opt]bAddNextPrevButtons: bool, [opt]bAddFileCustomPropsToPDF: bool, [opt]bSelectAllPMIModelViewsForPDF: bool, [opt]bstrDefaultModelView: BSTR, [opt]bGenAndAttachSTEPAP242: bool, [opt]bGenAndAttachJT: bool, [opt]ListOfAttachments: VARIANT, [opt]bSelectAllNamedViewsForPDF: bool, [opt]ListOfNamedViews: VARIANT, [opt]ListOfPMIModelViews: VARIANT | bool |
| `GetActiveCommand` | — | int |
| `get OpenNonSolidEdgeFileUIEvents` | — | OpenNonSolidEdgeFileUIEvents* |

### <a name="documents"></a>`Documents`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `QueryInterface` | riid: GUID*, [out] ppvObj: void** | void |
| `AddRef` | — | uint |
| `Release` | — | uint |
| `get Application` | — | Application* |
| `Close` | — | void |
| `get Count` | — | int |
| `get Parent` | — | Application* |
| `Add` | [opt]ProgID: VARIANT, [opt]TemplateDoc: VARIANT | IDispatch |
| `Item` | Index: VARIANT | IDispatch |
| `Open` | Filename: BSTR, [opt]DocRelationAutoServer: VARIANT, [opt]AltPath: VARIANT, [opt]RecognizeFeaturesIfPartTemplate: VARIANT, [opt]RevisionRuleOption: VARIANT, [opt]StopFileOpenIfRevisionRuleNotApplicable: VARIANT | IDispatch |
| `get _NewEnum` | — | IUnknown |
| `OpenWithTemplate` | Filename: BSTR, Template: BSTR, [opt]RecognizeFeaturesIfPartTemplate: VARIANT | IDispatch |
| `get TemplatePath` | — | BSTR |
| `get AutoCadConfigFile` | — | BSTR |
| `put AutoCadConfigFile` | p0: BSTR | void |
| `SetForeignFileConfigValue` | DocumentProgID: BSTR, Filename: BSTR, SectionName: BSTR, Name: BSTR, Value: BSTR | void |
| `GetForeignFileConfigValue` | DocumentProgID: BSTR, Filename: BSTR, SectionName: BSTR, Name: BSTR | BSTR |
| `CloseDocument` | Filename: BSTR, [opt]SaveChanges: VARIANT, [opt]SaveAsFileName: VARIANT, [opt]RouteWorkbook: VARIANT, [opt]DoIdle: VARIANT | void |
| `get TemplateManager` | — | TemplateManager* |
| `OpenWithFileOpenDialog` | [opt]Filename: VARIANT, [opt]DialogTitle: VARIANT, [opt]Flags: VARIANT | IDispatch |

### <a name="templatemanager"></a>`TemplateManager`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `QueryInterface` | riid: GUID*, [out] ppvObj: void** | void |
| `AddRef` | — | uint |
| `Release` | — | uint |
| `get Application` | — | Application* |
| `get Parent` | — | IDispatch |
| `GetActiveTemplates` | [out] bstrActiveListPath: BSTR*, [out] eActiveListType: TemplatesListType*, [out] astrActiveTemplates: SAFEARRAY(BSTR)* | void |

### <a name="environments"></a>`Environments`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `QueryInterface` | riid: GUID*, [out] ppvObj: void** | void |
| `AddRef` | — | uint |
| `Release` | — | uint |
| `get Application` | — | Application* |
| `get Count` | — | int |
| `get Parent` | — | Application* |
| `Item` | Index: VARIANT | Environment* |
| `get _NewEnum` | — | IUnknown |

### <a name="environment"></a>`Environment`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `QueryInterface` | riid: GUID*, [out] ppvObj: void** | void |
| `AddRef` | — | uint |
| `Release` | — | uint |
| `get Application` | — | Application* |
| `get Caption` | — | BSTR |
| `put Caption` | p0: BSTR | void |
| `get Index` | — | int |
| `get Name` | — | BSTR |
| `get Parent` | — | Environments* |
| `get CommandBars` | — | CommandBars* |
| `get Accelerators` | — | Accelerators* |
| `get SubTypeName` | — | BSTR |
| `get CommandCategories` | — | CommandCategories* |
| `get CATID` | — | BSTR |
| `get CustomizeDisplayName` | — | BSTR |
| `get CommandInfo` | CommandID: int | CommandInfo* |

### <a name="commandbars"></a>`CommandBars`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `QueryInterface` | riid: GUID*, [out] ppvObj: void** | void |
| `AddRef` | — | uint |
| `Release` | — | uint |
| `get ActiveMenuBar` | — | CommandBar* |
| `get Application` | — | IDispatch |
| `get Count` | — | int |
| `get DisplayTooltips` | — | bool |
| `put DisplayTooltips` | p0: bool | void |
| `get LargeButtons` | — | bool |
| `put LargeButtons` | p0: bool | void |
| `get Parent` | — | IDispatch |
| `Add` | [opt]Name: VARIANT, [opt]Position: VARIANT, [opt]MenuBar: VARIANT, [opt]Temporary: VARIANT | CommandBar* |
| `FindControl` | [opt]Type: VARIANT, [opt]Id: VARIANT, [opt]Tag: VARIANT, [opt]Visible: VARIANT | CommandBarControl* |
| `Item` | Index: VARIANT | CommandBar* |
| `get _NewEnum` | — | IUnknown |

### <a name="commandbar"></a>`CommandBar`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `QueryInterface` | riid: GUID*, [out] ppvObj: void** | void |
| `AddRef` | — | uint |
| `Release` | — | uint |
| `get Application` | — | IDispatch |
| `get BuiltIn` | — | bool |
| `get Controls` | — | CommandBarControls* |
| `get Enabled` | — | bool |
| `put Enabled` | p0: bool | void |
| `get Height` | — | int |
| `put Height` | p0: int | void |
| `get Index` | — | int |
| `get Left` | — | int |
| `put Left` | p0: int | void |
| `get Name` | — | BSTR |
| `get NameLocal` | — | BSTR |
| `put NameLocal` | p0: BSTR | void |
| `get Parent` | — | Environment* |
| `get Position` | — | SeBarPosition |
| `put Position` | p0: SeBarPosition | void |
| `get Top` | — | int |
| `put Top` | p0: int | void |
| `get Type` | — | SeBarType |
| `get Visible` | — | bool |
| `put Visible` | p0: bool | void |
| `get Width` | — | int |
| `put Width` | p0: int | void |
| `Delete` | — | void |
| `FindControl` | [opt]Type: VARIANT, [opt]Id: VARIANT, [opt]Tag: VARIANT, [opt]Visible: VARIANT, [opt]Recursive: VARIANT | CommandBarControl* |
| `Reset` | — | void |
| `ShowPopup` | [opt]x: VARIANT, [opt]y: VARIANT | void |

### <a name="commandbarcontrols"></a>`CommandBarControls`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `QueryInterface` | riid: GUID*, [out] ppvObj: void** | void |
| `AddRef` | — | uint |
| `Release` | — | uint |
| `get Application` | — | IDispatch |
| `get Count` | — | int |
| `get Parent` | — | CommandBar* |
| `Add` | [opt]Type: VARIANT, [opt]Id: VARIANT, [opt]Before: VARIANT, [opt]Temporary: VARIANT | CommandBarControl* |
| `Item` | Index: VARIANT | CommandBarControl* |
| `get _NewEnum` | — | IUnknown |

### <a name="commandbarcontrol"></a>`CommandBarControl`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `QueryInterface` | riid: GUID*, [out] ppvObj: void** | void |
| `AddRef` | — | uint |
| `Release` | — | uint |
| `get Application` | — | IDispatch |
| `get BeginGroup` | — | bool |
| `put BeginGroup` | p0: bool | void |
| `get BuiltIn` | — | bool |
| `get BuiltInFace` | — | bool |
| `put BuiltInFace` | p0: bool | void |
| `get Caption` | — | BSTR |
| `put Caption` | p0: BSTR | void |
| `get DescriptionText` | — | BSTR |
| `put DescriptionText` | p0: BSTR | void |
| `get Enabled` | — | bool |
| `put Enabled` | p0: bool | void |
| `get FaceId` | — | int |
| `put FaceId` | p0: int | void |
| `get Height` | — | int |
| `get HelpContextId` | — | int |
| `put HelpContextId` | p0: int | void |
| `get HelpFile` | — | BSTR |
| `put HelpFile` | p0: BSTR | void |
| `get Id` | — | int |
| `get Index` | — | int |
| `get Left` | — | int |
| `get OnAction` | — | BSTR |
| `put OnAction` | p0: BSTR | void |
| `get ParameterText` | — | BSTR |
| `put ParameterText` | p0: BSTR | void |
| `get Parent` | — | CommandBar* |
| `get ShortcutText` | — | BSTR |
| `put ShortcutText` | p0: BSTR | void |
| `get Tag` | — | BSTR |
| `put Tag` | p0: BSTR | void |
| `get TooltipText` | — | BSTR |
| `put TooltipText` | p0: BSTR | void |
| `get Top` | — | int |
| `get Type` | — | SeControlType |
| `get Visible` | — | bool |
| `put Visible` | p0: bool | void |
| `get Width` | — | int |
| `Delete` | [opt]Temporary: VARIANT | void |
| `Execute` | — | void |
| `Help` | — | void |
| `LoadFace` | Face: BSTR | void |

### <a name="accelerators"></a>`Accelerators`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `QueryInterface` | riid: GUID*, [out] ppvObj: void** | void |
| `AddRef` | — | uint |
| `Release` | — | uint |
| `get Count` | — | int |
| `get Parent` | — | IDispatch |
| `Item` | Index: VARIANT | Accelerator* |
| `get _NewEnum` | — | IUnknown |

### <a name="accelerator"></a>`Accelerator`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `QueryInterface` | riid: GUID*, [out] ppvObj: void** | void |
| `AddRef` | — | uint |
| `Release` | — | uint |
| `get Count` | — | int |
| `get Parent` | — | IDispatch |
| `Item` | Index: VARIANT | KeyBinding* |
| `get _NewEnum` | — | IUnknown |
| `get Type` | — | AcceleratorTypeConstants |
| `Reset` | — | void |
| `Remove` | KeyCode: int | void |
| `Add` | CommandID: int, KeyCode: int | KeyBinding* |
| `KeyBinding` | KeyCode: int | KeyBinding* |
| `BuildKeyCode` | KeyModifier: int, Key: int | int |

### <a name="keybinding"></a>`KeyBinding`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `QueryInterface` | riid: GUID*, [out] ppvObj: void** | void |
| `AddRef` | — | uint |
| `Release` | — | uint |
| `get Parent` | — | IDispatch |
| `get CommandID` | — | int |
| `get CommandString` | — | BSTR |
| `get KeyString` | — | BSTR |
| `get KeyCode` | — | int |

### <a name="commandcategories"></a>`CommandCategories`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `QueryInterface` | riid: GUID*, [out] ppvObj: void** | void |
| `AddRef` | — | uint |
| `Release` | — | uint |
| `get Parent` | — | IDispatch |
| `get Count` | — | int |
| `Item` | Index: VARIANT | CommandCategory* |
| `get _NewEnum` | — | IUnknown |

### <a name="commandcategory"></a>`CommandCategory`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `QueryInterface` | riid: GUID*, [out] ppvObj: void** | void |
| `AddRef` | — | uint |
| `Release` | — | uint |
| `get Caption` | — | BSTR |
| `get Count` | — | int |
| `Item` | Index: VARIANT | CommandInfo* |
| `get _NewEnum` | — | IUnknown |

### <a name="commandinfo"></a>`CommandInfo`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `QueryInterface` | riid: GUID*, [out] ppvObj: void** | void |
| `AddRef` | — | uint |
| `Release` | — | uint |
| `get Caption` | — | BSTR |
| `get Id` | — | int |
| `get Tooltip` | — | BSTR |
| `get Description` | — | BSTR |
| `get BuiltIn` | — | bool |
| `get Icon` | — | int |
| `SaveImage` | Filename: BSTR, [opt]Background: VARIANT | void |

### <a name="windows"></a>`Windows`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `QueryInterface` | riid: GUID*, [out] ppvObj: void** | void |
| `AddRef` | — | uint |
| `Release` | — | uint |
| `get Application` | — | Application* |
| `get Count` | — | int |
| `get Parent` | — | IDispatch |
| `Item` | Index: VARIANT | IDispatch |
| `get _NewEnum` | — | IUnknown |

### <a name="diseapplicationevents"></a>`DISEApplicationEvents`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `AfterActiveDocumentChange` | theDocument: IDispatch | void |
| `AfterCommandRun` | theCommandID: int | void |
| `AfterDocumentOpen` | theDocument: IDispatch | void |
| `AfterDocumentPrint` | theDocument: IDispatch, hDC: int, ModelToDC: SAFEARRAY(double)*, Rect: SAFEARRAY(int)* | void |
| `AfterDocumentSave` | theDocument: IDispatch | void |
| `AfterEnvironmentActivate` | theEnvironment: IDispatch | void |
| `AfterNewDocumentOpen` | theDocument: IDispatch | void |
| `AfterNewWindow` | theWindow: IDispatch | void |
| `AfterWindowActivate` | theWindow: IDispatch | void |
| `BeforeCommandRun` | theCommandID: int | void |
| `BeforeDocumentClose` | theDocument: IDispatch | void |
| `BeforeDocumentPrint` | theDocument: IDispatch, hDC: int, ModelToDC: SAFEARRAY(double)*, Rect: SAFEARRAY(int)* | void |
| `BeforeEnvironmentDeactivate` | theEnvironment: IDispatch | void |
| `BeforeWindowDeactivate` | theWindow: IDispatch | void |
| `BeforeQuit` | — | void |
| `BeforeDocumentSave` | theDocument: IDispatch | void |

### <a name="diseapplicationwindowevents"></a>`DISEApplicationWindowEvents`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `WindowProc` | hWnd: int, nMsg: int, wParam: int, lParam: int | void |

### <a name="disefileuievents"></a>`DISEFileUIEvents`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `OnFileOpenUI` | [out] Filename: BSTR*, [out] AppendToTitle: BSTR* | void |
| `OnFileSaveAsUI` | [out] Filename: BSTR*, [out] AppendToTitle: BSTR* | void |
| `OnFileNewUI` | [out] Filename: BSTR*, [out] AppendToTitle: BSTR* | void |
| `OnFileSaveAsImageUI` | [out] Filename: BSTR*, [out] AppendToTitle: BSTR*, [in,out] Width: int*, [in,out] Height: int*, [in,out] ImageQuality: SeImageQualityType* | void |
| `OnPlacePartUI` | [out] Filename: BSTR*, [out] AppendToTitle: BSTR* | void |
| `OnCreateInPlacePartUI` | [out] Filename: BSTR*, [out] AppendToTitle: BSTR*, [out] Template: BSTR* | void |

### <a name="disebeforefilesaveasevents"></a>`DISEBeforeFileSaveAsEvents`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `OnBeforeFileSaveAsUI` | TemplatePath: BSTR | void |

### <a name="disecommand"></a>`DISECommand`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `QueryInterface` | riid: GUID*, [out] ppvObj: void** | void |
| `AddRef` | — | uint |
| `Release` | — | uint |
| `get Mouse` | — | Mouse* |
| `get Window` | — | CommandWindow* |
| `get Done` | — | bool |
| `put Done` | p0: bool | void |
| `get OnEditOwnerChange` | — | int |
| `put OnEditOwnerChange` | p0: int | void |
| `get OnEnvironmentChange` | — | int |
| `put OnEnvironmentChange` | p0: int | void |
| `Start` | — | void |

### <a name="disemouse"></a>`DISEMouse`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `QueryInterface` | riid: GUID*, [out] ppvObj: void** | void |
| `AddRef` | — | uint |
| `Release` | — | uint |
| `put ScaleMode` | p0: int | void |
| `get ScaleMode` | — | int |
| `get EnabledMove` | — | bool |
| `put EnabledMove` | p0: bool | void |
| `get LastEventWindow` | — | IDispatch |
| `get LastUpEventWindow` | — | IDispatch |
| `get LastDownEventWindow` | — | IDispatch |
| `get LastMoveEventWindow` | — | IDispatch |
| `get LastEventShift` | — | short |
| `get LastUpEventShift` | — | short |
| `get LastDownEventShift` | — | short |
| `get LastMoveEventShift` | — | short |
| `get LastEventButton` | — | short |
| `get LastUpEventButton` | — | short |
| `get LastDownEventButton` | — | short |
| `get LastMoveEventButton` | — | short |
| `get LastEventX` | — | double |
| `get LastEventY` | — | double |
| `get LastEventZ` | — | double |
| `get LastUpEventX` | — | double |
| `get LastUpEventY` | — | double |
| `get LastUpEventZ` | — | double |
| `get LastDownEventX` | — | double |
| `get LastDownEventY` | — | double |
| `get LastDownEventZ` | — | double |
| `get LastMoveEventX` | — | double |
| `get LastMoveEventY` | — | double |
| `get LastMoveEventZ` | — | double |
| `get WindowTypes` | — | int |
| `put WindowTypes` | p0: int | void |
| `get LastEventType` | — | int |
| `get EnabledDrag` | — | bool |
| `put EnabledDrag` | p0: bool | void |
| `get LocateMode` | — | int |
| `put LocateMode` | p0: int | void |
| `get DynamicsMode` | — | int |
| `put DynamicsMode` | p0: int | void |
| `get PauseLocate` | — | int |
| `put PauseLocate` | p0: int | void |
| `ClearLocateFilter` | — | void |
| `AddToLocateFilter` | lFilter: int | void |
| `PointOnGraphic` | [out] PointOnGraphicFlag: int*, [out] PointOnGraphic_X: double*, [out] PointOnGraphic_Y: double*, [out] PointOnGraphic_Z: double* | void |
| `get InterDocumentLocate` | — | bool |
| `put InterDocumentLocate` | p0: bool | void |
| `get LocateFrontToBack` | — | bool |
| `put LocateFrontToBack` | p0: bool | void |
| `get PathfinderLocate` | — | bool |
| `put PathfinderLocate` | p0: bool | void |

### <a name="disemouseevents"></a>`DISEMouseEvents`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `MouseDown` | sButton: short, sShift: short, dX: double, dY: double, dZ: double, pWindowDispatch: IDispatch, lKeyPointType: int, pGraphicDispatch: IDispatch | void |
| `MouseUp` | sButton: short, sShift: short, dX: double, dY: double, dZ: double, pWindowDispatch: IDispatch, lKeyPointType: int, pGraphicDispatch: IDispatch | void |
| `MouseMove` | sButton: short, sShift: short, dX: double, dY: double, dZ: double, pWindowDispatch: IDispatch, lKeyPointType: int, pGraphicDispatch: IDispatch | void |
| `MouseClick` | sButton: short, sShift: short, dX: double, dY: double, dZ: double, pWindowDispatch: IDispatch, lKeyPointType: int, pGraphicDispatch: IDispatch | void |
| `MouseDblClick` | sButton: short, sShift: short, dX: double, dY: double, dZ: double, pWindowDispatch: IDispatch, lKeyPointType: int, pGraphicDispatch: IDispatch | void |
| `MouseDrag` | sButton: short, sShift: short, dX: double, dY: double, dZ: double, pWindowDispatch: IDispatch, DragState: short, lKeyPointType: int, pGraphicDispatch: IDispatch | void |

### <a name="disecommandwindowevents"></a>`DISECommandWindowEvents`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `WindowProc` | pUnkDoc: IDispatch, pUnkView: IDispatch, nMsg: int, wParam: int, lParam: int, [out] lResult: int* | void |

### <a name="disecommandevents"></a>`DISECommandEvents`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `Activate` | — | void |
| `Deactivate` | — | void |
| `Terminate` | — | void |
| `Idle` | lCount: int, pbMore: bool* | void |
| `KeyDown` | KeyCode: short*, Shift: short | void |
| `KeyPress` | KeyAscii: short* | void |
| `KeyUp` | KeyCode: short*, Shift: short | void |

### <a name="addins"></a>`AddIns`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `QueryInterface` | riid: GUID*, [out] ppvObj: void** | void |
| `AddRef` | — | uint |
| `Release` | — | uint |
| `get Application` | — | IDispatch |
| `get Count` | — | int |
| `get _NewEnum` | — | IUnknown |
| `Item` | Index: VARIANT | AddIn* |
| `Update` | — | void |

### <a name="addin"></a>`AddIn`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `QueryInterface` | riid: GUID*, [out] ppvObj: void** | void |
| `AddRef` | — | uint |
| `Release` | — | uint |
| `get Application` | — | IDispatch |
| `get AddInEvents` | — | AddInEvents* |
| `get Connect` | — | bool |
| `put Connect` | p0: bool | void |
| `get Description` | — | BSTR |
| `put Description` | p0: BSTR | void |
| `get GUID` | — | BSTR |
| `get GuiVersion` | — | int |
| `put GuiVersion` | p0: int | void |
| `get Object` | — | IDispatch |
| `put Object` | p0: IDispatch | void |
| `get ProgID` | — | BSTR |
| `get Visible` | — | bool |
| `put Visible` | p0: bool | void |
| `SetAddInInfo` | InstanceHandle: int, EnvironmentCatID: BSTR, CategoryName: BSTR, IDColorBitmapMedium: int, IDColorBitmapLarge: int, IDMonochromeBitmapMedium: int, IDMonochromeBitmapLarge: int, NumberOfCommands: int, CommandNames: SAFEARRAY(BSTR)*, [in,out] CommandIDs: SAFEARRAY(int)* | void |
| `AddCommand` | EnvironmentCatID: BSTR, CommandName: BSTR, CommandID: int | int |
| `AddCommandBarButton` | EnvironmentCatID: BSTR, CommandBarName: BSTR, CommandID: int | CommandBarButton* |

### <a name="diseaddinevents"></a>`DISEAddInEvents`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `OnCommand` | nCmdID: int | void |
| `OnCommandHelp` | hFrameWnd: int, uHelpCommand: int, nCmdID: int | void |
| `OnCommandUpdateUI` | nCmdID: int, [in,out] lCmdFlags: int*, [out] MenuItemText: BSTR*, [in,out] nIDBitmap: int* | void |

### <a name="commandbarbutton"></a>`CommandBarButton`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `QueryInterface` | riid: GUID*, [out] ppvObj: void** | void |
| `AddRef` | — | uint |
| `Release` | — | uint |
| `get Application` | — | IDispatch |
| `get BeginGroup` | — | bool |
| `put BeginGroup` | p0: bool | void |
| `get BuiltIn` | — | bool |
| `get BuiltInFace` | — | bool |
| `put BuiltInFace` | p0: bool | void |
| `get Caption` | — | BSTR |
| `put Caption` | p0: BSTR | void |
| `get DescriptionText` | — | BSTR |
| `put DescriptionText` | p0: BSTR | void |
| `get Enabled` | — | bool |
| `put Enabled` | p0: bool | void |
| `get FaceId` | — | int |
| `put FaceId` | p0: int | void |
| `get Height` | — | int |
| `get HelpContextId` | — | int |
| `put HelpContextId` | p0: int | void |
| `get HelpFile` | — | BSTR |
| `put HelpFile` | p0: BSTR | void |
| `get Id` | — | int |
| `get Index` | — | int |
| `get Left` | — | int |
| `get OnAction` | — | BSTR |
| `put OnAction` | p0: BSTR | void |
| `get ParameterText` | — | BSTR |
| `put ParameterText` | p0: BSTR | void |
| `get Parent` | — | CommandBar* |
| `get ShortcutText` | — | BSTR |
| `put ShortcutText` | p0: BSTR | void |
| `get Tag` | — | BSTR |
| `put Tag` | p0: BSTR | void |
| `get TooltipText` | — | BSTR |
| `put TooltipText` | p0: BSTR | void |
| `get Top` | — | int |
| `get Type` | — | SeControlType |
| `get Visible` | — | bool |
| `put Visible` | p0: bool | void |
| `get Width` | — | int |
| `Delete` | [opt]Temporary: VARIANT | void |
| `Execute` | — | void |
| `Help` | — | void |
| `LoadFace` | Face: BSTR | void |
| `get CommandBarButtonEvents` | — | CommandBarButtonEvents* |
| `get State` | — | SeButtonState |
| `put State` | p0: SeButtonState | void |
| `get Style` | — | SeButtonStyle |
| `put Style` | p0: SeButtonStyle | void |

### <a name="disecommandbarbuttonevents"></a>`DISECommandBarButtonEvents`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `Click` | — | void |
| `Help` | hFrameWnd: int, uHelpCommand: int | void |
| `UpdateUI` | — | void |

### <a name="insight"></a>`Insight`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `QueryInterface` | riid: GUID*, [out] ppvObj: void** | void |
| `AddRef` | — | uint |
| `Release` | — | uint |
| `DownloadDocumentFromServer` | DocumentToDownLoadFromServer: BSTR, DocumentAccessMode: DocumentAccess, [out] LocalPath: BSTR*, [opt]GetLatestDocuments: VARIANT, [opt]ProcessIndirectDocuments: VARIANT, [opt]RevisionRuleOption: VARIANT, [opt]StopFileOpenIfRevisionRuleNotApplicable: VARIANT | void |
| `ImportDocumentsToServer` | NumberOfDocumentsFoldersToImport: int, ListOfDocumentsFoldersToImport: VARIANT, ImportLocation: BSTR, TypeOfUpload: UploadType, CheckInOption: CheckInOptions | void |
| `UploadDocumentsToServer` | NumberOfDocumentsToUpload: int, DocumentsToUpload: VARIANT | void |
| `ExportDocumentsFromServer` | NumberOfDocumentsToExport: int, ListOfDocumentsToExport: VARIANT, ExportToLocation: BSTR, SetDocToReadOnly: bool, OverWriteOption: OverWriteFilesOption | void |
| `DeleteDocumentsFromServer` | NumberOfDocumentsToBeDeleted: int, ListOfDocumentsToBeDeleted: VARIANT, [out] NumberOfSuccessfullyDeletedDocuments: int*, [out] SuccessfullyDeletedDocuments: VARIANT* | void |
| `FindWhereUsedOnServer` | NumberOfProperties: int, ListOfProperties: VARIANT, NumberOfSharePointDirectories: int, ListOfSharePointDirectories: VARIANT, NumberOfDocuments: int, ListOfDocumentsForWhereUsed: VARIANT, [out] NumberOfUserFiles: int*, [out] DocumentsUsedByList: VARIANT*, [opt]TypeOfSearch: VARIANT | void |
| `CheckOutDocumentsFromServer` | NumberOfDocumentsToCheckOutFromServer: int, ListOfDocumentsToCheckOutFromServer: VARIANT | void |
| `CheckInDocumentsToServer` | NumberOfDocumentsToCheckInToServer: int, ListOfDocumentsToCheckInFromServer: VARIANT, [opt]FailIfDocumentsOpenInSolidEdge: VARIANT | void |
| `UndoCheckOutDocumentsFromServer` | NumberOfDocumentsToUndoCheckOutFromServer: int, ListOfDocumentsToUndoCheckOutFromServer: VARIANT | void |
| `ShowRevisionsForServerDocument` | DocumentNameToShowRevisions: BSTR, [out] NumberOfRevisions: int*, [out] DocumentNamesOfRevisions: VARIANT* | void |
| `GetRevisedFrom` | RevisedDocumentName: BSTR, [out] RevisedFromDocument: BSTR* | void |
| `SetInsightUserNamePassword` | WorkspaceUrl: BSTR, UserName: BSTR, Password: BSTR, DomainName: BSTR | void |
| `GetLastInsightTransactionMessages` | [out] TransactionString: BSTR*, [out] NumberOfDocuments: int*, [out] ListofDocumentNamesWithPath: VARIANT*, [out] ListofMessages: VARIANT*, [out] ListofSeverityCodes: VARIANT* | void |
| `GetOutOfDateDocuments` | [out] NumberOfOutOfDateDocuments: int*, ListOfOutOfDateDocuments: VARIANT* | void |
| `ClearCache` | — | void |
| `DeleteDocumentsFromCache` | NumberOfDocumentsToBeDeletedFromCache: int, ListOfDocumentsToBeDeletedFromCache: VARIANT, [out] NumberOfNotDeletedDocuments: int*, [out] ListOfNotDeletedDocuments: VARIANT* | void |
| `PutUserNameAndPasswordIntoCache` | WorkspaceUrl: BSTR | void |
| `EnableDeveloperLog` | bCreateFlag: bool | void |
| `SynchronizeDocumentsInCache` | NumberOfDocumentsToBeSynchronizedWithServer: int, ListOfDocumentsInCacheToBeSynchronized: VARIANT | void |
| `SynchronizeAllDocumentsInCache` | — | void |
| `CheckInAllCheckedOutDocumentsInCache` | — | void |
| `GetFilePropertiesFromServer` | NumberOfFilesToBeQueriedForProperties: int, FileUrlsList: VARIANT, NumberOfPropertiesTobeQueried: int, PropertyUrisList: VARIANT, [out] numberOfPropertiesValues: int*, [out] PropertyValueList: VARIANT* | void |
| `MoveDocumentsThroughWorkFlow` | Filename: BSTR, newstatus: DocumentStatus, [opt]NumberOfDraftFiles: VARIANT, [opt]draftFileList: VARIANT, [opt]draftFileStatusList: VARIANT, [opt]NumberOfRevisionFiles: VARIANT, [opt]revisionFileList: VARIANT, [opt]RevisionFileStatusList: VARIANT | void |
| `MoveAllDocumentsThroughWorkFlow` | Filename: BSTR, newstatus: DocumentStatus | void |
| `GetSharePointServerType` | Filename: BSTR, [out] SPServerType: SPServerType*, [opt]bProcessChecks: VARIANT* | void |
| `FileExists` | FileUrl: BSTR, [out] bFileExists: bool* | void |
| `CreateFolder` | numberOfFoldersToCreate: int, varListOfFoldersToCreate: VARIANT | void |
| `DeleteFolder` | NumberOfDocumentsToBeDeleted: int, varlistOfFilesToDelete: VARIANT, [out] NumberOfSuccessfullyDeletedDocuments: int*, [out] listOfFoldersSuccessfullyDeleted: VARIANT* | void |
| `FolderExists` | bstrFolderName: BSTR, [out] bFolderExists: bool* | void |
| `GetDirs` | ParentUrl: BSTR, [out] numberOfSubFoldersFound: int*, [out] ListOfSubFoldersFound: VARIANT* | void |
| `GetFiles` | ParentUrl: BSTR, [out] numberOfFilesFound: int*, [out] ListOfFilesFound: VARIANT*, [opt]FileFilter: VARIANT | void |
| `DoesUserHaveAdminRights` | FileOrFolderUrl: BSTR, UserName: BSTR, [out] bUserHasAdminRights: bool* | void |
| `IsInsightSupported` | [out] bInsightIsSupported: bool* | void |
| `IsFileCheckedOut` | FileUrl: BSTR, [out] bFileIsCheckedOut: bool*, [out] UserName: BSTR* | void |
| `GetCachePath` | numberOfFilesToGetPathFor: int, varListOfFilePaths: VARIANT, [out] numberOfFilesReturned: int*, [out] varListOfFilesContainingCachePaths: VARIANT* | void |
| `GetUserRole` | FileOrFolderUrl: BSTR, UserName: BSTR, [out] UserRole: BSTR* | void |
| `GetDocState` | UrlToGetStateFor: BSTR, [out] docState: VARIANT* | void |
| `CheckSupport` | ServerUrl: BSTR, [out] bSPSIsSupported: bool* | void |
| `GetUserRights` | FileOrFolderUrl: BSTR, [out] UserRights: InsightSPUserRights* | void |
| `GetIndirectFilesTree` | containerFileName: BSTR, [out] pIndirectFilesTree: VARIANT* | void |
| `UsePathAsDefaultFolderMapPath` | WorkspaceUrl: BSTR | void |
| `RemoveAllFilesFromRecycleBin` | bstrDocLibUrl: BSTR | void |
| `RestoreAllFilesFromRecycleBin` | bstrDocLibUrl: BSTR | void |
| `DownloadDocumentFromServerWithAllLinks` | DocumentToDownLoadFromServer: BSTR, DocumentAccessMode: DocumentAccess, [out] LocalPath: BSTR*, [opt]GetLatestDocuments: VARIANT, [opt]ProcessIndirectDocuments: VARIANT, [opt]RevisionRuleOption: VARIANT, [opt]StopFileOpenIfRevisionRuleNotApplicable: VARIANT | void |
| `SetInsightATPRunning` | bRunningInsightATP: bool | void |
| `SetInsightATPInfo` | bstrATPLevel1: BSTR, bstrATPClassName: BSTR, bstrATPName: BSTR, ATPId: int | void |
| `ValidateDocsOnLCA` | bstrInputURL: BSTR, numberOfDocumentstoValidate: int, varlistOfDocsToValidate: VARIANT, bstrInputData: BSTR | void |
| `IsVersioningEnabledForTheInputDocLib` | docLibName: BSTR, [out] pbIsVersioningOnForTheInputDocLib: bool* | void |
| `IsFileCheckedOutToSameUser` | filePath: BSTR, UserName: BSTR, Password: BSTR, [out] checkedoutby: BSTR*, [opt]UpdateCache: bool | void |
| `IsDocumentLibraryContainsRequiredProperty` | docLibName: BSTR, [out] pbIsRequiredPropertyExist: bool* | void |
| `SetInsightOfflineMode` | bOfflineModeVal: bool | void |
| `DisplayPropertyManagerDlg` | bstrFilename: BSTR | void |
| `GetCookieData` | bstrFilename: BSTR, valCookieDataToGet: CookieDataToGet, [out] varRevisionRule: RevisionRuleType* | void |
| `SetFilePropertiesOnServer` | bstrInputURL: BSTR, NumberOfPropertiesToSet: int, PropertyUrIList: VARIANT, PropertyValueList: VARIANT | void |
| `ISDocumentParserEnabled` | bstrInputURL: BSTR, [out] bDocParserEnabled: bool* | void |
| `GetLWFPathForUrl` | bstrUrl: BSTR, [out] bstrLWFPath: BSTR* | void |
| `DisplaySEPackNGoDlg` | bstrFilename: BSTR | void |

### <a name="disefeatureselectedfrompfevents"></a>`DISEFeatureSelectedFromPFEvents`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `QueryInterface` | riid: GUID*, [out] ppvObj: void** | void |
| `AddRef` | — | uint |
| `Release` | — | uint |
| `AfterFeatureSelectedFromPF` | theDocument: IDispatch, SelectedFeature: IDispatch, lFeatureType: int | void |

### <a name="mattable"></a>`MatTable`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `QueryInterface` | riid: GUID*, [out] ppvObj: void** | void |
| `AddRef` | — | uint |
| `Release` | — | uint |
| `GetMaterialList` | [out] plNumMaterials: int*, [out] listOfMaterials: VARIANT* | void |
| `SetActiveDocument` | pDocument: IDispatch | void |
| `AddMaterial` | bstrMatName: BSTR, lNumProps: int, varPropList: VARIANT, bstrFaceStyle: BSTR, bstrFillStyle: BSTR, bstrVSPlusStyle: BSTR | void |
| `GetMatPropValue` | bstrMatName: BSTR, lPropIndex: MatTablePropIndex, [out] varPropValue: VARIANT* | void |
| `SetMatPropValue` | bstrMatName: BSTR, lPropIndex: MatTablePropIndex, varPropValue: VARIANT | void |
| `DeleteMaterial` | bstrMatName: BSTR | void |
| `ApplyMaterial` | pDocument: IDispatch, bstrMatName: BSTR | void |
| `GetMatLibFileName` | [out] varMatLibName: VARIANT* | void |
| `WriteMatLibFileFromXML` | bstrXMLFile: BSTR, bstrMatLibName: BSTR | void |
| `WriteMaterialDataToXML` | bstrXMLFile: BSTR | void |
| `GetPSMGaugeListFromExcel` | bstrGageTableName: BSTR, [out] plNumGages: int*, [out] listOfGages: VARIANT* | void |
| `GetPSMGaugeInfoForDoc` | pDocument: IDispatch, [out] bstrGageName: BSTR*, [out] bstrGageFilePath: BSTR*, [out] iMTLUsingExcel: int*, [out] bstrMTLGageTableName: BSTR*, [out] iDocUsingExcel: int*, [out] bstrDocGageTableName: BSTR*, [out] iCountBendRadiusVals: int*, [out] iCountBendAngleVals: int*, [out] iCountNFVals: int* | void |
| `GetDefaultGageFileName` | [out] strGageFileName: BSTR* | void |
| `PerformGageDataValidation` | strExcelFile: BSTR, strGageTable: BSTR, strGage: BSTR | bool |
| `SetMaterialToGageTableAssociation` | pDocument: IDispatch, bstrMaterialName: BSTR, bstrMaterialGageTableName: BSTR, bAddAssociation: bool | void |
| `SetDocumentToGageTableAssociation` | pDocument: IDispatch, bstrDocGageName: BSTR, bstrDocGageTableName: BSTR, bUseNeutralFactorFromExcel: bool, bAddAssociation: bool | void |
| `UseNeutralFactorFromExcel` | pDocument: IDispatch, bUseNeutralFactorFromExcel: bool | void |
| `EditOpenGageExcelFile` | bstrDocGageTableName: BSTR | void |
| `GetCurrentGageName` | pDocument: IDispatch, [out] bstrGageName: BSTR* | void |
| `GetCurrentMaterialName` | pDocument: IDispatch, [out] bstrMaterialName: BSTR* | void |
| `GetMaterialListFromLibrary` | bstrLibraryName: BSTR, [out] plNumMaterials: int*, [out] listOfMaterials: VARIANT* | void |
| `AddMaterialToLibrary` | bstrMatName: BSTR, bstrLibrary: BSTR, bstrMaterialPath: BSTR, lNumProps: int, varPropList: VARIANT, bstrFaceStyle: BSTR, bstrFillStyle: BSTR | void |
| `DeleteMaterialFromLibrary` | bstrMatName: BSTR, bstrLibraryName: BSTR | void |
| `GetMaterialPropValueFromLibrary` | bstrMatName: BSTR, bstrLibraryName: BSTR, lPropIndex: MatTablePropIndex, [out] varPropValue: VARIANT* | void |
| `SetMaterialPropValueToLibrary` | bstrMatName: BSTR, bstrLibraryName: BSTR, lPropIndex: MatTablePropIndex, varPropValue: VARIANT | void |
| `GetMaterialPropValueFromDoc` | pDocument: IDispatch, lPropIndex: MatTablePropIndex, [out] varPropValue: VARIANT* | void |
| `ApplyMaterialToDoc` | pDocument: IDispatch, bstrMatName: BSTR, bstrLibraryName: BSTR | void |
| `AddMaterialToFavorites` | bstrMaterialName: BSTR, bstrLibraryName: BSTR | void |
| `GetFavoriteMaterialList` | [out] MaterialNames: VARIANT*, [out] LibraryNames: VARIANT*, [out] plNumMaterials: int* | void |
| `GetMRUMaterialList` | [out] MaterialNames: VARIANT*, [out] LibraryNames: VARIANT*, [out] plNumMaterials: int* | void |
| `SetMRUMaterialLimit` | nNoOfMRUMtls: int | void |
| `GetMRUMaterialLimit` | [out] nNoOfMRUMtls: int* | void |
| `ClearMRUList` | — | void |
| `AddCustomProperty` | bstrMatName: BSTR, bstrMatLibName: BSTR, bstrPropName: BSTR, ePropUnitType: UnitTypeConstants, varPropValue: VARIANT | void |
| `DeleteCustomProperty` | bstrMatName: BSTR, bstrMatLibName: BSTR, nPropIndex: int | void |
| `GetCountOfCustomProperties` | bstrMatName: BSTR, bstrMatLibName: BSTR, [out] nNumOfCustProps: int* | void |
| `GetCustomMaterialPropertyFromLibrary` | bstrMatName: BSTR, bstrMatLibName: BSTR, nPropIndex: int, [out] bstrPropName: BSTR*, [out] ePropUnitType: UnitTypeConstants*, [out] varPropValue: VARIANT* | void |
| `GetCustomMaterialPropertyFromDoc` | pDocument: IDispatch, nPropIndex: int, [out] bstrPropName: BSTR*, [out] ePropUnitType: UnitTypeConstants*, [out] varPropValue: VARIANT* | void |
| `GetMaterialsFolderPath` | [out] bstrMtlFolderPath: BSTR* | void |
| `GetMaterialLibraryFileList` | [out] MaterialLibList: VARIANT*, [out] plNumMaterialLibraries: int* | void |
| `CreateNewMaterialLibrary` | bstrLibInputName: BSTR | void |
| `CreateNewDirectory` | bstrLibname: BSTR, bstrDirectoryPath: BSTR | void |
| `RenameMaterial` | bstrMatOldName: BSTR, bstrLibname: BSTR, bstrMatNewName: BSTR | void |
| `RenameLibrary` | bstrLibOldName: BSTR, bstrLibNeName: BSTR | void |
| `RenameDirectory` | bstrDirOldName: BSTR, bstrLibname: BSTR, bstrDirNewName: BSTR | void |
| `ExportMaterialDataToFile` | bstrMaterialLibraryName: BSTR, bstrXMLFile: BSTR | void |
| `ImportMaterialDataFromFile` | bstrXMLFile: BSTR, bstrMatLibFile: BSTR | void |
| `SetMaterialsFolderPath` | bstrMtlFolderPath: BSTR | void |
| `DeleteDirectory` | bstrDirName: BSTR, bstrLibname: BSTR | void |
| `GetMaterialLibraryList` | [out] MaterialLibList: VARIANT*, [out] plNumMaterialLibraries: int* | void |
| `ApplyMaterialToFile` | bstrFilename: BSTR, bstrMatName: BSTR, bstrLibraryName: BSTR | void |
| `GetOODStatusofMaterialAndGage` | pDoc: IDispatch, [out] vbMaterialPropOOD: bool*, [out] vbGagePropOOD: bool* | void |
| `UpdateOODMaterialAndGageProperties` | pDoc: IDispatch, vbUpdateMaterialProp: bool, vbUpdateGageProp: bool | void |
| `GetNeutralFactor` | pDoc: IDispatch, dBendAngle: double, dBendRadius: double, [out] dNeutralFactor: double* | void |
| `ApplyGageFromLibraryToDoc` | pDocument: IDispatch, bstrGage: BSTR, bstrLibraryName: BSTR | void |
| `ApplyGageFromGageTableToDoc` | pDocument: IDispatch, bstrGage: BSTR, bstrGageTableName: BSTR | void |
| `ApplyMaterialToInternalComponents` | pDocument: IDispatch, NumOfInternalComponents: int, psaInternalComponents: SAFEARRAY(IDispatch)*, bstrMatName: BSTR, bstrLibraryName: BSTR | void |
| `ApplyGageFromLibraryToInternalComponents` | pDocument: IDispatch, NumOfInternalComponents: int, psaInternalComponents: SAFEARRAY(IDispatch)*, bstrGage: BSTR, bstrLibraryName: BSTR | void |
| `ApplyGageFromGageTableToInternalComponents` | pDocument: IDispatch, NumOfInternalComponents: int, psaInternalComponents: SAFEARRAY(IDispatch)*, bstrGage: BSTR, bstrGageTableName: BSTR | void |

### <a name="solidedgetce"></a>`SolidEdgeTCE`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `QueryInterface` | riid: GUID*, [out] ppvObj: void** | void |
| `AddRef` | — | uint |
| `Release` | — | uint |
| `GetPDMCachePath` | [out] bStrCachePath: BSTR* | void |
| `CheckInDocumentsToTeamCenterServer` | ppsaFileList: SAFEARRAY(VARIANT)*, bOnlyUpload: bool | void |
| `CheckOutDocumentsFromTeamCenterServer` | bstrDirctFileItemId: BSTR, bstrDirctFileItemRevId: BSTR, bOnlyDownload: bool, [opt]bstrFilename: BSTR, [opt]enumDownloadLevel: DocumentDownloadLevel | void |
| `IsTeamCenterFileCheckedOut` | bstrFilename: BSTR | int |
| `GetDocumentUID` | bstrFilename: BSTR, [out] bstrItemId: BSTR*, [out] bstrItemRev: BSTR* | void |
| `DoesTeamCenterFileExists` | bstrItemId: BSTR, bstrItemRev: BSTR, [out] bFileExists: bool* | void |
| `GetTeamCenterMode` | [out] bIsTeamCenterMode: bool* | void |
| `SetTeamCenterMode` | bMode: bool | void |
| `ValidateLogin` | bstrUserName: BSTR, bstrPassword: BSTR, bstrGroup: BSTR, bstrUserRole: BSTR, bstrDBUrl: BSTR | void |
| `AssignItemID` | bstrItemType: BSTR, [out] bstrItemId: BSTR*, [out] bstrItemRev: BSTR* | void |
| `PutItemTypeAsCustomProp` | bstrFilename: BSTR, bstrItemType: BSTR | void |
| `GetDatasetNameFromCookie` | bstrFilename: BSTR, [out] bstrDatasetName: BSTR* | void |
| `DeleteFilesFromCache` | psaCacheFiles: SAFEARRAY(VARIANT)* | void |
| `ImportDocumentsToServer` | lnumberOfDocumentsFoldersToImport: int, psalistOfFilesFoldersToImport: SAFEARRAY(VARIANT)*, bstrUserName: BSTR, bstrPassword: BSTR, bstrGroup: BSTR, bstrUserRole: BSTR, bstrDBUrl: BSTR, bImportAsPrecise: bool, [opt]bPerformOnlyDryRun: bool, [opt]bDisplayAlert: bool, [opt]bIsFromATP: bool, [opt]bIsOverwrite: bool, [opt]brestart: bool, [opt]bLinkTraversal: bool, [opt]bIncludeSubFolders: bool, [opt]bstrFolderUID: BSTR* | void |
| `OnUndoCheckOutDocuments` | psaCacheFiles: SAFEARRAY(VARIANT)* | void |
| `OnSynchronizeFile` | psaSynchFiles: SAFEARRAY(VARIANT)*, [opt]enumSyncOption: SyncOption | void |
| `GetOutOfDateDocuments` | [out] pvarListOfOutOfDateDocuments: VARIANT* | void |
| `GetUserLogMessages` | [out] pvarUserLogMessages: VARIANT* | void |
| `SaveAsToTeamCenter` | bstrItemId: BSTR, bstrItemRev: BSTR, bstrDataSetFileName: BSTR, bstrRevisionRule: BSTR, bstrFolderName: BSTR, [out] pvarListOldAndNewItemIDRevsFileNames: VARIANT* | void |
| `ReviseToTeamCenter` | bstrItemId: BSTR, bstrItemRev: BSTR, bstrDataSetFileName: BSTR, bstrRevisionRule: BSTR, bstrFolderName: BSTR, [out] pvarListOldAndNewItemIDRevsFileNames: VARIANT* | void |
| `OnGetWhereUsedForAutomation` | bstrItemId: BSTR, bstrItemRev: BSTR, bstrDataSetFileName: BSTR, [out] pvarListOfWhereUsedDocuments: VARIANT* | void |
| `CreateNewProject` | bstrProjectName: BSTR | void |
| `DeleteProject` | bstrProjectName: BSTR | void |
| `DeleteAllProjects` | — | void |
| `DownladDocumentsFromServerWithOptions` | bstrItemId: BSTR, bstrItemRev: BSTR, bstrDataSetFileName: BSTR, bstrRevisionRule: BSTR, bstrVariantRule: BSTR, vbReadOnly: bool, vbAllLevel: bool, dwDownloadOption: uint, [opt]pvarListOfFiles: SAFEARRAY(VARIANT)* | void |
| `GetListOfIndirectFilesForGivenFile` | bstrItemId: BSTR, bstrItemRev: BSTR, bstrDataSetFileName: BSTR, bstrRevisionRule: BSTR, bstrVariantRule: BSTR, [out] pvarListOfIndirectFiles: VARIANT* | void |
| `GetMappedPropertiesForGivenFile` | bstrItemId: BSTR, bstrItemRev: BSTR, bstrDataSetFileName: BSTR, [out] pvarListOfProperties: VARIANT* | void |
| `GetListOfFilesFromTeamcenterServer` | bstrDirctFileItemId: BSTR, bstrDirctFileItemRevId: BSTR, [out] vFileNames: VARIANT*, [out] nFiles: int* | void |
| `GetTALLogFileName` | [out] bstrLogFileName: BSTR* | void |
| `ValidateTcObjectModel` | bstrFilename: BSTR, bstrOldItemID: BSTR, bstrOldRev: BSTR, [out] bIsTcModelCorrect: bool*, [out] bsrtCompResult: BSTR*, [opt]vRevisionRule: VARIANT*, [opt]vValidateBOMOnly: VARIANT*, [opt][out] nNoOfServerComponents: int* | void |
| `GetBomStructure` | bstrItemId: BSTR, bstrItemRevId: BSTR, bstrRevisionRule: BSTR, bDeepList: bool, [out] NoOfComponents: int*, [out] ListOfItemRevIds: VARIANT*, [out] ListOfFileSpecs: VARIANT* | void |
| `GetItemRevBasedOnSEType` | nSEType: TCESETypes, bstrUserName: BSTR, [out] ListOfItemRevIds: VARIANT* | void |
| `GetItemTypesInfo` | [out] pbstrXML: BSTR*, [out] pbstrDefaultItemType: BSTR* | void |
| `GetSmartCodes` | [in,out] pvarSmartCodesInfo: VARIANT* | void |
| `UnGetSmartCodes` | ppsaUnGetInfo: SAFEARRAY(VARIANT)* | void |
| `CheckInDocumentsToTeamCenterServerEx` | pvarFilesToBeCheckedInInfo: VARIANT*, pvarArguments: VARIANT* | void |
| `IsItemTypeSmartCodesConfigured` | bstrItemType: BSTR, pvbIsSmartCodesConfigured: bool* | void |
| `GetSEECOrTCPreferenceValues` | [in,out] pvarPreferenceInfo: VARIANT* | void |
| `UpdateStatusInformation` | psaCacheFiles: SAFEARRAY(VARIANT)* | void |
| `GetProjectsForLoggedInUSer` | [out] pvarListOfUserProjects: VARIANT*, [out] pvarListOfUserProjectUIDs: VARIANT* | void |
| `GetProjectsForGivenItemIDs` | [in,out] pvarListOfItemIDsAndProjects: VARIANT* | void |
| `AddOrRemoveItemsToOrFromProjects` | pvarItemInfoToAddOrRemoveToProjects: VARIANT* | void |
| `CheckBomStructure` | bstrItemId: BSTR, bstrItemRevId: BSTR, bstrRevisionRule: BSTR, [out] bIsDuplicateBOMLineExists: bool*, [out] ListOfDupliacteObjIDs: VARIANT* | void |
| `AutoAssign` | vbAutoAssign: bool | void |
| `GetMFKAttributesForGivenItemType` | bstrItemType: BSTR, [out] pvMFKAttributes: VARIANT* | void |
| `SetPDMProperties` | bstrOldFileName: BSTR, pvarListOfPropsForFileSaveAs: VARIANT*, [out] bstrNewFileName: BSTR* | void |
| `GetCurrentUserName` | [out] bStrCurrentUser: BSTR* | void |
| `GetDocumentUIDEx` | bstrFilename: BSTR, [out] bstrItemId: BSTR*, [out] bstrItemRev: BSTR*, [out] bstrItemUID: BSTR*, [out] bstrRevUID: BSTR* | void |
| `DoesTeamCenterFileExistsUsingKeyProperties` | bstrItemType: BSTR, pvarMFKAttrInfo: VARIANT*, bstrItemRev: BSTR, [out] bFileExists: bool* | void |
| `CheckOutDocumentsFromTeamCenterServerUsingKeyProperties` | pvarMFKAttrInfo: VARIANT*, bstrItemType: BSTR, bstrDirctFileItemRevId: BSTR, bOnlyDownload: bool, [opt]bstrFilename: BSTR, [opt]enumDownloadLevel: DocumentDownloadLevel | void |
| `GetTeamcenterVersion` | [out] bstrMajorVersion: BSTR*, [out] bstrCompleteVersion: BSTR* | void |
| `GetItemIDAndRevisionPatterns` | bstrItemType: BSTR, [out] pvarItemIDPattern: VARIANT*, [out] pvarRevisionPattern: VARIANT* | void |
| `AssignItemIDAndRevUsingPatterns` | bstrItemType: BSTR, bstrItemIDPattern: BSTR, bstrRevisionPattern: BSTR, [out] pItemIDPattern: BSTR*, [out] pRevisionPattern: BSTR* | void |
| `GetItemTypesInfoEx` | bstrFilename: BSTR, [out] pvarItemTypeList: VARIANT* | void |
| `MapMFKAttributtesToFileProperties` | bstrFilename: BSTR, psaMFKAttributes: SAFEARRAY(VARIANT)*, [out] pvarPropertyInfo: VARIANT* | void |
| `GetListOfFilesFromTeamcenterServerUsingKeyProperties` | pvarMFKAttrInfo: VARIANT*, bstrDirctFileItemRevId: BSTR, bstrItemType: BSTR, [out] vFileNames: VARIANT*, [out] nFiles: int* | void |
| `GetBomStructureUsingKeyProperties` | pvarMFKAttrInfo: VARIANT*, bstrItemRevId: BSTR, bstrItemType: BSTR, bstrRevisionRule: BSTR, bDeepList: bool, [out] NoOfComponents: int*, [out] ListOfItemRevIds: VARIANT*, [out] ListOfFileSpecs: VARIANT* | void |
| `CheckBomStructureUsingKeyProperties` | pvarMFKAttrInfo: VARIANT*, bstrItemRevId: BSTR, bstrItemType: BSTR, bstrRevisionRule: BSTR, [out] bIsDuplicateBOMLineExists: bool*, [out] ListOfDupliacteObjIDs: VARIANT* | void |
| `SaveAsToTeamCenterUsingKeyProperties` | pvarMFKAttrInfo: VARIANT*, bstrItemRev: BSTR, bstrItemType: BSTR, bstrDataSetFileName: BSTR, bstrRevisionRule: BSTR, bstrFolderName: BSTR, [out] pvarListOldAndNewItemIDRevsFileNames: VARIANT* | void |
| `ReviseToTeamCenterUsingKeyProperties` | pvarMFKAttrInfo: VARIANT*, bstrItemRev: BSTR, bstrItemType: BSTR, bstrDataSetFileName: BSTR, bstrRevisionRule: BSTR, bstrFolderName: BSTR, [out] pvarListOldAndNewItemIDRevsFileNames: VARIANT* | void |
| `GetListOfIndirectFilesForGivenFileUsingKeyProperties` | pvarMFKAttrInfo: VARIANT*, bstrItemRev: BSTR, bstrItemType: BSTR, bstrDataSetFileName: BSTR, bstrRevisionRule: BSTR, [out] pvarListOfIndirectFiles: VARIANT* | void |
| `GetMappedPropertiesForGivenFileUsingKeyProperties` | pvarMFKAttrInfo: VARIANT*, bstrItemRev: BSTR, bstrItemType: BSTR, bstrDataSetFileName: BSTR, [out] pvarListOfProperties: VARIANT* | void |
| `ValidateTcObjectModelUsingKeyProperties` | bstrFilename: BSTR, pvarMFKAttrInfo: VARIANT*, bstrItemType: BSTR, bstrOldRev: BSTR, [out] bIsTcModelCorrect: bool*, [out] bsrtCompResult: BSTR*, [opt]vRevisionRule: VARIANT*, [opt]vValidateBOMOnly: VARIANT*, [opt][out] nNoOfServerComponents: int* | void |
| `OnGetWhereUsedForAutomationUsingKeyProperties` | pvarMFKAttrInfo: VARIANT*, bstrItemRev: BSTR, bstrItemType: BSTR, bstrDataSetFileName: BSTR, [out] pvarListOfWhereUsedDocuments: VARIANT* | void |
| `DownloadDocumentsFromServerWithOptionsUsingKeyProperties` | pvarMFKAttrInfo: VARIANT*, bstrItemRev: BSTR, bstrDataSetFileName: BSTR, bstrRevisionRule: BSTR, bstrItemType: BSTR, vbReadOnly: bool, vbAllLevel: bool, dwDownloadOption: uint, [opt]pvarListOfFiles: SAFEARRAY(VARIANT)* | void |
| `ValidateKeyProperties` | pvarMFKAttrInfo: VARIANT*, bstrRevID: BSTR, bstrItemType: BSTR, [out] pvarListOfPropsVsStatus: VARIANT* | void |
| `GetStorageNameForProperties` | bstrFilename: BSTR, [out] pvarListOfPropInfo: VARIANT* | void |
| `GetErrorMessages` | [out] pvarListOfErrorMsgs: VARIANT*, [out] pvarListOfWarningMsgs: VARIANT*, [out] pvarListOfInformationalMsgs: VARIANT* | void |
| `GetAllRevisions` | pvarMFKAttributes: VARIANT*, bstrItemType: BSTR, [out] pvarRevisionList: VARIANT* | void |
| `CreateZipOfCache` | bstrSourceCachePath: BSTR, bstrDestinationZipPath: BSTR | void |
| `GetListOfWorkflows` | pvarMFKAttributes: VARIANT*, bstrItemType: BSTR, bstrItemRev: BSTR, bGetFiltered: bool, [out] pVarWorkflows: VARIANT* | void |
| `ExecuteWorkflow` | pvarMFKAttributes: VARIANT*, bstrItemType: BSTR, bstrItemRev: BSTR, bstrprocessName: BSTR, bstrProcessDesr: BSTR, bstrTemplate: BSTR, [out] pVarOut: VARIANT* | void |
| `GetActivePDMMode` | [out] activePDM: uint* | void |
| `GetSolidEdgePreferencePath` | [out] lpSEPreferencePath: BSTR* | void |
| `SetSEECOptions` | enumSEECDialog: SEECOptions, dwValue: uint* | void |
| `GetBOMProperties` | pvarMFKAttributes: VARIANT*, bstrItemRevId: BSTR, bstrItemType: BSTR, bstrRevisionRule: BSTR, [out] NoOfComponents: int*, [out] FileOccProp: VARIANT* | void |
| `PublishFamilyOfAssemblyMembers` | pvarMFKAttrInfo: VARIANT*, bstrItemRev: BSTR, bstrDataSetFileName: BSTR, bstrRevisionRule: BSTR, bstrItemType: BSTR, psaMemberInfo: SAFEARRAY(VARIANT)*, [out] pvarListOfWhereUsedDocuments: VARIANT* | void |
| `GetFamilyOfAssemblyMembers` | pvarMFKAttrInfo: VARIANT*, bstrItemRev: BSTR, bstrItemType: BSTR, [out] psaPublishedMemberInfo: VARIANT*, [out] psaUnpublishedMemberInfo: VARIANT* | void |
| `GetWhereUsedInfoForPublishedFamilyOfAssembly` | pvarMFKAttrInfo: VARIANT*, bstrItemRev: BSTR, bstrDataSetFileName: BSTR, bstrRevisionRule: BSTR, bstrItemType: BSTR, [out] pvarListOfWhereUsedDocuments: VARIANT* | void |
| `GetWhereUsedInfo` | pvarMFKAttrInfo: VARIANT*, bstrItemRev: BSTR, bstrItemType: BSTR, bstrDataSetFileName: BSTR, [out] pvarListFilesWithMFKAttributes: VARIANT* | void |
| `PublishMembersOfActiveFamilyOfAssemblyDocument` | psaMemberInfo: SAFEARRAY(VARIANT)*, [out] pvarListOfWhereUsedDocuments: VARIANT* | void |
| `GetWhereUsedInfoForPublishedActiveFamilyOfAssemblyDocument` | [out] pvarListOfWhereUsedDocuments: VARIANT* | void |
| `GetFamilyOfAssemblyMembersOfActiveDocument` | [out] psaPublishedMemberInfo: VARIANT*, [out] psaUnpublishedMemberInfo: VARIANT* | void |
| `GetTeamcenterDefaultItemTypePreference` | bstrFilename: BSTR, [out] bstrDefaultItemType: BSTR* | void |
| `CreateBOMAndRelations` | pvarContainerInfo: VARIANT*, psaComponentsInfo: SAFEARRAY(VARIANT)*, bUploadFile: bool, bCreatePreciseBOM: bool, bstrRevRule: BSTR, [out] bHasOverrideBody: bool* | void |
| `UploadModelViewsOfActiveAssemblyDocument` | — | void |
| `ExtractTranslatedFiles` | pvarMFKAttrInfo: VARIANT*, bstrItemRev: BSTR, bstrRevisionRule: BSTR, bstrItemType: BSTR, bstrEtractLocation: BSTR, bstrDataSetFileName: BSTR, dwExpandSelectionOptions: uint, pvarSEFiletypeFilters: VARIANT*, pvarRelationFilters: VARIANT*, pvarReferanceFilters: VARIANT*, pvarExportFileextensions: VARIANT*, [out] pvarListOfFiles: VARIANT* | void |
| `ExtractTranslatedFilesOfActiveDocument` | bstrEtractLocation: BSTR, dwExpandSelectionOptions: uint, pvarSEFiletypeFilters: VARIANT*, pvarRelationFilters: VARIANT*, pvarReferanceFilters: VARIANT*, pvarExportFileextensions: VARIANT*, [out] pvarListOfFiles: VARIANT* | void |
| `CloneDraftDocument` | vbOpenCloneDocument: bool | void |
| `GetMFKAttributesAndItemTypeForGivenFile` | bstrFilename: BSTR, [out] bstrItemType: BSTR*, [out] pvarMFKAttributeValues: VARIANT* | void |
| `SetPDMPropsAndUploadTranslatedFile` | bstrOldFileName: BSTR, pvarListOfPropsForFileSaveAs: VARIANT*, [in,out] bstrNewFileName: BSTR* | void |
| `GetListOfFilesUnderItemRevision` | pvarMFKAttrInfo: VARIANT*, bstrItemRev: BSTR, bstrItemType: BSTR, [out] pvarSEFileListUnderItemRev: VARIANT*, [out] pvarAllFileListUnderItemRev: VARIANT* | void |
| `GetTCSaveAsTranslationMBDPrefValues` | [out] bRelation: BSTR*, [out] bDatasetType: BSTR*, [out] bNamedReference: BSTR*, [out] bStringToAppend: BSTR*, [out] bIncludeRevName: bool* | void |
| `GetRequiredPDMProperties` | bstrFilename: BSTR, pvarProperties: VARIANT*, [out] pvarPropertiesWithValues: VARIANT* | void |
| `GetActiveDocumentFilename` | [out] bstrFilename: BSTR*, [out] bstrDisplayname: BSTR*, [out] bstrReservedname: BSTR* | void |
| `GetTeamcenterInformation` | bstrFilename: BSTR, [out] psaTCInfo: VARIANT* | void |
| `OnUndoCheckOutDocumentsEx` | psaCacheFiles: SAFEARRAY(VARIANT)*, bIgnoreFileModifiedStatus: bool | void |
| `GetTCSaveAsTranslationPrefValues` | bstrExportType: BSTR, [out] bRelation: BSTR*, [out] bDatasetType: BSTR*, [out] bNamedReference: BSTR*, [out] bStringToAppend: BSTR*, [out] bIncludeRevName: bool* | void |
| `DownloadDocumentsFromServerWithOptionsUsingKeyPropertiesEx` | pvarMFKAttrInfo: VARIANT*, bstrItemRev: BSTR, bstrDataSetFileName: BSTR, bstrRevisionRule: BSTR, bstrVariantRule: BSTR, bstrItemType: BSTR, vbReadOnly: bool, vbAllLevel: bool, dwDownloadOption: uint, [opt]pvarListOfFiles: SAFEARRAY(VARIANT)* | void |
| `ValidateLogin_TCCS` | bstrUserName: BSTR, bstrPassword: BSTR, bstrGroup: BSTR, bstrUserRole: BSTR, bstrDBUrl: BSTR, bstrDBDesc: BSTR | void |
| `CheckInStdPartsToTeamcenter` | ppsaFileList: SAFEARRAY(VARIANT)*, bOnlyUpload: bool | void |

### <a name="solidedgeinsightxt"></a>`SolidEdgeInsightXT`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `QueryInterface` | riid: GUID*, [out] ppvObj: void** | void |
| `AddRef` | — | uint |
| `Release` | — | uint |
| `GetPDMCachePath` | [out] bStrCachePath: BSTR* | void |
| `CheckInDocumentsToInsightXTServer` | ppsaFileList: SAFEARRAY(VARIANT)*, bOnlyUpload: bool, bstrUrl: BSTR | void |
| `CheckOutDocumentsFromInsightXTServer` | bstrDirctFileItemId: BSTR, bstrDirctFileItemRevId: BSTR, bOnlyDownload: bool, [opt]bstrFilename: BSTR, [opt]enumDownloadLevel: DocumentDownloadLevel | void |
| `IsInsightXTFileCheckedOut` | bstrFilename: BSTR | int |
| `GetDocumentUID` | bstrFilename: BSTR, [out] bstrItemId: BSTR*, [out] bstrItemRev: BSTR* | void |
| `DoesInsightXTFileExists` | bstrItemId: BSTR, bstrItemRev: BSTR, [out] bFileExists: bool* | void |
| `GetInsightXTMode` | [out] bIsInsightXTMode: bool* | void |
| `SetInsightXTMode` | bMode: bool | void |
| `ValidateLogin` | bstrUserName: BSTR, bstrPassword: BSTR, bstrGroup: BSTR, bstrUserRole: BSTR, bstrDBUrl: BSTR | void |
| `AssignItemID` | bstrItemType: BSTR, [out] bstrItemId: BSTR*, [out] bstrItemRev: BSTR* | void |
| `SESP_GetItemAndRevisionNo` | bstrItemContentType: BSTR, bstrItemRevContentType: BSTR, [out] bstrPartno: BSTR*, [out] bstrPartRevno: BSTR* | void |
| `PutItemTypeAsCustomProp` | bstrFilename: BSTR, bstrItemType: BSTR | void |
| `GetDatasetNameFromCookie` | bstrFilename: BSTR, [out] bstrDatasetName: BSTR* | void |
| `DeleteFilesFromCache` | psaCacheFiles: SAFEARRAY(VARIANT)* | void |
| `ImportDocumentsToServer` | lnumberOfDocumentsFoldersToImport: int, psalistOfFilesFoldersToImport: SAFEARRAY(VARIANT)*, bstrUserName: BSTR, bstrPassword: BSTR, bstrGroup: BSTR, bstrUserRole: BSTR, bstrDBUrl: BSTR, bImportAsPrecise: bool, [opt]bPerformOnlyDryRun: bool, [opt]bDisplayAlert: bool, [opt]bIsFromATP: bool, [opt]bIsOverwrite: bool, [opt]brestart: bool, [opt]bLinkTraversal: bool, [opt]bIncludeSubFolders: bool, [opt]bstrFolderUID: BSTR* | void |
| `OnUndoCheckOutDocuments` | psaCacheFiles: SAFEARRAY(VARIANT)* | void |
| `OnSynchronizeFile` | psaSynchFiles: SAFEARRAY(VARIANT)*, [opt]enumSyncOption: SyncOption | void |
| `GetOutOfDateDocuments` | [out] pvarListOfOutOfDateDocuments: VARIANT* | void |
| `GetUserLogMessages` | [out] pvarUserLogMessages: VARIANT* | void |
| `SaveAsToInsightXT` | bstrItemId: BSTR, bstrItemRev: BSTR, bstrDataSetFileName: BSTR, bstrRevisionRule: BSTR, bstrFolderName: BSTR, [out] pvarListOldAndNewItemIDRevsFileNames: VARIANT* | void |
| `ReviseToInsightXT` | bstrItemId: BSTR, bstrItemRev: BSTR, bstrDataSetFileName: BSTR, bstrRevisionRule: BSTR, bstrFolderName: BSTR, [out] pvarListOldAndNewItemIDRevsFileNames: VARIANT* | void |
| `OnGetWhereUsedForAutomation` | bstrItemId: BSTR, bstrItemRev: BSTR, bstrDataSetFileName: BSTR, [out] pvarListOfWhereUsedDocuments: VARIANT* | void |
| `CreateNewProject` | bstrProjectName: BSTR | void |
| `DeleteProject` | bstrProjectName: BSTR | void |
| `DeleteAllProjects` | — | void |
| `DownladDocumentsFromServerWithOptions` | bstrItemId: BSTR, bstrItemRev: BSTR, bstrDataSetFileName: BSTR, bstrRevisionRule: BSTR, bstrVariantRule: BSTR, vbReadOnly: bool, vbAllLevel: bool, dwDownloadOption: uint, [opt]pvarListOfFiles: SAFEARRAY(VARIANT)* | void |
| `GetListOfIndirectFilesForGivenFile` | bstrItemId: BSTR, bstrItemRev: BSTR, bstrDataSetFileName: BSTR, bstrRevisionRule: BSTR, bstrVariantRule: BSTR, [out] pvarListOfIndirectFiles: VARIANT* | void |
| `GetMappedPropertiesForGivenFile` | bstrItemId: BSTR, bstrItemRev: BSTR, bstrDataSetFileName: BSTR, [out] pvarListOfProperties: VARIANT* | void |
| `GetListOfFilesFromInsightXTServer` | bstrDirctFileItemId: BSTR, bstrDirctFileItemRevId: BSTR, [out] vFileNames: VARIANT*, [out] nFiles: int* | void |
| `GetTALLogFileName` | [out] bstrLogFileName: BSTR* | void |
| `ValidateTcObjectModel` | bstrFilename: BSTR, bstrOldItemID: BSTR, bstrOldRev: BSTR, [out] bIsTcModelCorrect: bool*, [out] bsrtCompResult: BSTR*, [opt]vRevisionRule: VARIANT*, [opt]vValidateBOMOnly: VARIANT*, [opt][out] nNoOfServerComponents: int* | void |
| `GetBomStructure` | bstrItemId: BSTR, bstrItemRevId: BSTR, bstrRevisionRule: BSTR, bDeepList: bool, [out] NoOfComponents: int*, [out] ListOfItemRevIds: VARIANT*, [out] ListOfFileSpecs: VARIANT* | void |
| `GetItemRevBasedOnSEType` | nSEType: TCESETypes, bstrUserName: BSTR, [out] ListOfItemRevIds: VARIANT* | void |
| `GetItemTypesInfo` | [out] pbstrXML: BSTR*, [out] pbstrDefaultItemType: BSTR* | void |
| `GetSmartCodes` | [in,out] pvarSmartCodesInfo: VARIANT* | void |
| `UnGetSmartCodes` | ppsaUnGetInfo: SAFEARRAY(VARIANT)* | void |
| `CheckInDocumentsToInsightXTServerEx` | pvarFilesToBeCheckedInInfo: VARIANT*, pvarArguments: VARIANT* | void |
| `IsItemTypeSmartCodesConfigured` | bstrItemType: BSTR, pvbIsSmartCodesConfigured: bool* | void |
| `GetInsightXTOrTCPreferenceValues` | [in,out] pvarPreferenceInfo: VARIANT* | void |
| `UpdateStatusInformation` | psaCacheFiles: SAFEARRAY(VARIANT)* | void |
| `GetProjectsForLoggedInUSer` | [out] pvarListOfUserProjects: VARIANT*, [out] pvarListOfUserProjectUIDs: VARIANT* | void |
| `GetProjectsForGivenItemIDs` | [in,out] pvarListOfItemIDsAndProjects: VARIANT* | void |
| `AddOrRemoveItemsToOrFromProjects` | pvarItemInfoToAddOrRemoveToProjects: VARIANT* | void |
| `CheckBomStructure` | bstrItemId: BSTR, bstrItemRevId: BSTR, bstrRevisionRule: BSTR, [out] bIsDuplicateBOMLineExists: bool*, [out] ListOfDupliacteObjIDs: VARIANT* | void |
| `GetItemContentTypesSupportingRevisioning` | [out] pvarListOfContentTypes: VARIANT* | void |
| `ProcessUpdateDrawing` | [out] bTerminateProcess: bool* | void |
| `SESPUpdateWFCallouts` | plistItemAndRevId: VARIANT*, pListOldAndNewPropName: VARIANT*, [out] ListOfFailedDrafts: VARIANT*, [out] bTerminateProcess: bool* | void |
| `SESPGetActiveUrl` | [out] activeUrl: VARIANT* | void |
| `IsInsightXTLicenseAvailable` | [out] bIsInsightXTLicenseAvailable: bool* | void |
| `PutContentTypeIntoStorage` | bstrFilename: BSTR, bstrItemType: BSTR, bItemRevType: BSTR, bContentType: BSTR | void |

### <a name="customization"></a>`Customization`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `QueryInterface` | riid: GUID*, [out] ppvObj: void** | void |
| `AddRef` | — | uint |
| `Release` | — | uint |
| `get Application` | — | Application* |
| `get Parent` | — | Application* |
| `get RibbonBarThemes` | — | RibbonBarThemes* |
| `get RadialMenu` | — | RadialMenu* |
| `get SwitchWindowCust` | — | SwitchWindowCust* |
| `BeginCustomization` | — | void |
| `EndCustomization` | — | void |

### <a name="ribbonbarthemes"></a>`RibbonBarThemes`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `QueryInterface` | riid: GUID*, [out] ppvObj: void** | void |
| `AddRef` | — | uint |
| `Release` | — | uint |
| `get Application` | — | Application* |
| `get Parent` | — | Customization* |
| `get Count` | — | int |
| `Item` | Index: VARIANT | RibbonBarTheme* |
| `get _NewEnum` | — | IUnknown |
| `Create` | BasedOffTheme: VARIANT | RibbonBarTheme* |
| `Commit` | — | void |
| `Remove` | Theme: VARIANT | void |
| `ActivateTheme` | Theme: VARIANT | void |

### <a name="ribbonbartheme"></a>`RibbonBarTheme`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `QueryInterface` | riid: GUID*, [out] ppvObj: void** | void |
| `AddRef` | — | uint |
| `Release` | — | uint |
| `get Application` | — | Application* |
| `get Parent` | — | RibbonBarThemes* |
| `get RibbonBars` | — | RibbonBars* |
| `get Name` | — | BSTR |
| `put Name` | p0: BSTR | void |
| `get GlobalControlSize` | — | RibbonBarControlSize |
| `put GlobalControlSize` | p0: RibbonBarControlSize | void |
| `get GlobalControlText` | — | RibbonBarControlText |
| `put GlobalControlText` | p0: RibbonBarControlText | void |
| `get Active` | — | bool |

### <a name="ribbonbars"></a>`RibbonBars`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `QueryInterface` | riid: GUID*, [out] ppvObj: void** | void |
| `AddRef` | — | uint |
| `Release` | — | uint |
| `get Application` | — | Application* |
| `get Parent` | — | RibbonBarTheme* |
| `get Count` | — | int |
| `Item` | Index: VARIANT | RibbonBar* |
| `get _NewEnum` | — | IUnknown |

### <a name="ribbonbar"></a>`RibbonBar`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `QueryInterface` | riid: GUID*, [out] ppvObj: void** | void |
| `AddRef` | — | uint |
| `Release` | — | uint |
| `get Application` | — | Application* |
| `get Parent` | — | RibbonBars* |
| `get RibbonBarTabs` | — | RibbonBarTabs* |
| `get Environment` | — | BSTR |

### <a name="ribbonbartabs"></a>`RibbonBarTabs`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `QueryInterface` | riid: GUID*, [out] ppvObj: void** | void |
| `AddRef` | — | uint |
| `Release` | — | uint |
| `get Application` | — | Application* |
| `get Parent` | — | RibbonBar* |
| `get Count` | — | int |
| `Item` | Index: VARIANT | RibbonBarTab* |
| `get _NewEnum` | — | IUnknown |
| `Insert` | Item: VARIANT, AtItem: VARIANT, mode: RibbonBarInsertMode | RibbonBarTab* |
| `Remove` | Item: VARIANT | void |

### <a name="ribbonbartab"></a>`RibbonBarTab`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `QueryInterface` | riid: GUID*, [out] ppvObj: void** | void |
| `AddRef` | — | uint |
| `Release` | — | uint |
| `get Application` | — | Application* |
| `get Parent` | — | RibbonBarTabs* |
| `get RibbonBarGroups` | — | RibbonBarGroups* |
| `get Name` | — | BSTR |
| `put Name` | p0: BSTR | void |
| `get Id` | — | int |
| `get Visible` | — | bool |
| `put Visible` | p0: bool | void |
| `Activate` | — | void |

### <a name="ribbonbargroups"></a>`RibbonBarGroups`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `QueryInterface` | riid: GUID*, [out] ppvObj: void** | void |
| `AddRef` | — | uint |
| `Release` | — | uint |
| `get Application` | — | Application* |
| `get Parent` | — | RibbonBarTab* |
| `get Count` | — | int |
| `Item` | Index: VARIANT | RibbonBarGroup* |
| `get _NewEnum` | — | IUnknown |
| `Insert` | Item: VARIANT, AtItem: VARIANT, mode: RibbonBarInsertMode | RibbonBarGroup* |
| `Remove` | Item: VARIANT | void |

### <a name="ribbonbargroup"></a>`RibbonBarGroup`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `QueryInterface` | riid: GUID*, [out] ppvObj: void** | void |
| `AddRef` | — | uint |
| `Release` | — | uint |
| `get Application` | — | Application* |
| `get Parent` | — | RibbonBarGroups* |
| `get RibbonBarControls` | — | RibbonBarControls* |
| `get Name` | — | BSTR |
| `put Name` | p0: BSTR | void |
| `get Id` | — | int |
| `get Visible` | — | bool |
| `put Visible` | p0: bool | void |

### <a name="ribbonbarcontrols"></a>`RibbonBarControls`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `QueryInterface` | riid: GUID*, [out] ppvObj: void** | void |
| `AddRef` | — | uint |
| `Release` | — | uint |
| `get Application` | — | Application* |
| `get ParentRibbonBarGroup` | — | RibbonBarGroup* |
| `get ParentRibbonBarControl` | — | RibbonBarControl* |
| `get Count` | — | int |
| `Item` | Index: VARIANT | RibbonBarControl* |
| `get _NewEnum` | — | IUnknown |
| `Insert` | Item: VARIANT, AtItem: VARIANT, mode: RibbonBarInsertMode | RibbonBarControl* |
| `Remove` | Item: VARIANT | void |

### <a name="ribbonbarcontrol"></a>`RibbonBarControl`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `QueryInterface` | riid: GUID*, [out] ppvObj: void** | void |
| `AddRef` | — | uint |
| `Release` | — | uint |
| `get Application` | — | Application* |
| `get Parent` | — | RibbonBarControls* |
| `get RibbonBarControls` | — | RibbonBarControls* |
| `get Name` | — | BSTR |
| `put Name` | p0: BSTR | void |
| `get Visible` | — | bool |
| `put Visible` | p0: bool | void |
| `get Size` | — | RibbonBarControlSize |
| `put Size` | p0: RibbonBarControlSize | void |
| `get Text` | — | RibbonBarControlText |
| `put Text` | p0: RibbonBarControlText | void |
| `get Id` | — | int |
| `get IconId` | — | int |

### <a name="radialmenu"></a>`RadialMenu`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `QueryInterface` | riid: GUID*, [out] ppvObj: void** | void |
| `AddRef` | — | uint |
| `Release` | — | uint |
| `get Application` | — | Application* |
| `get Parent` | — | Customization* |
| `get Count` | — | int |
| `Item` | Index: VARIANT | RibbonBarTheme* |
| `get _NewEnum` | — | IUnknown |
| `Create` | BasedOffTheme: VARIANT | RibbonBarTheme* |
| `Commit` | — | void |
| `Remove` | Theme: VARIANT | void |
| `LoadPallets` | strConfigFilename: BSTR* | void |
| `SavePallets` | strConfigFilename: BSTR* | void |
| `DumpPallets` | strLogfileName: BSTR* | void |
| `DumpPallet` | strEnvironmentName: BSTR*, strLogfileName: BSTR* | void |
| `SetCommand` | strEnvironmentName: BSTR*, ring: int, wedge: int, cmdID: int, imageID: int | void |
| `RemoveCommand` | strEnvironmentName: BSTR*, ring: int, wedge: int | void |
| `SwapCommands` | strEnvironmentName: BSTR*, ring1: int, wedge1: int, ring2: int, wedge2: int | void |

### <a name="switchwindowcust"></a>`SwitchWindowCust`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `QueryInterface` | riid: GUID*, [out] ppvObj: void** | void |
| `AddRef` | — | uint |
| `Release` | — | uint |
| `get Application` | — | Application* |
| `get Parent` | — | Customization* |
| `get Count` | — | int |
| `Item` | Index: VARIANT | RibbonBarTheme* |
| `get _NewEnum` | — | IUnknown |
| `Create` | BasedOffTheme: VARIANT | RibbonBarTheme* |
| `Commit` | — | void |
| `Remove` | Theme: VARIANT | void |
| `EnumGraphicViews` | pNumGraphicViews: int* | void |
| `NextGraphicView` | strTitle: BSTR*, strFullName: BSTR*, fileType: int*, hWnd: uint*, bActive: uint*, bDirty: uint* | void |
| `ActivateGraphicView` | hWnd: uint | void |

### <a name="dynamicvisualization"></a>`DynamicVisualization`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `QueryInterface` | riid: GUID*, [out] ppvObj: void** | void |
| `AddRef` | — | uint |
| `Release` | — | uint |
| `EnableDelayedIndexing` | bEnableDelayedIndexing: bool | void |

### <a name="view"></a>`View`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `QueryInterface` | riid: GUID*, [out] ppvObj: void** | void |
| `AddRef` | — | uint |
| `Release` | — | uint |
| `Fit` | — | void |
| `get Window` | — | Window* |
| `ModelToDC` | [in,out] Matrix: SAFEARRAY(double)* | void |
| `ModelToView` | [in,out] Matrix: SAFEARRAY(double)* | void |
| `ViewToGLProjection` | [in,out] Matrix: SAFEARRAY(double)* | void |
| `Update` | — | void |
| `ShowDrawDC` | — | void |
| `SwapBuffers` | — | void |
| `get DrawDC` | — | int |
| `GetCamera` | [out] EyeX: double*, [out] EyeY: double*, [out] EyeZ: double*, [out] TargetX: double*, [out] TargetY: double*, [out] TargetZ: double*, [out] UpX: double*, [out] UpY: double*, [out] UpZ: double*, [out] Perspective: bool*, [out] ScaleOrAngle: double* | void |
| `BeginCameraDynamics` | — | void |
| `SetCamera` | EyeX: double, EyeY: double, EyeZ: double, TargetX: double, TargetY: double, TargetZ: double, UpX: double, UpY: double, UpZ: double, Perspective: bool, ScaleOrAngle: double | void |
| `EndCameraDynamics` | — | void |
| `RotateCamera` | Angle: double, CenterX: double, CenterY: double, CenterZ: double, AxisX: double, AxisY: double, AxisZ: double | void |
| `PanCamera` | dX: int, dY: int | void |
| `ZoomCamera` | __MIDL___IViewAuto0000: double | void |
| `OrientCamera` | cmdtype: int, X1: int, Y1: int, X2: int, Y2: int, X3: int, Y3: int | void |
| `get ViewEvents` | — | ViewEvents* |
| `get DisplayEvents` | — | DisplayEvents* |
| `get GLDisplayEvents` | — | GLDisplayEvents* |
| `get RenderEvents` | — | RenderEvents* |
| `get AnimationEvents` | — | AnimationEvents* |
| `get DisplayEnabled` | — | bool |
| `put DisplayEnabled` | p0: bool | void |
| `get CullingEnabled` | — | bool |
| `put CullingEnabled` | p0: bool | void |
| `get StyleFallbackEnabled` | — | bool |
| `put StyleFallbackEnabled` | p0: bool | void |
| `get SharpnessLevelCount` | — | int |
| `get SharpnessLevel` | — | int |
| `put SharpnessLevel` | p0: int | void |
| `get StereoEnabled` | — | bool |
| `put StereoEnabled` | p0: bool | void |
| `get StereoAngle` | — | double |
| `put StereoAngle` | p0: double | void |
| `get StereoDeviation` | — | double |
| `put StereoDeviation` | p0: double | void |
| `TransformModelToDC` | ModelX: double, ModelY: double, ModelZ: double, [out] DeviceX: int*, [out] DeviceY: int* | void |
| `TransformDCToModel` | DeviceX: int, DeviceY: int, [out] ModelX: double*, [out] ModelY: double*, [out] ModelZ: double* | void |
| `TransformModelToView` | ModelX: double, ModelY: double, ModelZ: double, [out] ViewX: double*, [out] ViewY: double*, [out] ViewZ: double* | void |
| `TransformViewToModel` | ViewX: double, ViewY: double, ViewZ: double, [out] ModelX: double*, [out] ModelY: double*, [out] ModelZ: double* | void |
| `TransformGLProjectionToView` | GLX: double, GLY: double, GLlZ: double, [out] ViewX: double*, [out] ViewY: double*, [out] ViewZ: double* | void |
| `TransformViewToGLProjection` | ViewX: double, ViewY: double, ViewZ: double, [out] GLX: double*, [out] GLY: double*, [out] GLZ: double* | void |
| `GetCounter` | Type: int, bReset: bool, [out] dCounter: double* | void |
| `get GDIBufferModified` | — | bool |
| `put GDIBufferModified` | p0: bool | void |
| `SaveAsImage` | Filename: BSTR, [opt]Width: VARIANT, [opt]Height: VARIANT, [opt]AltViewStyle: VARIANT, [opt]Resolution: VARIANT, [opt]ColorDepth: VARIANT, [opt]ImageQuality: SeImageQualityType, [opt]Invert: bool | void |
| `get ViewStyle` | — | IDispatch |
| `put ViewStyle` | p0: IDispatch | void |
| `get Style` | — | BSTR |
| `put Style` | p0: BSTR | void |
| `SetRenderMode` | mode: VARIANT | void |
| `get RenderModeType` | — | SeRenderModeType |
| `put RenderModeType` | p0: SeRenderModeType | void |
| `get SilhouettesEnabled` | — | bool |
| `put SilhouettesEnabled` | p0: bool | void |
| `get SectionPlanesEnabled` | — | bool |
| `put SectionPlanesEnabled` | p0: bool | void |
| `SetDisplayDepths` | dFront: double, dBack: double, [opt]FrontFaceStyle: VARIANT*, [opt]BackFaceStyle: VARIANT*, [opt]Monument: VARIANT* | void |
| `GetDisplayDepths` | [out] pdFront: double*, [out] pdBack: double*, [opt][out] FrontFaceStyle: VARIANT*, [opt][out] BackFaceStyle: VARIANT*, [opt][out] Monument: VARIANT* | void |
| `SetSectionPlanes` | nPlanes: int, [opt]Positions: VARIANT*, [opt]Normals: VARIANT*, [opt]FaceStyles: VARIANT* | void |
| `GetSectionPlanes` | [out] pnPlanes: int*, [opt][out] Positions: VARIANT*, [opt][out] Normals: VARIANT*, [opt][out] FaceStyles: VARIANT* | void |
| `SetAttribute` | Attribute: int, AttributeData: VARIANT | void |
| `GetAttribute` | Attribute: int, [out] AttributeData: VARIANT* | void |
| `ClearRotationFocus` | — | void |
| `GetRotationFocus` | [out] pdPointX: double*, [out] pdPointY: double*, [out] pdPointZ: double*, [out] pdDirectionX: double*, [out] pdDirectionZ: double*, [out] pdDirectionY: double*, [out] pdFront: double*, [out] pdBack: double*, [out] pdRadius: double*, [out] puOptions: int* | void |
| `SetRotationPoint` | dPointX: double, dPointY: double, dPointZ: double | void |
| `SetRotationAxis` | dPointX: double, dPointY: double, dPointZ: double, dDirectionX: double, dDirectionY: double, dDirectionZ: double | void |
| `SetRotationFocus` | dPointX: double, dPointY: double, dPointZ: double, dDirectionX: double, dDirectionZ: double, dDirectionY: double, dFront: double, dBack: double, dRadius: double, uOptions: int | void |
| `Locate` | lPointX: int, lPointY: int, lRadius: int, [out] pdHitPointX: double*, [out] pdHitPointY: double*, [out] pdHitPointZ: double* | void |
| `GetModelRange` | [out] pdLowX: double*, [out] pdLowY: double*, [out] pdLowZ: double*, [out] pdHighX: double*, [out] pdHighY: double*, [out] pdHighZ: double* | void |
| `OrientCameraEx` | lFlags: int, lButtons: int, dX: double, dY: double, dZ: double, dYaw: double, dPitch: double, dRoll: double | void |
| `GetCameraEx` | [out] lFlags: int*, [out] dEyeX: double*, [out] dEyeY: double*, [out] dEyeZ: double*, [out] dTargetX: double*, [out] dTargetY: double*, [out] dTargetZ: double*, [out] dUpX: double*, [out] dUpY: double*, [out] dUpZ: double*, [out] dNearClip: double*, [out] dFarClip: double*, [out] dFrameWidth: double*, [out] dFrameHeight: double*, [out] dFrameEyeX: double*, [out] dFrameEyeY: double*, [out] dFrameScale: double* | void |
| `SetCameraEx` | lFlags: int, dEyeX: double, dEyeY: double, dEyeZ: double, dTargetX: double, dTargetY: double, dTargetZ: double, dUpX: double, dUpY: double, dUpZ: double, dNearClip: double, dFarClip: double, dFrameWidth: double, dFrameHeight: double, dFrameEyeX: double, dFrameEyeY: double, dFrameScale: double | void |
| `SaveCurrentView` | Name: VARIANT | void |
| `ApplyNamedView` | Name: VARIANT | void |
| `AreaZoomCamera` | X1: int, Y1: int, X2: int, Y2: int | void |
| `CreateUserRange` | [out] pidUserRange: int* | void |
| `DeleteUserRange` | idUserRange: int | void |
| `GetUserRange` | idUserRange: int, [out] pdLowX: double*, [out] pdLowY: double*, [out] pdLowZ: double*, [out] pdHighX: double*, [out] pdHighY: double*, [out] pdHighZ: double* | void |
| `SetUserRange` | idUserRange: int, dLowX: double, dLowY: double, dLowZ: double, dHighX: double, dHighY: double, dHighZ: double | void |
| `get MovieFrameRate` | — | uint |
| `put MovieFrameRate` | p0: uint | void |
| `get MovieBitRate` | — | uint |
| `put MovieBitRate` | p0: uint | void |
| `get MovieCodec` | — | BSTR |
| `put MovieCodec` | p0: BSTR | void |
| `get MovieQuality` | — | uint |
| `put MovieQuality` | p0: uint | void |
| `get MovieTitle` | — | BSTR |
| `put MovieTitle` | p0: BSTR | void |
| `get MovieSubTitle` | — | BSTR |
| `put MovieSubTitle` | p0: BSTR | void |
| `get MovieCopyright` | — | BSTR |
| `put MovieCopyright` | p0: BSTR | void |
| `get MovieAuthor` | — | BSTR |
| `put MovieAuthor` | p0: BSTR | void |
| `get MovieAuthorURL` | — | BSTR |
| `put MovieAuthorURL` | p0: BSTR | void |
| `get MovieDescription` | — | BSTR |
| `put MovieDescription` | p0: BSTR | void |
| `GetAvailableMovieCodecs` | [out] AvailableCodecs: SAFEARRAY(BSTR)* | void |
| `SetMovieResolution` | StandardMovieResolution: seMovieStandardResolutionConstants | void |
| `SetCustomMovieResolution` | nWidth: int, nHeight: int | void |
| `CreateMovieRecorder` | Format: seMovieFormatConstants | void |
| `DestroyMovieRecorder` | — | void |
| `BeginMovieRecording` | Filename: BSTR | void |
| `AddFrameToMovie` | KeyFrame: bool, [out] pNewFrameCount: int* | void |
| `EndMovieRecording` | — | void |
| `RangeZoomCamera` | dLowX: double, dLowY: double, dLowZ: double, dHighX: double, dHighY: double, dHighZ: double | void |
| `UserRangeZoomCamera` | idUserRange: int | void |
| `RefreshView` | nOptions: int | void |
| `get SharpenLevel` | — | seSharpenLevelConstants |
| `put SharpenLevel` | p0: seSharpenLevelConstants | void |
| `get SectionPlanesOptions` | — | VARIANT |
| `put SectionPlanesOptions` | p0: VARIANT | void |
| `SetSectionPlanesParams` | Options: VARIANT, PlaneCount: VARIANT, [opt]Positions: VARIANT*, [opt]Normals: VARIANT*, [opt]Colors: VARIANT* | void |
| `GetSectionPlanesParams` | [out] Options: VARIANT*, [opt][out] PlaneCount: VARIANT*, [opt][out] Positions: VARIANT*, [opt][out] Normals: VARIANT*, [opt][out] Colors: VARIANT* | void |

### <a name="window"></a>`Window`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `QueryInterface` | riid: GUID*, [out] ppvObj: void** | void |
| `AddRef` | — | uint |
| `Release` | — | uint |
| `Activate` | — | void |
| `ActivateNext` | — | void |
| `ActivatePrevious` | — | void |
| `get Application` | — | Application* |
| `get Caption` | — | BSTR |
| `put Caption` | p0: BSTR | void |
| `Close` | [opt]SaveChanges: VARIANT, [opt]Filename: VARIANT, [opt]RouteWorkbook: VARIANT | void |
| `get Environment` | — | BSTR |
| `put Environment` | p0: BSTR | void |
| `get Height` | — | int |
| `put Height` | p0: int | void |
| `get hWnd` | — | int |
| `get Index` | — | int |
| `get Left` | — | int |
| `put Left` | p0: int | void |
| `get Parent` | — | IDispatch |
| `PrintOut` | — | void |
| `get SelectSet` | — | SelectSet* |
| `get Top` | — | int |
| `put Top` | p0: int | void |
| `get Type` | — | BSTR |
| `get Visible` | — | bool |
| `put Visible` | p0: bool | void |
| `get Width` | — | int |
| `put Width` | p0: int | void |
| `get WindowNumber` | — | int |
| `get WindowState` | — | int |
| `put WindowState` | p0: int | void |
| `get Icon` | — | int |
| `Paste` | — | void |
| `get UsableHeight` | — | int |
| `get UsableWidth` | — | int |
| `get View` | — | View* |
| `get DrawHwnd` | — | int |
| `get AttributeSets` | — | IDispatch |
| `get IsAttributeSetPresent` | Name: BSTR | bool |
| `get Floating` | — | bool |
| `FloatWindow` | — | void |
| `DockWindow` | — | void |

### <a name="diseviewevents"></a>`DISEViewEvents`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `Changed` | — | void |
| `Destroyed` | — | void |
| `StyleChanged` | — | void |

### <a name="disehdcdisplayevents"></a>`DISEhDCDisplayEvents`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `BeginDisplay` | — | void |
| `EndDisplay` | — | void |
| `BeginhDCMainDisplay` | hDC: int, ModelToDC: SAFEARRAY(double)*, Rect: SAFEARRAY(int)* | void |
| `EndhDCMainDisplay` | hDC: int, ModelToDC: SAFEARRAY(double)*, Rect: SAFEARRAY(int)* | void |

### <a name="diseanimationevents"></a>`DISEAnimationEvents`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `QueryInterface` | riid: GUID*, [out] ppvObj: void** | void |
| `AddRef` | — | uint |
| `Release` | — | uint |
| `AnimationEvent` | AnimationEventType: AnimationEventConstants, nFrame: int | void |

### <a name="namedview"></a>`NamedView`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `QueryInterface` | riid: GUID*, [out] ppvObj: void** | void |
| `AddRef` | — | uint |
| `Release` | — | uint |
| `get Name` | — | BSTR |
| `put Name` | p0: BSTR | void |
| `get Description` | — | BSTR |
| `put Description` | p0: BSTR | void |
| `GetCamera` | [out] EyeX: double*, [out] EyeY: double*, [out] EyeZ: double*, [out] TargetX: double*, [out] TargetY: double*, [out] TargetZ: double*, [out] UpX: double*, [out] UpY: double*, [out] UpZ: double*, [out] Perspective: bool*, [out] ScaleOrAngle: double* | void |
| `SetCamera` | EyeX: double, EyeY: double, EyeZ: double, TargetX: double, TargetY: double, TargetZ: double, UpX: double, UpY: double, UpZ: double, Perspective: bool, ScaleOrAngle: double | void |
| `GetCameraEx` | [out] plFlags: int*, [out] pdEyeX: double*, [out] pdEyeY: double*, [out] pdEyeZ: double*, [out] pdTargetX: double*, [out] pdTargetY: double*, [out] pdTargetZ: double*, [out] pdUpX: double*, [out] pdUpY: double*, [out] pdUpZ: double*, [out] pdNearClip: double*, [out] pdFarClip: double*, [out] pdFrameWidth: double*, [out] pdFrameHeight: double*, [out] pdFrameEyeX: double*, [out] pdFrameEyeY: double*, [out] pdFrameScale: double* | void |
| `SetCameraEx` | lFlags: int, dEyeX: double, dEyeY: double, dEyeZ: double, dTargetX: double, dTargetY: double, dTargetZ: double, dUpX: double, dUpY: double, dUpZ: double, dNearClip: double, dFarClip: double, dFrameWidth: double, dFrameHeight: double, dFrameEyeX: double, dFrameEyeY: double, dFrameScale: double | void |

### <a name="unitofmeasure"></a>`UnitOfMeasure`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `QueryInterface` | riid: GUID*, [out] ppvObj: void** | void |
| `AddRef` | — | uint |
| `Release` | — | uint |
| `get Type` | — | UnitTypeConstants |
| `get Units` | — | int |
| `put Units` | p0: int | void |
| `get Precision` | — | int |
| `put Precision` | p0: int | void |

### <a name="commandbarpopup"></a>`CommandBarPopup`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `QueryInterface` | riid: GUID*, [out] ppvObj: void** | void |
| `AddRef` | — | uint |
| `Release` | — | uint |
| `get Application` | — | IDispatch |
| `get BeginGroup` | — | bool |
| `put BeginGroup` | p0: bool | void |
| `get BuiltIn` | — | bool |
| `get BuiltInFace` | — | bool |
| `put BuiltInFace` | p0: bool | void |
| `get Caption` | — | BSTR |
| `put Caption` | p0: BSTR | void |
| `get DescriptionText` | — | BSTR |
| `put DescriptionText` | p0: BSTR | void |
| `get Enabled` | — | bool |
| `put Enabled` | p0: bool | void |
| `get FaceId` | — | int |
| `put FaceId` | p0: int | void |
| `get Height` | — | int |
| `get HelpContextId` | — | int |
| `put HelpContextId` | p0: int | void |
| `get HelpFile` | — | BSTR |
| `put HelpFile` | p0: BSTR | void |
| `get Id` | — | int |
| `get Index` | — | int |
| `get Left` | — | int |
| `get OnAction` | — | BSTR |
| `put OnAction` | p0: BSTR | void |
| `get ParameterText` | — | BSTR |
| `put ParameterText` | p0: BSTR | void |
| `get Parent` | — | CommandBar* |
| `get ShortcutText` | — | BSTR |
| `put ShortcutText` | p0: BSTR | void |
| `get Tag` | — | BSTR |
| `put Tag` | p0: BSTR | void |
| `get TooltipText` | — | BSTR |
| `put TooltipText` | p0: BSTR | void |
| `get Top` | — | int |
| `get Type` | — | SeControlType |
| `get Visible` | — | bool |
| `put Visible` | p0: bool | void |
| `get Width` | — | int |
| `Delete` | [opt]Temporary: VARIANT | void |
| `Execute` | — | void |
| `Help` | — | void |
| `LoadFace` | Face: BSTR | void |
| `get CommandBar` | — | CommandBar* |
| `get Controls` | — | CommandBarControls* |

### <a name="disebendtableevents"></a>`DISEBendTableEvents`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `BendTableStart` | — | HRESULT |
| `BendTableEnd` | — | HRESULT |
| `BendSelect` | BendIndex: int, ColumnId: int | HRESULT |
| `BendUserDataChanged` | BendIndex: int, ColumnId: int | HRESULT |

### <a name="diseassemblychangeevents"></a>`DISEAssemblyChangeEvents`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `QueryInterface` | riid: GUID*, [out] ppvObj: void** | void |
| `AddRef` | — | uint |
| `Release` | — | uint |
| `BeforeChange` | theDocument: IDispatch, Object: IDispatch, Type: ObjectType, ChangeType: seAssemblyChangeEventsConstants | void |
| `AfterChange` | theDocument: IDispatch, Object: IDispatch, Type: ObjectType, ChangeType: seAssemblyChangeEventsConstants | void |

### <a name="diseassemblyconfigurationchangeevents"></a>`DISEAssemblyConfigurationChangeEvents`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `QueryInterface` | riid: GUID*, [out] ppvObj: void** | void |
| `AddRef` | — | uint |
| `Release` | — | uint |
| `OnBeforeAssemblyConfigurationChange` | theDocument: IDispatch, varConfigNames: VARIANT*, nConfigNameCount: int | void |
| `OnAfterAssemblyConfigurationChange` | theDocument: IDispatch, varConfigNames: VARIANT*, nConfigNameCount: int | void |

### <a name="diseassemblyrecomputeevents"></a>`DISEAssemblyRecomputeEvents`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `QueryInterface` | riid: GUID*, [out] ppvObj: void** | void |
| `AddRef` | — | uint |
| `Release` | — | uint |
| `BeforeRecompute` | theDocument: IDispatch | void |
| `AfterRecompute` | theDocument: IDispatch | void |
| `AfterAdd` | theDocument: IDispatch, Object: IDispatch, Type: ObjectType | void |
| `BeforeDelete` | theDocument: IDispatch, Object: IDispatch, Type: ObjectType | void |
| `AfterModify` | theDocument: IDispatch, Object: IDispatch, Type: ObjectType, ModifyType: seAssemblyEventConstants | void |

### <a name="disedocumentevents"></a>`DISEDocumentEvents`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `BeforeClose` | — | void |
| `BeforeSave` | — | void |
| `AfterSave` | — | void |
| `SelectSetChanged` | SelectSet: IDispatch | void |

### <a name="diseassemblyphysicalpropertieschangeevents"></a>`DISEAssemblyPhysicalPropertiesChangeEvents`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `QueryInterface` | riid: GUID*, [out] ppvObj: void** | void |
| `AddRef` | — | uint |
| `Release` | — | uint |
| `OnAfterAssemblyPhysicalPropertiesChange` | theDocument: IDispatch | void |
| `OnBeforeAssemblyPhysicalPropertiesChange` | theDocument: IDispatch | void |

### <a name="diseaddineventsex"></a>`DISEAddInEventsEx`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `OnCommand` | nCmdID: int | void |
| `OnCommandHelp` | hFrameWnd: int, uHelpCommand: int, nCmdID: int | void |
| `OnCommandUpdateUI` | nCmdID: int, [in,out] lCmdFlags: int*, [out] MenuItemText: BSTR*, [in,out] nIDBitmap: int* | void |
| `OnCommandOnLineHelp` | uHelpCommand: int, nCmdID: int, [out] HelpURL: BSTR* | void |

### <a name="diseaddineventsex2"></a>`DISEAddInEventsEx2`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `OnCommand` | nCmdID: int, Context: int, ActiveDocumentType: int, pActiveDocument: IDispatch, pActiveWindow: IDispatch, pActiveSelectSet: IDispatch | void |
| `OnCommandHelp` | hFrameWnd: int, uHelpCommand: int, nCmdID: int | void |
| `OnCommandUpdateUI` | nCmdID: int, Context: int, ActiveDocumentType: int, pActiveDocument: IDispatch, pActiveWindow: IDispatch, pActiveSelectSet: IDispatch, [in,out] lCmdFlags: int*, [out] MenuItemText: BSTR*, [in,out] nIDBitmap: int* | void |
| `OnCommandOnLineHelp` | uHelpCommand: int, nCmdID: int, [out] HelpURL: BSTR* | void |

### <a name="diseassemblyfamilyevents"></a>`DISEAssemblyFamilyEvents`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `QueryInterface` | riid: GUID*, [out] ppvObj: void** | void |
| `AddRef` | — | uint |
| `Release` | — | uint |
| `BeforeMemberActivate` | theDocument: IDispatch, memberName: BSTR | void |
| `AfterMemberActivate` | theDocument: IDispatch, memberName: BSTR | void |
| `BeforeMemberCreate` | theDocument: IDispatch, memberName: BSTR | void |
| `AfterMemberCreate` | theDocument: IDispatch, memberName: BSTR | void |
| `BeforeMemberDelete` | theDocument: IDispatch, memberName: BSTR | void |
| `AfterMemberDelete` | theDocument: IDispatch, memberName: BSTR | void |

### <a name="diseassemblyfamilyevents2"></a>`DISEAssemblyFamilyEvents2`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `QueryInterface` | riid: GUID*, [out] ppvObj: void** | void |
| `AddRef` | — | uint |
| `Release` | — | uint |
| `BeforeMemberActivate` | theDocument: IDispatch, memberName: BSTR | void |
| `AfterMemberActivate` | theDocument: IDispatch, memberName: BSTR | void |
| `BeforeMemberCreate` | theDocument: IDispatch, memberName: BSTR | void |
| `AfterMemberCreate` | theDocument: IDispatch, memberName: BSTR | void |
| `BeforeMemberDelete` | theDocument: IDispatch, memberName: BSTR | void |
| `AfterMemberDelete` | theDocument: IDispatch, memberName: BSTR | void |
| `BeforeMemberRename` | theDocument: IDispatch, OldMemberName: BSTR | void |
| `AfterMemberRename` | theDocument: IDispatch, NewMemberName: BSTR | void |

### <a name="disesketchrecomputeevents"></a>`DISESketchRecomputeEvents`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `QueryInterface` | riid: GUID*, [out] ppvObj: void** | void |
| `AddRef` | — | uint |
| `Release` | — | uint |
| `BeforeRecompute` | Sketch: IDispatch | void |
| `AfterRecompute` | Sketch: IDispatch | void |
| `AfterSketchIsModified` | ModifySkFlag: SeModifySketchFlag, Entity: IDispatch, Sketch: IDispatch | void |
| `BeforeSketchIsDeleted` | — | void |

### <a name="layer"></a>`Layer`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `QueryInterface` | riid: GUID*, [out] ppvObj: void** | void |
| `AddRef` | — | uint |
| `Release` | — | uint |
| `get Application` | — | Application* |
| `get Parent` | — | IDispatch |
| `Delete` | — | void |
| `get IsEmpty` | — | bool |
| `get Key` | — | BSTR |
| `put Name` | p0: BSTR | void |
| `get Name` | — | BSTR |
| `put Description` | p0: BSTR | void |
| `get Description` | — | BSTR |
| `Activate` | — | void |
| `get Show` | — | bool |
| `put Show` | p0: bool | void |
| `get Locatable` | — | bool |
| `put Locatable` | p0: bool | void |
| `ShowInContext` | Context: IDispatch | void |
| `HideInContext` | Context: IDispatch | void |
| `MakeLocatableInContext` | Context: IDispatch | void |
| `MakeNonLocatableInContext` | Context: IDispatch | void |
| `ActivateInContext` | Context: IDispatch | void |
| `IsShownInContext` | Context: IDispatch | bool |
| `IsLocatableInContext` | Context: IDispatch | bool |
| `ShowOnly` | — | void |
| `ShowOnlyInContext` | Context: IDispatch | void |
| `DeleteLayerAndObjects` | — | void |
| `ShowEverywhere` | — | void |
| `HideEverywhere` | — | void |
| `MoveAllObjectsToLayer` | NewLayerDispatch: IDispatch | void |
| `get AttributeSets` | — | IDispatch |
| `get IsAttributeSetPresent` | Name: BSTR | bool |

### <a name="linearstyle"></a>`LinearStyle`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `QueryInterface` | riid: GUID*, [out] ppvObj: void** | void |
| `AddRef` | — | uint |
| `Release` | — | uint |
| `put Name` | p0: BSTR | void |
| `get Name` | — | BSTR |
| `put Parent` | p0: BSTR | void |
| `get Parent` | — | BSTR |
| `get Description` | — | BSTR |
| `put Units` | p0: StyleUnitsConstant | void |
| `get Units` | — | StyleUnitsConstant |
| `put Color` | p0: int | void |
| `get Color` | — | int |
| `put Width` | p0: double | void |
| `get Width` | — | double |
| `SetDashGap` | nCount: int, dDashGap: SAFEARRAY(double)*, fAutoPhase: bool | void |
| `get DashGapCount` | — | int |
| `GetDashGap` | [out] pnCount: int*, [out] dDashGap: SAFEARRAY(double)*, [out] pfAutoPhase: bool* | void |
| `put DashType` | p0: BSTR | void |
| `get DashType` | — | BSTR |
| `get AttributeSets` | — | IDispatch |
| `get IsAttributeSetPresent` | Name: BSTR | bool |

### <a name="fillstyle"></a>`FillStyle`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `QueryInterface` | riid: GUID*, [out] ppvObj: void** | void |
| `AddRef` | — | uint |
| `Release` | — | uint |
| `put Name` | p0: BSTR | void |
| `get Name` | — | BSTR |
| `get Description` | — | BSTR |
| `put PatternName` | p0: BSTR | void |
| `get PatternName` | — | BSTR |
| `get PatternType` | — | int |
| `put Color` | p0: int | void |
| `get Color` | — | int |
| `put FillBackground` | p0: int | void |
| `get FillBackground` | — | int |
| `put FillColor` | p0: int | void |
| `get FillColor` | — | int |
| `put Rotation` | p0: double | void |
| `get Rotation` | — | double |
| `put Spacing` | p0: double | void |
| `get Spacing` | — | double |
| `put Scale` | p0: double | void |
| `get Scale` | — | double |
| `put Units` | p0: int | void |
| `get Units` | — | int |
| `put Parent` | p0: BSTR | void |
| `get Parent` | — | BSTR |
| `put Active` | p0: BSTR | void |
| `get Active` | — | BSTR |

### <a name="hatchpatternstyle"></a>`HatchPatternStyle`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `QueryInterface` | riid: GUID*, [out] ppvObj: void** | void |
| `AddRef` | — | uint |
| `Release` | — | uint |
| `put Name` | p0: BSTR | void |
| `get Name` | — | BSTR |
| `put Parent` | p0: BSTR | void |
| `get Parent` | — | BSTR |
| `put Units` | p0: int | void |
| `get Units` | — | int |
| `get Count` | — | int |
| `AddHatch` | dRotation: double, dXOrigin: double, dYOrigin: double, dSpacing: double, dShift: double, nColor: int, dWidth: double, DashTypeName: BSTR | int |
| `GetHatch` | nDisplayIndex: int, [out] lpdRotation: double*, [out] lpdXOrigin: double*, [out] lpdYOrigin: double*, [out] lpdSpacing: double*, [out] lpdShift: double*, [out] lpnColor: int*, [out] lpdWidth: double*, [out] DashTypeName: BSTR* | void |
| `SetHatch` | nDisplayIndex: int, dRotation: double, dXOrigin: double, dYOrigin: double, dSpacing: double, dShift: double, nColor: int, dWidth: double, DashTypeName: BSTR | void |
| `RemoveHatch` | nDisplayIndex: int | void |
| `SetRotation` | nDisplayIndex: int, dRotation: double | void |
| `GetRotation` | nDisplayIndex: int | double |
| `SetOrigin` | nDisplayIndex: int, dX: double, dY: double | void |
| `GetOrigin` | nDisplayIndex: int, [out] lpdX: double*, [out] lpdY: double* | void |
| `SetSpacing` | nDisplayIndex: int, dSpacing: double | void |
| `GetSpacing` | nDisplayIndex: int | double |
| `SetShift` | nDisplayIndex: int, dShift: double | void |
| `GetShift` | nDisplayIndex: int | double |
| `SetColor` | nDisplayIndex: int, nColor: int | void |
| `GetColor` | nDisplayIndex: int | int |
| `SetWidth` | nDisplayIndex: int, dWidth: double | void |
| `GetWidth` | nDisplayIndex: int | double |
| `SetDashType` | nDisplayIndex: int, DashTypeName: BSTR | void |
| `GetDashType` | nDisplayIndex: int | BSTR |
| `SetDisplayIndex` | nCurrentIndex: int, nNewIndex: int | void |
| `SetDashGap` | nDisplayIndex: int, nCount: int, dDashGap: SAFEARRAY(double)* | void |
| `get DashGapCount` | nDisplayIndex: int | int |
| `GetDashGap` | nDisplayIndex: int, [out] pnCount: int*, [out] dDashGap: SAFEARRAY(double)* | void |
| `get AttributeSets` | — | IDispatch |
| `get IsAttributeSetPresent` | Name: BSTR | bool |
| `put MasterRotation` | p0: double | void |
| `get MasterRotation` | — | double |
| `put MasterScale` | p0: double | void |
| `get MasterScale` | — | double |
| `SetMasterColor` | nColor: int | void |
| `SetMasterWidth` | dWidth: double | void |
| `AddHatchWithOption` | dRotation: double, dXOrigin: double, dYOrigin: double, dSpacing: double, dShift: double, nColor: int, dWidth: double, DashTypeName: BSTR, elementType: HatchElementType, ellipseCenterLocation: RadialHatchElementCenterLocation, dEllipseAxisRatio: double | int |
| `GetHatchWithOption` | nDisplayIndex: int, [out] lpdRotation: double*, [out] lpdXOrigin: double*, [out] lpdYOrigin: double*, [out] lpdSpacing: double*, [out] lpdShift: double*, [out] lpnColor: int*, [out] lpdWidth: double*, [out] DashTypeName: BSTR*, [out] elementType: HatchElementType*, [out] ellipseCenterLocation: RadialHatchElementCenterLocation*, [out] dEllipseAxisRatio: double* | void |
| `SetHatchWithOption` | nDisplayIndex: int, dRotation: double, dXOrigin: double, dYOrigin: double, dSpacing: double, dShift: double, nColor: int, dWidth: double, DashTypeName: BSTR, elementType: HatchElementType, ellipseCenterLocation: RadialHatchElementCenterLocation, dEllipseAxisRatio: double | void |
| `SetElementType` | nDisplayIndex: int, elementType: HatchElementType | void |
| `GetElementType` | nDisplayIndex: int | HatchElementType |
| `SetRadialElementCenterLocation` | nDisplayIndex: int, ellipseCenterLocation: RadialHatchElementCenterLocation | void |
| `GetRadialElementCenterLocation` | nDisplayIndex: int | RadialHatchElementCenterLocation |
| `SetRadialElementAxisRatio` | nDisplayIndex: int, dEllipseAxisRatio: double | void |
| `GetRadialElementAxisRatio` | nDisplayIndex: int | double |
| `SetSourceColor` | nColor: int | void |
| `SetSourceWidth` | dWidth: double | void |
| `put SourceRotation` | p0: double | void |
| `get SourceRotation` | — | double |
| `put SourceScale` | p0: double | void |
| `get SourceScale` | — | double |

### <a name="dashstyle"></a>`DashStyle`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `QueryInterface` | riid: GUID*, [out] ppvObj: void** | void |
| `AddRef` | — | uint |
| `Release` | — | uint |
| `put Name` | p0: BSTR | void |
| `get Name` | — | BSTR |
| `put Units` | p0: int | void |
| `get Units` | — | int |
| `get DashGapCount` | — | int |
| `SetDashGap` | nCount: int, dDashGap: SAFEARRAY(double)* | void |
| `GetDashGap` | [out] pnCount: int*, [out] dDashGap: SAFEARRAY(double)* | void |
| `put Center` | p0: bool | void |
| `get Center` | — | bool |
| `put PercentStartEndDash` | p0: double | void |
| `get PercentStartEndDash` | — | double |
| `get AttributeSets` | — | IDispatch |
| `get IsAttributeSetPresent` | Name: BSTR | bool |

### <a name="facestyle"></a>`FaceStyle`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `QueryInterface` | riid: GUID*, [out] ppvObj: void** | void |
| `AddRef` | — | uint |
| `Release` | — | uint |
| `get StyleName` | — | BSTR |
| `put StyleName` | p0: BSTR | void |
| `get Parent` | — | BSTR |
| `put Parent` | p0: BSTR | void |
| `get Type` | — | int |
| `put Type` | p0: int | void |
| `get Flags` | — | int |
| `put Flags` | p0: int | void |
| `get WireframeColorRed` | — | float |
| `put WireframeColorRed` | p0: float | void |
| `get WireframeColorGreen` | — | float |
| `put WireframeColorGreen` | p0: float | void |
| `get WireframeColorBlue` | — | float |
| `put WireframeColorBlue` | p0: float | void |
| `get StipplePattern` | — | int |
| `put StipplePattern` | p0: int | void |
| `get StippleScale` | — | short |
| `put StippleScale` | p0: short | void |
| `get LineWidth` | — | float |
| `put LineWidth` | p0: float | void |
| `get WidthSpace` | — | short |
| `put WidthSpace` | p0: short | void |
| `get DiffuseRed` | — | float |
| `put DiffuseRed` | p0: float | void |
| `get DiffuseGreen` | — | float |
| `put DiffuseGreen` | p0: float | void |
| `get DiffuseBlue` | — | float |
| `put DiffuseBlue` | p0: float | void |
| `get SpecularRed` | — | float |
| `put SpecularRed` | p0: float | void |
| `get SpecularGreen` | — | float |
| `put SpecularGreen` | p0: float | void |
| `get SpecularBlue` | — | float |
| `put SpecularBlue` | p0: float | void |
| `get AmbientRed` | — | float |
| `put AmbientRed` | p0: float | void |
| `get AmbientGreen` | — | float |
| `put AmbientGreen` | p0: float | void |
| `get AmbientBlue` | — | float |
| `put AmbientBlue` | p0: float | void |
| `get EmissionRed` | — | float |
| `put EmissionRed` | p0: float | void |
| `get EmissionGreen` | — | float |
| `put EmissionGreen` | p0: float | void |
| `get EmissionBlue` | — | float |
| `put EmissionBlue` | p0: float | void |
| `get Shininess` | — | float |
| `put Shininess` | p0: float | void |
| `get Opacity` | — | float |
| `put Opacity` | p0: float | void |
| `get Reflectivity` | — | float |
| `put Reflectivity` | p0: float | void |
| `get Refraction` | — | float |
| `put Refraction` | p0: float | void |
| `get CastsShadows` | — | int |
| `put CastsShadows` | p0: int | void |
| `get AcceptsShadows` | — | int |
| `put AcceptsShadows` | p0: int | void |
| `get TextureFileName` | — | BSTR |
| `put TextureFileName` | p0: BSTR | void |
| `get TextureTransparent` | — | int |
| `put TextureTransparent` | p0: int | void |
| `get TextureTransparentColorRed` | — | float |
| `put TextureTransparentColorRed` | p0: float | void |
| `get TextureTransparentColorGreen` | — | float |
| `put TextureTransparentColorGreen` | p0: float | void |
| `get TextureTransparentColorBlue` | — | float |
| `put TextureTransparentColorBlue` | p0: float | void |
| `get TextureUnits` | — | int |
| `put TextureUnits` | p0: int | void |
| `get TextureScaleX` | — | float |
| `put TextureScaleX` | p0: float | void |
| `get TextureScaleY` | — | float |
| `put TextureScaleY` | p0: float | void |
| `get TextureOffsetX` | — | float |
| `put TextureOffsetX` | p0: float | void |
| `get TextureOffsetY` | — | float |
| `put TextureOffsetY` | p0: float | void |
| `get TextureMirrorX` | — | int |
| `put TextureMirrorX` | p0: int | void |
| `get TextureMirrorY` | — | int |
| `put TextureMirrorY` | p0: int | void |
| `get TextureRotation` | — | float |
| `put TextureRotation` | p0: float | void |
| `get TextureWeight` | — | float |
| `put TextureWeight` | p0: float | void |
| `get BumpmapFileName` | — | BSTR |
| `put BumpmapFileName` | p0: BSTR | void |
| `get BumpmapUnits` | — | int |
| `put BumpmapUnits` | p0: int | void |
| `get BumpmapScaleX` | — | float |
| `put BumpmapScaleX` | p0: float | void |
| `get BumpmapScaleY` | — | float |
| `put BumpmapScaleY` | p0: float | void |
| `get BumpmapOffsetX` | — | float |
| `put BumpmapOffsetX` | p0: float | void |
| `get BumpmapOffsetY` | — | float |
| `put BumpmapOffsetY` | p0: float | void |
| `get BumpmapMirrorX` | — | int |
| `put BumpmapMirrorX` | p0: int | void |
| `get BumpmapMirrorY` | — | int |
| `put BumpmapMirrorY` | p0: int | void |
| `get BumpmapRotation` | — | float |
| `put BumpmapRotation` | p0: float | void |
| `get BumpmapHeight` | — | float |
| `put BumpmapHeight` | p0: float | void |
| `get BumpmapInvert` | — | int |
| `put BumpmapInvert` | p0: int | void |
| `get SkyboxType` | — | SeSkyboxType |
| `put SkyboxType` | p0: SeSkyboxType | void |
| `get SkyboxAzimuth` | — | float |
| `put SkyboxAzimuth` | p0: float | void |
| `get SkyboxAltitude` | — | float |
| `put SkyboxAltitude` | p0: float | void |
| `get SkyboxRoll` | — | float |
| `put SkyboxRoll` | p0: float | void |
| `get SkyboxConeAngle` | — | float |
| `put SkyboxConeAngle` | p0: float | void |
| `get StyleID` | — | int |
| `BeginPropertyBuffer` | — | void |
| `FlushPropertyBuffer` | — | void |
| `HasWireframeProperties` | [out] pbResult: int* | void |
| `HasSurfaceProperties` | [out] pbResult: int* | void |
| `ClearWireframeProperties` | — | void |
| `ClearSurfaceProperties` | — | void |
| `GetWireframeColor` | [out] pfRed: float*, [out] pfGreen: float*, [out] pfBlue: float* | void |
| `SetWireframeColor` | fRed: float, fGreen: float, fBlue: float | void |
| `GetDiffuse` | [out] pfRed: float*, [out] pfGreen: float*, [out] pfBlue: float* | void |
| `SetDiffuse` | fRed: float, fGreen: float, fBlue: float | void |
| `GetSpecular` | [out] pfRed: float*, [out] pfGreen: float*, [out] pfBlue: float* | void |
| `SetSpecular` | fRed: float, fGreen: float, fBlue: float | void |
| `GetAmbient` | [out] pfRed: float*, [out] pfGreen: float*, [out] pfBlue: float* | void |
| `SetAmbient` | fRed: float, fGreen: float, fBlue: float | void |
| `GetEmission` | [out] pfRed: float*, [out] pfGreen: float*, [out] pfBlue: float* | void |
| `SetEmission` | fRed: float, fGreen: float, fBlue: float | void |
| `Delete` | — | void |
| `Detach` | — | void |
| `GetTextureTransparentColor` | [out] pfRed: float*, [out] pfGreen: float*, [out] pfBlue: float* | void |
| `SetTextureTransparentColor` | fRed: float, fGreen: float, fBlue: float | void |
| `GetTextureScale` | [out] pfXScale: float*, [out] pfYScale: float* | void |
| `SetTextureScale` | fXScale: float, fYScale: float | void |
| `GetTextureOffset` | [out] pfXOffset: float*, [out] pfYOffset: float* | void |
| `SetTextureOffset` | fXOffset: float, fYOffset: float | void |
| `GetBumpmapScale` | [out] pfXScale: float*, [out] pfYScale: float* | void |
| `SetBumpmapScale` | fXScale: float, fYScale: float | void |
| `GetBumpmapOffset` | [out] pfXOffset: float*, [out] pfYOffset: float* | void |
| `SetBumpmapOffset` | fXOffset: float, fYOffset: float | void |
| `SetSkyboxSkyFile` | sFilename: BSTR | void |
| `SetSkyboxSideFilename` | nSide: int, sFilename: BSTR | void |
| `GetSkyboxSideFilename` | nSide: int | BSTR |
| `SkyboxClear` | nSide: int | void |
| `SkyboxClearAll` | — | void |
| `GetSkyboxOrientation` | [out] pfxDirection: float*, [out] pfyDirection: float*, [out] pfzDirection: float*, [out] pfxUp: float*, [out] pfyUp: float*, [out] pfzUp: float*, [out] pfFieldOfView: float* | void |
| `SetSkyboxOrientation` | fxDirection: float, fyDirection: float, fzDirection: float, fxUp: float, fyUp: float, fzUp: float, fFieldOfView: float | void |
| `GetVersion` | eVersion: int | int |
| `SetVersion` | eVersion: int, nVersion: int | void |
| `GetShaderData` | [out] pnId: int*, [out] peType: int*, [out] pnHints: int* | void |
| `SetShaderData` | eType: int, nHints: int | void |
| `get AutomaticShaderType` | — | int |
| `get ShaderType` | — | int |
| `put ShaderType` | p0: int | void |
| `get AttributeSets` | — | IDispatch |
| `get IsAttributeSetPresent` | Name: BSTR | bool |
| `ResetSkyboxOrientation` | — | void |
| `DeleteSkybox` | — | void |
| `get RenderModeType` | — | SeRenderModeType |
| `put RenderModeType` | p0: SeRenderModeType | void |
| `HasPointProperties` | [out] pbResult: int* | void |
| `ClearPointProperties` | — | void |
| `get PointSize` | — | float |
| `put PointSize` | p0: float | void |
| `get PointSizeSpace` | — | SeRenderSpaceType |
| `put PointSizeSpace` | p0: SeRenderSpaceType | void |
| `GetPointOptions` | [out] peShape: SeRenderShapeType*, [out] peFillMode: SeRenderFillMode*, [out] peShadeMode: SeRenderShadeMode* | void |
| `SetPointOptions` | eShape: SeRenderShapeType, eFillMode: SeRenderFillMode, eShadeMode: SeRenderShadeMode | void |
| `GetPointColor` | [out] pfRed: float*, [out] pfGreen: float*, [out] pfBlue: float* | void |
| `SetPointColor` | fRed: float, fGreen: float, fBlue: float | void |
| `get TextureFileNameEx` | — | BSTR |
| `GetMaterial` | [out] psMaterial: BSTR*, eMode: SeRenderMaterialGetMode | void |
| `SetMaterial` | sMaterial: BSTR, eMode: SeRenderMaterialSetMode | void |
| `get Material` | — | BSTR |
| `put Material` | p0: BSTR | void |

### <a name="textstyle"></a>`TextStyle`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `QueryInterface` | riid: GUID*, [out] ppvObj: void** | void |
| `AddRef` | — | uint |
| `Release` | — | uint |
| `put Name` | p0: BSTR | void |
| `get Name` | — | BSTR |
| `get Description` | paperUnits: int, Precision: int | BSTR |
| `get Units` | — | int |
| `put Units` | p0: int | void |
| `put Parent` | p0: BSTR | void |
| `get Parent` | — | BSTR |
| `get Alignment` | — | int |
| `put Alignment` | p0: int | void |
| `get BeforeSpacing` | — | double |
| `put BeforeSpacing` | p0: double | void |
| `get AfterSpacing` | — | double |
| `put AfterSpacing` | p0: double | void |
| `get LineSpacing` | — | double |
| `put LineSpacing` | p0: double | void |
| `get Tabs` | — | double |
| `put Tabs` | p0: double | void |
| `put CharStyleName` | p0: BSTR | void |
| `get CharStyleName` | — | BSTR |
| `SetLineLeading` | leading: double, leadingType: int | void |
| `GetLineLeading` | [out] leading: double*, [out] leadingType: int* | void |
| `get NumberJustification` | — | TextStyleNumberJustificationConstants |
| `put NumberJustification` | p0: TextStyleNumberJustificationConstants | void |
| `get AttributeSets` | — | IDispatch |
| `get IsAttributeSetPresent` | Name: BSTR | bool |

### <a name="textcharstyle"></a>`TextCharStyle`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `QueryInterface` | riid: GUID*, [out] ppvObj: void** | void |
| `AddRef` | — | uint |
| `Release` | — | uint |
| `put Name` | p0: BSTR | void |
| `get Name` | — | BSTR |
| `get Description` | paperUnits: int, Precision: int | BSTR |
| `put Units` | p0: int | void |
| `get Units` | — | int |
| `get Color` | — | int |
| `put Color` | p0: int | void |
| `get Parent` | — | BSTR |
| `put Parent` | p0: BSTR | void |
| `put FontName` | p0: BSTR | void |
| `get FontName` | — | BSTR |
| `put Style` | p0: int | void |
| `get Style` | — | int |
| `get UnderlineStyle` | — | int |
| `put UnderlineStyle` | p0: int | void |
| `get LangID` | — | int |
| `put LangID` | p0: int | void |
| `get TextSize` | — | double |
| `put TextSize` | p0: double | void |
| `SetTextSize` | TextSize: double, SizeType: int | void |
| `GetTextSize` | [out] TextSize: double*, [out] SizeType: int* | void |
| `get AspectRatio` | — | double |
| `put AspectRatio` | p0: double | void |
| `get AttributeSets` | — | IDispatch |
| `get IsAttributeSetPresent` | Name: BSTR | bool |

### <a name="symbol2d"></a>`Symbol2d`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `QueryInterface` | riid: GUID*, [out] ppvObj: void** | void |
| `AddRef` | — | uint |
| `Release` | — | uint |
| `get Style` | — | IDispatch |
| `get UseSymbolLayer` | — | bool |
| `put UseSymbolLayer` | p0: bool | void |
| `get Layer` | — | BSTR |
| `put Layer` | p0: BSTR | void |
| `get Angle` | — | double |
| `put Angle` | p0: double | void |
| `get ScaleFactorLock` | — | bool |
| `put ScaleFactorLock` | p0: bool | void |
| `get Quantity` | — | int |
| `put Quantity` | p0: int | void |
| `get User` | — | IDispatch |
| `get ScaleFactor` | — | double |
| `put ScaleFactor` | p0: double | void |
| `GetOrigin` | [out] Ox: double*, [out] Oy: double* | void |
| `SetOrigin` | Ox: double, Oy: double | void |
| `GetRotations` | [out] Xx: double*, [out] Xy: double*, [out] Yx: double*, [out] Yy: double* | void |
| `SetRotations` | Xx: double, Xy: double, Yx: double, Yy: double | void |
| `get DisplayType` | — | DisplayTypeConstant |
| `put DisplayType` | p0: DisplayTypeConstant | void |
| `get NestedDisplay` | — | bool |
| `put NestedDisplay` | p0: bool | void |
| `get ContentsLocatable` | — | bool |
| `put ContentsLocatable` | p0: bool | void |
| `get SourceDoc` | — | BSTR |
| `get Class` | — | BSTR |
| `get Object` | — | IDispatch |
| `get OLEType` | — | OLEInsertionTypeConstant |
| `get UpdateOptions` | — | OLEUpdateOptionConstant |
| `put UpdateOptions` | p0: OLEUpdateOptionConstant | void |
| `Update` | — | void |
| `DoVerb` | [opt]verb: VARIANT | void |
| `get ObjectVerbsCount` | — | int |
| `ObjectVerbs` | [opt]Index: VARIANT | BSTR |
| `get AlternatePath` | — | BSTR |
| `put AlternatePath` | p0: BSTR* | void |
| `get Application` | — | Application* |
| `get Index` | — | int |
| `get Name` | [opt]Recurse: VARIANT | BSTR |
| `get Parent` | — | IDispatch |
| `get Type` | — | int |
| `get ZOrder` | — | int |
| `get Key` | [opt]Recurse: VARIANT | BSTR |
| `get Document` | — | IDispatch |
| `Copy` | — | void |
| `Cut` | — | void |
| `Delete` | — | void |
| `Move` | XFrom: double, YFrom: double, XTo: double, YTo: double | void |
| `Scale` | Factor: double | void |
| `Rotate` | Angle: double, x: double, y: double | void |
| `Range` | [out] XMinimum: double*, [out] YMinimum: double*, [out] XMaximum: double*, [out] YMaximum: double* | void |
| `Duplicate` | [opt]XDistance: VARIANT, [opt]YDistance: VARIANT | IDispatch |
| `Mirror` | X1: double, Y1: double, X2: double, Y2: double, [opt]BooleanCopyFlag: VARIANT | IDispatch |
| `BringToFront` | — | void |
| `BringForward` | — | void |
| `SendToBack` | — | void |
| `SendBackward` | — | void |
| `Select` | — | void |
| `get KeyPointCount` | — | int |
| `GetKeyPoint` | Index: int, [out] x: double*, [out] y: double*, [out] z: double*, [out] KeyPointType: KeyPointType*, [out] HandleType: int* | void |
| `SetKeyPoint` | Index: int, x: double, y: double, z: double | void |
| `get AttributeSets` | — | IDispatch |
| `get IsAttributeSetPresent` | Name: BSTR | bool |
| `ConvertToGroup` | — | void |
| `get MemberReference` | Member: IDispatch | IDispatch |
| `get SourceDocument` | — | IDispatch |

### <a name="viewstyle"></a>`ViewStyle`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `QueryInterface` | riid: GUID*, [out] ppvObj: void** | void |
| `AddRef` | — | uint |
| `Release` | — | uint |
| `get StyleName` | — | BSTR |
| `put StyleName` | p0: BSTR | void |
| `get Parent` | — | BSTR |
| `put Parent` | p0: BSTR | void |
| `get RenderMode` | — | int |
| `put RenderMode` | p0: int | void |
| `get AllowOverrides` | — | int |
| `put AllowOverrides` | p0: int | void |
| `get AntialiasWireframe` | — | int |
| `put AntialiasWireframe` | p0: int | void |
| `get AntialiasSurface` | — | int |
| `put AntialiasSurface` | p0: int | void |
| `get DepthFading` | — | int |
| `put DepthFading` | p0: int | void |
| `get Perspective` | — | int |
| `put Perspective` | p0: int | void |
| `get FocalLength` | — | int |
| `put FocalLength` | p0: int | void |
| `get NumLights` | — | int |
| `get AmbientColor` | — | int |
| `put AmbientColor` | p0: int | void |
| `get AmbientIntensity` | — | float |
| `put AmbientIntensity` | p0: float | void |
| `get AmbientRed` | — | float |
| `put AmbientRed` | p0: float | void |
| `get AmbientGreen` | — | float |
| `put AmbientGreen` | p0: float | void |
| `get AmbientBlue` | — | float |
| `put AmbientBlue` | p0: float | void |
| `get HiddenLineMode` | — | int |
| `put HiddenLineMode` | p0: int | void |
| `get DimPercentage` | — | float |
| `put DimPercentage` | p0: float | void |
| `get IsBackgroundImageDisplayed` | — | int |
| `BeginPropertyBuffer` | — | void |
| `FlushPropertyBuffer` | — | void |
| `AddLight` | fRed: float, fGreen: float, fBlue: float, fTheta: float, fPhi: float, [out] pnLight: int* | void |
| `DeleteLight` | nLight: int | void |
| `GetLight` | nLight: int, [out] pfRed: float*, [out] pfGreen: float*, [out] pfBlue: float*, [out] pfTheta: float*, [out] pfPhi: float* | void |
| `SetLight` | nLight: int, fRed: float, fGreen: float, fBlue: float, fTheta: float, fPhi: float | void |
| `GetLightColor` | nLight: int, [out] plLightColor: int* | void |
| `SetLightColor` | nLight: int, lLightColor: int | void |
| `GetLightIntensity` | nLight: int, [out] pfIntensity: float* | void |
| `SetLightIntensity` | nLight: int, fIntensity: float | void |
| `GetLightTheta` | nLight: int, [out] pfTheta: float* | void |
| `SetLightTheta` | nLight: int, fTheta: float | void |
| `GetLightPhi` | nLight: int, [out] pfPhi: float* | void |
| `SetLightPhi` | nLight: int, fPhi: float | void |
| `Delete` | — | void |
| `get RenderModeType` | — | SeRenderModeType |
| `put RenderModeType` | p0: SeRenderModeType | void |
| `get SilhouettesEnabled` | — | bool |
| `put SilhouettesEnabled` | p0: bool | void |
| `get StyleID` | — | int |
| `GetAnalysisParameters` | [out] peState: SeAnalysisStateType*, [opt][out] peMode: SeAnalysisModeType*, [opt][out] pQualityScale: VARIANT*, [opt][out] pArg1: VARIANT*, [opt][out] pArg2: VARIANT*, [opt][out] pArg3: VARIANT*, [opt][out] pArg4: VARIANT* | void |
| `SetAnalysisParameters` | eState: SeAnalysisStateType, [opt]eMode: SeAnalysisModeType, [opt]QualityScale: VARIANT, [opt]Arg1: VARIANT, [opt]Arg2: VARIANT, [opt]Arg3: VARIANT, [opt]Arg4: VARIANT | void |
| `get BackgroundType` | — | SeBackgroundType |
| `put BackgroundType` | p0: SeBackgroundType | void |
| `get BackgroundImageFile` | — | BSTR |
| `put BackgroundImageFile` | p0: BSTR | void |
| `get SkyboxType` | — | SeSkyboxType |
| `put SkyboxType` | p0: SeSkyboxType | void |
| `SetSkyboxSkyFile` | sFilename: BSTR | void |
| `SetSkyboxSideFilename` | nSide: int, sFilename: BSTR | void |
| `GetSkyboxSideFilename` | nSide: int | BSTR |
| `SkyboxClear` | nSide: int | void |
| `SkyboxClearAll` | — | void |
| `GetSkyboxOrientation` | [out] pfxDirection: float*, [out] pfyDirection: float*, [out] pfzDirection: float*, [out] pfxUp: float*, [out] pfyUp: float*, [out] pfzUp: float*, [out] pfFieldOfView: float* | void |
| `SetSkyboxOrientation` | fxDirection: float, fyDirection: float, fzDirection: float, fxUp: float, fyUp: float, fzUp: float, fFieldOfView: float | void |
| `get BackgroundMirrorX` | — | int |
| `put BackgroundMirrorX` | p0: int | void |
| `get BackgroundMirrorY` | — | int |
| `put BackgroundMirrorY` | p0: int | void |
| `get Textures` | — | int |
| `put Textures` | p0: int | void |
| `get Reflections` | — | int |
| `put Reflections` | p0: int | void |
| `get Bumpmaps` | — | int |
| `put Bumpmaps` | p0: int | void |
| `get FloorReflection` | — | int |
| `put FloorReflection` | p0: int | void |
| `get CastShadows` | — | int |
| `put CastShadows` | p0: int | void |
| `get DropShadow` | — | int |
| `put DropShadow` | p0: int | void |
| `SetGradientBackground` | eType: SeGradientType, crColor1: int, crColor2: int, [opt]SpotCenterX: VARIANT, [opt]SpotCenterY: VARIANT | void |
| `GetGradientBackground` | [out] peType: SeGradientType*, [out] pcrColor1: int*, [out] pcrColor2: int*, [opt][out] pSpotCenterX: VARIANT*, [opt][out] pSpotCenterY: VARIANT* | void |
| `SetGradientColor` | nColor: int, crColor: int | void |
| `GetGradientColor` | nColor: int | int |
| `get AntialiasLevel` | — | SeAntiAliasLevel |
| `put AntialiasLevel` | p0: SeAntiAliasLevel | void |
| `get Silhouettes` | — | int |
| `put Silhouettes` | p0: int | void |
| `get HiddenLines` | — | SeHiddenLineMode |
| `put HiddenLines` | p0: SeHiddenLineMode | void |
| `get HighQuality` | — | int |
| `put HighQuality` | p0: int | void |
| `ResetSkyboxOrientation` | — | void |
| `DeleteSkybox` | — | void |
| `get AmbientShadows` | — | int |
| `put AmbientShadows` | p0: int | void |
| `get FloorShadow` | — | int |
| `put FloorShadow` | p0: int | void |
| `GetLightFlags` | nLight: int, [out] pnFlags: int* | void |
| `SetLightFlags` | nLight: int, nFlags: int | void |

### <a name="properties"></a>`Properties`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `QueryInterface` | riid: GUID*, [out] ppvObj: void** | void |
| `AddRef` | — | uint |
| `Release` | — | uint |
| `get Count` | — | int |
| `get Parent` | — | IDispatch |
| `get Application` | — | Application* |
| `get _NewEnum` | — | IUnknown |
| `get Name` | — | BSTR |
| `Item` | vIndex: VARIANT | Property* |
| `Save` | — | void |
| `Add` | Name: VARIANT, Value: VARIANT | Property* |
| `PropertyByID` | vIndex: VARIANT | Property* |

### <a name="property"></a>`Property`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `QueryInterface` | riid: GUID*, [out] ppvObj: void** | void |
| `AddRef` | — | uint |
| `Release` | — | uint |
| `get Name` | — | BSTR |
| `get Value` | — | VARIANT |
| `put Value` | p0: VARIANT* | void |
| `get Type` | — | VARIANT |
| `Delete` | — | void |
| `Id` | — | VARIANT |

### <a name="attributeset"></a>`AttributeSet`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `QueryInterface` | riid: GUID*, [out] ppvObj: void** | void |
| `AddRef` | — | uint |
| `Release` | — | uint |
| `get Count` | — | int |
| `get _NewEnum` | — | IUnknown |
| `Item` | Index: VARIANT | Attribute* |
| `Add` | Name: BSTR, Type: AttributeTypeConstants | Attribute* |
| `Remove` | Name: BSTR | void |
| `get SetName` | — | BSTR |

### <a name="attribute"></a>`Attribute`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `QueryInterface` | riid: GUID*, [out] ppvObj: void** | void |
| `AddRef` | — | uint |
| `Release` | — | uint |
| `get Name` | — | BSTR |
| `get Value` | — | VARIANT |
| `put Value` | p0: VARIANT | void |
| `get Type` | — | AttributeTypeConstants |

### <a name="queryobjects"></a>`QueryObjects`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `QueryInterface` | riid: GUID*, [out] ppvObj: void** | void |
| `AddRef` | — | uint |
| `Release` | — | uint |
| `get Application` | — | Application* |
| `get Count` | — | int |
| `Item` | Index: int | IDispatch |
| `get Parent` | — | IDispatch |

### <a name="highlightset"></a>`HighlightSet`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `QueryInterface` | riid: GUID*, [out] ppvObj: void** | void |
| `AddRef` | — | uint |
| `Release` | — | uint |
| `AddItem` | Item: IDispatch | void |
| `AddSelected` | — | void |
| `RemoveItem` | Index: VARIANT | void |
| `RemoveAll` | — | void |
| `Draw` | — | void |
| `Delete` | — | void |
| `get Count` | — | int |
| `Item` | Index: VARIANT | IDispatch |
| `get Color` | — | int |
| `put Color` | p0: int | void |
| `SetTransform` | Matrix: SAFEARRAY(double)* | void |
| `ClearTransform` | — | void |

### <a name="namedviews"></a>`NamedViews`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `QueryInterface` | riid: GUID*, [out] ppvObj: void** | void |
| `AddRef` | — | uint |
| `Release` | — | uint |
| `get Count` | — | int |
| `get Names` | ) -> SAFEARRAY(BSTR | — |
| `Create` | Name: BSTR | NamedView* |
| `GetByName` | Name: BSTR | NamedView* |
| `Remove` | Name: BSTR | void |
| `Rename` | currName: BSTR, NewName: BSTR | void |
| `get _NewEnum` | — | IUnknown |
| `Item` | Index: VARIANT | NamedView* |

### <a name="unitsofmeasure"></a>`UnitsOfMeasure`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `QueryInterface` | riid: GUID*, [out] ppvObj: void** | void |
| `AddRef` | — | uint |
| `Release` | — | uint |
| `ParseUnit` | Index: int, UnitString: BSTR | VARIANT |
| `FormatUnit` | Index: int, Dbus: double, [opt]PrecisionConstant: VARIANT | VARIANT |
| `get Application` | — | Application* |
| `get Parent` | — | IDispatch |
| `get Count` | — | int |
| `get _NewEnum` | — | IUnknown |
| `Item` | Index: VARIANT | UnitOfMeasure* |

### <a name="variable"></a>`variable`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `QueryInterface` | riid: GUID*, [out] ppvObj: void** | void |
| `AddRef` | — | uint |
| `Release` | — | uint |
| `put Name` | p0: BSTR | void |
| `get Name` | — | BSTR |
| `get UnitsType` | — | int |
| `put UnitsType` | p0: int | void |
| `put Value` | p0: double | void |
| `get Value` | — | double |
| `put Properties` | p0: int | void |
| `get Properties` | — | int |
| `put Formula` | p0: BSTR | void |
| `get Formula` | — | BSTR |
| `SetRange` | LowValue: BSTR, Condition: int, HighValue: BSTR | void |
| `GetRange` | [out] LowValue: BSTR*, [out] Condition: int*, [out] HighValue: BSTR* | void |
| `SetRangeEx` | LowValue: BSTR, LowLimitVarName: BSTR, HighValue: BSTR, HighLimitVarName: BSTR, Condition: int, bSkipSettingInitialValue: int | void |
| `SetValue` | Value: BSTR | void |
| `GetValue` | [out] Value: BSTR* | void |
| `Delete` | — | void |
| `get Type` | — | ObjectType |
| `put VariableTableName` | p0: BSTR | void |
| `get VariableTableName` | — | BSTR |
| `put Expose` | p0: int | void |
| `get Expose` | — | int |
| `put ExposeName` | p0: BSTR | void |
| `get ExposeName` | — | BSTR |
| `get DisplayName` | — | BSTR |
| `get SystemName` | — | BSTR |
| `get IsSuppressVariable` | — | bool |
| `GetValueOutOfRange` | — | double |
| `GetDiscreteValues` | [out] DiscreteValues: SAFEARRAY(double)* | void |
| `SetDiscreteValues` | DiscreteValues: SAFEARRAY(double)* | void |
| `AddDiscreteValue` | DiscreteValue: double | void |
| `RemoveDiscreteValue` | DiscreteValue: double | void |
| `ClearLimitsOrDiscreteValues` | — | void |
| `AddDiscreteVariables` | DiscreteVariables: SAFEARRAY(BSTR)* | void |
| `GetDiscreteVariables` | [out] DiscreteVariables: VARIANT*, [out] numDiscreteVariables: int* | void |
| `RemoveDiscreteVariables` | DiscreteVariables: SAFEARRAY(BSTR)* | void |
| `GetComment` | — | BSTR |
| `SetComment` | Comment: BSTR | void |
| `HasExternalLink` | [out] bLinked: bool* | void |
| `IsExternalLinkFrozen` | [out] bFrozen: bool* | void |
| `GetExternalLinkInfo` | [out] SourceVariableName: BSTR*, [out] SourceDocumenetName: BSTR* | void |
| `FreezeExternalLink` | — | void |
| `ThawExternalLink` | — | void |
| `BreakExternalLink` | — | void |
| `get IsReadOnly` | — | bool |
| `get VariableType` | — | seVariableTypeConstants |
| `GetValueRangeHighValue` | [out] pdHighValue: double* | void |
| `SetValueRangeHighValue` | dHighValue: double | void |
| `GetValueRangeLowValue` | [out] pdHighValue: double* | void |
| `SetValueRangeLowValue` | dHighValue: double | void |
| `SetValueRangeValues` | LowValue: double, Condition: int, HighValue: double | void |
| `GetValueRangeValues` | [out] LowValue: double*, [out] Condition: int*, [out] HighValue: double* | void |
| `GetValueDiscreteValues` | [out] DiscreteValues: SAFEARRAY(double)* | void |
| `SetValueDiscreteValues` | DiscreteValues: SAFEARRAY(double)* | void |
| `GetValueEx` | [out] pdValue: double*, seUnitsType: seUnitsTypeConstants | void |
| `SetValueEx` | dValue: double, seUnitsType: seUnitsTypeConstants | void |
| `GetRangeEx` | [out] LowValue: BSTR*, [out] LowLimitVarName: BSTR*, [out] HighValue: BSTR*, [out] HighLimitVarName: BSTR*, [out] Condition: int* | void |
| `HasVariableLimit` | [out] bVariableLimit: bool*, [out] LimitValue: VariableLimitValueConstant* | void |
| `get AttributeSets` | — | IDispatch |
| `get IsAttributeSetPresent` | Name: BSTR | bool |

### <a name="variablelist"></a>`VariableList`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `QueryInterface` | riid: GUID*, [out] ppvObj: void** | void |
| `AddRef` | — | uint |
| `Release` | — | uint |
| `get Count` | — | int |
| `Item` | Index: VARIANT | IDispatch |
| `get _NewEnum` | — | IUnknown |
| `Remove` | Index: VARIANT | void |
| `Add` | variable: VARIANT | void |

### <a name="variables"></a>`Variables`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `QueryInterface` | riid: GUID*, [out] ppvObj: void** | void |
| `AddRef` | — | uint |
| `Release` | — | uint |
| `get Count` | — | int |
| `Item` | Index: VARIANT | IDispatch |
| `get _NewEnum` | — | IUnknown |
| `get Application` | — | IDispatch |
| `get Parent` | — | IDispatch |
| `Add` | pName: BSTR, pFormula: BSTR, [opt]UnitsType: VARIANT | IDispatch |
| `AddFromClipboard` | pName: BSTR, [opt]UnitsType: VARIANT | IDispatch |
| `Edit` | pName: BSTR, pFormula: BSTR | void |
| `EditFromClipboard` | pName: BSTR | void |
| `PutName` | pVariable: IDispatch, pName: BSTR | void |
| `GetName` | pVariable: IDispatch | BSTR |
| `Translate` | pName: BSTR | IDispatch |
| `Query` | pFindCriterium: BSTR, [opt]NamedBy: VARIANT, [opt]VarType: VARIANT, [opt]CaseInsensitive: VARIANT | IDispatch |
| `GetFormula` | wcpName: BSTR | BSTR |
| `GetDisplayName` | pVariable: IDispatch | BSTR |
| `GetSystemName` | pVariable: IDispatch | BSTR |
| `CopyToClipboard` | bsName: BSTR | void |

### <a name="interpartlink"></a>`InterpartLink`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `QueryInterface` | riid: GUID*, [out] ppvObj: void** | void |
| `AddRef` | — | uint |
| `Release` | — | uint |
| `IsFrozen` | [out] bFrozen: bool* | void |
| `GetInfo` | [out] SourceFeatureName: BSTR*, [out] SourceDocumenetName: BSTR* | void |
| `Freeze` | — | void |
| `Thaw` | — | void |
| `BreakLink` | — | void |

### <a name="interpartlinks"></a>`InterpartLinks`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `QueryInterface` | riid: GUID*, [out] ppvObj: void** | void |
| `AddRef` | — | uint |
| `Release` | — | uint |
| `get Count` | — | int |
| `Item` | Index: VARIANT | IDispatch |
| `get Application` | — | IDispatch |
| `get Parent` | — | IDispatch |

### <a name="sensor"></a>`Sensor`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `QueryInterface` | riid: GUID*, [out] ppvObj: void** | void |
| `AddRef` | — | uint |
| `Release` | — | uint |
| `get SensorType` | — | SensorTypeConstants |
| `get Status` | — | SensorStatusConstants |
| `get IsInRange` | — | bool |
| `get CurrentValue` | — | double |
| `put Name` | p0: BSTR | void |
| `get Name` | — | BSTR |
| `put Description` | p0: BSTR | void |
| `get Description` | — | BSTR |
| `put LowerRange` | p0: double | void |
| `get LowerRange` | — | double |
| `put UpperRange` | p0: double | void |
| `get UpperRange` | — | double |
| `put Operator` | p0: SensorOperatorConstants | void |
| `get Operator` | — | SensorOperatorConstants |
| `put MinimumThreshold` | p0: double | void |
| `get MinimumThreshold` | — | double |
| `put MaximumThreshold` | p0: double | void |
| `get MaximumThreshold` | — | double |
| `put DisplayType` | p0: SensorDisplayTypeConstants | void |
| `get DisplayType` | — | SensorDisplayTypeConstants |
| `put UpdateMechanism` | p0: SensorUpdateMechanismConstants | void |
| `get UpdateMechanism` | — | SensorUpdateMechanismConstants |
| `Update` | — | void |
| `Delete` | — | void |
| `get AttributeSets` | — | IDispatch |
| `get IsAttributeSetPresent` | Name: BSTR | bool |

### <a name="sensors"></a>`Sensors`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `QueryInterface` | riid: GUID*, [out] ppvObj: void** | void |
| `AddRef` | — | uint |
| `Release` | — | uint |
| `get Count` | — | int |
| `Item` | Index: VARIANT | IDispatch |
| `get _NewEnum` | — | IUnknown |
| `get Application` | — | IDispatch |
| `get Parent` | — | IDispatch |
| `AddVariableSensor` | variable: IDispatch, Name: BSTR, Description: BSTR, LowerRange: double, UpperRange: double, MinimumThreshold: double, MaximumThreshold: double, Operator: SensorOperatorConstants, DisplayType: SensorDisplayTypeConstants, UpdateMechanism: SensorUpdateMechanismConstants | IDispatch |
| `AddMinimumDistanceSensor` | Element1: IDispatch, Element2: IDispatch, Name: BSTR, Description: BSTR, LowerRange: double, UpperRange: double, MinimumThreshold: double, MaximumThreshold: double, Operator: SensorOperatorConstants, DisplayType: SensorDisplayTypeConstants, UpdateMechanism: SensorUpdateMechanismConstants | IDispatch |
| `AddSurfaceAreaSensor` | iSensorType: SurfaceAreaSensorAreaTypeConstants, iSelectionType: SurfaceAreaSensorSelectionTypeConstants, Name: BSTR, Description: BSTR, LowerRange: double, UpperRange: double, MinimumThreshold: double, MaximumThreshold: double, Operator: SensorOperatorConstants, DisplayType: SensorDisplayTypeConstants, UpdateMechanism: SensorUpdateMechanismConstants, Element: VARIANT* | IDispatch |

### <a name="sheetmetalsensors"></a>`SheetMetalSensors`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `QueryInterface` | riid: GUID*, [out] ppvObj: void** | void |
| `AddRef` | — | uint |
| `Release` | — | uint |
| `get Count` | — | int |
| `Item` | Index: VARIANT | IDispatch |
| `get _NewEnum` | — | IUnknown |
| `get Application` | — | IDispatch |
| `get Parent` | — | IDispatch |
| `AddVariableSensor` | variable: IDispatch, Name: BSTR, Description: BSTR, LowerRange: double, UpperRange: double, MinimumThreshold: double, MaximumThreshold: double, Operator: SensorOperatorConstants, DisplayType: SensorDisplayTypeConstants, UpdateMechanism: SensorUpdateMechanismConstants | IDispatch |
| `AddMinimumDistanceSensor` | Element1: IDispatch, Element2: IDispatch, Name: BSTR, Description: BSTR, LowerRange: double, UpperRange: double, MinimumThreshold: double, MaximumThreshold: double, Operator: SensorOperatorConstants, DisplayType: SensorDisplayTypeConstants, UpdateMechanism: SensorUpdateMechanismConstants | IDispatch |
| `AddSheetMetalCheckerSensor` | LeftFeatureType: SheetMetalSensorFeatureTypeConstants, RightFeatureType: SheetMetalSensorFeatureTypeConstants, Name: BSTR, Description: BSTR, Threshold: double, UpdateMechanism: SensorUpdateMechanismConstants, [opt]Element: VARIANT* | IDispatch |
| `AddSurfaceAreaSensor` | iSensorType: SurfaceAreaSensorAreaTypeConstants, iSelectionType: SurfaceAreaSensorSelectionTypeConstants, Name: BSTR, Description: BSTR, LowerRange: double, UpperRange: double, MinimumThreshold: double, MaximumThreshold: double, Operator: SensorOperatorConstants, DisplayType: SensorDisplayTypeConstants, UpdateMechanism: SensorUpdateMechanismConstants, Element: VARIANT* | IDispatch |

### <a name="layers"></a>`Layers`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `QueryInterface` | riid: GUID*, [out] ppvObj: void** | void |
| `AddRef` | — | uint |
| `Release` | — | uint |
| `Item` | Index: VARIANT | Layer* |
| `get Application` | — | Application* |
| `get Parent` | — | IDispatch |
| `get Count` | — | int |
| `Add` | Name: BSTR | Layer* |
| `get ActiveLayer` | — | Layer* |
| `get _NewEnum` | — | IUnknown |

### <a name="dashstyles"></a>`DashStyles`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `QueryInterface` | riid: GUID*, [out] ppvObj: void** | void |
| `AddRef` | — | uint |
| `Release` | — | uint |
| `get Count` | — | int |
| `get Parent` | — | IDispatch |
| `get Application` | — | IDispatch |
| `Item` | Index: VARIANT | DashStyle* |
| `get _NewEnum` | — | IUnknown |
| `Add` | Name: BSTR | DashStyle* |
| `Remove` | Name: BSTR | void |

### <a name="linearstyles"></a>`LinearStyles`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `QueryInterface` | riid: GUID*, [out] ppvObj: void** | void |
| `AddRef` | — | uint |
| `Release` | — | uint |
| `get Count` | — | int |
| `get Parent` | — | IDispatch |
| `get Application` | — | IDispatch |
| `Item` | Index: VARIANT | LinearStyle* |
| `get _NewEnum` | — | IUnknown |
| `Add` | Name: BSTR, Parent: BSTR | LinearStyle* |
| `Remove` | Name: BSTR | void |
| `get Active` | — | BSTR |
| `put Active` | p0: BSTR | void |

### <a name="fillstyles"></a>`FillStyles`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `QueryInterface` | riid: GUID*, [out] ppvObj: void** | void |
| `AddRef` | — | uint |
| `Release` | — | uint |
| `get Count` | — | int |
| `get Parent` | — | IDispatch |
| `get Application` | — | IDispatch |
| `Item` | Index: VARIANT | FillStyle* |
| `get _NewEnum` | — | IUnknown |
| `Add` | Name: BSTR, Parent: BSTR | FillStyle* |
| `Remove` | Name: BSTR | void |
| `put Active` | p0: BSTR | void |
| `get Active` | — | BSTR |

### <a name="hatchpatternstyles"></a>`HatchPatternStyles`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `QueryInterface` | riid: GUID*, [out] ppvObj: void** | void |
| `AddRef` | — | uint |
| `Release` | — | uint |
| `get Count` | — | int |
| `get Parent` | — | IDispatch |
| `get Application` | — | IDispatch |
| `Item` | Index: VARIANT | HatchPatternStyle* |
| `get _NewEnum` | — | IUnknown |
| `Add` | Name: BSTR, Parent: BSTR | HatchPatternStyle* |
| `Remove` | Name: BSTR | void |

### <a name="facestyles"></a>`FaceStyles`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `QueryInterface` | riid: GUID*, [out] ppvObj: void** | void |
| `AddRef` | — | uint |
| `Release` | — | uint |
| `Item` | Index: VARIANT | IDispatch |
| `get _NewEnum` | — | IUnknown |
| `get Application` | — | IDispatch |
| `get Count` | — | int |
| `get Parent` | — | IDispatch |
| `Add` | Name: BSTR, Parent: BSTR | FaceStyle* |
| `Remove` | Name: BSTR | void |
| `GetStyleByID` | StyleID: int | IDispatch |

### <a name="textstyles"></a>`TextStyles`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `QueryInterface` | riid: GUID*, [out] ppvObj: void** | void |
| `AddRef` | — | uint |
| `Release` | — | uint |
| `get Count` | — | int |
| `get Parent` | — | IDispatch |
| `get Application` | — | IDispatch |
| `Item` | Index: VARIANT | TextStyle* |
| `get _NewEnum` | — | IUnknown |
| `Add` | Name: BSTR, Parent: BSTR | TextStyle* |
| `Remove` | Name: BSTR | void |
| `get Active` | — | BSTR |
| `put Active` | p0: BSTR | void |

### <a name="textcharstyles"></a>`TextCharStyles`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `QueryInterface` | riid: GUID*, [out] ppvObj: void** | void |
| `AddRef` | — | uint |
| `Release` | — | uint |
| `get Count` | — | int |
| `get Parent` | — | IDispatch |
| `get Application` | — | IDispatch |
| `Item` | Index: VARIANT | TextCharStyle* |
| `get _NewEnum` | — | IUnknown |
| `Add` | Name: BSTR, Parent: BSTR | TextCharStyle* |
| `Remove` | Name: BSTR | void |

### <a name="symbols"></a>`Symbols`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `QueryInterface` | riid: GUID*, [out] ppvObj: void** | void |
| `AddRef` | — | uint |
| `Release` | — | uint |
| `get Application` | — | IDispatch |
| `get Count` | — | int |
| `get Parent` | — | IDispatch |
| `Item` | Index: VARIANT | IDispatch |
| `get _NewEnum` | — | IUnknown |
| `Add` | insertionType: int, filePath: BSTR, x: double, y: double, [opt]z: VARIANT | Symbol2d* |
| `InsertSymbolAsGeometry` | filePath: BSTR, dOriginX: double, dOriginY: double | void |

### <a name="viewstyles"></a>`ViewStyles`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `QueryInterface` | riid: GUID*, [out] ppvObj: void** | void |
| `AddRef` | — | uint |
| `Release` | — | uint |
| `Item` | Index: VARIANT | ViewStyle* |
| `get _NewEnum` | — | IUnknown |
| `get Application` | — | IDispatch |
| `get Count` | — | int |
| `get Parent` | — | IDispatch |
| `Add` | Name: BSTR, Parent: BSTR | ViewStyle* |
| `Remove` | Name: BSTR | void |
| `GetStyleByID` | StyleID: int | IDispatch |
| `AddFromFile` | Filename: BSTR, StyleName: BSTR | ViewStyle* |

### <a name="reference"></a>`Reference`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `QueryInterface` | riid: GUID*, [out] ppvObj: void** | void |
| `AddRef` | — | uint |
| `Release` | — | uint |
| `get Application` | — | IDispatch |
| `get Object` | — | IDispatch |
| `get Parent` | — | IDispatch |
| `get Type` | — | ObjectType |
| `GetMatrix` | [in,out] Matrix: SAFEARRAY(double)* | void |
| `get AttributeSets` | — | IDispatch |
| `get IsAttributeSetPresent` | Name: BSTR | bool |
| `get ImmediateParent` | — | IDispatch |
| `get Style` | — | BSTR |
| `put Style` | p0: BSTR | void |
| `GetOccurrencesInPath` | [out] TopOccurrence: IDispatch*, [out] NumSubOccurrencesInPath: int*, [out] NumBoundSubOccurrencesInPath: int*, [in,out] BoundSubOccurrencesInPath: SAFEARRAY(IDispatch)* | void |

### <a name="routingslip"></a>`RoutingSlip`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `QueryInterface` | riid: GUID*, [out] ppvObj: void** | void |
| `AddRef` | — | uint |
| `Release` | — | uint |
| `put Subject` | p0: BSTR | void |
| `get Subject` | — | BSTR |
| `put ReturnWhenDone` | p0: bool | void |
| `get ReturnWhenDone` | — | bool |
| `put Message` | p0: BSTR | void |
| `get Message` | — | BSTR |
| `put Recipients` | p0: VARIANT | void |
| `put Delivery` | p0: RouteType | void |
| `get Delivery` | — | RouteType |
| `get Status` | — | RouteStatus |
| `get HasRouted` | — | bool |
| `get Application` | — | IDispatch |
| `get Parent` | — | IDispatch |
| `put TrackStatus` | p0: bool | void |
| `get TrackStatus` | — | bool |
| `put AskForApproval` | p0: bool | void |
| `get AskForApproval` | — | bool |
| `put Approve` | p0: bool | void |
| `get Approve` | — | bool |
| `get Approved` | — | bool |
| `GetRouteInfo` | — | bool |
| `AddRecipient` | bsRecip: BSTR | void |
| `Route` | [opt]ConfirmRoute: VARIANT | void |
| `Reset` | — | void |
| `get AttributeSets` | — | IDispatch |
| `get IsAttributeSetPresent` | Name: BSTR | bool |

### <a name="symbolproperties"></a>`SymbolProperties`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `QueryInterface` | riid: GUID*, [out] ppvObj: void** | void |
| `AddRef` | — | uint |
| `Release` | — | uint |
| `get Application` | — | IDispatch |
| `get Parent` | — | IDispatch |
| `get Symbol` | — | IDispatch |
| `get AttributeSets` | — | IDispatch |
| `get IsAttributeSetPresent` | Name: BSTR | bool |

### <a name="propertysets"></a>`PropertySets`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `QueryInterface` | riid: GUID*, [out] ppvObj: void** | void |
| `AddRef` | — | uint |
| `Release` | — | uint |
| `get Count` | — | int |
| `get Parent` | — | IDispatch |
| `get Application` | — | Application* |
| `get _NewEnum` | — | IUnknown |
| `Item` | vIndex: VARIANT | Properties* |
| `Save` | — | void |

### <a name="propertyex"></a>`PropertyEx`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `QueryInterface` | riid: GUID*, [out] ppvObj: void** | void |
| `AddRef` | — | uint |
| `Release` | — | uint |
| `get Name` | — | BSTR |
| `get Value` | — | VARIANT |
| `put Value` | p0: VARIANT* | void |
| `get Type` | — | VARIANT |
| `Delete` | — | void |
| `Id` | — | VARIANT |
| `GetProps` | [out] bstName: BSTR*, [out] Value: VARIANT*, [out] Type: VARIANT* | void |

### <a name="summaryinfo"></a>`SummaryInfo`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `QueryInterface` | riid: GUID*, [out] ppvObj: void** | void |
| `AddRef` | — | uint |
| `Release` | — | uint |
| `get AccessDate` | — | VARIANT |
| `get Application` | — | Application* |
| `get Author` | — | BSTR |
| `put Author` | p0: BSTR | void |
| `get Category` | — | BSTR |
| `put Category` | p0: BSTR | void |
| `get Comments` | — | BSTR |
| `put Comments` | p0: BSTR | void |
| `get Company` | — | BSTR |
| `put Company` | p0: BSTR | void |
| `get CreateApp` | — | BSTR |
| `put CreateApp` | p0: BSTR | void |
| `get CreateDate` | — | VARIANT |
| `get CreationLocale` | — | int |
| `get DocumentNumber` | — | BSTR |
| `put DocumentNumber` | p0: BSTR | void |
| `get Keywords` | — | BSTR |
| `put Keywords` | p0: BSTR | void |
| `get LastSavedBy` | — | BSTR |
| `put LastSavedBy` | p0: BSTR | void |
| `get Manager` | — | BSTR |
| `put Manager` | p0: BSTR | void |
| `get Parent` | — | IDispatch |
| `get ProjectName` | — | BSTR |
| `put ProjectName` | p0: BSTR | void |
| `get RevisionNumber` | — | BSTR |
| `put RevisionNumber` | p0: BSTR | void |
| `get SaveApp` | — | BSTR |
| `put SaveApp` | p0: BSTR | void |
| `get SaveDate` | — | VARIANT |
| `get Subject` | — | BSTR |
| `put Subject` | p0: BSTR | void |
| `get Template` | — | BSTR |
| `put Template` | p0: BSTR | void |
| `get Title` | — | BSTR |
| `put Title` | p0: BSTR | void |
| `get TotalEdits` | — | BSTR |
| `put TotalEdits` | p0: BSTR | void |

### <a name="attributequery"></a>`AttributeQuery`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `QueryInterface` | riid: GUID*, [out] ppvObj: void** | void |
| `AddRef` | — | uint |
| `Release` | — | uint |
| `QueryByName` | [opt]AttributeSetName: VARIANT, [opt]AttributeName: VARIANT | QueryObjects* |
| `get Application` | — | Application* |
| `get Parent` | — | IDispatch |

### <a name="highlightsets"></a>`HighlightSets`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `QueryInterface` | riid: GUID*, [out] ppvObj: void** | void |
| `AddRef` | — | uint |
| `Release` | — | uint |
| `get Application` | — | Application* |
| `get Parent` | — | IDispatch |
| `get Count` | — | int |
| `get _NewEnum` | — | IUnknown |
| `Item` | Index: VARIANT | HighlightSet* |
| `Add` | — | HighlightSet* |

### <a name="segenericcollection"></a>`SEGenericCollection`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `QueryInterface` | riid: GUID*, [out] ppvObj: void** | void |
| `AddRef` | — | uint |
| `Release` | — | uint |
| `get Application` | — | Application* |
| `get Parent` | — | IDispatch |
| `get Count` | — | int |
| `get _NewEnum` | — | IUnknown |
| `Item` | Index: VARIANT | IDispatch |

### <a name="attributesets"></a>`AttributeSets`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `QueryInterface` | riid: GUID*, [out] ppvObj: void** | void |
| `AddRef` | — | uint |
| `Release` | — | uint |
| `get Count` | — | int |
| `get _NewEnum` | — | IUnknown |
| `Item` | Index: VARIANT | AttributeSet* |
| `Add` | Name: BSTR | AttributeSet* |
| `Remove` | Name: BSTR | void |

### <a name="solidedgedocument"></a>`SolidEdgeDocument`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `QueryInterface` | riid: GUID*, [out] ppvObj: void** | void |
| `AddRef` | — | uint |
| `Release` | — | uint |
| `Activate` | — | void |
| `get Application` | — | Application* |
| `Close` | [opt]SaveChanges: VARIANT, [opt]Filename: VARIANT, [opt]RouteWorkbook: VARIANT | void |
| `get FullName` | — | BSTR |
| `get Name` | — | BSTR |
| `get Parent` | — | Application* |
| `get Path` | — | BSTR |
| `PrintOut` | [opt]Printer: VARIANT, [opt]NumCopies: VARIANT, [opt]Orientation: VARIANT, [opt]PaperSize: VARIANT, [opt]Scale: VARIANT, [opt]PrintToFile: VARIANT, [opt]OutputFileName: VARIANT, [opt]PrintRange: VARIANT, [opt]Sheets: VARIANT, [opt]ColorAsBlack: VARIANT, [opt]Collate: VARIANT | void |
| `get ReadOnly` | — | bool |
| `get RoutingSlip` | — | IDispatch |
| `Save` | — | void |
| `SaveAs` | NewName: BSTR, [opt]IsATemplate: VARIANT, [opt]FileFormat: VARIANT, [opt]ReadOnlyEnforced: VARIANT, [opt]ReadOnlyRecommended: VARIANT, [opt]newstatus: VARIANT, [opt]CreateBackup: VARIANT, [opt]UpdateLinkInContainer: VARIANT, [opt]UpdateAllLinksInContainer: VARIANT | void |
| `SaveCopyAs` | Name: BSTR | void |
| `SaveAsJT` | NewName: BSTR, [opt]Include_PreciseGeom: VARIANT, [opt]Prod_Structure_Option: VARIANT, [opt]Export_PMI: VARIANT, [opt]Export_CoordinateSystem: VARIANT, [opt]Export_3DBodies: VARIANT, [opt]NumberofLODs: VARIANT, [opt]JTFileUnit: VARIANT, [opt]Write_Which_Files: VARIANT, [opt]Use_Simplified_TopAsm: VARIANT, [opt]Use_Simplified_SubAsm: VARIANT, [opt]Use_Simplified_Part: VARIANT, [opt]EnableDefaultOutputPath: VARIANT, [opt]IncludeSEProperties: VARIANT, [opt]Export_VisiblePartsOnly: VARIANT, [opt]Export_VisibleConstructionsOnly: VARIANT, [opt]RemoveUnsafeCharacters: VARIANT, [opt]ExportSEPartFileAsSingleJTFile: VARIANT | void |
| `SaveAsBIDM` | filePath: BSTR, DocumentNumber: BSTR, Revision: BSTR, Title: BSTR | BSTR |
| `ReviseBIDM` | filePath: BSTR, Revision: BSTR, Title: BSTR | BSTR |
| `get SelectSet` | — | SelectSet* |
| `SendMail` | [opt]Recipients: VARIANT, [opt]Subject: VARIANT, [opt]ReturnReceipt: VARIANT | void |
| `get SummaryInfo` | — | IDispatch |
| `get Windows` | — | Windows* |
| `get Properties` | — | IDispatch |
| `get IsTemplate` | — | bool |
| `put IsTemplate` | p0: bool | void |
| `get Status` | — | DocumentStatus |
| `put Status` | p0: DocumentStatus | void |
| `EditProperties` | — | void |
| `get UnitsOfMeasure` | — | UnitsOfMeasure* |
| `get ActiveSketch` | — | IDispatch |
| `get Type` | — | DocumentTypeConstants |
| `get DocumentEvents` | — | DocumentEvents* |
| `get RootStorage` | — | IUnknown |
| `get AddInsStorage` | Name: BSTR, grfMode: int | IUnknown |
| `get Dirty` | — | bool |
| `put Dirty` | p0: bool | void |
| `get AttributeQuery` | — | AttributeQuery* |
| `get CreatedVersion` | — | BSTR |
| `get LastSavedVersion` | — | BSTR |
| `get HighlightSets` | — | HighlightSets* |
| `get InPlaceActivated` | — | bool |
| `SeekWriteAccess` | [out] WriteAccess: bool* | void |
| `get UndoSteps` | — | int |
| `put UndoSteps` | p0: int | void |
| `CreatePreview` | — | void |
| `put ReadOnly` | p0: bool | void |
| `SeekReadOnlyAccess` | [out] ReadOnlyAccess: bool* | void |
| `ImportStyles2` | StyleType: seStyleTypeConstants, bReplace: bool, pSrcDocument: IDispatch | void |
| `get IsInsightFile` | — | bool |
| `get NamedViews` | — | NamedViews* |
| `GetRegisteredCustomPropertiesBiDM` | [out] varPropInfo: VARIANT* | void |
| `SaveAsWithCustomPropertiesBIDM` | filePath: BSTR, DocumentNumber: BSTR, Revision: BSTR, Title: BSTR, varPropInfo: VARIANT | BSTR |
| `ReviseWithCustomPropertiesBIDM` | filePath: BSTR, Revision: BSTR, Title: BSTR, varPropInfo: VARIANT | BSTR |
| `SaveAsPRC` | Filename: BSTR | void |
| `get Variables` | — | IDispatch |
| `NewWindow` | [opt]NewWindowOptions: VARIANT, [opt]Environment: VARIANT | VARIANT |
| `get Blocks` | — | IDispatch |
| `put Name` | p0: BSTR | void |
| `SaveAs3DPrint` | filePath: BSTR, NumberOfCoordinates: int, PositionArray: SAFEARRAY(double)*, [opt]NumberOfNormals: int, [opt]NormalArray: SAFEARRAY(double)*, [opt]NumberofColors: int, [opt]colorArray: SAFEARRAY(int)*, [opt]NumberofIndices: int, [opt]Indexarray: SAFEARRAY(int)*, [opt]NumberOfFaces: int, [opt]FaceArray: SAFEARRAY(int)* | void |
| `SaveAsPLMXML` | bstrPLMXMLFilePath: BSTR, bstrPLMXMLINIFilePath: BSTR | void |
| `get GetPredefineRelationProducer` | — | PredefineRelationProducer* |

### <a name="predefinerelationproducer"></a>`PredefineRelationProducer`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `QueryInterface` | riid: GUID*, [out] ppvObj: void** | void |
| `AddRef` | — | uint |
| `Release` | — | uint |
| `GroupCount` | — | int |
| `MagneticGroupCount` | — | int |
| `HasAssemblyCaptureFitRelation` | — | bool |
| `AddPredefineRelationGroup` | bstrGroupName: BSTR, ePolarity: PredefineRelationGroupPolarityConstants, bSetDefault: bool | uint |
| `put DefaultGroup` | p0: uint | void |
| `get DefaultGroup` | — | uint |
| `SetCaptureFitDefault` | bCaptureFitDefault: bool | void |
| `ClearDefault` | — | void |
| `SetGroupName` | nGroupId: uint, bstrGroupName: BSTR | void |
| `GetGroupName` | nGroupId: uint | BSTR |
| `SetGroupPolarity` | nGroupId: uint, ePolarity: PredefineRelationGroupPolarityConstants | void |
| `GetGroupPolarity` | nGroupId: uint | PredefineRelationGroupPolarityConstants |
| `GetRelationCount` | nGroupId: uint | int |
| `GetCaptureFitRelationCount` | — | int |
| `DeleteGroups` | numDeleteGroups: int, pnDeleteGroupIds: uint* | void |
| `GetRelationData` | nGroupId: uint, nRelationIndex: int, [opt][out] ppElement: IDispatch*, [opt][out] pRelationType: CapturedRelationshipTypeConstants*, [opt][out] pOffsetType: CapturedRelationshipOffsetTypeConstants*, [opt][out] pdOffsetOne: double*, [opt][out] pdOffsetTwo: double* | void |
| `SetRelationData` | nGroupId: uint, nRelationIndex: int, pElement: IDispatch, relationType: CapturedRelationshipTypeConstants, offsetType: CapturedRelationshipOffsetTypeConstants, dOffsetOne: double, dOffsetTwo: double | void |
| `DeleteRelation` | nGroupId: uint, nRelationIndex: int | void |
| `AddMateRelation` | nGroupId: uint, pElement: IDispatch, [opt]pOffsetType: CapturedRelationshipOffsetTypeConstants*, [opt]pdOffsetOne: double*, [opt]pdOffsetTwo: double* | void |
| `AddPlanarRelation` | nGroupId: uint, pElement: IDispatch, [opt]pOffsetType: CapturedRelationshipOffsetTypeConstants*, [opt]pdOffsetOne: double*, [opt]pdOffsetTwo: double* | void |
| `AddAxialRelation` | nGroupId: uint, pElement: IDispatch | void |
| `AddTangentRelation` | nGroupId: uint, pElement: IDispatch, [opt]pOffsetType: CapturedRelationshipOffsetTypeConstants*, [opt]pdOffsetOne: double*, [opt]pdOffsetTwo: double* | void |
| `AddConnectRelation` | nGroupId: uint, pElement: IDispatch, [opt]pOffsetType: CapturedRelationshipOffsetTypeConstants*, [opt]pdOffsetOne: double*, [opt]pdOffsetTwo: double* | void |
| `AddParallelRelation` | nGroupId: uint, pElement: IDispatch, [opt]pOffsetType: CapturedRelationshipOffsetTypeConstants*, [opt]pdOffsetOne: double*, [opt]pdOffsetTwo: double* | void |
| `get Application` | — | IDispatch |

### <a name="cpdinitializerinsightxt"></a>`CPDInitializerInsightXT`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `QueryInterface` | riid: GUID*, [out] ppvObj: void** | void |
| `AddRef` | — | uint |
| `Release` | — | uint |
| `GetDocuments` | [out] psaDocs: SAFEARRAY(VARIANT)* | void |
| `GetPropertiesInfo` | bstrDocName: BSTR, [out] pvPropInfo: VARIANT* | void |
| `SetPropertiesInfo` | bstrDocName: BSTR, vPropInfo: VARIANT | void |
| `GetItemTypes` | bstrDocName: BSTR, [out] psaItemTypes: SAFEARRAY(VARIANT)* | void |
| `GetMappedPropertiesInfo` | bstrDocName: BSTR, bstrItemType: BSTR, [out] pvPropInfo: VARIANT* | void |
| `SetControlsBehavior` | vbDisableAssignAllButtonAndMenu: bool, vbDisableRestoreButton: bool, vbDisableItemIDCell: bool, vbDisableItemRevisionCell: bool, vbDisableItemNameCell: bool, vbDisableDatasetNameCell: bool | void |

### <a name="cpdinitializer"></a>`CPDInitializer`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `QueryInterface` | riid: GUID*, [out] ppvObj: void** | void |
| `AddRef` | — | uint |
| `Release` | — | uint |
| `GetDocuments` | [out] psaDocs: SAFEARRAY(VARIANT)* | void |
| `GetPropertiesInfo` | bstrDocName: BSTR, [out] pvPropInfo: VARIANT* | void |
| `SetPropertiesInfo` | bstrDocName: BSTR, vPropInfo: VARIANT | void |
| `GetItemTypes` | bstrDocName: BSTR, [out] psaItemTypes: SAFEARRAY(VARIANT)* | void |
| `GetMappedPropertiesInfo` | bstrDocName: BSTR, bstrItemType: BSTR, [out] pvPropInfo: VARIANT* | void |
| `SetControlsBehavior` | vbDisableAssignAllButtonAndMenu: bool, vbDisableRestoreButton: bool, vbDisableItemIDCell: bool, vbDisableItemRevisionCell: bool, vbDisableItemNameCell: bool, vbDisableDatasetNameCell: bool | void |

### <a name="sectionview"></a>`SectionView`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `QueryInterface` | riid: GUID*, [out] ppvObj: void** | void |
| `AddRef` | — | uint |
| `Release` | — | uint |
| `put Caption` | p0: BSTR | void |
| `get Caption` | — | BSTR |
| `Show` | bShowSectionView: bool | void |
| `Delete` | — | void |
| `put Name` | p0: BSTR | void |
| `get Name` | — | BSTR |
| `put Style` | p0: BSTR | void |
| `get Style` | — | BSTR |
| `put CuttingPlaneColor` | p0: int | void |
| `get CuttingPlaneColor` | — | int |
| `put CuttingPlaneEdgeColor` | p0: int | void |
| `get CuttingPlaneEdgeColor` | — | int |
| `put Opacity` | p0: double | void |
| `get Opacity` | — | double |
| `put ThroughAllExtent` | p0: double | void |
| `get ThroughAllExtent` | — | double |
| `put CutHardware` | p0: int | void |
| `get CutHardware` | — | int |
| `put SectionDisplayMode` | p0: PMISectionDisplayModeConstants | void |
| `get SectionDisplayMode` | — | PMISectionDisplayModeConstants |
| `put ShowCuttingPlane` | p0: int | void |
| `get ShowCuttingPlane` | — | int |
| `AddToModelView` | ModelView: IUnknown | void |
| `RemoveFromModelView` | ModelView: IUnknown | void |
| `EditByPlane` | nNumPlanes: int, pPlanes: SAFEARRAY(IDispatch)*, PlaneCutDirections: SAFEARRAY(int)*, eExtentType: SectionViewPlaneExtentTypeConstant, bCutHardwareParts: int | void |
| `put PlaneExtentType` | p0: SectionViewPlaneExtentTypeConstant | void |
| `get PlaneExtentType` | — | SectionViewPlaneExtentTypeConstant |
| `get AttributeSets` | — | IDispatch |
| `get IsAttributeSetPresent` | Name: BSTR | bool |
| `EditByPlaneEx` | nNumPlanes: int, pPlanes: SAFEARRAY(IDispatch)*, PlaneCutDirections: SAFEARRAY(int)*, SectionViewPlaneTypes: SAFEARRAY(SectionViewPlaneType)*, eExtentType: SectionViewPlaneExtentTypeConstant, bCutHardwareParts: bool | void |

### <a name="sectionviews"></a>`SectionViews`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `QueryInterface` | riid: GUID*, [out] ppvObj: void** | void |
| `AddRef` | — | uint |
| `Release` | — | uint |
| `get Count` | — | int |
| `Item` | Index: VARIANT | IDispatch |
| `get _NewEnum` | — | IUnknown |
| `get Application` | — | IDispatch |
| `get Parent` | — | IDispatch |
| `Add` | nNumProfiles: int, pProfiles: SAFEARRAY(IUnknown)*, szCaption: BSTR, dExtent: double, eExtentSide: SectionViewExtentSide, eProfileSide: SectionViewProfileSide, bCutHardwareParts: int | IDispatch |
| `AddByPlane` | nNumPlanes: int, pPlanes: SAFEARRAY(IDispatch)*, PlaneCutDirections: SAFEARRAY(int)*, eExtentType: SectionViewPlaneExtentTypeConstant, szCaption: BSTR, bCutHardwareParts: int | IDispatch |
| `AddByPlaneEx` | nNumPlanes: int, pPlanes: SAFEARRAY(IDispatch)*, PlaneCutDirections: SAFEARRAY(int)*, SectionViewPlaneTypes: SAFEARRAY(SectionViewPlaneType)*, eExtentType: SectionViewPlaneExtentTypeConstant, szCaption: BSTR, bCutHardwareParts: bool | IDispatch |

### <a name="interdocumentupdate"></a>`InterDocumentUpdate`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `QueryInterface` | riid: GUID*, [out] ppvObj: void** | void |
| `AddRef` | — | uint |
| `Release` | — | uint |
| `GetFilesToUpdate` | [out] FilesToUpdate: SAFEARRAY(BSTR)*, [opt]FutureUse: VARIANT | void |
| `LoadFilesToUpdate` | [opt]FutureUse: VARIANT | void |
| `Update` | UpdateMode: InterDocumentUpdateMode, [opt]FutureUse: VARIANT | void |
| `GetFilesToSave` | [out] FilesToSave: SAFEARRAY(BSTR)*, [opt]FutureUse: VARIANT | void |
| `SaveChangedFiles` | [opt][out] FilesNotSaved: SAFEARRAY(BSTR)*, [opt]FutureUse: VARIANT | void |

### <a name="steeringwheel"></a>`SteeringWheel`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `QueryInterface` | riid: GUID*, [out] ppvObj: void** | void |
| `AddRef` | — | uint |
| `Release` | — | uint |
| `GetOrigin` | [out] OriginX: double*, [out] OriginY: double*, [out] OriginZ: double* | void |
| `SetOrigin` | OriginX: double, OriginY: double, OriginZ: double | void |
| `GetOriginAndAxis` | AxisType: seSteeringWheelConstants, [out] OriginX: double*, [out] OriginY: double*, [out] OriginZ: double*, [out] AxisXComponent: double*, [out] AxisYComponent: double*, [out] AxisZComponent: double* | void |
| `Align` | AxisType: seSteeringWheelConstants, AxisXComponent: double, AxisYComponent: double, AxisZComponent: double | void |
| `AlignAlongLinerElement` | AxisType: seSteeringWheelConstants, LinearElementToAlignWith: IDispatch | void |

### <a name="cpdinitializerbidm"></a>`CPDInitializerBiDM`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `QueryInterface` | riid: GUID*, [out] ppvObj: void** | void |
| `AddRef` | — | uint |
| `Release` | — | uint |
| `GetDocuments` | [out] psaDocs: SAFEARRAY(VARIANT)* | void |
| `GetPropertiesInfo` | bstrDocName: BSTR, [out] pvPropInfo: VARIANT* | void |
| `SetPropertiesInfo` | bstrDocName: BSTR, vPropInfo: VARIANT | void |
| `SetControlsBehavior` | vbDisableAssignAllButtonAndMenu: bool, vbDisableDocumentNumberCell: bool, vbDisableRevisionIDCell: bool | void |

## ENUM

| Enum | Membros |
|------|---------|
| [ObjectType](#objecttype) | 130 |
| [TemplatesListType](#templateslisttype) | 4 |
| [SeControlType](#secontroltype) | 3 |
| [SeBarPosition](#sebarposition) | 5 |
| [SeBarType](#sebartype) | 3 |
| [AcceleratorTypeConstants](#acceleratortypeconstants) | 7 |
| [LinksUpdateOption](#linksupdateoption) | 3 |
| [DocumentAccess](#documentaccess) | 3 |
| [NotifyOption](#notifyoption) | 5 |
| [DocumentTypeConstants](#documenttypeconstants) | 13 |
| [SeImageQualityType](#seimagequalitytype) | 3 |
| [SolidEdgeCommandConstants](#solidedgecommandconstants) | 9 |
| [SeButtonState](#sebuttonstate) | 3 |
| [SeButtonStyle](#sebuttonstyle) | 9 |
| [ApplicationGlobalConstants](#applicationglobalconstants) | 491 |
| [SeObjectType](#seobjecttype) | 3 |
| [UploadType](#uploadtype) | 2 |
| [CheckInOptions](#checkinoptions) | 2 |
| [OverWriteFilesOption](#overwritefilesoption) | 2 |
| [DocumentStatus](#documentstatus) | 7 |
| [SPServerType](#spservertype) | 6 |
| [InsightSPUserRights](#insightspuserrights) | 23 |
| [CookieDataToGet](#cookiedatatoget) | 1 |
| [RevisionRuleType](#revisionruletype) | 5 |
| [BulkMigrationTypeConstants](#bulkmigrationtypeconstants) | 6 |
| [MatTablePropIndexConstants](#mattablepropindexconstants) | 13 |
| [UnitTypeConstants](#unittypeconstants) | 63 |
| [ShortCutMenuContextConstants](#shortcutmenucontextconstants) | 7 |
| [DocumentDownloadLevel](#documentdownloadlevel) | 3 |
| [SyncOption](#syncoption) | 2 |
| [TCESETypes](#tcesetypes) | 5 |
| [SEECOptions](#seecoptions) | 2 |
| [eCPDMode](#ecpdmode) | 4 |
| [RibbonBarControlSize](#ribbonbarcontrolsize) | 3 |
| [RibbonBarControlText](#ribbonbarcontroltext) | 3 |
| [RibbonBarInsertMode](#ribbonbarinsertmode) | 6 |
| [ArrangeWindowsStyles](#arrangewindowsstyles) | 4 |
| [GenerateMasterImportListError](#generatemasterimportlisterror) | 1 |
| [ConfigResetType](#configresettype) | 2 |
| [ConfigForForeignFileType](#configforforeignfiletype) | 1 |
| [FileTranslationMode](#filetranslationmode) | 2 |
| [WorkflowType](#workflowtype) | 2 |
| [WorkflowAction](#workflowaction) | 4 |
| [GenerateSourceImportListError](#generatesourceimportlisterror) | 1 |
| [OpenNonSolidEdgeFileContext](#opennonsolidedgefilecontext) | 9 |
| [AnimationEventConstants](#animationeventconstants) | 4 |
| [SeRenderModeType](#serendermodetype) | 12 |
| [seMovieStandardResolutionConstants](#semoviestandardresolutionconstants) | 5 |
| [seMovieFormatConstants](#semovieformatconstants) | 2 |
| [seSharpenLevelConstants](#sesharpenlevelconstants) | 9 |
| [SeFeatureAddFlag](#sefeatureaddflag) | 5 |
| [SeFeatureDeleteFlag](#sefeaturedeleteflag) | 5 |
| [SeFeatureModifyFlag](#sefeaturemodifyflag) | 3 |
| [ApplicationBeforeDocumentOpenEvent](#applicationbeforedocumentopenevent) | 6 |
| [ApplicationReadyEvent](#applicationreadyevent) | 2 |
| [ApplicationActiveFrameSwitchingEvent](#applicationactiveframeswitchingevent) | 2 |
| [ApplicationLicenseEvent](#applicationlicenseevent) | 2 |
| [ApplicationDocumentLoadingEvent](#applicationdocumentloadingevent) | 1 |
| [AssemblyChangeEventsConstants](#assemblychangeeventsconstants) | 12 |
| [AssemblyEventConstants](#assemblyeventconstants) | 1 |
| [SeConnectMode](#seconnectmode) | 3 |
| [SeDisconnectMode](#sedisconnectmode) | 3 |
| [CommandBarHeaderDialogControlIDs](#commandbarheaderdialogcontrolids) | 2 |
| [SeModifySketchFlag](#semodifysketchflag) | 3 |
| [seVariableTypeConstants](#sevariabletypeconstants) | 4 |
| [seUnitsTypeConstants](#seunitstypeconstants) | 2 |
| [VariableLimitValueConstant](#variablelimitvalueconstant) | 3 |
| [SensorTypeConstants](#sensortypeconstants) | 4 |
| [SensorStatusConstants](#sensorstatusconstants) | 3 |
| [SensorOperatorConstants](#sensoroperatorconstants) | 7 |
| [SensorDisplayTypeConstants](#sensordisplaytypeconstants) | 3 |
| [SensorUpdateMechanismConstants](#sensorupdatemechanismconstants) | 3 |
| [SurfaceAreaSensorAreaTypeConstants](#surfaceareasensorareatypeconstants) | 2 |
| [SurfaceAreaSensorSelectionTypeConstants](#surfaceareasensorselectiontypeconstants) | 2 |
| [SheetMetalSensorFeatureTypeConstants](#sheetmetalsensorfeaturetypeconstants) | 8 |
| [PMISectionDisplayModeConstants](#pmisectiondisplaymodeconstants) | 4 |
| [SectionViewPlaneExtentTypeConstant](#sectionviewplaneextenttypeconstant) | 2 |
| [SectionViewPlaneType](#sectionviewplanetype) | 2 |
| [SectionViewExtentSide](#sectionviewextentside) | 6 |
| [SectionViewProfileSide](#sectionviewprofileside) | 4 |
| [StyleUnitsConstant](#styleunitsconstant) | 3 |
| [HatchElementType](#hatchelementtype) | 3 |
| [RadialHatchElementCenterLocation](#radialhatchelementcenterlocation) | 10 |
| [SeSkyboxType](#seskyboxtype) | 4 |
| [SeRenderSpaceType](#serenderspacetype) | 3 |
| [SeRenderShapeType](#serendershapetype) | 2 |
| [SeRenderFillMode](#serenderfillmode) | 3 |
| [SeRenderShadeMode](#serendershademode) | 2 |
| [SeRenderMaterialGetMode](#serendermaterialgetmode) | 2 |
| [SeRenderMaterialSetMode](#serendermaterialsetmode) | 4 |
| [TextStyleNumberJustificationConstants](#textstylenumberjustificationconstants) | 3 |
| [DisplayTypeConstant](#displaytypeconstant) | 3 |
| [OLEInsertionTypeConstant](#oleinsertiontypeconstant) | 5 |
| [OLEUpdateOptionConstant](#oleupdateoptionconstant) | 3 |
| [KeyPointType](#keypointtype) | 13 |
| [SeAnalysisStateType](#seanalysisstatetype) | 3 |
| [SeAnalysisModeType](#seanalysismodetype) | 6 |
| [SeBackgroundType](#sebackgroundtype) | 4 |
| [SeGradientType](#segradienttype) | 7 |
| [SeAntiAliasLevel](#seantialiaslevel) | 4 |
| [SeHiddenLineMode](#sehiddenlinemode) | 3 |
| [RouteType](#routetype) | 2 |
| [RouteStatus](#routestatus) | 4 |
| [AttributeTypeConstants](#attributetypeconstants) | 11 |
| [seStyleTypeConstants](#sestyletypeconstants) | 7 |
| [PredefineRelationGroupPolarityConstants](#predefinerelationgrouppolarityconstants) | 4 |
| [CapturedRelationshipTypeConstants](#capturedrelationshiptypeconstants) | 6 |
| [CapturedRelationshipOffsetTypeConstants](#capturedrelationshipoffsettypeconstants) | 3 |
| [InterDocumentUpdateMode](#interdocumentupdatemode) | 2 |
| [seSteeringWheelConstants](#sesteeringwheelconstants) | 3 |

### <a name="objecttype"></a>`ObjectType`

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

### <a name="templateslisttype"></a>`TemplatesListType`

| Constante | Valor |
|-----------|-------|
| `eUnknownTemplateList` | 0 |
| `eStandardTemplateList` | 1 |
| `eUserTemplateList` | 2 |
| `eCustomTemplateList` | 3 |

### <a name="secontroltype"></a>`SeControlType`

| Constante | Valor |
|-----------|-------|
| `seControlPopup` | 1 |
| `seControlButton` | 2 |
| `seControlSeparator` | 3 |

### <a name="sebarposition"></a>`SeBarPosition`

| Constante | Valor |
|-----------|-------|
| `seBarTop` | 1 |
| `seBarBottom` | 2 |
| `seBarLeft` | 3 |
| `seBarRight` | 4 |
| `seBarFloating` | 5 |

### <a name="sebartype"></a>`SeBarType`

| Constante | Valor |
|-----------|-------|
| `seBarTypeMenuBar` | 1 |
| `seBarTypeNormal` | 2 |
| `seBarTypePopup` | 3 |

### <a name="acceleratortypeconstants"></a>`AcceleratorTypeConstants`

| Constante | Valor |
|-----------|-------|
| `seExecutable` | 1 |
| `seEmbeded` | 2 |
| `seServerInPlace` | 3 |
| `seContainerInPlace` | 4 |
| `seMainFrame` | 5 |
| `seServerInPlaceLink` | 6 |
| `seContainerInPlaceLink` | 7 |

### <a name="linksupdateoption"></a>`LinksUpdateOption`

| Constante | Valor |
|-----------|-------|
| `igNoLinksUpdate` | 0 |
| `igLinksUpdateWithDefpath` | 1 |
| `igLinksUpdateWithAltPath` | 2 |

### <a name="documentaccess"></a>`DocumentAccess`

| Constante | Valor |
|-----------|-------|
| `igReadWrite` | 0 |
| `igReadOnly` | 1 |
| `igReadExclusive` | 2 |

### <a name="notifyoption"></a>`NotifyOption`

| Constante | Valor |
|-----------|-------|
| `igNotifyWhenReadable` | 0 |
| `igNotifyWhenWriteable` | 1 |
| `igNotifyWhenAvailable` | 2 |
| `igNoNotify` | 3 |
| `igNotifyWhenExclusive` | 4 |

### <a name="documenttypeconstants"></a>`DocumentTypeConstants`

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

### <a name="seimagequalitytype"></a>`SeImageQualityType`

| Constante | Valor |
|-----------|-------|
| `seImageQualityLow` | 1 |
| `seImageQualityMedium` | 2 |
| `seImageQualityHigh` | 3 |

### <a name="solidedgecommandconstants"></a>`SolidEdgeCommandConstants`

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

### <a name="sebuttonstate"></a>`SeButtonState`

| Constante | Valor |
|-----------|-------|
| `seButtonDown` | 1 |
| `seButtonMixed` | 2 |
| `seButtonUp` | 3 |

### <a name="sebuttonstyle"></a>`SeButtonStyle`

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

### <a name="applicationglobalconstants"></a>`ApplicationGlobalConstants`

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

### <a name="seobjecttype"></a>`SeObjectType`

| Constante | Valor |
|-----------|-------|
| `seObjectNamedViews` | 1 |
| `seObjectViewStyles` | 2 |
| `seObjectFaceStyles` | 3 |

### <a name="uploadtype"></a>`UploadType`

| Constante | Valor |
|-----------|-------|
| `DeepUploadType` | 0 |
| `ShallowUploadType` | 1 |

### <a name="checkinoptions"></a>`CheckInOptions`

| Constante | Valor |
|-----------|-------|
| `DoNotCheckInOption` | 0 |
| `UploadAndCheckInOption` | 1 |

### <a name="overwritefilesoption"></a>`OverWriteFilesOption`

| Constante | Valor |
|-----------|-------|
| `NoToAll` | 0 |
| `YesToAll` | 1 |

### <a name="documentstatus"></a>`DocumentStatus`

| Constante | Valor |
|-----------|-------|
| `igStatusAvailable` | 0 |
| `igStatusInWork` | 1 |
| `igStatusInReview` | 2 |
| `igStatusReleased` | 3 |
| `igStatusBaselined` | 4 |
| `igStatusObsolete` | 5 |
| `igStatusUnknown` | 6 |

### <a name="spservertype"></a>`SPServerType`

| Constante | Valor |
|-----------|-------|
| `SERVER_TYPE_NOT_SHAREPOINT` | 0 |
| `SHAREPOINT_V1_SERVER` | 1 |
| `SHAREPOINT_V2_SERVER` | 2 |
| `SHAREPOINT_V3_SERVER` | 3 |
| `SHAREPOINT_V4_SERVER` | 4 |
| `SHAREPOINT_V5_SERVER` | 5 |

### <a name="insightspuserrights"></a>`InsightSPUserRights`

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

### <a name="cookiedatatoget"></a>`CookieDataToGet`

| Constante | Valor |
|-----------|-------|
| `GET_REVISION_RULE` | 0 |

### <a name="revisionruletype"></a>`RevisionRuleType`

| Constante | Valor |
|-----------|-------|
| `LastSavedType` | 0 |
| `LatestReleasedRevision` | 1 |
| `LatestRevision` | 2 |
| `ExternalBOM` | 3 |
| `VersionFromCache` | 4 |

### <a name="bulkmigrationtypeconstants"></a>`BulkMigrationTypeConstants`

| Constante | Valor |
|-----------|-------|
| `igNoBulkMigration` | 0 |
| `igTDMBulkMigration` | 1 |
| `igProEBulkMigration` | 2 |
| `igNX2DBulkMigration` | 3 |
| `igMDTBulkMigration` | 4 |
| `igSWBulkMigration` | 5 |

### <a name="mattablepropindexconstants"></a>`MatTablePropIndexConstants`

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

### <a name="unittypeconstants"></a>`UnitTypeConstants`

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

### <a name="shortcutmenucontextconstants"></a>`ShortCutMenuContextConstants`

| Constante | Valor |
|-----------|-------|
| `seShortCutForGraphicLocate` | 1 |
| `seShortCutForView` | 2 |
| `seShortCutForFeaturePathFinder` | 3 |
| `seShortCutForFeaturePathFinderDocument` | 4 |
| `seShortCutNone` | 5 |
| `seShortCutSimulationPathfinder` | 6 |
| `seShortCutForPredictCommand` | 7 |

### <a name="documentdownloadlevel"></a>`DocumentDownloadLevel`

| Constante | Valor |
|-----------|-------|
| `SEECDownloadAllLevel` | 0 |
| `SEECDownloadFirstLevel` | 1 |
| `SEECDownloadTopLevel` | 2 |

### <a name="syncoption"></a>`SyncOption`

| Constante | Valor |
|-----------|-------|
| `SEECSyncAll` | 0 |
| `SEECSyncOne` | 1 |

### <a name="tcesetypes"></a>`TCESETypes`

| Constante | Valor |
|-----------|-------|
| `TCE_SEPart` | 0 |
| `TCE_SEAssembly` | 1 |
| `TCE_SEWeldment` | 2 |
| `TCE_SESheetmetal` | 3 |
| `TCE_SEDraft` | 4 |

### <a name="seecoptions"></a>`SEECOptions`

| Constante | Valor |
|-----------|-------|
| `SEEC_eUnknownOption` | 0 |
| `SEEC_SearchLimit` | 1 |

### <a name="ecpdmode"></a>`eCPDMode`

| Constante | Valor |
|-----------|-------|
| `CPD_NEW_FILE` | 1 |
| `CPD_UPLOAD_FILE` | 2 |
| `CPD_SAVEAS_FILE` | 3 |
| `CPD_REVISE_FILE` | 4 |

### <a name="ribbonbarcontrolsize"></a>`RibbonBarControlSize`

| Constante | Valor |
|-----------|-------|
| `seRibbonBarControlSizeDefault` | 0 |
| `seRibbonBarControlSizeSmall` | 1 |
| `seRibbonBarControlSizeLarge` | 2 |

### <a name="ribbonbarcontroltext"></a>`RibbonBarControlText`

| Constante | Valor |
|-----------|-------|
| `seRibbonBarControlTextDefault` | 0 |
| `seRibbonBarControlTextOn` | 1 |
| `seRibbonBarControlTextOff` | 2 |

### <a name="ribbonbarinsertmode"></a>`RibbonBarInsertMode`

| Constante | Valor |
|-----------|-------|
| `seRibbonBarInsertCopy` | 0 |
| `seRibbonBarInsertMove` | 1 |
| `seRibbonBarInsertCreate` | 2 |
| `seRibbonBarInsertCreateButton` | 3 |
| `seRibbonBarInsertCreatePopup` | 4 |
| `seRibbonBarInsertCreateSplitButtonPopup` | 5 |

### <a name="arrangewindowsstyles"></a>`ArrangeWindowsStyles`

| Constante | Valor |
|-----------|-------|
| `igWindowsTiled` | 1 |
| `igWindowsHorizontal` | 2 |
| `igWindowsVertical` | 4 |
| `igWindowsCascade` | 8 |

### <a name="generatemasterimportlisterror"></a>`GenerateMasterImportListError`

| Constante | Valor |
|-----------|-------|
| `NoDocsFound` | 1 |

### <a name="configresettype"></a>`ConfigResetType`

| Constante | Valor |
|-----------|-------|
| `seResetAll` | -1801520595 |
| `seResetGroup` | -1957181463 |

### <a name="configforforeignfiletype"></a>`ConfigForForeignFileType`

| Constante | Valor |
|-----------|-------|
| `seAutoCADConfigFile` | 1067709598 |

### <a name="filetranslationmode"></a>`FileTranslationMode`

| Constante | Valor |
|-----------|-------|
| `seImport` | 1493142125 |
| `seExport` | -1720541218 |

### <a name="workflowtype"></a>`WorkflowType`

| Constante | Valor |
|-----------|-------|
| `OneStepRelease` | 0 |
| `QuickRelease` | 1 |

### <a name="workflowaction"></a>`WorkflowAction`

| Constante | Valor |
|-----------|-------|
| `Initiate` | 0 |
| `Delegate` | 1 |
| `Accept` | 2 |
| `Reject` | 3 |

### <a name="generatesourceimportlisterror"></a>`GenerateSourceImportListError`

| Constante | Valor |
|-----------|-------|
| `GenerateSourceImportListError_NoDocsFound` | 1 |

### <a name="opennonsolidedgefilecontext"></a>`OpenNonSolidEdgeFileContext`

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

### <a name="animationeventconstants"></a>`AnimationEventConstants`

| Constante | Valor |
|-----------|-------|
| `BeforeTimelineFrameUpdate` | 1 |
| `AfterTimelineFrameUpdate` | 2 |
| `BeforeDragComponentFrameUpdate` | 3 |
| `AfterDragComponentFrameUpdate` | 4 |

### <a name="serendermodetype"></a>`SeRenderModeType`

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

### <a name="semoviestandardresolutionconstants"></a>`seMovieStandardResolutionConstants`

| Constante | Valor |
|-----------|-------|
| `seMovieStandardResolutionNTSC` | 0 |
| `seMovieStandardResolutionPAL` | 1 |
| `seMovieStandardResolutionHD` | 2 |
| `seMovieStandardResolutionFullHD` | 3 |
| `seMovieStandardResolutionCurrentView` | 4 |

### <a name="semovieformatconstants"></a>`seMovieFormatConstants`

| Constante | Valor |
|-----------|-------|
| `seMovieFormatAVI` | 0 |
| `seMovieFormatWMV` | 1 |

### <a name="sesharpenlevelconstants"></a>`seSharpenLevelConstants`

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

### <a name="sefeatureaddflag"></a>`SeFeatureAddFlag`

| Constante | Valor |
|-----------|-------|
| `seNew` | 1 |
| `seUnSuppress` | 2 |
| `seUnSuppressUpTo` | 3 |
| `seNewPatternItem` | 4 |
| `seUnSuppressPatternItem` | 5 |

### <a name="sefeaturedeleteflag"></a>`SeFeatureDeleteFlag`

| Constante | Valor |
|-----------|-------|
| `sePermanent` | 1 |
| `seSuppress` | 2 |
| `seSuppressDownTo` | 3 |
| `sePermanentPatternItem` | 4 |
| `seSuppressPatternItem` | 5 |

### <a name="sefeaturemodifyflag"></a>`SeFeatureModifyFlag`

| Constante | Valor |
|-----------|-------|
| `seSchemaChanged` | 1 |
| `seDirectInputsChanged` | 2 |
| `seReordered` | 3 |

### <a name="applicationbeforedocumentopenevent"></a>`ApplicationBeforeDocumentOpenEvent`

| Constante | Valor |
|-----------|-------|
| `OpenFromUnknown` | 1 |
| `OpenFromMRU` | 2 |
| `OpenDropTagetApplication` | 3 |
| `OpenDropTargetDocumentView` | 4 |
| `OpenFromAutomation` | 5 |
| `OpenFromClipboardForCopyPasted` | 6 |

### <a name="applicationreadyevent"></a>`ApplicationReadyEvent`

| Constante | Valor |
|-----------|-------|
| `ApplicationIsUIReady` | 1 |
| `ActiveDocumentIsUIReady` | 2 |

### <a name="applicationactiveframeswitchingevent"></a>`ApplicationActiveFrameSwitchingEvent`

| Constante | Valor |
|-----------|-------|
| `ApplicationSwitchingToMainFrame` | 1 |
| `ApplicationSwitchingToFloatingFrame` | 2 |

### <a name="applicationlicenseevent"></a>`ApplicationLicenseEvent`

| Constante | Valor |
|-----------|-------|
| `ApplicationLicenseCheckin` | 1 |
| `ApplicationLicenseCheckout` | 2 |

### <a name="applicationdocumentloadingevent"></a>`ApplicationDocumentLoadingEvent`

| Constante | Valor |
|-----------|-------|
| `ApplicationWaitingForNextLevel` | 1 |

### <a name="assemblychangeeventsconstants"></a>`AssemblyChangeEventsConstants`

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

### <a name="assemblyeventconstants"></a>`AssemblyEventConstants`

| Constante | Valor |
|-----------|-------|
| `seAssemblyOccurrenceReplace` | 1 |

### <a name="seconnectmode"></a>`SeConnectMode`

| Constante | Valor |
|-----------|-------|
| `seConnectAtStartup` | 1 |
| `seConnectByUser` | 2 |
| `seConnectExternally` | 3 |

### <a name="sedisconnectmode"></a>`SeDisconnectMode`

| Constante | Valor |
|-----------|-------|
| `seDisconnectAtShutdown` | 1 |
| `seDisconnectByUser` | 2 |
| `seDisconnectExternally` | 3 |

### <a name="commandbarheaderdialogcontrolids"></a>`CommandBarHeaderDialogControlIDs`

| Constante | Valor |
|-----------|-------|
| `CommandBarHeaderDoitButton` | 1073 |
| `CommandBarHeaderOptionsButton` | 1074 |

### <a name="semodifysketchflag"></a>`SeModifySketchFlag`

| Constante | Valor |
|-----------|-------|
| `seInsertEntity` | 1 |
| `seRemoveEntity` | 2 |
| `seModifyEntity` | 3 |

### <a name="sevariabletypeconstants"></a>`seVariableTypeConstants`

| Constante | Valor |
|-----------|-------|
| `seVariableType_Dimension` | 1661573600 |
| `seVariableType_UserDefined` | 1560616706 |
| `seVariableType_Simulation` | 215773802 |
| `seVariableType_Text` | -170730141 |

### <a name="seunitstypeconstants"></a>`seUnitsTypeConstants`

| Constante | Valor |
|-----------|-------|
| `seUnitsType_DataBase` | -730794371 |
| `seUnitsType_Document` | 1886781498 |

### <a name="variablelimitvalueconstant"></a>`VariableLimitValueConstant`

| Constante | Valor |
|-----------|-------|
| `igVariableLimitNone` | 0 |
| `igDiscreteList` | 1 |
| `igMinMaxLimit` | 2 |

### <a name="sensortypeconstants"></a>`SensorTypeConstants`

| Constante | Valor |
|-----------|-------|
| `seSensorTypeInvalid` | 0 |
| `seSensorTypeVariable` | 1 |
| `seSensorTypeMinimumDistance` | 6 |
| `seSensorTypeUser` | 7 |

### <a name="sensorstatusconstants"></a>`SensorStatusConstants`

| Constante | Valor |
|-----------|-------|
| `seSensorStatusUpToDate` | 0 |
| `seSensorStatusOutOfDate` | 1 |
| `seSensorStatusInError` | 2 |

### <a name="sensoroperatorconstants"></a>`SensorOperatorConstants`

| Constante | Valor |
|-----------|-------|
| `seSensorOperatorInvalid` | 0 |
| `seSensorOperatorGreaterThan` | 1 |
| `seSensorOperatorLessThan` | 2 |
| `seSensorOperatorEqualTo` | 3 |
| `seSensorOperatorNotEqualTo` | 4 |
| `seSensorOperatorBetween` | 5 |
| `seSensorOperatorNotBetween` | 6 |

### <a name="sensordisplaytypeconstants"></a>`SensorDisplayTypeConstants`

| Constante | Valor |
|-----------|-------|
| `seSensorDisplayTypeInvalid` | 0 |
| `seSensorDisplayTypeHorizontalRange` | 1 |
| `seSensorDisplayTypeTrueFalse` | 2 |

### <a name="sensorupdatemechanismconstants"></a>`SensorUpdateMechanismConstants`

| Constante | Valor |
|-----------|-------|
| `seSensorUpdateMechanismInvalid` | 0 |
| `seSensorUpdateMechanismAutomatic` | 1 |
| `seSensorUpdateMechanismManual` | 2 |

### <a name="surfaceareasensorareatypeconstants"></a>`SurfaceAreaSensorAreaTypeConstants`

| Constante | Valor |
|-----------|-------|
| `seSurfaceAreaSensorAreaTypeNeg` | 0 |
| `seSurfaceAreaSensorAreaTypePos` | 1 |

### <a name="surfaceareasensorselectiontypeconstants"></a>`SurfaceAreaSensorSelectionTypeConstants`

| Constante | Valor |
|-----------|-------|
| `seSurfaceAreaSensorSelectFace` | 0 |
| `seSurfaceAreaSensorSelectFaceChain` | 1 |

### <a name="sheetmetalsensorfeaturetypeconstants"></a>`SheetMetalSensorFeatureTypeConstants`

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

### <a name="pmisectiondisplaymodeconstants"></a>`PMISectionDisplayModeConstants`

| Constante | Valor |
|-----------|-------|
| `sePMISectionDisplayShowOnlyCutFaces` | 0 |
| `sePMISectionDisplayShowCutFacesAndCutBodies` | 1 |
| `sePMISectionDisplayShowCutFacesWithOriginalBodies` | 2 |
| `sePMISectionDisplayShowOnlyOriginalBodies` | 3 |

### <a name="sectionviewplaneextenttypeconstant"></a>`SectionViewPlaneExtentTypeConstant`

| Constante | Valor |
|-----------|-------|
| `SectionViewPlaneExtentTypeConstant_Bounded` | 1 |
| `SectionViewPlaneExtentTypeConstant_UnBounded` | 2 |

### <a name="sectionviewplanetype"></a>`SectionViewPlaneType`

| Constante | Valor |
|-----------|-------|
| `igDynamic` | 1 |
| `igAssociative` | 2 |

### <a name="sectionviewextentside"></a>`SectionViewExtentSide`

| Constante | Valor |
|-----------|-------|
| `igLeftExtent` | 1 |
| `igRightExtent` | 2 |
| `igFiniteSymmetricExtent` | 3 |
| `igInfiniteLeftExtent` | 4 |
| `igInfiniteRightExtent` | 5 |
| `igThroughAllExtent` | 6 |

### <a name="sectionviewprofileside"></a>`SectionViewProfileSide`

| Constante | Valor |
|-----------|-------|
| `igLeftProfileSide` | 1 |
| `igRightProfileSide` | 2 |
| `igInsideProfileSide` | 3 |
| `igOutsideProfileSide` | 4 |

### <a name="styleunitsconstant"></a>`StyleUnitsConstant`

| Constante | Valor |
|-----------|-------|
| `PAPER_STYLEUNITS` | 11 |
| `DESIGN_STYLEUNITS` | 12 |
| `VIEW_STYLEUNITS` | 13 |

### <a name="hatchelementtype"></a>`HatchElementType`

| Constante | Valor |
|-----------|-------|
| `igHatchElementTypeUnknown` | 0 |
| `igHatchElementTypeLinear` | 1 |
| `igHatchElementTypeRadial` | 2 |

### <a name="radialhatchelementcenterlocation"></a>`RadialHatchElementCenterLocation`

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

### <a name="seskyboxtype"></a>`SeSkyboxType`

| Constante | Valor |
|-----------|-------|
| `seSkyboxTypeSkybox` | 0 |
| `seSkyboxTypeSingleImage` | 1 |
| `seSkyboxTypeSpheremap` | 2 |
| `seSkyboxTypeUndefined` | -1 |

### <a name="serenderspacetype"></a>`SeRenderSpaceType`

| Constante | Valor |
|-----------|-------|
| `seRenderSpaceDevice` | 0 |
| `seRenderSpacePaper` | 1 |
| `seRenderSpaceWorld` | 2 |

### <a name="serendershapetype"></a>`SeRenderShapeType`

| Constante | Valor |
|-----------|-------|
| `seRenderShapeSquare` | 1 |
| `seRenderShapeRound` | 2 |

### <a name="serenderfillmode"></a>`SeRenderFillMode`

| Constante | Valor |
|-----------|-------|
| `seRenderFillSolid` | 1 |
| `seRenderFillBorder` | 2 |
| `seRenderFillSolidBorder` | 3 |

### <a name="serendershademode"></a>`SeRenderShadeMode`

| Constante | Valor |
|-----------|-------|
| `seRenderShadeModeFlat` | 1 |
| `seRenderShadeModeSmooth` | 2 |

### <a name="serendermaterialgetmode"></a>`SeRenderMaterialGetMode`

| Constante | Valor |
|-----------|-------|
| `seGetModeExisting` | 0 |
| `seGetModeCreateOnDemand` | 1 |

### <a name="serendermaterialsetmode"></a>`SeRenderMaterialSetMode`

| Constante | Valor |
|-----------|-------|
| `seSetModeDetach` | 0 |
| `seSetModeAttach` | 1 |
| `seSetModeUpdate` | 2 |
| `seSetModeAttachAndUpdate` | 3 |

### <a name="textstylenumberjustificationconstants"></a>`TextStyleNumberJustificationConstants`

| Constante | Valor |
|-----------|-------|
| `igLeftJustificationStyle` | 0 |
| `igCenterJustificationStyle` | 1 |
| `igRightJustificationStyle` | 2 |

### <a name="displaytypeconstant"></a>`DisplayTypeConstant`

| Constante | Valor |
|-----------|-------|
| `igNotSpecifiedDisplay` | -1 |
| `igContentsDisplay` | 0 |
| `igIconDisplay` | 1 |

### <a name="oleinsertiontypeconstant"></a>`OLEInsertionTypeConstant`

| Constante | Valor |
|-----------|-------|
| `igUseSymbolPreferences` | -1 |
| `igOLELinked` | 0 |
| `igOLEEmbedded` | 1 |
| `igOLENone` | 3 |
| `igOLESharedEmbedded` | 4 |

### <a name="oleupdateoptionconstant"></a>`OLEUpdateOptionConstant`

| Constante | Valor |
|-----------|-------|
| `igOLEAutomatic` | 0 |
| `igOLEFrozen` | 1 |
| `igOLEManual` | 2 |

### <a name="keypointtype"></a>`KeyPointType`

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

### <a name="seanalysisstatetype"></a>`SeAnalysisStateType`

| Constante | Valor |
|-----------|-------|
| `seAnalysisStateNone` | 0 |
| `seAnalysisStateGlobal` | 1 |
| `seAnalysisStateLocal` | 2 |

### <a name="seanalysismodetype"></a>`SeAnalysisModeType`

| Constante | Valor |
|-----------|-------|
| `seAnalysisModeDefault` | 0 |
| `seAnalysisModeZebraStripeLinear` | 1 |
| `seAnalysisModeZebraStripeSpherical` | 2 |
| `seAnalysisModeZebraStripeReflection` | 3 |
| `seAnalysisModeCurvatureColor` | 4 |
| `seAnalysisModeDraftAngle` | 5 |

### <a name="sebackgroundtype"></a>`SeBackgroundType`

| Constante | Valor |
|-----------|-------|
| `seBackgroundTypeSolid` | 0 |
| `seBackgroundTypeGradient` | 1 |
| `seBackgroundTypeImage` | 2 |
| `seBackgroundTypeImageReference` | 3 |

### <a name="segradienttype"></a>`SeGradientType`

| Constante | Valor |
|-----------|-------|
| `seGradientTypeHorizontal` | 1 |
| `seGradientTypeVertical` | 2 |
| `seGradientTypeDiagonalUp` | 3 |
| `seGradientTypeDiagonalDown` | 4 |
| `seGradientTypeSquareSpot` | 5 |
| `seGradientTypeCircularSpot` | 6 |
| `seGradientTypeCustom` | 7 |

### <a name="seantialiaslevel"></a>`SeAntiAliasLevel`

| Constante | Valor |
|-----------|-------|
| `seAntiAliasLevelNone` | 0 |
| `seAntiAliasLevelLow` | 2 |
| `seAntiAliasLevelMedium` | 4 |
| `seAntiAliasLevelHigh` | 8 |

### <a name="sehiddenlinemode"></a>`SeHiddenLineMode`

| Constante | Valor |
|-----------|-------|
| `seHiddenLineModeOff` | 0 |
| `seHiddenLineModeDim` | 1 |
| `seHiddenLineModeDashed` | 2 |

### <a name="routetype"></a>`RouteType`

| Constante | Valor |
|-----------|-------|
| `igOneAfterAnother` | 0 |
| `igAllAtOnce` | 1 |

### <a name="routestatus"></a>`RouteStatus`

| Constante | Valor |
|-----------|-------|
| `igInvalidSlip` | 0 |
| `igRouteComplete` | 1 |
| `igNotYetRouted` | 2 |
| `igRouteInProgress` | 3 |

### <a name="attributetypeconstants"></a>`AttributeTypeConstants`

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

### <a name="sestyletypeconstants"></a>`seStyleTypeConstants`

| Constante | Valor |
|-----------|-------|
| `igDimensionStyle` | 0 |
| `igDrawingViewStyle` | 1 |
| `igFillStyle` | 2 |
| `igHatchStyle` | 3 |
| `igLineStyle` | 4 |
| `igTableStyle` | 5 |
| `igTextStyle` | 6 |

### <a name="predefinerelationgrouppolarityconstants"></a>`PredefineRelationGroupPolarityConstants`

| Constante | Valor |
|-----------|-------|
| `MagneticGroup` | 0 |
| `SPoleGroup` | 1 |
| `NPoleGroup` | 2 |
| `CaptureFitGroup` | 3 |

### <a name="capturedrelationshiptypeconstants"></a>`CapturedRelationshipTypeConstants`

| Constante | Valor |
|-----------|-------|
| `seMate` | 0 |
| `sePlanarAlign` | 1 |
| `seAxialAlign` | 2 |
| `seTangent` | 3 |
| `seConnect` | 4 |
| `seParallel` | 5 |

### <a name="capturedrelationshipoffsettypeconstants"></a>`CapturedRelationshipOffsetTypeConstants`

| Constante | Valor |
|-----------|-------|
| `seFixed` | 0 |
| `seFloating` | 1 |
| `seOffsetNotSupported` | 2 |

### <a name="interdocumentupdatemode"></a>`InterDocumentUpdateMode`

| Constante | Valor |
|-----------|-------|
| `seActiveLevel` | 0 |
| `seAllOpenDocuments` | 1 |

### <a name="sesteeringwheelconstants"></a>`seSteeringWheelConstants`

| Constante | Valor |
|-----------|-------|
| `seSteeringWheelConstantsXAxis` | 1 |
| `seSteeringWheelConstantsYAxis` | 2 |
| `seSteeringWheelConstantsZAxis` | 3 |

## INTERFACE

| Tipo | Membros |
|------|---------|
| [_IApplicationAuto](#_iapplicationauto) | 177 |
| [_ISelectSetAuto](#_iselectsetauto) | 18 |
| [_IDocumentsAuto](#_idocumentsauto) | 17 |
| [_ITemplateManagerAuto](#_itemplatemanagerauto) | 3 |
| [_IEnvironmentsAuto](#_ienvironmentsauto) | 5 |
| [_IEnvironmentAuto](#_ienvironmentauto) | 13 |
| [ISECommandBars](#isecommandbars) | 12 |
| [ISECommandBar](#isecommandbar) | 27 |
| [ISECommandBarControls](#isecommandbarcontrols) | 6 |
| [ISECommandBarControl](#isecommandbarcontrol) | 42 |
| [ISEAccelerators](#iseaccelerators) | 4 |
| [ISEAccelerator](#iseaccelerator) | 10 |
| [ISEKeyBinding](#isekeybinding) | 5 |
| [ISECommandCategories](#isecommandcategories) | 4 |
| [ISECommandCategory](#isecommandcategory) | 4 |
| [ISECommandInfo](#isecommandinfo) | 7 |
| [_IWindowsAuto](#_iwindowsauto) | 5 |
| [ISEApplicationEvents](#iseapplicationevents) | 16 |
| [ISEApplicationWindowEvents](#iseapplicationwindowevents) | 1 |
| [ISEFileUIEvents](#isefileuievents) | 6 |
| [ISEBeforeFileSaveAsEvents](#isebeforefilesaveasevents) | 1 |
| [ISECommand](#isecommand) | 9 |
| [ISEMouseEx3](#isemouseex3) | 2 |
| [ISEMouseEx2](#isemouseex2) | 2 |
| [ISEMouseEx](#isemouseex) | 3 |
| [ISEMouse](#isemouse) | 41 |
| [ISEMouseEvents](#isemouseevents) | 6 |
| [ISECommandWindowEvents](#isecommandwindowevents) | 1 |
| [ISECommandEvents](#isecommandevents) | 7 |
| [ISEAddIns](#iseaddins) | 5 |
| [ISEAddIn](#iseaddin) | 17 |
| [ISEAddInEvents](#iseaddinevents) | 3 |
| [ISECommandBarButton](#isecommandbarbutton) | 5 |
| [ISECommandBarButtonEvents](#isecommandbarbuttonevents) | 3 |
| [ISEFeatureLibraryEvents](#isefeaturelibraryevents) | 3 |
| [_IInsightAuto](#_iinsightauto) | 57 |
| [ISEApplicationV8AfterDocumentOpenEvent](#iseapplicationv8afterdocumentopenevent) | 1 |
| [ISEFeatureSelectedFromPFEvents](#isefeatureselectedfrompfevents) | 1 |
| [_IMatTableAuto](#_imattableauto) | 59 |
| [ISENewFileUIEvents](#isenewfileuievents) | 1 |
| [ISEShortCutMenuEvents](#iseshortcutmenuevents) | 1 |
| [_ISolidEdgeTCEAuto](#_isolidedgetceauto) | 101 |
| [_ISolidEdgeInsightXTAuto](#_isolidedgeinsightxtauto) | 50 |
| [ISEECEvents](#iseecevents) | 3 |
| [ISESPEvents](#isespevents) | 3 |
| [IBiDMEvents](#ibidmevents) | 2 |
| [_ICustomizationAuto](#_icustomizationauto) | 7 |
| [_IRibbonBarThemesAuto](#_iribbonbarthemesauto) | 9 |
| [_IRibbonBarThemeAuto](#_iribbonbarthemeauto) | 10 |
| [_IRibbonBarsAuto](#_iribbonbarsauto) | 5 |
| [_IRibbonBarAuto](#_iribbonbarauto) | 4 |
| [_IRibbonBarTabsAuto](#_iribbonbartabsauto) | 7 |
| [_IRibbonBarTabAuto](#_iribbonbartabauto) | 9 |
| [_IRibbonBarGroupsAuto](#_iribbonbargroupsauto) | 7 |
| [_IRibbonBarGroupAuto](#_iribbonbargroupauto) | 8 |
| [_IRibbonBarControlsAuto](#_iribbonbarcontrolsauto) | 8 |
| [_IRibbonBarControlAuto](#_iribbonbarcontrolauto) | 13 |
| [_IRadialMenuAuto](#_iradialmenuauto) | 15 |
| [_ISwitchWindowCustAuto](#_iswitchwindowcustauto) | 11 |
| [_IDynamicVisualizationAuto](#_idynamicvisualizationauto) | 1 |
| [ISEOpenNonSolidEdgeFileUIEvents](#iseopennonsolidedgefileuievents) | 1 |
| [_IWindowAuto](#_iwindowauto) | 39 |
| [_IViewAuto](#_iviewauto) | 118 |
| [ISEViewEvents](#iseviewevents) | 3 |
| [ISEhDCDisplayEvents](#isehdcdisplayevents) | 4 |
| [ISEIGLDisplayEvents](#iseigldisplayevents) | 4 |
| [ISERenderEvents](#iserenderevents) | 3 |
| [ISEAnimationEvents](#iseanimationevents) | 1 |
| [_INamedViewsAuto](#_inamedviewsauto) | 8 |
| [_INamedViewAuto](#_inamedviewauto) | 8 |
| [_IUnitsOfMeasureAuto](#_iunitsofmeasureauto) | 7 |
| [_IUnitOfMeasureAuto](#_iunitofmeasureauto) | 5 |
| [_ICPDInitializerBiDMAuto](#_icpdinitializerbidmauto) | 4 |
| [ISECommandBarPopup](#isecommandbarpopup) | 2 |
| [ISEDocumentEvents](#isedocumentevents) | 4 |
| [ISEBendTableEvents](#isebendtableevents) | 4 |
| [ISEModelRecomputeEvents](#isemodelrecomputeevents) | 6 |
| [ISEDynamicEditEvents](#isedynamiceditevents) | 2 |
| [ISEApplicationEventsEx](#iseapplicationeventsex) | 1 |
| [ISEApplicationEventsEx2](#iseapplicationeventsex2) | 1 |
| [ISEApplicationReadyEvents](#iseapplicationreadyevents) | 1 |
| [ISEApplicationActiveFrameSwitchingEvents](#iseapplicationactiveframeswitchingevents) | 1 |
| [ISEApplicationLicenseEvents](#iseapplicationlicenseevents) | 1 |
| [ISEApplicationDocumentLoadingEvents](#iseapplicationdocumentloadingevents) | 1 |
| [ISEAddInEventsEx](#iseaddineventsex) | 1 |
| [ISEAddInEventsEx2](#iseaddineventsex2) | 4 |
| [ISEAddInEdgeBarEvents](#iseaddinedgebarevents) | 3 |
| [ISEAddInEdgeBarEventsEx](#iseaddinedgebareventsex) | 3 |
| [ISEAssemblyChangeEvents](#iseassemblychangeevents) | 2 |
| [ISEAssemblyConfigurationChangeEvents](#iseassemblyconfigurationchangeevents) | 2 |
| [ISEAssemblyRecomputeEvents](#iseassemblyrecomputeevents) | 5 |
| [ISEAssemblyFamilyEvents](#iseassemblyfamilyevents) | 6 |
| [ISEAssemblyFamilyEvents2](#iseassemblyfamilyevents2) | 8 |
| [ISEFamilyOfPartsEvents](#isefamilyofpartsevents) | 3 |
| [ISEFamilyOfPartsExEvents](#isefamilyofpartsexevents) | 3 |
| [ISEDividePartEvents](#isedividepartevents) | 3 |
| [ISEDrawingViewEvents](#isedrawingviewevents) | 1 |
| [ISEPartsListEvents](#isepartslistevents) | 1 |
| [ISEDraftBendTableEvents](#isedraftbendtableevents) | 1 |
| [ISEConnectorTableEvents](#iseconnectortableevents) | 1 |
| [ISEBlockTableEvents](#iseblocktableevents) | 1 |
| [ISECommandInfoEx](#isecommandinfoex) | 1 |
| [ISEAssemblyPhysicalPropertiesChangeEvents](#iseassemblyphysicalpropertieschangeevents) | 2 |
| [ISEPhysicalPropertiesChangeEvents](#isephysicalpropertieschangeevents) | 2 |
| [ISELocateFilterEvents](#iselocatefilterevents) | 1 |
| [ISECommandEx](#isecommandex) | 1 |
| [ISECommandEx2](#isecommandex2) | 2 |
| [ISEAddInEx](#iseaddinex) | 2 |
| [ISEAddInEx2](#iseaddinex2) | 1 |
| [ISEAddInSaveAsTranslatorEvents](#iseaddinsaveastranslatorevents) | 3 |
| [ISEAddInSaveAsTranslator](#iseaddinsaveastranslator) | 2 |
| [ISolidEdgeAddIn](#isolidedgeaddin) | 3 |
| [ISolidEdgeBar](#isolidedgebar) | 3 |
| [ISolidEdgeBarEx](#isolidedgebarex) | 1 |
| [ISolidEdgeBarEx2](#isolidedgebarex2) | 3 |
| [ISolidEdgeRibbonBar](#isolidedgeribbonbar) | 9 |
| [ISolidEdgeRibbonBarEx](#isolidedgeribbonbarex) | 1 |
| [ISolidEdgeCommandBar](#isolidedgecommandbar) | 20 |
| [ISEECEventsEx](#iseeceventsex) | 1 |
| [ISESketchRecomputeEvents](#isesketchrecomputeevents) | 4 |
| [_IVariableAuto](#_ivariableauto) | 59 |
| [_IVariableListAuto](#_ivariablelistauto) | 5 |
| [_IVariablesAuto](#_ivariablesauto) | 17 |
| [_IInterpartLinkAuto](#_iinterpartlinkauto) | 5 |
| [_IInterpartLinksAuto](#_iinterpartlinksauto) | 4 |
| [_ISensorAuto](#_isensorauto) | 26 |
| [_ISensorsAuto](#_isensorsauto) | 8 |
| [_ISheetMetalSensorsAuto](#_isheetmetalsensorsauto) | 9 |
| [_ISectionViewAuto](#_isectionviewauto) | 30 |
| [_ISectionViewsAuto](#_isectionviewsauto) | 8 |
| [_ILayerAuto](#_ilayerauto) | 29 |
| [_ILayersAuto](#_ilayersauto) | 7 |
| [_ILinearStyleAuto](#_ilinearstyleauto) | 18 |
| [_IFillStyleAuto](#_ifillstyleauto) | 24 |
| [_IHatchPatternStyleAuto](#_ihatchpatternstyleauto) | 52 |
| [_ILinearStylesAuto](#_ilinearstylesauto) | 9 |
| [_IFillStylesAuto](#_ifillstylesauto) | 9 |
| [_IHatchPatternStylesAuto](#_ihatchpatternstylesauto) | 7 |
| [_IDashStyleAuto](#_idashstyleauto) | 13 |
| [_IDashStylesAuto](#_idashstylesauto) | 7 |
| [_IFaceStyleAuto](#_ifacestyleauto) | 182 |
| [_IFaceStylesAuto](#_ifacestylesauto) | 8 |
| [_ITextStyleAuto](#_itextstyleauto) | 25 |
| [_ITextStylesAuto](#_itextstylesauto) | 9 |
| [_ITextCharStyleAuto](#_itextcharstyleauto) | 25 |
| [_ITextCharStylesAuto](#_itextcharstylesauto) | 7 |
| [_ISymbol2dAuto](#_isymbol2dauto) | 66 |
| [_ISymbolsAuto](#_isymbolsauto) | 7 |
| [_ISymbolPropertiesAuto](#_isymbolpropertiesauto) | 5 |
| [_IViewStyleAuto](#_iviewstyleauto) | 105 |
| [_IViewStylesAuto](#_iviewstylesauto) | 9 |
| [_IReferenceAuto](#_ireferenceauto) | 11 |
| [_IRoutingSlipAuto](#_iroutingslipauto) | 26 |
| [_IPropertySetsAuto](#_ipropertysetsauto) | 6 |
| [_IPropertiesAuto](#_ipropertiesauto) | 9 |
| [_IPropertyAuto](#_ipropertyauto) | 6 |
| [_IPropertyExAuto](#_ipropertyexauto) | 1 |
| [_ISummaryInfoAuto](#_isummaryinfoauto) | 38 |
| [_IAttributeSetsAuto](#_iattributesetsauto) | 5 |
| [_IAttributeSetAuto](#_iattributesetauto) | 6 |
| [_IAttributeAuto](#_iattributeauto) | 4 |
| [_IAttributeQueryAuto](#_iattributequeryauto) | 3 |
| [_IQueryObjectsAuto](#_iqueryobjectsauto) | 4 |
| [_IHighlightSetsAuto](#_ihighlightsetsauto) | 6 |
| [_IHighlightSetAuto](#_ihighlightsetauto) | 12 |
| [_ISEGenericCollectionAuto](#_isegenericcollectionauto) | 5 |
| [_ISolidEdgeDocumentAuto](#_isolidedgedocumentauto) | 59 |
| [_IPredefineRelationProducerAuto](#_ipredefinerelationproducerauto) | 25 |
| [_ICPDInitializerInsightXTAuto](#_icpdinitializerinsightxtauto) | 6 |
| [_ICPDInitializerAuto](#_icpdinitializerauto) | 6 |
| [_IInterDocumentUpdateAuto](#_iinterdocumentupdateauto) | 5 |
| [_ISteeringWheelAuto](#_isteeringwheelauto) | 5 |

### <a name="_iapplicationauto"></a>`_IApplicationAuto`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `Activate` | — | HRESULT |
| `get ActiveDocument` | [out] ActiveDocument: IDispatch* | HRESULT |
| `get ActiveEnvironment` | [out] ActiveEnvironment: BSTR* | HRESULT |
| `get ActivePrinter` | [out] ActivePrinter: BSTR* | HRESULT |
| `get ActiveSelectSet` | [out] ActiveSelectSet: SelectSet** | HRESULT |
| `get ActiveStatusBarPart` | [out] ActiveStatusBarPart: int* | HRESULT |
| `put ActiveStatusBarPart` | ActiveStatusBarPart: int | HRESULT |
| `get ActiveWindow` | [out] ActiveWindow: IDispatch* | HRESULT |
| `get Application` | [out] Application: Application** | HRESULT |
| `get Caption` | [out] Caption: BSTR* | HRESULT |
| `put Caption` | Caption: BSTR | HRESULT |
| `get DefaultFilePath` | [out] Path: BSTR* | HRESULT |
| `put DefaultFilePath` | Path: BSTR | HRESULT |
| `put DelayCompute` | DelayCompute: bool | HRESULT |
| `get DelayCompute` | [out] DelayCompute: bool* | HRESULT |
| `get DisplayAlerts` | [out] DisplayAlerts: bool* | HRESULT |
| `put DisplayAlerts` | DisplayAlerts: bool | HRESULT |
| `get DisplayFullScreen` | [out] DisplayFullScreen: bool* | HRESULT |
| `put DisplayFullScreen` | DisplayFullScreen: bool | HRESULT |
| `get DisplayRecentFiles` | [out] DisplayRecentFiles: bool* | HRESULT |
| `put DisplayRecentFiles` | DisplayRecentFiles: bool | HRESULT |
| `get DisplayRecentFilesCount` | [out] DisplayRecentFilesCount: int* | HRESULT |
| `put DisplayRecentFilesCount` | DisplayRecentFilesCount: int | HRESULT |
| `get Documents` | [out] Documents: Documents** | HRESULT |
| `get Environments` | [out] Environments: Environments** | HRESULT |
| `GetOpenFileName` | [out] LinksUpdate: LinksUpdateOption*, [out] AltLinkPath: BSTR*, [out] DocAccess: DocumentAccess*, [out] OptNotify: NotifyOption*, [out] DocRelationAutoServer: IDispatch*, [opt]FileFilter: VARIANT, [opt]FilterIndex: VARIANT, [opt]Title: VARIANT, [opt]IgnoreWarnings: VARIANT, [out] ReturnedName: VARIANT* | HRESULT |
| `SearchDocuments` | bUseSearchScope: bool, bstrFolders: BSTR, bIncludeSubFolders: bool, [out] ListOfFoundDocuments: VARIANT*, [out] iNumDocsFound: int*, [opt]varFileFilterOrText: VARIANT, [opt]PropertyList: VARIANT, [opt]ConditionList: VARIANT, [opt]PropertyValueList: VARIANT, [opt]varNumProps: VARIANT, [opt]varCheckModified: VARIANT, [opt]varNumberOfDays: VARIANT, [opt][out] ListOfTitles: VARIANT*, [opt][out] ListOfSubjects: VARIANT*, [opt][out] ListOfModifiedDates: VARIANT* | int |
| `GetSaveAsFileName` | [out] LinkSaveOption: int*, [out] SelectedFilter: int*, [opt]InitialFilename: VARIANT, [opt]FileFilter: VARIANT, [opt]FilterIndex: VARIANT, [opt]Title: VARIANT, [opt]IsTemplate: VARIANT, [out] ReturnedName: VARIANT* | HRESULT |
| `FindFile` | [out] ReturnedNameOrBool: VARIANT* | HRESULT |
| `GetDirectoryName` | [out] ReturnedNameOrBool: VARIANT* | HRESULT |
| `get Height` | [out] Height: int* | HRESULT |
| `put Height` | Height: int | HRESULT |
| `get hWnd` | [out] hWnd: int* | HRESULT |
| `get Interactive` | [out] Interactive: bool* | HRESULT |
| `put Interactive` | Interactive: bool | HRESULT |
| `get Left` | [out] Left: int* | HRESULT |
| `put Left` | Left: int | HRESULT |
| `MailLogoff` | — | HRESULT |
| `MailLogon` | [opt]Name: VARIANT, [opt]Password: VARIANT, [opt]DownloadNewMail: VARIANT | HRESULT |
| `get MailSession` | [out] MailSession: int* | HRESULT |
| `get Name` | [out] Name: BSTR* | HRESULT |
| `get Parent` | [out] Parent: Application** | HRESULT |
| `Quit` | — | HRESULT |
| `get ScreenUpdating` | [out] ScreenUpdating: bool* | HRESULT |
| `put ScreenUpdating` | ScreenUpdating: bool | HRESULT |
| `get StatusBar` | [out] StatusBar: BSTR* | HRESULT |
| `put StatusBar` | StatusBar: BSTR | HRESULT |
| `get StatusBarDelayUpdate` | [out] StatusBarDelayUpdate: bool* | HRESULT |
| `put StatusBarDelayUpdate` | StatusBarDelayUpdate: bool | HRESULT |
| `get StatusBarHeight` | [out] StatusBarHeight: int* | HRESULT |
| `get StatusBarPartCount` | [out] StatusBarPartCount: int* | HRESULT |
| `put StatusBarPartCount` | StatusBarPartCount: int | HRESULT |
| `get StatusBarPartWidth` | [out] StatusBarPartWidth: int* | HRESULT |
| `put StatusBarPartWidth` | StatusBarPartWidth: int | HRESULT |
| `get StatusBarVisible` | [out] StatusBarVisible: bool* | HRESULT |
| `put StatusBarVisible` | StatusBarVisible: bool | HRESULT |
| `get Top` | [out] Top: int* | HRESULT |
| `put Top` | Top: int | HRESULT |
| `get UsableHeight` | [out] UsableHeight: int* | HRESULT |
| `get UsableWidth` | [out] UsableWidth: int* | HRESULT |
| `get UserName` | [out] UserName: BSTR* | HRESULT |
| `put UserName` | UserName: BSTR | HRESULT |
| `get Value` | [out] Value: BSTR* | HRESULT |
| `get Version` | [out] Version: BSTR* | HRESULT |
| `get Visible` | [out] Visible: bool* | HRESULT |
| `put Visible` | Visible: bool | HRESULT |
| `get Width` | [out] Width: int* | HRESULT |
| `put Width` | Width: int | HRESULT |
| `get Windows` | [out] Windows: Windows** | HRESULT |
| `get WindowState` | [out] WindowState: int* | HRESULT |
| `put WindowState` | WindowState: int | HRESULT |
| `get ApplicationEvents` | [out] Events: ApplicationEvents** | HRESULT |
| `get ApplicationWindowEvents` | [out] WindowEvents: ApplicationWindowEvents** | HRESULT |
| `get ActiveDocumentType` | [out] Type: DocumentTypeConstants* | HRESULT |
| `get FileUIEvents` | [out] Events: FileUIEvents** | HRESULT |
| `get BeforeFileSaveAsEvents` | [out] Events: BeforeFileSaveAsEvents** | HRESULT |
| `StartCommand` | CommandID: SolidEdgeCommandConstants | HRESULT |
| `CommandEnabled` | CommandID: int, strEnvironment: BSTR, [out] bLicensed: bool*, [out] bUnknownCmd: bool*, [out] bEnabled: bool* | HRESULT |
| `CreateCommand` | CmdFlags: int, [out] Cmd: Command** | HRESULT |
| `ReplaceReference` | [opt]FromReference: VARIANT, [opt]ToReference: VARIANT, [opt]Scope: VARIANT, [opt]Recursive: VARIANT | HRESULT |
| `RunMacro` | Filename: BSTR | HRESULT |
| `get AddIns` | [out] AddIns: AddIns** | HRESULT |
| `get EnableStereo` | [out] EnableStereo: bool* | HRESULT |
| `put EnableStereo` | EnableStereo: bool | HRESULT |
| `get EdgeBarVisible` | [out] EdgeBarVisible: bool* | HRESULT |
| `put EdgeBarVisible` | EdgeBarVisible: bool | HRESULT |
| `get FeatureLibraryEvents` | [out] Events: FeatureLibraryEvents** | HRESULT |
| `GetGlobalParameter` | Parameter: ApplicationGlobalConstants, [in,out] Value: VARIANT* | HRESULT |
| `SetGlobalParameter` | Parameter: ApplicationGlobalConstants, Value: VARIANT | HRESULT |
| `get ActiveObject` | Type: SeObjectType, [out] ActiveObject: IDispatch* | HRESULT |
| `get Insight` | [out] InsightObject: Insight** | HRESULT |
| `get ApplicationV8AfterDocumentOpenEvent` | [out] EventObject: ApplicationV8DocumentOpenEvent** | HRESULT |
| `SetOLERequestPendingTimeout` | [opt]SetOLERequestPendingTimeout: VARIANT | HRESULT |
| `SetOLEServerBusyTimeout` | [opt]SetOLEServerBusyTimeout: VARIANT | HRESULT |
| `get FeatureSelectedFromPFEvents` | [out] Events: FeatureSelectedFromPFEvents** | HRESULT |
| `CreateSEDocumentFromTDMAuto` | bstrHostName: BSTR, bstrServerName: BSTR, bstrFolderLocation: BSTR, bstrProject: BSTR, bstrLibrary: BSTR, bstrItemGUID: BSTR, bstrVersionGUID: BSTR, bstrVersionNumber: BSTR, SEDocType: DocumentTypeConstants, bstrAssemblyTemplate: BSTR, bstrPartTemplate: BSTR | HRESULT |
| `CreateSEDraftDocFromDXFAuto` | bstrDxfFileName: BSTR, bstrDraftFileLocation: BSTR, bstrDraftTemplateFile: BSTR, bstrclsidDoc: BSTR | HRESULT |
| `CreateSEDocumentFromForeignFile` | bstrForeignFilePath: BSTR, bstrSEFileLocation: BSTR, bstrTemplatePath: BSTR, bstrClsid: BSTR, MigrationType: BulkMigrationTypeConstants | HRESULT |
| `GetTemplateFileName` | [out] DocType: DocumentTypeConstants*, [opt]FileFilter: VARIANT, [out] ReturnedName: BSTR* | HRESULT |
| `GetDefaultTemplatePath` | DocType: DocumentTypeConstants, [out] TemplatePath: BSTR* | HRESULT |
| `SetDefaultTemplatePath` | DocType: DocumentTypeConstants, TemplatePath: BSTR | HRESULT |
| `DoIdle` | — | HRESULT |
| `GetMaterialTable` | [out] MatTable: MatTable** | HRESULT |
| `get NewFileUIEvents` | [out] Events: NewFileUIEvents** | HRESULT |
| `SEAdminUpdate` | — | HRESULT |
| `get ShortcutMenuEvents` | [out] Events: ShortcutMenuEvents** | HRESULT |
| `get ApprenticeMode` | [out] ApprenticeModeOn: bool* | HRESULT |
| `put ApprenticeMode` | ApprenticeModeOn: bool | HRESULT |
| `get ShowStartupScreen` | [out] ShowStartupScreen: bool* | HRESULT |
| `put ShowStartupScreen` | ShowStartupScreen: bool | HRESULT |
| `get SolidEdgeTCE` | [out] SolidEdgeTCEObject: SolidEdgeTCE** | HRESULT |
| `get SolidEdgeInsightXT` | [out] SolidEdgeInsightXTObject: SolidEdgeInsightXT** | HRESULT |
| `get IsIdling` | MilliSec: int, [out] IsIdling: bool* | HRESULT |
| `get ResolveLink` | [out] ResolveLink: bool* | HRESULT |
| `put ResolveLink` | ResolveLink: bool | HRESULT |
| `DisableEventsForGivenAddIn` | bstrClsid: BSTR | HRESULT |
| `SetAddInInterfaces` | bstrClsid: BSTR, pSaUnknownPtrs: SAFEARRAY(IUnknown)* | HRESULT |
| `EnableEventsForGivenAddIn` | bstrClsid: BSTR | HRESULT |
| `ShowCommand` | nCmdID: int, Highlight: bool | HRESULT |
| `get ProcessID` | [out] ProcessID: int* | HRESULT |
| `get SEECEvents` | [out] Events: SEECEvents** | HRESULT |
| `get SESPEvents` | [out] Events: SESPEvents** | HRESULT |
| `get BiDMEvents` | [out] Events: BiDMEvents** | HRESULT |
| `WriteDocumentFormulaIntoXML` | outputXMLPath: BSTR, knownResXMLPath: BSTR, [opt]bDeepTree: bool | HRESULT |
| `SetBuiltInATPRunningFlagAndATPID` | bRunningFlag: bool, strATPID: BSTR | HRESULT |
| `SetValuesForBIDMCPD` | pvarListOfValues: VARIANT* | HRESULT |
| `SetMessageForBIDMCPD` | pvarListOfMessages: VARIANT* | HRESULT |
| `SetBIDMATPInfo` | bstrATPClassName: BSTR, bstrATPName: BSTR, ATPId: int | HRESULT |
| `GetCountOfOpenModelsInFemap` | [out] nOpenModelsInFemap: int* | HRESULT |
| `get Customization` | [out] Customization: Customization** | HRESULT |
| `GetDraftPrintUtility` | [out] DraftPrintUtility: IDispatch* | HRESULT |
| `ArrangeWindows` | Style: ArrangeWindowsStyles | HRESULT |
| `GetOpenFileNameWithOptions` | dwFlagForOpen: uint, [out] LinksUpdate: LinksUpdateOption*, [out] AltLinkPath: BSTR*, [out] DocAccess: DocumentAccess*, [out] OptNotify: NotifyOption*, [out] DocRelationAutoServer: IDispatch*, [opt]FileFilter: VARIANT, [opt]FilterIndex: VARIANT, [opt]Title: VARIANT, [opt]IgnoreWarnings: VARIANT, [out] ReturnedName: VARIANT* | HRESULT |
| `SEGetFileVersionInfo` | Filename: BSTR, [out] DocType: DocumentTypeConstants*, [out] CreatedVersion: BSTR*, [out] LastSavedVersion: BSTR*, [out] GeometricVersion: uint* | HRESULT |
| `GenerateMasterImportListForDataPrep` | psalistOfFilesFolders: SAFEARRAY(VARIANT)*, IncludeSubFolders: bool, FileTypes: uint, TimeStamp: BSTR, WorkingFolderLocation: BSTR, [out] OrderedCSVFilePath: BSTR*, [out] UnOrderedCSVFilePath: BSTR*, [out] BrokenLinkXMLFilePath: BSTR*, [out] iNumberOfBrokenLinks: int*, [out] LinkReportFilePath: BSTR*, [out] ErrorMsg: BSTR*, [out] ErrCode: GenerateMasterImportListError* | HRESULT |
| `FindWhereUsedDocuments` | DocumentPathName: VARIANT, psalistOfDirectories: SAFEARRAY(VARIANT)*, IncludeSubFolders: bool, psaFilterList: SAFEARRAY(VARIANT)*, [out] pVarResultSetWU: VARIANT* | HRESULT |
| `QuerySystemInformation` | Search: BSTR, [out] Results: VARIANT* | HRESULT |
| `DisableBuilInDataMgmt` | bDisableBuiltInDM: bool | HRESULT |
| `get RegistryPath` | [out] RegistryPath: BSTR* | HRESULT |
| `get AppDataFolder` | [out] AppDataFolder: BSTR* | HRESULT |
| `GetRevisionLinkInfo` | bstrFilePath: BSTR, [out] pVarRevisionRoot: VARIANT*, [out] pVarRevisedFrom: VARIANT* | HRESULT |
| `GetRevisionsHistory` | PathName: BSTR, psaScope: SAFEARRAY(VARIANT)*, [out] psaRevHistoryFileNameList: VARIANT*, [out] psaRevHistoryRevisionFromList: VARIANT* | HRESULT |
| `OpenDraft` | — | HRESULT |
| `GetLatestRevision` | PathName: BSTR, psaScope: SAFEARRAY(VARIANT)*, [out] bLatestRevPath: BSTR*, [out] bLatestReleasedRevPath: BSTR* | HRESULT |
| `GetTopLevelAssemblyFileNames` | FileNames: SAFEARRAY(BSTR)*, [out] TopLevelAssemblyFileNames: SAFEARRAY(BSTR)* | HRESULT |
| `FindSEDocumentsContainingText` | text_to_search: BSTR, psaScope: SAFEARRAY(VARIANT)*, file_types: BSTR, bIncludeSubFolders: bool, [out] FilesFoundInSearch: SAFEARRAY(BSTR)* | HRESULT |
| `ResetConfigFile` | eResetType: ConfigResetType, eConfigFileType: ConfigForForeignFileType, eTranslationMode: FileTranslationMode, [opt]GroupName: BSTR, [opt]pFile: VARIANT*, [opt]pTemplateName: VARIANT* | HRESULT |
| `GetNextDocumentNumbers` | countOfFiles: int, [out] pVarPrefix: VARIANT*, [out] pVarDocNumbs: VARIANT*, [out] iRetValue: int* | HRESULT |
| `Get_Set_UseBiDM_SEOption` | bGet: bool, [out] iValue: bool* | HRESULT |
| `Get_Set_FileNamingRule` | bGet: bool, [out] bValue: bool* | HRESULT |
| `GetDocNameFormulaForFile` | bFilename: BSTR, [out] bstrDocFormula: BSTR* | HRESULT |
| `BiDM_RegisterCustomProps` | bProcessCustomPropsFromPropSeed: bool, bProcessCustomPropsFromTemplates: bool | HRESULT |
| `PerformSolidEdgeWorkflow` | bstrFilePath: BSTR, [in,out] pSEWorkflowInfo: SolidEdgeWorkflowInfo* | HRESULT |
| `GetSolidEdgeWorkflowInformation` | bstrFilePath: BSTR, [out] pSEWorkflowQueryInfo: SolidEdgeWorkflowQueryInfo* | HRESULT |
| `SuspendMRU` | — | HRESULT |
| `ResumeMRU` | — | HRESULT |
| `ClearMRU` | — | HRESULT |
| `AbortCommand` | AbortAllCommands: bool | HRESULT |
| `Publish3DPDF` | bstrInputFileOrFolderPath: BSTR, bstr3DPDFTemplateFile: BSTR, [opt]bIncludeSubFolders: bool, [opt]bstrOutputFolderPath: BSTR, [opt]bstr3DPDFFileName: BSTR, [opt]bOpenPDFAfterPublish: bool, [opt]bPublishHTML: bool, [opt]bAddNextPrevButtons: bool, [opt]bAddFileCustomPropsToPDF: bool, [opt]bSelectAllPMIModelViewsForPDF: bool, [opt]bstrDefaultModelView: BSTR, [opt]bGenAndAttachSTEPAP242: bool, [opt]bGenAndAttachJT: bool, [opt]ListOfAttachments: VARIANT, [out] bSuccess: bool* | HRESULT |
| `ConvertByFilePath` | InputFileOrFolderPath: BSTR, OutputFileOrFolderPath: BSTR, [out] bSuccess: bool* | HRESULT |
| `get CommandPredictionLearningMode` | [out] pbEnabled: bool* | HRESULT |
| `put CommandPredictionLearningMode` | pbEnabled: bool | HRESULT |
| `get SoldToID` | [out] SoldToID: BSTR* | HRESULT |
| `GetListOfTopLevelAssembliesFromFolder` | FolderPath: BSTR, [out] TopAssembliesList: SAFEARRAY(BSTR)* | HRESULT |
| `get LicenseType` | [out] pbstrLicenseType: BSTR* | HRESULT |
| `GenerateSourceImportListForDataPrep` | psalistOfFilesFolders: SAFEARRAY(VARIANT)*, IncludeSubFolders: bool, FileTypes: uint, TimeStamp: BSTR, WorkingFolderLocation: BSTR, [out] OrderedCSVFilePath: BSTR*, [out] UnOrderedCSVFilePath: BSTR*, [out] BrokenLinkXMLFilePath: BSTR*, [out] iNumberOfBrokenLinks: int*, [out] LinkReportFilePath: BSTR*, [out] ErrorMsg: BSTR*, [out] ErrCode: GenerateSourceImportListError* | HRESULT |
| `get ActiveFramehWnd` | [out] hWnd: int* | HRESULT |
| `get DynamicVisualization` | [out] DynamicVisualizationObject: DynamicVisualization** | HRESULT |
| `get LicenseHandle` | [out] LicenseHandle: vt20* | HRESULT |
| `OpenNoteLibrary` | — | HRESULT |
| `CloseNoteLibrary` | — | HRESULT |
| `GetSavedNoteList` | [out] saSavedNote: SAFEARRAY(BSTR)* | HRESULT |
| `GetSavedNote` | bstrNoteName: BSTR, [out] textBox: IUnknown* | HRESULT |
| `AddNote` | bstrNoteName: BSTR, bstrText: BSTR, bNoteOverWrite: bool | HRESULT |
| `Publish3DPDFEx` | bstrInputFileOrFolderPath: BSTR, bstr3DPDFTemplateFile: BSTR, [opt]bIncludeSubFolders: bool, [opt]bstrOutputFolderPath: BSTR, [opt]bstr3DPDFFileName: BSTR, [opt]bOpenPDFAfterPublish: bool, [opt]bPublishHTML: bool, [opt]bAddNextPrevButtons: bool, [opt]bAddFileCustomPropsToPDF: bool, [opt]bSelectAllPMIModelViewsForPDF: bool, [opt]bstrDefaultModelView: BSTR, [opt]bGenAndAttachSTEPAP242: bool, [opt]bGenAndAttachJT: bool, [opt]ListOfAttachments: VARIANT, [opt]bSelectAllNamedViewsForPDF: bool, [opt]ListOfNamedViews: VARIANT, [opt]ListOfPMIModelViews: VARIANT, [out] bSuccess: bool* | HRESULT |
| `GetActiveCommand` | [out] nCmdID: int* | HRESULT |
| `get OpenNonSolidEdgeFileUIEvents` | [out] Events: OpenNonSolidEdgeFileUIEvents** | HRESULT |

### <a name="_iselectsetauto"></a>`_ISelectSetAuto`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `get Application` | [out] Application: Application** | HRESULT |
| `get Parent` | [out] Parent: IDispatch* | HRESULT |
| `get Count` | [out] Count: int* | HRESULT |
| `Add` | Dispatch: IDispatch | HRESULT |
| `Remove` | Index: VARIANT | HRESULT |
| `RemoveAll` | — | HRESULT |
| `Item` | Index: VARIANT, [out] Item: IDispatch* | HRESULT |
| `get _NewEnum` | [out] Unknown: IUnknown* | HRESULT |
| `Copy` | — | HRESULT |
| `Cut` | — | HRESULT |
| `Delete` | — | HRESULT |
| `AddAll` | — | HRESULT |
| `get Type` | [out] Type: ObjectType* | HRESULT |
| `CopyProfile` | — | HRESULT |
| `CutProfile` | — | HRESULT |
| `SuspendDisplay` | — | HRESULT |
| `ResumeDisplay` | — | HRESULT |
| `RefreshDisplay` | — | HRESULT |

### <a name="_idocumentsauto"></a>`_IDocumentsAuto`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `get Application` | [out] Application: Application** | HRESULT |
| `Close` | — | HRESULT |
| `get Count` | [out] Count: int* | HRESULT |
| `get Parent` | [out] Parent: Application** | HRESULT |
| `Add` | [opt]ProgID: VARIANT, [opt]TemplateDoc: VARIANT, [out] NewDocument: IDispatch* | HRESULT |
| `Item` | Index: VARIANT, [out] Item: IDispatch* | HRESULT |
| `Open` | Filename: BSTR, [opt]DocRelationAutoServer: VARIANT, [opt]AltPath: VARIANT, [opt]RecognizeFeaturesIfPartTemplate: VARIANT, [opt]RevisionRuleOption: VARIANT, [opt]StopFileOpenIfRevisionRuleNotApplicable: VARIANT, [out] Document: IDispatch* | HRESULT |
| `get _NewEnum` | [out] Unknown: IUnknown* | HRESULT |
| `OpenWithTemplate` | Filename: BSTR, Template: BSTR, [opt]RecognizeFeaturesIfPartTemplate: VARIANT, [out] Document: IDispatch* | HRESULT |
| `get TemplatePath` | [out] TemplatePath: BSTR* | HRESULT |
| `get AutoCadConfigFile` | [out] AutoCadConfigFile: BSTR* | HRESULT |
| `put AutoCadConfigFile` | AutoCadConfigFile: BSTR | HRESULT |
| `SetForeignFileConfigValue` | DocumentProgID: BSTR, Filename: BSTR, SectionName: BSTR, Name: BSTR, Value: BSTR | HRESULT |
| `GetForeignFileConfigValue` | DocumentProgID: BSTR, Filename: BSTR, SectionName: BSTR, Name: BSTR, [out] Value: BSTR* | HRESULT |
| `CloseDocument` | Filename: BSTR, [opt]SaveChanges: VARIANT, [opt]SaveAsFileName: VARIANT, [opt]RouteWorkbook: VARIANT, [opt]DoIdle: VARIANT | HRESULT |
| `get TemplateManager` | [out] ppTemplateManager: TemplateManager** | HRESULT |
| `OpenWithFileOpenDialog` | [opt]Filename: VARIANT, [opt]DialogTitle: VARIANT, [opt]Flags: VARIANT, [out] Document: IDispatch* | HRESULT |

### <a name="_itemplatemanagerauto"></a>`_ITemplateManagerAuto`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `get Application` | [out] ppApplication: Application** | HRESULT |
| `get Parent` | [out] ppParent: IDispatch* | HRESULT |
| `GetActiveTemplates` | [out] bstrActiveListPath: BSTR*, [out] eActiveListType: TemplatesListType*, [out] astrActiveTemplates: SAFEARRAY(BSTR)* | HRESULT |

### <a name="_ienvironmentsauto"></a>`_IEnvironmentsAuto`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `get Application` | [out] Application: Application** | HRESULT |
| `get Count` | [out] Count: int* | HRESULT |
| `get Parent` | [out] Parent: Application** | HRESULT |
| `Item` | Index: VARIANT, [out] Item: Environment** | HRESULT |
| `get _NewEnum` | [out] Unknown: IUnknown* | HRESULT |

### <a name="_ienvironmentauto"></a>`_IEnvironmentAuto`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `get Application` | [out] Application: Application** | HRESULT |
| `get Caption` | [out] Caption: BSTR* | HRESULT |
| `put Caption` | Caption: BSTR | HRESULT |
| `get Index` | [out] Index: int* | HRESULT |
| `get Name` | [out] Name: BSTR* | HRESULT |
| `get Parent` | [out] Parent: Environments** | HRESULT |
| `get CommandBars` | [out] CommandBars: CommandBars** | HRESULT |
| `get Accelerators` | [out] Accelerators: Accelerators** | HRESULT |
| `get SubTypeName` | [out] SubTypeName: BSTR* | HRESULT |
| `get CommandCategories` | [out] CommandCategories: CommandCategories** | HRESULT |
| `get CATID` | [out] CATID: BSTR* | HRESULT |
| `get CustomizeDisplayName` | [out] CustomizeDisplayName: BSTR* | HRESULT |
| `get CommandInfo` | CommandID: int, [out] Info: CommandInfo** | HRESULT |

### <a name="isecommandbars"></a>`ISECommandBars`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `get ActiveMenuBar` | [out] CommandBar: CommandBar** | HRESULT |
| `get Application` | [out] Application: IDispatch* | HRESULT |
| `get Count` | [out] Count: int* | HRESULT |
| `get DisplayTooltips` | [out] DisplayTooltips: bool* | HRESULT |
| `put DisplayTooltips` | DisplayTooltips: bool | HRESULT |
| `get LargeButtons` | [out] LargeButtons: bool* | HRESULT |
| `put LargeButtons` | LargeButtons: bool | HRESULT |
| `get Parent` | [out] Parent: IDispatch* | HRESULT |
| `Add` | [opt]Name: VARIANT, [opt]Position: VARIANT, [opt]MenuBar: VARIANT, [opt]Temporary: VARIANT, [out] CommandBar: CommandBar** | HRESULT |
| `FindControl` | [opt]Type: VARIANT, [opt]Id: VARIANT, [opt]Tag: VARIANT, [opt]Visible: VARIANT, [out] CommandBarControl: CommandBarControl** | HRESULT |
| `Item` | Index: VARIANT, [out] CommandBar: CommandBar** | HRESULT |
| `get _NewEnum` | [out] Unknown: IUnknown* | HRESULT |

### <a name="isecommandbar"></a>`ISECommandBar`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `get Application` | [out] Application: IDispatch* | HRESULT |
| `get BuiltIn` | [out] BuiltIn: bool* | HRESULT |
| `get Controls` | [out] CommandBarControls: CommandBarControls** | HRESULT |
| `get Enabled` | [out] Enabled: bool* | HRESULT |
| `put Enabled` | Enabled: bool | HRESULT |
| `get Height` | [out] Height: int* | HRESULT |
| `put Height` | Height: int | HRESULT |
| `get Index` | [out] Index: int* | HRESULT |
| `get Left` | [out] Left: int* | HRESULT |
| `put Left` | Left: int | HRESULT |
| `get Name` | [out] Name: BSTR* | HRESULT |
| `get NameLocal` | [out] NameLocal: BSTR* | HRESULT |
| `put NameLocal` | NameLocal: BSTR | HRESULT |
| `get Parent` | [out] Parent: Environment** | HRESULT |
| `get Position` | [out] Position: SeBarPosition* | HRESULT |
| `put Position` | Position: SeBarPosition | HRESULT |
| `get Top` | [out] Top: int* | HRESULT |
| `put Top` | Top: int | HRESULT |
| `get Type` | [out] Type: SeBarType* | HRESULT |
| `get Visible` | [out] Visible: bool* | HRESULT |
| `put Visible` | Visible: bool | HRESULT |
| `get Width` | [out] Width: int* | HRESULT |
| `put Width` | Width: int | HRESULT |
| `Delete` | — | HRESULT |
| `FindControl` | [opt]Type: VARIANT, [opt]Id: VARIANT, [opt]Tag: VARIANT, [opt]Visible: VARIANT, [opt]Recursive: VARIANT, [out] CommandBarControl: CommandBarControl** | HRESULT |
| `Reset` | — | HRESULT |
| `ShowPopup` | [opt]x: VARIANT, [opt]y: VARIANT | HRESULT |

### <a name="isecommandbarcontrols"></a>`ISECommandBarControls`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `get Application` | [out] Application: IDispatch* | HRESULT |
| `get Count` | [out] Count: int* | HRESULT |
| `get Parent` | [out] Parent: CommandBar** | HRESULT |
| `Add` | [opt]Type: VARIANT, [opt]Id: VARIANT, [opt]Before: VARIANT, [opt]Temporary: VARIANT, [out] CommandBarControl: CommandBarControl** | HRESULT |
| `Item` | Index: VARIANT, [out] CommandBarControl: CommandBarControl** | HRESULT |
| `get _NewEnum` | [out] Unknown: IUnknown* | HRESULT |

### <a name="isecommandbarcontrol"></a>`ISECommandBarControl`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `get Application` | [out] Application: IDispatch* | HRESULT |
| `get BeginGroup` | [out] BeginGroup: bool* | HRESULT |
| `put BeginGroup` | BeginGroup: bool | HRESULT |
| `get BuiltIn` | [out] BuiltIn: bool* | HRESULT |
| `get BuiltInFace` | [out] BuiltInFace: bool* | HRESULT |
| `put BuiltInFace` | BuiltInFace: bool | HRESULT |
| `get Caption` | [out] Caption: BSTR* | HRESULT |
| `put Caption` | Caption: BSTR | HRESULT |
| `get DescriptionText` | [out] DescriptionText: BSTR* | HRESULT |
| `put DescriptionText` | DescriptionText: BSTR | HRESULT |
| `get Enabled` | [out] Enabled: bool* | HRESULT |
| `put Enabled` | Enabled: bool | HRESULT |
| `get FaceId` | [out] FaceId: int* | HRESULT |
| `put FaceId` | FaceId: int | HRESULT |
| `get Height` | [out] Height: int* | HRESULT |
| `get HelpContextId` | [out] HelpContextId: int* | HRESULT |
| `put HelpContextId` | HelpContextId: int | HRESULT |
| `get HelpFile` | [out] HelpFile: BSTR* | HRESULT |
| `put HelpFile` | HelpFile: BSTR | HRESULT |
| `get Id` | [out] Id: int* | HRESULT |
| `get Index` | [out] Index: int* | HRESULT |
| `get Left` | [out] Left: int* | HRESULT |
| `get OnAction` | [out] OnAction: BSTR* | HRESULT |
| `put OnAction` | OnAction: BSTR | HRESULT |
| `get ParameterText` | [out] ParameterText: BSTR* | HRESULT |
| `put ParameterText` | ParameterText: BSTR | HRESULT |
| `get Parent` | [out] Parent: CommandBar** | HRESULT |
| `get ShortcutText` | [out] ShortcutText: BSTR* | HRESULT |
| `put ShortcutText` | ShortcutText: BSTR | HRESULT |
| `get Tag` | [out] Tag: BSTR* | HRESULT |
| `put Tag` | Tag: BSTR | HRESULT |
| `get TooltipText` | [out] TooltipText: BSTR* | HRESULT |
| `put TooltipText` | TooltipText: BSTR | HRESULT |
| `get Top` | [out] Top: int* | HRESULT |
| `get Type` | [out] Type: SeControlType* | HRESULT |
| `get Visible` | [out] Visible: bool* | HRESULT |
| `put Visible` | Visible: bool | HRESULT |
| `get Width` | [out] Width: int* | HRESULT |
| `Delete` | [opt]Temporary: VARIANT | HRESULT |
| `Execute` | — | HRESULT |
| `Help` | — | HRESULT |
| `LoadFace` | Face: BSTR | HRESULT |

### <a name="iseaccelerators"></a>`ISEAccelerators`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `get Count` | [out] Count: int* | HRESULT |
| `get Parent` | [out] Parent: IDispatch* | HRESULT |
| `Item` | Index: VARIANT, [out] ppAccelerator: Accelerator** | HRESULT |
| `get _NewEnum` | [out] Unknown: IUnknown* | HRESULT |

### <a name="iseaccelerator"></a>`ISEAccelerator`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `get Count` | [out] Count: int* | HRESULT |
| `get Parent` | [out] Parent: IDispatch* | HRESULT |
| `Item` | Index: VARIANT, [out] ppKeyBinding: KeyBinding** | HRESULT |
| `get _NewEnum` | [out] Unknown: IUnknown* | HRESULT |
| `get Type` | [out] Type: AcceleratorTypeConstants* | HRESULT |
| `Reset` | — | HRESULT |
| `Remove` | KeyCode: int | HRESULT |
| `Add` | CommandID: int, KeyCode: int, [out] ppKeyBinding: KeyBinding** | HRESULT |
| `KeyBinding` | KeyCode: int, [out] ppKeyBinding: KeyBinding** | HRESULT |
| `BuildKeyCode` | KeyModifier: int, Key: int, [out] KeyCode: int* | HRESULT |

### <a name="isekeybinding"></a>`ISEKeyBinding`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `get Parent` | [out] Parent: IDispatch* | HRESULT |
| `get CommandID` | [out] CommandID: int* | HRESULT |
| `get CommandString` | [out] CommandString: BSTR* | HRESULT |
| `get KeyString` | [out] KeyString: BSTR* | HRESULT |
| `get KeyCode` | [out] KeyCode: int* | HRESULT |

### <a name="isecommandcategories"></a>`ISECommandCategories`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `get Parent` | [out] Parent: IDispatch* | HRESULT |
| `get Count` | [out] Count: int* | HRESULT |
| `Item` | Index: VARIANT, [out] CommandCategory: CommandCategory** | HRESULT |
| `get _NewEnum` | [out] Unknown: IUnknown* | HRESULT |

### <a name="isecommandcategory"></a>`ISECommandCategory`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `get Caption` | [out] Caption: BSTR* | HRESULT |
| `get Count` | [out] Count: int* | HRESULT |
| `Item` | Index: VARIANT, [out] CommandInfo: CommandInfo** | HRESULT |
| `get _NewEnum` | [out] Unknown: IUnknown* | HRESULT |

### <a name="isecommandinfo"></a>`ISECommandInfo`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `get Caption` | [out] Caption: BSTR* | HRESULT |
| `get Id` | [out] Id: int* | HRESULT |
| `get Tooltip` | [out] Tooltip: BSTR* | HRESULT |
| `get Description` | [out] Description: BSTR* | HRESULT |
| `get BuiltIn` | [out] BuiltIn: bool* | HRESULT |
| `get Icon` | [out] Icon: int* | HRESULT |
| `SaveImage` | Filename: BSTR, [opt]Background: VARIANT | HRESULT |

### <a name="_iwindowsauto"></a>`_IWindowsAuto`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `get Application` | [out] Application: Application** | HRESULT |
| `get Count` | [out] Count: int* | HRESULT |
| `get Parent` | [out] Parent: IDispatch* | HRESULT |
| `Item` | Index: VARIANT, [out] Item: IDispatch* | HRESULT |
| `get _NewEnum` | [out] Unknown: IUnknown* | HRESULT |

### <a name="iseapplicationevents"></a>`ISEApplicationEvents`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `AfterActiveDocumentChange` | theDocument: IDispatch | HRESULT |
| `AfterCommandRun` | theCommandID: int | HRESULT |
| `AfterDocumentOpen` | theDocument: IDispatch | HRESULT |
| `AfterDocumentPrint` | theDocument: IDispatch, hDC: int, ModelToDC: double*, Rect: int* | HRESULT |
| `AfterDocumentSave` | theDocument: IDispatch | HRESULT |
| `AfterEnvironmentActivate` | theEnvironment: IDispatch | HRESULT |
| `AfterNewDocumentOpen` | theDocument: IDispatch | HRESULT |
| `AfterNewWindow` | theWindow: IDispatch | HRESULT |
| `AfterWindowActivate` | theWindow: IDispatch | HRESULT |
| `BeforeCommandRun` | theCommandID: int | HRESULT |
| `BeforeDocumentClose` | theDocument: IDispatch | HRESULT |
| `BeforeDocumentPrint` | theDocument: IDispatch, hDC: int, ModelToDC: double*, Rect: int* | HRESULT |
| `BeforeEnvironmentDeactivate` | theEnvironment: IDispatch | HRESULT |
| `BeforeWindowDeactivate` | theWindow: IDispatch | HRESULT |
| `BeforeQuit` | — | HRESULT |
| `BeforeDocumentSave` | theDocument: IDispatch | HRESULT |

### <a name="iseapplicationwindowevents"></a>`ISEApplicationWindowEvents`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `WindowProc` | hWnd: int, nMsg: int, wParam: int, lParam: int | HRESULT |

### <a name="isefileuievents"></a>`ISEFileUIEvents`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `OnFileOpenUI` | [out] Filename: BSTR*, [out] AppendToTitle: BSTR* | HRESULT |
| `OnFileSaveAsUI` | [out] Filename: BSTR*, [out] AppendToTitle: BSTR* | HRESULT |
| `OnFileNewUI` | [out] Filename: BSTR*, [out] AppendToTitle: BSTR* | HRESULT |
| `OnFileSaveAsImageUI` | [out] Filename: BSTR*, [out] AppendToTitle: BSTR*, [in,out] Width: int*, [in,out] Height: int*, [in,out] ImageQuality: SeImageQualityType* | HRESULT |
| `OnPlacePartUI` | [out] Filename: BSTR*, [out] AppendToTitle: BSTR* | HRESULT |
| `OnCreateInPlacePartUI` | [out] Filename: BSTR*, [out] AppendToTitle: BSTR*, [out] Template: BSTR* | HRESULT |

### <a name="isebeforefilesaveasevents"></a>`ISEBeforeFileSaveAsEvents`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `OnBeforeFileSaveAsUI` | TemplatePath: BSTR | HRESULT |

### <a name="isecommand"></a>`ISECommand`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `get Mouse` | [out] ppMouse: Mouse** | HRESULT |
| `get Window` | [out] ppWindow: CommandWindow** | HRESULT |
| `get Done` | [out] pbDone: bool* | HRESULT |
| `put Done` | pbDone: bool | HRESULT |
| `get OnEditOwnerChange` | [out] plContinueOnChange: int* | HRESULT |
| `put OnEditOwnerChange` | plContinueOnChange: int | HRESULT |
| `get OnEnvironmentChange` | [out] plContinueOnChange: int* | HRESULT |
| `put OnEnvironmentChange` | plContinueOnChange: int | HRESULT |
| `Start` | — | HRESULT |

### <a name="isemouseex3"></a>`ISEMouseEx3`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `get PathfinderLocate` | [out] PathfinderLocate: bool* | HRESULT |
| `put PathfinderLocate` | PathfinderLocate: bool | HRESULT |

### <a name="isemouseex2"></a>`ISEMouseEx2`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `get LocateFrontToBack` | [out] LocateFrontToBack: bool* | HRESULT |
| `put LocateFrontToBack` | LocateFrontToBack: bool | HRESULT |

### <a name="isemouseex"></a>`ISEMouseEx`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `PointOnGraphic` | [out] PointOnGraphicFlag: int*, [out] PointOnGraphic_X: double*, [out] PointOnGraphic_Y: double*, [out] PointOnGraphic_Z: double* | HRESULT |
| `get InterDocumentLocate` | [out] plInterDocumentLocate: bool* | HRESULT |
| `put InterDocumentLocate` | plInterDocumentLocate: bool | HRESULT |

### <a name="isemouse"></a>`ISEMouse`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `put ScaleMode` | plScaleMode: int | HRESULT |
| `get ScaleMode` | [out] plScaleMode: int* | HRESULT |
| `get EnabledMove` | [out] pbMoveEnabled: bool* | HRESULT |
| `put EnabledMove` | pbMoveEnabled: bool | HRESULT |
| `get LastEventWindow` | [out] pWindowDispatch: IDispatch* | HRESULT |
| `get LastUpEventWindow` | [out] pWindowDispatch: IDispatch* | HRESULT |
| `get LastDownEventWindow` | [out] pWindowDispatch: IDispatch* | HRESULT |
| `get LastMoveEventWindow` | [out] pWindowDispatch: IDispatch* | HRESULT |
| `get LastEventShift` | [out] pShift: short* | HRESULT |
| `get LastUpEventShift` | [out] pShift: short* | HRESULT |
| `get LastDownEventShift` | [out] pShift: short* | HRESULT |
| `get LastMoveEventShift` | [out] pShift: short* | HRESULT |
| `get LastEventButton` | [out] pButton: short* | HRESULT |
| `get LastUpEventButton` | [out] pButton: short* | HRESULT |
| `get LastDownEventButton` | [out] pButton: short* | HRESULT |
| `get LastMoveEventButton` | [out] pButton: short* | HRESULT |
| `get LastEventX` | [out] pX: double* | HRESULT |
| `get LastEventY` | [out] pY: double* | HRESULT |
| `get LastEventZ` | [out] pZ: double* | HRESULT |
| `get LastUpEventX` | [out] pX: double* | HRESULT |
| `get LastUpEventY` | [out] pY: double* | HRESULT |
| `get LastUpEventZ` | [out] pZ: double* | HRESULT |
| `get LastDownEventX` | [out] pX: double* | HRESULT |
| `get LastDownEventY` | [out] pY: double* | HRESULT |
| `get LastDownEventZ` | [out] pZ: double* | HRESULT |
| `get LastMoveEventX` | [out] pX: double* | HRESULT |
| `get LastMoveEventY` | [out] pY: double* | HRESULT |
| `get LastMoveEventZ` | [out] pZ: double* | HRESULT |
| `get WindowTypes` | [out] plTypes: int* | HRESULT |
| `put WindowTypes` | plTypes: int | HRESULT |
| `get LastEventType` | [out] plType: int* | HRESULT |
| `get EnabledDrag` | [out] pbEnabledDrag: bool* | HRESULT |
| `put EnabledDrag` | pbEnabledDrag: bool | HRESULT |
| `get LocateMode` | [out] plLocateMode: int* | HRESULT |
| `put LocateMode` | plLocateMode: int | HRESULT |
| `get DynamicsMode` | [out] plDynamicsMode: int* | HRESULT |
| `put DynamicsMode` | plDynamicsMode: int | HRESULT |
| `get PauseLocate` | [out] plPauseLocate: int* | HRESULT |
| `put PauseLocate` | plPauseLocate: int | HRESULT |
| `ClearLocateFilter` | — | HRESULT |
| `AddToLocateFilter` | lFilter: int | HRESULT |

### <a name="isemouseevents"></a>`ISEMouseEvents`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `MouseDown` | sButton: short, sShift: short, dX: double, dY: double, dZ: double, pWindowDispatch: IDispatch, lKeyPointType: int, pGraphicDispatch: IDispatch | HRESULT |
| `MouseUp` | sButton: short, sShift: short, dX: double, dY: double, dZ: double, pWindowDispatch: IDispatch, lKeyPointType: int, pGraphicDispatch: IDispatch | HRESULT |
| `MouseMove` | sButton: short, sShift: short, dX: double, dY: double, dZ: double, pWindowDispatch: IDispatch, lKeyPointType: int, pGraphicDispatch: IDispatch | HRESULT |
| `MouseClick` | sButton: short, sShift: short, dX: double, dY: double, dZ: double, pWindowDispatch: IDispatch, lKeyPointType: int, pGraphicDispatch: IDispatch | HRESULT |
| `MouseDblClick` | sButton: short, sShift: short, dX: double, dY: double, dZ: double, pWindowDispatch: IDispatch, lKeyPointType: int, pGraphicDispatch: IDispatch | HRESULT |
| `MouseDrag` | sButton: short, sShift: short, dX: double, dY: double, dZ: double, pWindowDispatch: IDispatch, DragState: short, lKeyPointType: int, pGraphicDispatch: IDispatch | HRESULT |

### <a name="isecommandwindowevents"></a>`ISECommandWindowEvents`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `WindowProc` | pTheDoc: IDispatch, pTheView: IDispatch, nMsg: uint, wParam: UINT_PTR, lParam: LONG_PTR, [out] lResult: LONG_PTR* | HRESULT |

### <a name="isecommandevents"></a>`ISECommandEvents`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `Activate` | — | HRESULT |
| `Deactivate` | — | HRESULT |
| `Terminate` | — | HRESULT |
| `Idle` | lCount: int, [out] pbMore: bool* | HRESULT |
| `KeyDown` | KeyCode: short*, Shift: short | HRESULT |
| `KeyPress` | KeyAscii: short* | HRESULT |
| `KeyUp` | KeyCode: short*, Shift: short | HRESULT |

### <a name="iseaddins"></a>`ISEAddIns`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `get Application` | [out] Application: IDispatch* | HRESULT |
| `get Count` | [out] Count: int* | HRESULT |
| `get _NewEnum` | [out] Unknown: IUnknown* | HRESULT |
| `Item` | Index: VARIANT, [out] AddIn: AddIn** | HRESULT |
| `Update` | — | HRESULT |

### <a name="iseaddin"></a>`ISEAddIn`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `get Application` | [out] Application: IDispatch* | HRESULT |
| `get AddInEvents` | [out] AddInEvents: AddInEvents** | HRESULT |
| `get Connect` | [out] Connect: bool* | HRESULT |
| `put Connect` | Connect: bool | HRESULT |
| `get Description` | [out] Description: BSTR* | HRESULT |
| `put Description` | Description: BSTR | HRESULT |
| `get GUID` | [out] GUID: BSTR* | HRESULT |
| `get GuiVersion` | [out] GuiVersion: int* | HRESULT |
| `put GuiVersion` | GuiVersion: int | HRESULT |
| `get Object` | [out] Object: IDispatch* | HRESULT |
| `put Object` | Object: IDispatch | HRESULT |
| `get ProgID` | [out] ProgID: BSTR* | HRESULT |
| `get Visible` | [out] Visible: bool* | HRESULT |
| `put Visible` | Visible: bool | HRESULT |
| `SetAddInInfo` | InstanceHandle: int, EnvironmentCatID: BSTR, CategoryName: BSTR, IDColorBitmapMedium: int, IDColorBitmapLarge: int, IDMonochromeBitmapMedium: int, IDMonochromeBitmapLarge: int, NumberOfCommands: int, CommandNames: SAFEARRAY(BSTR)*, [in,out] CommandIDs: SAFEARRAY(int)* | HRESULT |
| `AddCommand` | EnvironmentCatID: BSTR, CommandName: BSTR, CommandID: int, [out] SolidEdgeCommandID: int* | HRESULT |
| `AddCommandBarButton` | EnvironmentCatID: BSTR, CommandBarName: BSTR, CommandID: int, [out] CommandBarButton: CommandBarButton** | HRESULT |

### <a name="iseaddinevents"></a>`ISEAddInEvents`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `OnCommand` | CommandID: int | HRESULT |
| `OnCommandHelp` | hFrameWnd: int, HelpCommandID: int, CommandID: int | HRESULT |
| `OnCommandUpdateUI` | CommandID: int, [in,out] CommandFlags: int*, [out] MenuItemText: BSTR*, [in,out] BitmapID: int* | HRESULT |

### <a name="isecommandbarbutton"></a>`ISECommandBarButton`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `get CommandBarButtonEvents` | [out] CommandBarButtonEvents: CommandBarButtonEvents** | HRESULT |
| `get State` | [out] State: SeButtonState* | HRESULT |
| `put State` | State: SeButtonState | HRESULT |
| `get Style` | [out] Style: SeButtonStyle* | HRESULT |
| `put Style` | Style: SeButtonStyle | HRESULT |

### <a name="isecommandbarbuttonevents"></a>`ISECommandBarButtonEvents`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `Click` | — | HRESULT |
| `Help` | hFrameWnd: int, HelpCommandID: int | HRESULT |
| `UpdateUI` | — | HRESULT |

### <a name="isefeaturelibraryevents"></a>`ISEFeatureLibraryEvents`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `AfterFeatureLibraryDocumentCreated` | Name: BSTR | HRESULT |
| `AfterFeatureLibraryDocumentRenamed` | NewName: BSTR, OldName: BSTR | HRESULT |
| `AfterFeatureLibraryDocumentDeleted` | Name: BSTR | HRESULT |

### <a name="_iinsightauto"></a>`_IInsightAuto`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `DownloadDocumentFromServer` | DocumentToDownLoadFromServer: BSTR, DocumentAccessMode: DocumentAccess, [out] LocalPath: BSTR*, [opt]GetLatestDocuments: VARIANT, [opt]ProcessIndirectDocuments: VARIANT, [opt]RevisionRuleOption: VARIANT, [opt]StopFileOpenIfRevisionRuleNotApplicable: VARIANT | HRESULT |
| `ImportDocumentsToServer` | NumberOfDocumentsFoldersToImport: int, ListOfDocumentsFoldersToImport: VARIANT, ImportLocation: BSTR, TypeOfUpload: UploadType, CheckInOption: CheckInOptions | HRESULT |
| `UploadDocumentsToServer` | NumberOfDocumentsToUpload: int, DocumentsToUpload: VARIANT | HRESULT |
| `ExportDocumentsFromServer` | NumberOfDocumentsToExport: int, ListOfDocumentsToExport: VARIANT, ExportToLocation: BSTR, SetDocToReadOnly: bool, OverWriteOption: OverWriteFilesOption | HRESULT |
| `DeleteDocumentsFromServer` | NumberOfDocumentsToBeDeleted: int, ListOfDocumentsToBeDeleted: VARIANT, [out] NumberOfSuccessfullyDeletedDocuments: int*, [out] SuccessfullyDeletedDocuments: VARIANT* | HRESULT |
| `FindWhereUsedOnServer` | NumberOfProperties: int, ListOfProperties: VARIANT, NumberOfSharePointDirectories: int, ListOfSharePointDirectories: VARIANT, NumberOfDocuments: int, ListOfDocumentsForWhereUsed: VARIANT, [out] NumberOfUserFiles: int*, [out] DocumentsUsedByList: VARIANT*, [opt]TypeOfSearch: VARIANT | HRESULT |
| `CheckOutDocumentsFromServer` | NumberOfDocumentsToCheckOutFromServer: int, ListOfDocumentsToCheckOutFromServer: VARIANT | HRESULT |
| `CheckInDocumentsToServer` | NumberOfDocumentsToCheckInToServer: int, ListOfDocumentsToCheckInFromServer: VARIANT, [opt]FailIfDocumentsOpenInSolidEdge: VARIANT | HRESULT |
| `UndoCheckOutDocumentsFromServer` | NumberOfDocumentsToUndoCheckOutFromServer: int, ListOfDocumentsToUndoCheckOutFromServer: VARIANT | HRESULT |
| `ShowRevisionsForServerDocument` | DocumentNameToShowRevisions: BSTR, [out] NumberOfRevisions: int*, [out] DocumentNamesOfRevisions: VARIANT* | HRESULT |
| `GetRevisedFrom` | RevisedDocumentName: BSTR, [out] RevisedFromDocument: BSTR* | HRESULT |
| `SetInsightUserNamePassword` | WorkspaceUrl: BSTR, UserName: BSTR, Password: BSTR, DomainName: BSTR | HRESULT |
| `GetLastInsightTransactionMessages` | [out] TransactionString: BSTR*, [out] NumberOfDocuments: int*, [out] ListofDocumentNamesWithPath: VARIANT*, [out] ListofMessages: VARIANT*, [out] ListofSeverityCodes: VARIANT* | HRESULT |
| `GetOutOfDateDocuments` | [out] NumberOfOutOfDateDocuments: int*, ListOfOutOfDateDocuments: VARIANT* | HRESULT |
| `ClearCache` | — | HRESULT |
| `DeleteDocumentsFromCache` | NumberOfDocumentsToBeDeletedFromCache: int, ListOfDocumentsToBeDeletedFromCache: VARIANT, [out] NumberOfNotDeletedDocuments: int*, [out] ListOfNotDeletedDocuments: VARIANT* | HRESULT |
| `PutUserNameAndPasswordIntoCache` | WorkspaceUrl: BSTR | HRESULT |
| `EnableDeveloperLog` | bCreateFlag: bool | HRESULT |
| `SynchronizeDocumentsInCache` | NumberOfDocumentsToBeSynchronizedWithServer: int, ListOfDocumentsInCacheToBeSynchronized: VARIANT | HRESULT |
| `SynchronizeAllDocumentsInCache` | — | HRESULT |
| `CheckInAllCheckedOutDocumentsInCache` | — | HRESULT |
| `GetFilePropertiesFromServer` | NumberOfFilesToBeQueriedForProperties: int, FileUrlsList: VARIANT, NumberOfPropertiesTobeQueried: int, PropertyUrisList: VARIANT, [out] numberOfPropertiesValues: int*, [out] PropertyValueList: VARIANT* | HRESULT |
| `MoveDocumentsThroughWorkFlow` | Filename: BSTR, newstatus: DocumentStatus, [opt]NumberOfDraftFiles: VARIANT, [opt]draftFileList: VARIANT, [opt]draftFileStatusList: VARIANT, [opt]NumberOfRevisionFiles: VARIANT, [opt]revisionFileList: VARIANT, [opt]RevisionFileStatusList: VARIANT | HRESULT |
| `MoveAllDocumentsThroughWorkFlow` | Filename: BSTR, newstatus: DocumentStatus | HRESULT |
| `GetSharePointServerType` | Filename: BSTR, [out] SPServerType: SPServerType*, [opt]bProcessChecks: VARIANT* | HRESULT |
| `FileExists` | FileUrl: BSTR, [out] bFileExists: bool* | HRESULT |
| `CreateFolder` | numberOfFoldersToCreate: int, varListOfFoldersToCreate: VARIANT | HRESULT |
| `DeleteFolder` | NumberOfDocumentsToBeDeleted: int, varlistOfFilesToDelete: VARIANT, [out] NumberOfSuccessfullyDeletedDocuments: int*, [out] listOfFoldersSuccessfullyDeleted: VARIANT* | HRESULT |
| `FolderExists` | bstrFolderName: BSTR, [out] bFolderExists: bool* | HRESULT |
| `GetDirs` | ParentUrl: BSTR, [out] numberOfSubFoldersFound: int*, [out] ListOfSubFoldersFound: VARIANT* | HRESULT |
| `GetFiles` | ParentUrl: BSTR, [out] numberOfFilesFound: int*, [out] ListOfFilesFound: VARIANT*, [opt]FileFilter: VARIANT | HRESULT |
| `DoesUserHaveAdminRights` | FileOrFolderUrl: BSTR, UserName: BSTR, [out] bUserHasAdminRights: bool* | HRESULT |
| `IsInsightSupported` | [out] bInsightIsSupported: bool* | HRESULT |
| `IsFileCheckedOut` | FileUrl: BSTR, [out] bFileIsCheckedOut: bool*, [out] UserName: BSTR* | HRESULT |
| `GetCachePath` | numberOfFilesToGetPathFor: int, varListOfFilePaths: VARIANT, [out] numberOfFilesReturned: int*, [out] varListOfFilesContainingCachePaths: VARIANT* | HRESULT |
| `GetUserRole` | FileOrFolderUrl: BSTR, UserName: BSTR, [out] UserRole: BSTR* | HRESULT |
| `GetDocState` | UrlToGetStateFor: BSTR, [out] docState: VARIANT* | HRESULT |
| `CheckSupport` | ServerUrl: BSTR, [out] bSPSIsSupported: bool* | HRESULT |
| `GetUserRights` | FileOrFolderUrl: BSTR, [out] UserRights: InsightSPUserRights* | HRESULT |
| `GetIndirectFilesTree` | containerFileName: BSTR, [out] pIndirectFilesTree: VARIANT* | HRESULT |
| `UsePathAsDefaultFolderMapPath` | WorkspaceUrl: BSTR | HRESULT |
| `RemoveAllFilesFromRecycleBin` | bstrDocLibUrl: BSTR | HRESULT |
| `RestoreAllFilesFromRecycleBin` | bstrDocLibUrl: BSTR | HRESULT |
| `DownloadDocumentFromServerWithAllLinks` | DocumentToDownLoadFromServer: BSTR, DocumentAccessMode: DocumentAccess, [out] LocalPath: BSTR*, [opt]GetLatestDocuments: VARIANT, [opt]ProcessIndirectDocuments: VARIANT, [opt]RevisionRuleOption: VARIANT, [opt]StopFileOpenIfRevisionRuleNotApplicable: VARIANT | HRESULT |
| `SetInsightATPRunning` | bRunningInsightATP: bool | HRESULT |
| `SetInsightATPInfo` | bstrATPLevel1: BSTR, bstrATPClassName: BSTR, bstrATPName: BSTR, ATPId: int | HRESULT |
| `ValidateDocsOnLCA` | bstrInputURL: BSTR, numberOfDocumentstoValidate: int, varlistOfDocsToValidate: VARIANT, bstrInputData: BSTR | HRESULT |
| `IsVersioningEnabledForTheInputDocLib` | docLibName: BSTR, [out] pbIsVersioningOnForTheInputDocLib: bool* | HRESULT |
| `IsFileCheckedOutToSameUser` | filePath: BSTR, UserName: BSTR, Password: BSTR, [out] checkedoutby: BSTR*, [opt]UpdateCache: bool | HRESULT |
| `IsDocumentLibraryContainsRequiredProperty` | docLibName: BSTR, [out] pbIsRequiredPropertyExist: bool* | HRESULT |
| `SetInsightOfflineMode` | bOfflineModeVal: bool | HRESULT |
| `DisplayPropertyManagerDlg` | bstrFilename: BSTR | HRESULT |
| `GetCookieData` | bstrFilename: BSTR, valCookieDataToGet: CookieDataToGet, [out] varRevisionRule: RevisionRuleType* | HRESULT |
| `SetFilePropertiesOnServer` | bstrInputURL: BSTR, NumberOfPropertiesToSet: int, PropertyUrIList: VARIANT, PropertyValueList: VARIANT | HRESULT |
| `ISDocumentParserEnabled` | bstrInputURL: BSTR, [out] bDocParserEnabled: bool* | HRESULT |
| `GetLWFPathForUrl` | bstrUrl: BSTR, [out] bstrLWFPath: BSTR* | HRESULT |
| `DisplaySEPackNGoDlg` | bstrFilename: BSTR | HRESULT |

### <a name="iseapplicationv8afterdocumentopenevent"></a>`ISEApplicationV8AfterDocumentOpenEvent`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `AfterDocumentOpen` | theDocument: IDispatch | HRESULT |

### <a name="isefeatureselectedfrompfevents"></a>`ISEFeatureSelectedFromPFEvents`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `AfterFeatureSelectedFromPF` | theDocument: IDispatch, SelectedFeature: IDispatch, lFeatureType: int | HRESULT |

### <a name="_imattableauto"></a>`_IMatTableAuto`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `GetMaterialList` | [out] plNumMaterials: int*, [out] listOfMaterials: VARIANT* | HRESULT |
| `SetActiveDocument` | pDocument: IDispatch | HRESULT |
| `AddMaterial` | bstrMatName: BSTR, lNumProps: int, varPropList: VARIANT, bstrFaceStyle: BSTR, bstrFillStyle: BSTR, bstrVSPlusStyle: BSTR | HRESULT |
| `GetMatPropValue` | bstrMatName: BSTR, lPropIndex: MatTablePropIndex, [out] varPropValue: VARIANT* | HRESULT |
| `SetMatPropValue` | bstrMatName: BSTR, lPropIndex: MatTablePropIndex, varPropValue: VARIANT | HRESULT |
| `DeleteMaterial` | bstrMatName: BSTR | HRESULT |
| `ApplyMaterial` | pDocument: IDispatch, bstrMatName: BSTR | HRESULT |
| `GetMatLibFileName` | [out] varMatLibName: VARIANT* | HRESULT |
| `WriteMatLibFileFromXML` | bstrXMLFile: BSTR, bstrMatLibName: BSTR | HRESULT |
| `WriteMaterialDataToXML` | bstrXMLFile: BSTR | HRESULT |
| `GetPSMGaugeListFromExcel` | bstrGageTableName: BSTR, [out] plNumGages: int*, [out] listOfGages: VARIANT* | HRESULT |
| `GetPSMGaugeInfoForDoc` | pDocument: IDispatch, [out] bstrGageName: BSTR*, [out] bstrGageFilePath: BSTR*, [out] iMTLUsingExcel: int*, [out] bstrMTLGageTableName: BSTR*, [out] iDocUsingExcel: int*, [out] bstrDocGageTableName: BSTR*, [out] iCountBendRadiusVals: int*, [out] iCountBendAngleVals: int*, [out] iCountNFVals: int* | HRESULT |
| `GetDefaultGageFileName` | [out] strGageFileName: BSTR* | HRESULT |
| `PerformGageDataValidation` | strExcelFile: BSTR, strGageTable: BSTR, strGage: BSTR, [out] bValidGage: bool* | HRESULT |
| `SetMaterialToGageTableAssociation` | pDocument: IDispatch, bstrMaterialName: BSTR, bstrMaterialGageTableName: BSTR, bAddAssociation: bool | HRESULT |
| `SetDocumentToGageTableAssociation` | pDocument: IDispatch, bstrDocGageName: BSTR, bstrDocGageTableName: BSTR, bUseNeutralFactorFromExcel: bool, bAddAssociation: bool | HRESULT |
| `UseNeutralFactorFromExcel` | pDocument: IDispatch, bUseNeutralFactorFromExcel: bool | HRESULT |
| `EditOpenGageExcelFile` | bstrDocGageTableName: BSTR | HRESULT |
| `GetCurrentGageName` | pDocument: IDispatch, [out] bstrGageName: BSTR* | HRESULT |
| `GetCurrentMaterialName` | pDocument: IDispatch, [out] bstrMaterialName: BSTR* | HRESULT |
| `GetMaterialListFromLibrary` | bstrLibraryName: BSTR, [out] plNumMaterials: int*, [out] listOfMaterials: VARIANT* | HRESULT |
| `AddMaterialToLibrary` | bstrMatName: BSTR, bstrLibrary: BSTR, bstrMaterialPath: BSTR, lNumProps: int, varPropList: VARIANT, bstrFaceStyle: BSTR, bstrFillStyle: BSTR | HRESULT |
| `DeleteMaterialFromLibrary` | bstrMatName: BSTR, bstrLibraryName: BSTR | HRESULT |
| `GetMaterialPropValueFromLibrary` | bstrMatName: BSTR, bstrLibraryName: BSTR, lPropIndex: MatTablePropIndex, [out] varPropValue: VARIANT* | HRESULT |
| `SetMaterialPropValueToLibrary` | bstrMatName: BSTR, bstrLibraryName: BSTR, lPropIndex: MatTablePropIndex, varPropValue: VARIANT | HRESULT |
| `GetMaterialPropValueFromDoc` | pDocument: IDispatch, lPropIndex: MatTablePropIndex, [out] varPropValue: VARIANT* | HRESULT |
| `ApplyMaterialToDoc` | pDocument: IDispatch, bstrMatName: BSTR, bstrLibraryName: BSTR | HRESULT |
| `AddMaterialToFavorites` | bstrMaterialName: BSTR, bstrLibraryName: BSTR | HRESULT |
| `GetFavoriteMaterialList` | [out] MaterialNames: VARIANT*, [out] LibraryNames: VARIANT*, [out] plNumMaterials: int* | HRESULT |
| `GetMRUMaterialList` | [out] MaterialNames: VARIANT*, [out] LibraryNames: VARIANT*, [out] plNumMaterials: int* | HRESULT |
| `SetMRUMaterialLimit` | nNoOfMRUMtls: int | HRESULT |
| `GetMRUMaterialLimit` | [out] nNoOfMRUMtls: int* | HRESULT |
| `ClearMRUList` | — | HRESULT |
| `AddCustomProperty` | bstrMatName: BSTR, bstrMatLibName: BSTR, bstrPropName: BSTR, ePropUnitType: UnitTypeConstants, varPropValue: VARIANT | HRESULT |
| `DeleteCustomProperty` | bstrMatName: BSTR, bstrMatLibName: BSTR, nPropIndex: int | HRESULT |
| `GetCountOfCustomProperties` | bstrMatName: BSTR, bstrMatLibName: BSTR, [out] nNumOfCustProps: int* | HRESULT |
| `GetCustomMaterialPropertyFromLibrary` | bstrMatName: BSTR, bstrMatLibName: BSTR, nPropIndex: int, [out] bstrPropName: BSTR*, [out] ePropUnitType: UnitTypeConstants*, [out] varPropValue: VARIANT* | HRESULT |
| `GetCustomMaterialPropertyFromDoc` | pDocument: IDispatch, nPropIndex: int, [out] bstrPropName: BSTR*, [out] ePropUnitType: UnitTypeConstants*, [out] varPropValue: VARIANT* | HRESULT |
| `GetMaterialsFolderPath` | [out] bstrMtlFolderPath: BSTR* | HRESULT |
| `GetMaterialLibraryFileList` | [out] MaterialLibList: VARIANT*, [out] plNumMaterialLibraries: int* | HRESULT |
| `CreateNewMaterialLibrary` | bstrLibInputName: BSTR | HRESULT |
| `CreateNewDirectory` | bstrLibname: BSTR, bstrDirectoryPath: BSTR | HRESULT |
| `RenameMaterial` | bstrMatOldName: BSTR, bstrLibname: BSTR, bstrMatNewName: BSTR | HRESULT |
| `RenameLibrary` | bstrLibOldName: BSTR, bstrLibNeName: BSTR | HRESULT |
| `RenameDirectory` | bstrDirOldName: BSTR, bstrLibname: BSTR, bstrDirNewName: BSTR | HRESULT |
| `ExportMaterialDataToFile` | bstrMaterialLibraryName: BSTR, bstrXMLFile: BSTR | HRESULT |
| `ImportMaterialDataFromFile` | bstrXMLFile: BSTR, bstrMatLibFile: BSTR | HRESULT |
| `SetMaterialsFolderPath` | bstrMtlFolderPath: BSTR | HRESULT |
| `DeleteDirectory` | bstrDirName: BSTR, bstrLibname: BSTR | HRESULT |
| `GetMaterialLibraryList` | [out] MaterialLibList: VARIANT*, [out] plNumMaterialLibraries: int* | HRESULT |
| `ApplyMaterialToFile` | bstrFilename: BSTR, bstrMatName: BSTR, bstrLibraryName: BSTR | HRESULT |
| `GetOODStatusofMaterialAndGage` | pDoc: IDispatch, [out] vbMaterialPropOOD: bool*, [out] vbGagePropOOD: bool* | HRESULT |
| `UpdateOODMaterialAndGageProperties` | pDoc: IDispatch, vbUpdateMaterialProp: bool, vbUpdateGageProp: bool | HRESULT |
| `GetNeutralFactor` | pDoc: IDispatch, dBendAngle: double, dBendRadius: double, [out] dNeutralFactor: double* | HRESULT |
| `ApplyGageFromLibraryToDoc` | pDocument: IDispatch, bstrGage: BSTR, bstrLibraryName: BSTR | HRESULT |
| `ApplyGageFromGageTableToDoc` | pDocument: IDispatch, bstrGage: BSTR, bstrGageTableName: BSTR | HRESULT |
| `ApplyMaterialToInternalComponents` | pDocument: IDispatch, NumOfInternalComponents: int, psaInternalComponents: SAFEARRAY(IDispatch)*, bstrMatName: BSTR, bstrLibraryName: BSTR | HRESULT |
| `ApplyGageFromLibraryToInternalComponents` | pDocument: IDispatch, NumOfInternalComponents: int, psaInternalComponents: SAFEARRAY(IDispatch)*, bstrGage: BSTR, bstrLibraryName: BSTR | HRESULT |
| `ApplyGageFromGageTableToInternalComponents` | pDocument: IDispatch, NumOfInternalComponents: int, psaInternalComponents: SAFEARRAY(IDispatch)*, bstrGage: BSTR, bstrGageTableName: BSTR | HRESULT |

### <a name="isenewfileuievents"></a>`ISENewFileUIEvents`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `OnNewFileUI` | DocumentType: DocumentTypeConstants, [out] Filename: BSTR*, [out] AppendToTitle: BSTR* | HRESULT |

### <a name="iseshortcutmenuevents"></a>`ISEShortCutMenuEvents`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `BuildMenu` | EnvCatID: BSTR, Context: ShortCutMenuContextConstants, pGraphicDispatch: IDispatch, [out] MenuStrings: SAFEARRAY(BSTR)*, [out] CommandIDs: SAFEARRAY(int)* | HRESULT |

### <a name="_isolidedgetceauto"></a>`_ISolidEdgeTCEAuto`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `GetPDMCachePath` | [out] bStrCachePath: BSTR* | HRESULT |
| `CheckInDocumentsToTeamCenterServer` | ppsaFileList: SAFEARRAY(VARIANT)*, bOnlyUpload: bool | HRESULT |
| `CheckOutDocumentsFromTeamCenterServer` | bstrDirctFileItemId: BSTR, bstrDirctFileItemRevId: BSTR, bOnlyDownload: bool, [opt]bstrFilename: BSTR, [opt]enumDownloadLevel: DocumentDownloadLevel | HRESULT |
| `IsTeamCenterFileCheckedOut` | bstrFilename: BSTR | int |
| `GetDocumentUID` | bstrFilename: BSTR, [out] bstrItemId: BSTR*, [out] bstrItemRev: BSTR* | HRESULT |
| `DoesTeamCenterFileExists` | bstrItemId: BSTR, bstrItemRev: BSTR, [out] bFileExists: bool* | HRESULT |
| `GetTeamCenterMode` | [out] bIsTeamCenterMode: bool* | HRESULT |
| `SetTeamCenterMode` | bMode: bool | HRESULT |
| `ValidateLogin` | bstrUserName: BSTR, bstrPassword: BSTR, bstrGroup: BSTR, bstrUserRole: BSTR, bstrDBUrl: BSTR | HRESULT |
| `AssignItemID` | bstrItemType: BSTR, [out] bstrItemId: BSTR*, [out] bstrItemRev: BSTR* | HRESULT |
| `PutItemTypeAsCustomProp` | bstrFilename: BSTR, bstrItemType: BSTR | HRESULT |
| `GetDatasetNameFromCookie` | bstrFilename: BSTR, [out] bstrDatasetName: BSTR* | HRESULT |
| `DeleteFilesFromCache` | psaCacheFiles: SAFEARRAY(VARIANT)* | HRESULT |
| `ImportDocumentsToServer` | lnumberOfDocumentsFoldersToImport: int, psalistOfFilesFoldersToImport: SAFEARRAY(VARIANT)*, bstrUserName: BSTR, bstrPassword: BSTR, bstrGroup: BSTR, bstrUserRole: BSTR, bstrDBUrl: BSTR, bImportAsPrecise: bool, [opt]bPerformOnlyDryRun: bool, [opt]bDisplayAlert: bool, [opt]bIsFromATP: bool, [opt]bIsOverwrite: bool, [opt]brestart: bool, [opt]bLinkTraversal: bool, [opt]bIncludeSubFolders: bool, [opt]bstrFolderUID: BSTR* | HRESULT |
| `OnUndoCheckOutDocuments` | psaCacheFiles: SAFEARRAY(VARIANT)* | HRESULT |
| `OnSynchronizeFile` | psaSynchFiles: SAFEARRAY(VARIANT)*, [opt]enumSyncOption: SyncOption | HRESULT |
| `GetOutOfDateDocuments` | [out] pvarListOfOutOfDateDocuments: VARIANT* | HRESULT |
| `GetUserLogMessages` | [out] pvarUserLogMessages: VARIANT* | HRESULT |
| `SaveAsToTeamCenter` | bstrItemId: BSTR, bstrItemRev: BSTR, bstrDataSetFileName: BSTR, bstrRevisionRule: BSTR, bstrFolderName: BSTR, [out] pvarListOldAndNewItemIDRevsFileNames: VARIANT* | HRESULT |
| `ReviseToTeamCenter` | bstrItemId: BSTR, bstrItemRev: BSTR, bstrDataSetFileName: BSTR, bstrRevisionRule: BSTR, bstrFolderName: BSTR, [out] pvarListOldAndNewItemIDRevsFileNames: VARIANT* | HRESULT |
| `OnGetWhereUsedForAutomation` | bstrItemId: BSTR, bstrItemRev: BSTR, bstrDataSetFileName: BSTR, [out] pvarListOfWhereUsedDocuments: VARIANT* | HRESULT |
| `CreateNewProject` | bstrProjectName: BSTR | HRESULT |
| `DeleteProject` | bstrProjectName: BSTR | HRESULT |
| `DeleteAllProjects` | — | HRESULT |
| `DownladDocumentsFromServerWithOptions` | bstrItemId: BSTR, bstrItemRev: BSTR, bstrDataSetFileName: BSTR, bstrRevisionRule: BSTR, bstrVariantRule: BSTR, vbReadOnly: bool, vbAllLevel: bool, dwDownloadOption: uint, [opt]pvarListOfFiles: SAFEARRAY(VARIANT)* | HRESULT |
| `GetListOfIndirectFilesForGivenFile` | bstrItemId: BSTR, bstrItemRev: BSTR, bstrDataSetFileName: BSTR, bstrRevisionRule: BSTR, bstrVariantRule: BSTR, [out] pvarListOfIndirectFiles: VARIANT* | HRESULT |
| `GetMappedPropertiesForGivenFile` | bstrItemId: BSTR, bstrItemRev: BSTR, bstrDataSetFileName: BSTR, [out] pvarListOfProperties: VARIANT* | HRESULT |
| `GetListOfFilesFromTeamcenterServer` | bstrDirctFileItemId: BSTR, bstrDirctFileItemRevId: BSTR, [out] vFileNames: VARIANT*, [out] nFiles: int* | HRESULT |
| `GetTALLogFileName` | [out] bstrLogFileName: BSTR* | HRESULT |
| `ValidateTcObjectModel` | bstrFilename: BSTR, bstrOldItemID: BSTR, bstrOldRev: BSTR, [out] bIsTcModelCorrect: bool*, [out] bsrtCompResult: BSTR*, [opt]vRevisionRule: VARIANT*, [opt]vValidateBOMOnly: VARIANT*, [opt][out] nNoOfServerComponents: int* | HRESULT |
| `GetBomStructure` | bstrItemId: BSTR, bstrItemRevId: BSTR, bstrRevisionRule: BSTR, bDeepList: bool, [out] NoOfComponents: int*, [out] ListOfItemRevIds: VARIANT*, [out] ListOfFileSpecs: VARIANT* | HRESULT |
| `GetItemRevBasedOnSEType` | nSEType: TCESETypes, bstrUserName: BSTR, [out] ListOfItemRevIds: VARIANT* | HRESULT |
| `GetItemTypesInfo` | [out] pbstrXML: BSTR*, [out] pbstrDefaultItemType: BSTR* | HRESULT |
| `GetSmartCodes` | [in,out] pvarSmartCodesInfo: VARIANT* | HRESULT |
| `UnGetSmartCodes` | ppsaUnGetInfo: SAFEARRAY(VARIANT)* | HRESULT |
| `CheckInDocumentsToTeamCenterServerEx` | pvarFilesToBeCheckedInInfo: VARIANT*, pvarArguments: VARIANT* | HRESULT |
| `IsItemTypeSmartCodesConfigured` | bstrItemType: BSTR, pvbIsSmartCodesConfigured: bool* | HRESULT |
| `GetSEECOrTCPreferenceValues` | [in,out] pvarPreferenceInfo: VARIANT* | HRESULT |
| `UpdateStatusInformation` | psaCacheFiles: SAFEARRAY(VARIANT)* | HRESULT |
| `GetProjectsForLoggedInUSer` | [out] pvarListOfUserProjects: VARIANT*, [out] pvarListOfUserProjectUIDs: VARIANT* | HRESULT |
| `GetProjectsForGivenItemIDs` | [in,out] pvarListOfItemIDsAndProjects: VARIANT* | HRESULT |
| `AddOrRemoveItemsToOrFromProjects` | pvarItemInfoToAddOrRemoveToProjects: VARIANT* | HRESULT |
| `CheckBomStructure` | bstrItemId: BSTR, bstrItemRevId: BSTR, bstrRevisionRule: BSTR, [out] bIsDuplicateBOMLineExists: bool*, [out] ListOfDupliacteObjIDs: VARIANT* | HRESULT |
| `AutoAssign` | vbAutoAssign: bool | HRESULT |
| `GetMFKAttributesForGivenItemType` | bstrItemType: BSTR, [out] pvMFKAttributes: VARIANT* | HRESULT |
| `SetPDMProperties` | bstrOldFileName: BSTR, pvarListOfPropsForFileSaveAs: VARIANT*, [out] bstrNewFileName: BSTR* | HRESULT |
| `GetCurrentUserName` | [out] bStrCurrentUser: BSTR* | HRESULT |
| `GetDocumentUIDEx` | bstrFilename: BSTR, [out] bstrItemId: BSTR*, [out] bstrItemRev: BSTR*, [out] bstrItemUID: BSTR*, [out] bstrRevUID: BSTR* | HRESULT |
| `DoesTeamCenterFileExistsUsingKeyProperties` | bstrItemType: BSTR, pvarMFKAttrInfo: VARIANT*, bstrItemRev: BSTR, [out] bFileExists: bool* | HRESULT |
| `CheckOutDocumentsFromTeamCenterServerUsingKeyProperties` | pvarMFKAttrInfo: VARIANT*, bstrItemType: BSTR, bstrDirctFileItemRevId: BSTR, bOnlyDownload: bool, [opt]bstrFilename: BSTR, [opt]enumDownloadLevel: DocumentDownloadLevel | HRESULT |
| `GetTeamcenterVersion` | [out] bstrMajorVersion: BSTR*, [out] bstrCompleteVersion: BSTR* | HRESULT |
| `GetItemIDAndRevisionPatterns` | bstrItemType: BSTR, [out] pvarItemIDPattern: VARIANT*, [out] pvarRevisionPattern: VARIANT* | HRESULT |
| `AssignItemIDAndRevUsingPatterns` | bstrItemType: BSTR, bstrItemIDPattern: BSTR, bstrRevisionPattern: BSTR, [out] pItemIDPattern: BSTR*, [out] pRevisionPattern: BSTR* | HRESULT |
| `GetItemTypesInfoEx` | bstrFilename: BSTR, [out] pvarItemTypeList: VARIANT* | HRESULT |
| `MapMFKAttributtesToFileProperties` | bstrFilename: BSTR, psaMFKAttributes: SAFEARRAY(VARIANT)*, [out] pvarPropertyInfo: VARIANT* | HRESULT |
| `GetListOfFilesFromTeamcenterServerUsingKeyProperties` | pvarMFKAttrInfo: VARIANT*, bstrDirctFileItemRevId: BSTR, bstrItemType: BSTR, [out] vFileNames: VARIANT*, [out] nFiles: int* | HRESULT |
| `GetBomStructureUsingKeyProperties` | pvarMFKAttrInfo: VARIANT*, bstrItemRevId: BSTR, bstrItemType: BSTR, bstrRevisionRule: BSTR, bDeepList: bool, [out] NoOfComponents: int*, [out] ListOfItemRevIds: VARIANT*, [out] ListOfFileSpecs: VARIANT* | HRESULT |
| `CheckBomStructureUsingKeyProperties` | pvarMFKAttrInfo: VARIANT*, bstrItemRevId: BSTR, bstrItemType: BSTR, bstrRevisionRule: BSTR, [out] bIsDuplicateBOMLineExists: bool*, [out] ListOfDupliacteObjIDs: VARIANT* | HRESULT |
| `SaveAsToTeamCenterUsingKeyProperties` | pvarMFKAttrInfo: VARIANT*, bstrItemRev: BSTR, bstrItemType: BSTR, bstrDataSetFileName: BSTR, bstrRevisionRule: BSTR, bstrFolderName: BSTR, [out] pvarListOldAndNewItemIDRevsFileNames: VARIANT* | HRESULT |
| `ReviseToTeamCenterUsingKeyProperties` | pvarMFKAttrInfo: VARIANT*, bstrItemRev: BSTR, bstrItemType: BSTR, bstrDataSetFileName: BSTR, bstrRevisionRule: BSTR, bstrFolderName: BSTR, [out] pvarListOldAndNewItemIDRevsFileNames: VARIANT* | HRESULT |
| `GetListOfIndirectFilesForGivenFileUsingKeyProperties` | pvarMFKAttrInfo: VARIANT*, bstrItemRev: BSTR, bstrItemType: BSTR, bstrDataSetFileName: BSTR, bstrRevisionRule: BSTR, [out] pvarListOfIndirectFiles: VARIANT* | HRESULT |
| `GetMappedPropertiesForGivenFileUsingKeyProperties` | pvarMFKAttrInfo: VARIANT*, bstrItemRev: BSTR, bstrItemType: BSTR, bstrDataSetFileName: BSTR, [out] pvarListOfProperties: VARIANT* | HRESULT |
| `ValidateTcObjectModelUsingKeyProperties` | bstrFilename: BSTR, pvarMFKAttrInfo: VARIANT*, bstrItemType: BSTR, bstrOldRev: BSTR, [out] bIsTcModelCorrect: bool*, [out] bsrtCompResult: BSTR*, [opt]vRevisionRule: VARIANT*, [opt]vValidateBOMOnly: VARIANT*, [opt][out] nNoOfServerComponents: int* | HRESULT |
| `OnGetWhereUsedForAutomationUsingKeyProperties` | pvarMFKAttrInfo: VARIANT*, bstrItemRev: BSTR, bstrItemType: BSTR, bstrDataSetFileName: BSTR, [out] pvarListOfWhereUsedDocuments: VARIANT* | HRESULT |
| `DownloadDocumentsFromServerWithOptionsUsingKeyProperties` | pvarMFKAttrInfo: VARIANT*, bstrItemRev: BSTR, bstrDataSetFileName: BSTR, bstrRevisionRule: BSTR, bstrItemType: BSTR, vbReadOnly: bool, vbAllLevel: bool, dwDownloadOption: uint, [opt]pvarListOfFiles: SAFEARRAY(VARIANT)* | HRESULT |
| `ValidateKeyProperties` | pvarMFKAttrInfo: VARIANT*, bstrRevID: BSTR, bstrItemType: BSTR, [out] pvarListOfPropsVsStatus: VARIANT* | HRESULT |
| `GetStorageNameForProperties` | bstrFilename: BSTR, [out] pvarListOfPropInfo: VARIANT* | HRESULT |
| `GetErrorMessages` | [out] pvarListOfErrorMsgs: VARIANT*, [out] pvarListOfWarningMsgs: VARIANT*, [out] pvarListOfInformationalMsgs: VARIANT* | HRESULT |
| `GetAllRevisions` | pvarMFKAttributes: VARIANT*, bstrItemType: BSTR, [out] pvarRevisionList: VARIANT* | HRESULT |
| `CreateZipOfCache` | bstrSourceCachePath: BSTR, bstrDestinationZipPath: BSTR | HRESULT |
| `GetListOfWorkflows` | pvarMFKAttributes: VARIANT*, bstrItemType: BSTR, bstrItemRev: BSTR, bGetFiltered: bool, [out] pVarWorkflows: VARIANT* | HRESULT |
| `ExecuteWorkflow` | pvarMFKAttributes: VARIANT*, bstrItemType: BSTR, bstrItemRev: BSTR, bstrprocessName: BSTR, bstrProcessDesr: BSTR, bstrTemplate: BSTR, [out] pVarOut: VARIANT* | HRESULT |
| `GetActivePDMMode` | [out] activePDM: uint* | HRESULT |
| `GetSolidEdgePreferencePath` | [out] lpSEPreferencePath: BSTR* | HRESULT |
| `SetSEECOptions` | enumSEECDialog: SEECOptions, dwValue: uint* | HRESULT |
| `GetBOMProperties` | pvarMFKAttributes: VARIANT*, bstrItemRevId: BSTR, bstrItemType: BSTR, bstrRevisionRule: BSTR, [out] NoOfComponents: int*, [out] FileOccProp: VARIANT* | HRESULT |
| `PublishFamilyOfAssemblyMembers` | pvarMFKAttrInfo: VARIANT*, bstrItemRev: BSTR, bstrDataSetFileName: BSTR, bstrRevisionRule: BSTR, bstrItemType: BSTR, psaMemberInfo: SAFEARRAY(VARIANT)*, [out] pvarListOfWhereUsedDocuments: VARIANT* | HRESULT |
| `GetFamilyOfAssemblyMembers` | pvarMFKAttrInfo: VARIANT*, bstrItemRev: BSTR, bstrItemType: BSTR, [out] psaPublishedMemberInfo: VARIANT*, [out] psaUnpublishedMemberInfo: VARIANT* | HRESULT |
| `GetWhereUsedInfoForPublishedFamilyOfAssembly` | pvarMFKAttrInfo: VARIANT*, bstrItemRev: BSTR, bstrDataSetFileName: BSTR, bstrRevisionRule: BSTR, bstrItemType: BSTR, [out] pvarListOfWhereUsedDocuments: VARIANT* | HRESULT |
| `GetWhereUsedInfo` | pvarMFKAttrInfo: VARIANT*, bstrItemRev: BSTR, bstrItemType: BSTR, bstrDataSetFileName: BSTR, [out] pvarListFilesWithMFKAttributes: VARIANT* | HRESULT |
| `PublishMembersOfActiveFamilyOfAssemblyDocument` | psaMemberInfo: SAFEARRAY(VARIANT)*, [out] pvarListOfWhereUsedDocuments: VARIANT* | HRESULT |
| `GetWhereUsedInfoForPublishedActiveFamilyOfAssemblyDocument` | [out] pvarListOfWhereUsedDocuments: VARIANT* | HRESULT |
| `GetFamilyOfAssemblyMembersOfActiveDocument` | [out] psaPublishedMemberInfo: VARIANT*, [out] psaUnpublishedMemberInfo: VARIANT* | HRESULT |
| `GetTeamcenterDefaultItemTypePreference` | bstrFilename: BSTR, [out] bstrDefaultItemType: BSTR* | HRESULT |
| `CreateBOMAndRelations` | pvarContainerInfo: VARIANT*, psaComponentsInfo: SAFEARRAY(VARIANT)*, bUploadFile: bool, bCreatePreciseBOM: bool, bstrRevRule: BSTR, [out] bHasOverrideBody: bool* | HRESULT |
| `UploadModelViewsOfActiveAssemblyDocument` | — | HRESULT |
| `ExtractTranslatedFiles` | pvarMFKAttrInfo: VARIANT*, bstrItemRev: BSTR, bstrRevisionRule: BSTR, bstrItemType: BSTR, bstrEtractLocation: BSTR, bstrDataSetFileName: BSTR, dwExpandSelectionOptions: uint, pvarSEFiletypeFilters: VARIANT*, pvarRelationFilters: VARIANT*, pvarReferanceFilters: VARIANT*, pvarExportFileextensions: VARIANT*, [out] pvarListOfFiles: VARIANT* | HRESULT |
| `ExtractTranslatedFilesOfActiveDocument` | bstrEtractLocation: BSTR, dwExpandSelectionOptions: uint, pvarSEFiletypeFilters: VARIANT*, pvarRelationFilters: VARIANT*, pvarReferanceFilters: VARIANT*, pvarExportFileextensions: VARIANT*, [out] pvarListOfFiles: VARIANT* | HRESULT |
| `CloneDraftDocument` | vbOpenCloneDocument: bool | HRESULT |
| `GetMFKAttributesAndItemTypeForGivenFile` | bstrFilename: BSTR, [out] bstrItemType: BSTR*, [out] pvarMFKAttributeValues: VARIANT* | HRESULT |
| `SetPDMPropsAndUploadTranslatedFile` | bstrOldFileName: BSTR, pvarListOfPropsForFileSaveAs: VARIANT*, [in,out] bstrNewFileName: BSTR* | HRESULT |
| `GetListOfFilesUnderItemRevision` | pvarMFKAttrInfo: VARIANT*, bstrItemRev: BSTR, bstrItemType: BSTR, [out] pvarSEFileListUnderItemRev: VARIANT*, [out] pvarAllFileListUnderItemRev: VARIANT* | HRESULT |
| `GetTCSaveAsTranslationMBDPrefValues` | [out] bRelation: BSTR*, [out] bDatasetType: BSTR*, [out] bNamedReference: BSTR*, [out] bStringToAppend: BSTR*, [out] bIncludeRevName: bool* | HRESULT |
| `GetRequiredPDMProperties` | bstrFilename: BSTR, pvarProperties: VARIANT*, [out] pvarPropertiesWithValues: VARIANT* | HRESULT |
| `GetActiveDocumentFilename` | [out] bstrFilename: BSTR*, [out] bstrDisplayname: BSTR*, [out] bstrReservedname: BSTR* | HRESULT |
| `GetTeamcenterInformation` | bstrFilename: BSTR, [out] psaTCInfo: VARIANT* | HRESULT |
| `OnUndoCheckOutDocumentsEx` | psaCacheFiles: SAFEARRAY(VARIANT)*, bIgnoreFileModifiedStatus: bool | HRESULT |
| `GetTCSaveAsTranslationPrefValues` | bstrExportType: BSTR, [out] bRelation: BSTR*, [out] bDatasetType: BSTR*, [out] bNamedReference: BSTR*, [out] bStringToAppend: BSTR*, [out] bIncludeRevName: bool* | HRESULT |
| `DownloadDocumentsFromServerWithOptionsUsingKeyPropertiesEx` | pvarMFKAttrInfo: VARIANT*, bstrItemRev: BSTR, bstrDataSetFileName: BSTR, bstrRevisionRule: BSTR, bstrVariantRule: BSTR, bstrItemType: BSTR, vbReadOnly: bool, vbAllLevel: bool, dwDownloadOption: uint, [opt]pvarListOfFiles: SAFEARRAY(VARIANT)* | HRESULT |
| `ValidateLogin_TCCS` | bstrUserName: BSTR, bstrPassword: BSTR, bstrGroup: BSTR, bstrUserRole: BSTR, bstrDBUrl: BSTR, bstrDBDesc: BSTR | HRESULT |
| `CheckInStdPartsToTeamcenter` | ppsaFileList: SAFEARRAY(VARIANT)*, bOnlyUpload: bool | HRESULT |

### <a name="_isolidedgeinsightxtauto"></a>`_ISolidEdgeInsightXTAuto`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `GetPDMCachePath` | [out] bStrCachePath: BSTR* | HRESULT |
| `CheckInDocumentsToInsightXTServer` | ppsaFileList: SAFEARRAY(VARIANT)*, bOnlyUpload: bool, bstrUrl: BSTR | HRESULT |
| `CheckOutDocumentsFromInsightXTServer` | bstrDirctFileItemId: BSTR, bstrDirctFileItemRevId: BSTR, bOnlyDownload: bool, [opt]bstrFilename: BSTR, [opt]enumDownloadLevel: DocumentDownloadLevel | HRESULT |
| `IsInsightXTFileCheckedOut` | bstrFilename: BSTR | int |
| `GetDocumentUID` | bstrFilename: BSTR, [out] bstrItemId: BSTR*, [out] bstrItemRev: BSTR* | HRESULT |
| `DoesInsightXTFileExists` | bstrItemId: BSTR, bstrItemRev: BSTR, [out] bFileExists: bool* | HRESULT |
| `GetInsightXTMode` | [out] bIsInsightXTMode: bool* | HRESULT |
| `SetInsightXTMode` | bMode: bool | HRESULT |
| `ValidateLogin` | bstrUserName: BSTR, bstrPassword: BSTR, bstrGroup: BSTR, bstrUserRole: BSTR, bstrDBUrl: BSTR | HRESULT |
| `AssignItemID` | bstrItemType: BSTR, [out] bstrItemId: BSTR*, [out] bstrItemRev: BSTR* | HRESULT |
| `SESP_GetItemAndRevisionNo` | bstrItemContentType: BSTR, bstrItemRevContentType: BSTR, [out] bstrPartno: BSTR*, [out] bstrPartRevno: BSTR* | HRESULT |
| `PutItemTypeAsCustomProp` | bstrFilename: BSTR, bstrItemType: BSTR | HRESULT |
| `GetDatasetNameFromCookie` | bstrFilename: BSTR, [out] bstrDatasetName: BSTR* | HRESULT |
| `DeleteFilesFromCache` | psaCacheFiles: SAFEARRAY(VARIANT)* | HRESULT |
| `ImportDocumentsToServer` | lnumberOfDocumentsFoldersToImport: int, psalistOfFilesFoldersToImport: SAFEARRAY(VARIANT)*, bstrUserName: BSTR, bstrPassword: BSTR, bstrGroup: BSTR, bstrUserRole: BSTR, bstrDBUrl: BSTR, bImportAsPrecise: bool, [opt]bPerformOnlyDryRun: bool, [opt]bDisplayAlert: bool, [opt]bIsFromATP: bool, [opt]bIsOverwrite: bool, [opt]brestart: bool, [opt]bLinkTraversal: bool, [opt]bIncludeSubFolders: bool, [opt]bstrFolderUID: BSTR* | HRESULT |
| `OnUndoCheckOutDocuments` | psaCacheFiles: SAFEARRAY(VARIANT)* | HRESULT |
| `OnSynchronizeFile` | psaSynchFiles: SAFEARRAY(VARIANT)*, [opt]enumSyncOption: SyncOption | HRESULT |
| `GetOutOfDateDocuments` | [out] pvarListOfOutOfDateDocuments: VARIANT* | HRESULT |
| `GetUserLogMessages` | [out] pvarUserLogMessages: VARIANT* | HRESULT |
| `SaveAsToInsightXT` | bstrItemId: BSTR, bstrItemRev: BSTR, bstrDataSetFileName: BSTR, bstrRevisionRule: BSTR, bstrFolderName: BSTR, [out] pvarListOldAndNewItemIDRevsFileNames: VARIANT* | HRESULT |
| `ReviseToInsightXT` | bstrItemId: BSTR, bstrItemRev: BSTR, bstrDataSetFileName: BSTR, bstrRevisionRule: BSTR, bstrFolderName: BSTR, [out] pvarListOldAndNewItemIDRevsFileNames: VARIANT* | HRESULT |
| `OnGetWhereUsedForAutomation` | bstrItemId: BSTR, bstrItemRev: BSTR, bstrDataSetFileName: BSTR, [out] pvarListOfWhereUsedDocuments: VARIANT* | HRESULT |
| `CreateNewProject` | bstrProjectName: BSTR | HRESULT |
| `DeleteProject` | bstrProjectName: BSTR | HRESULT |
| `DeleteAllProjects` | — | HRESULT |
| `DownladDocumentsFromServerWithOptions` | bstrItemId: BSTR, bstrItemRev: BSTR, bstrDataSetFileName: BSTR, bstrRevisionRule: BSTR, bstrVariantRule: BSTR, vbReadOnly: bool, vbAllLevel: bool, dwDownloadOption: uint, [opt]pvarListOfFiles: SAFEARRAY(VARIANT)* | HRESULT |
| `GetListOfIndirectFilesForGivenFile` | bstrItemId: BSTR, bstrItemRev: BSTR, bstrDataSetFileName: BSTR, bstrRevisionRule: BSTR, bstrVariantRule: BSTR, [out] pvarListOfIndirectFiles: VARIANT* | HRESULT |
| `GetMappedPropertiesForGivenFile` | bstrItemId: BSTR, bstrItemRev: BSTR, bstrDataSetFileName: BSTR, [out] pvarListOfProperties: VARIANT* | HRESULT |
| `GetListOfFilesFromInsightXTServer` | bstrDirctFileItemId: BSTR, bstrDirctFileItemRevId: BSTR, [out] vFileNames: VARIANT*, [out] nFiles: int* | HRESULT |
| `GetTALLogFileName` | [out] bstrLogFileName: BSTR* | HRESULT |
| `ValidateTcObjectModel` | bstrFilename: BSTR, bstrOldItemID: BSTR, bstrOldRev: BSTR, [out] bIsTcModelCorrect: bool*, [out] bsrtCompResult: BSTR*, [opt]vRevisionRule: VARIANT*, [opt]vValidateBOMOnly: VARIANT*, [opt][out] nNoOfServerComponents: int* | HRESULT |
| `GetBomStructure` | bstrItemId: BSTR, bstrItemRevId: BSTR, bstrRevisionRule: BSTR, bDeepList: bool, [out] NoOfComponents: int*, [out] ListOfItemRevIds: VARIANT*, [out] ListOfFileSpecs: VARIANT* | HRESULT |
| `GetItemRevBasedOnSEType` | nSEType: TCESETypes, bstrUserName: BSTR, [out] ListOfItemRevIds: VARIANT* | HRESULT |
| `GetItemTypesInfo` | [out] pbstrXML: BSTR*, [out] pbstrDefaultItemType: BSTR* | HRESULT |
| `GetSmartCodes` | [in,out] pvarSmartCodesInfo: VARIANT* | HRESULT |
| `UnGetSmartCodes` | ppsaUnGetInfo: SAFEARRAY(VARIANT)* | HRESULT |
| `CheckInDocumentsToInsightXTServerEx` | pvarFilesToBeCheckedInInfo: VARIANT*, pvarArguments: VARIANT* | HRESULT |
| `IsItemTypeSmartCodesConfigured` | bstrItemType: BSTR, pvbIsSmartCodesConfigured: bool* | HRESULT |
| `GetInsightXTOrTCPreferenceValues` | [in,out] pvarPreferenceInfo: VARIANT* | HRESULT |
| `UpdateStatusInformation` | psaCacheFiles: SAFEARRAY(VARIANT)* | HRESULT |
| `GetProjectsForLoggedInUSer` | [out] pvarListOfUserProjects: VARIANT*, [out] pvarListOfUserProjectUIDs: VARIANT* | HRESULT |
| `GetProjectsForGivenItemIDs` | [in,out] pvarListOfItemIDsAndProjects: VARIANT* | HRESULT |
| `AddOrRemoveItemsToOrFromProjects` | pvarItemInfoToAddOrRemoveToProjects: VARIANT* | HRESULT |
| `CheckBomStructure` | bstrItemId: BSTR, bstrItemRevId: BSTR, bstrRevisionRule: BSTR, [out] bIsDuplicateBOMLineExists: bool*, [out] ListOfDupliacteObjIDs: VARIANT* | HRESULT |
| `GetItemContentTypesSupportingRevisioning` | [out] pvarListOfContentTypes: VARIANT* | HRESULT |
| `ProcessUpdateDrawing` | [out] bTerminateProcess: bool* | HRESULT |
| `SESPUpdateWFCallouts` | plistItemAndRevId: VARIANT*, pListOldAndNewPropName: VARIANT*, [out] ListOfFailedDrafts: VARIANT*, [out] bTerminateProcess: bool* | HRESULT |
| `SESPGetActiveUrl` | [out] activeUrl: VARIANT* | HRESULT |
| `IsInsightXTLicenseAvailable` | [out] bIsInsightXTLicenseAvailable: bool* | HRESULT |
| `PutContentTypeIntoStorage` | bstrFilename: BSTR, bstrItemType: BSTR, bItemRevType: BSTR, bContentType: BSTR | HRESULT |

### <a name="iseecevents"></a>`ISEECEvents`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `SEEC_IsPreCPDEventSupported` | [out] pvbPreCPDEventSupported: bool* | HRESULT |
| `SEEC_BeforeCPDDisplay` | pCPDInitializer: IDispatch, eCPDMode: eCPDMode | HRESULT |
| `PDM_OnFileOpenUI` | [out] bstrFilename: BSTR* | HRESULT |

### <a name="isespevents"></a>`ISESPEvents`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `SESP_IsPreCPDEventSupported` | [out] pvbPreCPDEventSupported: bool* | HRESULT |
| `SESP_BeforeCPDDisplay` | pCPDInitializer: IDispatch, eCPDMode: eCPDMode | HRESULT |
| `SESPPDM_OnFileOpenUI` | [out] bstrFilename: BSTR* | HRESULT |

### <a name="ibidmevents"></a>`IBiDMEvents`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `BiDM_IsPreCPDEventSupported` | [out] pvbPreCPDEventSupported: bool* | HRESULT |
| `BiDM_BeforeCPDDisplay` | pCPDInitializer: IDispatch, eCPDMode: eCPDMode | HRESULT |

### <a name="_icustomizationauto"></a>`_ICustomizationAuto`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `get Application` | [out] Application: Application** | HRESULT |
| `get Parent` | [out] Parent: Application** | HRESULT |
| `get RibbonBarThemes` | [out] RibbonBarThemes: RibbonBarThemes** | HRESULT |
| `get RadialMenu` | [out] RadialMenu: RadialMenu** | HRESULT |
| `get SwitchWindowCust` | [out] SwitchWindowCust: SwitchWindowCust** | HRESULT |
| `BeginCustomization` | — | HRESULT |
| `EndCustomization` | — | HRESULT |

### <a name="_iribbonbarthemesauto"></a>`_IRibbonBarThemesAuto`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `get Application` | [out] Application: Application** | HRESULT |
| `get Parent` | [out] Parent: Customization** | HRESULT |
| `get Count` | [out] Count: int* | HRESULT |
| `Item` | Index: VARIANT, [out] Item: RibbonBarTheme** | HRESULT |
| `get _NewEnum` | [out] Unknown: IUnknown* | HRESULT |
| `Create` | BasedOffTheme: VARIANT, [out] Theme: RibbonBarTheme** | HRESULT |
| `Commit` | — | HRESULT |
| `Remove` | Theme: VARIANT | HRESULT |
| `ActivateTheme` | Theme: VARIANT | HRESULT |

### <a name="_iribbonbarthemeauto"></a>`_IRibbonBarThemeAuto`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `get Application` | [out] Application: Application** | HRESULT |
| `get Parent` | [out] Parent: RibbonBarThemes** | HRESULT |
| `get RibbonBars` | [out] RibbonBars: RibbonBars** | HRESULT |
| `get Name` | [out] Name: BSTR* | HRESULT |
| `put Name` | Name: BSTR | HRESULT |
| `get GlobalControlSize` | [out] SizeType: RibbonBarControlSize* | HRESULT |
| `put GlobalControlSize` | SizeType: RibbonBarControlSize | HRESULT |
| `get GlobalControlText` | [out] TextType: RibbonBarControlText* | HRESULT |
| `put GlobalControlText` | TextType: RibbonBarControlText | HRESULT |
| `get Active` | [out] retVal: bool* | HRESULT |

### <a name="_iribbonbarsauto"></a>`_IRibbonBarsAuto`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `get Application` | [out] Application: Application** | HRESULT |
| `get Parent` | [out] Parent: RibbonBarTheme** | HRESULT |
| `get Count` | [out] Count: int* | HRESULT |
| `Item` | Index: VARIANT, [out] Item: RibbonBar** | HRESULT |
| `get _NewEnum` | [out] Unknown: IUnknown* | HRESULT |

### <a name="_iribbonbarauto"></a>`_IRibbonBarAuto`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `get Application` | [out] Application: Application** | HRESULT |
| `get Parent` | [out] Parent: RibbonBars** | HRESULT |
| `get RibbonBarTabs` | [out] Tabs: RibbonBarTabs** | HRESULT |
| `get Environment` | [out] Name: BSTR* | HRESULT |

### <a name="_iribbonbartabsauto"></a>`_IRibbonBarTabsAuto`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `get Application` | [out] Application: Application** | HRESULT |
| `get Parent` | [out] Parent: RibbonBar** | HRESULT |
| `get Count` | [out] Count: int* | HRESULT |
| `Item` | Index: VARIANT, [out] Item: RibbonBarTab** | HRESULT |
| `get _NewEnum` | [out] Unknown: IUnknown* | HRESULT |
| `Insert` | Item: VARIANT, AtItem: VARIANT, mode: RibbonBarInsertMode, [out] InsertedItem: RibbonBarTab** | HRESULT |
| `Remove` | Item: VARIANT | HRESULT |

### <a name="_iribbonbartabauto"></a>`_IRibbonBarTabAuto`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `get Application` | [out] Application: Application** | HRESULT |
| `get Parent` | [out] Parent: RibbonBarTabs** | HRESULT |
| `get RibbonBarGroups` | [out] Groups: RibbonBarGroups** | HRESULT |
| `get Name` | [out] Name: BSTR* | HRESULT |
| `put Name` | Name: BSTR | HRESULT |
| `get Id` | [out] Id: int* | HRESULT |
| `get Visible` | [out] Visible: bool* | HRESULT |
| `put Visible` | Visible: bool | HRESULT |
| `Activate` | — | HRESULT |

### <a name="_iribbonbargroupsauto"></a>`_IRibbonBarGroupsAuto`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `get Application` | [out] Application: Application** | HRESULT |
| `get Parent` | [out] Parent: RibbonBarTab** | HRESULT |
| `get Count` | [out] Count: int* | HRESULT |
| `Item` | Index: VARIANT, [out] Item: RibbonBarGroup** | HRESULT |
| `get _NewEnum` | [out] Unknown: IUnknown* | HRESULT |
| `Insert` | Item: VARIANT, AtItem: VARIANT, mode: RibbonBarInsertMode, [out] InsertedItem: RibbonBarGroup** | HRESULT |
| `Remove` | Item: VARIANT | HRESULT |

### <a name="_iribbonbargroupauto"></a>`_IRibbonBarGroupAuto`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `get Application` | [out] Application: Application** | HRESULT |
| `get Parent` | [out] Parent: RibbonBarGroups** | HRESULT |
| `get RibbonBarControls` | [out] Controls: RibbonBarControls** | HRESULT |
| `get Name` | [out] Name: BSTR* | HRESULT |
| `put Name` | Name: BSTR | HRESULT |
| `get Id` | [out] Id: int* | HRESULT |
| `get Visible` | [out] Visible: bool* | HRESULT |
| `put Visible` | Visible: bool | HRESULT |

### <a name="_iribbonbarcontrolsauto"></a>`_IRibbonBarControlsAuto`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `get Application` | [out] Application: Application** | HRESULT |
| `get ParentRibbonBarGroup` | [out] Parent: RibbonBarGroup** | HRESULT |
| `get ParentRibbonBarControl` | [out] Parent: RibbonBarControl** | HRESULT |
| `get Count` | [out] Count: int* | HRESULT |
| `Item` | Index: VARIANT, [out] Item: RibbonBarControl** | HRESULT |
| `get _NewEnum` | [out] Unknown: IUnknown* | HRESULT |
| `Insert` | Item: VARIANT, AtItem: VARIANT, mode: RibbonBarInsertMode, [out] InsertedItem: RibbonBarControl** | HRESULT |
| `Remove` | Item: VARIANT | HRESULT |

### <a name="_iribbonbarcontrolauto"></a>`_IRibbonBarControlAuto`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `get Application` | [out] Application: Application** | HRESULT |
| `get Parent` | [out] Parent: RibbonBarControls** | HRESULT |
| `get RibbonBarControls` | [out] Controls: RibbonBarControls** | HRESULT |
| `get Name` | [out] Name: BSTR* | HRESULT |
| `put Name` | Name: BSTR | HRESULT |
| `get Visible` | [out] Visible: bool* | HRESULT |
| `put Visible` | Visible: bool | HRESULT |
| `get Size` | [out] SizeType: RibbonBarControlSize* | HRESULT |
| `put Size` | SizeType: RibbonBarControlSize | HRESULT |
| `get Text` | [out] TextType: RibbonBarControlText* | HRESULT |
| `put Text` | TextType: RibbonBarControlText | HRESULT |
| `get Id` | [out] Id: int* | HRESULT |
| `get IconId` | [out] IconId: int* | HRESULT |

### <a name="_iradialmenuauto"></a>`_IRadialMenuAuto`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `get Application` | [out] Application: Application** | HRESULT |
| `get Parent` | [out] Parent: Customization** | HRESULT |
| `get Count` | [out] Count: int* | HRESULT |
| `Item` | Index: VARIANT, [out] Item: RibbonBarTheme** | HRESULT |
| `get _NewEnum` | [out] Unknown: IUnknown* | HRESULT |
| `Create` | BasedOffTheme: VARIANT, [out] Theme: RibbonBarTheme** | HRESULT |
| `Commit` | — | HRESULT |
| `Remove` | Theme: VARIANT | HRESULT |
| `LoadPallets` | strConfigFilename: BSTR* | HRESULT |
| `SavePallets` | strConfigFilename: BSTR* | HRESULT |
| `DumpPallets` | strLogfileName: BSTR* | HRESULT |
| `DumpPallet` | strEnvironmentName: BSTR*, strLogfileName: BSTR* | HRESULT |
| `SetCommand` | strEnvironmentName: BSTR*, ring: int, wedge: int, cmdID: int, imageID: int | HRESULT |
| `RemoveCommand` | strEnvironmentName: BSTR*, ring: int, wedge: int | HRESULT |
| `SwapCommands` | strEnvironmentName: BSTR*, ring1: int, wedge1: int, ring2: int, wedge2: int | HRESULT |

### <a name="_iswitchwindowcustauto"></a>`_ISwitchWindowCustAuto`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `get Application` | [out] Application: Application** | HRESULT |
| `get Parent` | [out] Parent: Customization** | HRESULT |
| `get Count` | [out] Count: int* | HRESULT |
| `Item` | Index: VARIANT, [out] Item: RibbonBarTheme** | HRESULT |
| `get _NewEnum` | [out] Unknown: IUnknown* | HRESULT |
| `Create` | BasedOffTheme: VARIANT, [out] Theme: RibbonBarTheme** | HRESULT |
| `Commit` | — | HRESULT |
| `Remove` | Theme: VARIANT | HRESULT |
| `EnumGraphicViews` | pNumGraphicViews: int* | HRESULT |
| `NextGraphicView` | strTitle: BSTR*, strFullName: BSTR*, fileType: int*, hWnd: uint*, bActive: uint*, bDirty: uint* | HRESULT |
| `ActivateGraphicView` | hWnd: uint | HRESULT |

### <a name="_idynamicvisualizationauto"></a>`_IDynamicVisualizationAuto`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `EnableDelayedIndexing` | bEnableDelayedIndexing: bool | HRESULT |

### <a name="iseopennonsolidedgefileuievents"></a>`ISEOpenNonSolidEdgeFileUIEvents`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `OnOpenNonSolidEdgeFileUI` | FileContext: OpenNonSolidEdgeFileContext, FileFilter: BSTR, [out] Filename: BSTR* | HRESULT |

### <a name="_iwindowauto"></a>`_IWindowAuto`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `Activate` | — | HRESULT |
| `ActivateNext` | — | HRESULT |
| `ActivatePrevious` | — | HRESULT |
| `get Application` | [out] Application: Application** | HRESULT |
| `get Caption` | [out] Caption: BSTR* | HRESULT |
| `put Caption` | Caption: BSTR | HRESULT |
| `Close` | [opt]SaveChanges: VARIANT, [opt]Filename: VARIANT, [opt]RouteWorkbook: VARIANT | HRESULT |
| `get Environment` | [out] Environment: BSTR* | HRESULT |
| `put Environment` | Environment: BSTR | HRESULT |
| `get Height` | [out] Height: int* | HRESULT |
| `put Height` | Height: int | HRESULT |
| `get hWnd` | [out] hWnd: int* | HRESULT |
| `get Index` | [out] Index: int* | HRESULT |
| `get Left` | [out] Left: int* | HRESULT |
| `put Left` | Left: int | HRESULT |
| `get Parent` | [out] Parent: IDispatch* | HRESULT |
| `PrintOut` | — | HRESULT |
| `get SelectSet` | [out] SelectSet: SelectSet** | HRESULT |
| `get Top` | [out] Top: int* | HRESULT |
| `put Top` | Top: int | HRESULT |
| `get Type` | [out] Type: BSTR* | HRESULT |
| `get Visible` | [out] Visible: bool* | HRESULT |
| `put Visible` | Visible: bool | HRESULT |
| `get Width` | [out] Width: int* | HRESULT |
| `put Width` | Width: int | HRESULT |
| `get WindowNumber` | [out] WindowNumber: int* | HRESULT |
| `get WindowState` | [out] WindowState: int* | HRESULT |
| `put WindowState` | WindowState: int | HRESULT |
| `get Icon` | [out] Icon: int* | HRESULT |
| `Paste` | — | HRESULT |
| `get UsableHeight` | [out] UsableHeight: int* | HRESULT |
| `get UsableWidth` | [out] UsableWidth: int* | HRESULT |
| `get View` | [out] View: View** | HRESULT |
| `get DrawHwnd` | [out] DrawHwnd: int* | HRESULT |
| `get AttributeSets` | [out] AttributeSets: IDispatch* | HRESULT |
| `get IsAttributeSetPresent` | Name: BSTR, [out] IsAttributeSetPresent: bool* | HRESULT |
| `get Floating` | [out] Floating: bool* | HRESULT |
| `FloatWindow` | — | HRESULT |
| `DockWindow` | — | HRESULT |

### <a name="_iviewauto"></a>`_IViewAuto`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `Fit` | — | HRESULT |
| `get Window` | [out] Window: Window** | HRESULT |
| `ModelToDC` | [in,out] Matrix: SAFEARRAY(double)* | HRESULT |
| `ModelToView` | [in,out] Matrix: SAFEARRAY(double)* | HRESULT |
| `ViewToGLProjection` | [in,out] Matrix: SAFEARRAY(double)* | HRESULT |
| `Update` | — | HRESULT |
| `ShowDrawDC` | — | HRESULT |
| `SwapBuffers` | — | HRESULT |
| `get DrawDC` | [out] DrawDC: int* | HRESULT |
| `GetCamera` | [out] EyeX: double*, [out] EyeY: double*, [out] EyeZ: double*, [out] TargetX: double*, [out] TargetY: double*, [out] TargetZ: double*, [out] UpX: double*, [out] UpY: double*, [out] UpZ: double*, [out] Perspective: bool*, [out] ScaleOrAngle: double* | HRESULT |
| `BeginCameraDynamics` | — | HRESULT |
| `SetCamera` | EyeX: double, EyeY: double, EyeZ: double, TargetX: double, TargetY: double, TargetZ: double, UpX: double, UpY: double, UpZ: double, Perspective: bool, ScaleOrAngle: double | HRESULT |
| `EndCameraDynamics` | — | HRESULT |
| `RotateCamera` | Angle: double, CenterX: double, CenterY: double, CenterZ: double, AxisX: double, AxisY: double, AxisZ: double | HRESULT |
| `PanCamera` | dX: int, dY: int | HRESULT |
| `ZoomCamera` | __MIDL___IViewAuto0000: double | HRESULT |
| `OrientCamera` | cmdtype: int, X1: int, Y1: int, X2: int, Y2: int, X3: int, Y3: int | HRESULT |
| `get ViewEvents` | [out] ViewEvents: ViewEvents** | HRESULT |
| `get DisplayEvents` | [out] DisplayEvents: DisplayEvents** | HRESULT |
| `get GLDisplayEvents` | [out] GLDisplayEvents: GLDisplayEvents** | HRESULT |
| `get RenderEvents` | [out] RenderEvents: RenderEvents** | HRESULT |
| `get AnimationEvents` | [out] AnimationEvents: AnimationEvents** | HRESULT |
| `get DisplayEnabled` | [out] Enabled: bool* | HRESULT |
| `put DisplayEnabled` | Enabled: bool | HRESULT |
| `get CullingEnabled` | [out] Enabled: bool* | HRESULT |
| `put CullingEnabled` | Enabled: bool | HRESULT |
| `get StyleFallbackEnabled` | [out] Enabled: bool* | HRESULT |
| `put StyleFallbackEnabled` | Enabled: bool | HRESULT |
| `get SharpnessLevelCount` | [out] MaxLevel: int* | HRESULT |
| `get SharpnessLevel` | [out] Level: int* | HRESULT |
| `put SharpnessLevel` | Level: int | HRESULT |
| `get StereoEnabled` | [out] Stereo: bool* | HRESULT |
| `put StereoEnabled` | Stereo: bool | HRESULT |
| `get StereoAngle` | [out] Angle: double* | HRESULT |
| `put StereoAngle` | Angle: double | HRESULT |
| `get StereoDeviation` | [out] Deviation: double* | HRESULT |
| `put StereoDeviation` | Deviation: double | HRESULT |
| `TransformModelToDC` | ModelX: double, ModelY: double, ModelZ: double, [out] DeviceX: int*, [out] DeviceY: int* | HRESULT |
| `TransformDCToModel` | DeviceX: int, DeviceY: int, [out] ModelX: double*, [out] ModelY: double*, [out] ModelZ: double* | HRESULT |
| `TransformModelToView` | ModelX: double, ModelY: double, ModelZ: double, [out] ViewX: double*, [out] ViewY: double*, [out] ViewZ: double* | HRESULT |
| `TransformViewToModel` | ViewX: double, ViewY: double, ViewZ: double, [out] ModelX: double*, [out] ModelY: double*, [out] ModelZ: double* | HRESULT |
| `TransformGLProjectionToView` | GLX: double, GLY: double, GLlZ: double, [out] ViewX: double*, [out] ViewY: double*, [out] ViewZ: double* | HRESULT |
| `TransformViewToGLProjection` | ViewX: double, ViewY: double, ViewZ: double, [out] GLX: double*, [out] GLY: double*, [out] GLZ: double* | HRESULT |
| `GetCounter` | Type: int, bReset: bool, [out] dCounter: double* | HRESULT |
| `get GDIBufferModified` | [out] GDIBufferModified: bool* | HRESULT |
| `put GDIBufferModified` | GDIBufferModified: bool | HRESULT |
| `SaveAsImage` | Filename: BSTR, [opt]Width: VARIANT, [opt]Height: VARIANT, [opt]AltViewStyle: VARIANT, [opt]Resolution: VARIANT, [opt]ColorDepth: VARIANT, [opt]ImageQuality: SeImageQualityType, [opt]Invert: bool | HRESULT |
| `get ViewStyle` | [out] ViewStyle: IDispatch* | HRESULT |
| `put ViewStyle` | ViewStyle: IDispatch | HRESULT |
| `get Style` | [out] Style: BSTR* | HRESULT |
| `put Style` | Style: BSTR | HRESULT |
| `SetRenderMode` | mode: VARIANT | HRESULT |
| `get RenderModeType` | [out] pbEnabled: SeRenderModeType* | HRESULT |
| `put RenderModeType` | pbEnabled: SeRenderModeType | HRESULT |
| `get SilhouettesEnabled` | [out] pbEnabled: bool* | HRESULT |
| `put SilhouettesEnabled` | pbEnabled: bool | HRESULT |
| `get SectionPlanesEnabled` | [out] pbEnabled: bool* | HRESULT |
| `put SectionPlanesEnabled` | pbEnabled: bool | HRESULT |
| `SetDisplayDepths` | dFront: double, dBack: double, [opt]FrontFaceStyle: VARIANT*, [opt]BackFaceStyle: VARIANT*, [opt]Monument: VARIANT* | HRESULT |
| `GetDisplayDepths` | [out] pdFront: double*, [out] pdBack: double*, [opt][out] FrontFaceStyle: VARIANT*, [opt][out] BackFaceStyle: VARIANT*, [opt][out] Monument: VARIANT* | HRESULT |
| `SetSectionPlanes` | nPlanes: int, [opt]Positions: VARIANT*, [opt]Normals: VARIANT*, [opt]FaceStyles: VARIANT* | HRESULT |
| `GetSectionPlanes` | [out] pnPlanes: int*, [opt][out] Positions: VARIANT*, [opt][out] Normals: VARIANT*, [opt][out] FaceStyles: VARIANT* | HRESULT |
| `SetAttribute` | Attribute: int, AttributeData: VARIANT | HRESULT |
| `GetAttribute` | Attribute: int, [out] AttributeData: VARIANT* | HRESULT |
| `ClearRotationFocus` | — | HRESULT |
| `GetRotationFocus` | [out] pdPointX: double*, [out] pdPointY: double*, [out] pdPointZ: double*, [out] pdDirectionX: double*, [out] pdDirectionZ: double*, [out] pdDirectionY: double*, [out] pdFront: double*, [out] pdBack: double*, [out] pdRadius: double*, [out] puOptions: int* | HRESULT |
| `SetRotationPoint` | dPointX: double, dPointY: double, dPointZ: double | HRESULT |
| `SetRotationAxis` | dPointX: double, dPointY: double, dPointZ: double, dDirectionX: double, dDirectionY: double, dDirectionZ: double | HRESULT |
| `SetRotationFocus` | dPointX: double, dPointY: double, dPointZ: double, dDirectionX: double, dDirectionZ: double, dDirectionY: double, dFront: double, dBack: double, dRadius: double, uOptions: int | HRESULT |
| `Locate` | lPointX: int, lPointY: int, lRadius: int, [out] pdHitPointX: double*, [out] pdHitPointY: double*, [out] pdHitPointZ: double* | HRESULT |
| `GetModelRange` | [out] pdLowX: double*, [out] pdLowY: double*, [out] pdLowZ: double*, [out] pdHighX: double*, [out] pdHighY: double*, [out] pdHighZ: double* | HRESULT |
| `OrientCameraEx` | lFlags: int, lButtons: int, dX: double, dY: double, dZ: double, dYaw: double, dPitch: double, dRoll: double | HRESULT |
| `GetCameraEx` | [out] lFlags: int*, [out] dEyeX: double*, [out] dEyeY: double*, [out] dEyeZ: double*, [out] dTargetX: double*, [out] dTargetY: double*, [out] dTargetZ: double*, [out] dUpX: double*, [out] dUpY: double*, [out] dUpZ: double*, [out] dNearClip: double*, [out] dFarClip: double*, [out] dFrameWidth: double*, [out] dFrameHeight: double*, [out] dFrameEyeX: double*, [out] dFrameEyeY: double*, [out] dFrameScale: double* | HRESULT |
| `SetCameraEx` | lFlags: int, dEyeX: double, dEyeY: double, dEyeZ: double, dTargetX: double, dTargetY: double, dTargetZ: double, dUpX: double, dUpY: double, dUpZ: double, dNearClip: double, dFarClip: double, dFrameWidth: double, dFrameHeight: double, dFrameEyeX: double, dFrameEyeY: double, dFrameScale: double | HRESULT |
| `SaveCurrentView` | Name: VARIANT | HRESULT |
| `ApplyNamedView` | Name: VARIANT | HRESULT |
| `AreaZoomCamera` | X1: int, Y1: int, X2: int, Y2: int | HRESULT |
| `CreateUserRange` | [out] pidUserRange: int* | HRESULT |
| `DeleteUserRange` | idUserRange: int | HRESULT |
| `GetUserRange` | idUserRange: int, [out] pdLowX: double*, [out] pdLowY: double*, [out] pdLowZ: double*, [out] pdHighX: double*, [out] pdHighY: double*, [out] pdHighZ: double* | HRESULT |
| `SetUserRange` | idUserRange: int, dLowX: double, dLowY: double, dLowZ: double, dHighX: double, dHighY: double, dHighZ: double | HRESULT |
| `get MovieFrameRate` | [out] pdwMovieFrameRate: uint* | HRESULT |
| `put MovieFrameRate` | pdwMovieFrameRate: uint | HRESULT |
| `get MovieBitRate` | [out] pdwMovieBitRate: uint* | HRESULT |
| `put MovieBitRate` | pdwMovieBitRate: uint | HRESULT |
| `get MovieCodec` | [out] pCodec: BSTR* | HRESULT |
| `put MovieCodec` | pCodec: BSTR | HRESULT |
| `get MovieQuality` | [out] pdwMovieQuality: uint* | HRESULT |
| `put MovieQuality` | pdwMovieQuality: uint | HRESULT |
| `get MovieTitle` | [out] pTitle: BSTR* | HRESULT |
| `put MovieTitle` | pTitle: BSTR | HRESULT |
| `get MovieSubTitle` | [out] pSubTitle: BSTR* | HRESULT |
| `put MovieSubTitle` | pSubTitle: BSTR | HRESULT |
| `get MovieCopyright` | [out] pCopyright: BSTR* | HRESULT |
| `put MovieCopyright` | pCopyright: BSTR | HRESULT |
| `get MovieAuthor` | [out] pAuthor: BSTR* | HRESULT |
| `put MovieAuthor` | pAuthor: BSTR | HRESULT |
| `get MovieAuthorURL` | [out] pAuthorURL: BSTR* | HRESULT |
| `put MovieAuthorURL` | pAuthorURL: BSTR | HRESULT |
| `get MovieDescription` | [out] pDescription: BSTR* | HRESULT |
| `put MovieDescription` | pDescription: BSTR | HRESULT |
| `GetAvailableMovieCodecs` | [out] AvailableCodecs: SAFEARRAY(BSTR)* | HRESULT |
| `SetMovieResolution` | StandardMovieResolution: seMovieStandardResolutionConstants | HRESULT |
| `SetCustomMovieResolution` | nWidth: int, nHeight: int | HRESULT |
| `CreateMovieRecorder` | Format: seMovieFormatConstants | HRESULT |
| `DestroyMovieRecorder` | — | HRESULT |
| `BeginMovieRecording` | Filename: BSTR | HRESULT |
| `AddFrameToMovie` | KeyFrame: bool, [out] pNewFrameCount: int* | HRESULT |
| `EndMovieRecording` | — | HRESULT |
| `RangeZoomCamera` | dLowX: double, dLowY: double, dLowZ: double, dHighX: double, dHighY: double, dHighZ: double | HRESULT |
| `UserRangeZoomCamera` | idUserRange: int | HRESULT |
| `RefreshView` | nOptions: int | HRESULT |
| `get SharpenLevel` | [out] peLevel: seSharpenLevelConstants* | HRESULT |
| `put SharpenLevel` | peLevel: seSharpenLevelConstants | HRESULT |
| `get SectionPlanesOptions` | [out] Options: VARIANT* | HRESULT |
| `put SectionPlanesOptions` | Options: VARIANT | HRESULT |
| `SetSectionPlanesParams` | Options: VARIANT, PlaneCount: VARIANT, [opt]Positions: VARIANT*, [opt]Normals: VARIANT*, [opt]Colors: VARIANT* | HRESULT |
| `GetSectionPlanesParams` | [out] Options: VARIANT*, [opt][out] PlaneCount: VARIANT*, [opt][out] Positions: VARIANT*, [opt][out] Normals: VARIANT*, [opt][out] Colors: VARIANT* | HRESULT |

### <a name="iseviewevents"></a>`ISEViewEvents`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `Changed` | — | HRESULT |
| `Destroyed` | — | HRESULT |
| `StyleChanged` | — | HRESULT |

### <a name="isehdcdisplayevents"></a>`ISEhDCDisplayEvents`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `BeginDisplay` | — | HRESULT |
| `EndDisplay` | — | HRESULT |
| `BeginhDCMainDisplay` | hDC: int, ModelToDC: double*, Rect: int* | HRESULT |
| `EndhDCMainDisplay` | hDC: int, ModelToDC: double*, Rect: int* | HRESULT |

### <a name="iseigldisplayevents"></a>`ISEIGLDisplayEvents`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `BeginDisplay` | — | HRESULT |
| `EndDisplay` | — | HRESULT |
| `BeginIGLMainDisplay` | pUnknownIGL: IUnknown | HRESULT |
| `EndIGLMainDisplay` | pUnknownIGL: IUnknown | HRESULT |

### <a name="iserenderevents"></a>`ISERenderEvents`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `PreRender` | pDevice: IUnknown, pScene: IUnknown | HRESULT |
| `Render` | pDevice: IUnknown, pScene: IUnknown, pProgress: IUnknown | HRESULT |
| `PostRender` | pDevice: IUnknown, pScene: IUnknown | HRESULT |

### <a name="iseanimationevents"></a>`ISEAnimationEvents`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `AnimationEvent` | AnimationEventType: AnimationEventConstants, nFrame: int | HRESULT |

### <a name="_inamedviewsauto"></a>`_INamedViewsAuto`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `get Count` | [out] Count: int* | HRESULT |
| `get Names` | [out] Names: SAFEARRAY(BSTR)* | HRESULT |
| `Create` | Name: BSTR, [out] Item: NamedView** | HRESULT |
| `GetByName` | Name: BSTR, [out] Item: NamedView** | HRESULT |
| `Remove` | Name: BSTR | HRESULT |
| `Rename` | currName: BSTR, NewName: BSTR | HRESULT |
| `get _NewEnum` | [out] Unknown: IUnknown* | HRESULT |
| `Item` | Index: VARIANT, [out] Item: NamedView** | HRESULT |

### <a name="_inamedviewauto"></a>`_INamedViewAuto`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `get Name` | [out] Name: BSTR* | HRESULT |
| `put Name` | Name: BSTR | HRESULT |
| `get Description` | [out] Description: BSTR* | HRESULT |
| `put Description` | Description: BSTR | HRESULT |
| `GetCamera` | [out] EyeX: double*, [out] EyeY: double*, [out] EyeZ: double*, [out] TargetX: double*, [out] TargetY: double*, [out] TargetZ: double*, [out] UpX: double*, [out] UpY: double*, [out] UpZ: double*, [out] Perspective: bool*, [out] ScaleOrAngle: double* | HRESULT |
| `SetCamera` | EyeX: double, EyeY: double, EyeZ: double, TargetX: double, TargetY: double, TargetZ: double, UpX: double, UpY: double, UpZ: double, Perspective: bool, ScaleOrAngle: double | HRESULT |
| `GetCameraEx` | [out] plFlags: int*, [out] pdEyeX: double*, [out] pdEyeY: double*, [out] pdEyeZ: double*, [out] pdTargetX: double*, [out] pdTargetY: double*, [out] pdTargetZ: double*, [out] pdUpX: double*, [out] pdUpY: double*, [out] pdUpZ: double*, [out] pdNearClip: double*, [out] pdFarClip: double*, [out] pdFrameWidth: double*, [out] pdFrameHeight: double*, [out] pdFrameEyeX: double*, [out] pdFrameEyeY: double*, [out] pdFrameScale: double* | HRESULT |
| `SetCameraEx` | lFlags: int, dEyeX: double, dEyeY: double, dEyeZ: double, dTargetX: double, dTargetY: double, dTargetZ: double, dUpX: double, dUpY: double, dUpZ: double, dNearClip: double, dFarClip: double, dFrameWidth: double, dFrameHeight: double, dFrameEyeX: double, dFrameEyeY: double, dFrameScale: double | HRESULT |

### <a name="_iunitsofmeasureauto"></a>`_IUnitsOfMeasureAuto`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `ParseUnit` | Index: int, UnitString: BSTR, [out] Dbus: VARIANT* | HRESULT |
| `FormatUnit` | Index: int, Dbus: double, [opt]PrecisionConstant: VARIANT, [out] UnitString: VARIANT* | HRESULT |
| `get Application` | [out] Application: Application** | HRESULT |
| `get Parent` | [out] Parent: IDispatch* | HRESULT |
| `get Count` | [out] Count: int* | HRESULT |
| `get _NewEnum` | [out] Enum: IUnknown* | HRESULT |
| `Item` | Index: VARIANT, [out] Item: UnitOfMeasure** | HRESULT |

### <a name="_iunitofmeasureauto"></a>`_IUnitOfMeasureAuto`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `get Type` | [out] Type: UnitTypeConstants* | HRESULT |
| `get Units` | [out] Units: int* | HRESULT |
| `put Units` | Units: int | HRESULT |
| `get Precision` | [out] Precision: int* | HRESULT |
| `put Precision` | Precision: int | HRESULT |

### <a name="_icpdinitializerbidmauto"></a>`_ICPDInitializerBiDMAuto`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `GetDocuments` | [out] psaDocs: SAFEARRAY(VARIANT)* | HRESULT |
| `GetPropertiesInfo` | bstrDocName: BSTR, [out] pvPropInfo: VARIANT* | HRESULT |
| `SetPropertiesInfo` | bstrDocName: BSTR, vPropInfo: VARIANT | HRESULT |
| `SetControlsBehavior` | vbDisableAssignAllButtonAndMenu: bool, vbDisableDocumentNumberCell: bool, vbDisableRevisionIDCell: bool | HRESULT |

### <a name="isecommandbarpopup"></a>`ISECommandBarPopup`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `get CommandBar` | [out] CommandBar: CommandBar** | HRESULT |
| `get Controls` | [out] Controls: CommandBarControls** | HRESULT |

### <a name="isedocumentevents"></a>`ISEDocumentEvents`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `BeforeClose` | — | HRESULT |
| `BeforeSave` | — | HRESULT |
| `AfterSave` | — | HRESULT |
| `SelectSetChanged` | SelectSet: IDispatch | HRESULT |

### <a name="isebendtableevents"></a>`ISEBendTableEvents`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `BendTableStart` | — | HRESULT |
| `BendTableEnd` | — | HRESULT |
| `BendSelect` | BendIndex: int, ColumnId: int | HRESULT |
| `BendUserDataChanged` | BendIndex: int, ColumnId: int | HRESULT |

### <a name="isemodelrecomputeevents"></a>`ISEModelRecomputeEvents`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `BeforeRecompute` | — | HRESULT |
| `AfterFeatureIsAdded` | AddFlag: SeFeatureAddFlag, Feature: IDispatch | HRESULT |
| `BeforeFeatureIsDeleted` | DeleteFlag: SeFeatureDeleteFlag, Feature: IDispatch | HRESULT |
| `AfterFeatureIsModified` | ModifyFlag: SeFeatureModifyFlag, Feature: IDispatch | HRESULT |
| `AfterRecompute` | — | HRESULT |
| `BeforeModelIsDeleted` | Model: IDispatch | HRESULT |

### <a name="isedynamiceditevents"></a>`ISEDynamicEditEvents`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `BeginDynamicEdit` | — | HRESULT |
| `EndDynamicEdit` | — | HRESULT |

### <a name="iseapplicationeventsex"></a>`ISEApplicationEventsEx`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `OnCommandUpdateUI` | CommandID: int, [in,out] CommandFlags: int*, [out] MenuItemTextD: BSTR* | HRESULT |

### <a name="iseapplicationeventsex2"></a>`ISEApplicationEventsEx2`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `OnBeforeDocumentOpen` | Context: ApplicationBeforeDocumentOpenEvent, Filename: BSTR, [out] CancelOpen: bool* | HRESULT |

### <a name="iseapplicationreadyevents"></a>`ISEApplicationReadyEvents`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `OnApplicationReady` | Context: ApplicationReadyEvent | HRESULT |

### <a name="iseapplicationactiveframeswitchingevents"></a>`ISEApplicationActiveFrameSwitchingEvents`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `OnApplicationActiveFrameSwitching` | Context: ApplicationActiveFrameSwitchingEvent, hWndPreviouslyActiveFrame: int, hWndNewlyActiveFrame: int | HRESULT |

### <a name="iseapplicationlicenseevents"></a>`ISEApplicationLicenseEvents`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `OnApplicationLicense` | Context: ApplicationLicenseEvent, FeatureName: BSTR | HRESULT |

### <a name="iseapplicationdocumentloadingevents"></a>`ISEApplicationDocumentLoadingEvents`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `OnApplicationDocumentLoading` | TopLevelFilename: BSTR, Context: ApplicationDocumentLoadingEvent, Level: uint, [out] Cancel: bool* | HRESULT |

### <a name="iseaddineventsex"></a>`ISEAddInEventsEx`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `OnCommandOnLineHelp` | HelpCommandID: int, CommandID: int, [out] HelpURL: BSTR* | HRESULT |

### <a name="iseaddineventsex2"></a>`ISEAddInEventsEx2`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `OnCommand` | CommandID: int, Context: ShortCutMenuContextConstants, ActiveDocumentType: DocumentTypeConstants, pActiveDocument: IDispatch, pActiveWindow: IDispatch, pActiveSelectSet: IDispatch | HRESULT |
| `OnCommandHelp` | hFrameWnd: int, HelpCommandID: int, CommandID: int | HRESULT |
| `OnCommandUpdateUI` | CommandID: int, Context: ShortCutMenuContextConstants, ActiveDocumentType: DocumentTypeConstants, pActiveDocument: IDispatch, pActiveWindow: IDispatch, pActiveSelectSet: IDispatch, [in,out] CommandFlags: int*, [out] MenuItemText: BSTR*, [in,out] BitmapID: int* | HRESULT |
| `OnCommandOnLineHelp` | HelpCommandID: int, CommandID: int, [out] HelpURL: BSTR* | HRESULT |

### <a name="iseaddinedgebarevents"></a>`ISEAddInEdgeBarEvents`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `AddPage` | theDocument: IDispatch | HRESULT |
| `RemovePage` | theDocument: IDispatch | HRESULT |
| `IsPageDisplayable` | theDocument: IDispatch, EnvironmentCatID: BSTR, [out] vbIsPageDisplayable: bool* | HRESULT |

### <a name="iseaddinedgebareventsex"></a>`ISEAddInEdgeBarEventsEx`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `AddPage` | theDocument: IDispatch | HRESULT |
| `RemovePage` | theDocument: IDispatch | HRESULT |
| `IsPageDisplayable` | theDocument: IDispatch, EnvironmentCatID: BSTR, [out] vbIsPageDisplayable: bool* | HRESULT |

### <a name="iseassemblychangeevents"></a>`ISEAssemblyChangeEvents`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `BeforeChange` | theDocument: IDispatch, Object: IDispatch, Type: ObjectType, ChangeType: seAssemblyChangeEventsConstants | HRESULT |
| `AfterChange` | theDocument: IDispatch, Object: IDispatch, Type: ObjectType, ChangeType: seAssemblyChangeEventsConstants | HRESULT |

### <a name="iseassemblyconfigurationchangeevents"></a>`ISEAssemblyConfigurationChangeEvents`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `OnBeforeAssemblyConfigurationChange` | theDocument: IDispatch, varConfigNames: VARIANT*, nConfigNameCount: int | HRESULT |
| `OnAfterAssemblyConfigurationChange` | theDocument: IDispatch, varConfigNames: VARIANT*, nConfigNameCount: int | HRESULT |

### <a name="iseassemblyrecomputeevents"></a>`ISEAssemblyRecomputeEvents`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `BeforeRecompute` | theDocument: IDispatch | HRESULT |
| `AfterRecompute` | theDocument: IDispatch | HRESULT |
| `AfterAdd` | theDocument: IDispatch, Object: IDispatch, Type: ObjectType | HRESULT |
| `BeforeDelete` | theDocument: IDispatch, Object: IDispatch, Type: ObjectType | HRESULT |
| `AfterModify` | theDocument: IDispatch, Object: IDispatch, Type: ObjectType, ModifyType: seAssemblyEventConstants | HRESULT |

### <a name="iseassemblyfamilyevents"></a>`ISEAssemblyFamilyEvents`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `BeforeMemberActivate` | theDocument: IDispatch, memberName: BSTR | HRESULT |
| `AfterMemberActivate` | theDocument: IDispatch, memberName: BSTR | HRESULT |
| `BeforeMemberCreate` | theDocument: IDispatch, memberName: BSTR | HRESULT |
| `AfterMemberCreate` | theDocument: IDispatch, memberName: BSTR | HRESULT |
| `BeforeMemberDelete` | theDocument: IDispatch, memberName: BSTR | HRESULT |
| `AfterMemberDelete` | theDocument: IDispatch, memberName: BSTR | HRESULT |

### <a name="iseassemblyfamilyevents2"></a>`ISEAssemblyFamilyEvents2`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `BeforeMemberActivate` | theDocument: IDispatch, memberName: BSTR | HRESULT |
| `AfterMemberActivate` | theDocument: IDispatch, memberName: BSTR | HRESULT |
| `BeforeMemberCreate` | theDocument: IDispatch, memberName: BSTR | HRESULT |
| `AfterMemberCreate` | theDocument: IDispatch, memberName: BSTR | HRESULT |
| `BeforeMemberDelete` | theDocument: IDispatch, memberName: BSTR | HRESULT |
| `AfterMemberDelete` | theDocument: IDispatch, memberName: BSTR | HRESULT |
| `BeforeMemberRename` | theDocument: IDispatch, OldMemberName: BSTR | HRESULT |
| `AfterMemberRename` | theDocument: IDispatch, NewMemberName: BSTR | HRESULT |

### <a name="isefamilyofpartsevents"></a>`ISEFamilyOfPartsEvents`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `AfterMemberDocumentCreated` | theMember: IDispatch | HRESULT |
| `AfterMemberDocumentRenamed` | theMember: IDispatch, OldName: BSTR | HRESULT |
| `BeforeMemberDocumentDeleted` | theMember: IDispatch | HRESULT |

### <a name="isefamilyofpartsexevents"></a>`ISEFamilyOfPartsExEvents`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `AfterMemberDocumentCreated` | theMember: IDispatch, DocumentExists: bool | HRESULT |
| `AfterMemberDocumentRenamed` | theMember: IDispatch, OldName: BSTR | HRESULT |
| `BeforeMemberDocumentDeleted` | theMember: IDispatch | HRESULT |

### <a name="isedividepartevents"></a>`ISEDividePartEvents`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `AfterDividePartDocumentCreated` | theMember: IDispatch | HRESULT |
| `AfterDividePartDocumentRenamed` | theMember: IDispatch, OldName: BSTR | HRESULT |
| `BeforeDividePartDocumentDeleted` | theMember: IDispatch | HRESULT |

### <a name="isedrawingviewevents"></a>`ISEDrawingViewEvents`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `AfterUpdate` | DrawingView: IDispatch | HRESULT |

### <a name="isepartslistevents"></a>`ISEPartsListEvents`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `AfterUpdate` | PartsList: IDispatch | HRESULT |

### <a name="isedraftbendtableevents"></a>`ISEDraftBendTableEvents`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `AfterUpdate` | DraftBendTable: IDispatch | HRESULT |

### <a name="iseconnectortableevents"></a>`ISEConnectorTableEvents`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `AfterUpdate` | ConnectorTable: IDispatch | HRESULT |

### <a name="iseblocktableevents"></a>`ISEBlockTableEvents`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `AfterUpdate` | BlockTable: IDispatch | HRESULT |

### <a name="isecommandinfoex"></a>`ISECommandInfoEx`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `get imageID` | [out] Id: int* | HRESULT |

### <a name="iseassemblyphysicalpropertieschangeevents"></a>`ISEAssemblyPhysicalPropertiesChangeEvents`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `OnAfterAssemblyPhysicalPropertiesChange` | theDocument: IDispatch | HRESULT |
| `OnBeforeAssemblyPhysicalPropertiesChange` | theDocument: IDispatch | HRESULT |

### <a name="isephysicalpropertieschangeevents"></a>`ISEPhysicalPropertiesChangeEvents`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `OnAfterPartPhysicalPropertiesChange` | theMember: IDispatch | HRESULT |
| `OnBeforePartPhysicalPropertiesChange` | theMember: IDispatch | HRESULT |

### <a name="iselocatefilterevents"></a>`ISELocateFilterEvents`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `Filter` | pGraphicDispatch: IDispatch, [out] vbValid: bool* | HRESULT |

### <a name="isecommandex"></a>`ISECommandEx`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `BeginUndoTransaction` | TransactionName: BSTR | HRESULT |

### <a name="isecommandex2"></a>`ISECommandEx2`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `BeginTransparentUndoTransaction` | TransactionName: BSTR | HRESULT |
| `EndUndoTransaction` | — | HRESULT |

### <a name="iseaddinex"></a>`ISEAddInEx`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `SetAddInInfoEx` | ResourceFilename: BSTR, EnvironmentCatID: BSTR, CategoryName: BSTR, IDColorBitmapMedium: int, IDColorBitmapLarge: int, IDMonochromeBitmapMedium: int, IDMonochromeBitmapLarge: int, NumberOfCommands: int, CommandNames: SAFEARRAY(BSTR)*, [in,out] CommandIDs: SAFEARRAY(int)* | HRESULT |
| `get AddInEdgeBarEvents` | [out] AddInEdgeBarEvents: AddInEdgeBarEvents** | HRESULT |

### <a name="iseaddinex2"></a>`ISEAddInEx2`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `SetAddInInfoEx2` | ResourceFilename: BSTR, EnvironmentCatID: BSTR, CategoryName: BSTR, IDColorBitmapMedium: int, IDColorBitmapLarge: int, IDMonochromeBitmapMedium: int, IDMonochromeBitmapLarge: int, NumberOfCommands: int, CommandNames: SAFEARRAY(BSTR)*, [in,out] CommandIDs: SAFEARRAY(int)*, CommandButtonStyles: SAFEARRAY(int)* | HRESULT |

### <a name="iseaddinsaveastranslatorevents"></a>`ISEAddInSaveAsTranslatorEvents`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `OnOptions` | theDocument: IDispatch, FileExtension: BSTR | HRESULT |
| `OnOptionsUpdateUI` | theDocument: IDispatch, FileExtension: BSTR, [in,out] Flags: int* | HRESULT |
| `OnSaveAs` | theDocument: IDispatch, SaveAsFileName: BSTR, [out] hResult: int* | HRESULT |

### <a name="iseaddinsaveastranslator"></a>`ISEAddInSaveAsTranslator`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `SetSaveAsTranlatorInfo` | DocumentType: DocumentTypeConstants, Filter: BSTR | HRESULT |
| `get AddInSaveAsTranslatorEvents` | [out] AddInSaveAsTranslatorEvents: AddInSaveAsTranslatorEvents** | HRESULT |

### <a name="isolidedgeaddin"></a>`ISolidEdgeAddIn`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `OnConnection` | Application: IDispatch, ConnectMode: SeConnectMode, AddInInstance: AddIn* | HRESULT |
| `OnConnectToEnvironment` | EnvCatID: BSTR, pEnvironmentDispatch: IDispatch, bFirstTime: bool | HRESULT |
| `OnDisconnection` | DisconnectMode: SeDisconnectMode | HRESULT |

### <a name="isolidedgebar"></a>`ISolidEdgeBar`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `AddPage` | theDocument: IDispatch, InstanceHandle: int, nBitmapID: int, Tooltip: BSTR, nOptions: int, [out] hWnd: int* | HRESULT |
| `RemovePage` | theDocument: IDispatch, hWnd: int, nOptions: int | HRESULT |
| `SetActivePage` | theDocument: IDispatch, hWnd: int, nOptions: int | HRESULT |

### <a name="isolidedgebarex"></a>`ISolidEdgeBarEx`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `AddPageEx` | theDocument: IDispatch, ResourceFilename: BSTR, nBitmapID: int, Tooltip: BSTR, nOptions: int, [out] hWnd: int* | HRESULT |

### <a name="isolidedgebarex2"></a>`ISolidEdgeBarEx2`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `AddPageEx2` | theDocument: IDispatch, ResourceFilename: BSTR, Index: int, nBitmapID: int, Tooltip: BSTR, Title: BSTR, Caption: BSTR, nOptions: int, [opt]Direction: VARIANT, [opt]InitialWidth: VARIANT, [opt]InitialHeight: VARIANT, [out] hWnd: int* | HRESULT |
| `RemovePageEx2` | theDocument: IDispatch, Index: int, hWnd: int, nOptions: int | HRESULT |
| `SetActivePageEx2` | theDocument: IDispatch, Index: int, hWnd: int, nOptions: int | HRESULT |

### <a name="isolidedgeribbonbar"></a>`ISolidEdgeRibbonBar`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `AddRibbon` | DialogId: int, InstanceHandle: int, [out] hWndRibbon: int* | HRESULT |
| `ShowRibbon` | — | HRESULT |
| `HideRibbon` | — | HRESULT |
| `RemoveRibbon` | — | HRESULT |
| `AddEditField` | Id: int | HRESULT |
| `SetCurrentFocus` | — | HRESULT |
| `GetCurrentFocus` | [out] Id: int* | HRESULT |
| `NextFocus` | — | HRESULT |
| `SetAccelerators` | Accelerators: SAFEARRAY(sbyte)* | HRESULT |

### <a name="isolidedgeribbonbarex"></a>`ISolidEdgeRibbonBarEx`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `AddRibbonEx` | DialogId: int, ResourceFilename: BSTR, [out] hWndRibbon: int* | HRESULT |

### <a name="isolidedgecommandbar"></a>`ISolidEdgeCommandBar`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `AddGroup` | nTitleID: int, nDialogId: int, nBitmapID: int, ResourceFilename: BSTR, bExpandable: bool, bExpanded: bool, bEnabled: bool, bRedraw: bool, [out] hWndGroupDialog: int* | HRESULT |
| `AddCommandBarHeader` | bDoIt: bool, bOptions: bool, DoitText: BSTR, OptionsText: BSTR | HRESULT |
| `SetCommandBarHeaderText` | DoitText: BSTR, OptionsText: BSTR | HRESULT |
| `RemoveGroup` | nTitleID: int, [opt]hWndGroupDialog: VARIANT | HRESULT |
| `ShowGroup` | nTitleID: int, [opt]hWndGroupDialog: VARIANT | HRESULT |
| `HideGroup` | nTitleID: int, [opt]hWndGroupDialog: VARIANT | HRESULT |
| `EnableGroup` | nTitleID: int, bEnabled: bool, bDisableAllOthers: bool | HRESULT |
| `IsGroupEnabled` | nTitleID: int, [out] bEnabled: bool* | HRESULT |
| `ExpandGroup` | nTitleID: int, bExpanded: bool, bCollapseAllOthers: bool | HRESULT |
| `IsGroupExpanded` | nTitleID: int, [out] bExpanded: bool* | HRESULT |
| `EnsureVisible` | nTitleID: int, [opt]hWndGroupDialog: VARIANT | HRESULT |
| `ShowGroups` | — | HRESULT |
| `HideGroups` | — | HRESULT |
| `RemoveGroups` | — | HRESULT |
| `AddBitmapToButton` | hWndGroupDialog: int, nButtonID: int, nBitmapID: int, [opt]ResourceFilename: VARIANT | HRESULT |
| `AddEditField` | hWndGroupDialog: int, Id: int | HRESULT |
| `SetCurrentFocus` | hWndGroupDialog: int | HRESULT |
| `GetCurrentFocus` | hWndGroupDialog: int, [out] Id: int* | HRESULT |
| `NextFocus` | hWndGroupDialog: int | HRESULT |
| `SetAccelerators` | hWndGroupDialog: int, Accelerators: SAFEARRAY(sbyte)* | HRESULT |

### <a name="iseeceventsex"></a>`ISEECEventsEx`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `PDM_OnAfterDocumentUpload` | varUploadFileList: VARIANT* | HRESULT |

### <a name="isesketchrecomputeevents"></a>`ISESketchRecomputeEvents`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `BeforeRecompute` | Sketch: IDispatch | HRESULT |
| `AfterRecompute` | Sketch: IDispatch | HRESULT |
| `AfterSketchIsModified` | ModifySkFlag: SeModifySketchFlag, Entity: IDispatch, Sketch: IDispatch | HRESULT |
| `BeforeSketchIsDeleted` | — | HRESULT |

### <a name="_ivariableauto"></a>`_IVariableAuto`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `put Name` | pName: BSTR | HRESULT |
| `get Name` | [out] pName: BSTR* | HRESULT |
| `get UnitsType` | [out] pUnitType: int* | HRESULT |
| `put UnitsType` | pUnitType: int | HRESULT |
| `put Value` | pDoubleValue: double | HRESULT |
| `get Value` | [out] pDoubleValue: double* | HRESULT |
| `put Properties` | pProperties: int | HRESULT |
| `get Properties` | [out] pProperties: int* | HRESULT |
| `put Formula` | pFormula: BSTR | HRESULT |
| `get Formula` | [out] pFormula: BSTR* | HRESULT |
| `SetRange` | LowValue: BSTR, Condition: int, HighValue: BSTR | HRESULT |
| `GetRange` | [out] LowValue: BSTR*, [out] Condition: int*, [out] HighValue: BSTR* | HRESULT |
| `SetRangeEx` | LowValue: BSTR, LowLimitVarName: BSTR, HighValue: BSTR, HighLimitVarName: BSTR, Condition: int, bSkipSettingInitialValue: int | HRESULT |
| `SetValue` | Value: BSTR | HRESULT |
| `GetValue` | [out] Value: BSTR* | HRESULT |
| `Delete` | — | HRESULT |
| `get Type` | [out] Type: ObjectType* | HRESULT |
| `put VariableTableName` | pName: BSTR | HRESULT |
| `get VariableTableName` | [out] pName: BSTR* | HRESULT |
| `put Expose` | pbExpose: int | HRESULT |
| `get Expose` | [out] pbExpose: int* | HRESULT |
| `put ExposeName` | pbsName: BSTR | HRESULT |
| `get ExposeName` | [out] pbsName: BSTR* | HRESULT |
| `get DisplayName` | [out] pName: BSTR* | HRESULT |
| `get SystemName` | [out] pName: BSTR* | HRESULT |
| `get IsSuppressVariable` | [out] IsSuppressVariable: bool* | HRESULT |
| `GetValueOutOfRange` | [out] ValueOutOfRange: double* | HRESULT |
| `GetDiscreteValues` | [out] DiscreteValues: SAFEARRAY(double)* | HRESULT |
| `SetDiscreteValues` | DiscreteValues: SAFEARRAY(double)* | HRESULT |
| `AddDiscreteValue` | DiscreteValue: double | HRESULT |
| `RemoveDiscreteValue` | DiscreteValue: double | HRESULT |
| `ClearLimitsOrDiscreteValues` | — | HRESULT |
| `AddDiscreteVariables` | DiscreteVariables: SAFEARRAY(BSTR)* | HRESULT |
| `GetDiscreteVariables` | [out] DiscreteVariables: VARIANT*, [out] numDiscreteVariables: int* | HRESULT |
| `RemoveDiscreteVariables` | DiscreteVariables: SAFEARRAY(BSTR)* | HRESULT |
| `GetComment` | [out] Comment: BSTR* | HRESULT |
| `SetComment` | Comment: BSTR | HRESULT |
| `HasExternalLink` | [out] bLinked: bool* | HRESULT |
| `IsExternalLinkFrozen` | [out] bFrozen: bool* | HRESULT |
| `GetExternalLinkInfo` | [out] SourceVariableName: BSTR*, [out] SourceDocumenetName: BSTR* | HRESULT |
| `FreezeExternalLink` | — | HRESULT |
| `ThawExternalLink` | — | HRESULT |
| `BreakExternalLink` | — | HRESULT |
| `get IsReadOnly` | [out] pbIsReadOnly: bool* | HRESULT |
| `get VariableType` | [out] pbVariableType: seVariableTypeConstants* | HRESULT |
| `GetValueRangeHighValue` | [out] pdHighValue: double* | HRESULT |
| `SetValueRangeHighValue` | dHighValue: double | HRESULT |
| `GetValueRangeLowValue` | [out] pdHighValue: double* | HRESULT |
| `SetValueRangeLowValue` | dHighValue: double | HRESULT |
| `SetValueRangeValues` | LowValue: double, Condition: int, HighValue: double | HRESULT |
| `GetValueRangeValues` | [out] LowValue: double*, [out] Condition: int*, [out] HighValue: double* | HRESULT |
| `GetValueDiscreteValues` | [out] DiscreteValues: SAFEARRAY(double)* | HRESULT |
| `SetValueDiscreteValues` | DiscreteValues: SAFEARRAY(double)* | HRESULT |
| `GetValueEx` | [out] pdValue: double*, seUnitsType: seUnitsTypeConstants | HRESULT |
| `SetValueEx` | dValue: double, seUnitsType: seUnitsTypeConstants | HRESULT |
| `GetRangeEx` | [out] LowValue: BSTR*, [out] LowLimitVarName: BSTR*, [out] HighValue: BSTR*, [out] HighLimitVarName: BSTR*, [out] Condition: int* | HRESULT |
| `HasVariableLimit` | [out] bVariableLimit: bool*, [out] LimitValue: VariableLimitValueConstant* | HRESULT |
| `get AttributeSets` | [out] AttributeSets: IDispatch* | HRESULT |
| `get IsAttributeSetPresent` | Name: BSTR, [out] IsAttributeSetPresent: bool* | HRESULT |

### <a name="_ivariablelistauto"></a>`_IVariableListAuto`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `get Count` | [out] pCount: int* | HRESULT |
| `Item` | Index: VARIANT, [out] pItem: IDispatch* | HRESULT |
| `get _NewEnum` | [out] pEnum: IUnknown* | HRESULT |
| `Remove` | Index: VARIANT | HRESULT |
| `Add` | variable: VARIANT | HRESULT |

### <a name="_ivariablesauto"></a>`_IVariablesAuto`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `get Count` | [out] pCount: int* | HRESULT |
| `Item` | Index: VARIANT, [out] pItem: IDispatch* | HRESULT |
| `get _NewEnum` | [out] pEnum: IUnknown* | HRESULT |
| `get Application` | [out] pApp: IDispatch* | HRESULT |
| `get Parent` | [out] pParent: IDispatch* | HRESULT |
| `Add` | pName: BSTR, pFormula: BSTR, [opt]UnitsType: VARIANT, [out] ppVariable: IDispatch* | HRESULT |
| `AddFromClipboard` | pName: BSTR, [opt]UnitsType: VARIANT, [out] ppVariable: IDispatch* | HRESULT |
| `Edit` | pName: BSTR, pFormula: BSTR | HRESULT |
| `EditFromClipboard` | pName: BSTR | HRESULT |
| `PutName` | pVariable: IDispatch, pName: BSTR | HRESULT |
| `GetName` | pVariable: IDispatch, [out] pName: BSTR* | HRESULT |
| `Translate` | pName: BSTR, [out] ppVariable: IDispatch* | HRESULT |
| `Query` | pFindCriterium: BSTR, [opt]NamedBy: VARIANT, [opt]VarType: VARIANT, [opt]CaseInsensitive: VARIANT, [out] pEnum: IDispatch* | HRESULT |
| `GetFormula` | wcpName: BSTR, [out] wcpFormula: BSTR* | HRESULT |
| `GetDisplayName` | pVariable: IDispatch, [out] pName: BSTR* | HRESULT |
| `GetSystemName` | pVariable: IDispatch, [out] pName: BSTR* | HRESULT |
| `CopyToClipboard` | bsName: BSTR | HRESULT |

### <a name="_iinterpartlinkauto"></a>`_IInterpartLinkAuto`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `IsFrozen` | [out] bFrozen: bool* | HRESULT |
| `GetInfo` | [out] SourceFeatureName: BSTR*, [out] SourceDocumenetName: BSTR* | HRESULT |
| `Freeze` | — | HRESULT |
| `Thaw` | — | HRESULT |
| `BreakLink` | — | HRESULT |

### <a name="_iinterpartlinksauto"></a>`_IInterpartLinksAuto`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `get Count` | [out] pCount: int* | HRESULT |
| `Item` | Index: VARIANT, [out] pItem: IDispatch* | HRESULT |
| `get Application` | [out] pApp: IDispatch* | HRESULT |
| `get Parent` | [out] pParent: IDispatch* | HRESULT |

### <a name="_isensorauto"></a>`_ISensorAuto`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `get SensorType` | [out] SensorType: SensorTypeConstants* | HRESULT |
| `get Status` | [out] Status: SensorStatusConstants* | HRESULT |
| `get IsInRange` | [out] IsInRange: bool* | HRESULT |
| `get CurrentValue` | [out] CurrentValue: double* | HRESULT |
| `put Name` | Name: BSTR | HRESULT |
| `get Name` | [out] Name: BSTR* | HRESULT |
| `put Description` | Description: BSTR | HRESULT |
| `get Description` | [out] Description: BSTR* | HRESULT |
| `put LowerRange` | LowerRange: double | HRESULT |
| `get LowerRange` | [out] LowerRange: double* | HRESULT |
| `put UpperRange` | UpperRange: double | HRESULT |
| `get UpperRange` | [out] UpperRange: double* | HRESULT |
| `put Operator` | Operator: SensorOperatorConstants | HRESULT |
| `get Operator` | [out] Operator: SensorOperatorConstants* | HRESULT |
| `put MinimumThreshold` | MinimumThreshold: double | HRESULT |
| `get MinimumThreshold` | [out] MinimumThreshold: double* | HRESULT |
| `put MaximumThreshold` | MaximumThreshold: double | HRESULT |
| `get MaximumThreshold` | [out] MaximumThreshold: double* | HRESULT |
| `put DisplayType` | DisplayType: SensorDisplayTypeConstants | HRESULT |
| `get DisplayType` | [out] DisplayType: SensorDisplayTypeConstants* | HRESULT |
| `put UpdateMechanism` | UpdateMechanism: SensorUpdateMechanismConstants | HRESULT |
| `get UpdateMechanism` | [out] UpdateMechanism: SensorUpdateMechanismConstants* | HRESULT |
| `Update` | — | HRESULT |
| `Delete` | — | HRESULT |
| `get AttributeSets` | [out] AttributeSets: IDispatch* | HRESULT |
| `get IsAttributeSetPresent` | Name: BSTR, [out] IsAttributeSetPresent: bool* | HRESULT |

### <a name="_isensorsauto"></a>`_ISensorsAuto`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `get Count` | [out] Count: int* | HRESULT |
| `Item` | Index: VARIANT, [out] Item: IDispatch* | HRESULT |
| `get _NewEnum` | [out] Enum: IUnknown* | HRESULT |
| `get Application` | [out] Application: IDispatch* | HRESULT |
| `get Parent` | [out] Parent: IDispatch* | HRESULT |
| `AddVariableSensor` | variable: IDispatch, Name: BSTR, Description: BSTR, LowerRange: double, UpperRange: double, MinimumThreshold: double, MaximumThreshold: double, Operator: SensorOperatorConstants, DisplayType: SensorDisplayTypeConstants, UpdateMechanism: SensorUpdateMechanismConstants, [out] Sensor: IDispatch* | HRESULT |
| `AddMinimumDistanceSensor` | Element1: IDispatch, Element2: IDispatch, Name: BSTR, Description: BSTR, LowerRange: double, UpperRange: double, MinimumThreshold: double, MaximumThreshold: double, Operator: SensorOperatorConstants, DisplayType: SensorDisplayTypeConstants, UpdateMechanism: SensorUpdateMechanismConstants, [out] Sensor: IDispatch* | HRESULT |
| `AddSurfaceAreaSensor` | iSensorType: SurfaceAreaSensorAreaTypeConstants, iSelectionType: SurfaceAreaSensorSelectionTypeConstants, Name: BSTR, Description: BSTR, LowerRange: double, UpperRange: double, MinimumThreshold: double, MaximumThreshold: double, Operator: SensorOperatorConstants, DisplayType: SensorDisplayTypeConstants, UpdateMechanism: SensorUpdateMechanismConstants, Element: VARIANT*, [out] Sensor: IDispatch* | HRESULT |

### <a name="_isheetmetalsensorsauto"></a>`_ISheetMetalSensorsAuto`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `get Count` | [out] Count: int* | HRESULT |
| `Item` | Index: VARIANT, [out] Item: IDispatch* | HRESULT |
| `get _NewEnum` | [out] Enum: IUnknown* | HRESULT |
| `get Application` | [out] Application: IDispatch* | HRESULT |
| `get Parent` | [out] Parent: IDispatch* | HRESULT |
| `AddVariableSensor` | variable: IDispatch, Name: BSTR, Description: BSTR, LowerRange: double, UpperRange: double, MinimumThreshold: double, MaximumThreshold: double, Operator: SensorOperatorConstants, DisplayType: SensorDisplayTypeConstants, UpdateMechanism: SensorUpdateMechanismConstants, [out] Sensor: IDispatch* | HRESULT |
| `AddMinimumDistanceSensor` | Element1: IDispatch, Element2: IDispatch, Name: BSTR, Description: BSTR, LowerRange: double, UpperRange: double, MinimumThreshold: double, MaximumThreshold: double, Operator: SensorOperatorConstants, DisplayType: SensorDisplayTypeConstants, UpdateMechanism: SensorUpdateMechanismConstants, [out] Sensor: IDispatch* | HRESULT |
| `AddSheetMetalCheckerSensor` | LeftFeatureType: SheetMetalSensorFeatureTypeConstants, RightFeatureType: SheetMetalSensorFeatureTypeConstants, Name: BSTR, Description: BSTR, Threshold: double, UpdateMechanism: SensorUpdateMechanismConstants, [opt]Element: VARIANT*, [out] Sensor: IDispatch* | HRESULT |
| `AddSurfaceAreaSensor` | iSensorType: SurfaceAreaSensorAreaTypeConstants, iSelectionType: SurfaceAreaSensorSelectionTypeConstants, Name: BSTR, Description: BSTR, LowerRange: double, UpperRange: double, MinimumThreshold: double, MaximumThreshold: double, Operator: SensorOperatorConstants, DisplayType: SensorDisplayTypeConstants, UpdateMechanism: SensorUpdateMechanismConstants, Element: VARIANT*, [out] Sensor: IDispatch* | HRESULT |

### <a name="_isectionviewauto"></a>`_ISectionViewAuto`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `put Caption` | Caption: BSTR | HRESULT |
| `get Caption` | [out] Caption: BSTR* | HRESULT |
| `Show` | bShowSectionView: bool | HRESULT |
| `Delete` | — | HRESULT |
| `put Name` | Name: BSTR | HRESULT |
| `get Name` | [out] Name: BSTR* | HRESULT |
| `put Style` | Style: BSTR | HRESULT |
| `get Style` | [out] Style: BSTR* | HRESULT |
| `put CuttingPlaneColor` | PlaneColor: int | HRESULT |
| `get CuttingPlaneColor` | [out] PlaneColor: int* | HRESULT |
| `put CuttingPlaneEdgeColor` | EdgeColor: int | HRESULT |
| `get CuttingPlaneEdgeColor` | [out] EdgeColor: int* | HRESULT |
| `put Opacity` | pdOpacity: double | HRESULT |
| `get Opacity` | [out] pdOpacity: double* | HRESULT |
| `put ThroughAllExtent` | pdExtent: double | HRESULT |
| `get ThroughAllExtent` | [out] pdExtent: double* | HRESULT |
| `put CutHardware` | pbCutHardware: int | HRESULT |
| `get CutHardware` | [out] pbCutHardware: int* | HRESULT |
| `put SectionDisplayMode` | val: PMISectionDisplayModeConstants | HRESULT |
| `get SectionDisplayMode` | [out] val: PMISectionDisplayModeConstants* | HRESULT |
| `put ShowCuttingPlane` | pbShowCuttingPlane: int | HRESULT |
| `get ShowCuttingPlane` | [out] pbShowCuttingPlane: int* | HRESULT |
| `AddToModelView` | ModelView: IUnknown | HRESULT |
| `RemoveFromModelView` | ModelView: IUnknown | HRESULT |
| `EditByPlane` | nNumPlanes: int, pPlanes: SAFEARRAY(IDispatch)*, PlaneCutDirections: SAFEARRAY(int)*, eExtentType: SectionViewPlaneExtentTypeConstant, bCutHardwareParts: int | HRESULT |
| `put PlaneExtentType` | peExtentType: SectionViewPlaneExtentTypeConstant | HRESULT |
| `get PlaneExtentType` | [out] peExtentType: SectionViewPlaneExtentTypeConstant* | HRESULT |
| `get AttributeSets` | [out] AttributeSets: IDispatch* | HRESULT |
| `get IsAttributeSetPresent` | Name: BSTR, [out] IsAttributeSetPresent: bool* | HRESULT |
| `EditByPlaneEx` | nNumPlanes: int, pPlanes: SAFEARRAY(IDispatch)*, PlaneCutDirections: SAFEARRAY(int)*, SectionViewPlaneTypes: SAFEARRAY(SectionViewPlaneType)*, eExtentType: SectionViewPlaneExtentTypeConstant, bCutHardwareParts: bool | HRESULT |

### <a name="_isectionviewsauto"></a>`_ISectionViewsAuto`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `get Count` | [out] Count: int* | HRESULT |
| `Item` | Index: VARIANT, [out] Item: IDispatch* | HRESULT |
| `get _NewEnum` | [out] Enum: IUnknown* | HRESULT |
| `get Application` | [out] Application: IDispatch* | HRESULT |
| `get Parent` | [out] Parent: IDispatch* | HRESULT |
| `Add` | nNumProfiles: int, pProfiles: SAFEARRAY(IUnknown)*, szCaption: BSTR, dExtent: double, eExtentSide: SectionViewExtentSide, eProfileSide: SectionViewProfileSide, bCutHardwareParts: int, [out] SectionView: IDispatch* | HRESULT |
| `AddByPlane` | nNumPlanes: int, pPlanes: SAFEARRAY(IDispatch)*, PlaneCutDirections: SAFEARRAY(int)*, eExtentType: SectionViewPlaneExtentTypeConstant, szCaption: BSTR, bCutHardwareParts: int, [out] SectionView: IDispatch* | HRESULT |
| `AddByPlaneEx` | nNumPlanes: int, pPlanes: SAFEARRAY(IDispatch)*, PlaneCutDirections: SAFEARRAY(int)*, SectionViewPlaneTypes: SAFEARRAY(SectionViewPlaneType)*, eExtentType: SectionViewPlaneExtentTypeConstant, szCaption: BSTR, bCutHardwareParts: bool, [out] SectionView: IDispatch* | HRESULT |

### <a name="_ilayerauto"></a>`_ILayerAuto`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `get Application` | [out] pApplication: Application** | HRESULT |
| `get Parent` | [out] Parent: IDispatch* | HRESULT |
| `Delete` | — | HRESULT |
| `get IsEmpty` | [out] Empty: bool* | HRESULT |
| `get Key` | [out] Key: BSTR* | HRESULT |
| `put Name` | Name: BSTR | HRESULT |
| `get Name` | [out] Name: BSTR* | HRESULT |
| `put Description` | Description: BSTR | HRESULT |
| `get Description` | [out] Description: BSTR* | HRESULT |
| `Activate` | — | HRESULT |
| `get Show` | [out] Value: bool* | HRESULT |
| `put Show` | Value: bool | HRESULT |
| `get Locatable` | [out] Value: bool* | HRESULT |
| `put Locatable` | Value: bool | HRESULT |
| `ShowInContext` | Context: IDispatch | HRESULT |
| `HideInContext` | Context: IDispatch | HRESULT |
| `MakeLocatableInContext` | Context: IDispatch | HRESULT |
| `MakeNonLocatableInContext` | Context: IDispatch | HRESULT |
| `ActivateInContext` | Context: IDispatch | HRESULT |
| `IsShownInContext` | Context: IDispatch, [out] Value: bool* | HRESULT |
| `IsLocatableInContext` | Context: IDispatch, [out] Value: bool* | HRESULT |
| `ShowOnly` | — | HRESULT |
| `ShowOnlyInContext` | Context: IDispatch | HRESULT |
| `DeleteLayerAndObjects` | — | HRESULT |
| `ShowEverywhere` | — | HRESULT |
| `HideEverywhere` | — | HRESULT |
| `MoveAllObjectsToLayer` | NewLayerDispatch: IDispatch | HRESULT |
| `get AttributeSets` | [out] AttributeSets: IDispatch* | HRESULT |
| `get IsAttributeSetPresent` | Name: BSTR, [out] IsAttributeSetPresent: bool* | HRESULT |

### <a name="_ilayersauto"></a>`_ILayersAuto`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `Item` | Index: VARIANT, [out] Layer: Layer** | HRESULT |
| `get Application` | [out] pApplication: Application** | HRESULT |
| `get Parent` | [out] Parent: IDispatch* | HRESULT |
| `get Count` | [out] Count: int* | HRESULT |
| `Add` | Name: BSTR, [out] Layer: Layer** | HRESULT |
| `get ActiveLayer` | [out] Layer: Layer** | HRESULT |
| `get _NewEnum` | [out] Unknown: IUnknown* | HRESULT |

### <a name="_ilinearstyleauto"></a>`_ILinearStyleAuto`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `put Name` | Name: BSTR | HRESULT |
| `get Name` | [out] Name: BSTR* | HRESULT |
| `put Parent` | Name: BSTR | HRESULT |
| `get Parent` | [out] Name: BSTR* | HRESULT |
| `get Description` | [out] Description: BSTR* | HRESULT |
| `put Units` | Units: StyleUnitsConstant | HRESULT |
| `get Units` | [out] Units: StyleUnitsConstant* | HRESULT |
| `put Color` | Color: int | HRESULT |
| `get Color` | [out] Color: int* | HRESULT |
| `put Width` | Width: double | HRESULT |
| `get Width` | [out] Width: double* | HRESULT |
| `SetDashGap` | nCount: int, dDashGap: SAFEARRAY(double)*, fAutoPhase: bool | HRESULT |
| `get DashGapCount` | [out] pnCount: int* | HRESULT |
| `GetDashGap` | [out] pnCount: int*, [out] dDashGap: SAFEARRAY(double)*, [out] pfAutoPhase: bool* | HRESULT |
| `put DashType` | Name: BSTR | HRESULT |
| `get DashType` | [out] Name: BSTR* | HRESULT |
| `get AttributeSets` | [out] AttributeSets: IDispatch* | HRESULT |
| `get IsAttributeSetPresent` | Name: BSTR, [out] IsAttributeSetPresent: bool* | HRESULT |

### <a name="_ifillstyleauto"></a>`_IFillStyleAuto`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `put Name` | Name: BSTR | HRESULT |
| `get Name` | [out] Name: BSTR* | HRESULT |
| `get Description` | [out] Description: BSTR* | HRESULT |
| `put PatternName` | Name: BSTR | HRESULT |
| `get PatternName` | [out] Name: BSTR* | HRESULT |
| `get PatternType` | [out] patternCode: int* | HRESULT |
| `put Color` | Color: int | HRESULT |
| `get Color` | [out] Color: int* | HRESULT |
| `put FillBackground` | flag: int | HRESULT |
| `get FillBackground` | [out] flag: int* | HRESULT |
| `put FillColor` | Color: int | HRESULT |
| `get FillColor` | [out] Color: int* | HRESULT |
| `put Rotation` | Angle: double | HRESULT |
| `get Rotation` | [out] Angle: double* | HRESULT |
| `put Spacing` | Spacing: double | HRESULT |
| `get Spacing` | [out] Spacing: double* | HRESULT |
| `put Scale` | __MIDL___IFillStyleAuto0001: double | HRESULT |
| `get Scale` | [out] __MIDL___IFillStyleAuto0001: double* | HRESULT |
| `put Units` | Units: int | HRESULT |
| `get Units` | [out] Units: int* | HRESULT |
| `put Parent` | Name: BSTR | HRESULT |
| `get Parent` | [out] Name: BSTR* | HRESULT |
| `put Active` | Name: BSTR | HRESULT |
| `get Active` | [out] Name: BSTR* | HRESULT |

### <a name="_ihatchpatternstyleauto"></a>`_IHatchPatternStyleAuto`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `put Name` | lpName: BSTR | HRESULT |
| `get Name` | [out] lpName: BSTR* | HRESULT |
| `put Parent` | lpName: BSTR | HRESULT |
| `get Parent` | [out] lpName: BSTR* | HRESULT |
| `put Units` | lpUnits: int | HRESULT |
| `get Units` | [out] lpUnits: int* | HRESULT |
| `get Count` | [out] lpnCount: int* | HRESULT |
| `AddHatch` | dRotation: double, dXOrigin: double, dYOrigin: double, dSpacing: double, dShift: double, nColor: int, dWidth: double, DashTypeName: BSTR, [out] lpnDisplayIndex: int* | HRESULT |
| `GetHatch` | nDisplayIndex: int, [out] lpdRotation: double*, [out] lpdXOrigin: double*, [out] lpdYOrigin: double*, [out] lpdSpacing: double*, [out] lpdShift: double*, [out] lpnColor: int*, [out] lpdWidth: double*, [out] DashTypeName: BSTR* | HRESULT |
| `SetHatch` | nDisplayIndex: int, dRotation: double, dXOrigin: double, dYOrigin: double, dSpacing: double, dShift: double, nColor: int, dWidth: double, DashTypeName: BSTR | HRESULT |
| `RemoveHatch` | nDisplayIndex: int | HRESULT |
| `SetRotation` | nDisplayIndex: int, dRotation: double | HRESULT |
| `GetRotation` | nDisplayIndex: int, [out] lpdRotation: double* | HRESULT |
| `SetOrigin` | nDisplayIndex: int, dX: double, dY: double | HRESULT |
| `GetOrigin` | nDisplayIndex: int, [out] lpdX: double*, [out] lpdY: double* | HRESULT |
| `SetSpacing` | nDisplayIndex: int, dSpacing: double | HRESULT |
| `GetSpacing` | nDisplayIndex: int, [out] lpdSpacing: double* | HRESULT |
| `SetShift` | nDisplayIndex: int, dShift: double | HRESULT |
| `GetShift` | nDisplayIndex: int, [out] lpdShift: double* | HRESULT |
| `SetColor` | nDisplayIndex: int, nColor: int | HRESULT |
| `GetColor` | nDisplayIndex: int, [out] lpnColor: int* | HRESULT |
| `SetWidth` | nDisplayIndex: int, dWidth: double | HRESULT |
| `GetWidth` | nDisplayIndex: int, [out] lpdWidth: double* | HRESULT |
| `SetDashType` | nDisplayIndex: int, DashTypeName: BSTR | HRESULT |
| `GetDashType` | nDisplayIndex: int, [out] lpDashTypeName: BSTR* | HRESULT |
| `SetDisplayIndex` | nCurrentIndex: int, nNewIndex: int | HRESULT |
| `SetDashGap` | nDisplayIndex: int, nCount: int, dDashGap: SAFEARRAY(double)* | HRESULT |
| `get DashGapCount` | nDisplayIndex: int, [out] pnCount: int* | HRESULT |
| `GetDashGap` | nDisplayIndex: int, [out] pnCount: int*, [out] dDashGap: SAFEARRAY(double)* | HRESULT |
| `get AttributeSets` | [out] AttributeSets: IDispatch* | HRESULT |
| `get IsAttributeSetPresent` | Name: BSTR, [out] IsAttributeSetPresent: bool* | HRESULT |
| `put MasterRotation` | pdRotation: double | HRESULT |
| `get MasterRotation` | [out] pdRotation: double* | HRESULT |
| `put MasterScale` | pdScale: double | HRESULT |
| `get MasterScale` | [out] pdScale: double* | HRESULT |
| `SetMasterColor` | nColor: int | HRESULT |
| `SetMasterWidth` | dWidth: double | HRESULT |
| `AddHatchWithOption` | dRotation: double, dXOrigin: double, dYOrigin: double, dSpacing: double, dShift: double, nColor: int, dWidth: double, DashTypeName: BSTR, elementType: HatchElementType, ellipseCenterLocation: RadialHatchElementCenterLocation, dEllipseAxisRatio: double, [out] lpnDisplayIndex: int* | HRESULT |
| `GetHatchWithOption` | nDisplayIndex: int, [out] lpdRotation: double*, [out] lpdXOrigin: double*, [out] lpdYOrigin: double*, [out] lpdSpacing: double*, [out] lpdShift: double*, [out] lpnColor: int*, [out] lpdWidth: double*, [out] DashTypeName: BSTR*, [out] elementType: HatchElementType*, [out] ellipseCenterLocation: RadialHatchElementCenterLocation*, [out] dEllipseAxisRatio: double* | HRESULT |
| `SetHatchWithOption` | nDisplayIndex: int, dRotation: double, dXOrigin: double, dYOrigin: double, dSpacing: double, dShift: double, nColor: int, dWidth: double, DashTypeName: BSTR, elementType: HatchElementType, ellipseCenterLocation: RadialHatchElementCenterLocation, dEllipseAxisRatio: double | HRESULT |
| `SetElementType` | nDisplayIndex: int, elementType: HatchElementType | HRESULT |
| `GetElementType` | nDisplayIndex: int, [out] pElementType: HatchElementType* | HRESULT |
| `SetRadialElementCenterLocation` | nDisplayIndex: int, ellipseCenterLocation: RadialHatchElementCenterLocation | HRESULT |
| `GetRadialElementCenterLocation` | nDisplayIndex: int, [out] pEllipseCenterLocation: RadialHatchElementCenterLocation* | HRESULT |
| `SetRadialElementAxisRatio` | nDisplayIndex: int, dEllipseAxisRatio: double | HRESULT |
| `GetRadialElementAxisRatio` | nDisplayIndex: int, [out] pdEllipseAxisRatio: double* | HRESULT |
| `SetSourceColor` | nColor: int | HRESULT |
| `SetSourceWidth` | dWidth: double | HRESULT |
| `put SourceRotation` | pdRotation: double | HRESULT |
| `get SourceRotation` | [out] pdRotation: double* | HRESULT |
| `put SourceScale` | pdScale: double | HRESULT |
| `get SourceScale` | [out] pdScale: double* | HRESULT |

### <a name="_ilinearstylesauto"></a>`_ILinearStylesAuto`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `get Count` | [out] Count: int* | HRESULT |
| `get Parent` | [out] pParent: IDispatch* | HRESULT |
| `get Application` | [out] pApp: IDispatch* | HRESULT |
| `Item` | Index: VARIANT, [out] pItem: LinearStyle** | HRESULT |
| `get _NewEnum` | [out] pEnum: IUnknown* | HRESULT |
| `Add` | Name: BSTR, Parent: BSTR, [out] GeoStyle: LinearStyle** | HRESULT |
| `Remove` | Name: BSTR | HRESULT |
| `get Active` | [out] Name: BSTR* | HRESULT |
| `put Active` | Name: BSTR | HRESULT |

### <a name="_ifillstylesauto"></a>`_IFillStylesAuto`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `get Count` | [out] Count: int* | HRESULT |
| `get Parent` | [out] pParent: IDispatch* | HRESULT |
| `get Application` | [out] pApp: IDispatch* | HRESULT |
| `Item` | Index: VARIANT, [out] pItem: FillStyle** | HRESULT |
| `get _NewEnum` | [out] pEnum: IUnknown* | HRESULT |
| `Add` | Name: BSTR, Parent: BSTR, [out] hFillStyle: FillStyle** | HRESULT |
| `Remove` | Name: BSTR | HRESULT |
| `put Active` | Name: BSTR | HRESULT |
| `get Active` | [out] Name: BSTR* | HRESULT |

### <a name="_ihatchpatternstylesauto"></a>`_IHatchPatternStylesAuto`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `get Count` | [out] Count: int* | HRESULT |
| `get Parent` | [out] pParent: IDispatch* | HRESULT |
| `get Application` | [out] pApp: IDispatch* | HRESULT |
| `Item` | Index: VARIANT, [out] pItem: HatchPatternStyle** | HRESULT |
| `get _NewEnum` | [out] pEnum: IUnknown* | HRESULT |
| `Add` | Name: BSTR, Parent: BSTR, [out] hHatchPatternStyle: HatchPatternStyle** | HRESULT |
| `Remove` | Name: BSTR | HRESULT |

### <a name="_idashstyleauto"></a>`_IDashStyleAuto`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `put Name` | lpName: BSTR | HRESULT |
| `get Name` | [out] lpName: BSTR* | HRESULT |
| `put Units` | lpUnits: int | HRESULT |
| `get Units` | [out] lpUnits: int* | HRESULT |
| `get DashGapCount` | [out] lpnCount: int* | HRESULT |
| `SetDashGap` | nCount: int, dDashGap: SAFEARRAY(double)* | HRESULT |
| `GetDashGap` | [out] pnCount: int*, [out] dDashGap: SAFEARRAY(double)* | HRESULT |
| `put Center` | pvbCenter: bool | HRESULT |
| `get Center` | [out] pvbCenter: bool* | HRESULT |
| `put PercentStartEndDash` | pdPercentStartEndDash: double | HRESULT |
| `get PercentStartEndDash` | [out] pdPercentStartEndDash: double* | HRESULT |
| `get AttributeSets` | [out] AttributeSets: IDispatch* | HRESULT |
| `get IsAttributeSetPresent` | Name: BSTR, [out] IsAttributeSetPresent: bool* | HRESULT |

### <a name="_idashstylesauto"></a>`_IDashStylesAuto`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `get Count` | [out] Count: int* | HRESULT |
| `get Parent` | [out] pParent: IDispatch* | HRESULT |
| `get Application` | [out] pApp: IDispatch* | HRESULT |
| `Item` | Index: VARIANT, [out] pItem: DashStyle** | HRESULT |
| `get _NewEnum` | [out] pEnum: IUnknown* | HRESULT |
| `Add` | Name: BSTR, [out] hDashStyle: DashStyle** | HRESULT |
| `Remove` | Name: BSTR | HRESULT |

### <a name="_ifacestyleauto"></a>`_IFaceStyleAuto`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `get StyleName` | [out] psStyleName: BSTR* | HRESULT |
| `put StyleName` | psStyleName: BSTR | HRESULT |
| `get Parent` | [out] psParentName: BSTR* | HRESULT |
| `put Parent` | psParentName: BSTR | HRESULT |
| `get Type` | [out] plType: int* | HRESULT |
| `put Type` | plType: int | HRESULT |
| `get Flags` | [out] plFlags: int* | HRESULT |
| `put Flags` | plFlags: int | HRESULT |
| `get WireframeColorRed` | [out] pfWireframeColorRed: float* | HRESULT |
| `put WireframeColorRed` | pfWireframeColorRed: float | HRESULT |
| `get WireframeColorGreen` | [out] pfWireframeColorGreen: float* | HRESULT |
| `put WireframeColorGreen` | pfWireframeColorGreen: float | HRESULT |
| `get WireframeColorBlue` | [out] pfWireframeColorBlue: float* | HRESULT |
| `put WireframeColorBlue` | pfWireframeColorBlue: float | HRESULT |
| `get StipplePattern` | [out] plStipplePattern: int* | HRESULT |
| `put StipplePattern` | plStipplePattern: int | HRESULT |
| `get StippleScale` | [out] psStippleScale: short* | HRESULT |
| `put StippleScale` | psStippleScale: short | HRESULT |
| `get LineWidth` | [out] pfLineWidth: float* | HRESULT |
| `put LineWidth` | pfLineWidth: float | HRESULT |
| `get WidthSpace` | [out] psWidthSpace: short* | HRESULT |
| `put WidthSpace` | psWidthSpace: short | HRESULT |
| `get DiffuseRed` | [out] pfDiffuseRed: float* | HRESULT |
| `put DiffuseRed` | pfDiffuseRed: float | HRESULT |
| `get DiffuseGreen` | [out] pfDiffuseGreen: float* | HRESULT |
| `put DiffuseGreen` | pfDiffuseGreen: float | HRESULT |
| `get DiffuseBlue` | [out] pfDiffuseBlue: float* | HRESULT |
| `put DiffuseBlue` | pfDiffuseBlue: float | HRESULT |
| `get SpecularRed` | [out] pfSpecularRed: float* | HRESULT |
| `put SpecularRed` | pfSpecularRed: float | HRESULT |
| `get SpecularGreen` | [out] pfSpecularGreen: float* | HRESULT |
| `put SpecularGreen` | pfSpecularGreen: float | HRESULT |
| `get SpecularBlue` | [out] pfSpecularBlue: float* | HRESULT |
| `put SpecularBlue` | pfSpecularBlue: float | HRESULT |
| `get AmbientRed` | [out] pfAmbientRed: float* | HRESULT |
| `put AmbientRed` | pfAmbientRed: float | HRESULT |
| `get AmbientGreen` | [out] pfAmbientGreen: float* | HRESULT |
| `put AmbientGreen` | pfAmbientGreen: float | HRESULT |
| `get AmbientBlue` | [out] pfAmbientBlue: float* | HRESULT |
| `put AmbientBlue` | pfAmbientBlue: float | HRESULT |
| `get EmissionRed` | [out] pfEmissionRed: float* | HRESULT |
| `put EmissionRed` | pfEmissionRed: float | HRESULT |
| `get EmissionGreen` | [out] pfEmissionGreen: float* | HRESULT |
| `put EmissionGreen` | pfEmissionGreen: float | HRESULT |
| `get EmissionBlue` | [out] pfEmissionBlue: float* | HRESULT |
| `put EmissionBlue` | pfEmissionBlue: float | HRESULT |
| `get Shininess` | [out] pfShininess: float* | HRESULT |
| `put Shininess` | pfShininess: float | HRESULT |
| `get Opacity` | [out] pfOpacity: float* | HRESULT |
| `put Opacity` | pfOpacity: float | HRESULT |
| `get Reflectivity` | [out] pfReflectivity: float* | HRESULT |
| `put Reflectivity` | pfReflectivity: float | HRESULT |
| `get Refraction` | [out] pfRefraction: float* | HRESULT |
| `put Refraction` | pfRefraction: float | HRESULT |
| `get CastsShadows` | [out] pbCastsShadows: int* | HRESULT |
| `put CastsShadows` | pbCastsShadows: int | HRESULT |
| `get AcceptsShadows` | [out] pbAcceptsShadows: int* | HRESULT |
| `put AcceptsShadows` | pbAcceptsShadows: int | HRESULT |
| `get TextureFileName` | [out] psTextureFileName: BSTR* | HRESULT |
| `put TextureFileName` | psTextureFileName: BSTR | HRESULT |
| `get TextureTransparent` | [out] pbTextureTransparent: int* | HRESULT |
| `put TextureTransparent` | pbTextureTransparent: int | HRESULT |
| `get TextureTransparentColorRed` | [out] pfRed: float* | HRESULT |
| `put TextureTransparentColorRed` | pfRed: float | HRESULT |
| `get TextureTransparentColorGreen` | [out] pfGreen: float* | HRESULT |
| `put TextureTransparentColorGreen` | pfGreen: float | HRESULT |
| `get TextureTransparentColorBlue` | [out] pfBlue: float* | HRESULT |
| `put TextureTransparentColorBlue` | pfBlue: float | HRESULT |
| `get TextureUnits` | [out] pnUnits: int* | HRESULT |
| `put TextureUnits` | pnUnits: int | HRESULT |
| `get TextureScaleX` | [out] pfScaleX: float* | HRESULT |
| `put TextureScaleX` | pfScaleX: float | HRESULT |
| `get TextureScaleY` | [out] pfScaleY: float* | HRESULT |
| `put TextureScaleY` | pfScaleY: float | HRESULT |
| `get TextureOffsetX` | [out] pfOffsetX: float* | HRESULT |
| `put TextureOffsetX` | pfOffsetX: float | HRESULT |
| `get TextureOffsetY` | [out] pfOffsetY: float* | HRESULT |
| `put TextureOffsetY` | pfOffsetY: float | HRESULT |
| `get TextureMirrorX` | [out] pbMirrorX: int* | HRESULT |
| `put TextureMirrorX` | pbMirrorX: int | HRESULT |
| `get TextureMirrorY` | [out] pbMirrorY: int* | HRESULT |
| `put TextureMirrorY` | pbMirrorY: int | HRESULT |
| `get TextureRotation` | [out] pfRotation: float* | HRESULT |
| `put TextureRotation` | pfRotation: float | HRESULT |
| `get TextureWeight` | [out] pfWeight: float* | HRESULT |
| `put TextureWeight` | pfWeight: float | HRESULT |
| `get BumpmapFileName` | [out] psBumpmapFileName: BSTR* | HRESULT |
| `put BumpmapFileName` | psBumpmapFileName: BSTR | HRESULT |
| `get BumpmapUnits` | [out] pnUnits: int* | HRESULT |
| `put BumpmapUnits` | pnUnits: int | HRESULT |
| `get BumpmapScaleX` | [out] pfScaleX: float* | HRESULT |
| `put BumpmapScaleX` | pfScaleX: float | HRESULT |
| `get BumpmapScaleY` | [out] pfScaleY: float* | HRESULT |
| `put BumpmapScaleY` | pfScaleY: float | HRESULT |
| `get BumpmapOffsetX` | [out] pfOffsetX: float* | HRESULT |
| `put BumpmapOffsetX` | pfOffsetX: float | HRESULT |
| `get BumpmapOffsetY` | [out] pfOffsetY: float* | HRESULT |
| `put BumpmapOffsetY` | pfOffsetY: float | HRESULT |
| `get BumpmapMirrorX` | [out] pbMirrorX: int* | HRESULT |
| `put BumpmapMirrorX` | pbMirrorX: int | HRESULT |
| `get BumpmapMirrorY` | [out] pbMirrorY: int* | HRESULT |
| `put BumpmapMirrorY` | pbMirrorY: int | HRESULT |
| `get BumpmapRotation` | [out] pfRotation: float* | HRESULT |
| `put BumpmapRotation` | pfRotation: float | HRESULT |
| `get BumpmapHeight` | [out] pfHeight: float* | HRESULT |
| `put BumpmapHeight` | pfHeight: float | HRESULT |
| `get BumpmapInvert` | [out] pbInvert: int* | HRESULT |
| `put BumpmapInvert` | pbInvert: int | HRESULT |
| `get SkyboxType` | [out] peType: SeSkyboxType* | HRESULT |
| `put SkyboxType` | peType: SeSkyboxType | HRESULT |
| `get SkyboxAzimuth` | [out] pfAzimuth: float* | HRESULT |
| `put SkyboxAzimuth` | pfAzimuth: float | HRESULT |
| `get SkyboxAltitude` | [out] pfAltitude: float* | HRESULT |
| `put SkyboxAltitude` | pfAltitude: float | HRESULT |
| `get SkyboxRoll` | [out] pfRoll: float* | HRESULT |
| `put SkyboxRoll` | pfRoll: float | HRESULT |
| `get SkyboxConeAngle` | [out] pfConeAngle: float* | HRESULT |
| `put SkyboxConeAngle` | pfConeAngle: float | HRESULT |
| `get StyleID` | [out] plStyleID: int* | HRESULT |
| `BeginPropertyBuffer` | — | HRESULT |
| `FlushPropertyBuffer` | — | HRESULT |
| `HasWireframeProperties` | [out] pbResult: int* | HRESULT |
| `HasSurfaceProperties` | [out] pbResult: int* | HRESULT |
| `ClearWireframeProperties` | — | HRESULT |
| `ClearSurfaceProperties` | — | HRESULT |
| `GetWireframeColor` | [out] pfRed: float*, [out] pfGreen: float*, [out] pfBlue: float* | HRESULT |
| `SetWireframeColor` | fRed: float, fGreen: float, fBlue: float | HRESULT |
| `GetDiffuse` | [out] pfRed: float*, [out] pfGreen: float*, [out] pfBlue: float* | HRESULT |
| `SetDiffuse` | fRed: float, fGreen: float, fBlue: float | HRESULT |
| `GetSpecular` | [out] pfRed: float*, [out] pfGreen: float*, [out] pfBlue: float* | HRESULT |
| `SetSpecular` | fRed: float, fGreen: float, fBlue: float | HRESULT |
| `GetAmbient` | [out] pfRed: float*, [out] pfGreen: float*, [out] pfBlue: float* | HRESULT |
| `SetAmbient` | fRed: float, fGreen: float, fBlue: float | HRESULT |
| `GetEmission` | [out] pfRed: float*, [out] pfGreen: float*, [out] pfBlue: float* | HRESULT |
| `SetEmission` | fRed: float, fGreen: float, fBlue: float | HRESULT |
| `Delete` | — | HRESULT |
| `Detach` | — | HRESULT |
| `GetTextureTransparentColor` | [out] pfRed: float*, [out] pfGreen: float*, [out] pfBlue: float* | HRESULT |
| `SetTextureTransparentColor` | fRed: float, fGreen: float, fBlue: float | HRESULT |
| `GetTextureScale` | [out] pfXScale: float*, [out] pfYScale: float* | HRESULT |
| `SetTextureScale` | fXScale: float, fYScale: float | HRESULT |
| `GetTextureOffset` | [out] pfXOffset: float*, [out] pfYOffset: float* | HRESULT |
| `SetTextureOffset` | fXOffset: float, fYOffset: float | HRESULT |
| `GetBumpmapScale` | [out] pfXScale: float*, [out] pfYScale: float* | HRESULT |
| `SetBumpmapScale` | fXScale: float, fYScale: float | HRESULT |
| `GetBumpmapOffset` | [out] pfXOffset: float*, [out] pfYOffset: float* | HRESULT |
| `SetBumpmapOffset` | fXOffset: float, fYOffset: float | HRESULT |
| `SetSkyboxSkyFile` | sFilename: BSTR | HRESULT |
| `SetSkyboxSideFilename` | nSide: int, sFilename: BSTR | HRESULT |
| `GetSkyboxSideFilename` | nSide: int, [out] psFilename: BSTR* | HRESULT |
| `SkyboxClear` | nSide: int | HRESULT |
| `SkyboxClearAll` | — | HRESULT |
| `GetSkyboxOrientation` | [out] pfxDirection: float*, [out] pfyDirection: float*, [out] pfzDirection: float*, [out] pfxUp: float*, [out] pfyUp: float*, [out] pfzUp: float*, [out] pfFieldOfView: float* | HRESULT |
| `SetSkyboxOrientation` | fxDirection: float, fyDirection: float, fzDirection: float, fxUp: float, fyUp: float, fzUp: float, fFieldOfView: float | HRESULT |
| `GetVersion` | eVersion: int, [out] pnVersion: int* | HRESULT |
| `SetVersion` | eVersion: int, nVersion: int | HRESULT |
| `GetShaderData` | [out] pnId: int*, [out] peType: int*, [out] pnHints: int* | HRESULT |
| `SetShaderData` | eType: int, nHints: int | HRESULT |
| `get AutomaticShaderType` | [out] peType: int* | HRESULT |
| `get ShaderType` | [out] peType: int* | HRESULT |
| `put ShaderType` | peType: int | HRESULT |
| `get AttributeSets` | [out] AttributeSets: IDispatch* | HRESULT |
| `get IsAttributeSetPresent` | Name: BSTR, [out] IsAttributeSetPresent: bool* | HRESULT |
| `ResetSkyboxOrientation` | — | HRESULT |
| `DeleteSkybox` | — | HRESULT |
| `get RenderModeType` | [out] peType: SeRenderModeType* | HRESULT |
| `put RenderModeType` | peType: SeRenderModeType | HRESULT |
| `HasPointProperties` | [out] pbResult: int* | HRESULT |
| `ClearPointProperties` | — | HRESULT |
| `get PointSize` | [out] pfSize: float* | HRESULT |
| `put PointSize` | pfSize: float | HRESULT |
| `get PointSizeSpace` | [out] peSpace: SeRenderSpaceType* | HRESULT |
| `put PointSizeSpace` | peSpace: SeRenderSpaceType | HRESULT |
| `GetPointOptions` | [out] peShape: SeRenderShapeType*, [out] peFillMode: SeRenderFillMode*, [out] peShadeMode: SeRenderShadeMode* | HRESULT |
| `SetPointOptions` | eShape: SeRenderShapeType, eFillMode: SeRenderFillMode, eShadeMode: SeRenderShadeMode | HRESULT |
| `GetPointColor` | [out] pfRed: float*, [out] pfGreen: float*, [out] pfBlue: float* | HRESULT |
| `SetPointColor` | fRed: float, fGreen: float, fBlue: float | HRESULT |
| `get TextureFileNameEx` | [out] psTextureFileName: BSTR* | HRESULT |
| `GetMaterial` | [out] psMaterial: BSTR*, eMode: SeRenderMaterialGetMode | HRESULT |
| `SetMaterial` | sMaterial: BSTR, eMode: SeRenderMaterialSetMode | HRESULT |
| `get Material` | [out] psMaterial: BSTR* | HRESULT |
| `put Material` | psMaterial: BSTR | HRESULT |

### <a name="_ifacestylesauto"></a>`_IFaceStylesAuto`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `Item` | Index: VARIANT, [out] pItem: IDispatch* | HRESULT |
| `get _NewEnum` | [out] pEnum: IUnknown* | HRESULT |
| `get Application` | [out] pApp: IDispatch* | HRESULT |
| `get Count` | [out] Count: int* | HRESULT |
| `get Parent` | [out] pParent: IDispatch* | HRESULT |
| `Add` | Name: BSTR, Parent: BSTR, [out] Style3d: FaceStyle** | HRESULT |
| `Remove` | Name: BSTR | HRESULT |
| `GetStyleByID` | StyleID: int, [out] pItem: IDispatch* | HRESULT |

### <a name="_itextstyleauto"></a>`_ITextStyleAuto`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `put Name` | Name: BSTR | HRESULT |
| `get Name` | [out] Name: BSTR* | HRESULT |
| `get Description` | paperUnits: int, Precision: int, [out] Description: BSTR* | HRESULT |
| `get Units` | [out] Units: int* | HRESULT |
| `put Units` | Units: int | HRESULT |
| `put Parent` | Name: BSTR | HRESULT |
| `get Parent` | [out] Name: BSTR* | HRESULT |
| `get Alignment` | [out] Alignment: int* | HRESULT |
| `put Alignment` | Alignment: int | HRESULT |
| `get BeforeSpacing` | [out] Spacing: double* | HRESULT |
| `put BeforeSpacing` | Spacing: double | HRESULT |
| `get AfterSpacing` | [out] Spacing: double* | HRESULT |
| `put AfterSpacing` | Spacing: double | HRESULT |
| `get LineSpacing` | [out] lSpacing: double* | HRESULT |
| `put LineSpacing` | lSpacing: double | HRESULT |
| `get Tabs` | [out] tabDistance: double* | HRESULT |
| `put Tabs` | tabDistance: double | HRESULT |
| `put CharStyleName` | Name: BSTR | HRESULT |
| `get CharStyleName` | [out] Name: BSTR* | HRESULT |
| `SetLineLeading` | leading: double, leadingType: int | HRESULT |
| `GetLineLeading` | [out] leading: double*, [out] leadingType: int* | HRESULT |
| `get NumberJustification` | [out] NumberJustification: TextStyleNumberJustificationConstants* | HRESULT |
| `put NumberJustification` | NumberJustification: TextStyleNumberJustificationConstants | HRESULT |
| `get AttributeSets` | [out] AttributeSets: IDispatch* | HRESULT |
| `get IsAttributeSetPresent` | Name: BSTR, [out] IsAttributeSetPresent: bool* | HRESULT |

### <a name="_itextstylesauto"></a>`_ITextStylesAuto`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `get Count` | [out] Count: int* | HRESULT |
| `get Parent` | [out] pParent: IDispatch* | HRESULT |
| `get Application` | [out] pApp: IDispatch* | HRESULT |
| `Item` | Index: VARIANT, [out] pItem: TextStyle** | HRESULT |
| `get _NewEnum` | [out] pEnum: IUnknown* | HRESULT |
| `Add` | Name: BSTR, Parent: BSTR, [out] GeoStyle: TextStyle** | HRESULT |
| `Remove` | Name: BSTR | HRESULT |
| `get Active` | [out] Name: BSTR* | HRESULT |
| `put Active` | Name: BSTR | HRESULT |

### <a name="_itextcharstyleauto"></a>`_ITextCharStyleAuto`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `put Name` | Name: BSTR | HRESULT |
| `get Name` | [out] Name: BSTR* | HRESULT |
| `get Description` | paperUnits: int, Precision: int, [out] Description: BSTR* | HRESULT |
| `put Units` | Units: int | HRESULT |
| `get Units` | [out] Units: int* | HRESULT |
| `get Color` | [out] Color: int* | HRESULT |
| `put Color` | Color: int | HRESULT |
| `get Parent` | [out] Name: BSTR* | HRESULT |
| `put Parent` | Name: BSTR | HRESULT |
| `put FontName` | Name: BSTR | HRESULT |
| `get FontName` | [out] Name: BSTR* | HRESULT |
| `put Style` | Style: int | HRESULT |
| `get Style` | [out] Style: int* | HRESULT |
| `get UnderlineStyle` | [out] Style: int* | HRESULT |
| `put UnderlineStyle` | Style: int | HRESULT |
| `get LangID` | [out] LangID: int* | HRESULT |
| `put LangID` | LangID: int | HRESULT |
| `get TextSize` | [out] TextSize: double* | HRESULT |
| `put TextSize` | TextSize: double | HRESULT |
| `SetTextSize` | TextSize: double, SizeType: int | HRESULT |
| `GetTextSize` | [out] TextSize: double*, [out] SizeType: int* | HRESULT |
| `get AspectRatio` | [out] TextSize: double* | HRESULT |
| `put AspectRatio` | TextSize: double | HRESULT |
| `get AttributeSets` | [out] AttributeSets: IDispatch* | HRESULT |
| `get IsAttributeSetPresent` | Name: BSTR, [out] IsAttributeSetPresent: bool* | HRESULT |

### <a name="_itextcharstylesauto"></a>`_ITextCharStylesAuto`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `get Count` | [out] Count: int* | HRESULT |
| `get Parent` | [out] pParent: IDispatch* | HRESULT |
| `get Application` | [out] pApp: IDispatch* | HRESULT |
| `Item` | Index: VARIANT, [out] pItem: TextCharStyle** | HRESULT |
| `get _NewEnum` | [out] pEnum: IUnknown* | HRESULT |
| `Add` | Name: BSTR, Parent: BSTR, [out] GeoStyle: TextCharStyle** | HRESULT |
| `Remove` | Name: BSTR | HRESULT |

### <a name="_isymbol2dauto"></a>`_ISymbol2dAuto`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `get Style` | [out] pStyle: IDispatch* | HRESULT |
| `get UseSymbolLayer` | [out] flag: bool* | HRESULT |
| `put UseSymbolLayer` | flag: bool | HRESULT |
| `get Layer` | [out] pLayer: BSTR* | HRESULT |
| `put Layer` | pLayer: BSTR | HRESULT |
| `get Angle` | [out] Angle: double* | HRESULT |
| `put Angle` | Angle: double | HRESULT |
| `get ScaleFactorLock` | [out] lock: bool* | HRESULT |
| `put ScaleFactorLock` | lock: bool | HRESULT |
| `get Quantity` | [out] Quantity: int* | HRESULT |
| `put Quantity` | Quantity: int | HRESULT |
| `get User` | [out] pUser: IDispatch* | HRESULT |
| `get ScaleFactor` | [out] __MIDL___ISymbol2dAuto0000: double* | HRESULT |
| `put ScaleFactor` | __MIDL___ISymbol2dAuto0000: double | HRESULT |
| `GetOrigin` | [out] Ox: double*, [out] Oy: double* | HRESULT |
| `SetOrigin` | Ox: double, Oy: double | HRESULT |
| `GetRotations` | [out] Xx: double*, [out] Xy: double*, [out] Yx: double*, [out] Yy: double* | HRESULT |
| `SetRotations` | Xx: double, Xy: double, Yx: double, Yy: double | HRESULT |
| `get DisplayType` | [out] Type: DisplayTypeConstant* | HRESULT |
| `put DisplayType` | Type: DisplayTypeConstant | HRESULT |
| `get NestedDisplay` | [out] flag: bool* | HRESULT |
| `put NestedDisplay` | flag: bool | HRESULT |
| `get ContentsLocatable` | [out] flag: bool* | HRESULT |
| `put ContentsLocatable` | flag: bool | HRESULT |
| `get SourceDoc` | [out] SourceDoc: BSTR* | HRESULT |
| `get Class` | [out] sourceClass: BSTR* | HRESULT |
| `get Object` | [out] OLEObject: IDispatch* | HRESULT |
| `get OLEType` | [out] Type: OLEInsertionTypeConstant* | HRESULT |
| `get UpdateOptions` | [out] option: OLEUpdateOptionConstant* | HRESULT |
| `put UpdateOptions` | option: OLEUpdateOptionConstant | HRESULT |
| `Update` | — | HRESULT |
| `DoVerb` | [opt]verb: VARIANT | HRESULT |
| `get ObjectVerbsCount` | [out] Count: int* | HRESULT |
| `ObjectVerbs` | [opt]Index: VARIANT, [out] verb: BSTR* | HRESULT |
| `get AlternatePath` | [out] currentPath: BSTR* | HRESULT |
| `put AlternatePath` | currentPath: BSTR* | HRESULT |
| `get Application` | [out] Application: Application** | HRESULT |
| `get Index` | [out] Index: int* | HRESULT |
| `get Name` | [opt]Recurse: VARIANT, Lcid: int, [out] Name: BSTR* | HRESULT |
| `get Parent` | [out] Parent: IDispatch* | HRESULT |
| `get Type` | [out] Type: int* | HRESULT |
| `get ZOrder` | [out] ZOrder: int* | HRESULT |
| `get Key` | [opt]Recurse: VARIANT, [out] Key: BSTR* | HRESULT |
| `get Document` | [out] Document: IDispatch* | HRESULT |
| `Copy` | — | HRESULT |
| `Cut` | — | HRESULT |
| `Delete` | — | HRESULT |
| `Move` | XFrom: double, YFrom: double, XTo: double, YTo: double | HRESULT |
| `Scale` | Factor: double | HRESULT |
| `Rotate` | Angle: double, x: double, y: double | HRESULT |
| `Range` | [out] XMinimum: double*, [out] YMinimum: double*, [out] XMaximum: double*, [out] YMaximum: double* | HRESULT |
| `Duplicate` | [opt]XDistance: VARIANT, [opt]YDistance: VARIANT, [out] NewObject: IDispatch* | HRESULT |
| `Mirror` | X1: double, Y1: double, X2: double, Y2: double, [opt]BooleanCopyFlag: VARIANT, [out] Object: IDispatch* | HRESULT |
| `BringToFront` | — | HRESULT |
| `BringForward` | — | HRESULT |
| `SendToBack` | — | HRESULT |
| `SendBackward` | — | HRESULT |
| `Select` | — | HRESULT |
| `get KeyPointCount` | [out] Count: int* | HRESULT |
| `GetKeyPoint` | Index: int, [out] x: double*, [out] y: double*, [out] z: double*, [out] KeyPointType: KeyPointType*, [out] HandleType: int* | HRESULT |
| `SetKeyPoint` | Index: int, x: double, y: double, z: double | HRESULT |
| `get AttributeSets` | [out] AttributeSets: IDispatch* | HRESULT |
| `get IsAttributeSetPresent` | Name: BSTR, [out] IsAttributeSetPresent: bool* | HRESULT |
| `ConvertToGroup` | — | HRESULT |
| `get MemberReference` | Member: IDispatch, [out] Reference: IDispatch* | HRESULT |
| `get SourceDocument` | [out] SourceDocument: IDispatch* | HRESULT |

### <a name="_isymbolsauto"></a>`_ISymbolsAuto`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `get Application` | [out] pApp: IDispatch* | HRESULT |
| `get Count` | [out] Count: int* | HRESULT |
| `get Parent` | [out] pParent: IDispatch* | HRESULT |
| `Item` | Index: VARIANT, [out] pSymbol: IDispatch* | HRESULT |
| `get _NewEnum` | [out] pEnum: IUnknown* | HRESULT |
| `Add` | insertionType: int, filePath: BSTR, x: double, y: double, [opt]z: VARIANT, [out] pSymbol: Symbol2d** | HRESULT |
| `InsertSymbolAsGeometry` | filePath: BSTR, dOriginX: double, dOriginY: double | HRESULT |

### <a name="_isymbolpropertiesauto"></a>`_ISymbolPropertiesAuto`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `get Application` | [out] pApp: IDispatch* | HRESULT |
| `get Parent` | [out] pParent: IDispatch* | HRESULT |
| `get Symbol` | [out] pSymbolprops: IDispatch* | HRESULT |
| `get AttributeSets` | [out] AttributeSets: IDispatch* | HRESULT |
| `get IsAttributeSetPresent` | Name: BSTR, [out] IsAttributeSetPresent: bool* | HRESULT |

### <a name="_iviewstyleauto"></a>`_IViewStyleAuto`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `get StyleName` | [out] psStyleName: BSTR* | HRESULT |
| `put StyleName` | psStyleName: BSTR | HRESULT |
| `get Parent` | [out] psParentName: BSTR* | HRESULT |
| `put Parent` | psParentName: BSTR | HRESULT |
| `get RenderMode` | [out] pnRenderMode: int* | HRESULT |
| `put RenderMode` | pnRenderMode: int | HRESULT |
| `get AllowOverrides` | [out] pbAllowOverrides: int* | HRESULT |
| `put AllowOverrides` | pbAllowOverrides: int | HRESULT |
| `get AntialiasWireframe` | [out] pnAntialiasWireframe: int* | HRESULT |
| `put AntialiasWireframe` | pnAntialiasWireframe: int | HRESULT |
| `get AntialiasSurface` | [out] pnAntialiasSurface: int* | HRESULT |
| `put AntialiasSurface` | pnAntialiasSurface: int | HRESULT |
| `get DepthFading` | [out] pbDepthFading: int* | HRESULT |
| `put DepthFading` | pbDepthFading: int | HRESULT |
| `get Perspective` | [out] pbPerspective: int* | HRESULT |
| `put Perspective` | pbPerspective: int | HRESULT |
| `get FocalLength` | [out] pnFocalLength: int* | HRESULT |
| `put FocalLength` | pnFocalLength: int | HRESULT |
| `get NumLights` | [out] pnNumLights: int* | HRESULT |
| `get AmbientColor` | [out] plAmbientColor: int* | HRESULT |
| `put AmbientColor` | plAmbientColor: int | HRESULT |
| `get AmbientIntensity` | [out] pfAmbientIntensity: float* | HRESULT |
| `put AmbientIntensity` | pfAmbientIntensity: float | HRESULT |
| `get AmbientRed` | [out] pfAmbientRed: float* | HRESULT |
| `put AmbientRed` | pfAmbientRed: float | HRESULT |
| `get AmbientGreen` | [out] pfAmbientGreen: float* | HRESULT |
| `put AmbientGreen` | pfAmbientGreen: float | HRESULT |
| `get AmbientBlue` | [out] pfAmbientBlue: float* | HRESULT |
| `put AmbientBlue` | pfAmbientBlue: float | HRESULT |
| `get HiddenLineMode` | [out] pnHiddenLineMode: int* | HRESULT |
| `put HiddenLineMode` | pnHiddenLineMode: int | HRESULT |
| `get DimPercentage` | [out] pfDimPercentage: float* | HRESULT |
| `put DimPercentage` | pfDimPercentage: float | HRESULT |
| `get IsBackgroundImageDisplayed` | [out] pbIsBgImageDisplayed: int* | HRESULT |
| `BeginPropertyBuffer` | — | HRESULT |
| `FlushPropertyBuffer` | — | HRESULT |
| `AddLight` | fRed: float, fGreen: float, fBlue: float, fTheta: float, fPhi: float, [out] pnLight: int* | HRESULT |
| `DeleteLight` | nLight: int | HRESULT |
| `GetLight` | nLight: int, [out] pfRed: float*, [out] pfGreen: float*, [out] pfBlue: float*, [out] pfTheta: float*, [out] pfPhi: float* | HRESULT |
| `SetLight` | nLight: int, fRed: float, fGreen: float, fBlue: float, fTheta: float, fPhi: float | HRESULT |
| `GetLightColor` | nLight: int, [out] plLightColor: int* | HRESULT |
| `SetLightColor` | nLight: int, lLightColor: int | HRESULT |
| `GetLightIntensity` | nLight: int, [out] pfIntensity: float* | HRESULT |
| `SetLightIntensity` | nLight: int, fIntensity: float | HRESULT |
| `GetLightTheta` | nLight: int, [out] pfTheta: float* | HRESULT |
| `SetLightTheta` | nLight: int, fTheta: float | HRESULT |
| `GetLightPhi` | nLight: int, [out] pfPhi: float* | HRESULT |
| `SetLightPhi` | nLight: int, fPhi: float | HRESULT |
| `Delete` | — | HRESULT |
| `get RenderModeType` | [out] pnRenderMode: SeRenderModeType* | HRESULT |
| `put RenderModeType` | pnRenderMode: SeRenderModeType | HRESULT |
| `get SilhouettesEnabled` | [out] pbEnabled: bool* | HRESULT |
| `put SilhouettesEnabled` | pbEnabled: bool | HRESULT |
| `get StyleID` | [out] plStyleID: int* | HRESULT |
| `GetAnalysisParameters` | [out] peState: SeAnalysisStateType*, [opt][out] peMode: SeAnalysisModeType*, [opt][out] pQualityScale: VARIANT*, [opt][out] pArg1: VARIANT*, [opt][out] pArg2: VARIANT*, [opt][out] pArg3: VARIANT*, [opt][out] pArg4: VARIANT* | HRESULT |
| `SetAnalysisParameters` | eState: SeAnalysisStateType, [opt]eMode: SeAnalysisModeType, [opt]QualityScale: VARIANT, [opt]Arg1: VARIANT, [opt]Arg2: VARIANT, [opt]Arg3: VARIANT, [opt]Arg4: VARIANT | HRESULT |
| `get BackgroundType` | [out] pnBackgroundType: SeBackgroundType* | HRESULT |
| `put BackgroundType` | pnBackgroundType: SeBackgroundType | HRESULT |
| `get BackgroundImageFile` | [out] psBackgroundImageFile: BSTR* | HRESULT |
| `put BackgroundImageFile` | psBackgroundImageFile: BSTR | HRESULT |
| `get SkyboxType` | [out] peType: SeSkyboxType* | HRESULT |
| `put SkyboxType` | peType: SeSkyboxType | HRESULT |
| `SetSkyboxSkyFile` | sFilename: BSTR | HRESULT |
| `SetSkyboxSideFilename` | nSide: int, sFilename: BSTR | HRESULT |
| `GetSkyboxSideFilename` | nSide: int, [out] psFilename: BSTR* | HRESULT |
| `SkyboxClear` | nSide: int | HRESULT |
| `SkyboxClearAll` | — | HRESULT |
| `GetSkyboxOrientation` | [out] pfxDirection: float*, [out] pfyDirection: float*, [out] pfzDirection: float*, [out] pfxUp: float*, [out] pfyUp: float*, [out] pfzUp: float*, [out] pfFieldOfView: float* | HRESULT |
| `SetSkyboxOrientation` | fxDirection: float, fyDirection: float, fzDirection: float, fxUp: float, fyUp: float, fzUp: float, fFieldOfView: float | HRESULT |
| `get BackgroundMirrorX` | [out] pbMirrorX: int* | HRESULT |
| `put BackgroundMirrorX` | pbMirrorX: int | HRESULT |
| `get BackgroundMirrorY` | [out] pbMirrorY: int* | HRESULT |
| `put BackgroundMirrorY` | pbMirrorY: int | HRESULT |
| `get Textures` | [out] pbTextures: int* | HRESULT |
| `put Textures` | pbTextures: int | HRESULT |
| `get Reflections` | [out] pbReflections: int* | HRESULT |
| `put Reflections` | pbReflections: int | HRESULT |
| `get Bumpmaps` | [out] pbBumpmaps: int* | HRESULT |
| `put Bumpmaps` | pbBumpmaps: int | HRESULT |
| `get FloorReflection` | [out] pbFloorReflection: int* | HRESULT |
| `put FloorReflection` | pbFloorReflection: int | HRESULT |
| `get CastShadows` | [out] pbCastShadows: int* | HRESULT |
| `put CastShadows` | pbCastShadows: int | HRESULT |
| `get DropShadow` | [out] pbDropShadow: int* | HRESULT |
| `put DropShadow` | pbDropShadow: int | HRESULT |
| `SetGradientBackground` | eType: SeGradientType, crColor1: int, crColor2: int, [opt]SpotCenterX: VARIANT, [opt]SpotCenterY: VARIANT | HRESULT |
| `GetGradientBackground` | [out] peType: SeGradientType*, [out] pcrColor1: int*, [out] pcrColor2: int*, [opt][out] pSpotCenterX: VARIANT*, [opt][out] pSpotCenterY: VARIANT* | HRESULT |
| `SetGradientColor` | nColor: int, crColor: int | HRESULT |
| `GetGradientColor` | nColor: int, [out] pcrColor: int* | HRESULT |
| `get AntialiasLevel` | [out] pnAntialiasLevel: SeAntiAliasLevel* | HRESULT |
| `put AntialiasLevel` | pnAntialiasLevel: SeAntiAliasLevel | HRESULT |
| `get Silhouettes` | [out] pbEnabled: int* | HRESULT |
| `put Silhouettes` | pbEnabled: int | HRESULT |
| `get HiddenLines` | [out] peMode: SeHiddenLineMode* | HRESULT |
| `put HiddenLines` | peMode: SeHiddenLineMode | HRESULT |
| `get HighQuality` | [out] pbHighQuality: int* | HRESULT |
| `put HighQuality` | pbHighQuality: int | HRESULT |
| `ResetSkyboxOrientation` | — | HRESULT |
| `DeleteSkybox` | — | HRESULT |
| `get AmbientShadows` | [out] pbAmbientShadows: int* | HRESULT |
| `put AmbientShadows` | pbAmbientShadows: int | HRESULT |
| `get FloorShadow` | [out] pbFloorShadow: int* | HRESULT |
| `put FloorShadow` | pbFloorShadow: int | HRESULT |
| `GetLightFlags` | nLight: int, [out] pnFlags: int* | HRESULT |
| `SetLightFlags` | nLight: int, nFlags: int | HRESULT |

### <a name="_iviewstylesauto"></a>`_IViewStylesAuto`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `Item` | Index: VARIANT, [out] pItem: ViewStyle** | HRESULT |
| `get _NewEnum` | [out] pEnum: IUnknown* | HRESULT |
| `get Application` | [out] pApp: IDispatch* | HRESULT |
| `get Count` | [out] Count: int* | HRESULT |
| `get Parent` | [out] pParent: IDispatch* | HRESULT |
| `Add` | Name: BSTR, Parent: BSTR, [out] ViewStyle: ViewStyle** | HRESULT |
| `Remove` | Name: BSTR | HRESULT |
| `GetStyleByID` | StyleID: int, [out] pItem: IDispatch* | HRESULT |
| `AddFromFile` | Filename: BSTR, StyleName: BSTR, [out] ViewStyle: ViewStyle** | HRESULT |

### <a name="_ireferenceauto"></a>`_IReferenceAuto`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `get Application` | [out] Application: IDispatch* | HRESULT |
| `get Object` | [out] Object: IDispatch* | HRESULT |
| `get Parent` | [out] Parent: IDispatch* | HRESULT |
| `get Type` | [out] Type: ObjectType* | HRESULT |
| `GetMatrix` | [in,out] Matrix: SAFEARRAY(double)* | HRESULT |
| `get AttributeSets` | [out] AttributeSets: IDispatch* | HRESULT |
| `get IsAttributeSetPresent` | Name: BSTR, [out] IsAttributeSetPresent: bool* | HRESULT |
| `get ImmediateParent` | [out] Parent: IDispatch* | HRESULT |
| `get Style` | [out] Style: BSTR* | HRESULT |
| `put Style` | Style: BSTR | HRESULT |
| `GetOccurrencesInPath` | [out] TopOccurrence: IDispatch*, [out] NumSubOccurrencesInPath: int*, [out] NumBoundSubOccurrencesInPath: int*, [in,out] BoundSubOccurrencesInPath: SAFEARRAY(IDispatch)* | HRESULT |

### <a name="_iroutingslipauto"></a>`_IRoutingSlipAuto`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `put Subject` | bsSubject: BSTR | HRESULT |
| `get Subject` | [out] bsSubject: BSTR* | HRESULT |
| `put ReturnWhenDone` | pReturn: bool | HRESULT |
| `get ReturnWhenDone` | [out] pReturn: bool* | HRESULT |
| `put Message` | pbsMessage: BSTR | HRESULT |
| `get Message` | [out] pbsMessage: BSTR* | HRESULT |
| `put Recipients` | p0: VARIANT | HRESULT |
| `put Delivery` | RoutMethod: RouteType | HRESULT |
| `get Delivery` | [out] RoutMethod: RouteType* | HRESULT |
| `get Status` | [out] RouteStatus: RouteStatus* | HRESULT |
| `get HasRouted` | [out] HasRouted: bool* | HRESULT |
| `get Application` | [out] lpApplication: IDispatch* | HRESULT |
| `get Parent` | [out] lpParent: IDispatch* | HRESULT |
| `put TrackStatus` | TrackStatus: bool | HRESULT |
| `get TrackStatus` | [out] TrackStatus: bool* | HRESULT |
| `put AskForApproval` | pAskApproval: bool | HRESULT |
| `get AskForApproval` | [out] pAskApproval: bool* | HRESULT |
| `put Approve` | pApprove: bool | HRESULT |
| `get Approve` | [out] pApprove: bool* | HRESULT |
| `get Approved` | [out] pVoted: bool* | HRESULT |
| `GetRouteInfo` | [out] pbRoute: bool* | HRESULT |
| `AddRecipient` | bsRecip: BSTR | HRESULT |
| `Route` | [opt]ConfirmRoute: VARIANT | HRESULT |
| `Reset` | — | HRESULT |
| `get AttributeSets` | [out] AttributeSets: IDispatch* | HRESULT |
| `get IsAttributeSetPresent` | Name: BSTR, [out] IsAttributeSetPresent: bool* | HRESULT |

### <a name="_ipropertysetsauto"></a>`_IPropertySetsAuto`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `get Count` | [out] Count: int* | HRESULT |
| `get Parent` | [out] Parent: IDispatch* | HRESULT |
| `get Application` | [out] Application: Application** | HRESULT |
| `get _NewEnum` | [out] Unknown: IUnknown* | HRESULT |
| `Item` | vIndex: VARIANT, [out] Item: Properties** | HRESULT |
| `Save` | — | HRESULT |

### <a name="_ipropertiesauto"></a>`_IPropertiesAuto`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `get Count` | [out] Count: int* | HRESULT |
| `get Parent` | [out] Parent: IDispatch* | HRESULT |
| `get Application` | [out] Application: Application** | HRESULT |
| `get _NewEnum` | [out] Unknown: IUnknown* | HRESULT |
| `get Name` | [out] Name: BSTR* | HRESULT |
| `Item` | vIndex: VARIANT, [out] Item: Property** | HRESULT |
| `Save` | — | HRESULT |
| `Add` | Name: VARIANT, Value: VARIANT, [out] Property: Property** | HRESULT |
| `PropertyByID` | vIndex: VARIANT, [out] Property: Property** | HRESULT |

### <a name="_ipropertyauto"></a>`_IPropertyAuto`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `get Name` | [out] bstName: BSTR* | HRESULT |
| `get Value` | [out] Value: VARIANT* | HRESULT |
| `put Value` | Value: VARIANT* | HRESULT |
| `get Type` | [out] Type: VARIANT* | HRESULT |
| `Delete` | — | HRESULT |
| `Id` | [out] pvPID: VARIANT* | HRESULT |

### <a name="_ipropertyexauto"></a>`_IPropertyExAuto`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `GetProps` | [out] bstName: BSTR*, [out] Value: VARIANT*, [out] Type: VARIANT* | HRESULT |

### <a name="_isummaryinfoauto"></a>`_ISummaryInfoAuto`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `get AccessDate` | [out] vValue: VARIANT* | HRESULT |
| `get Application` | [out] lpApp: Application** | HRESULT |
| `get Author` | [out] vValue: BSTR* | HRESULT |
| `put Author` | vValue: BSTR | HRESULT |
| `get Category` | [out] vValue: BSTR* | HRESULT |
| `put Category` | vValue: BSTR | HRESULT |
| `get Comments` | [out] vValue: BSTR* | HRESULT |
| `put Comments` | vValue: BSTR | HRESULT |
| `get Company` | [out] vValue: BSTR* | HRESULT |
| `put Company` | vValue: BSTR | HRESULT |
| `get CreateApp` | [out] vValue: BSTR* | HRESULT |
| `put CreateApp` | vValue: BSTR | HRESULT |
| `get CreateDate` | [out] vValue: VARIANT* | HRESULT |
| `get CreationLocale` | [out] vValue: int* | HRESULT |
| `get DocumentNumber` | [out] vValue: BSTR* | HRESULT |
| `put DocumentNumber` | vValue: BSTR | HRESULT |
| `get Keywords` | [out] vValue: BSTR* | HRESULT |
| `put Keywords` | vValue: BSTR | HRESULT |
| `get LastSavedBy` | [out] vValue: BSTR* | HRESULT |
| `put LastSavedBy` | vValue: BSTR | HRESULT |
| `get Manager` | [out] vValue: BSTR* | HRESULT |
| `put Manager` | vValue: BSTR | HRESULT |
| `get Parent` | [out] lpParent: IDispatch* | HRESULT |
| `get ProjectName` | [out] vValue: BSTR* | HRESULT |
| `put ProjectName` | vValue: BSTR | HRESULT |
| `get RevisionNumber` | [out] vValue: BSTR* | HRESULT |
| `put RevisionNumber` | vValue: BSTR | HRESULT |
| `get SaveApp` | [out] vValue: BSTR* | HRESULT |
| `put SaveApp` | vValue: BSTR | HRESULT |
| `get SaveDate` | [out] vValue: VARIANT* | HRESULT |
| `get Subject` | [out] vValue: BSTR* | HRESULT |
| `put Subject` | vValue: BSTR | HRESULT |
| `get Template` | [out] vValue: BSTR* | HRESULT |
| `put Template` | vValue: BSTR | HRESULT |
| `get Title` | [out] vValue: BSTR* | HRESULT |
| `put Title` | vValue: BSTR | HRESULT |
| `get TotalEdits` | [out] vValue: BSTR* | HRESULT |
| `put TotalEdits` | vValue: BSTR | HRESULT |

### <a name="_iattributesetsauto"></a>`_IAttributeSetsAuto`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `get Count` | [out] Count: int* | HRESULT |
| `get _NewEnum` | [out] Enum: IUnknown* | HRESULT |
| `Item` | Index: VARIANT, [out] Item: AttributeSet** | HRESULT |
| `Add` | Name: BSTR, [out] AttributeSet: AttributeSet** | HRESULT |
| `Remove` | Name: BSTR | HRESULT |

### <a name="_iattributesetauto"></a>`_IAttributeSetAuto`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `get Count` | [out] Count: int* | HRESULT |
| `get _NewEnum` | [out] Enum: IUnknown* | HRESULT |
| `Item` | Index: VARIANT, [out] Item: Attribute** | HRESULT |
| `Add` | Name: BSTR, Type: AttributeTypeConstants, [out] Attribute: Attribute** | HRESULT |
| `Remove` | Name: BSTR | HRESULT |
| `get SetName` | [out] Name: BSTR* | HRESULT |

### <a name="_iattributeauto"></a>`_IAttributeAuto`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `get Name` | [out] Name: BSTR* | HRESULT |
| `get Value` | [out] Value: VARIANT* | HRESULT |
| `put Value` | Value: VARIANT | HRESULT |
| `get Type` | [out] Type: AttributeTypeConstants* | HRESULT |

### <a name="_iattributequeryauto"></a>`_IAttributeQueryAuto`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `QueryByName` | [opt]AttributeSetName: VARIANT, [opt]AttributeName: VARIANT, [out] QueryObjects: QueryObjects** | HRESULT |
| `get Application` | [out] Application: Application** | HRESULT |
| `get Parent` | [out] Parent: IDispatch* | HRESULT |

### <a name="_iqueryobjectsauto"></a>`_IQueryObjectsAuto`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `get Application` | [out] Application: Application** | HRESULT |
| `get Count` | [out] Count: int* | HRESULT |
| `Item` | Index: int, [out] Item: IDispatch* | HRESULT |
| `get Parent` | [out] Parent: IDispatch* | HRESULT |

### <a name="_ihighlightsetsauto"></a>`_IHighlightSetsAuto`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `get Application` | [out] Application: Application** | HRESULT |
| `get Parent` | [out] Parent: IDispatch* | HRESULT |
| `get Count` | [out] Count: int* | HRESULT |
| `get _NewEnum` | [out] Enum: IUnknown* | HRESULT |
| `Item` | Index: VARIANT, [out] Item: HighlightSet** | HRESULT |
| `Add` | [out] HighlightSet: HighlightSet** | HRESULT |

### <a name="_ihighlightsetauto"></a>`_IHighlightSetAuto`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `AddItem` | Item: IDispatch | HRESULT |
| `AddSelected` | — | HRESULT |
| `RemoveItem` | Index: VARIANT | HRESULT |
| `RemoveAll` | — | HRESULT |
| `Draw` | — | HRESULT |
| `Delete` | — | HRESULT |
| `get Count` | [out] Count: int* | HRESULT |
| `Item` | Index: VARIANT, [out] Item: IDispatch* | HRESULT |
| `get Color` | [out] Color: int* | HRESULT |
| `put Color` | Color: int | HRESULT |
| `SetTransform` | Matrix: SAFEARRAY(double)* | HRESULT |
| `ClearTransform` | — | HRESULT |

### <a name="_isegenericcollectionauto"></a>`_ISEGenericCollectionAuto`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `get Application` | [out] Application: Application** | HRESULT |
| `get Parent` | [out] Parent: IDispatch* | HRESULT |
| `get Count` | [out] Count: int* | HRESULT |
| `get _NewEnum` | [out] Enum: IUnknown* | HRESULT |
| `Item` | Index: VARIANT, [out] Item: IDispatch* | HRESULT |

### <a name="_isolidedgedocumentauto"></a>`_ISolidEdgeDocumentAuto`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `Activate` | — | HRESULT |
| `get Application` | [out] Application: Application** | HRESULT |
| `Close` | [opt]SaveChanges: VARIANT, [opt]Filename: VARIANT, [opt]RouteWorkbook: VARIANT | HRESULT |
| `get FullName` | [out] FullName: BSTR* | HRESULT |
| `get Name` | [out] Name: BSTR* | HRESULT |
| `get Parent` | [out] Parent: Application** | HRESULT |
| `get Path` | [out] Path: BSTR* | HRESULT |
| `PrintOut` | [opt]Printer: VARIANT, [opt]NumCopies: VARIANT, [opt]Orientation: VARIANT, [opt]PaperSize: VARIANT, [opt]Scale: VARIANT, [opt]PrintToFile: VARIANT, [opt]OutputFileName: VARIANT, [opt]PrintRange: VARIANT, [opt]Sheets: VARIANT, [opt]ColorAsBlack: VARIANT, [opt]Collate: VARIANT | HRESULT |
| `get ReadOnly` | [out] ReadOnly: bool* | HRESULT |
| `get RoutingSlip` | [out] RoutingSlip: IDispatch* | HRESULT |
| `Save` | — | HRESULT |
| `SaveAs` | NewName: BSTR, [opt]IsATemplate: VARIANT, [opt]FileFormat: VARIANT, [opt]ReadOnlyEnforced: VARIANT, [opt]ReadOnlyRecommended: VARIANT, [opt]newstatus: VARIANT, [opt]CreateBackup: VARIANT, [opt]UpdateLinkInContainer: VARIANT, [opt]UpdateAllLinksInContainer: VARIANT | HRESULT |
| `SaveCopyAs` | Name: BSTR | HRESULT |
| `SaveAsJT` | NewName: BSTR, [opt]Include_PreciseGeom: VARIANT, [opt]Prod_Structure_Option: VARIANT, [opt]Export_PMI: VARIANT, [opt]Export_CoordinateSystem: VARIANT, [opt]Export_3DBodies: VARIANT, [opt]NumberofLODs: VARIANT, [opt]JTFileUnit: VARIANT, [opt]Write_Which_Files: VARIANT, [opt]Use_Simplified_TopAsm: VARIANT, [opt]Use_Simplified_SubAsm: VARIANT, [opt]Use_Simplified_Part: VARIANT, [opt]EnableDefaultOutputPath: VARIANT, [opt]IncludeSEProperties: VARIANT, [opt]Export_VisiblePartsOnly: VARIANT, [opt]Export_VisibleConstructionsOnly: VARIANT, [opt]RemoveUnsafeCharacters: VARIANT, [opt]ExportSEPartFileAsSingleJTFile: VARIANT | HRESULT |
| `SaveAsBIDM` | filePath: BSTR, DocumentNumber: BSTR, Revision: BSTR, Title: BSTR, [out] NewFileName: BSTR* | HRESULT |
| `ReviseBIDM` | filePath: BSTR, Revision: BSTR, Title: BSTR, [out] NewFileName: BSTR* | HRESULT |
| `get SelectSet` | [out] SelectSet: SelectSet** | HRESULT |
| `SendMail` | [opt]Recipients: VARIANT, [opt]Subject: VARIANT, [opt]ReturnReceipt: VARIANT | HRESULT |
| `get SummaryInfo` | [out] SummaryInfo: IDispatch* | HRESULT |
| `get Windows` | [out] Windows: Windows** | HRESULT |
| `get Properties` | [out] Properties: IDispatch* | HRESULT |
| `get IsTemplate` | [out] IsTemplate: bool* | HRESULT |
| `put IsTemplate` | IsTemplate: bool | HRESULT |
| `get Status` | [out] Status: DocumentStatus* | HRESULT |
| `put Status` | Status: DocumentStatus | HRESULT |
| `EditProperties` | — | HRESULT |
| `get UnitsOfMeasure` | [out] UnitsOfMeasurement: UnitsOfMeasure** | HRESULT |
| `get ActiveSketch` | [out] ActiveSketch: IDispatch* | HRESULT |
| `get Type` | [out] Type: DocumentTypeConstants* | HRESULT |
| `get DocumentEvents` | [out] Events: DocumentEvents** | HRESULT |
| `get RootStorage` | [out] RootStorageUnknown: IUnknown* | HRESULT |
| `get AddInsStorage` | Name: BSTR, grfMode: int, [out] AddInsStorageUnknown: IUnknown* | HRESULT |
| `get Dirty` | [out] Dirty: bool* | HRESULT |
| `put Dirty` | Dirty: bool | HRESULT |
| `get AttributeQuery` | [out] AttributeQuery: AttributeQuery** | HRESULT |
| `get CreatedVersion` | [out] Version: BSTR* | HRESULT |
| `get LastSavedVersion` | [out] Version: BSTR* | HRESULT |
| `get HighlightSets` | [out] HighlightSets: HighlightSets** | HRESULT |
| `get InPlaceActivated` | [out] InPlaceActivated: bool* | HRESULT |
| `SeekWriteAccess` | [out] WriteAccess: bool* | HRESULT |
| `get UndoSteps` | [out] NumberOfUndoSteps: int* | HRESULT |
| `put UndoSteps` | NumberOfUndoSteps: int | HRESULT |
| `CreatePreview` | — | HRESULT |
| `put ReadOnly` | ReadOnly: bool | HRESULT |
| `SeekReadOnlyAccess` | [out] ReadOnlyAccess: bool* | HRESULT |
| `ImportStyles2` | StyleType: seStyleTypeConstants, bReplace: bool, pSrcDocument: IDispatch | HRESULT |
| `get IsInsightFile` | [out] IsInsight: bool* | HRESULT |
| `get NamedViews` | [out] NamedViews: NamedViews** | HRESULT |
| `GetRegisteredCustomPropertiesBiDM` | [out] varPropInfo: VARIANT* | HRESULT |
| `SaveAsWithCustomPropertiesBIDM` | filePath: BSTR, DocumentNumber: BSTR, Revision: BSTR, Title: BSTR, varPropInfo: VARIANT, [out] NewFileName: BSTR* | HRESULT |
| `ReviseWithCustomPropertiesBIDM` | filePath: BSTR, Revision: BSTR, Title: BSTR, varPropInfo: VARIANT, [out] NewFileName: BSTR* | HRESULT |
| `SaveAsPRC` | Filename: BSTR | HRESULT |
| `get Variables` | [out] pVars: IDispatch* | HRESULT |
| `NewWindow` | [opt]NewWindowOptions: VARIANT, [opt]Environment: VARIANT, [out] Window: VARIANT* | HRESULT |
| `get Blocks` | [out] Blocks: IDispatch* | HRESULT |
| `put Name` | Name: BSTR | HRESULT |
| `SaveAs3DPrint` | filePath: BSTR, NumberOfCoordinates: int, PositionArray: SAFEARRAY(double)*, [opt]NumberOfNormals: int, [opt]NormalArray: SAFEARRAY(double)*, [opt]NumberofColors: int, [opt]colorArray: SAFEARRAY(int)*, [opt]NumberofIndices: int, [opt]Indexarray: SAFEARRAY(int)*, [opt]NumberOfFaces: int, [opt]FaceArray: SAFEARRAY(int)* | HRESULT |
| `SaveAsPLMXML` | bstrPLMXMLFilePath: BSTR, bstrPLMXMLINIFilePath: BSTR | HRESULT |
| `get GetPredefineRelationProducer` | [out] PredefineRelationProducer: PredefineRelationProducer** | HRESULT |

### <a name="_ipredefinerelationproducerauto"></a>`_IPredefineRelationProducerAuto`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `GroupCount` | [out] nGroupCount: int* | HRESULT |
| `MagneticGroupCount` | [out] nMagneticGroupCount: int* | HRESULT |
| `HasAssemblyCaptureFitRelation` | [out] bHasAssemblyCaptureFit: bool* | HRESULT |
| `AddPredefineRelationGroup` | bstrGroupName: BSTR, ePolarity: PredefineRelationGroupPolarityConstants, bSetDefault: bool, [out] nGroupId: uint* | HRESULT |
| `put DefaultGroup` | nGroupId: uint | HRESULT |
| `get DefaultGroup` | [out] nGroupId: uint* | HRESULT |
| `SetCaptureFitDefault` | bCaptureFitDefault: bool | HRESULT |
| `ClearDefault` | — | HRESULT |
| `SetGroupName` | nGroupId: uint, bstrGroupName: BSTR | HRESULT |
| `GetGroupName` | nGroupId: uint, [out] pbstrGroupName: BSTR* | HRESULT |
| `SetGroupPolarity` | nGroupId: uint, ePolarity: PredefineRelationGroupPolarityConstants | HRESULT |
| `GetGroupPolarity` | nGroupId: uint, [out] pePolarity: PredefineRelationGroupPolarityConstants* | HRESULT |
| `GetRelationCount` | nGroupId: uint, [out] pnRelationCount: int* | HRESULT |
| `GetCaptureFitRelationCount` | [out] pnRelationCount: int* | HRESULT |
| `DeleteGroups` | numDeleteGroups: int, pnDeleteGroupIds: uint* | HRESULT |
| `GetRelationData` | nGroupId: uint, nRelationIndex: int, [opt][out] ppElement: IDispatch*, [opt][out] pRelationType: CapturedRelationshipTypeConstants*, [opt][out] pOffsetType: CapturedRelationshipOffsetTypeConstants*, [opt][out] pdOffsetOne: double*, [opt][out] pdOffsetTwo: double* | HRESULT |
| `SetRelationData` | nGroupId: uint, nRelationIndex: int, pElement: IDispatch, relationType: CapturedRelationshipTypeConstants, offsetType: CapturedRelationshipOffsetTypeConstants, dOffsetOne: double, dOffsetTwo: double | HRESULT |
| `DeleteRelation` | nGroupId: uint, nRelationIndex: int | HRESULT |
| `AddMateRelation` | nGroupId: uint, pElement: IDispatch, [opt]pOffsetType: CapturedRelationshipOffsetTypeConstants*, [opt]pdOffsetOne: double*, [opt]pdOffsetTwo: double* | HRESULT |
| `AddPlanarRelation` | nGroupId: uint, pElement: IDispatch, [opt]pOffsetType: CapturedRelationshipOffsetTypeConstants*, [opt]pdOffsetOne: double*, [opt]pdOffsetTwo: double* | HRESULT |
| `AddAxialRelation` | nGroupId: uint, pElement: IDispatch | HRESULT |
| `AddTangentRelation` | nGroupId: uint, pElement: IDispatch, [opt]pOffsetType: CapturedRelationshipOffsetTypeConstants*, [opt]pdOffsetOne: double*, [opt]pdOffsetTwo: double* | HRESULT |
| `AddConnectRelation` | nGroupId: uint, pElement: IDispatch, [opt]pOffsetType: CapturedRelationshipOffsetTypeConstants*, [opt]pdOffsetOne: double*, [opt]pdOffsetTwo: double* | HRESULT |
| `AddParallelRelation` | nGroupId: uint, pElement: IDispatch, [opt]pOffsetType: CapturedRelationshipOffsetTypeConstants*, [opt]pdOffsetOne: double*, [opt]pdOffsetTwo: double* | HRESULT |
| `get Application` | [out] Application: IDispatch* | HRESULT |

### <a name="_icpdinitializerinsightxtauto"></a>`_ICPDInitializerInsightXTAuto`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `GetDocuments` | [out] psaDocs: SAFEARRAY(VARIANT)* | HRESULT |
| `GetPropertiesInfo` | bstrDocName: BSTR, [out] pvPropInfo: VARIANT* | HRESULT |
| `SetPropertiesInfo` | bstrDocName: BSTR, vPropInfo: VARIANT | HRESULT |
| `GetItemTypes` | bstrDocName: BSTR, [out] psaItemTypes: SAFEARRAY(VARIANT)* | HRESULT |
| `GetMappedPropertiesInfo` | bstrDocName: BSTR, bstrItemType: BSTR, [out] pvPropInfo: VARIANT* | HRESULT |
| `SetControlsBehavior` | vbDisableAssignAllButtonAndMenu: bool, vbDisableRestoreButton: bool, vbDisableItemIDCell: bool, vbDisableItemRevisionCell: bool, vbDisableItemNameCell: bool, vbDisableDatasetNameCell: bool | HRESULT |

### <a name="_icpdinitializerauto"></a>`_ICPDInitializerAuto`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `GetDocuments` | [out] psaDocs: SAFEARRAY(VARIANT)* | HRESULT |
| `GetPropertiesInfo` | bstrDocName: BSTR, [out] pvPropInfo: VARIANT* | HRESULT |
| `SetPropertiesInfo` | bstrDocName: BSTR, vPropInfo: VARIANT | HRESULT |
| `GetItemTypes` | bstrDocName: BSTR, [out] psaItemTypes: SAFEARRAY(VARIANT)* | HRESULT |
| `GetMappedPropertiesInfo` | bstrDocName: BSTR, bstrItemType: BSTR, [out] pvPropInfo: VARIANT* | HRESULT |
| `SetControlsBehavior` | vbDisableAssignAllButtonAndMenu: bool, vbDisableRestoreButton: bool, vbDisableItemIDCell: bool, vbDisableItemRevisionCell: bool, vbDisableItemNameCell: bool, vbDisableDatasetNameCell: bool | HRESULT |

### <a name="_iinterdocumentupdateauto"></a>`_IInterDocumentUpdateAuto`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `GetFilesToUpdate` | [out] FilesToUpdate: SAFEARRAY(BSTR)*, [opt]FutureUse: VARIANT | HRESULT |
| `LoadFilesToUpdate` | [opt]FutureUse: VARIANT | HRESULT |
| `Update` | UpdateMode: InterDocumentUpdateMode, [opt]FutureUse: VARIANT | HRESULT |
| `GetFilesToSave` | [out] FilesToSave: SAFEARRAY(BSTR)*, [opt]FutureUse: VARIANT | HRESULT |
| `SaveChangedFiles` | [opt][out] FilesNotSaved: SAFEARRAY(BSTR)*, [opt]FutureUse: VARIANT | HRESULT |

### <a name="_isteeringwheelauto"></a>`_ISteeringWheelAuto`

| Assinatura | Parâmetros | Retorno |
|------------|------------|---------|
| `GetOrigin` | [out] OriginX: double*, [out] OriginY: double*, [out] OriginZ: double* | HRESULT |
| `SetOrigin` | OriginX: double, OriginY: double, OriginZ: double | HRESULT |
| `GetOriginAndAxis` | AxisType: seSteeringWheelConstants, [out] OriginX: double*, [out] OriginY: double*, [out] OriginZ: double*, [out] AxisXComponent: double*, [out] AxisYComponent: double*, [out] AxisZComponent: double* | HRESULT |
| `Align` | AxisType: seSteeringWheelConstants, AxisXComponent: double, AxisYComponent: double, AxisZComponent: double | HRESULT |
| `AlignAlongLinerElement` | AxisType: seSteeringWheelConstants, LinearElementToAlignWith: IDispatch | HRESULT |

## RECORD

| Tipo | Membros |
|------|---------|
| [SolidEdgeWorkflowInfo](#solidedgeworkflowinfo) | 0 |
| [SolidEdgeWorkflowQueryInfo](#solidedgeworkflowqueryinfo) | 0 |

### <a name="solidedgeworkflowinfo"></a>`SolidEdgeWorkflowInfo`

_Sem membros documentados._

### <a name="solidedgeworkflowqueryinfo"></a>`SolidEdgeWorkflowQueryInfo`

_Sem membros documentados._
