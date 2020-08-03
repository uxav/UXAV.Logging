param([string] $path = "" )
$dll = Get-Item -Path $path
$version = [version]$dll.VersionInfo.ProductVersion
$versionString = "v{0}.{1}.{2}" -f $version.Major, $version.Minor, $version.Build
Write-Output $versionString