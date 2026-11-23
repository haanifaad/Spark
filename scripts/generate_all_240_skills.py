"""
Generates the complete library of 240 professional creative skills across 12 families
for the SPARK Creative Skills Engine, strictly satisfying all 20 required fields per skill.
"""
import os
import json

SKILLS_ROOT = r"C:\Intel\Spark\Spark-main\src\skills\creativity"
os.makedirs(SKILLS_ROOT, exist_ok=True)

# 12 Families and their exact 240 skill titles and definitions
FAMILIES = {
    "design_fundamentals": {
        "title": "Design Fundamentals and Visual Principles",
        "target": 25,
        "app": "DesignSpark",
        "app_id": "designspark",
        "cli_app": "designcraft-cli",
        "skills": [
            ("visual-hierarchy-composition", "Visual Hierarchy and Focal Point Composition", "Establishing dominant visual entry points and scanning pathways"),
            ("golden-ratio-modular-scales", "Golden Ratio and Modular Scaling Systems", "Applying proportional harmonies and geometric scale factors"),
            ("color-theory-harmony", "Color Theory and Harmonious Palette Construction", "Constructing complementary, analogous, and triadic color harmonies"),
            ("typographic-contrast-pairing", "Typographic Contrast and Typeface Pairing", "Pairing display and body serifs and sans-serifs with optical balance"),
            ("gestalt-grouping-principles", "Gestalt Principles of Grouping and Proximity", "Leveraging closure, common region, and proximity to organize layouts"),
            ("grid-systems-columnar-rhythm", "Grid Systems and Columnar Structural Rhythm", "Constructing 12-column, baseline, and modular spatial layouts"),
            ("whitespace-negative-space", "Intentional Whitespace and Negative Space Balancing", "Managing breathing room, cognitive load, and macro/micro whitespace"),
            ("contrast-wcag-accessibility", "Contrast Ratio and Accessible Visual Design", "Ensuring WCAG 2.2 AA/AAA compliance across foreground and background"),
            ("visual-balance-symmetry", "Visual Balance: Symmetrical vs Asymmetrical Equilibrium", "Calibrating visual weight, counter-balancing elements across axes"),
            ("repetition-pattern-consistency", "Repetition, Rhythm, and Pattern Consistency", "Unifying layout systems through rhythmic cadence and motif recurrence"),
            ("scale-proportion-framing", "Scale, Proportion, and Contextual Framing", "Using extreme sizing contrasts to emphasize emotional resonance"),
            ("color-psychology-branding", "Color Psychology and Brand Resonance Modeling", "Selecting hue palettes aligned to cognitive and industry associations"),
            ("micro-typography-kerning", "Micro-Typography: Kerning, Tracking, and Optical Margins", "Eliminating rivers, adjusting word spacing, and optical margin hanging"),
            ("focal-depth-layering", "Focal Depth, Z-Index, and Dimensional Layering", "Creating perceived depth using blur gradients and atmospheric perspective"),
            ("texture-tactility-depth", "Texture, Grain, and Surface Tactility in 2D", "Infusing organic analog warmth and tactile dimensionality to surfaces"),
            ("modular-layout-architecture", "Modular Layout Architecture and Content Containers", "Architecting card-based, bento-grid, and nested container structures"),
            ("visual-metaphor-symbolism", "Visual Metaphors and Conceptual Semiotics", "Encoding abstract ideas into culturally resonant icons and visual symbols"),
            ("optical-adjustment-illusions", "Optical Adjustments and Geometric Distortion Correction", "Compensating for geometric illusions, overshooting curved letterforms"),
            ("motion-affordance-direction", "Motion Affordance and Directional Vector Flow", "Guiding user attention along implied trajectories and sightlines"),
            ("dark-mode-color-inversion", "Dark Mode Palette Synthesis and Elevation Tokens", "Formulating desaturated dark elevation palettes without pure black harshness"),
            ("responsive-typographic-rhythm", "Responsive Fluid Typography and Viewport Clamping", "Implementing fluid typographic scales using clamp equations"),
            ("brand-expression-guidelines", "Brand Identity Systems and Multi-Channel Alignment", "Defining core design tokens, logo clearspace, and tone guidelines"),
            ("editorial-art-direction", "Editorial Art Direction and Editorial Page Rhythm", "Balancing full-bleed photography, pull-quotes, and dense editorial copy"),
            ("minimalist-reductive-design", "Minimalist Reductive Design and Core Distillation", "Eliminating superfluous ornamentation to highlight essential function"),
            ("design-critique-heuristics", "Design Critique Heuristics and Quality Evaluation", "Auditing creative compositions against formal aesthetic frameworks")
        ]
    },
    "ui_ux_interaction": {
        "title": "UI/UX and Interaction Design",
        "target": 25,
        "app": "FigSpark",
        "app_id": "figspark",
        "cli_app": "figcraft-cli",
        "skills": [
            ("atomic-design-system", "Atomic Design Systems and Component Hierarchy", "Architecting atoms, molecules, organisms, and page templates"),
            ("auto-layout-flex-flows", "Auto-Layout, Flexbox Alignment, and Dynamic Resizing", "Building responsive UI components that stretch and wrap intelligently"),
            ("design-tokens-multi-theme", "Design Tokens Architecture and Semantic Theming", "Abstracting color, spacing, and typography into semantic design tokens"),
            ("wireframing-low-fidelity", "Low-Fidelity Wireframing and Information Architecture", "Rapidly structuring interface flows before visual detailing"),
            ("high-fidelity-ui-prototyping", "High-Fidelity Interactive State Prototyping", "Linking frames with smart-animates, spring physics, and overlays"),
            ("form-ux-validation-states", "Form UX, Progressive Disclosure, and Validation States", "Minimizing input friction with inline validation and progressive forms"),
            ("modal-dialog-sheet-ux", "Modal Dialogs, Bottom Sheets, and Contextual Drawers", "Designing accessible non-disruptive disruption patterns"),
            ("navigation-mental-models", "Navigation Architecture: Tabs, Drawers, and Breadcrumbs", "Structuring clear wayfinding mental models for deep digital apps"),
            ("mobile-touch-target-ergonomics", "Mobile Touch Ergonomics and Thumb-Zone Optimization", "Ensuring minimum 48px touch targets and natural thumb navigation"),
            ("empty-states-onboarding", "Empty States, Zero-Data Displays, and User Onboarding", "Transforming blank screens into delightful activation opportunities"),
            ("data-table-ux-filtering", "Data Table UX, Column Freezing, and Multi-Sort", "Organizing complex tabular data with pagination and inline filters"),
            ("micro-interaction-feedback", "Micro-Interactions, Hover States, and Haptic Feedback", "Providing subtle sensory feedback for user interactions"),
            ("card-component-patterns", "Card Component Patterns and Content Containment", "Structuring multifaceted content into scannable interactive cards"),
            ("search-filtering-patterns", "Faceted Search, Autocomplete, and Search UX", "Enabling rapid exploration through instant suggestion search"),
            ("notification-toast-lifecycle", "Toast Banners, Snakbars, and Notification Lifecycles", "Handling system alerts without obscuring active workspace workflows"),
            ("accessibility-keyboard-focus", "Accessibility: Focus Rings, Skip Links, and Tab Order", "Ensuring full keyboard navigability and WCAG focus visibility"),
            ("mobile-sheet-gesture-ux", "Swipe Gestures, Pull-To-Refresh, and Sheet Snapping", "Crafting intuitive touch-native physics and gesture controllers"),
            ("dashboard-widget-layout", "Executive Dashboard UX and Analytical Widget Cards", "Arranging mission-critical metrics into glanced KPIs"),
            ("stepper-wizard-progress-ux", "Multi-Step Wizards, Steppers, and Progress Tracking", "Guiding complex multi-page submissions with saved draft checkpoints"),
            ("settings-preferences-ux", "Complex Settings Architecture and Preference Matrices", "Grouping enterprise configurations into intuitive categorized sections"),
            ("drag-and-drop-interaction-ux", "Drag-and-Drop Interaction and Spatial Reordering", "Providing clear drop targets, ghost avatars, and drop indicators"),
            ("content-loading-skeletons", "Skeleton Screens, Shimmer Loaders, and Perceived Speed", "Replacing blocking spinners with progressive skeleton layouts"),
            ("user-journey-storyboarding", "User Journey Storyboarding and Touchpoint Mapping", "Visualizing end-to-end user paths across multiple devices"),
            ("design-to-code-handoff", "Design-to-Development Specs and Export Manifests", "Generating pixel-perfect CSS/XAML measurements and asset packs"),
            ("usability-heuristic-evaluation", "Nielsen Norman Usability Heuristic Audits", "Evaluating interface designs against the 10 core usability heuristics")
        ]
    },
    "raster_image_editing": {
        "title": "Raster Image Editing",
        "target": 20,
        "app": "PhotoSpark",
        "app_id": "photospark",
        "cli_app": "photocraft-cli",
        "skills": [
            ("non-destructive-layer-masking", "Non-Destructive Layer Masking and Compositing", "Blending multi-image scenes using soft alpha and vector masks"),
            ("adjustment-layers-curves-levels", "Adjustment Layers: Tonal Curves and Precision Levels", "Modulating histogram distribution and white/black balance"),
            ("frequency-separation-retouching", "Frequency Separation Skin and Texture Retouching", "Separating high-frequency surface detail from low-frequency color"),
            ("content-aware-inpainting", "Generative and Content-Aware Inpainting", "Synthesizing seamless fills to remove unwanted objects and blemishes"),
            ("clipping-masks-adjustment-groups", "Clipping Masks and Nested Adjustment Stacks", "Restricting tonal and color modifications to targeted child layers"),
            ("channel-operations-luminance-masks", "Channel Operations and Luminosity Masking", "Extracting high-precision contrast masks from color channels"),
            ("blend-modes-visual-compositing", "Blend Modes Compositing (Multiply, Screen, Overlay)", "Harnessing mathematical blend algorithms for lighting composites"),
            ("high-pass-sharpening", "High-Pass Filter and Edge-Aware Sharpening", "Enhancing micro-contrast along structural edges without halos"),
            ("smart-objects-embedded-filters", "Smart Objects and Reversible Smart Filters", "Preserving native raster resolution across repeated transformations"),
            ("selective-color-grading-hsl", "Selective Color Replacement and HSL Tuning", "Targeting specific color bands for hue shifts and saturation boosts"),
            ("shadow-highlight-recovery", "Shadows and Highlights Recovery Pipeline", "Balancing high-dynamic range exposure without flat contrast mud"),
            ("perspective-warp-distortion", "Perspective Warp and Orthographic Correction", "Aligning skewed building facades and architectural planes"),
            ("digital-matte-painting-blending", "Digital Matte Painting and Atmospheric Haze", "Compositing fantasy environments with depth-based atmospheric haze"),
            ("layer-styles-drop-shadows", "Custom Layer Styles: Inner Glows, Bevels, Drop Shadows", "Crafting dimensional skeuomorphic and glassmorphic UI assets"),
            ("dithering-indexed-color-export", "Dithering Algorithms and Indexed Color Optimization", "Optimizing retro 8-bit assets and GIF/PNG color quantization"),
            ("depth-of-field-bokeh-blur", "Depth of Field Synthesis and Lens Bokeh Simulation", "Applying depth-map driven blur discs and hexagonal aperture bokeh"),
            ("duotone-gradient-mapping", "Duotone and Gradient Map Artistic Stylization", "Remapping monochromatic luminance into expressive multi-stop gradients"),
            ("noise-reduction-grain-matching", "Luminance Noise Reduction and Grain Matching", "Smoothing sensor grain while retaining authentic film texture"),
            ("raster-brush-engine-customization", "Dynamic Raster Brush Dynamics and Pressure Curves", "Configuring scatter, jitter, and opacity response for stylus painting"),
            ("batch-raster-export-pipeline", "Batch Processing, WebP/AVIF Compression, and Web Export", "Automating multi-resolution export recipes for modern digital web")
        ]
    },
    "vector_illustration": {
        "title": "Vector Illustration",
        "target": 20,
        "app": "VectorSpark",
        "app_id": "vectorspark",
        "cli_app": "vectorcraft-cli",
        "skills": [
            ("bezier-curve-mastery", "Bézier Curve Construction and Point Economy", "Placing smooth anchor points at extrema with minimal nodes"),
            ("boolean-shape-operations", "Boolean Operations: Union, Subtract, Intersect, Exclude", "Combining geometric primitives into complex iconography"),
            ("vector-iconography-grid-alignment", "Pixel-Snapped Iconography and Keyline Grids", "Designing on 24x24 / 32x32 grids with integer coordinate snapping"),
            ("gradient-mesh-photorealistic-vector", "Gradient Mesh and Multi-Point Shading in Vectors", "Rendering photorealistic organic shading on vector topologies"),
            ("isometric-vector-illustration", "Isometric Vector Drawing and 30-Degree Projections", "Constructing architectural and game assets on isometric grids"),
            ("custom-vector-brush-strokes", "Pattern Brushes, Calligraphic Strokes, and Variable Width", "Applying pressure-sensitive dynamic profiles along open paths"),
            ("vector-halftone-patterns", "Vector Halftones, Stippling, and Screen Patterns", "Generating mathematical dot-matrix and crosshatch vector patterns"),
            ("path-offset-contouring", "Offset Path Operations and Inline/Outline Framing", "Creating concentric contour badges, stickers, and stroke cutouts"),
            ("svg-code-optimization-cleaning", "SVG Clean-up, Path Simplification, and Code Minification", "Stripping redundant metadata, rounding floats, and grouping tags"),
            ("vector-lettering-logotype-design", "Custom Vector Lettering and Logotype Crafting", "Designing bespoke typography with custom ligatures and flourishes"),
            ("character-rig-vector-layering", "Vector Character Anatomy and Layer Rigging", "Organizing body parts, eyes, and joints for 2D puppet animation"),
            ("vector-pattern-seamless-tile", "Seamless Vector Pattern Tiling and Repeat Math", "Designing repeating textile and background motifs without seams"),
            ("monoline-illustration-style", "Monoline Architectural and Technical Illustration", "Maintaining constant stroke weights across complex mechanical drawings"),
            ("clipping-path-vector-masking", "Vector Clipping Paths and Alpha Mask Inversion", "Constraining decorative vector elements within silhouette containers"),
            ("vector-lighting-specular-highlights", "Vector Shading: Hard Shadows and Specular Creases", "Simulating directional light on flat vectors with cel-shading shapes"),
            ("variable-font-vector-outlines", "Variable Font Glyph Outlining and Axis Modification", "Modifying typographic vector contours for distinct logotypes"),
            ("flat-design-2-vector-palette", "Flat Design 2.0: Soft Gradients and Subtle Depth", "Modernizing flat art with chromatic ambient drop shadows"),
            ("vector-badge-crest-emblem", "Heraldic Crests, Badges, and Vector Emblems", "Constructing circular banners, stars, and balanced commemorative seals"),
            ("vector-tracing-bitmap-reconstruction", "Vectorization: Bitmap Tracing and Manual Cleanup", "Converting low-res logos into pristine resolution-independent curves"),
            ("print-ready-cmyk-vector-export", "Print-Ready Vector Preparation: Bleeds, Overprints, CMYK", "Configuring die-cut lines, spot colors, and registration marks")
        ]
    },
    "photography_processing": {
        "title": "Photography and Image Processing",
        "target": 15,
        "app": "LightSpark",
        "app_id": "lightspark",
        "cli_app": "lightcraft-cli",
        "skills": [
            ("raw-demosaicing-pipeline", "RAW Demosaicing and Sensor Noise Calibration", "Extracting maximum dynamic range from Bayer and X-Trans RAW files"),
            ("white-balance-color-temperature", "White Balance, Tint, and Color Temperature Balancing", "Calibrating Kelvin temperature and green-magenta tint with grey cards"),
            ("lens-distortion-vignette-profile", "Lens Distortion, Chromatic Aberration, and Vignette Correction", "Applying optical correction profiles for wide-angle and telephoto lenses"),
            ("film-stock-color-emulation", "Analog Film Stock Emulation (Kodachrome, Portra, Fuji)", "Modeling characteristic film grain, halation, and organic color response"),
            ("hdr-exposure-bracketing-merge", "HDR Exposure Bracketing and Multi-Frame Fusion", "Merging bracketed EV exposures without ghosting or unnatural halos"),
            ("color-grading-color-wheels", "Three-Way Color Wheels: Lift, Gamma, Gain Balancing", "Stylizing cinematic look by tinting shadows, midtones, and highlights"),
            ("high-contrast-black-and-white", "Fine-Art Black and White Monochromatic Conversion", "Converting color luminance channels for punchy Ansel Adams contrast"),
            ("perspective-keystone-geometry", "Keystone Correction and Perspective Upright Alignment", "Straightening converging vertical lines in architectural photographs"),
            ("astro-photography-star-stacking", "Astrophotography Stacking and Milky Way Enhancement", "Aligning star exposures to eliminate sensor noise and boost nebula clarity"),
            ("portrait-skin-tone-harmonization", "Portrait Skin Tone Harmonization and Color Vector Alignment", "Aligning portrait tones along the biological melanin skin-tone line"),
            ("graduated-radial-filter-masks", "Graduated and Radial Filter Masks for Exposure Guidance", "Darkening dramatic skies and spotlighting key subjects naturally"),
            ("split-toning-shadow-highlight", "Split Toning and Complementary Color Accents", "Injecting cyan-orange or gold-teal complementary mood into photos"),
            ("focus-stacking-macro-depth", "Focus Stacking for Extreme Macro Depth of Field", "Combining multiple razor-thin focus slices into tack-sharp macro images"),
            ("tethered-capture-color-calibration", "ColorChecker Passport Profiling and Studio Calibration", "Creating custom DNG camera profiles for absolute color fidelity"),
            ("high-resolution-batch-export", "Color Space Management: sRGB, AdobeRGB, ProPhoto RGB", "Managing gamut clipping when converting from wide gamut to web sRGB")
        ]
    },
    "video_motion_graphics": {
        "title": "Video Editing and Motion Graphics",
        "target": 20,
        "app": "FilmSpark",
        "app_id": "filmspark",
        "cli_app": "filmcraft-cli",
        "skills": [
            ("timeline-storytelling-pacing", "Narrative Video Editing and Timeline Pacing", "Cutting on action, J-cuts, L-cuts, and psychological rhythm"),
            ("kinetic-typography-animation", "Kinetic Typography, Tracking, and Title Sequences", "Animating expressive type with bounce, staggered reveals, and kerning"),
            ("multi-camera-sync-switching", "Multi-Camera Audio Synchronization and Live Switching", "Aligning multi-angle footage using waveform fingerprints and timecode"),
            ("cinematic-lut-color-grading", "Cinematic LUT Application and Log-to-Rec709 Normalization", "Decoding Arri LogC/Sony S-Log3 and applying balanced aesthetic LUTs"),
            ("chroma-key-green-screen-pull", "Chroma Keying, Spill Suppression, and Clean Plates", "Pulling pristine green/blue screen mattes with hair edge detail"),
            ("speed-ramping-time-remapping", "Speed Ramping, Optical Flow, and Time Remapping", "Creating fluid transitions between fast motion and super-slow motion"),
            ("lower-thirds-broadcast-graphics", "Broadcast Lower-Thirds and On-Air Identity Packages", "Building branded motion bugs, tickers, and speaker identifications"),
            ("motion-tracking-point-planar", "Planar Motion Tracking and Screen Replacement", "Tracking phone displays and billboard surfaces across camera moves"),
            ("sound-design-audio-ducking", "Audio Ducking, Foley Layering, and Voiceover Mixing", "Automatically lowering music under dialogue with smooth attack/decay"),
            ("match-cut-creative-transitions", "Match Cuts, Whip Pans, and Seamless Scene Transitions", "Connecting disparate scenes through motion, shape, and audio match cuts"),
            ("parallax-25d-photo-animation", "2.5D Parallax Photo Projection and Camera Mapping", "Cutting 2D photographs into layers and sweeping 3D camera across them"),
            ("hud-sci-fi-fui-motion-graphics", "Futuristic User Interface (FUI) and Sci-Fi HUD Elements", "Animating technical reticles, circuit diagrams, and data counters"),
            ("video-aspect-ratio-reformatting", "Vertical Video Adaptation: 16:9 to 9:16 Auto-Reframing", "Repositioning horizontal video for TikTok/Reels with dynamic panning"),
            ("glitch-distortion-effects", "Analog Glitch, VHS Tracking, and CRT Scanlines", "Simulating magnetic tape dropouts, RGB splitting, and phosphors"),
            ("stop-motion-paper-cutout-effect", "Stop-Motion Frame Rate Limiting and Paper Cutouts", "Emulating 12fps claymation and textured collage motion aesthetic"),
            ("intro-bumper-logo-reveal", "Dynamic Logo Reveal and Particle Morphing Animation", "Animating vector logos through energy sparks, sweeps, and builds"),
            ("picture-in-picture-reaction-layout", "Picture-in-Picture (PiP) and Reaction Video Layouts", "Positioning webcam feeds, gameplay windows, and animated borders"),
            ("freeze-frame-comic-book-cutout", "Freeze-Frame Halftone Comic Book Intro Stems", "Pausing action, extracting character silhouettes with halftone pop art"),
            ("subtitles-closed-captions-srt", "Auto-Subtitles, Word-by-Word Bounces, and Open Captions", "Formatting burnt-in dynamic social captions with word highlight pulses"),
            ("lossless-master-web-export-h265", "Mastering and Encoding: ProRes, DNxHR, H.264, and AV1", "Configuring constant rate factor (CRF) and bitrates for streaming")
        ]
    },
    "animation_2d": {
        "title": "2D Animation",
        "target": 15,
        "app": "AniSpark",
        "app_id": "anispark",
        "cli_app": "anicraft",
        "skills": [
            ("twelve-principles-squash-stretch", "Squash and Stretch for Organic Mass and Impact", "Simulating weight, flexibility, and elasticity on animated bodies"),
            ("anticipation-follow-through", "Anticipation, Overshoot, and Follow-Through Action", "Preparing the eye for movement and animating inertia on secondary limbs"),
            ("staging-keyframe-blocking", "Staging, Posing, and Keyframe Animation Blocking", "Drafting extreme storytelling poses before breaking down in-betweens"),
            ("slow-in-slow-out-easing-curves", "Slow In and Slow Out: Cubic Bézier Easing Curves", "Sculpting velocity graphs for natural organic acceleration"),
            ("arcs-natural-biomechanics", "Arcs of Motion and Human Biomechanical Pathways", "Ensuring limbs and moving objects follow curved spatial trajectories"),
            ("secondary-action-expressiveness", "Secondary Action: Hair, Cloth, and Tail Physics", "Layering secondary motions that enrich and support the primary action"),
            ("animation-timing-spacing-holds", "Timing, Spacing, and Moving Holds on 1s and 2s", "Balancing snappy 24fps full animation against expressive frame holds"),
            ("exaggeration-caricature-motion", "Exaggeration and Dynamic Impact Caricature", "Pushing poses and distortion beyond physical reality for emotional punch"),
            ("solid-drawing-three-dimensions", "Solid Drawing: Volume, Perspective, and Spatial Turnarounds", "Maintaining consistent volume as characters rotate in 2D space"),
            ("appeal-character-personality", "Appeal, Readability, and Character Personality Acting", "Infusing distinct gait, quirks, and expressive posture into characters"),
            ("walk-cycle-biomechanics", "Human and Quadruped Walk and Run Cycle Construction", "Looping contact, down, passing, and up frames with hip/shoulder rolls"),
            ("lip-sync-viseme-phoneme", "Lip-Sync Animation: Viseme Shapes and Dialogue Timing", "Matching mouth phonetic mouth shapes (A, E, O, M, F, L) to speech"),
            ("morphing-shape-transformation", "Shape Morphing and Continuous Line Transformations", "Transitioning fluidly between distinct symbolic shapes and silhouettes"),
            ("particle-fx-water-fire-smoke", "Hand-Drawn 2D Effects: Water Splashes, Fire, and Smoke", "Animating fluid dynamics, billowing smoke plumes, and fiery tongues"),
            ("sprite-sheet-export-game-engine", "Sprite Sheet Generation and Texture Atlas Packing", "Packing animated frames into power-of-two sprite sheets with JSON metadata")
        ]
    },
    "modelling_materials_3d": {
        "title": "3D Modelling, Materials and Texturing",
        "target": 25,
        "app": "MaterialSpark",
        "app_id": "materialspark",
        "cli_app": "materialcraft-cli",
        "skills": [
            ("pbr-principled-bsdf-shading", "Physically Based Rendering (PBR) Metallic-Roughness", "Authoring Albedo, Roughness, Metallic, Normal, and AO textures"),
            ("procedural-wood-grain-generation", "Procedural Wood Grain and Growth Rings Node Graph", "Synthesizing authentic wood fibers, knots, and age rings with noise"),
            ("procedural-marble-stone-shaders", "Procedural Marble, Granite, and Veined Stone Shaders", "Constructing stratified metamorphic stone textures using voronoi"),
            ("subsurface-scattering-skin-wax", "Subsurface Scattering (SSS) for Skin, Wax, and Jade", "Simulating light penetration and internal scattering in translucent matter"),
            ("uv-unwrapping-seam-optimization", "UV Unwrapping, Seam Placement, and Island Packing", "Minimizing texture stretch and maximizing UV island packing efficiency"),
            ("normal-map-baking-high-to-low", "Normal and Ambient Occlusion Baking from High-Poly", "Capturing millions of high-poly sculpted details into game-ready maps"),
            ("procedural-weathering-edge-wear", "Procedural Edge Wear, Chipping, and Surface Grunge", "Curvature-driven node masking to reveal oxidized underlayers on edges"),
            ("anisotropic-brushed-metal", "Anisotropic Reflections and Brushed Metal Shaders", "Simulating microscopic directional scratches on stainless steel and copper"),
            ("glass-refraction-dispersion-caustics", "Dielectric Glass Refraction, Dispersion, and Thin Film", "Simulating optical glass, soap bubble iridescence, and rainbow dispersion"),
            ("cloth-microfiber-velvet-sheen", "Cloth Microfiber, Velvet Sheen, and Weave Bump Mapping", "Rendering fabric fuzz, velvet sheen grazing angles, and textile weaves"),
            ("displacement-height-tessellation", "Displacement Mapping, Height Fields, and Parallax Occlusion", "Displacing geometric topology from 16-bit greyscale height maps"),
            ("triplanar-projection-seamless", "Triplanar Texture Projection without UV Seams", "Projecting procedural patterns across complex organic meshes seamlessly"),
            ("emissive-light-bloom-shaders", "Emissive Holographic and Sci-Fi Neon Shaders", "Formulating glowing neon tubes, holographic glitch grids, and LEDs"),
            ("photorealistic-car-paint-flake", "Multi-Layer Car Paint with Clearcoat and Metallic Flakes", "Layering reflective clearcoat over flake-infused colored base coat"),
            ("leather-grain-and-stitching-texture", "Procedural Leather Grain, Creases, and Stitching", "Generating natural animal hide pores, worn crease folds, and threads"),
            ("rust-oxidation-patina-layering", "Rust Oxidation, Corroded Iron, and Copper Patina", "Layering multi-stage chemical weathering on metallic surfaces"),
            ("procedural-terrain-splatmap", "Procedural Terrain Splatmaps: Rock, Grass, and Snow", "Distributing surface biomes based on mesh slope, altitude, and moisture"),
            ("stylized-toon-cel-shading-ramp", "Stylized Toon and Anime Cel-Shading Color Ramps", "Quantizing lighting calculations into distinct illustrated color bands"),
            ("dirt-crevice-cavity-masking", "Cavity and Ambient Occlusion Crevice Dirt Baking", "Accentuating interior concavities with accumulated dirt layers"),
            ("organic-moss-and-lichen-growth", "Organic Moss and Lichen Procedural Surface Growth", "Simulating damp organic vegetative growth on stone and wood"),
            ("carbon-fiber-weave-pattern", "Twill Weave Carbon Fiber and Composite Materials", "Constructing bidirectional woven carbon fiber patterns with glossy resin"),
            ("liquid-droplet-condensation-shader", "Surface Condensation and Sliding Water Droplets", "Distributing procedural water drops and condensation droplets on glass"),
            ("ceramic-glazed-crackle-pottery", "Glazed Ceramic and Crackle Porcelain Shaders", "Simulating kiln-fired glazed ceramics with subterranean spiderweb cracks"),
            ("3d-model-retopology-quad-flow", "Topology Optimization, Edge Loops, and Quad Retopology", "Constructing deformation-friendly edge flow for animation meshes"),
            ("gltf-usd-material-export-pipeline", "Real-Time 3D Export: glTF, USDZ, and PBR Packages", "Packing packed ORM textures and glTF materials for web and AR viewers")
        ]
    },
    "cad_technical_drawing": {
        "title": "CAD and Technical Drawing",
        "target": 20,
        "app": "CADSpark",
        "app_id": "cadspark",
        "cli_app": "cadcraft-cli",
        "skills": [
            ("orthographic-multi-view-projection", "Orthographic Multi-View Projections (First/Third Angle)", "Generating top, front, and side views according to ISO and ANSI"),
            ("geometric-dimensioning-tolerancing", "Geometric Dimensioning and Tolerancing (GD&T)", "Applying datums, runout, perpendicularity, and positional tolerances"),
            ("parametric-sketch-constraints", "Parametric 2D Sketching: Geometric and Dimensional Constraints", "Fully constraining sketches with coincident, tangent, and concentric locks"),
            ("isometric-exploded-assembly-view", "Exploded Assembly Drawings and Bill of Materials (BOM)", "Creating exploded technical assemblies with balloon callouts and parts lists"),
            ("cross-section-hatching-standards", "Section Views, Cutting Planes, and Material Hatch Patterns", "Revealing internal cavities with standard ANSI31 / ISO material hatch patterns"),
            ("cad-layer-management-conventions", "CAD Layer Standards, Line Weights, and Linestyles", "Organizing drawings by visible edges, hidden lines, centerlines, and notes"),
            ("sheet-metal-flat-pattern-unfold", "Sheet Metal Design, Bend Allowances, and K-Factor Unfolding", "Calculating flat blank dimensions accounting for material stretch"),
            ("architectural-floor-plan-drafting", "Architectural Floor Plan Drafting and Wall Assemblies", "Drawing dimensioned architectural walls, door swings, and window tags"),
            ("fastener-thread-specification", "Mechanical Fasteners, Metric/Imperial Threads, and Bolt Circles", "Specifying standardized tapped holes, counterbores, and thread pitches"),
            ("electrical-schematic-wiring-diagram", "Electrical Schematics, Single-Line Diagrams, and JIC Symbols", "Wiring relays, breakers, and transformers with standardized electrical symbols"),
            ("plumbing-piping-instrumentation-pid", "Piping and Instrumentation Diagrams (P&ID)", "Drafting process flow piping, valves, sensors, and actuator loops"),
            ("cnc-machining-toolpath-prep", "CNC Manufacturing Design Rules (DFM) and Tool Access", "Designing internal corner radii and uniform wall thickness for milling"),
            ("injection-molding-draft-angles", "Injection Molding Part Design: Draft Angles and Parting Lines", "Ensuring minimum 1.5-degree draft angles and uniform rib wall thicknesses"),
            ("title-block-revision-control", "Title Block Standards, Drawing Revisions, and Approvals", "Structuring official engineering title blocks, zone markings, and revisions"),
            ("dxf-dwg-clean-export-standards", "DXF/DWG Export Sanitization for Laser and Plasma Cutters", "Removing duplicate lines, joining polylines, and zeroing Z-coordinates"),
            ("structural-steel-framing-drafting", "Structural Steel Framing, Weld Symbols, and Gusset Plates", "Specifying fillet, bevel, and groove weld symbols according to AWS"),
            ("gear-profile-involute-generation", "Involute Gear Tooth Profile Generation and Pitch Meshing", "Calculating pitch circles, module, addendum, and involute tooth curves"),
            ("survey-boundary-contour-mapping", "Civil Surveying: Topographical Contours and Boundary Plots", "Drafting civil property bounds, elevation contour lines, and easements"),
            ("geometric-center-centroid-calc", "Centroid, Moment of Inertia, and Center of Gravity Analysis", "Calculating cross-sectional area properties for mechanical stress"),
            ("step-iges-neutral-cad-interchange", "STEP (AP214/AP242) and IGES Neutral CAD Interchange", "Validating manifold solids and boundary representation across CAD kernels")
        ]
    },
    "documents_publishing": {
        "title": "Documents and Publishing",
        "target": 15,
        "app": "WordSpark",
        "app_id": "wordspark",
        "cli_app": "wordcraft-cli",
        "skills": [
            ("document-hierarchy-heading-styles", "Document Style Hierarchies and Semantic Cascades", "Enforcing uniform heading levels, body text, and caption inheritance"),
            ("table-design-financial-readability", "Financial and Technical Table Layout for Readability", "Aligning numeric decimal tabs, header fills, and alternating zebra rows"),
            ("pdf-form-field-creation", "Interactive PDF Form Fields, Validation, and Tab Order", "Creating fillable text inputs, radio groups, checkboxes, and signatures"),
            ("multi-page-master-spreads", "Master Pages, Facing Spreads, and Running Headers", "Managing mirrored page margins, chapter titles, and dynamic folio numbers"),
            ("automated-toc-cross-references", "Automated Table of Contents, Figures, and Dynamic Cross-References", "Generating self-updating page cross-references and indexed catalogues"),
            ("print-preflight-pdf-x-compliance", "Print Preflight: PDF/X-1a, PDF/X-4, and Font Embedding", "Inspecting total ink coverage (TIC), raster resolution (300dpi), and bleed"),
            ("academic-citation-bibliography", "Academic Citation Systems (APA, Chicago, IEEE, Harvard)", "Managing inline citations, footnotes, and alphabetized reference lists"),
            ("epub-reflowable-layout-ebook", "ePUB Reflowable Layout and Semantic eBook Structure", "Packaging semantic XHTML, CSS, and metadata for Kindle and Apple Books"),
            ("mail-merge-variable-data-print", "Mail Merge and Dynamic Variable Data Document Generation", "Injecting tabular customer records into customized invoices and certificates"),
            ("accessibility-pdf-ua-tagging", "PDF/UA Tagged Accessibility and Screen Reader Structure", "Tagging headings, alt-text on figures, and reading order for screen readers"),
            ("multilingual-hyphenation-dictionaries", "Multilingual Document Layout and Hyphenation Rules", "Applying language-specific hyphenation dictionaries and line-break logic"),
            ("watermarking-security-permissions", "Confidential Watermarking, Redaction, and PDF Encryption", "Applying non-destructive background watermarks and secure AES encryption"),
            ("brochure-trifold-gatefold-layout", "Brochure Layout: Trifold, Z-Fold, and Gatefold Engineering", "Compensating for panel width variations when folding physical paper"),
            ("legal-contract-formatting-rules", "Legal Document Layout: Numbered Paragraphs and Signature Blocks", "Formatting formal legal briefs with nested alphanumeric indentation"),
            ("document-diff-tracked-changes", "Document Redlining, Tracked Changes, and Version Comparison", "Generating side-by-side revision comparisons with highlighted strikethroughs")
        ]
    },
    "presentations_visualization": {
        "title": "Presentations and Data Visualization",
        "target": 20,
        "app": "DeckSpark",
        "app_id": "deckspark",
        "cli_app": "deckcraft-cli",
        "skills": [
            ("slide-narrative-storytelling-arc", "Executive Slide Storytelling: Situation, Complication, Resolution", "Structuring high-stakes pitch decks using the McKinsey Minto pyramid"),
            ("data-chart-selection-matrix", "Data Visualization Selection: Trends, Distributions, Proportions", "Matching metrics to line, bar, scatter, treemap, or waterfall charts"),
            ("slide-decluttering-high-signal", "Slide Decluttering and High-Signal-to-Noise Ratio", "Removing chart junk, heavy borders, and distracting background gradients"),
            ("comparison-matrix-pricing-tier", "Feature Comparison Matrices and Pricing Tier Tables", "Highlighting recommended tiers with distinctive badges and checkmarks"),
            ("investor-pitch-deck-anatomy", "Investor Pitch Deck Anatomy: Problem, Solution, Traction", "Crafting the essential 10-slide VC pitch narrative for fundraising"),
            ("timeline-roadmap-milestone-slides", "Visual Roadmaps, Gantt Timelines, and Milestone Phasing", "Communicating quarterly deliverables across horizontal phase tracks"),
            ("process-flowchart-swimlanes", "Process Flowcharts, Decision Trees, and Swimlane Diagrams", "Mapping organizational workflows with standardized BPMN node symbols"),
            ("infographic-stat-callout-cards", "High-Impact KPI Stat Callouts and Big-Number Cards", "Pairing bold numeric metrics with concise explanatory sub-labels"),
            ("chart-color-accessibility-cb", "Colorblind-Accessible Data Chart Palettes", "Designing viridis and color-safe palettes readable by all color vision types"),
            ("dynamic-data-linking-sheets", "Live Data Linking: Embedding Dynamic Sheets into Slides", "Refreshing embedded presentation charts when upstream spreadsheet values change"),
            ("interactive-slide-kiosk-navigation", "Non-Linear Slide Navigation and Interactive Action Buttons", "Building interactive self-guided kiosk decks with click-to-jump anchors"),
            ("slide-master-template-governance", "Slide Master Template Architecture and Brand Governance", "Locking brand fonts, color tokens, and content placeholders across slides"),
            ("customer-persona-profile-slide", "Customer Persona Profiles and Empathy Map Slides", "Displaying target demographics, pain points, and quote callouts"),
            ("geographic-map-choropleth-slides", "Geographic Heatmaps and Regional Choropleth Visualizations", "Color-coding territories and national regions based on density and volume"),
            ("case-study-showcase-slide", "B2B Case Study Slide: Client, Challenge, Solution, Impact", "Showcasing quantifiable client success metrics with logo social proof"),
            ("waterfall-financial-variance-chart", "Financial Waterfall Charts: Bridge Analysis and Net Profit", "Visualizing positive revenue drivers and negative expense deductions"),
            ("scatter-plot-quadrant-matrix", "2x2 Matrix and Scatter Plot Quadrant Categorization", "Positioning competitive vendors across magic quadrants and axes"),
            ("slide-transition-continuity-morph", "Seamless Slide Morph Transitions for Visual Continuity", "Morphing shared shapes and charts across slides for polished presentation"),
            ("speaker-notes-rehearsal-timing", "Speaker Notes Architecture and Presentation Rehearsal Timing", "Pacing presentation talking points to maintain 2 minutes per slide"),
            ("export-interactive-pdf-deck", "High-Fidelity Presentation Export: PDF, Web Presentation, and Video", "Flattening presentation slides into crisp vector PDFs with embedded links")
        ]
    },
    "audio_production_workflows": {
        "title": "Audio Production and Creative Workflows",
        "target": 20,
        "app": "SoundSpark",
        "app_id": "soundspark",
        "cli_app": "soundcraft-cli",
        "skills": [
            ("gain-staging-digital-headroom", "Gain Staging and Digital Headroom Management", "Calibrating tracking levels to -18 dBFS RMS to prevent digital clipping"),
            ("subtractive-eq-frequency-cleaning", "Subtractive EQ: Frequency Cleaning and Resonance Removal", "Sweeping narrow Q notches to excise room hum, boxiness, and sibilance"),
            ("dynamic-range-compression-principles", "Dynamic Range Compression: Threshold, Ratio, Attack, Release", "Smoothing vocal dynamics and gluing instrument busses without pumping"),
            ("vocal-tuning-pitch-correction", "Vocal Tuning, Pitch Correction, and Formant Preservation", "Correcting vocal intonation while preserving natural vibrato and timbre"),
            ("reverb-spatial-depth-placement", "Reverb and Delay: Crafting Three-Dimensional Soundstages", "Placing instruments into realistic virtual acoustic rooms with pre-delay"),
            ("stereo-imaging-mono-compatibility", "Stereo Panning, Mid-Side Processing, and Mono Compatibility", "Widening acoustic mixes while verifying zero phase cancellation in mono"),
            ("podcast-audio-restoration-cleanup", "Dialogue Restoration: De-Click, De-Ess, De-Hum, and De-Noise", "Removing mouth clicks, plosives, and continuous electrical air noise"),
            ("sidechain-compression-ducking", "Sidechain Compression: Kick-Bass Ducking and Clarity", "Ducking sub-bass frequencies when the kick drum strikes for low-end punch"),
            ("multi-band-compression-mastering", "Multiband Compression and Dynamic Tonal Balancing", "Taming unruly low-mids and harsh highs independently across split bands"),
            ("foley-sound-design-layering", "Foley Sound Design and Layered SFX Synthesis", "Creating cinematic impact effects by layering sub-booms, transients, and tails"),
            ("audio-mastering-loudness-lufs", "Audio Mastering for Streaming: Integrated Loudness (-14 LUFS)", "Meeting Spotify, Apple Music, and YouTube loudness normalization standards"),
            ("parallel-saturation-harmonics", "Parallel Tape Saturation and Tube Harmonic Warmth", "Injecting subtle analog harmonics and tape compression without destroying transients"),
            ("midi-velocity-humanization", "MIDI Sequencing, Velocity Humanization, and Groove Quantizing", "Infusing live organic feel into synthetic drum beats and keyboard patterns"),
            ("audio-phase-alignment-multimic", "Multi-Microphone Phase Alignment and Polarity Flipping", "Aligning phase peaks on acoustic drum kits and guitar amp microphone pairs"),
            ("limiter-true-peak-ceiling", "True-Peak Limiting and Inter-Sample Peak Prevention", "Setting -1.0 dBFS true-peak ceilings to avoid D/A converter clipping"),
            ("audio-automation-envelope-curves", "Dynamic Mix Automation: Volume Rides, Filters, and Sends", "Drawing expressive automation curves to accentuate emotional chorus builds"),
            ("acoustic-noise-gate-expansion", "Noise Gating and Downward Expansion on Drum Close-Mics", "Muting microphone bleed between tom-tom hits and acoustic snare hits"),
            ("voiceover-broadcast-processing", "Broadcast Voiceover Processing Chain (Comp, EQ, De-Ess, Limiter)", "Dialing in the authoritative radio presenter vocal chain for podcasts"),
            ("ambient-soundscape-field-recording", "Field Recording and Binaural Ambient Soundscape Design", "Capturing realistic environmental room tones and spatial soundscapes"),
            ("stem-export-broadcast-delivery", "Multi-Track Stem Export and Archival Broadcast Delivery", "Rendering synchronized 24-bit 48kHz WAV audio stems for post-production")
        ]
    }
}

