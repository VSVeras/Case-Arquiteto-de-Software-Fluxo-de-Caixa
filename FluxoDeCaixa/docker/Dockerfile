FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["src/LivroRazao/LivroRazao.csproj", "src/LivroRazao/"]
RUN dotnet restore "src/LivroRazao/LivroRazao.csproj"

COPY . .

WORKDIR "/src/src/LivroRazao"

RUN dotnet build "LivroRazao.csproj" -c Release --no-restore

FROM build AS migrations

RUN dotnet tool install --global dotnet-ef --version 8.0.11

ENV PATH="${PATH}:/root/.dotnet/tools"

ENTRYPOINT ["dotnet", "ef", "database", "update", "--project", "LivroRazao.csproj", "--startup-project", "LivroRazao.csproj", "--context", "LivroRazaoContexto", "--configuration", "Release", "--no-build"]

FROM build AS publish

RUN dotnet publish "LivroRazao.csproj" -c Release -o /app/publish --no-restore --no-build /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

EXPOSE 8080

COPY --from=publish /app/publish .

ENTRYPOINT ["dotnet", "LivroRazao.dll"]