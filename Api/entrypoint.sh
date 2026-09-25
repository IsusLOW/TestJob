# Каждый запуск restore + build + run в этом же контейнере.
set -e
dotnet restore
exec dotnet run --no-launch-profile -c Release
