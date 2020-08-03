param([string] $Path = "" )
$dll = Get-Item -Path $Path
$version = [version]$dll.VersionInfo.ProductVersion
$versionString = "v{0}.{1}.{2}" -f $version.Major, $version.Minor, $version.Build
Write-Output $versionString