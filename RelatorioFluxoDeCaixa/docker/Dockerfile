FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["BuildingBlocks/Directory.Build.props", "BuildingBlocks/"]
COPY ["BuildingBlocks/src/Seguranca/Seguranca.csproj", "BuildingBlocks/src/Seguranca/"]
COPY ["BuildingBlocks/src/Mensageria/Mensageria.csproj", "BuildingBlocks/src/Mensageria/"]
COPY ["RelatorioFluxoDeCaixa/Directory.Build.props", "RelatorioFluxoDeCaixa/"]
COPY ["RelatorioFluxoDeCaixa/src/ConsolidadoDiario/ConsolidadoDiario.csproj", "RelatorioFluxoDeCaixa/src/ConsolidadoDiario/"]

RUN dotnet restore "RelatorioFluxoDeCaixa/src/ConsolidadoDiario/ConsolidadoDiario.csproj"

COPY ["BuildingBlocks/src/Seguranca/", "BuildingBlocks/src/Seguranca/"]
COPY ["BuildingBlocks/src/Mensageria/", "BuildingBlocks/src/Mensageria/"]
COPY ["RelatorioFluxoDeCaixa/", "RelatorioFluxoDeCaixa/"]

WORKDIR /src/RelatorioFluxoDeCaixa/src/ConsolidadoDiario
RUN dotnet build "ConsolidadoDiario.csproj" -c Release --no-restore

FROM build AS publish
RUN dotnet publish "ConsolidadoDiario.csproj" -c Release -o /app/publish --no-restore --no-build /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
EXPOSE 8080
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "ConsolidadoDiario.dll"]
