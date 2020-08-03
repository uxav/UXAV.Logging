param([string] $path = "" )
$version = (Get-Item PaymentSenseAV/bin/Release/PaymentSenseAV.dll).VersionInfo.ProductVersion
$version = "v{0}.{1}.{2}" -f $version.Major, $version.Minor, $version.Build
Write-Output $version