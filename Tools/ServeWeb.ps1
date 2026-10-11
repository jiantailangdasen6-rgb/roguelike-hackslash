# Serves Builds/WebGL on http://localhost:8099/ for local testing (browsers refuse to run WebGL from file://).
#   powershell -File Tools/ServeWeb.ps1 [-Port 8099]
param([int]$Port = 8099)
$root = Join-Path (Split-Path -Parent $PSScriptRoot) 'Builds\WebGL'
$types = @{
    '.html' = 'text/html; charset=utf-8'; '.js' = 'application/javascript'; '.wasm' = 'application/wasm';
    '.css' = 'text/css'; '.png' = 'image/png'; '.ico' = 'image/x-icon'; '.json' = 'application/json'
}
$listener = New-Object System.Net.HttpListener
$listener.Prefixes.Add("http://localhost:$Port/")
$listener.Start()
Write-Host "Serving $root on http://localhost:$Port/"
while ($listener.IsListening) {
    $ctx = $listener.GetContext()
    $path = [Uri]::UnescapeDataString($ctx.Request.Url.AbsolutePath.TrimStart('/'))
    if ($path -eq '') { $path = 'index.html' }
    $file = Join-Path $root $path
    if (Test-Path $file -PathType Leaf) {
        $ext = [IO.Path]::GetExtension($file).ToLower()
        $ctx.Response.ContentType = if ($types.ContainsKey($ext)) { $types[$ext] } else { 'application/octet-stream' }
        $bytes = [IO.File]::ReadAllBytes($file)
        $ctx.Response.ContentLength64 = $bytes.Length
        $ctx.Response.OutputStream.Write($bytes, 0, $bytes.Length)
    } else {
        $ctx.Response.StatusCode = 404
    }
    $ctx.Response.Close()
}
