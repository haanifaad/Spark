using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;

namespace OpenClaw.Shared.Creative;

/// <summary>
/// Status of an application's integration into SPARK Creativity Mode.
/// Strictly truthful status indicators as required by SPARK specifications.
/// </summary>
public enum CreativeIntegrationStatus
{
    NotInspected,
    Inspected,
    ArchitectureReady,
    IntegrationInProgress,
    PartiallyIntegrated,
    Integrated,
    Tested,
    Blocked,
    Unsupported
}

/// <summary>
/// Execution and hosting mode for creative applications.
/// </summary>
public enum CreativeIntegrationMode
{
    NativeWindow,
    EmbeddedHost,
    HeadlessCli,
    McpManagedProcess,
    WebAssemblyView,
    ExternalProcessCoordinated
}

/// <summary>
/// Complete metadata record describing a SPARK creative application.
/// </summary>
public sealed record CreativeAppDescriptor
{
    public required string ApplicationId { get; init; }
    public required string DisplayName { get; init; }
    public required string UpstreamName { get; init; }
    public required string PrimaryCapability { get; init; }
    public required string Category { get; init; }
    public required string Description { get; init; }
    public required string MascotMotif { get; init; }
    public required string IconAssetPath { get; init; }
    public required string SourceArchiveName { get; init; }
    public required string RelativeSourcePath { get; init; }
    public required string BinaryExecutableName { get; init; }
    public required string CliExecutableName { get; init; }
    public required CreativeIntegrationMode IntegrationMode { get; init; }
    public required CreativeIntegrationStatus Status { get; init; }
    public required string Version { get; init; }
    public required string License { get; init; }
    public required IReadOnlyList<string> SupportedInterfaces { get; init; }
    public required IReadOnlyList<string> SupportedFileFormats { get; init; }
    public required IReadOnlyList<string> ProjectTypes { get; init; }
    public required IReadOnlyList<string> Capabilities { get; init; }
    public required string DefaultExtension { get; init; }
    public required string RecommendedModel { get; init; }
    public string? RunningProcessName { get; set; }
    public int? ActiveProcessId { get; set; }
    public bool IsProcessRunning => ActiveProcessId.HasValue;
}

/// <summary>
/// Central registry of the 16 applications forming the SPARK Creativity ecosystem.
/// </summary>
public static class CreativeAppRegistry
{
    public const string CraftArchivesRoot = @"C:\Users\PC\Documents\Spark\SPARK-Reference-Library\AI-Creative\ArtCraft-Applications";
    public const string ExtractedRoot = @"C:\Users\PC\Documents\Spark\SPARK-Reference-Library\AI-Creative\ArtCraft-Applications";
    public const string AssetsRoot = "ms-appx:///Assets/Creative/Bots";

    private static readonly ImmutableDictionary<string, CreativeAppDescriptor> s_apps;

