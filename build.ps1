$ErrorActionPreference = 'Stop'
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $scriptDir

Add-Type -AssemblyName System.Drawing

function New-WinForgeIcon {
    param([string]$OutPath)

    $sizes = @(16, 32, 48, 256)
    $pngList = New-Object System.Collections.Generic.List[byte[]]

    foreach ($sz in $sizes) {
        $bmp = New-Object System.Drawing.Bitmap($sz, $sz, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
        $g = [System.Drawing.Graphics]::FromImage($bmp)
        $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
        $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
        $g.Clear([System.Drawing.Color]::Transparent)

        $pad = [float]($sz * 0.06)
        $rect = New-Object System.Drawing.RectangleF($pad, $pad, ($sz - $pad * 2), ($sz - $pad * 2))
        $rad = [float]($sz * 0.22)
        $d = $rad * 2.0

        $path = New-Object System.Drawing.Drawing2D.GraphicsPath
        $path.AddArc($rect.X, $rect.Y, $d, $d, 180, 90)
        $path.AddArc($rect.Right - $d, $rect.Y, $d, $d, 270, 90)
        $path.AddArc($rect.Right - $d, $rect.Bottom - $d, $d, $d, 0, 90)
        $path.AddArc($rect.X, $rect.Bottom - $d, $d, $d, 90, 90)
        $path.CloseFigure()

        $bgBrush = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
            $rect,
            [System.Drawing.Color]::FromArgb(255, 19, 30, 44),
            [System.Drawing.Color]::FromArgb(255, 10, 15, 22),
            45.0
        )
        $g.FillPath($bgBrush, $path)

        $borderPen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(255, 44, 68, 92), [Math]::Max(1.0, $sz * 0.035))
        $g.DrawPath($borderPen, $path)

        # Cyan Hexagon
        $hx = [float]($sz * 0.21)
        $hy = [float]($sz * 0.17)
        $hw = [float]($sz * 0.58)
        $hh = [float]($sz * 0.66)

        $hexPts = [System.Drawing.PointF[]]@(
            (New-Object System.Drawing.PointF(($hx + $hw * 0.50), $hy)),
            (New-Object System.Drawing.PointF(($hx + $hw), ($hy + $hh * 0.25))),
            (New-Object System.Drawing.PointF(($hx + $hw), ($hy + $hh * 0.75))),
            (New-Object System.Drawing.PointF(($hx + $hw * 0.50), ($hy + $hh))),
            (New-Object System.Drawing.PointF($hx, ($hy + $hh * 0.75))),
            (New-Object System.Drawing.PointF($hx, ($hy + $hh * 0.25)))
        )

        $hexFill = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(52, 56, 189, 248))
        $g.FillPolygon($hexFill, $hexPts)

        $hexPen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(255, 56, 189, 248), [Math]::Max(1.4, $sz * 0.064))
        $hexPen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
        $g.DrawPolygon($hexPen, $hexPts)

        # Emerald Power Bolt inside hexagon
        $boltPts = [System.Drawing.PointF[]]@(
            (New-Object System.Drawing.PointF(($hx + $hw * 0.56), ($hy + $hh * 0.18))),
            (New-Object System.Drawing.PointF(($hx + $hw * 0.24), ($hy + $hh * 0.54))),
            (New-Object System.Drawing.PointF(($hx + $hw * 0.50), ($hy + $hh * 0.54))),
            (New-Object System.Drawing.PointF(($hx + $hw * 0.42), ($hy + $hh * 0.84))),
            (New-Object System.Drawing.PointF(($hx + $hw * 0.76), ($hy + $hh * 0.46))),
            (New-Object System.Drawing.PointF(($hx + $hw * 0.50), ($hy + $hh * 0.46)))
        )
        $boltBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 52, 211, 153))
        $g.FillPolygon($boltBrush, $boltPts)

        $ms = New-Object System.IO.MemoryStream
        $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
        $pngList.Add($ms.ToArray())

        $ms.Dispose()
        $boltBrush.Dispose()
        $hexPen.Dispose()
        $hexFill.Dispose()
        $borderPen.Dispose()
        $bgBrush.Dispose()
        $path.Dispose()
        $g.Dispose()
        $bmp.Dispose()
    }

    $fs = [System.IO.File]::Create($OutPath)
    $bw = New-Object System.IO.BinaryWriter($fs)
    try {
        $bw.Write([uint16]0)
        $bw.Write([uint16]1)
        $bw.Write([uint16]$sizes.Count)

        $offset = 6 + (16 * $sizes.Count)
        for ($i = 0; $i -lt $sizes.Count; $i++) {
            $sz = $sizes[$i]
            $bytes = $pngList[$i]
            $dim = if ($sz -ge 256) { [byte]0 } else { [byte]$sz }
            $bw.Write($dim); $bw.Write($dim); $bw.Write([byte]0); $bw.Write([byte]0)
            $bw.Write([uint16]1); $bw.Write([uint16]32)
            $bw.Write([uint32]$bytes.Length); $bw.Write([uint32]$offset)
            $offset += $bytes.Length
        }
        foreach ($bytes in $pngList) { $bw.Write($bytes) }
    }
    finally {
        $bw.Dispose(); $fs.Dispose()
    }
}

Write-Host "Generating app.ico..."
New-WinForgeIcon -OutPath (Join-Path $scriptDir "app.ico")

$csc = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if (-not (Test-Path $csc)) { throw "csc.exe not found: $csc" }

Write-Host "Compiling WinForge.exe..."
& $csc `
    /nologo `
    /target:winexe `
    /optimize+ `
    /platform:anycpu `
    /codepage:65001 `
    /utf8output `
    /win32icon:"$scriptDir\app.ico" `
    /win32manifest:"$scriptDir\app.manifest" `
    /reference:System.dll `
    /reference:System.Drawing.dll `
    /reference:System.Windows.Forms.dll `
    /out:"$scriptDir\WinForge.exe" `
    "$scriptDir\Localization.cs" `
    "$scriptDir\TweaksEngine.cs" `
    "$scriptDir\WingetApps.cs" `
    "$scriptDir\UpdatesAndRepair.cs" `
    "$scriptDir\Program.cs"

if ($LASTEXITCODE -ne 0) { throw "Compilation failed (ExitCode=$LASTEXITCODE)" }

$exeInfo = Get-Item (Join-Path $scriptDir "WinForge.exe")
Write-Host ("Successfully built: {0} ({1:N1} KB)" -f $exeInfo.FullName, ($exeInfo.Length / 1024.0))
