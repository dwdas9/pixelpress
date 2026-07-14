# PixelPress Feature Audit vs Squoosh

**Date**: 2026-07-14  
**Purpose**: Compare implemented features in PixelPress against Google Squoosh to identify what's missing.

---

## Input Formats (Decodable)

### PixelPress ✓
- JPEG / JPG / JPE / JFIF
- PNG
- WebP
- AVIF
- HEIC / HEIF (no encoder)
- TIFF / TIF
- BMP / DIB
- GIF
- JPEG XL / JXL
- ICO (no encoder)
- Camera RAW / DNG / CR2 / CR3 / NEF / ARW / ORF / RW2 / RAF (no encoder)

### Squoosh (inferred from screenshots)
- JPEG
- PNG
- WebP
- AVIF
- JPEG XL
- GIF
- Likely others

**Status**: ✓ PixelPress covers a broader input range including HEIC and RAW.

---

## Output Formats (Encodable)

### PixelPress ✓
- JPEG
- PNG
- WebP
- AVIF
- TIFF
- BMP
- GIF
- JPEG XL

### Squoosh (inferred from screenshots)
- Same as above, plus possibly others

**Status**: ✓ Feature parity.

---

## Compression Controls

### PixelPress ✓
| Control | Type | Range | Notes |
|---------|------|-------|-------|
| Quality | Single dial | 1–100 | Unified across lossy formats (ADR-0006) |
| Resize | Limit longest edge | 16–20,000 px | Independent of quality |
| Metadata | Strip / Preserve | Toggle | EXIF, GPS, ICC profiles |

### Squoosh
| Feature | Scope |
|---------|-------|
| Quality | Per-codec dial (0–100 or variant ranges) |
| Format codecs | Multiple algorithms per format |
| Quantization methods | 8+ named algorithms (ImageMagick, JPEG Annex K, MSSIM-tuned, etc.) |
| Channels | Grayscale, RGB, YCbCr selection |
| Chroma subsampling | Auto subsample chroma toggle |
| Chroma quality | Separate slider per channel |
| Progressive rendering | Toggle for JPEG |
| Smoothing | Slider (0–100) |
| Trellis quantization | Toggle + tuning parameters |

**Gap Analysis**:
- ❌ **No per-codec parameters**: PixelPress maps all codecs through Magick.NET's uniform quality dial; Squoosh exposes codec-specific tuning.
- ❌ **No quantization algorithm selection**: Squoosh offers 8+ named quantization methods; PixelPress uses Magick.NET defaults.
- ❌ **No channel selection**: Squoosh allows Grayscale/RGB/YCbCr; PixelPress applies full RGB encoding always.
- ❌ **No chroma subsampling control**: Squoosh toggles auto subsampling and separate chroma quality; PixelPress uses codec defaults.
- ❌ **No progressive JPEG**: Squoosh toggles progressive rendering; PixelPress does not.
- ❌ **No smoothing/trellis/advanced codec tuning**: These are all unavailable.

---

## Architecture Constraints

### Why the Gap Exists

**PixelPress codec layer** (MagickImageCodec.cs, line 51):
```csharp
private static void ApplyQuality(IMagickImage<byte> image, ImageFormatId format, int quality)
{
    if (FormatRegistry.Get(format).HasQualityDial)
    {
        image.Quality = (uint)quality;  // ← Unified quality only
    }
}
```

The app was explicitly designed (ADR-0006) to have *one* quality slider across all formats, not per-format tuning. The comment in MagickImageCodec.cs (line 141–142) notes:

> PNG compression-level tuning is a deferred follow-up — see ARCHITECTURE.md M4 notes. Default compression is used for now.

**Magick.NET** wrapper limits access:
- Magick.NET Q8 bundles expose a simplified API (Quality property for lossy, little else).
- Codec-specific parameters (quantization, chroma handling) would require:
  1. Exposing lower-level ImageMagick -define flags via MagickImage.SetArtifact() or similar.
  2. Building a per-codec settings model in JobRequest.
  3. Threading those through the UI and planner.