    static CreativeAppRegistry()
    {
        var list = new List<CreativeAppDescriptor>
        {
            new()
            {
                ApplicationId = "photospark",
                DisplayName = "PhotoSpark",
                UpstreamName = "PhotoCraft",
                PrimaryCapability = "Raster image editing",
                Category = "Imaging",
                Description = "Professional layer-based raster image editor with non-destructive adjustments, PSD roundtrip, masking, and high-frequency retouching.",
                MascotMotif = "Flame mascot with a camera/lens motif",
                IconAssetPath = $"{AssetsRoot}/photospark.png",
                SourceArchiveName = "photocraft-main.zip",
                RelativeSourcePath = "photocraft-main",
                BinaryExecutableName = "photocraft.exe",
                CliExecutableName = "photocraft-cli.exe",
                IntegrationMode = CreativeIntegrationMode.NativeWindow,
                Status = CreativeIntegrationStatus.Tested,
                Version = "0.1.0-alpha",
                License = "MIT OR Apache-2.0",
                SupportedInterfaces = ["CLI", "MCP", "JSON", "NativeWindow"],
                SupportedFileFormats = [".pcraft", ".psd", ".png", ".jpg", ".jpeg", ".webp", ".tiff", ".bmp"],
                ProjectTypes = ["PhotoSpark Document (*.pcraft)", "Adobe Photoshop Document (*.psd)"],
                Capabilities = ["layers", "masks", "adjustments", "curves", "blend_modes", "psd_import_export", "headless_rendering"],
                DefaultExtension = ".pcraft",
                RecommendedModel = "Gemini 2.5 Pro Vision"
            },
            new()
            {
                ApplicationId = "vectorspark",
                DisplayName = "VectorSpark",
                UpstreamName = "VectorCraft",
                PrimaryCapability = "Vector illustration",
                Category = "Graphics",
                Description = "Precision vector illustration engine with Bézier curve tools, boolean operations, isometric drawing, and SVG optimization.",
                MascotMotif = "Flame mascot with Bézier/path geometry",
                IconAssetPath = $"{AssetsRoot}/vectorspark.png",
                SourceArchiveName = "vectorcraft-main.zip",
                RelativeSourcePath = "vectorcraft-main",
                BinaryExecutableName = "vectorcraft.exe",
                CliExecutableName = "vectorcraft-cli.exe",
                IntegrationMode = CreativeIntegrationMode.NativeWindow,
                Status = CreativeIntegrationStatus.Tested,
                Version = "0.1.0-alpha",
                License = "MIT OR Apache-2.0",
                SupportedInterfaces = ["CLI", "MCP", "JSON", "NativeWindow"],
                SupportedFileFormats = [".vcraft", ".svg", ".ai", ".eps", ".pdf", ".png"],
                ProjectTypes = ["VectorSpark Drawing (*.vcraft)", "Scalable Vector Graphics (*.svg)"],
                Capabilities = ["bezier_curves", "boolean_ops", "strokes", "gradients", "svg_export", "path_offset", "pixel_snapping"],
                DefaultExtension = ".vcraft",
                RecommendedModel = "Claude 3.7 Sonnet"
            },
            new()
            {
                ApplicationId = "filmspark",
                DisplayName = "FilmSpark",
                UpstreamName = "FilmCraft",
                PrimaryCapability = "Video editing",
                Category = "Video",
                Description = "Non-linear video editor with multi-track timeline, J/L cuts, speed ramping, color grading LUTs, and audio ducking.",
                MascotMotif = "Flame mascot with a film-frame motif",
                IconAssetPath = $"{AssetsRoot}/filmspark.png",
                SourceArchiveName = "filmcraft-main.zip",
                RelativeSourcePath = "filmcraft-main",
                BinaryExecutableName = "filmcraft.exe",
                CliExecutableName = "filmcraft-cli.exe",
                IntegrationMode = CreativeIntegrationMode.NativeWindow,
                Status = CreativeIntegrationStatus.Tested,
                Version = "0.1.0-alpha",
                License = "MIT OR Apache-2.0",
                SupportedInterfaces = ["CLI", "MCP", "JSON", "NativeWindow"],
                SupportedFileFormats = [".filmcraft", ".mp4", ".mov", ".mkv", ".avi", ".webm"],
                ProjectTypes = ["FilmSpark Timeline (*.filmcraft)"],
                Capabilities = ["timeline_editing", "cuts_transitions", "speed_ramping", "lut_grading", "audio_ducking", "h264_export"],
                DefaultExtension = ".filmcraft",
                RecommendedModel = "Gemini 2.5 Pro Vision"
            },
            new()
            {
                ApplicationId = "lightspark",
                DisplayName = "LightSpark",
                UpstreamName = "LightCraft",
                PrimaryCapability = "Photo library and RAW development",
                Category = "Imaging",
                Description = "Non-destructive digital darkroom and RAW photo processor with demosaicing, chromatic correction, and 3-way color wheels.",
                MascotMotif = "Flame mascot with a photographic aperture",
                IconAssetPath = $"{AssetsRoot}/lightspark.png",
                SourceArchiveName = "lightcraft-main.zip",
                RelativeSourcePath = "lightcraft-main",
                BinaryExecutableName = "lightcraft.exe",
                CliExecutableName = "lightcraft-cli.exe",
                IntegrationMode = CreativeIntegrationMode.NativeWindow,
                Status = CreativeIntegrationStatus.Tested,
                Version = "0.1.0-alpha",
                License = "MIT OR Apache-2.0",
                SupportedInterfaces = ["CLI", "MCP", "JSON", "NativeWindow"],
                SupportedFileFormats = [".lcraft", ".dng", ".cr2", ".cr3", ".nef", ".arw", ".jpg", ".tiff"],
                ProjectTypes = ["LightSpark Catalog (*.lcraft)"],
                Capabilities = ["raw_demosaic", "white_balance", "lens_correction", "film_emulation", "hdr_merge", "color_wheels"],
                DefaultExtension = ".lcraft",
                RecommendedModel = "Gemini 2.5 Pro Vision"
            },
            new()
            {
                ApplicationId = "pdfspark",
                DisplayName = "PDFSpark",
                UpstreamName = "PdfCraft",
                PrimaryCapability = "PDF workbench",
                Category = "Documents",
                Description = "Comprehensive PDF workbench for merging, splitting, OCR text recognition, form field authoring, and PDF/UA accessibility.",
                MascotMotif = "Flame mascot with a document motif",
                IconAssetPath = $"{AssetsRoot}/pdfspark.png",
                SourceArchiveName = "pdfcraft-main.zip",
                RelativeSourcePath = "pdfcraft-main",
                BinaryExecutableName = "pdfcraft.exe",
                CliExecutableName = "pdfcraft-cli.exe",
                IntegrationMode = CreativeIntegrationMode.NativeWindow,
                Status = CreativeIntegrationStatus.Tested,
                Version = "0.1.0-alpha",
                License = "MIT OR Apache-2.0",
                SupportedInterfaces = ["CLI", "MCP", "JSON", "NativeWindow"],
                SupportedFileFormats = [".pdfcraft", ".pdf", ".xps", ".txt"],
                ProjectTypes = ["PDF Document (*.pdf)"],
                Capabilities = ["pdf_merge", "pdf_split", "ocr_extraction", "form_fields", "watermark", "pdf_ua_tagging"],
                DefaultExtension = ".pdf",
                RecommendedModel = "GPT-4o / Claude 3.7 Sonnet"
            },
            new()
            {
                ApplicationId = "effectspark",
                DisplayName = "EffectSpark",
                UpstreamName = "EffectCraft",
                PrimaryCapability = "Motion graphics and VFX",
                Category = "Video",
                Description = "Node- and layer-based motion graphics, visual effects compositor, kinetic typography, particle emitters, and 2.5D camera projection.",
                MascotMotif = "Flame mascot with a visual-effects motif",
                IconAssetPath = $"{AssetsRoot}/effectspark.png",
                SourceArchiveName = "effectcraft-main.zip",
                RelativeSourcePath = "effectcraft-main",
                BinaryExecutableName = "effectcraft.exe",
                CliExecutableName = "effectcraft-cli.exe",
                IntegrationMode = CreativeIntegrationMode.NativeWindow,
                Status = CreativeIntegrationStatus.Tested,
                Version = "0.1.0-alpha",
                License = "MIT OR Apache-2.0",
                SupportedInterfaces = ["CLI", "MCP", "JSON", "NativeWindow"],
                SupportedFileFormats = [".ecraft", ".aep", ".mp4", ".mov", ".png", ".exr"],
                ProjectTypes = ["EffectSpark VFX Composition (*.ecraft)"],
                Capabilities = ["motion_graphics", "particle_systems", "planar_tracking", "chroma_key", "kinetic_type", "parallax_3d"],
                DefaultExtension = ".ecraft",
                RecommendedModel = "Claude 3.7 Sonnet"
            },
            new()
            {
                ApplicationId = "anispark",
                DisplayName = "AniSpark",
                UpstreamName = "AniCraft",
                PrimaryCapability = "2D animation",
                Category = "Animation",
                Description = "Traditional and digital 2D animation studio with frame-by-frame light table, onion-skinning, vector tweening, and sprite sheet export.",
                MascotMotif = "Flame mascot with an animation/timeline motif",
                IconAssetPath = $"{AssetsRoot}/anispark.png",
                SourceArchiveName = "anicraft-main.zip",
                RelativeSourcePath = "anicraft-main",
                BinaryExecutableName = "anicraft.exe",
                CliExecutableName = "anicraft.exe",
                IntegrationMode = CreativeIntegrationMode.NativeWindow,
                Status = CreativeIntegrationStatus.Tested,
                Version = "0.1.0-alpha",
                License = "MIT OR Apache-2.0",
                SupportedInterfaces = ["CLI", "MCP", "JSON", "NativeWindow"],
                SupportedFileFormats = [".acraft", ".gif", ".mp4", ".png", ".json"],
                ProjectTypes = ["AniSpark Animation (*.acraft)"],
                Capabilities = ["onion_skinning", "squash_stretch", "easing_curves", "walk_cycles", "lip_sync", "sprite_sheet_pack"],
                DefaultExtension = ".acraft",
                RecommendedModel = "Claude 3.7 Sonnet"
            },
            new()
            {
                ApplicationId = "designspark",
                DisplayName = "DesignSpark",
                UpstreamName = "DesignCraft",
                PrimaryCapability = "Page layout and publishing",
                Category = "Publishing",
                Description = "Professional desktop publishing and multi-page layout application for magazines, books, brochures, and print-ready PDF/X.",
                MascotMotif = "Flame mascot with a page-layout motif",
                IconAssetPath = $"{AssetsRoot}/designspark.png",
                SourceArchiveName = "designcraft-main.zip",
                RelativeSourcePath = "designcraft-main",
                BinaryExecutableName = "designcraft.exe",
                CliExecutableName = "designcraft-cli.exe",
                IntegrationMode = CreativeIntegrationMode.NativeWindow,
                Status = CreativeIntegrationStatus.Tested,
                Version = "0.1.0-alpha",
                License = "MIT OR Apache-2.0",
                SupportedInterfaces = ["CLI", "MCP", "JSON", "NativeWindow"],
                SupportedFileFormats = [".dcraft", ".indd", ".idml", ".pdf", ".epub"],
                ProjectTypes = ["DesignSpark Publication (*.dcraft)"],
                Capabilities = ["master_pages", "facing_spreads", "baseline_grids", "threaded_text", "preflight_cmyk", "bleed_slug"],
                DefaultExtension = ".dcraft",
                RecommendedModel = "Claude 3.7 Sonnet"
            },
            new()
            {
                ApplicationId = "figspark",
                DisplayName = "FigSpark",
                UpstreamName = "FigCraft",
                PrimaryCapability = "UI/UX design and prototyping",
                Category = "UI/UX",
                Description = "Modern UI/UX design and prototyping platform supporting auto-layout flex frames, component variants, design tokens, and smart-animate.",
                MascotMotif = "Flame mascot with a UI/component motif",
                IconAssetPath = $"{AssetsRoot}/figspark.png",
                SourceArchiveName = "figcraft-main.zip",
                RelativeSourcePath = "figcraft-main",
                BinaryExecutableName = "figcraft.exe",
                CliExecutableName = "figcraft-cli.exe",
                IntegrationMode = CreativeIntegrationMode.NativeWindow,
                Status = CreativeIntegrationStatus.Tested,
                Version = "0.1.0-alpha",
                License = "MIT OR Apache-2.0",
                SupportedInterfaces = ["CLI", "MCP", "JSON", "NativeWindow"],
                SupportedFileFormats = [".figcraft", ".fig", ".json", ".svg", ".png"],
                ProjectTypes = ["FigSpark Design Canvas (*.figcraft)", "Figma File (*.fig)"],
                Capabilities = ["auto_layout", "component_variants", "design_tokens", "smart_animate", "design_to_code", "responsive_frames"],
                DefaultExtension = ".figcraft",
                RecommendedModel = "Claude 3.7 Sonnet / GPT-4o"
            },
            new()
            {
                ApplicationId = "texturespark",
                DisplayName = "TextureSpark",
                UpstreamName = "TextureCraft",
                PrimaryCapability = "3D texture painting",
                Category = "3D",
                Description = "Direct 3D mesh texture painting and projection workbench with multi-channel layer stacks, symmetry painting, and texture baking.",
                MascotMotif = "Flame mascot with a textured surface motif",
                IconAssetPath = $"{AssetsRoot}/texturespark.png",
                SourceArchiveName = "texturecraft-main.zip",
                RelativeSourcePath = "texturecraft-main",
                BinaryExecutableName = "texturecraft.exe",
                CliExecutableName = "texturecraft-cli.exe",
                IntegrationMode = CreativeIntegrationMode.NativeWindow,
                Status = CreativeIntegrationStatus.Tested,
                Version = "0.1.0-alpha",
                License = "MIT OR Apache-2.0",
                SupportedInterfaces = ["CLI", "MCP", "JSON", "NativeWindow"],
                SupportedFileFormats = [".tcraft", ".obj", ".fbx", ".gltf", ".png", ".exr"],
                ProjectTypes = ["TextureSpark Project (*.tcraft)"],
                Capabilities = ["3d_brush_paint", "uv_projection", "normal_bake", "ambient_occlusion_bake", "pbr_channels", "curvature_mask"],
                DefaultExtension = ".tcraft",
                RecommendedModel = "Gemini 2.5 Pro Vision"
            },
            new()
            {
                ApplicationId = "materialspark",
                DisplayName = "MaterialSpark",
                UpstreamName = "MaterialCraft",
                PrimaryCapability = "Procedural materials",
                Category = "3D",
                Description = "Node-graph procedural material authoring system creating seamless PBR materials, noise synthesis, edge wear, and texture patterns.",
                MascotMotif = "Flame mascot with a procedural node motif",
                IconAssetPath = $"{AssetsRoot}/materialspark.png",
                SourceArchiveName = "materialcraft-main.zip",
                RelativeSourcePath = "materialcraft-main",
                BinaryExecutableName = "materialcraft.exe",
                CliExecutableName = "materialcraft-cli.exe",
                IntegrationMode = CreativeIntegrationMode.NativeWindow,
                Status = CreativeIntegrationStatus.Tested,
                Version = "0.1.0-alpha",
                License = "MIT OR Apache-2.0",
                SupportedInterfaces = ["CLI", "MCP", "JSON", "NativeWindow"],
                SupportedFileFormats = [".mcraft", ".sbsar", ".png", ".tiff", ".exr"],
                ProjectTypes = ["MaterialSpark Node Graph (*.mcraft)"],
                Capabilities = ["procedural_nodes", "pbr_roughness_metallic", "pattern_generators", "noise_synthesis", "triplanar_mapping", "height_displacement"],
                DefaultExtension = ".mcraft",
                RecommendedModel = "Claude 3.7 Sonnet"
            },
            new()
            {
                ApplicationId = "soundspark",
                DisplayName = "SoundSpark",
                UpstreamName = "SoundCraft",
                PrimaryCapability = "Audio recording and editing",
                Category = "Audio",
                Description = "Digital audio workstation (DAW) and audio restoration suite with multi-track recording, subtractive EQ, compression, and loudness metering.",
                MascotMotif = "Flame mascot with a waveform motif",
                IconAssetPath = $"{AssetsRoot}/soundspark.png",
                SourceArchiveName = "soundcraft-main.zip",
                RelativeSourcePath = "soundcraft-main",
                BinaryExecutableName = "soundcraft.exe",
                CliExecutableName = "soundcraft-cli.exe",
                IntegrationMode = CreativeIntegrationMode.NativeWindow,
                Status = CreativeIntegrationStatus.Tested,
                Version = "0.1.0-alpha",
                License = "MIT OR Apache-2.0",
                SupportedInterfaces = ["CLI", "MCP", "JSON", "NativeWindow"],
                SupportedFileFormats = [".scraft", ".wav", ".flac", ".mp3", ".ogg", ".mid"],
                ProjectTypes = ["SoundSpark Session (*.scraft)"],
                Capabilities = ["multitrack_recording", "spectral_editing", "parametric_eq", "dynamics_compression", "lufs_metering", "stem_export"],
                DefaultExtension = ".scraft",
                RecommendedModel = "Claude 3.7 Sonnet"
            },
            new()
            {
                ApplicationId = "wordspark",
                DisplayName = "WordSpark",
                UpstreamName = "WordCraft",
                PrimaryCapability = "Word processing",
                Category = "Documents",
                Description = "High-precision word processor and long-document publisher with WordML/DOCX conversion, citation management, styles, and tracked changes.",
                MascotMotif = "Flame mascot with a document/typing motif",
                IconAssetPath = $"{AssetsRoot}/wordspark.png",
                SourceArchiveName = "wordcraft-main.zip",
                RelativeSourcePath = "wordcraft-main",
                BinaryExecutableName = "wordcraft.exe",
                CliExecutableName = "wordcraft-cli.exe",
                IntegrationMode = CreativeIntegrationMode.NativeWindow,
                Status = CreativeIntegrationStatus.Tested,
                Version = "0.1.0-alpha",
                License = "MIT OR Apache-2.0",
                SupportedInterfaces = ["CLI", "MCP", "JSON", "NativeWindow"],
                SupportedFileFormats = [".wcraft", ".docx", ".doc", ".odt", ".rtf", ".html", ".md", ".txt"],
                ProjectTypes = ["WordSpark Document (*.wcraft)", "Microsoft Word Document (*.docx)"],
                Capabilities = ["docx_roundtrip", "heading_hierarchies", "zotero_citations", "tracked_changes", "table_formatting", "styles_cascade"],
                DefaultExtension = ".wcraft",
                RecommendedModel = "GPT-4o / Claude 3.7 Sonnet"
            },
            new()
            {
                ApplicationId = "deckspark",
                DisplayName = "DeckSpark",
                UpstreamName = "DeckCraft",
                PrimaryCapability = "Presentations",
                Category = "Presentations",
                Description = "Executive presentation slide designer and slide deck generator with PPTX roundtrip, dynamic charts, layouts, and slide morphing.",
                MascotMotif = "Flame mascot with a presentation motif",
                IconAssetPath = $"{AssetsRoot}/deckspark.png",
                SourceArchiveName = "deckcraft-main.zip",
                RelativeSourcePath = "deckcraft-main",
                BinaryExecutableName = "deckcraft.exe",
                CliExecutableName = "deckcraft-cli.exe",
                IntegrationMode = CreativeIntegrationMode.NativeWindow,
                Status = CreativeIntegrationStatus.Tested,
                Version = "0.1.0-alpha",
                License = "MIT OR Apache-2.0",
                SupportedInterfaces = ["CLI", "MCP", "JSON", "NativeWindow"],
                SupportedFileFormats = [".deckcraft", ".pptx", ".pdf", ".png"],
                ProjectTypes = ["DeckSpark Presentation (*.deckcraft)", "PowerPoint Presentation (*.pptx)"],
                Capabilities = ["pptx_roundtrip", "slide_masters", "chart_integration", "slide_inspect", "headless_slide_render", "deck_narrative"],
                DefaultExtension = ".deckcraft",
                RecommendedModel = "GPT-4o / Claude 3.7 Sonnet"
            },
            new()
            {
                ApplicationId = "cadspark",
                DisplayName = "CADSpark",
                UpstreamName = "CADCraft",
                PrimaryCapability = "CAD and technical drafting",
                Category = "Engineering",
                Description = "Parametric 2D/3D CAD drafting and technical drawing system with DXF/DWG/STEP support, GD&T tolerancing, constraints, and views.",
                MascotMotif = "Flame mascot with a geometric drafting motif",
                IconAssetPath = $"{AssetsRoot}/cadspark.png",
                SourceArchiveName = "cadcraft-main.zip",
                RelativeSourcePath = "cadcraft-main",
                BinaryExecutableName = "cadcraft.exe",
                CliExecutableName = "cadcraft-cli.exe",
                IntegrationMode = CreativeIntegrationMode.NativeWindow,
                Status = CreativeIntegrationStatus.Tested,
                Version = "0.1.0-alpha",
                License = "MIT OR Apache-2.0",
                SupportedInterfaces = ["CLI", "MCP", "JSON", "NativeWindow"],
                SupportedFileFormats = [".cadcraft", ".dxf", ".dwg", ".step", ".iges", ".svg"],
                ProjectTypes = ["CADSpark Technical Drawing (*.cadcraft)", "AutoCAD Drawing Interchange (*.dxf)"],
                Capabilities = ["orthographic_projection", "gd_and_t", "parametric_sketching", "layer_standards", "section_hatching", "dxf_export"],
                DefaultExtension = ".cadcraft",
                RecommendedModel = "Claude 3.7 Sonnet"
            },
            new()
            {
                ApplicationId = "gridspark",
                DisplayName = "GridSpark",
                UpstreamName = "GridCraft",
                PrimaryCapability = "Spreadsheets and data analysis",
                Category = "Data",
                Description = "Formula-driven financial spreadsheet and analytical grid with XLSX interoperability, multi-cell calculations, and live charts.",
                MascotMotif = "Flame mascot with a spreadsheet/data motif",
                IconAssetPath = $"{AssetsRoot}/gridspark.png",
                SourceArchiveName = "gridcraft-main.zip",
                RelativeSourcePath = "gridcraft-main",
                BinaryExecutableName = "gridcraft.exe",
                CliExecutableName = "gridcraft-cli.exe",
                IntegrationMode = CreativeIntegrationMode.NativeWindow,
                Status = CreativeIntegrationStatus.Tested,
                Version = "0.1.0-alpha",
                License = "MIT OR Apache-2.0",
                SupportedInterfaces = ["CLI", "MCP", "JSON", "NativeWindow"],
                SupportedFileFormats = [".gcraft", ".xlsx", ".csv", ".tsv", ".json"],
                ProjectTypes = ["GridSpark Workbook (*.gcraft)", "Microsoft Excel Workbook (*.xlsx)"],
                Capabilities = ["formula_engine", "xlsx_roundtrip", "table_matrix", "data_filtering", "chart_generation", "cross_sheet_references"],
                DefaultExtension = ".gcraft",
                RecommendedModel = "GPT-4o / Claude 3.7 Sonnet"
            }
        };

        s_apps = list.ToImmutableDictionary(a => a.ApplicationId, StringComparer.OrdinalIgnoreCase);
    }

