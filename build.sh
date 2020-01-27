#!/bin/sh
if [ ! -e "paket.lock" ]
then
    exec mono .paket/paket.exe install
fi
dotnet restore src/LPEditor
dotnet build src/LPEditor

