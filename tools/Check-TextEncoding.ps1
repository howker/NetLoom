param(
    [string]$Root = (Split-Path -Parent $PSScriptRoot),
    [switch]$ShowSuspects,
    [string]$SuspectAllowlistPath = (Join-Path $PSScriptRoot "TextIntegrity-DroppedCapital-Allowlist.tsv")
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

$suspectFingerprintCounts =
    @{}

$reviewedSuspectCounts =
    @{}

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

function Get-SuspectFingerprint
{
    param(
        [string]$RelativePath,
        [string]$LineText
    )

    $normalizedPath =
        $RelativePath.Replace(
            "\",
            "/")

    return $normalizedPath +
        "`t" +
        $LineText.Trim()
}

function Add-SuspectFingerprint
{
    param(
        [hashtable]$Counts,
        [string]$Fingerprint
    )

    if ($Counts.ContainsKey($Fingerprint))
    {
        $Counts[$Fingerprint] =
            [int]$Counts[$Fingerprint] + 1
    }
    else
    {
        $Counts[$Fingerprint] = 1
    }
}

function Format-SuspectFingerprint
{
    param(
        [string]$Fingerprint
    )

    return $Fingerprint.Replace(
        "`t",
        " :: ")
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
            $trimmedLine =
                $line.Trim()

            $suspects.Add(
                "SUSPECT_DROPPED_CAPITAL: " +
                $relative +
                ":" +
                ($i + 1) +
                ": " +
                $trimmedLine)

            $fingerprint =
                Get-SuspectFingerprint `
                    -RelativePath $relative `
                    -LineText $trimmedLine

            Add-SuspectFingerprint `
                -Counts $suspectFingerprintCounts `
                -Fingerprint $fingerprint
        }
    }
}

$allowlistLoaded =
    $false

if (-not (Test-Path -LiteralPath $SuspectAllowlistPath))
{
    $issues.Add(
        "MISSING_SUSPECT_ALLOWLIST: " +
        $SuspectAllowlistPath)
}
else
{
    $allowlistBytes =
        [IO.File]::ReadAllBytes(
            $SuspectAllowlistPath)

    try
    {
        $allowlistText =
            $utf8Strict.GetString(
                $allowlistBytes)

        if ($allowlistText.Length -gt 0 -and
            $allowlistText[0] -eq [char]0xFEFF)
        {
            $allowlistText =
                $allowlistText.Substring(1)
        }

        $allowlistLoaded =
            $true

        $allowlistLines =
            $allowlistText -split "`r?`n"

        for ($i = 0; $i -lt $allowlistLines.Length; $i++)
        {
            $allowlistLine =
                $allowlistLines[$i]

            if ([string]::IsNullOrWhiteSpace($allowlistLine))
            {
                continue
            }

            if ($allowlistLine.TrimStart().StartsWith("#"))
            {
                continue
            }

            if ($allowlistLine -notmatch `
                "^(?<Count>[1-9][0-9]*)`t(?<Path>[^`t]+)`t(?<Text>.+)$")
            {
                $issues.Add(
                    "INVALID_SUSPECT_ALLOWLIST_LINE: " +
                    ($i + 1) +
                    ": " +
                    $allowlistLine)

                continue
            }

            $reviewedCount =
                [int]$matches["Count"]

            $reviewedPath =
                $matches["Path"].Replace(
                    "\",
                    "/")

            $reviewedText =
                $matches["Text"].Trim()

            $fingerprint =
                $reviewedPath +
                "`t" +
                $reviewedText

            if ($reviewedSuspectCounts.ContainsKey($fingerprint))
            {
                $issues.Add(
                    "DUPLICATE_SUSPECT_ALLOWLIST_ENTRY: " +
                    ($i + 1) +
                    ": " +
                    (Format-SuspectFingerprint `
                        -Fingerprint $fingerprint))

                continue
            }

            $reviewedSuspectCounts[$fingerprint] =
                $reviewedCount
        }
    }
    catch
    {
        $issues.Add(
            "INVALID_SUSPECT_ALLOWLIST_UTF8: " +
            $SuspectAllowlistPath +
            ": " +
            $_.Exception.Message)
    }
}

if ($allowlistLoaded)
{
    foreach ($fingerprint in $suspectFingerprintCounts.Keys)
    {
        $currentCount =
            [int]$suspectFingerprintCounts[$fingerprint]

        if (-not $reviewedSuspectCounts.ContainsKey($fingerprint))
        {
            $issues.Add(
                "UNREVIEWED_SUSPECT_DROPPED_CAPITAL: " +
                $currentCount +
                "x " +
                (Format-SuspectFingerprint `
                    -Fingerprint $fingerprint))

            continue
        }

        $reviewedCount =
            [int]$reviewedSuspectCounts[$fingerprint]

        if ($currentCount -ne $reviewedCount)
        {
            $issues.Add(
                "SUSPECT_DROPPED_CAPITAL_COUNT_MISMATCH: current=" +
                $currentCount +
                " reviewed=" +
                $reviewedCount +
                " :: " +
                (Format-SuspectFingerprint `
                    -Fingerprint $fingerprint))
        }
    }

    foreach ($fingerprint in $reviewedSuspectCounts.Keys)
    {
        if (-not $suspectFingerprintCounts.ContainsKey($fingerprint))
        {
            $issues.Add(
                "STALE_SUSPECT_ALLOWLIST_ENTRY: reviewed=" +
                [int]$reviewedSuspectCounts[$fingerprint] +
                " :: " +
                (Format-SuspectFingerprint `
                    -Fingerprint $fingerprint))
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

$reviewedSuspectLineCount =
    0

foreach ($reviewedCount in $reviewedSuspectCounts.Values)
{
    $reviewedSuspectLineCount +=
        [int]$reviewedCount
}

Write-Host "OK: all checked text is valid UTF-8"
Write-Host "OK: no replacement characters"
Write-Host "OK: no question-mark corruption"
Write-Host "OK: all PowerShell scripts use UTF-8 BOM"
Write-Host "OK: WPF .resx <value> starts passed text-integrity policy"
Write-Host (
    "OK: suspect dropped-capital fingerprints match reviewed allowlist: " +
    $reviewedSuspectLineCount +
    " lines / " +
    $reviewedSuspectCounts.Count +
    " fingerprints")
exit 0
