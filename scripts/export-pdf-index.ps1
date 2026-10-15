param(
    [Parameter(Mandatory = $true)][string]$ExtractedText,
    [Parameter(Mandatory = $true)][string]$OriginalPdf,
    [string]$OutputPath
)
$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($OutputPath)) { $OutputPath = Join-Path $PSScriptRoot '..\docs\PDF_REQUIREMENT_INDEX.csv' }
$lines = [IO.File]::ReadAllLines((Resolve-Path -LiteralPath $ExtractedText), [Text.Encoding]::UTF8)
$pdfHash = (Get-FileHash -LiteralPath $OriginalPdf -Algorithm SHA256).Hash
$page = 0
$section = ''
$rows = [Collections.Generic.List[object]]::new()
for ($i = 0; $i -lt $lines.Length; $i++) {
    $line = $lines[$i].Trim()
    if ($line -match '^=== PDF PAGE (\d+) ===$') { $page = [int]$Matches[1]; continue }
    if ($line -match '^(FR-\d+:|\d+\.\d+ |Phase \d+|\d+\. (Project overview|Technology stack|Architecture requirements|Recommended solution structure|Functional requirements|Database design|Coding standards|Security requirements|Testing strategy|API and external integration tasks|Localization and accessibility requirements|Training and implementation plan|Required trainer deliverables|Acceptance criteria|Definition of done|Final instruction))') {
        $section = $line
        continue
    }
    if ($line -notmatch '^[\u2022\u2610]\s*|^\d+\.\s+') { continue }
    $parts = [Collections.Generic.List[string]]::new()
    $parts.Add($line)
    # Only join obvious wrapped continuation lines. SourceLine always locates the original wording.
    $j = $i + 1
    while ($j -lt $lines.Length -and $lines[$j] -cmatch '^[a-z(]') {
        $parts.Add($lines[$j].Trim()); $j++
    }
    $rows.Add([pscustomobject][ordered]@{
        Id = ('PDF-{0:D3}' -f ($rows.Count + 1))
        Page = $page
        Section = $section
        SourceLine = $i + 1
        Requirement = ($parts -join ' ')
        Review = 'PDF_DETAILED_REVIEW_AR.md'
        EvidenceStatus = 'Source inventory only; consult the detailed review for implementation, tests and unresolved decisions.'
        SourcePdfSha256 = $pdfHash
    })
}
$rows | Export-Csv -LiteralPath $OutputPath -NoTypeInformation -Encoding UTF8
Write-Output ('Indexed {0} list items across {1} PDF pages. This inventory is not a compliance verdict.' -f $rows.Count, $page)
