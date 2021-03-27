param([string] $Path, [string] $Version)

$file = Get-Item $Path;
Write-Output "File to open is $file"
Write-Output "Specific version is $Version"
if(!$Version)
{
    Write-Output("Version is not set, exiting!")
    exit(1);
}

$versionMatch = [Regex]::Match($Version, "^(\d+\.\d+\.\d+)(?:-([\w-]+))?$");

if (!$versionMatch.Success) {
    Write-Output("Version should be formatted x.x.x(-beta-1) etc");
    exit(1);
}

$baseVersion = $versionMatch.Captures.Groups[1].Value;
if($versionMatch.Captures.Groups[2].Success)
{
    $preReleaseTag = $versionMatch.Captures.Groups[2].Value;
}

Write-Output "baseVersion = $baseVersion"
$newAssemblyVersion = 'AssemblyVersion("' + $baseVersion + '.*")'
Write-Output "AssemblyVersion = $NewAssemblyVersion"
$newAssemblyFileVersion = 'AssemblyFileVersion("' + $baseVersion + '")'
Write-Output "AssemblyFileVersion = $newAssemblyFileVersion"
if($preReleaseTag)
{
    $newAssemblyInformationalVersion = 'AssemblyInformationalVersion("' + $baseVersion + '-' + $preReleaseTag + '")'
}
else
{
    $newAssemblyInformationalVersion = 'AssemblyInformationalVersion("' + $baseVersion + '")'
}
Write-Output "AssemblyInformationalVersion = $newAssemblyInformationalVersion"

$TmpFile = $file.FullName + ".tmp"
Get-Content $file.FullName |
        ForEach-Object {
            $_ -replace 'AssemblyVersion\(".*"\)', $newAssemblyVersion } |
        ForEach-Object {
            $_ -replace 'AssemblyFileVersion\(".*"\)', $newAssemblyFileVersion } |
        ForEach-Object {
            $_ -replace 'AssemblyInformationalVersion\(".*"\)', $newAssemblyInformationalVersion
        }  > $TmpFile
move-item $TmpFile $file.FullName -force
