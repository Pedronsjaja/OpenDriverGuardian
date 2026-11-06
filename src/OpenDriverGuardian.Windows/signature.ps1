$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = [Text.UTF8Encoding]::new($false)
Import-Module "$PSHOME\Modules\Microsoft.PowerShell.Security\Microsoft.PowerShell.Security.psd1" -ErrorAction Stop
$signature = Get-AuthenticodeSignature -LiteralPath $env:ODG_SIGNATURE_FILE
$status = switch ([string]$signature.Status) {
    'Valid' { if ($signature.SignerCertificate) { 'VALID' } else { 'NOT_VERIFIED' } }
    'NotSigned' { 'NOT_VERIFIED' }
    'UnknownError' { 'NOT_VERIFIED' }
    'Incompatible' { 'NOT_SUPPORTED' }
    'NotSupportedFileFormat' { 'NOT_SUPPORTED' }
    default { 'INVALID' }
}
[ordered]@{ Status = $status; SignerThumbprint = $signature.SignerCertificate.Thumbprint } | ConvertTo-Json -Compress
