FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["src/OpenFindBearings.Mobile/OpenFindBearings.Mobile.csproj", "OpenFindBearings.Mobile/"]
RUN dotnet restore "OpenFindBearings.Mobile/OpenFindBearings.Mobile.csproj"
COPY src/OpenFindBearings.Mobile/ "OpenFindBearings.Mobile/"
WORKDIR "/src/OpenFindBearings.Mobile"
RUN dotnet publish -c Release -o /app/publish --no-restore

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "OpenFindBearings.Mobile.dll"]
