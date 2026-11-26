#!/bin/sh
set -eu

rm -rf ./dist
dotnet publish DotNetUnicodeExplorer.csproj \
  --configuration Release \
  --output dist \
  --disable-build-servers \
  -p:UseSharedCompilation=false
