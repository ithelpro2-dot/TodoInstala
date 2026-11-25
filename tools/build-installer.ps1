param([string]$BuildDirectory)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (!(Test-Path -LiteralPath $compiler)) { throw 'No se encontró el compilador .NET Framework de Windows.' }
$buildDir = if ($BuildDirectory) { $BuildDirectory } else { Join-Path $projectRoot 'installer\build' }
[System.IO.Directory]::CreateDirectory($buildDir) | Out-Null
$catalog = Join-Path $projectRoot 'public\catalog.json'
$output = Join-Path $buildDir 'CatalogoBase.exe'
& $compiler /nologo /target:winexe /platform:anycpu /optimize+ /utf8output "/out:$output" "/win32manifest:$(Join-Path $projectRoot 'installer\app.manifest')" "/resource:$catalog,catalog.json" /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Web.Extensions.dll /reference:System.IO.Compression.dll (Join-Path $projectRoot 'installer\AssemblyInfo.cs') (Join-Path $projectRoot 'installer\Core.cs') (Join-Path $projectRoot 'installer\Program.cs') (Join-Path $projectRoot 'installer\DirectInstaller.cs') (Join-Path $projectRoot 'installer\SpybotInstaller.cs') (Join-Path $projectRoot 'installer\UserAppInstaller.cs') (Join-Path $projectRoot 'installer\AgentInstaller.cs') (Join-Path $projectRoot 'installer\CdbInstaller.cs') (Join-Path $projectRoot 'installer\GooseArchive.cs') (Join-Path $projectRoot 'installer\GooseInstaller.cs') (Join-Path $projectRoot 'installer\DeepSeekInstaller.cs') (Join-Path $projectRoot 'installer\AnythingInstaller.cs') (Join-Path $projectRoot 'installer\AnythingRuntime.cs') (Join-Path $projectRoot 'installer\GooseBridge.cs') (Join-Path $projectRoot 'installer\GeminiInstaller.cs') (Join-Path $projectRoot 'installer\SpotifyInstaller.cs') (Join-Path $projectRoot 'installer\ClineInstaller.cs') (Join-Path $projectRoot 'installer\GeminiLauncher.cs') (Join-Path $projectRoot 'installer\GeminiConflict.cs') (Join-Path $projectRoot 'installer\GeminiPath.cs') (Join-Path $projectRoot 'installer\NodeArchive.cs')
if ($LASTEXITCODE -ne 0) { throw 'No se pudo compilar el instalador.' }
$metadata = @{ catalogHash = (Get-FileHash -LiteralPath $catalog -Algorithm SHA256).Hash.ToLower(); binaryHash = (Get-FileHash -LiteralPath $output -Algorithm SHA256).Hash.ToLower(); version = '0.1.0'; signed = $false }
[IO.File]::WriteAllText((Join-Path $buildDir 'build.json'), ($metadata | ConvertTo-Json), (New-Object Text.UTF8Encoding($false)))
Write-Output ("Instalador compilado: " + $output)
Write-Output 'La compilación privada está lista para la firma.'
