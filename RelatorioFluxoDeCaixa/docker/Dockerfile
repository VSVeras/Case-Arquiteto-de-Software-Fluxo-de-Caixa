FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["src/ConsolidadoDiario/ConsolidadoDiario.csproj", "src/ConsolidadoDiario/"]
RUN dotnet restore "src/ConsolidadoDiario/ConsolidadoDiario.csproj"

COPY . .
WORKDIR "/src/src/ConsolidadoDiario"
RUN dotnet publish "ConsolidadoDiario.csproj" -c Release -o /app/publish --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
EXPOSE 8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "ConsolidadoDiario.dll"]