    public static IReadOnlyList<CreativeAppDescriptor> AllApplications => s_apps.Values.ToList();

    public static bool TryGetApplication(string applicationId, out CreativeAppDescriptor descriptor)
    {
        if (s_apps.TryGetValue(applicationId, out var direct))
        {
            descriptor = direct;
            return true;
        }

        // Try lookup by upstream or display name
        var matched = s_apps.Values.FirstOrDefault(a =>
            string.Equals(a.DisplayName, applicationId, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(a.UpstreamName, applicationId, StringComparison.OrdinalIgnoreCase));

        if (matched != null)
        {
            descriptor = matched;
            return true;
        }

        descriptor = default!;
        return false;
    }

    public static CreativeAppDescriptor GetApplication(string applicationId)
    {
        if (TryGetApplication(applicationId, out var app))
        {
            return app;
        }

        throw new KeyNotFoundException($"Creative application with identifier '{applicationId}' was not found in the SPARK registry.");
    }

    public static IEnumerable<CreativeAppDescriptor> SearchApplications(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return AllApplications;
        }

        var q = query.Trim();
        return AllApplications.Where(a =>
            a.DisplayName.Contains(q, StringComparison.OrdinalIgnoreCase) ||
            a.ApplicationId.Contains(q, StringComparison.OrdinalIgnoreCase) ||
            a.PrimaryCapability.Contains(q, StringComparison.OrdinalIgnoreCase) ||
            a.Category.Contains(q, StringComparison.OrdinalIgnoreCase) ||
            a.Capabilities.Any(c => c.Contains(q, StringComparison.OrdinalIgnoreCase)));
    }
}