---

## Batch Operations

### PixelPress ✓
- File queue with thumbnail previews
- Format fallback (e.g., HEIC → JPEG auto-conversion)
- File rename conflict resolution
- Skip reasons (not an image, missing, duplicate)
- Batch size estimation + sample-based measurement
- Inflation guard (ADR-0007: keep original if output is larger)
- Per-file encoding status (✓ Optimized, = Unchanged, ▲ Inflated, ! Failed)

### Squoosh
- Side-by-side comparison (before/after drag divider)
- Per-image estimate
- No explicit batch operations shown

**Status**: ✓ PixelPress has richer batch UX.

---

## Preview & Comparison

### PixelPress ✓
- **Live preview**: Before/after at current settings, real-time as quality slider moves
- **Comparison divider**: Drag-to-pan wipe between original and output
- **Zoom controls**: Fit to window, 100%, zoom in/out, pan
- **Accuracy markers**: "Exact. This image encoded" vs. "Estimated"
- **Per-image stats**: Original/output size, savings %, dimensions, resize status

### Squoosh
- **Side-by-side with draggable divider** (your screenshots show this)
- **Format selector dropdown** within the preview

**Status**: ✓ PixelPress has more detailed stats; Squoosh has format picker in the preview pane.

---

## Recommendations

### Short Term (High Impact)
1. **Progressive JPEG toggle**: Add one boolean flag in Inspector > Compression. Exposes MagickImage.Interlace when encoding JPEG.
   - **Effort**: 1–2 hours (UI + JobRequest + MagickImageCodec)
   - **Impact**: Addresses a real web performance concern.

2. **Grayscale conversion**: Checkbox to desaturate before encoding.
   - **Effort**: 1 hour (single boolean property + one MagickImage.Colorspace call)
   - **Impact**: Useful for document scans, b&w photos; reduces file size significantly.

3. **PNG compression level**: Slider for 1–9 (currently hardcoded to default).
   - **Effort**: 2 hours (expose via JobRequest, apply via MagickImage.CompressionLevel)
   - **Impact**: PNG is lossless; giving users control over speed vs. size tradeoff is a quick win.

### Medium Term
4. **Per-codec advanced panels**: Expandable sections for JPEG-specific (progressive, optimize Huffman, DCT method), WebP-specific (method, filter), etc.
   - **Effort**: 6–8 hours (UI layout, codec enum mapping, planner changes, codec layer)
   - **Impact**: Competitive with Squoosh; gives power users the knobs they want.

5. **Chroma subsampling control**: Toggle for JPEG/WebP (4:2:0 vs. 4:4:4).
   - **Effort**: 3–4 hours
   - **Impact**: Critical for web images; 4:2:0 saves ~10–15% more bytes.

### Long Term
6. **Quantization method selection**: Expose ImageMagick's -quality flag variants (PSNR, SSIM, etc.) if Magick.NET surface allows.
   - **Effort**: Unknown (depends on Magick.NET's API surface)
   - **Impact**: Niche; Squoosh audience expects it.

---

## Decision Checkpoint

**Question**: Should PixelPress position as a *batch optimizer* (current) or a *codec tuning studio* (Squoosh model)?

- **Current positioning** (batch focus): Simplicity is a feature. One slider, one output, done. Great for photographers who want fire-and-forget.
- **Squoosh positioning** (codec focus): Power users who want to trade off speed, compatibility, visual quality per image.

The two goals are not incompatible, but they compete for UI real estate. Adding progressive JPEG + PNG compression level keeps the UI lean while giving users two popular knobs.

---

## Files to Review

- **FormatRegistry.cs**: Add `HasProgressiveOption`, `HasCompressionLevelOption` per format.
- **JobRequest.cs**: Add `ProgressiveJpeg`, `PngCompressionLevel`, etc. properties.
- **MagickImageCodec.cs**: Apply these settings via Interlace, CompressionLevel.
- **MainWindow.axaml**: Add Inspector expandable sections.
- **MainWindowViewModel.cs**: Bind new properties.