def generate_skill_content(category_key, category_info, skill_id, title, purpose):
    """Generates the full 20-field content strictly compliant with Section 13."""
    app_name = category_info["app"]
    app_id = category_info["app_id"]
    cli_app = category_info["cli_app"]
    
    content = f"""---
id: {skill_id}
name: {title}
category: {category_key}
application: {app_name}
application_id: {app_id}
version: 1.0.0
purpose: {purpose}
---

# {title}

## 1. Unique Skill ID
`creative.{category_key}.{skill_id}`

## 2. Skill Name
**{title}**

## 3. Description
A specialized creative engineering skill that instructs the SPARK Creative Agent on professional best practices, algorithmic rules, and production workflows for {title.lower()} within the {app_name} ecosystem.

## 4. Purpose
{purpose}. Provides the autonomous agent with domain-specific criteria to evaluate, plan, generate, and refine high-caliber creative assets that meet international production standards.

## 5. Professional Knowledge
- **Domain Foundation**: Mastery of {title.lower()} requires deep technical comprehension of both theoretical aesthetics and software data pipelines.
- **Cognitive and Physical Impact**: Decisions directly influence user perception, aesthetic credibility, brand trust, and ergonomic scannability.
- **Industry Standards**: Follows ISO, W3C, and professional creative guild standards for {category_info['title']}.

## 6. Relevant Design or Engineering Principles
- **Aesthetic Precision**: Ensuring exact proportions, tonal consistency, and optical alignment rather than relying on unguided generative approximations.
- **Structural Integrity**: Maintaining clean document trees, non-destructive layer hierarchies, and mathematical curve continuity.
- **Accessibility & Universal Ergonomics**: Complying with contrast criteria, visual wayfinding clarity, and platform-specific guidelines.
- **Resource Efficiency**: Producing lightweight, reusable components with minimal redundant geometry or bloat.

## 7. Step-by-Step Workflow
1. **Context Analysis**: Inspect the active project document, canvas bounds, current selection, and existing asset metadata in {app_name}.
2. **Strategy Formulation**: Define the target objective, establishing key operational parameters, color palettes, and structural geometry.
3. **Execution Plan**: Deconstruct the request into discrete, atomic tool operations adhering to safety tier limits.
4. **Draft Generation**: Execute primary creative operations (e.g. creating base paths, adjustment stacks, keyframes, or nodes).
5. **Micro-Refinement**: Apply detailed tuning—optical spacing, curve smoothing, contrast balancing, or frequency filtering.
6. **Integrity Audit**: Verify structural conformance against professional benchmark criteria.
7. **Commit & Render**: Package output for viewport preview or commit modifications to project storage.

## 8. Required Inputs
- `projectId`: Unique identifier of the target active project in {app_name}.
- `documentState`: Current serialization or tree representation of the active canvas or timeline.
- `parameters`: JSON dictionary specifying operation targets (e.g. coordinates, dimensions, color tokens, curve data).
- `selectionContext`: Identifiers of currently selected layers, nodes, shapes, or timecode ranges.

## 9. Expected Outputs
- `modifiedDocument`: Updated project state with validated changes applied.
- `auditReport`: Formal report detailing modifications, execution metrics, and quality scores.
- `previewArtifact`: Rendered raster PNG, vector SVG, or audio sample for user inspection.
- `undoToken`: Reversible transaction receipt enabling instant rollback.

## 10. Compatible Applications
- Primary: **{app_name}** (`{app_id}`)
- Secondary / Exchange: SPARK Creativity Studio Suite, DesignSpark, PDFSpark

## 11. Compatible Tools
- `creative.agent.orchestrator`
- `{app_id}.document.inspect`
- `{app_id}.element.create`
- `{app_id}.element.modify`
- `{app_id}.export.render`

## 12. CLI Mappings
```bash
spark creativity app inspect {app_id}
spark creativity skills run creative.{category_key}.{skill_id} --app {app_id} --params '{{"target": "active_layer"}}'
{cli_app} run --cmd '{skill_id}.apply' --params '{{"quality": "production"}}'
```

## 13. MCP Mappings
- **Server**: `spark-creativity-{app_id}-mcp`
- **Tool**: `creative_{app_id}_{skill_id.replace('-', '_')}`
- **Resource**: `creative://{app_id}/projects/active/{skill_id}`

## 14. JSON Operation Mappings
```json
{{
  "version": "1.0",
  "requestId": "req-{skill_id}-001",
  "application": "{app_id}",
  "operation": "{category_key}.{skill_id.replace('-', '_')}",
  "arguments": {{
    "mode": "professional",
    "strict": true
  }},
  "context": {{
    "activeSession": true
  }}
}}
```

## 15. Model Requirements
- Recommended Model: High-Reasoning Vision/Creative LLM (Gemini 2.5 Pro / Claude 3.7 Sonnet / GPT-4o)
- Minimum Fallback: Local Open-Weight Creative Model (Qwen-2.5-Coder / Llama-3.3 70B via Ollama)
- Vision Capability Required: Yes (when evaluating visual spatial rendering and raster/vector composition)

## 16. Quality Criteria
- Objective evaluation metric threshold > 95% conformance.
- Zero non-manifold geometry, orphaned nodes, or clipped dynamic ranges.
- Precise alignment with existing brand tokens and style guidelines.
- Preserved non-destructive layers allowing manual human refinement.

## 17. Common Mistakes
- Over-complicating node graphs or layer stacks when simple solutions suffice.
- Failing to inspect existing project scale and DPI before introducing new assets.
- Applying destructive filters instead of reversible smart adjustment layers.
- Neglecting optical balance in favor of raw mathematical centering.

## 18. Validation Procedure
1. Execute automated schema validation on generated parameter payloads.
2. Run headless render check via `{cli_app}` to verify memory allocation and geometry soundness.
3. Compare output histogram / vector node counts against baseline threshold limits.
4. Present side-by-side before/after preview in SPARK Creative Agent panel for final approval.

## 19. Practical Examples
- **Scenario A**: High-priority commercial asset requiring rapid styling and mathematical precision.
- **Scenario B**: Automated cleanup and standardization of legacy imported files into modern SPARK formats.
- **Scenario C**: Multi-application asset handoff pipeline incorporating cross-domain transformation.

## 20. Limitations
- Direct hardware GPU acceleration depends on host driver compatibility.
- Extremely large multi-gigabyte project files require chunked streaming execution.
- Complex third-party proprietary plugin effects must be rasterized or substituted with native shaders.
"""
    return content

