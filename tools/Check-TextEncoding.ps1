param(
    [string]$Root = (Split-Path -Parent $PSScriptRoot),
    [switch]$ShowSuspects
)

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

$suspects =
    New-Object System.Collections.Generic.List[string]

$rootPath =
    [IO.Path]::GetFullPath($Root)

$files =
    Get-ChildItem `
        -LiteralPath $rootPath `
        -Recurse `
        -File |
    Where-Object {
        $_.FullName -notmatch "\\(\.git|\.vs|bin|obj|packages)\\" -and
        $extensions -contains $_.Extension.ToLowerInvariant()
    }

$lowerCyr =
    "[\u0430-\u044f\u0451]"

$rxHeading =
    [regex](
        "^\s*#{1,6}\s+(" +
        $lowerCyr +
        ")")

$rxComment =
    [regex](
        "^\s*//\s*(" +
        $lowerCyr +
        ")")

$rxListSentence =
    [regex](
        "^\s*(?:[-*+]\s+(?:\[[ xX]\]\s*)?|\d+[.)]\s+)" +
        "(" +
        $lowerCyr +
        ")" +
        ".*[.:]\s*$")

$rxParagraph =
    [regex](
        "^\s*(" +
        $lowerCyr +
        ")" +
        ".*[.:]\s*$")

$allowedWpfResxLowercaseNames =
    @(
        "EvidenceCount"
    )

function Get-RepositoryRelativePath
{
    param(
        [string]$FullName
    )

    return $FullName.Substring(
        $rootPath.Length).
        TrimStart(
            [IO.Path]::DirectorySeparatorChar,
            [IO.Path]::AltDirectorySeparatorChar)
}

function Test-LowercaseCyrillicStart
{
    param(
        [string]$Value
    )

    if ([string]::IsNullOrWhiteSpace($Value))
    {
        return $false
    }

    return [regex]::IsMatch(
        $Value,
        "^\s*" + $lowerCyr)
}

foreach ($file in $files)
{
    $bytes =
        [IO.File]::ReadAllBytes(
            $file.FullName)

    try
    {
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

    if ($text.Contains([char]0xFFFD))
    {
        $issues.Add(
            "REPLACEMENT_CHARACTER: " +
            $file.FullName)
    }

    if ($text -match "\?{4,}")
    {
        $issues.Add(
            "QUESTION_MARK_RUN: " +
            $file.FullName)
    }

    if ($file.Extension.ToLowerInvariant() -eq ".ps1")
    {
        $hasBom =
            $bytes.Length -ge 3 -and
            $bytes[0] -eq 0xEF -and
            $bytes[1] -eq 0xBB -and
            $bytes[2] -eq 0xBF

        if (-not $hasBom)
        {
            $issues.Add(
                "PS1_WITHOUT_UTF8_BOM: " +
                $file.FullName)
        }
    }

    $relative =
        Get-RepositoryRelativePath `
            -FullName $file.FullName

    $relativeNormalized =
        $relative.Replace("\", "/")

    $isWpfResx =
        $file.Extension.ToLowerInvariant() -eq ".resx" -and
        $relativeNormalized.StartsWith(
            "src/NetLoom.Wpf/Resources/",
            [StringComparison]::OrdinalIgnoreCase)

    if ($isWpfResx)
    {
        try
        {
            $xml =
                New-Object System.Xml.XmlDocument

            $xml.PreserveWhitespace = $true
            $xml.LoadXml($text)

            $dataNodes =
                $xml.SelectNodes("/root/data")

            foreach ($dataNode in $dataNodes)
            {
                $name =
                    [string]$dataNode.GetAttribute("name")

                $valueNode =
                    $dataNode.SelectSingleNode("value")

                if ($null -eq $valueNode)
                {
                    continue
                }

                $value =
                    [string]$valueNode.InnerText

                if ($allowedWpfResxLowercaseNames -contains $name)
                {
                    continue
                }

                if (Test-LowercaseCyrillicStart -Value $value)
                {
                    $issues.Add(
                        "RESX_DROPPED_CAPITAL: " +
                        $relative +
                        ":" +
                        $name +
                        ": " +
                        $value.Trim())
                }
            }
        }
        catch
        {
            $issues.Add(
                "INVALID_RESX_XML: " +
                $relative +
                ": " +
                $_.Exception.Message)
        }
    }

    $lines =
        $text -split "`r?`n"

    for ($i = 0; $i -lt $lines.Length; $i++)
    {
        $line =
            $lines[$i]

        $previousBlank =
            $i -eq 0 -or
            [string]::IsNullOrWhiteSpace(
                $lines[$i - 1])

        $isSuspect =
            $rxHeading.IsMatch($line) -or
            $rxComment.IsMatch($line) -or
            $rxListSentence.IsMatch($line) -or
            ($previousBlank -and
             $rxParagraph.IsMatch($line))

        if ($isSuspect)
        {
            $suspects.Add(
                "SUSPECT_DROPPED_CAPITAL: " +
                $relative +
                ":" +
                ($i + 1) +
                ": " +
                $line.Trim())
        }
    }
}

if ($ShowSuspects -and $suspects.Count -gt 0)
{
    $suspects |
        Sort-Object -Unique |
        ForEach-Object {
            Write-Host $_
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
Write-Host "OK: no question-mark corruption"
Write-Host "OK: all PowerShell scripts use UTF-8 BOM"
Write-Host "OK: WPF .resx <value> starts passed text-integrity policy"
Write-Host ("INFO: suspect dropped-capital lines: " + $suspects.Count)
exit 0
