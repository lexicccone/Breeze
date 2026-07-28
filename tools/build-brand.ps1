# Rebuilds the branding rasters from the vector master. Run from the repository root after
# replacing Breeze\Assets\Brand\logo.svg:
#
#   powershell -ExecutionPolicy Bypass -File tools\build-brand.ps1
#
# Produces logo.png, the bitmap the native views draw, and breeze.ico, the Windows icon. Headless
# Edge does the vector rendering, so no extra tooling is needed.
param(
    [string] $Master = "Breeze\Assets\Brand\logo.svg"
)

Add-Type -AssemblyName System.Drawing

$master = $Master
$brand = "Breeze\Assets\Brand"
$edge = "C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe"
$work = Join-Path $env:TEMP "breezebrand"

Remove-Item $work -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Path $work | Out-Null

$svg = Get-Content $master -Raw

# Small icon variant: the three 3.5 unit dots land on well under a pixel below 32 px, so they go,
# and the remaining shapes get a stroke in the brand blue so they survive the downscale. Measured
# against the plain artwork at 16 px this lifts the share of fully opaque pixels from a third to
# about a half, which is what makes a shape read at that size.
$small = $svg -replace '<circle[\s\S]*?/>', ''
$small = $small -replace 'stroke-width:0\.264999;stroke-dasharray:none', 'stroke:#1aaeff;stroke-width:3.5;stroke-linejoin:round;stroke-linecap:round;stroke-dasharray:none;paint-order:stroke fill'

Set-Content -Path "$work\full.svg" -Value $svg -Encoding UTF8
Set-Content -Path "$work\small.svg" -Value $small -Encoding UTF8

@'
<!DOCTYPE html><html><head><style>html,body{margin:0;padding:0;background:transparent}
img{display:block;width:100vw;height:100vh}</style></head><body><img src="NAME"></body></html>
'@ -replace 'NAME', 'full.svg' | Set-Content "$work\full.html" -Encoding UTF8

@'
<!DOCTYPE html><html><head><style>html,body{margin:0;padding:0;background:transparent}
img{display:block;width:100vw;height:100vh}</style></head><body><img src="NAME"></body></html>
'@ -replace 'NAME', 'small.svg' | Set-Content "$work\small.html" -Encoding UTF8

