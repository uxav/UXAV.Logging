param([string] $path = "" )
$version = (Get-Item UXAV.Logging/bin/Release/UXAV.Logging.dll).VersionInfo.ProductVersion
$versionString = "v{0}.{1}.{2}" -f $version.Major, $version.Minor, $version.Build
Write-Output $versionString