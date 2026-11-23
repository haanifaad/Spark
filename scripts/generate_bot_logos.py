"""
Generates the 16 SPARK Creative Application Bot Logos based on the official SPARK flame mascot.
Creates high-fidelity badges, motifs, and vector-style overlays for:
PhotoSpark, VectorSpark, FilmSpark, LightSpark, PDFSpark, EffectSpark, AniSpark,
DesignSpark, FigSpark, TextureSpark, MaterialSpark, SoundSpark, WordSpark, DeckSpark,
CADSpark, GridSpark.
"""
import os
import math
from PIL import Image, ImageDraw, ImageFilter, ImageEnhance

BASE_MASCOT_PATH = r"C:\Intel\Spark\Spark-main\src\OpenClaw.Tray.WinUI\Assets\Setup\SparkMascot.png"
OUTPUT_DIR = r"C:\Intel\Spark\Spark-main\src\OpenClaw.Tray.WinUI\Assets\Creative\Bots"
os.makedirs(OUTPUT_DIR, exist_ok=True)

# Application definitions with visual motif parameters
BOT_DEFS = {
    "photospark": {
        "name": "PhotoSpark",
        "title": "PhotoSpark Raster Bot",
        "theme_color": (0, 180, 255),    # Cyan/Blue
        "accent_color": (255, 215, 0),   # Golden
        "motif": "camera_lens",
    },
    "vectorspark": {
        "name": "VectorSpark",
        "title": "VectorSpark Vector Bot",
        "theme_color": (255, 120, 0),    # Vibrant Orange-Red
        "accent_color": (0, 240, 255),   # Electric Cyan
        "motif": "bezier_pen",
    },
    "filmspark": {
        "name": "FilmSpark",
        "title": "FilmSpark Video Bot",
        "theme_color": (240, 60, 110),   # Magenta/Film Pink
        "accent_color": (255, 255, 255),
        "motif": "film_strip",
    },
    "lightspark": {
        "name": "LightSpark",
        "title": "LightSpark Photo & RAW Bot",
        "theme_color": (255, 190, 40),   # Warm Sunlight Amber
        "accent_color": (255, 255, 230),
        "motif": "aperture",
    },
    "pdfspark": {
        "name": "PDFSpark",
        "title": "PDFSpark Document Bot",
        "theme_color": (230, 40, 40),    # PDF Crimson
        "accent_color": (255, 255, 255),
        "motif": "pdf_sheet",
    },
    "effectspark": {
        "name": "EffectSpark",
        "title": "EffectSpark VFX Bot",
        "theme_color": (160, 50, 255),   # Violet Purple
        "accent_color": (0, 255, 220),   # Aqua Glow
        "motif": "vfx_burst",
    },
    "anispark": {
        "name": "AniSpark",
        "title": "AniSpark 2D Animation Bot",
        "theme_color": (255, 90, 160),   # Animation Bubblegum
        "accent_color": (255, 240, 80),
        "motif": "animation_timeline",
    },
    "designspark": {
        "name": "DesignSpark",
        "title": "DesignSpark Publishing Bot",
        "theme_color": (180, 40, 220),   # Editorial Purple
        "accent_color": (255, 160, 50),
        "motif": "layout_grid",
    },
    "figspark": {
        "name": "FigSpark",
        "title": "FigSpark UI/UX Bot",
        "theme_color": (15, 170, 255),   # Figma Sky Blue
        "accent_color": (10, 210, 140),  # Mint Green
        "motif": "ui_frame",
    },
    "texturespark": {
        "name": "TextureSpark",
        "title": "TextureSpark 3D Texture Bot",
        "theme_color": (220, 140, 40),   # Ochre/Clay
        "accent_color": (80, 220, 255),
        "motif": "uv_checker",
    },
    "materialspark": {
        "name": "MaterialSpark",
        "title": "MaterialSpark Procedural Bot",
        "theme_color": (40, 190, 120),   # Emerald Shader Green
        "accent_color": (255, 200, 50),
        "motif": "material_nodes",
    },
    "soundspark": {
        "name": "SoundSpark",
        "title": "SoundSpark Audio Bot",
        "theme_color": (30, 210, 255),   # Waveform Cyan
        "accent_color": (150, 255, 100), # Lime
        "motif": "sound_wave",
    },
    "wordspark": {
        "name": "WordSpark",
        "title": "WordSpark Writing Bot",
        "theme_color": (30, 110, 230),   # Classic Word Royal Blue
        "accent_color": (255, 255, 255),
        "motif": "quill_doc",
    },
    "deckspark": {
        "name": "DeckSpark",
        "title": "DeckSpark Presentation Bot",
        "theme_color": (235, 100, 30),   # PowerPoint Coral Orange
        "accent_color": (255, 230, 100),
        "motif": "slide_chart",
    },
    "cadspark": {
        "name": "CADSpark",
        "title": "CADSpark Technical Bot",
        "theme_color": (0, 150, 200),    # Blueprint Blue
        "accent_color": (255, 230, 80),  # Drafting Yellow
        "motif": "cad_calipers",
    },
    "gridspark": {
        "name": "GridSpark",
        "title": "GridSpark Spreadsheet Bot",
        "theme_color": (20, 160, 80),    # Excel Emerald Green
        "accent_color": (255, 255, 255),
        "motif": "spreadsheet_matrix",
    },
}