function Render($page, $width, $height, $out) {
    $url = "file:///" + (($work -replace '\\', '/') + "/$page")
    & $edge --headless=new --disable-gpu --hide-scrollbars --force-device-scale-factor=1 `
        --default-background-color=00000000 --window-size=$width,$height --screenshot="$out" $url 2>&1 | Out-Null
    if (-not (Test-Path $out)) { throw "render failed: $out" }
    return [System.Drawing.Bitmap]::new($out)
}

function Crop($bitmap) {
    $rect = [System.Drawing.Rectangle]::new(0, 0, $bitmap.Width, $bitmap.Height)
    $data = $bitmap.LockBits($rect, "ReadOnly", "Format32bppArgb")
    $bytes = [byte[]]::new($data.Stride * $bitmap.Height)
    [System.Runtime.InteropServices.Marshal]::Copy($data.Scan0, $bytes, 0, $bytes.Length)
    $bitmap.UnlockBits($data)

    $minX = $bitmap.Width; $maxX = -1; $minY = $bitmap.Height; $maxY = -1

    for ($y = 0; $y -lt $bitmap.Height; $y++) {
        $row = $y * $data.Stride
        for ($x = 0; $x -lt $bitmap.Width; $x++) {
            if ($bytes[$row + ($x * 4) + 3] -gt 8) {
                if ($x -lt $minX) { $minX = $x }
                if ($x -gt $maxX) { $maxX = $x }
                if ($y -lt $minY) { $minY = $y }
                if ($y -gt $maxY) { $maxY = $y }
            }
        }
    }

    $box = [System.Drawing.Rectangle]::new($minX, $minY, $maxX - $minX + 1, $maxY - $minY + 1)
    $cropped = [System.Drawing.Bitmap]::new($box.Width, $box.Height, "Format32bppArgb")
    $graphics = [System.Drawing.Graphics]::FromImage($cropped)
    $graphics.DrawImage($bitmap, [System.Drawing.Rectangle]::new(0, 0, $box.Width, $box.Height), $box, "Pixel")
    $graphics.Dispose()
    return $cropped
}

function Fit($source, $size, $margin) {
    $canvas = [System.Drawing.Bitmap]::new($size, $size, "Format32bppArgb")
    $graphics = [System.Drawing.Graphics]::FromImage($canvas)
    $graphics.CompositingQuality = "HighQuality"
    $graphics.InterpolationMode = "HighQualityBicubic"
    $graphics.SmoothingMode = "HighQuality"
    $graphics.PixelOffsetMode = "HighQuality"

    $box = $size * (1 - (2 * $margin))
    $scale = [Math]::Min($box / $source.Width, $box / $source.Height)
    $width = [int][Math]::Round($source.Width * $scale)
    $height = [int][Math]::Round($source.Height * $scale)
    $graphics.DrawImage($source, [System.Drawing.Rectangle]::new([int](($size - $width) / 2), [int](($size - $height) / 2), $width, $height))
    $graphics.Dispose()
    return $canvas
}

# Strengthens the faint edges a downscale leaves behind, so a 16 px icon reads as a shape
# rather than a grey smudge.
function Sharpen($bitmap, $gamma) {
    $rect = [System.Drawing.Rectangle]::new(0, 0, $bitmap.Width, $bitmap.Height)
    $data = $bitmap.LockBits($rect, "ReadWrite", "Format32bppArgb")
    $bytes = [byte[]]::new($data.Stride * $bitmap.Height)
    [System.Runtime.InteropServices.Marshal]::Copy($data.Scan0, $bytes, 0, $bytes.Length)

    for ($i = 3; $i -lt $bytes.Length; $i += 4) {
        $alpha = $bytes[$i]
        if ($alpha -gt 0 -and $alpha -lt 255) {
            $bytes[$i] = [byte][Math]::Min(255, [Math]::Round(255 * [Math]::Pow($alpha / 255, $gamma)))
        }
    }

    [System.Runtime.InteropServices.Marshal]::Copy($bytes, 0, $data.Scan0, $bytes.Length)
    $bitmap.UnlockBits($data)
}

function Png($bitmap) {
    $memory = [System.IO.MemoryStream]::new()
    $bitmap.Save($memory, [System.Drawing.Imaging.ImageFormat]::Png)
    $bytes = $memory.ToArray()
    $memory.Dispose()
    return , $bytes
}

# Icon frames below 256 px are stored as an uncompressed 32 bit DIB: LoadImage and the shell read
# PNG compressed frames of that size unreliably, and a frame Windows cannot parse is a frame it
# silently replaces with a rescaled one.
function Dib($bitmap) {
    $width = $bitmap.Width
    $height = $bitmap.Height
    $maskStride = [int](([Math]::Ceiling($width / 8) + 3) / 4) * 4
    $pixels = $width * $height * 4
    $mask = $maskStride * $height

    $stream = [System.IO.MemoryStream]::new()
    $writer = [System.IO.BinaryWriter]::new($stream)

    $writer.Write([uint32] 40)
    $writer.Write([int32] $width)
    $writer.Write([int32] ($height * 2))   # colour rows plus mask rows
    $writer.Write([uint16] 1)
    $writer.Write([uint16] 32)
    $writer.Write([uint32] 0)              # BI_RGB
    $writer.Write([uint32] ($pixels + $mask))
    $writer.Write([int32] 0)
    $writer.Write([int32] 0)
    $writer.Write([uint32] 0)
    $writer.Write([uint32] 0)

    $rect = [System.Drawing.Rectangle]::new(0, 0, $width, $height)
    $data = $bitmap.LockBits($rect, "ReadOnly", "Format32bppArgb")
    $bytes = [byte[]]::new($data.Stride * $height)
    [System.Runtime.InteropServices.Marshal]::Copy($data.Scan0, $bytes, 0, $bytes.Length)
    $bitmap.UnlockBits($data)

    for ($y = $height - 1; $y -ge 0; $y--) {
        $writer.Write($bytes, $y * $data.Stride, $width * 4)
    }

    $writer.Write([byte[]]::new($mask))
    $writer.Flush()
    $result = $stream.ToArray()
    $writer.Dispose()
    return , $result
}

# Faithful artwork, high resolution, then trimmed to the ink.
$rendered = Render "full.html" 1400 1120 "$work\full.png"
$artwork = Crop $rendered
$rendered.Dispose()
"artwork: $($artwork.Width)x$($artwork.Height)"

# The About page bitmap keeps the framing of the vector master, plus a transparent margin. Without
# it the ink sits flush against the edges, and scaling 862 px down to about 52 px for display
# samples those outermost rows against nothing, which shaves the strokes there and reads as a
# clipped logo.
$pad = 31
$inner = 1024 - (2 * $pad)
$logo = [System.Drawing.Bitmap]::new(1024, [int][Math]::Round($inner * $artwork.Height / $artwork.Width) + (2 * $pad), "Format32bppArgb")
$graphics = [System.Drawing.Graphics]::FromImage($logo)
$graphics.CompositingQuality = "HighQuality"
$graphics.InterpolationMode = "HighQualityBicubic"
$graphics.SmoothingMode = "HighQuality"
$graphics.PixelOffsetMode = "HighQuality"
$graphics.DrawImage($artwork, [System.Drawing.Rectangle]::new($pad, $pad, $inner, $logo.Height - (2 * $pad)))
$graphics.Dispose()
[System.IO.File]::WriteAllBytes((Join-Path (Get-Location) "$brand\logo.png"), (Png $logo))
"logo.png: $($logo.Width)x$($logo.Height)"
$logo.Dispose()

# Installer wizard artwork. Inno Setup wants bitmaps and does not scale them, so every size it asks
# for is drawn from the vector render rather than resampled from one image.
function Wizard($width, $height, $margin, $out) {
    $canvas = [System.Drawing.Bitmap]::new($width, $height, "Format24bppRgb")
    $graphics = [System.Drawing.Graphics]::FromImage($canvas)
    $graphics.CompositingQuality = "HighQuality"
    $graphics.InterpolationMode = "HighQualityBicubic"
    $graphics.SmoothingMode = "HighQuality"
    $graphics.PixelOffsetMode = "HighQuality"

    # Breeze's dark chrome colour, so the pages read as the browser rather than a default wizard.
    $graphics.Clear([System.Drawing.Color]::FromArgb(0x17, 0x17, 0x1A))

    $box = [Math]::Min($width, $height) * (1 - (2 * $margin))
    $scale = [Math]::Min($box / $artwork.Width, $box / $artwork.Height)
    $drawWidth = [int][Math]::Round($artwork.Width * $scale)
    $drawHeight = [int][Math]::Round($artwork.Height * $scale)
    $graphics.DrawImage($artwork, [System.Drawing.Rectangle]::new([int](($width - $drawWidth) / 2), [int](($height - $drawHeight) / 2), $drawWidth, $drawHeight))
    $graphics.Dispose()

    $canvas.Save((Join-Path (Get-Location) $out), [System.Drawing.Imaging.ImageFormat]::Bmp)
    $canvas.Dispose()
    "$out : $width x $height"
}

$wizard = "installer\assets"
New-Item -ItemType Directory -Force -Path $wizard | Out-Null

# The sizes Inno Setup loads at 100%, 125%, 150% and 200% display scaling.
Wizard 164 314 0.10 "$wizard\wizard-large.bmp"
Wizard 192 386 0.10 "$wizard\wizard-large@125.bmp"
Wizard 246 459 0.10 "$wizard\wizard-large@150.bmp"
Wizard 328 604 0.10 "$wizard\wizard-large@200.bmp"
Wizard 55 55 0.06 "$wizard\wizard-small.bmp"
Wizard 64 68 0.06 "$wizard\wizard-small@125.bmp"
Wizard 83 80 0.06 "$wizard\wizard-small@150.bmp"
Wizard 110 106 0.06 "$wizard\wizard-small@200.bmp"

# Simplified artwork for the sizes where fine detail cannot survive.
$renderedSmall = Render "small.html" 1400 1120 "$work\small.png"
$simple = Crop $renderedSmall
$renderedSmall.Dispose()
"simplified: $($simple.Width)x$($simple.Height)"

$frames = @()

foreach ($size in 16, 20, 24, 32, 40, 48, 64, 128, 256) {
    $simplified = $size -le 24
    $source = if ($simplified) { $simple } else { $artwork }
    $bitmap = Fit $source $size $(if ($simplified) { 0.02 } else { 0.04 })

    if ($simplified) {
        Sharpen $bitmap 0.7
    }

    $data = if ($size -eq 256) { Png $bitmap } else { Dib $bitmap }
    $frames += , @{ Size = $size; Data = $data; Simplified = $simplified }
    $bitmap.Dispose()
}

$artwork.Dispose()
$simple.Dispose()

$stream = [System.IO.File]::Create((Join-Path (Get-Location) "$brand\breeze.ico"))
$writer = [System.IO.BinaryWriter]::new($stream)

$writer.Write([uint16] 0)
$writer.Write([uint16] 1)
$writer.Write([uint16] $frames.Count)

$offset = 6 + (16 * $frames.Count)

foreach ($frame in $frames) {
    $writer.Write([byte] ($frame.Size % 256))
    $writer.Write([byte] ($frame.Size % 256))
    $writer.Write([byte] 0)
    $writer.Write([byte] 0)
    $writer.Write([uint16] 1)
    $writer.Write([uint16] 32)
    $writer.Write([uint32] $frame.Data.Length)
    $writer.Write([uint32] $offset)
    $offset += $frame.Data.Length
}

foreach ($frame in $frames) { $writer.Write([byte[]] $frame.Data) }

$writer.Flush(); $writer.Dispose(); $stream.Dispose()

"breeze.ico: $((Get-Item "$brand\breeze.ico").Length) bytes"
$frames | ForEach-Object { "  $($_.Size) px $(if ($_.Simplified) { 'simplified' } else { 'faithful  ' }) $(if ($_.Size -eq 256) { 'png' } else { 'dib' }) $($_.Data.Length) bytes" }
