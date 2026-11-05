FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["BuildingBlocks/Directory.Build.props", "BuildingBlocks/"]
COPY ["BuildingBlocks/src/Seguranca/Seguranca.csproj", "BuildingBlocks/src/Seguranca/"]
COPY ["BuildingBlocks/src/Mensageria/Mensageria.csproj", "BuildingBlocks/src/Mensageria/"]
COPY ["FluxoDeCaixa/Directory.Build.props", "FluxoDeCaixa/"]
COPY ["FluxoDeCaixa/src/LivroRazao/LivroRazao.csproj", "FluxoDeCaixa/src/LivroRazao/"]

RUN dotnet restore "FluxoDeCaixa/src/LivroRazao/LivroRazao.csproj"

COPY ["BuildingBlocks/src/Mensageria/", "BuildingBlocks/src/Mensageria/"]
COPY ["BuildingBlocks/src/Seguranca/", "BuildingBlocks/src/Seguranca/"]
COPY ["FluxoDeCaixa/", "FluxoDeCaixa/"]

WORKDIR "/src/FluxoDeCaixa/src/LivroRazao"

RUN dotnet build "LivroRazao.csproj" -c Release --no-restore

FROM build AS migrations

RUN dotnet tool install --global dotnet-ef --version 10.0.11

ENV PATH="${PATH}:/root/.dotnet/tools"

ENTRYPOINT ["dotnet", "ef", "database", "update", "--project", "LivroRazao.csproj", "--startup-project", "LivroRazao.csproj", "--context", "LivroRazaoContexto", "--configuration", "Release", "--no-build"]

FROM build AS publish

RUN dotnet publish "LivroRazao.csproj" -c Release -o /app/publish --no-restore --no-build /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

EXPOSE 8080

COPY --from=publish /app/publish .

ENTRYPOINT ["dotnet", "LivroRazao.dll"]
