using System.Text.Json.Nodes;

namespace Civil3DMcpPlugin;

/// <summary>
/// Routes JSON-RPC method names to the appropriate command handler.
/// Each method string maps directly to a static async method.
/// </summary>
public static class CommandDispatcher
{
  public static Task<object?> DispatchAsync(
    string method,
    JsonObject? parameters,
    CancellationToken cancellationToken)
  {
    return method switch
    {
      // Plugin / Health
      "getCivil3DHealth" => DrawingCommands.GetCivil3DHealthAsync(),
      "getCivil3DHealthVerbose" => DrawingCommands.GetCivil3DHealthVerboseAsync(),

      // Drawing operations
      "getDrawingInfo" => DrawingCommands.GetDrawingInfoAsync(),
      "getDrawingSettings" => DrawingCommands.GetDrawingSettingsAsync(),
      "saveDrawing" => DrawingCommands.SaveDrawingAsync(parameters),
      "newDrawing" => DrawingCommands.NewDrawingAsync(parameters),
      "undoDrawing" => DrawingCommands.UndoDrawingAsync(parameters),
      "redoDrawing" => DrawingCommands.RedoDrawingAsync(parameters),
      "listCivilObjectTypes" => DrawingCommands.ListCivilObjectTypesAsync(),
      "getSelectedCivilObjectsInfo" => DrawingCommands.GetSelectedCivilObjectsInfoAsync(parameters),

      // Geometry (AutoCAD)
      "createLineSegment" => GeometryCommands.CreateLineSegmentAsync(parameters),
      "createPolyline" => GeometryCommands.CreatePolylineAsync(parameters),
      "create3dPolyline" => GeometryCommands.Create3dPolylineAsync(parameters),
      "createText" => GeometryCommands.CreateTextAsync(parameters),
      "createMText" => GeometryCommands.CreateMTextAsync(parameters),
      "offsetLinesToBoundary" => GeometryCommands.OffsetLinesToBoundaryAsync(parameters),

      // COGO Points
      "listCogoPoints" => PointCommands.ListCogoPointsAsync(parameters),
      "getCogoPoint" => PointCommands.GetCogoPointAsync(parameters),
      "createCogoPoints" => PointCommands.CreateCogoPointsAsync(parameters),
      "deleteCogoPoints" => PointCommands.DeleteCogoPointsAsync(parameters),
      "listPointGroups" => PointCommands.ListPointGroupsAsync(),
      "createPointGroup" => PointCommands.CreatePointGroupAsync(parameters),
      "deletePointGroup" => PointCommands.DeletePointGroupAsync(parameters),
      "updatePointGroup" => PointCommands.UpdatePointGroupAsync(parameters),
      "transformCogoPoints" => PointCommands.TransformCogoPointsAsync(parameters),
      "getDescriptionKeySets" => PointCommands.GetDescriptionKeySetsAsync(parameters),
      "importCogoPoints" => PointCommands.ImportCogoPointsAsync(parameters),
      "exportCogoPoints" => PointCommands.ExportCogoPointsAsync(parameters),

      // Surfaces
      "listSurfaces" => SurfaceCommands.ListSurfacesAsync(),
      "getSurface" => SurfaceCommands.GetSurfaceAsync(parameters),
      "getSurfaceElevation" => SurfaceCommands.GetSurfaceElevationAsync(parameters),
      "getSurfaceStatistics" => SurfaceCommands.GetSurfaceStatisticsAsync(parameters),
      "createSurface" => SurfaceCommands.CreateSurfaceAsync(parameters),
      "deleteSurface" => SurfaceCommands.DeleteSurfaceAsync(parameters),
      "addSurfacePoints" => SurfaceCommands.AddSurfacePointsAsync(parameters),
      "addSurfaceBreakline" => SurfaceCommands.AddSurfaceBreaklineAsync(parameters),
      "addSurfaceBoundary" => SurfaceCommands.AddSurfaceBoundaryAsync(parameters),
      "extractSurfaceContours" => SurfaceCommands.ExtractSurfaceContoursAsync(parameters),
      "computeSurfaceVolume" => SurfaceCommands.ComputeSurfaceVolumeAsync(parameters),
      "calculateSurfaceVolume" => SurfaceCommands.CalculateSurfaceVolumeAsync(parameters),
      "getSurfaceVolumeReport" => SurfaceCommands.GetSurfaceVolumeReportAsync(parameters),
      "calculateSurfaceVolumeByRegion" => SurfaceCommands.CalculateSurfaceVolumeByRegionAsync(parameters),
      "analyzeSurfaceSlope" => SurfaceCommands.AnalyzeSurfaceSlopeAsync(parameters),
      "analyzeSurfaceElevation" => SurfaceCommands.AnalyzeSurfaceElevationAsync(parameters),
      "analyzeSurfaceDirections" => SurfaceCommands.AnalyzeSurfaceDirectionsAsync(parameters),
      "addSurfaceWatershed" => SurfaceCommands.AddSurfaceWatershedsAsync(parameters),
      "addSurfaceWatersheds" => SurfaceCommands.AddSurfaceWatershedsAsync(parameters),
      "setSurfaceContourInterval" => SurfaceCommands.SetSurfaceContourIntervalAsync(parameters),
      "getSurfaceStatisticsDetailed" => SurfaceCommands.GetSurfaceStatisticsDetailedAsync(parameters),
      "sampleSurfaceElevations" => SurfaceCommands.SampleSurfaceElevationsAsync(parameters),
      "createSurfaceFromDem" => SurfaceCommands.CreateSurfaceFromDemAsync(parameters),
      "getSurfaceAreaElevationTable" => SurfaceCommands.GetSurfaceAreaElevationTableAsync(parameters),
      "computeContourVolume" => SurfaceCommands.ComputeContourVolumeAsync(parameters),
      "closeContoursAgainstBoundary" => SurfaceCommands.CloseContoursAgainstBoundaryAsync(parameters),
      "deleteSurfacePoints" => SurfaceCommands.DeleteSurfacePointsAsync(parameters),
      "listSurfaceTriangles" => SurfaceCommands.ListSurfaceTrianglesAsync(parameters),
      "getSurfaceTriangleAtPoint" => SurfaceCommands.GetSurfaceTriangleAtPointAsync(parameters),
      "pasteSurface" => SurfaceCommands.PasteSurfaceAsync(parameters),
      "getSurfaceOperations" => SurfaceCommands.GetSurfaceOperationsAsync(parameters),
      "deleteSurfaceBoundary" => SurfaceCommands.DeleteSurfaceBoundaryAsync(parameters),
      "getSurfaceBuildOptions" => SurfaceCommands.GetSurfaceBuildOptionsAsync(parameters),
      "setSurfaceBuildOptions" => SurfaceCommands.SetSurfaceBuildOptionsAsync(parameters),
      "addSurfaceContourData" => SurfaceCommands.AddSurfaceContourDataAsync(parameters),
      "swapSurfaceEdge" => SurfaceCommands.SwapSurfaceEdgeAsync(parameters),
      "minimizeFlatTriangles" => SurfaceCommands.MinimizeFlatTrianglesAsync(parameters),
      "minimizeConvexTriangles" => SurfaceCommands.MinimizeConvexTrianglesAsync(parameters),
      "deleteSurfaceBreakline" => SurfaceCommands.DeleteSurfaceBreaklineAsync(parameters),
      "deleteSurfaceOperation" => SurfaceCommands.DeleteSurfaceOperationAsync(parameters),

      // Alignments
      "listAlignments" => AlignmentCommands.ListAlignmentsAsync(),
      "getAlignment" => AlignmentCommands.GetAlignmentAsync(parameters),
      "createAlignment" => AlignmentCommands.CreateAlignmentAsync(parameters),
      "deleteAlignment" => AlignmentCommands.DeleteAlignmentAsync(parameters),
      "alignmentStationToPoint" => AlignmentCommands.StationToPointAsync(parameters),
      "alignmentPointToStation" => AlignmentCommands.PointToStationAsync(parameters),
      "listSuperelevationCurves" => AlignmentCommands.ListSuperelevationCurvesAsync(parameters),
      "listSuperelevationCriticalStations" => AlignmentCommands.ListSuperelevationCriticalStationsAsync(parameters),
      "listDesignSpeeds" => AlignmentCommands.ListDesignSpeedsAsync(parameters),

      // Profiles
      "listProfiles" => ProfileCommands.ListProfilesAsync(parameters),
      "getProfile" => ProfileCommands.GetProfileAsync(parameters),
      "getProfileElevation" => ProfileCommands.GetProfileElevationAsync(parameters),
      "createProfileFromSurface" => ProfileCommands.CreateProfileFromSurfaceAsync(parameters),
      "createLayoutProfile" => ProfileCommands.CreateLayoutProfileAsync(parameters),
      "addProfileTangent" => ProfileCommands.AddProfileTangentAsync(parameters),
      "addProfileParabola" => ProfileCommands.AddProfileParabolaAsync(parameters),
      "listProfileEntities" => ProfileCommands.ListProfileEntitiesAsync(parameters),
      "deleteProfile" => ProfileCommands.DeleteProfileAsync(parameters),

      // Profile Views
      "createProfileView" => ProfileViewCommands.CreateProfileViewAsync(parameters),
      "listProfileViews" => ProfileViewCommands.ListProfileViewsAsync(),
      "getProfileView" => ProfileViewCommands.GetProfileViewAsync(parameters),
      "deleteProfileView" => ProfileViewCommands.DeleteProfileViewAsync(parameters),
      "getProfileViewBands" => ProfileViewCommands.GetProfileViewBandsAsync(parameters),

      // Corridors
      "listCorridors" => CorridorCommands.ListCorridorsAsync(),
      "getCorridor" => CorridorCommands.GetCorridorAsync(parameters),
      "rebuildCorridor" => CorridorCommands.RebuildCorridorAsync(parameters),
      "getCorridorSurfaces" => CorridorCommands.GetCorridorSurfacesAsync(parameters),
      "getCorridorFeatureLines" => CorridorCommands.GetCorridorFeatureLinesAsync(parameters),
      "computeCorridorVolumes" => CorridorCommands.ComputeCorridorVolumesAsync(parameters),
      "listBaselines" => CorridorCommands.ListBaselinesAsync(parameters),
      "listBaselineRegions" => CorridorCommands.ListBaselineRegionsAsync(parameters),
      "addBaselineRegion" => CorridorCommands.AddBaselineRegionAsync(parameters),
      "getCorridorTargets" => CorridorCommands.GetCorridorTargetsAsync(parameters),
      "createSurfaceFromCorridorSurface" => CorridorCommands.CreateSurfaceFromCorridorSurfaceAsync(parameters),
      "setCorridorTargetMappings" => CorridorEditingCommands.SetCorridorTargetMappingsAsync(parameters),
      "deleteCorridorRegion" => CorridorEditingCommands.DeleteCorridorRegionAsync(parameters),

      // Pipe Networks
      "listPipeNetworks" => PipeNetworkCommands.ListPipeNetworksAsync(),
      "getPipeNetwork" => PipeNetworkCommands.GetPipeNetworkAsync(parameters),
      "getPipe" => PipeNetworkCommands.GetPipeAsync(parameters),
      "getStructure" => PipeNetworkCommands.GetStructureAsync(parameters),
      "createPipeNetwork" => PipeNetworkCommands.CreatePipeNetworkAsync(parameters),
      "addPipeToNetwork" => PipeNetworkCommands.AddPipeToNetworkAsync(parameters),
      "addStructureToNetwork" => PipeNetworkCommands.AddStructureToNetworkAsync(parameters),
      "checkPipeNetworkInterference" => PipeNetworkCommands.CheckPipeNetworkInterferenceAsync(parameters),
      "listPipes" => PipeNetworkCommands.ListPipesAsync(parameters),
      "listStructures" => PipeNetworkCommands.ListStructuresAsync(parameters),
      "listPartsLists" => PipeNetworkCommands.ListPartsListsAsync(),
      "getPipeRuleSet" => PipeNetworkCommands.GetPipeRuleSetAsync(parameters),
      "getPipeOverriddenRules" => PipeNetworkCommands.GetPipeOverriddenRulesAsync(parameters),
      "resizePipeInNetwork" => PipeNetworkCommands.ResizePipeInNetworkAsync(parameters),
      "calculatePipeNetworkHgl" => PipeHydraulicsCommands.CalculatePipeNetworkHglAsync(parameters),
      "analyzePipeNetworkHydraulics" => PipeHydraulicsCommands.AnalyzePipeNetworkHydraulicsAsync(parameters),
      "getPipeStructureProperties" => PipeHydraulicsCommands.GetPipeStructurePropertiesAsync(parameters),

      // Pressure Pipe Networks
      "listPressureNetworks" => PressurePipeCommands.ListPressureNetworksAsync(),
      "getPressureNetwork" => PressurePipeCommands.GetPressureNetworkAsync(parameters),
      "listPressureParts" => PressurePipeCommands.ListPressurePartsAsync(parameters),
      "getPressurePart" => PressurePipeCommands.GetPressurePartAsync(parameters),
      "createPressureNetwork" => PressurePipeCommands.CreatePressureNetworkAsync(parameters),
      "deletePressureNetwork" => PressurePipeCommands.DeletePressureNetworkAsync(parameters),
      "assignPressurePartsList" => PressurePipeCommands.AssignPressurePartsListAsync(parameters),
      "setPressureNetworkCover" => PressurePipeCommands.SetPressureNetworkCoverAsync(parameters),
      "validatePressureNetwork" => PressurePipeCommands.ValidatePressureNetworkAsync(parameters),
      "exportPressureNetwork" => PressurePipeCommands.ExportPressureNetworkAsync(parameters),
      "connectPressureNetworks" => PressurePipeCommands.ConnectPressureNetworksAsync(parameters),
      "addPressurePipe" => PressurePipeCommands.AddPressurePipeAsync(parameters),
      "getPressurePipeProperties" => PressurePipeCommands.GetPressurePipePropertiesAsync(parameters),
      "resizePressurePipe" => PressurePipeCommands.ResizePressurePipeAsync(parameters),
      "addPressureFitting" => PressurePipeCommands.AddPressureFittingAsync(parameters),
      "getPressureFittingProperties" => PressurePipeCommands.GetPressureFittingPropertiesAsync(parameters),
      "addPressureAppurtenance" => PressurePipeCommands.AddPressureAppurtenanceAsync(parameters),

      // Sample Lines / Section Views / Mass Haul / QTO
      "createSampleLineGroup" => SampleLineCommands.CreateSampleLineGroupAsync(parameters),
      "listSampleLineGroups" => SampleLineCommands.ListSampleLineGroupsAsync(),
      "createSampleLine" => SampleLineCommands.CreateSampleLineAsync(parameters),
      "listSampleLines" => SampleLineCommands.ListSampleLinesAsync(parameters),
      "deleteSampleLineGroup" => SampleLineCommands.DeleteSampleLineGroupAsync(parameters),
      "createSectionViewGroup" => SampleLineCommands.CreateSectionViewGroupAsync(parameters),
      "listSectionViews" => SampleLineCommands.ListSectionViewsAsync(),
      "deleteSectionView" => SampleLineCommands.DeleteSectionViewAsync(parameters),
      "listMassHaulLines" => SampleLineCommands.ListMassHaulLinesAsync(),
      "createMassHaulLine" => SampleLineCommands.CreateMassHaulLineAsync(parameters),
      "reportQuantities" => SampleLineCommands.ReportQuantitiesAsync(parameters),
      "listMaterialLists" => SampleLineCommands.ListMaterialListsAsync(),
      "getSectionData" => SampleLineCommands.GetSectionDataAsync(parameters),
      "createSectionViews" => SampleLineCommands.CreateSectionViewsAsync(parameters),
      "updateSectionViewStyles" => SampleLineCommands.UpdateSectionViewStylesAsync(parameters),
      "exportSectionData" => SampleLineCommands.ExportSectionDataAsync(parameters),

      // Sheet Production (view frames / match lines — read-only, creation not exposed by .NET API)
      "listViewFrames" => SheetProductionCommands.ListViewFramesAsync(),
      "listMatchLines" => SheetProductionCommands.ListMatchLinesAsync(),
      "listSheetSets" => SheetProductionCommands.ListSheetSetsAsync(),
      "getSheetSetInfo" => SheetProductionCommands.GetSheetSetInfoAsync(parameters),
      "addSheet" => SheetProductionCommands.AddSheetAsync(parameters),
      "getSheetProperties" => SheetProductionCommands.GetSheetPropertiesAsync(parameters),
      "setSheetTitleBlock" => SheetProductionCommands.SetSheetTitleBlockAsync(parameters),
      "updatePlanProfileSheetAlignment" => SheetProductionCommands.UpdatePlanProfileSheetAlignmentAsync(parameters),
      "createSheetView" => SheetProductionCommands.CreateSheetViewAsync(parameters),
      "setSheetViewScale" => SheetProductionCommands.SetSheetViewScaleAsync(parameters),

      // Parcels
      "listParcelSites" => ParcelCommands.ListParcelSitesAsync(),
      "listParcels" => ParcelCommands.ListParcelsAsync(parameters),
      "getParcel" => ParcelCommands.GetParcelAsync(parameters),
      "deleteParcel" => ParcelCommands.DeleteParcelAsync(parameters),
      "createParcel" => ParcelCommands.CreateParcelAsync(parameters),
      "editParcel" => ParcelEditingCommands.EditParcelAsync(parameters),
      "adjustParcelLotLine" => ParcelEditingCommands.AdjustParcelLotLineAsync(parameters),
      "reportParcels" => ParcelEditingCommands.ReportParcelsAsync(parameters),

      // Assemblies
      "listAssemblies" => AssemblyCommands.ListAssembliesAsync(),
      "getAssembly" => AssemblyCommands.GetAssemblyAsync(parameters),
      "listAssemblySubassemblies" => AssemblyCommands.ListSubassembliesAsync(parameters),
      "deleteAssembly" => AssemblyCommands.DeleteAssemblyAsync(parameters),
      "createAssembly" => AssemblyCommands.CreateAssemblyAsync(parameters),
      "getSubassemblyParameters" => AssemblyCommands.GetSubassemblyParametersAsync(parameters),
      "setSubassemblyParameter" => AssemblyCommands.SetSubassemblyParameterAsync(parameters),
      "createSubassembly" => AssemblyCommands.CreateSubassemblyAsync(parameters),
      "editAssembly" => AssemblyCommands.EditAssemblyAsync(parameters),

      // Generic / Núcleo genérico
      "getObjectProperties" => GenericObjectCommands.GetObjectPropertiesAsync(parameters),
      "listObjectsByType" => GenericObjectCommands.ListObjectsByTypeAsync(parameters),
      "resolveLocation" => GenericObjectCommands.ResolveLocationAsync(parameters),
      "setObjectStyle" => GenericObjectCommands.SetObjectStyleAsync(parameters),
      "deleteEntity" => GenericObjectCommands.DeleteEntityAsync(parameters),
      "ensureLayer" => GenericObjectCommands.EnsureLayerAsync(parameters),
      "listLayers" => GenericObjectCommands.ListLayersAsync(),
      "setLayer" => GenericObjectCommands.SetLayerAsync(parameters),
      "moveEntity" => GenericObjectCommands.MoveEntityAsync(parameters),
      "copyEntity" => GenericObjectCommands.CopyEntityAsync(parameters),
      "rotateEntity" => GenericObjectCommands.RotateEntityAsync(parameters),
      "getEntityBounds" => GenericObjectCommands.GetEntityBoundsAsync(parameters),

      // Blocks (Módulo A — lectura de planos: inventario de bloques)
      "listBlockDefinitions" => BlockCommands.ListBlockDefinitionsAsync(),
      "countBlocksByName" => BlockCommands.CountBlocksByNameAsync(parameters),
      "getBlockAttributes" => BlockCommands.GetBlockAttributesAsync(parameters),
      "listBlocksByLayer" => BlockCommands.ListBlocksByLayerAsync(parameters),
      "getBlockInsertionPoints" => BlockCommands.GetBlockInsertionPointsAsync(parameters),
      "listDynamicBlockStates" => BlockCommands.ListDynamicBlockStatesAsync(parameters),

      // Labels (Módulo B — lectura de planos: texto y anotaciones)
      "extractTextEntities" => LabelCommands.ExtractTextEntitiesAsync(parameters),
      "extractLeaderAnnotations" => LabelCommands.ExtractLeaderAnnotationsAsync(),
      "extractDimensions" => LabelCommands.ExtractDimensionsAsync(parameters),

      // Legend (Módulo C — lectura de planos: simbología y leyenda)
      "readLegendTable" => LegendCommands.ReadLegendTableAsync(parameters),

      // Shape Detection (Módulo D — lectura de planos: geometría cruda / heurística)
      "detectParallelLinePairs" => ShapeDetectionCommands.DetectParallelLinePairsAsync(parameters),
      "groupEntitiesByProximity" => ShapeDetectionCommands.GroupEntitiesByProximityAsync(parameters),
      "getEntityExtendedData" => ShapeDetectionCommands.GetEntityExtendedDataAsync(parameters),

      // Grading
      "listGradingGroups" => GradingCommands.ListGradingGroupsAsync(),
      "getGradingGroup" => GradingCommands.GetGradingGroupAsync(parameters),
      "deleteGradingGroup" => GradingCommands.DeleteGradingGroupAsync(parameters),
      "createGradingGroup" => GradingCommands.CreateGradingGroupAsync(parameters),
      "listFeatureLines" => GradingCommands.ListFeatureLinesAsync(),
      "getFeatureLine" => GradingCommands.GetFeatureLineAsync(parameters),
      "deleteFeatureLine" => GradingCommands.DeleteFeatureLineAsync(parameters),
      "createFeatureLine" => GradingCommands.CreateFeatureLineAsync(parameters),
      "getGradingGroupVolume" => GradingCommands.GetGradingGroupVolumeAsync(parameters),
      "createSurfaceFromGradingGroup" => GradingCommands.CreateSurfaceFromGradingGroupAsync(parameters),
      "listGradings" => GradingCommands.ListGradingsAsync(parameters),
      "getGrading" => GradingCommands.GetGradingAsync(parameters),
      "createGrading" => GradingCommands.CreateGradingAsync(parameters),
      "deleteGrading" => GradingCommands.DeleteGradingAsync(parameters),
      "listGradingCriteria" => GradingCommands.ListGradingCriteriaAsync(),

      // Data Shortcuts
      "getDataShortcutProjectId" => DataShortcutCommands.GetDataShortcutProjectIdAsync(parameters),
      "associateDataShortcutProject" => DataShortcutCommands.AssociateDataShortcutProjectAsync(parameters),
      "createDataReference" => DataShortcutCommands.CreateDataReferenceAsync(parameters),
      "promoteDataReference" => DataShortcutCommands.PromoteDataReferenceAsync(parameters),
      "listDataShortcuts" => DataShortcutCommands.ListDataShortcutsAsync(),
      "createDataShortcut" => DataShortcutCommands.CreateDataShortcutAsync(parameters),
      "referenceDataShortcut" => DataShortcutCommands.ReferenceDataShortcutAsync(parameters),
      "syncDataShortcuts" => DataShortcutCommands.SyncDataShortcutsAsync(parameters),
      "promoteDataShortcut" => DataShortcutCommands.PromoteDataShortcutAsync(parameters),

      // Survey
      // Quantity takeoff
      // Jobs (async, background)
      "startJob" => JobCommands.StartJobAsync(parameters),
      "getJobStatus" => JobCommands.GetJobStatusAsync(parameters),
      "cancelJob" => JobCommands.CancelJobAsync(parameters),

      // Workflows
      "corridorQcReportWorkflow" => WorkflowCommands.CorridorQcReportWorkflowAsync(parameters),
      "surfaceComparisonReportWorkflow" => WorkflowCommands.SurfaceComparisonReportWorkflowAsync(parameters),
      "dataShortcutPublishSyncWorkflow" => WorkflowCommands.DataShortcutPublishSyncWorkflowAsync(parameters),
      "dataShortcutReferenceSyncWorkflow" => WorkflowCommands.DataShortcutReferenceSyncWorkflowAsync(parameters),
      "projectStartupWorkflow" => WorkflowCommands.ProjectStartupWorkflowAsync(parameters),
      "projectReferenceSetupWorkflow" => WorkflowCommands.ProjectReferenceSetupWorkflowAsync(parameters),
      "drawingReadinessAuditWorkflow" => WorkflowCommands.DrawingReadinessAuditWorkflowAsync(parameters),
      "featureLineToGradingWorkflow" => WorkflowCommands.FeatureLineToGradingWorkflowAsync(parameters),
      "qcFixAndVerifyWorkflow" => WorkflowCommands.QcFixAndVerifyWorkflowAsync(parameters),

      "qtySurfaceVolume" => QuantityCommands.QtySurfaceVolumeAsync(parameters),
      "qtyPipeNetworkLengths" => QuantityCommands.QtyPipeNetworkLengthsAsync(parameters),
      "qtyPressureNetworkLengths" => QuantityCommands.QtyPressureNetworkLengthsAsync(parameters),
      "qtyParcelAreas" => QuantityCommands.QtyParcelAreasAsync(parameters),
      "qtyAlignmentLengths" => QuantityCommands.QtyAlignmentLengthsAsync(parameters),
      "qtyPointCountByGroup" => QuantityCommands.QtyPointCountByGroupAsync(parameters),
      "qtyExportToCsv" => QuantityCommands.QtyExportToCsvAsync(parameters),
      "qtyEarthworkSummary" => QuantityCommands.QtyEarthworkSummaryAsync(parameters),

      "listLabelStyles" => LabelStyleCommands.ListLabelStylesAsync(parameters),
      "listLabels" => LabelStyleCommands.ListLabelsAsync(parameters),
      "addLabel" => LabelStyleCommands.AddLabelAsync(parameters),
      "listStyles" => StyleCommands.ListStylesAsync(parameters),
      "getStyle" => StyleCommands.GetStyleAsync(parameters),

      "listSurveyFigureStyles" => SurveyCommands.ListSurveyFigureStylesAsync(),
      "listSurveyNetworks" => CogoCommands.ListSurveyNetworksAsync(parameters),
      "listSurveyFigures" => CogoCommands.ListSurveyFiguresAsync(parameters),
      "listSurveyDatabases" => CogoCommands.ListSurveyDatabasesAsync(),
      "getSurveyFigure" => CogoCommands.GetSurveyFigureAsync(parameters),
      "listSurveyObservations" => CogoCommands.ListSurveyObservationsAsync(parameters),

      // COGO (Coordinate Geometry)
      "cogoInverse" => CogoCommands.CogoInverseAsync(parameters),
      "cogoDirectionDistance" => CogoCommands.CogoDirectionDistanceAsync(parameters),
      "cogoTraverse" => CogoCommands.CogoTraverseAsync(parameters),
      "cogoCurveSolve" => CogoCommands.CogoCurveSolveAsync(parameters),

      // Import/Export (LandXML/GIS)
      "importLandXml" => ImportExportCommands.ImportLandXmlAsync(parameters),
      "exportSurfaceToLandXml" => ImportExportCommands.ExportSurfaceToLandXmlAsync(parameters),
      "exportToShapefile" => ImportExportCommands.ExportToShapefileAsync(parameters),

      // Coordinate System (portado de Civil3D-mcp-main)
      "getCoordinateSystemInfo" => CoordinateSystemCommands.GetCoordinateSystemInfoAsync(),
      "transformCoordinates" => CoordinateSystemCommands.TransformCoordinatesAsync(parameters),

      // Detention (portado de Civil3D-mcp-main)
      "calculateDetentionBasinSize" => DetentionCommands.CalculateDetentionBasinSizeAsync(parameters),
      "calculateDetentionStageStorage" => DetentionCommands.CalculateDetentionStageStorageAsync(parameters),

      // Intersection (portado de Civil3D-mcp-main)
      "listIntersections" => IntersectionCommands.ListIntersectionsAsync(parameters),
      "createIntersection" => IntersectionCommands.CreateIntersectionAsync(parameters),
      "getIntersection" => IntersectionCommands.GetIntersectionAsync(parameters),

      // Sight Distance (portado de Civil3D-mcp-main)
      "calculateSightDistance" => SightDistanceCommands.CalculateSightDistanceAsync(parameters),
      "checkStoppingDistance" => SightDistanceCommands.CheckStoppingDistanceAsync(parameters),

      // Slope Analysis (portado de Civil3D-mcp-main)
      "calculateSlopeGeometry" => SlopeAnalysisCommands.CalculateSlopeGeometryAsync(parameters),
      "checkSlopeStability" => SlopeAnalysisCommands.CheckSlopeStabilityAsync(parameters),

      // Hydrology (portado de Civil3D-mcp-main). runoffPipeWorkflow pendiente (Lote 6 / PipeHydraulicsCommands).
      "listHydrologyCapabilities" => HydrologyCommands.ListHydrologyCapabilitiesAsync(),
      "traceHydrologyFlowPath" => HydrologyCommands.TraceFlowPathAsync(parameters),
      "findHydrologyLowPoint" => HydrologyCommands.FindLowPointAsync(parameters),
      "estimateHydrologyRunoff" => HydrologyCommands.EstimateRunoffAsync(parameters),
      "delineateWatershed" => HydrologyCommands.DelineateWatershedAsync(parameters),
      "calculateCatchmentArea" => HydrologyCommands.CalculateCatchmentAreaAsync(parameters),
      "watershedRunoffWorkflow" => HydrologyCommands.WatershedRunoffWorkflowAsync(parameters),
      "runoffDetentionWorkflow" => HydrologyCommands.RunoffDetentionWorkflowAsync(parameters),
      "runoffPipeWorkflow" => HydrologyCommands.RunoffPipeWorkflowAsync(parameters),

      // Catchment (portado de Civil3D-mcp-main)
      "listCatchmentGroups" => CatchmentCommands.ListCatchmentGroupsAsync(),
      "getCatchmentGroup" => CatchmentCommands.GetCatchmentGroupAsync(parameters),
      "listCatchments" => CatchmentCommands.ListCatchmentsAsync(),
      "getCatchmentProperties" => CatchmentCommands.GetCatchmentPropertiesAsync(parameters),
      "setCatchmentProperties" => CatchmentCommands.SetCatchmentPropertiesAsync(parameters),
      "copyCatchmentToGroup" => CatchmentCommands.CopyCatchmentToGroupAsync(parameters),
      "getCatchmentFlowPath" => CatchmentCommands.GetCatchmentFlowPathAsync(parameters),
      "getCatchmentBoundary" => CatchmentCommands.GetCatchmentBoundaryAsync(parameters),

      // Time of Concentration / Hydrograph (portado de Civil3D-mcp-main)
      "calculateTimeOfConcentration" => TimeOfConcentrationCommands.CalculateTimeOfConcentrationAsync(parameters),
      "generateHydrograph" => TimeOfConcentrationCommands.GenerateHydrographAsync(parameters),
      "listTcMethods" => TimeOfConcentrationCommands.ListTcMethodsAsync(),

      // STM / Storm and Sanitary Analysis (portado de Civil3D-mcp-main)
      "listSsaCapabilities" => StmCommands.ListSsaCapabilitiesAsync(),
      "exportStm" => StmCommands.ExportStmAsync(parameters),
      "importStm" => StmCommands.ImportStmAsync(parameters),
      "openStormSanitaryAnalysis" => StmCommands.OpenStormSanitaryAnalysisAsync(parameters),

      // Cost Estimation (portado de Civil3D-mcp-main)
      "exportPayItems" => CostEstimationCommands.ExportPayItemsAsync(parameters),
      "calculateMaterialCostEstimate" => CostEstimationCommands.CalculateMaterialCostEstimateAsync(parameters),

      // Superelevation (portado de Civil3D-mcp-main)
      "getSuperelevation" => SuperelevationCommands.GetSuperelevationAsync(parameters),
      "setSuperelevation" => SuperelevationCommands.SetSuperelevationAsync(parameters),
      "checkSuperelevationDesign" => SuperelevationCommands.CheckSuperelevationDesignAsync(parameters),
      "generateSuperelevationReport" => SuperelevationCommands.GenerateSuperelevationReportAsync(parameters),

      // Alignment editing (portado de Civil3D-mcp-main)
      "alignmentAddTangent" => AlignmentEditCommands.AddTangentAsync(parameters),
      "alignmentAddCurve" => AlignmentEditCommands.AddCurveAsync(parameters),
      "alignmentAddSpiral" => AlignmentEditCommands.AddSpiralAsync(parameters),
      "alignmentDeleteEntity" => AlignmentEditCommands.DeleteEntityAsync(parameters),
      "alignmentSetStationEquation" => AlignmentEditCommands.SetStationEquationAsync(parameters),
      "alignmentGetStationOffset" => AlignmentEditCommands.GetStationOffsetAsync(parameters),
      "alignmentOffsetCreate" => AlignmentEditCommands.OffsetCreateAsync(parameters),
      "alignmentWidenTransition" => AlignmentEditCommands.WidenTransitionAsync(parameters),

      // Profile editing (portado de Civil3D-mcp-main)
      "profileAddPvi" => ProfileEditCommands.AddPviAsync(parameters),
      "profileDeletePvi" => ProfileEditCommands.DeletePviAsync(parameters),
      "profileAddCurve" => ProfileEditCommands.AddCurveAsync(parameters),
      "profileSetGrade" => ProfileEditCommands.SetGradeAsync(parameters),
      "profileCheckKValues" => ProfileEditCommands.CheckKValuesAsync(parameters),

      // QC (portado de Civil3D-mcp-main)
      "qcCheckAlignment" => QcCommands.QcCheckAlignmentAsync(parameters),
      "qcCheckProfile" => QcCommands.QcCheckProfileAsync(parameters),
      "qcCheckCorridor" => QcCommands.QcCheckCorridorAsync(parameters),
      "qcCheckPipeNetwork" => QcCommands.QcCheckPipeNetworkAsync(parameters),
      "qcCheckSurface" => QcCommands.QcCheckSurfaceAsync(parameters),
      "qcCheckLabels" => QcCommands.QcCheckLabelsAsync(parameters),
      "qcReportGenerate" => QcCommands.QcReportGenerateAsync(parameters),
      "qcCheckDrawingStandards" => QcCommands.QcCheckDrawingStandardsAsync(parameters),
      "qcFixDrawingStandards" => QcCommands.QcFixDrawingStandardsAsync(parameters),

      // Unknown method
      _ => throw new JsonRpcDispatchException(
        "CIVIL3D.INVALID_INPUT",
        $"Plugin method '{method}' is not implemented yet."
      ),
    };
  }
}