def draw_badge_background(draw, cx, cy, radius, fill_color, border_color):
    """Draws a polished glowing round badge for the motif."""
    # Outer glow
    for r in range(radius + 6, radius, -1):
        alpha = int(40 * (radius + 6 - r) / 6)
        draw.ellipse([cx - r, cy - r, cx + r, cy + r], outline=(*border_color[:3], alpha), width=2)
    # Fill
    draw.ellipse([cx - radius, cy - radius, cx + radius, cy + radius], fill=(*fill_color[:3], 240), outline=(*border_color[:3], 255), width=3)

def render_motif(draw, motif, cx, cy, radius, primary, accent):
    """Renders the application-specific visual motif inside/around the badge."""
    if motif == "camera_lens":
        # Camera lens aperture and reflective glass
        draw_badge_background(draw, cx, cy, radius, (20, 30, 45), primary)
        # Inner lens rings
        draw.ellipse([cx - radius*0.7, cy - radius*0.7, cx + radius*0.7, cy + radius*0.7], outline=primary, width=3)
        draw.ellipse([cx - radius*0.4, cy - radius*0.4, cx + radius*0.4, cy + radius*0.4], fill=(10, 20, 35), outline=accent, width=2)
        # Glare arc
        draw.arc([cx - radius*0.55, cy - radius*0.55, cx + radius*0.55, cy + radius*0.55], start=210, end=310, fill=(255, 255, 255, 200), width=3)
        # Lens dot
        draw.ellipse([cx - 4, cy - 4, cx + 4, cy + 4], fill=accent)

    elif motif == "bezier_pen":
        # Pen tool nib and Bézier curve handles
        draw_badge_background(draw, cx, cy, radius, (30, 20, 20), primary)
        # Bézier curve
        points = [(cx - radius*0.7, cy + radius*0.3), (cx - radius*0.2, cy - radius*0.6), (cx + radius*0.5, cy + radius*0.2)]
        draw.line(points, fill=accent, width=3)
        # Handle lines & nodes
        draw.line([(cx - radius*0.5, cy - radius*0.4), (cx + radius*0.1, cy - radius*0.4)], fill=(200, 200, 200), width=1)
        draw.rectangle([cx - radius*0.5 - 4, cy - radius*0.4 - 4, cx - radius*0.5 + 4, cy - radius*0.4 + 4], fill=primary, outline=(255, 255, 255))
        draw.rectangle([cx + radius*0.1 - 4, cy - radius*0.4 - 4, cx + radius*0.1 + 4, cy - radius*0.4 + 4], fill=primary, outline=(255, 255, 255))
        # Pen nib tip
        nib = [(cx + 2, cy - radius*0.2), (cx + radius*0.4, cy + radius*0.4), (cx + radius*0.1, cy + radius*0.5), (cx - radius*0.1, cy + radius*0.2)]
        draw.polygon(nib, fill=(240, 240, 240), outline=primary)

    elif motif == "film_strip":
        # 35mm film strip cell
        draw_badge_background(draw, cx, cy, radius, (25, 15, 30), primary)
        w, h = radius * 1.2, radius * 0.9
        x0, y0 = cx - w/2, cy - h/2
        draw.rectangle([x0, y0, x0 + w, y0 + h], fill=(15, 15, 20), outline=primary, width=2)
        # Perforations
        for i in range(4):
            px = x0 + 6 + i * (w - 12) / 3
            draw.rectangle([px - 3, y0 + 3, px + 3, y0 + 7], fill=accent)
            draw.rectangle([px - 3, y0 + h - 7, px + 3, y0 + h - 3], fill=accent)
        # Play triangle inside
        draw.polygon([(cx - 8, cy - 10), (cx - 8, cy + 10), (cx + 12, cy)], fill=primary)

    elif motif == "aperture":
        # Multi-blade aperture diaphragm
        draw_badge_background(draw, cx, cy, radius, (35, 25, 10), primary)
        num_blades = 6
        for i in range(num_blades):
            ang = i * (2 * math.pi / num_blades)
            x1 = cx + math.cos(ang) * radius * 0.75
            y1 = cy + math.sin(ang) * radius * 0.75
            x2 = cx + math.cos(ang + 1.2) * radius * 0.35
            y2 = cy + math.sin(ang + 1.2) * radius * 0.35
            draw.line([(x1, y1), (x2, y2)], fill=accent, width=2)
        draw.ellipse([cx - radius*0.25, cy - radius*0.25, cx + radius*0.25, cy + radius*0.25], outline=primary, width=2)

    elif motif == "pdf_sheet":
        # Crisp folded page with PDF badge
        draw_badge_background(draw, cx, cy, radius, (35, 15, 15), primary)
        w, h = radius * 0.9, radius * 1.2
        x0, y0 = cx - w/2, cy - h/2
        # Document sheet
        draw.rectangle([x0, y0, x0 + w - 8, y0 + h], fill=(245, 245, 245), outline=primary, width=1)
        # Folded corner
        draw.polygon([(x0 + w - 8, y0), (x0 + w, y0 + 8), (x0 + w - 8, y0 + 8)], fill=(200, 200, 200))
        # Text lines
        draw.line([(x0 + 6, y0 + 12), (x0 + w - 14, y0 + 12)], fill=primary, width=3)
        draw.line([(x0 + 6, y0 + 20), (x0 + w - 10, y0 + 20)], fill=(150, 150, 150), width=2)
        draw.line([(x0 + 6, y0 + 27), (x0 + w - 16, y0 + 27)], fill=(150, 150, 150), width=2)
        # Red ribbon banner
        draw.rectangle([x0 - 2, y0 + h - 14, x0 + w + 2, y0 + h - 2], fill=primary)

    elif motif == "vfx_burst":
        # Dynamic particle burst and energy rings
        draw_badge_background(draw, cx, cy, radius, (25, 10, 40), primary)
        for i in range(8):
            ang = i * (math.pi / 4)
            x1 = cx + math.cos(ang) * radius * 0.3
            y1 = cy + math.sin(ang) * radius * 0.3
            x2 = cx + math.cos(ang) * radius * 0.8
            y2 = cy + math.sin(ang) * radius * 0.8
            draw.line([(x1, y1), (x2, y2)], fill=accent, width=2)
        # Center glowing star
        draw.ellipse([cx - 8, cy - 8, cx + 8, cy + 8], fill=primary)
        draw.ellipse([cx - 4, cy - 4, cx + 4, cy + 4], fill=(255, 255, 255))

    elif motif == "animation_timeline":
        # Timeline track and keyframe diamonds
        draw_badge_background(draw, cx, cy, radius, (40, 15, 30), primary)
        draw.line([(cx - radius*0.8, cy), (cx + radius*0.8, cy)], fill=(200, 200, 200), width=2)
        # Keyframe diamonds
        for offset in [-radius*0.5, 0, radius*0.5]:
            kx = cx + offset
            diamond = [(kx, cy - 8), (kx + 8, cy), (kx, cy + 8), (kx - 8, cy)]
            draw.polygon(diamond, fill=accent if offset == 0 else primary, outline=(255, 255, 255), width=1)
        # Playhead line
        draw.line([(cx, cy - radius*0.6), (cx, cy + radius*0.6)], fill=accent, width=2)

    elif motif == "layout_grid":
        # Publication column grid and baseline guides
        draw_badge_background(draw, cx, cy, radius, (30, 15, 35), primary)
        w, h = radius * 1.1, radius * 1.1
        x0, y0 = cx - w/2, cy - h/2
        # Two-column box
        col_w = (w - 6) / 2
        draw.rectangle([x0, y0, x0 + col_w, y0 + h], outline=primary, width=2)
        draw.rectangle([x0 + col_w + 6, y0, x0 + w, y0 + h], outline=primary, width=2)
        # Header block
        draw.rectangle([x0 + 4, y0 + 4, x0 + col_w - 4, y0 + 12], fill=accent)
        # Body text bars
        for y in range(int(y0 + 18), int(y0 + h - 6), 6):
            draw.line([(x0 + 4, y), (x0 + col_w - 4, y)], fill=(180, 180, 200), width=2)
            draw.line([(x0 + col_w + 10, y), (x0 + w - 4, y)], fill=(180, 180, 200), width=2)

    elif motif == "ui_frame":
        # UI component box with corner radius tags and auto-layout
        draw_badge_background(draw, cx, cy, radius, (15, 25, 40), primary)
        w, h = radius * 1.1, radius * 0.9
        x0, y0 = cx - w/2, cy - h/2
        draw.rounded_rectangle([x0, y0, x0 + w, y0 + h], radius=6, outline=primary, width=2)
        # Nested button / component
        draw.rounded_rectangle([x0 + 6, y0 + 6, x0 + w - 6, y0 + 18], radius=4, fill=accent)
        # Cards inside
        cw = (w - 18) / 2
        draw.rounded_rectangle([x0 + 6, y0 + 24, x0 + 6 + cw, y0 + h - 6], radius=3, outline=(150, 180, 220), width=1)
        draw.rounded_rectangle([x0 + 12 + cw, y0 + 24, x0 + w - 6, y0 + h - 6], radius=3, outline=(150, 180, 220), width=1)

    elif motif == "uv_checker":
        # 3D texture UV checkerboard cube
        draw_badge_background(draw, cx, cy, radius, (35, 25, 15), primary)
        grid_s = 4
        sz = (radius * 1.2) / grid_s
        start_x, start_y = cx - (radius * 0.6), cy - (radius * 0.6)
        for r in range(grid_s):
            for c in range(grid_s):
                color = primary if (r + c) % 2 == 0 else (40, 40, 45)
                draw.rectangle([start_x + c*sz, start_y + r*sz, start_x + (c+1)*sz, start_y + (r+1)*sz], fill=color, outline=accent if (r==c==1) else None)
        # Texture brush tip accent
        draw.line([(cx + radius*0.3, cy + radius*0.5), (cx + radius*0.7, cy + radius*0.1)], fill=accent, width=3)

    elif motif == "material_nodes":
        # Procedural node graph with sockets
        draw_badge_background(draw, cx, cy, radius, (15, 35, 25), primary)
        # Input node
        draw.rectangle([cx - radius*0.7, cy - radius*0.4, cx - radius*0.2, cy - radius*0.05], fill=(30, 40, 35), outline=accent, width=2)
        # Input node 2
        draw.rectangle([cx - radius*0.7, cy + radius*0.1, cx - radius*0.2, cy + radius*0.45], fill=(30, 40, 35), outline=accent, width=2)
        # Output node
        draw.rectangle([cx + radius*0.2, cy - radius*0.2, cx + radius*0.7, cy + radius*0.25], fill=(30, 40, 35), outline=primary, width=2)
        # Connecting wires (splines)
        draw.line([(cx - radius*0.2, cy - radius*0.22), (cx + radius*0.2, cy)], fill=accent, width=2)
        draw.line([(cx - radius*0.2, cy + radius*0.28), (cx + radius*0.2, cy)], fill=primary, width=2)

    elif motif == "sound_wave":
        # Equalizer audio spectrum waveform
        draw_badge_background(draw, cx, cy, radius, (15, 30, 40), primary)
        bar_heights = [0.3, 0.6, 0.9, 0.4, 0.75, 1.0, 0.65, 0.45, 0.8, 0.35]
        bar_w = 4
        gap = 3
        total_w = len(bar_heights) * (bar_w + gap) - gap
        sx = cx - total_w / 2
        for i, bh in enumerate(bar_heights):
            bx = sx + i * (bar_w + gap)
            h = bh * radius * 1.1
            color = accent if i in [4, 5] else primary
            draw.rectangle([bx, cy - h/2, bx + bar_w, cy + h/2], fill=color)

    elif motif == "quill_doc":
        # Stylized writing quill / fountain pen and parchment
        draw_badge_background(draw, cx, cy, radius, (15, 25, 45), primary)
        # Slanted document
        draw.rectangle([cx - radius*0.6, cy - radius*0.4, cx + radius*0.3, cy + radius*0.5], fill=(240, 240, 245), outline=primary, width=2)
        for y in range(int(cy - radius*0.2), int(cy + radius*0.4), 8):
            draw.line([(cx - radius*0.45, y), (cx + radius*0.15, y)], fill=(160, 170, 190), width=2)
        # Slanted pen nib
        pen = [(cx + radius*0.4, cy - radius*0.6), (cx + radius*0.6, cy - radius*0.4), (cx + radius*0.1, cy + radius*0.2), (cx, cy + radius*0.1)]
        draw.polygon(pen, fill=accent, outline=(255, 255, 255))

    elif motif == "slide_chart":
        # Presentation slide display with bar chart
        draw_badge_background(draw, cx, cy, radius, (40, 20, 15), primary)
        w, h = radius * 1.2, radius * 0.9
        x0, y0 = cx - w/2, cy - h/2
        draw.rectangle([x0, y0, x0 + w, y0 + h], fill=(255, 255, 255), outline=primary, width=2)
        # Presentation easel stand
        draw.line([(cx - 8, y0 + h), (cx - 14, y0 + h + 12)], fill=(120, 120, 120), width=2)
        draw.line([(cx + 8, y0 + h), (cx + 14, y0 + h + 12)], fill=(120, 120, 120), width=2)
        # Bar chart on slide
        bars = [0.35, 0.6, 0.85, 0.5]
        bw = 8
        for i, bh in enumerate(bars):
            bx = x0 + 10 + i * 14
            by2 = y0 + h - 6
            by1 = by2 - (bh * (h - 16))
            draw.rectangle([bx, by1, bx + bw, by2], fill=primary if i==2 else accent)

    elif motif == "cad_calipers":
        # Blueprint grid and precision drafting caliper/compass
        draw_badge_background(draw, cx, cy, radius, (10, 30, 45), primary)
        # Blueprint grid lines
        for offset in [-radius*0.5, 0, radius*0.5]:
            draw.line([(cx - radius*0.8, cy + offset), (cx + radius*0.8, cy + offset)], fill=(30, 60, 90), width=1)
            draw.line([(cx + offset, cy - radius*0.8), (cx + offset, cy + radius*0.8)], fill=(30, 60, 90), width=1)
        # Drafting compass legs
        draw.line([(cx, cy - radius*0.5), (cx - radius*0.4, cy + radius*0.4)], fill=accent, width=3)
        draw.line([(cx, cy - radius*0.5), (cx + radius*0.4, cy + radius*0.4)], fill=accent, width=3)
        # Pivot circle
        draw.ellipse([cx - 5, cy - radius*0.5 - 5, cx + 5, cy - radius*0.5 + 5], fill=primary, outline=(255, 255, 255))
        # Measured arc
        draw.arc([cx - radius*0.4, cy + radius*0.1, cx + radius*0.4, cy + radius*0.6], start=30, end=150, fill=primary, width=2)

    elif motif == "spreadsheet_matrix":
        # Emerald spreadsheet grid with highlighted cell and formula bar
        draw_badge_background(draw, cx, cy, radius, (10, 35, 20), primary)
        w, h = radius * 1.2, radius * 0.9
        x0, y0 = cx - w/2, cy - h/2
        draw.rectangle([x0, y0, x0 + w, y0 + h], fill=(250, 255, 250), outline=primary, width=2)
        # Green header bar
        draw.rectangle([x0, y0, x0 + w, y0 + 8], fill=primary)
        # Grid cells
        cols = 3
        rows = 3
        cw = w / cols
        ch = (h - 8) / rows
        for r in range(rows):
            for c in range(cols):
                rx0 = x0 + c * cw
                ry0 = y0 + 8 + r * ch
                draw.rectangle([rx0, ry0, rx0 + cw, ry0 + ch], outline=(180, 200, 180), width=1)
        # Highlight active cell (row 1, col 1)
        draw.rectangle([x0 + cw, y0 + 8 + ch, x0 + 2*cw, y0 + 8 + 2*ch], outline=primary, fill=(220, 250, 230), width=2)

