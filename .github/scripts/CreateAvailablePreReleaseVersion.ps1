param([string] $Version, [string] $PreReleaseName = "beta")

if (![Regex]::Match($Version, '^\d+\.\d+\.\d+$').Success) {
    Write-Output("Version should be formatted x.x.x");
    exit(1);
}

$result = "$Version-$PreReleaseName-01";
$tagData = git show-ref --tags
$existingTags = @();
ForEach ($line in $( $tagData -split "`r`n" ))
{
    $tag = [Regex]::Match($line, 'refs/tags/v?(.*)$').Captures.Groups[1].Value;
    $existingTags += $tag;
}

$count = 1;
while($existingTags.Contains($result))
{
    $versionElement = [Regex]::Match($result, '(\d+\.\d+\.\d+-' + $PreReleaseName + ')').Captures.Groups[1].Value;
    $indexString = "-{0:d2}" -f $count
    $result = $versionElement + $indexString;
    $count ++;
}

Write-Output $result;

exit(0)
