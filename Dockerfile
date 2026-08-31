# Stage 1: Build Taro H5
FROM node:20-alpine AS taro-build
WORKDIR /taro
COPY OpenFindBearings.Taro/package.json OpenFindBearings.Taro/pnpm-lock.yaml ./
RUN corepack enable && pnpm install --frozen-lockfile
COPY OpenFindBearings.Taro/ ./
RUN pnpm run build:h5

# Stage 2: Build BFF
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS dotnet-build
WORKDIR /src
COPY OpenFindBearings.Mobile/src/OpenFindBearings.Mobile/OpenFindBearings.Mobile.csproj OpenFindBearings.Mobile/
RUN dotnet restore OpenFindBearings.Mobile/OpenFindBearings.Mobile.csproj
COPY OpenFindBearings.Mobile/src/OpenFindBearings.Mobile/ OpenFindBearings.Mobile/
WORKDIR /src/OpenFindBearings.Mobile
RUN dotnet publish -c Release -o /app/publish --no-restore

# Stage 3: Final image
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
EXPOSE 8080

# 复制 BFF 发布产物
COPY --from=dotnet-build /app/publish .

# 复制 Taro H5 构建产物到 wwwroot
COPY --from=taro-build /taro/dist ./wwwroot

ENTRYPOINT ["dotnet", "OpenFindBearings.Mobile.dll"]
