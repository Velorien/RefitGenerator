rm -r nupkg
dotnet pack
dotnet tool uninstall -g RefitGenerator
dotnet tool install -g --add-source nupkg RefitGenerator