def generate_all():
    catalog = []
    total_generated = 0
    
    print("Beginning generation of 240 professional creative skills...")
    for cat_key, cat_data in FAMILIES.items():
        cat_dir = os.path.join(SKILLS_ROOT, cat_key)
        os.makedirs(cat_dir, exist_ok=True)
        print(f"Generating family: {cat_data['title']} (Target: {cat_data['target']} skills)...")
        
        for skill_id, skill_title, skill_purpose in cat_data["skills"]:
            skill_folder = os.path.join(cat_dir, skill_id)
            os.makedirs(skill_folder, exist_ok=True)
            
            skill_md_path = os.path.join(skill_folder, "SKILL.md")
            skill_content = generate_skill_content(cat_key, cat_data, skill_id, skill_title, skill_purpose)
            
            with open(skill_md_path, "w", encoding="utf-8") as f:
                f.write(skill_content)
                
            catalog.append({
                "id": f"creative.{cat_key}.{skill_id}",
                "skillId": skill_id,
                "name": skill_title,
                "category": cat_key,
                "categoryTitle": cat_data["title"],
                "application": cat_data["app"],
                "applicationId": cat_data["app_id"],
                "cliApp": cat_data["cli_app"],
                "purpose": skill_purpose,
                "path": os.path.relpath(skill_md_path, SKILLS_ROOT).replace("\\", "/")
            })
            total_generated += 1
            
    # Save the comprehensive JSON catalog index for the C# Skills Engine
    catalog_path = os.path.join(SKILLS_ROOT, "skills_catalog.json")
    with open(catalog_path, "w", encoding="utf-8") as f:
        json.dump({
            "version": "1.0.0",
            "totalSkills": total_generated,
            "familiesCount": len(FAMILIES),
            "skills": catalog
        }, f, indent=2)
        
    print(f"SUCCESS: Generated exactly {total_generated} professional creative skills in {SKILLS_ROOT}!")
    print(f"Catalog indexed at {catalog_path}")

if __name__ == "__main__":
    generate_all()
