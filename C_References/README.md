# C# References for Civil 3D Plugin

This directory should contain copies of the following DLLs from your Civil 3D installation.

## Required DLLs

Copy these from your Civil 3D installation directory (typically `C:\Program Files\Autodesk\AutoCAD 2025\`):

| DLL | Purpose |
|-----|---------|
| `accoremgd.dll` | AutoCAD Core Managed |
| `AcDbMgd.dll` | AutoCAD Database Managed |
| `acmgd.dll` | AutoCAD Managed |
| `AecBaseMgd.dll` | AEC Base Managed |
| `AeccDbMgd.dll` | Civil 3D Database Managed |
| `AeccPressurePipesMgd.dll` | Civil 3D Pressure Pipes Managed (pressure pipe networks — a separate object model from gravity pipe networks) |
| `AeccDataShortcutMgd.dll` | Civil 3D Data Shortcuts Managed (`Autodesk.Civil.DataShortcuts` namespace) |

Some Civil 3D features (like pressure pipes and data shortcuts) live in their own managed DLL
rather than `AeccDbMgd.dll`. This project's `C_References/` currently has **all** `*Mgd.dll`
files copied from the Civil3D/AutoCAD install (~30 of them, including several `AcMap*Mgd.dll`
satellite modules from AutoCAD Map 3D) — it's harmless to have extras present, since each is
only wired into the build once actually referenced in `Civil3dMcpPlugin.csproj`. If a future
feature needs a DLL not yet referenced there, check this folder first before assuming it's
missing — it's likely already here.

## Instructions

1. Navigate to your Civil 3D installation directory
2. Copy the DLLs listed above into this `C_References/` directory
3. Build the plugin with `dotnet build` from the `plugin/Civil3dMcpPlugin/` directory

> **Note**: These DLLs are proprietary Autodesk files and must NOT be committed to version control.
> They are already excluded by `.gitignore`.
