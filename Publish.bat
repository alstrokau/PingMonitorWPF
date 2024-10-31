echo "Publish start"
dotnet publish -c Release -r win-x64 -p:PublishSingleFile=true
pause "Publish done"