rm .\src\LPEditor\WebRoot\*.* -Exclude editor.css
rm ./publish/*.* -re -force
pushd .\src\LPEditor.Js
npx webpack -p
cd ..\LPEditor\
dotnet publish -o ../../publish
popd