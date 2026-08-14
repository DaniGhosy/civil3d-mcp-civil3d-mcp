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

      // Pressure Pipe Networks
      "listPressureNetworks" => PressurePipeCommands.ListPressureNetworksAsync(),
      "getPressureNetwork" => PressurePipeCommands.GetPressureNetworkAsync(parameters),
      "listPressureParts" => PressurePipeCommands.ListPressurePartsAsync(parameters),
      "getPressurePart" => PressurePipeCommands.GetPressurePartAsync(parameters),
      "createPressureNetwork" => PressurePipeCommands.CreatePressureNetworkAsync(parameters),

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

      // Sheet Production (view frames / match lines — read-only, creation not exposed by .NET API)
      "listViewFrames" => SheetProductionCommands.ListViewFramesAsync(),
      "listMatchLines" => SheetProductionCommands.ListMatchLinesAsync(),

      // Parcels
      "listParcelSites" => ParcelCommands.ListParcelSitesAsync(),
      "listParcels" => ParcelCommands.ListParcelsAsync(parameters),
      "getParcel" => ParcelCommands.GetParcelAsync(parameters),
      "deleteParcel" => ParcelCommands.DeleteParcelAsync(parameters),
      "createParcel" => ParcelCommands.CreateParcelAsync(parameters),

      // Assemblies
      "listAssemblies" => AssemblyCommands.ListAssembliesAsync(),
      "getAssembly" => AssemblyCommands.GetAssemblyAsync(parameters),
      "listAssemblySubassemblies" => AssemblyCommands.ListSubassembliesAsync(parameters),
      "deleteAssembly" => AssemblyCommands.DeleteAssemblyAsync(parameters),
      "createAssembly" => AssemblyCommands.CreateAssemblyAsync(parameters),
      "getSubassemblyParameters" => AssemblyCommands.GetSubassemblyParametersAsync(parameters),
      "setSubassemblyParameter" => AssemblyCommands.SetSubassemblyParameterAsync(parameters),

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

      // Data Shortcuts
      "getDataShortcutProjectId" => DataShortcutCommands.GetDataShortcutProjectIdAsync(parameters),
      "associateDataShortcutProject" => DataShortcutCommands.AssociateDataShortcutProjectAsync(parameters),
      "createDataReference" => DataShortcutCommands.CreateDataReferenceAsync(parameters),
      "promoteDataReference" => DataShortcutCommands.PromoteDataReferenceAsync(parameters),

      // Survey
      "listSurveyFigureStyles" => SurveyCommands.ListSurveyFigureStylesAsync(),
      "listSurveyNetworks" => SurveyCommands.ListSurveyNetworksAsync(),
      "listSurveyFigures" => SurveyCommands.ListSurveyFiguresAsync(),

      // Import/Export (LandXML/GIS)
      "importLandXml" => ImportExportCommands.ImportLandXmlAsync(parameters),
      "exportSurfaceToLandXml" => ImportExportCommands.ExportSurfaceToLandXmlAsync(parameters),
      "exportToShapefile" => ImportExportCommands.ExportToShapefileAsync(parameters),

      // Unknown method
      _ => throw new JsonRpcDispatchException(
        "CIVIL3D.INVALID_INPUT",
        $"Plugin method '{method}' is not implemented yet."
      ),
    };
  }
}
