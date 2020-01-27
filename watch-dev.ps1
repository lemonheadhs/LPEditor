$(
    "cd ./src/LPEditor; dotnet watch run"
    "cd ./src/LPEditor.Js; npm run build"
) | % -Parallel {iex $_}