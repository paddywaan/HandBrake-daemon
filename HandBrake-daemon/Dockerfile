#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.
FROM mcr.microsoft.com/dotnet/core/runtime:3.1-buster-slim AS base
WORKDIR /app
RUN apt-get update && apt-get install -y handbrake-cli && apt-get clean 

FROM mcr.microsoft.com/dotnet/core/sdk:3.1-buster AS build
WORKDIR /src
COPY ["HandBrake-daemon/HandBrake-daemon.csproj", "HandBrake-daemon/"]


RUN dotnet restore "HandBrake-daemon/HandBrake-daemon.csproj"
COPY . .

WORKDIR "/src/HandBrake-daemon"
RUN dotnet build "HandBrake-daemon.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "HandBrake-daemon.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
COPY --from=publish /app/publish/default.conf .
ENTRYPOINT ["dotnet", "handbrake-daemon.dll"]