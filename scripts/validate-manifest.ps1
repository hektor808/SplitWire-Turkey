[CmdletBinding()]
param(
    [Parameter(Mandatory = $false)]
    [string]$ManifestPath = "src/SplitWireTurkey/Services/DownloadSecurityManifest.cs",

    [Parameter(Mandatory = $false)]
    [string]$WgcfVersion,

    [Parameter(Mandatory = $false)]
    [string]$WgcfPath,

    [Parameter(Mandatory = $false)]
    [string]$DiscordStableVersion,

    [Parameter(Mandatory = $false)]
    [string]$DiscordStablePath,

    [Parameter(Mandatory = $false)]
    [string]$DiscordPtbVersion,

    [Parameter(Mandatory = $false)]
    [string]$DiscordPtbPath
)

$ErrorActionPreference = 'Stop'

if (-not (Test-Path -LiteralPath $ManifestPath)) {
    throw "Manifest dosyası bulunamadı: $ManifestPath"
}

$manifestContent = Get-Content -LiteralPath $ManifestPath -Raw

function Get-ComputedSha256([string]$FilePath) {
    if ([string]::IsNullOrWhiteSpace($FilePath)) {
        return $null
    }

    if (-not (Test-Path -LiteralPath $FilePath)) {
        throw "Hash hesaplanacak dosya bulunamadı: $FilePath"
    }

    return (Get-FileHash -LiteralPath $FilePath -Algorithm SHA256).Hash.ToUpperInvariant()
}

function Assert-ManifestContainsHash([string]$ArtifactKey, [string]$Version, [string]$Hash) {
    if ([string]::IsNullOrWhiteSpace($Version) -or [string]::IsNullOrWhiteSpace($Hash)) {
        Write-Host "[SKIP] $ArtifactKey için sürüm/hash sağlanmadı."
        return
    }

    $escapedVersion = [regex]::Escape($Version)
    $escapedHash = [regex]::Escape($Hash)
    $escapedArtifactKey = [regex]::Escape($ArtifactKey)

    $entryPattern = '\["' + $escapedVersion + '"\]\s*=\s*"' + $escapedHash + '"'
    $artifactSectionPattern =
        '\["' + $escapedArtifactKey + '"\].*?' +
        'sha256ByVersion:\s*new Dictionary<string, string>\(StringComparer\.OrdinalIgnoreCase\)\s*\{' +
        '(?<entries>.*?)' +
        '\}\)'

    $artifactMatch = [regex]::Match($manifestContent, $artifactSectionPattern, [System.Text.RegularExpressions.RegexOptions]::Singleline)
    if (-not $artifactMatch.Success) {
        throw "Manifest içinde artifact bölümü bulunamadı: $ArtifactKey"
    }

    $entries = $artifactMatch.Groups['entries'].Value
    if (-not [regex]::IsMatch($entries, $entryPattern, [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)) {
        throw "Manifest güncel değil: $ArtifactKey $Version için SHA-256 '$Hash' bulunamadı."
    }

    Write-Host "[OK] Manifest kaydı doğrulandı: $ArtifactKey $Version"
}

$wgcfHash = Get-ComputedSha256 -FilePath $WgcfPath
$stableHash = Get-ComputedSha256 -FilePath $DiscordStablePath
$ptbHash = Get-ComputedSha256 -FilePath $DiscordPtbPath

Assert-ManifestContainsHash -ArtifactKey 'wgcf' -Version $WgcfVersion -Hash $wgcfHash
Assert-ManifestContainsHash -ArtifactKey 'discord_stable' -Version $DiscordStableVersion -Hash $stableHash
Assert-ManifestContainsHash -ArtifactKey 'discord_ptb' -Version $DiscordPtbVersion -Hash $ptbHash

Write-Host "Manifest doğrulama başarıyla tamamlandı."
