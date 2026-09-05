$ErrorActionPreference = "Stop"

$utf8Strict =
    New-Object System.Text.UTF8Encoding(
        $false,
        $true)

$extensions =
    @(
        ".cs",
        ".xaml",
        ".csproj",
        ".ps1",
        ".md",
        ".json",
        ".yml",
        ".yaml",
        ".resx",
        ".config"
    )

$issues =
    New-Object System.Collections.Generic.List[string]

$files =
    Get-ChildItem `
        src,tests,tools,docs `
        -Recurse `
        -File |
    Where-Object {
        $_.FullName -notmatch "\\(bin|obj)\\" -and
        $extensions -contains $_.Extension.ToLowerInvariant()
    }

foreach ($file in $files)
{
    try
    {
        $bytes =
            [IO.File]::ReadAllBytes(
                $file.FullName)

        $text =
            $utf8Strict.GetString(
                $bytes)
    }
    catch
    {
        $issues.Add(
            "INVALID_UTF8: " +
            $file.FullName)

        continue
    }

    if ($text.Contains(
        [char]0xFFFD))
    {
        $issues.Add(
            "REPLACEMENT_CHAR: " +
            $file.FullName)
    }

    if ($text -match "\?{4,}")
    {
        $issues.Add(
            "QUESTION_MARK_RUN: " +
            $file.FullName)
    }

    if ($file.Extension -eq ".md" -and
        $text -match
            "(?im)^(апрещено|атериализация|н содержит|еализовано)\b")
    {
        $issues.Add(
            "SUSPICIOUS_DOC_TEXT: " +
            $file.FullName)
    }
}

if ($issues.Count -gt 0)
{
    $issues |
        Sort-Object -Unique |
        ForEach-Object {
            Write-Host $_
        }

    exit 1
}

Write-Host "OK: all checked text is valid UTF-8"
Write-Host "OK: no replacement characters"
Write-Host "OK: no obvious ASCII-loss markers"
