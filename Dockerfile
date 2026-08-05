FROM node:22-alpine AS frontend-build
WORKDIR /src/frontend

COPY frontend/package.json frontend/package-lock.json ./
RUN npm ci

COPY frontend/ ./
RUN npm run build

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS backend-build
WORKDIR /src

COPY backend/PadelBooking.API/PadelBooking.API.csproj backend/PadelBooking.API/
RUN dotnet restore backend/PadelBooking.API/PadelBooking.API.csproj

COPY backend/PadelBooking.API/ backend/PadelBooking.API/
RUN dotnet publish backend/PadelBooking.API/PadelBooking.API.csproj \
    --configuration Release \
    --output /app/publish \
    --no-restore \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

ENV ASPNETCORE_ENVIRONMENT=Production \
    ASPNETCORE_HTTP_PORTS=8080 \
    ASPNETCORE_FORWARDEDHEADERS_ENABLED=true

COPY --from=backend-build /app/publish ./
COPY --from=frontend-build /src/frontend/dist ./wwwroot

EXPOSE 8080
ENTRYPOINT ["dotnet", "PadelBooking.API.dll"]
