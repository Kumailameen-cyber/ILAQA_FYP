
# Stage 1: Build ILAQA
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY backend/ILAQA/ILAQA.csproj backend/ILAQA/
RUN dotnet restore backend/ILAQA/ILAQA.csproj

COPY backend/ILAQA/ backend/ILAQA/
RUN dotnet publish backend/ILAQA/ILAQA.csproj -c Release -o /app/publish --no-restore

# Stage 2: Run ILAQA
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app

COPY --from=build /app/publish .

ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

USER $APP_UID

ENTRYPOINT ["dotnet", "ILAQA.dll"]
