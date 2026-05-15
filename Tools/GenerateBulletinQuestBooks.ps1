param(
    [string]$QuestSqlPath = "",
    [string]$BookDirectory = ""
)

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($QuestSqlPath)) {
    $QuestSqlPath = Join-Path $PSScriptRoot "FixBulletinBoardQuests.sql"
}

if ([string]::IsNullOrWhiteSpace($BookDirectory)) {
    $sourceRoot = Split-Path $PSScriptRoot -Parent
    $workspaceRoot = Split-Path $sourceRoot -Parent
    $BookDirectory = Join-Path $workspaceRoot "Asda2 - Client\data\NPC\Episode\BOOK"
}

if (!(Test-Path -LiteralPath $QuestSqlPath)) {
    throw "Quest SQL file was not found: $QuestSqlPath"
}

if (!(Test-Path -LiteralPath $BookDirectory)) {
    throw "Quest book directory was not found: $BookDirectory"
}

$arabicEncoding = [Text.Encoding]::GetEncoding(1256)

function Add-Int32 {
    param(
        [System.Collections.Generic.List[byte]]$Bytes,
        [int]$Value
    )
    $Bytes.AddRange([BitConverter]::GetBytes($Value))
}

function Add-Byte {
    param(
        [System.Collections.Generic.List[byte]]$Bytes,
        [byte]$Value
    )
    $Bytes.Add($Value)
}

function Get-AnsiBytes {
    param(
        [string]$Text,
        [int]$MaxBytes = 0
    )

    $bytes = $arabicEncoding.GetBytes($Text)
    if ($MaxBytes -gt 0 -and $bytes.Length -gt $MaxBytes) {
        $trimmed = New-Object System.Collections.Generic.List[byte]
        foreach ($char in $Text.ToCharArray()) {
            $charBytes = $arabicEncoding.GetBytes([string]$char)
            if (($trimmed.Count + $charBytes.Length) -gt $MaxBytes) {
                break
            }
            $trimmed.AddRange($charBytes)
        }
        return $trimmed.ToArray()
    }

    return $bytes
}

function New-QuestHeader {
    param(
        [string]$QuestName
    )

    $header = New-Object byte[] 64
    $nameBytes = Get-AnsiBytes $QuestName 48
    [Array]::Copy($nameBytes, 0, $header, 0, $nameBytes.Length)

    $englishOffset = [Math]::Min($nameBytes.Length + 1, 56)
    $englishBytes = Get-AnsiBytes "Bulletin" (63 - $englishOffset)
    if ($englishBytes.Length -gt 0) {
        [Array]::Copy($englishBytes, 0, $header, $englishOffset, $englishBytes.Length)
    }

    return $header
}

function New-QuestBookBytes {
    param(
        [int]$QuestId,
        [string]$QuestName,
        [int[]]$RequiredAmounts
    )

    $requiredObjectives = @($RequiredAmounts | Where-Object { $_ -gt 0 })
    $totalRequired = 0
    foreach ($amount in $requiredObjectives) {
        $totalRequired += $amount
    }

    if ($requiredObjectives.Count -gt 1) {
        $objectiveText = "Complete the bulletin quest objectives for $QuestName."
    }
    elseif ($totalRequired -gt 0) {
        $objectiveText = "Collect $totalRequired objective item(s) for $QuestName."
    }
    else {
        $objectiveText = "Complete the bulletin quest $QuestName."
    }

    $completeText = ".Bulletin quest $QuestName is complete. Return to the quest board."
    $objectiveBytes = [byte[]](Get-AnsiBytes $objectiveText)
    $completeBytes = [byte[]](Get-AnsiBytes $completeText)

    $bytes = New-Object System.Collections.Generic.List[byte]
    Add-Int32 $bytes 220
    Add-Int32 $bytes $QuestId
    $headerBytes = [byte[]](New-QuestHeader $QuestName)
    $bytes.AddRange($headerBytes)
    Add-Int32 $bytes 2
    Add-Int32 $bytes ([Math]::Max(0, $objectiveBytes.Length - 1))
    Add-Int32 $bytes 0
    $bytes.AddRange($objectiveBytes)
    Add-Byte $bytes 0
    Add-Byte $bytes 0
    Add-Byte $bytes 0
    Add-Int32 $bytes -1
    $bytes.AddRange($completeBytes)

    return $bytes.ToArray()
}

$created = 0
$lines = Get-Content -LiteralPath $QuestSqlPath -Encoding UTF8
foreach ($line in $lines) {
    if ($line -notmatch "INSERT\s+IGNORE\s+INTO\s+``asda2bbquestrecord``\s+VALUES\s+\('(?<name>(?:''|[^'])*)',\s*(?<values>.+)\);") {
        continue
    }

    $questName = $Matches["name"].Replace("''", "'")
    $values = $Matches["values"].Split(",") | ForEach-Object { $_.Trim() }
    if ($values.Count -lt 35) {
        throw "Unexpected bulletin quest row shape: $line"
    }

    $questId = [int]$values[0]
    $requiredAmounts = @(
        [int]$values[30],
        [int]$values[31],
        [int]$values[32],
        [int]$values[33],
        [int]$values[34]
    )

    $outputPath = Join-Path $BookDirectory ("{0}QST.QST" -f $questId)
    [IO.File]::WriteAllBytes($outputPath, (New-QuestBookBytes $questId $questName $requiredAmounts))
    $created++
}

Write-Output ("Generated {0} bulletin quest book files in {1}" -f $created, $BookDirectory)
