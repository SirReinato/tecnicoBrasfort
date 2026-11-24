param(
    [string]$OutputPath = ""
)

$ErrorActionPreference = "Stop"

Add-Type -AssemblyName System.Drawing

$RepoRoot = Split-Path -Parent $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($OutputPath)) {
    $OutputPath = Join-Path $RepoRoot "src\TecnicoBrasfort\Assets\app.ico"
}

$OutDir = Split-Path -Parent $OutputPath
if (-not (Test-Path $OutDir)) {
    New-Item -ItemType Directory -Path $OutDir -Force | Out-Null
}

function New-ShieldIconFrameBytes {
    param([int]$Size)

    $bmp = $null
    $g = $null
    $ms = $null
    $bw = $null
    $shieldPath = $null
    $innerPath = $null
    $shieldBrush = $null
    $borderPen = $null
    $accentBrush = $null

    try {
        $bmp = New-Object System.Drawing.Bitmap($Size, $Size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
        $g = [System.Drawing.Graphics]::FromImage($bmp)
        $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
        $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
        $g.Clear([System.Drawing.Color]::Transparent)

        $s = [float]$Size
        $pad = [Math]::Max(1.0, $s * 0.08)
        $w = $s - (2.0 * $pad)
        $h = $s - (2.0 * $pad)

        # Escudo geometrico (#303841) com borda teal (#76ABAE)
        $shieldPts = @(
            (New-Object System.Drawing.PointF(($pad + $w * 0.50), $pad)),
            (New-Object System.Drawing.PointF(($pad + $w * 0.94), ($pad + $h * 0.16))),
            (New-Object System.Drawing.PointF(($pad + $w * 0.90), ($pad + $h * 0.56))),
            (New-Object System.Drawing.PointF(($pad + $w * 0.50), ($pad + $h * 0.98))),
            (New-Object System.Drawing.PointF(($pad + $w * 0.10), ($pad + $h * 0.56))),
            (New-Object System.Drawing.PointF(($pad + $w * 0.06), ($pad + $h * 0.16)))
        )

        $shieldPath = New-Object System.Drawing.Drawing2D.GraphicsPath
        $shieldPath.AddPolygon($shieldPts)

        $colorSlate = [System.Drawing.Color]::FromArgb(255, 48, 56, 65)      # #303841
        $colorTeal = [System.Drawing.Color]::FromArgb(255, 118, 171, 174)   # #76ABAE
        $colorOrange = [System.Drawing.Color]::FromArgb(255, 255, 87, 34)   # #FF5722

        $shieldBrush = New-Object System.Drawing.SolidBrush($colorSlate)
        $g.FillPath($shieldBrush, $shieldPath)

        $strokeW = [Math]::Max(1.5, $s * 0.075)
        $borderPen = New-Object System.Drawing.Pen($colorTeal, $strokeW)
        $borderPen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
        $g.DrawPath($borderPen, $shieldPath)

        # Simbolo tecnico central (raio/chevron #FF5722 de alto contraste)
        $boltPts = @(
            (New-Object System.Drawing.PointF(($pad + $w * 0.56), ($pad + $h * 0.22))),
            (New-Object System.Drawing.PointF(($pad + $w * 0.30), ($pad + $h * 0.53))),
            (New-Object System.Drawing.PointF(($pad + $w * 0.48), ($pad + $h * 0.53))),
            (New-Object System.Drawing.PointF(($pad + $w * 0.42), ($pad + $h * 0.80))),
            (New-Object System.Drawing.PointF(($pad + $w * 0.70), ($pad + $h * 0.46))),
            (New-Object System.Drawing.PointF(($pad + $w * 0.52), ($pad + $h * 0.46)))
        )

        $innerPath = New-Object System.Drawing.Drawing2D.GraphicsPath
        $innerPath.AddPolygon($boltPts)

        $accentBrush = New-Object System.Drawing.SolidBrush($colorOrange)
        $g.FillPath($accentBrush, $innerPath)

        $ms = New-Object System.IO.MemoryStream
        if ($Size -ge 256) {
            $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
            return ,($ms.ToArray())
        }

        # Frames 16, 32, 48 em DIB BMP 32-bit BGRA + AND mask para compatibilidade estrita com csc.exe v4.0 /win32icon
        $bw = New-Object System.IO.BinaryWriter($ms)
        $pixelBytes = $Size * $Size * 4
        $andRowStride = [int]([Math]::Floor(($Size + 31) / 32) * 4)
        $andMaskBytes = $andRowStride * $Size

        # BITMAPINFOHEADER (40 bytes)
        $bw.Write([uint32]40)                 # biSize
        $bw.Write([int32]$Size)               # biWidth
        $bw.Write([int32]($Size * 2))         # biHeight (XOR + AND)
        $bw.Write([uint16]1)                  # biPlanes
        $bw.Write([uint16]32)                 # biBitCount
        $bw.Write([uint32]0)                  # biCompression (BI_RGB)
        $bw.Write([uint32]($pixelBytes + $andMaskBytes)) # biSizeImage
        $bw.Write([int32]0)                   # biXPelsPerMeter
        $bw.Write([int32]0)                   # biYPelsPerMeter
        $bw.Write([uint32]0)                  # biClrUsed
        $bw.Write([uint32]0)                  # biClrImportant

        # Pixels bottom-up em BGRA
        for ($y = $Size - 1; $y -ge 0; $y--) {
            for ($x = 0; $x -lt $Size; $x++) {
                $px = $bmp.GetPixel($x, $y)
                $bw.Write([byte]$px.B)
                $bw.Write([byte]$px.G)
                $bw.Write([byte]$px.R)
                $bw.Write([byte]$px.A)
            }
        }

        # AND mask (0 = usa canal alpha)
        $zeroMask = New-Object byte[] $andMaskBytes
        $bw.Write($zeroMask)
        $bw.Flush()
        return ,($ms.ToArray())
    }
    finally {
        if ($bw) { $bw.Dispose() }
        if ($accentBrush) { $accentBrush.Dispose() }
        if ($borderPen) { $borderPen.Dispose() }
        if ($shieldBrush) { $shieldBrush.Dispose() }
        if ($innerPath) { $innerPath.Dispose() }
        if ($shieldPath) { $shieldPath.Dispose() }
        if ($g) { $g.Dispose() }
        if ($bmp) { $bmp.Dispose() }
        if ($ms) { $ms.Dispose() }
    }
}

$Sizes = @(16, 32, 48, 256)
$Frames = @()
foreach ($sz in $Sizes) {
    $frameBytes = New-ShieldIconFrameBytes -Size $sz
    $Frames += [PSCustomObject]@{
        Size  = $sz
        Bytes = $frameBytes
    }
}

$fs = $null
$bw = $null
try {
    $fs = [System.IO.File]::Open($OutputPath, [System.IO.FileMode]::Create, [System.IO.FileAccess]::Write)
    $bw = New-Object System.IO.BinaryWriter($fs)

    # ICONDIR (6 bytes)
    $bw.Write([uint16]0)             # idReserved
    $bw.Write([uint16]1)             # idType (1 = ICO)
    $bw.Write([uint16]$Frames.Count) # idCount

    $offset = 6 + (16 * $Frames.Count)
    foreach ($frame in $Frames) {
        $dimByte = if ($frame.Size -ge 256) { [byte]0 } else { [byte]$frame.Size }
        $bw.Write($dimByte)                     # bWidth
        $bw.Write($dimByte)                     # bHeight
        $bw.Write([byte]0)                      # bColorCount
        $bw.Write([byte]0)                      # bReserved
        $bw.Write([uint16]1)                    # wPlanes
        $bw.Write([uint16]32)                   # wBitCount
        $bw.Write([uint32]$frame.Bytes.Length)  # dwBytesInRes
        $bw.Write([uint32]$offset)              # dwImageOffset
        $offset += $frame.Bytes.Length
    }

    foreach ($frame in $Frames) {
        $bw.Write($frame.Bytes)
    }
}
finally {
    if ($bw) { $bw.Dispose() }
    if ($fs) { $fs.Dispose() }
}

Write-Host "Icone gerado em: $OutputPath (frames: $($Sizes -join ', '))"
