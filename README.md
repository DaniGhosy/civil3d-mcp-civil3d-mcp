# Civil 3D MCP Server

An MCP (Model Context Protocol) server that enables AI assistants (Claude, Cline, etc.) to interact with Autodesk Civil 3D through natural language.

## Architecture

```
┌─────────────────┐     stdio      ┌──────────────────┐     TCP/JSON-RPC    ┌──────────────────┐
│   AI Assistant   │ ◄────────────► │  MCP Server (TS) │ ◄──────────────────► │  Civil 3D Plugin │
│ (Claude, Cline)  │               │   Node.js         │     port 8080       │   (.NET 8.0 C#)  │
└─────────────────┘               └──────────────────┘                      └──────────────────┘
                                                                                     │
                                                                              Civil 3D API
                                                                            (Surfaces, Alignments,
                                                                             Points, Corridors...)
```

The system has two components:
1. **MCP Server** (TypeScript/Node.js) — Communicates with AI assistants via MCP protocol
2. **Civil 3D Plugin** (C# .NET 8.0) — Runs inside Civil 3D and executes API commands

## Available Tools

| Tool | Actions | Description |
|------|---------|-------------|
| `civil3d_health` | health, health_verbose (plugin version, build date, drawing name, open document count) | Check plugin connectivity |
| `civil3d_object` | get_properties (curated safe field set for Surface/TinSurface — full reflection hangs there, confirmed in live testing), list_by_type, resolve_location (rejects Surface handles in reference_object mode — no meaningful anchor point), set_style_name, delete_entity, ensure_layer, list_layers, set_layer, move_entity, copy_entity, rotate_entity, get_entity_bounds | Generic reflection-based introspection for any object type, plus resolve_location (coordinates / interactive mouse click / relative to a reference object) to place anything anywhere, plus set_style_name — the generic style engine, works on any object or already-created label with a writable StyleName — the "escape valve" primitives that reduce the need for domain-specific commands. The entity-generic primitives (delete/ensure_layer/list_layers/set_layer/move/copy/rotate/get_bounds) close the test/cleanup loop — before these, there was no way to undo test objects without entering Civil3D manually |
| `civil3d_drawing` | info, settings, save, undo, redo, list_object_types, get_selected | Drawing operations |
| `civil3d_surface` | list, get, get_elevation, get_statistics, create, delete, add_points, add_breakline (real only for breaklineType="standard"), add_boundary, compute_volume, get_area_elevation_table, compute_contour_volume, close_contours_against_boundary (auto-picks the non-self-intersecting tramo for irregular/hairpin boundaries), delete_points, list_triangles, get_triangle_at_point, paste_surface, get_build_options, extract_contours (planned), get_operations (planned), delete_boundary (planned), set_build_options (planned), add_contour_data (planned), swap_edge (planned), minimize_flat_triangles (planned), minimize_convex_triangles (planned), delete_breakline (planned), delete_operation (planned) | Surface management |
| `civil3d_alignment` | list, get, create, delete, station_to_point, point_to_station, list_superelevation_curves, list_superelevation_critical_stations, list_design_speeds | Alignment operations |
| `civil3d_profile` | list, get, get_elevation, create_from_surface, create_layout (no label set yet), list_entities, delete, create_tangent (planned), create_parabola (planned) | Profile management |
| `civil3d_profile_view` | create, list, get, delete, get_bands | Profile views (graphic display of a profile) |
| `civil3d_corridor` | list, get, rebuild, get_surfaces, create_surface_from_corridor_surface, list_baselines, list_baseline_regions, add_baseline_region, get_targets, get_feature_lines, compute_volumes (planned, combine surface actions instead) | Corridor operations (no .NET API for intersections/roundabouts — COM/UI only) |
| `civil3d_sample_line` | create_group, list_groups, create_line, list_lines, delete_group, create_section_view_group, list_section_views, delete_section_view, list_mass_haul_lines, report_quantities, create_mass_haul_line (planned), list_material_lists (planned) | Sample lines, section views, mass haul, quantity takeoff |
| `civil3d_sheet_production` | list_view_frames, list_match_lines | Read-only view frames / match lines — creating them is not exposed by the .NET API (confirmed Autodesk limitation) |
| `civil3d_pipe` | list_networks, get_network, list_pipes, get_pipe, list_structures, get_structure, get_rule_set, get_overridden_rules, create_network (planned), add_pipe (planned), add_structure (planned), list_parts_lists (planned), check_interference (planned) | Gravity pipe networks |
| `civil3d_pressure_pipe` | list_networks (planned), get_network (planned), list_parts (planned), get_part (planned), create_network (planned) | Pressure pipe networks — AeccPressurePipesMgd.dll referenced, real namespace of its API still unconfirmed |
| `civil3d_point` | list (optionally by group), get, create, delete, list_groups, create_group, delete_group, import (PNEZD/PENZD text), export (PNEZD/PENZD text), description_keys (planned) | COGO points |
| `civil3d_data_shortcut` | get_project_id, associate_project, promote_reference, create_reference (planned) | Data shortcuts (shared references between drawings) |
| `civil3d_survey` | list_figure_styles (confirmed to hang 120s in live testing without a diagnosed cause — see CLAUDE.md), list_networks (planned), list_figures (planned) | Survey data (read-only) |
| `civil3d_import_export` | import_landxml (planned — confirmed impossible via .NET API), export_surface_to_landxml (planned), export_to_shapefile (planned) | LandXML / GIS import-export — use civil3d_point import/export for plain-text points instead |
| `civil3d_parcel` | list_sites, list, get, delete, create (planned) | Parcels |
| `civil3d_assembly` | list, get, delete, list_subassemblies, get_subassembly_parameters, set_subassembly_parameter, create (planned) | Corridor assemblies |
| `civil3d_grading` | list_feature_lines, get_feature_line, delete_feature_line, create_feature_line (planned), list_groups (planned), get_group (planned), delete_group (planned), create_group (planned) | Grading feature lines (groups pending real API access) |
| `civil3d_geometry` | create_line, create_polyline, create_3d_polyline, create_text, create_mtext (all 5 auto-create the target layer if missing instead of throwing eKeyNotFound), offset_lines_to_boundary (returns a `warning` field when 0 lines were created, e.g. no intersections found) | Basic AutoCAD geometry |

## Setup

### 1. Build the MCP Server

```bash
npm install
npm run build
```

### 2. Build the Civil 3D Plugin

1. Copy the required DLLs from your Civil 3D installation to `C_References/` (see [C_References/README.md](C_References/README.md))
2. Build the plugin:

```bash
cd plugin/Civil3dMcpPlugin
dotnet build
```

### 3. Load the Plugin in Civil 3D

1. Open Civil 3D 2025+
2. Type `NETLOAD` in the command line
3. Browse to `plugin/Civil3dMcpPlugin/bin/Debug/net8.0-windows/Civil3dMcpPlugin.dll`
4. The plugin starts automatically. Use `C3DMCPSTATUS` to verify.

### 4. Configure Your AI Assistant

**Claude Desktop** — Add to `claude_desktop_config.json`:

```json
{
  "mcpServers": {
    "civil3d": {
      "command": "node",
      "args": ["/path/to/civil3d-mcp/build/index.js"]
    }
  }
}
```

## Environment Variables

| Variable | Default | Description |
|----------|---------|-------------|
| `CIVIL3D_HOST` | `localhost` | Civil 3D plugin host |
| `CIVIL3D_PORT` | `8080` | Civil 3D plugin port |
| `CIVIL3D_CONNECT_TIMEOUT` | `5000` | Connection timeout (ms) |
| `CIVIL3D_COMMAND_TIMEOUT` | `120000` | Command execution timeout (ms) |
| `LOG_LEVEL` | `info` | Log level (debug, info, warn, error) |

## Plugin Commands

| Command | Description |
|---------|-------------|
| `C3DMCPSTART` | Start the TCP listener |
| `C3DMCPSTOP` | Stop the TCP listener |
| `C3DMCPSTATUS` | Check listener status |

## License

MIT
