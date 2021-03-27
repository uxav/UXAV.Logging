param([string] $Version)

if (![Regex]::Match($Version, '^\d+\.\d+\.\d+.*').Success) {
    Write-Output("Version should be formatted x.x.x");
    exit(1);
}

$r = [Regex]::Match($Version, '(\d+\.\d+\.\d+)(?:-(\w+)-(\d+))?');

$baseVersion = $r.Captures.Groups[1].Value;

$result = "v$baseVersion";

if($r.Captures.Groups[2].Success)
{
    $releaseType = $r.Captures.Groups[2].Value;
    $releaseNumber = [int]$r.Captures.Groups[3].Value;
    
    $result = "{0} {1} {2}" -f $result, $releaseType, $releaseNumber
}

Write-Output $result;

exit(0);