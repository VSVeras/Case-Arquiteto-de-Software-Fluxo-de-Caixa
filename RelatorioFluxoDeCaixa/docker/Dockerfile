FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build

WORKDIR /src

COPY ["Directory.Build.props", "./"]
COPY ["RelatorioFluxoDeCaixa.sln", "./"]
COPY ["src/ConsolidadoDiario/ConsolidadoDiario.csproj", "src/ConsolidadoDiario/"]

RUN dotnet restore "src/ConsolidadoDiario/ConsolidadoDiario.csproj"

COPY . .

WORKDIR /src/src/ConsolidadoDiario

RUN dotnet publish "ConsolidadoDiario.csproj" \
    --configuration Release \
    --output /app/publish \
    --no-restore \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final

WORKDIR /app

COPY --from=build /app/publish .

EXPOSE 8080

ENTRYPOINT ["dotnet", "ConsolidadoDiario.dll"]