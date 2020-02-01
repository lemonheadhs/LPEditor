$(
    "cd ./src/LPEditor; dotnet watch run -- '../../tmp/Claims-based Identity Second Edition device.txt'"
    "cd ./src/LPEditor.Js; npm run build"
) | % -Parallel {iex $_}