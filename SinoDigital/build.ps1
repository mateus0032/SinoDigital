# Compila o Sino Digital em um único SinoDigital.exe (C# / .NET Framework 4.8, nativo do Windows 10/11)
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$dist = Join-Path $root 'dist'
New-Item -ItemType Directory -Force $dist | Out-Null

# --- ícone (sino âmbar sobre fundo escuro) ---
$ico = Join-Path $root 'sino.ico'
if (-not (Test-Path $ico)) {
    Add-Type -AssemblyName System.Drawing
    $bmp = New-Object System.Drawing.Bitmap 256, 256
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'; $g.TextRenderingHint = 'AntiAlias'
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $path.AddArc(0, 0, 60, 60, 180, 90); $path.AddArc(196, 0, 60, 60, 270, 90)
    $path.AddArc(196, 196, 60, 60, 0, 90); $path.AddArc(0, 196, 60, 60, 90, 90); $path.CloseFigure()
    $g.FillPath((New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 27, 34, 49))), $path)
    $f = New-Object System.Drawing.Font 'Segoe UI Emoji', 120, ([System.Drawing.FontStyle]::Regular), ([System.Drawing.GraphicsUnit]::Pixel)
    $sf = New-Object System.Drawing.StringFormat; $sf.Alignment = 'Center'; $sf.LineAlignment = 'Center'
    $g.DrawString([string]::new([char[]]@(0xD83D, 0xDD14)), $f, (New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 255, 176, 32))), (New-Object System.Drawing.RectangleF 0, 10, 256, 256), $sf)
    $g.Dispose()
    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $png = $ms.ToArray()
    $fs = [System.IO.File]::Create($ico)
    $bw = New-Object System.IO.BinaryWriter $fs
    $bw.Write([uint16]0); $bw.Write([uint16]1); $bw.Write([uint16]1)
    $bw.Write([byte]0); $bw.Write([byte]0); $bw.Write([byte]0); $bw.Write([byte]0)
    $bw.Write([uint16]1); $bw.Write([uint16]32); $bw.Write([uint32]$png.Length); $bw.Write([uint32]22)
    $bw.Write($png); $bw.Close(); $fs.Close()
}

# --- compilador (Roslyn do VS Build Tools, com fallback para o csc do .NET Framework) ---
$csc = Get-ChildItem 'C:\Program Files*\Microsoft Visual Studio' -Recurse -Filter csc.exe -ErrorAction SilentlyContinue |
    Where-Object { $_.FullName -like '*Roslyn*' } | Select-Object -First 1 -ExpandProperty FullName
if (-not $csc) { $csc = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'; Write-Warning 'Roslyn nao encontrado: usando csc do .NET Framework (C# 5 - pode falhar).' }
Write-Host "Compilador: $csc"

$fw = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319'
$refs = @(
    "$fw\System.dll", "$fw\System.Core.dll", "$fw\System.Drawing.dll", "$fw\System.Windows.Forms.dll",
    "$fw\System.Web.Extensions.dll", "$fw\System.IO.Compression.dll", "$fw\System.IO.Compression.FileSystem.dll",
    "$fw\WPF\PresentationCore.dll", "$fw\WPF\WindowsBase.dll"
) | ForEach-Object { "/r:$_" }

$out = Join-Path $dist 'SinoDigital.exe'
& $csc /nologo /target:winexe /platform:anycpu /optimize+ /langversion:latest "/out:$out" "/win32icon:$ico" "/win32manifest:$(Join-Path $root 'app.manifest')" @refs (Get-ChildItem (Join-Path $root 'src') -Filter *.cs | ForEach-Object FullName)
if ($LASTEXITCODE -ne 0) { throw 'Falha na compilacao.' }
Write-Host "OK -> $out"