def generate_all():
    print(f"Loading base mascot from {BASE_MASCOT_PATH}...")
    base_mascot = Image.open(BASE_MASCOT_PATH).convert("RGBA")
    
    # Save base mascot to Creative Assets as SparkCreativeMascot.png
    base_mascot.save(os.path.join(OUTPUT_DIR, "SparkCreativeMascot.png"))

    for app_id, data in BOT_DEFS.items():
        print(f"Generating Bot Logo for {data['name']} ({app_id})...")
        # Create a fresh copy of base mascot
        canvas = Image.new("RGBA", (512, 512), (0, 0, 0, 0))
        
        # Slight tinting/ambient reflection on the flame character to subtly associate with app palette
        mascot_copy = base_mascot.copy()
        
        # Draw badge motif overlay on top-right / chest region
        # Chest/Badge center: (370, 370) or lower right medallion
        badge_canvas = Image.new("RGBA", (512, 512), (0, 0, 0, 0))
        badge_draw = ImageDraw.Draw(badge_canvas)
        
        # Position badge dynamically on lower-right side of mascot
        cx, cy, radius = 375, 375, 68
        render_motif(badge_draw, data["motif"], cx, cy, radius, data["theme_color"], data["accent_color"])
        
        # Composite base mascot and motif
        canvas.alpha_composite(mascot_copy)
        canvas.alpha_composite(badge_canvas)
        
        # Save 512x512 Master PNG
        png_path = os.path.join(OUTPUT_DIR, f"{app_id}.png")
        canvas.save(png_path, "PNG")
        
        # Save multiple icon sizes (256, 128, 64, 32)
        sizes = [256, 128, 64, 32]
        icon_images = []
        for sz in sizes:
            resized = canvas.resize((sz, sz), Image.Resampling.LANCZOS)
            resized.save(os.path.join(OUTPUT_DIR, f"{app_id}_{sz}.png"), "PNG")
            icon_images.append(resized)
            
        # Save Windows .ICO
        ico_path = os.path.join(OUTPUT_DIR, f"{app_id}.ico")
        canvas.save(ico_path, format="ICO", sizes=[(256, 256), (128, 128), (64, 64), (32, 32), (16, 16)])
        
        # Also copy to the extracted Craft application directory if it exists!
        craft_extracted_app = rf"C:\Intel\Spark\Craft\extracted\{app_id.replace('spark', 'craft')}-main"
        if os.path.exists(craft_extracted_app):
            craft_icons_dir = os.path.join(craft_extracted_app, "assets", "app-icon")
            os.makedirs(craft_icons_dir, exist_ok=True)
            canvas.save(os.path.join(craft_icons_dir, f"{app_id}-512.png"))
            canvas.resize((256, 256), Image.Resampling.LANCZOS).save(os.path.join(craft_icons_dir, f"{app_id}-256.png"))
            canvas.save(os.path.join(craft_icons_dir, f"{app_id}.ico"), format="ICO", sizes=[(256, 256), (128, 128), (64, 64), (32, 32)])

    print("All 16 SPARK Creative Bot Logos successfully generated and deployed!")

if __name__ == "__main__":
    generate_all()
