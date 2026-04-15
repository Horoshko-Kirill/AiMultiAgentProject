FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY . .

RUN dotnet restore src/AiMultiAgent.Stats.Service/AiMultiAgent.Stats.Service.csproj

RUN dotnet publish src/AiMultiAgent.Stats.Service/AiMultiAgent.Stats.Service.csproj     -c Release     -o /app/publish     --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
ENV ConnectionStrings__StatsDb=Data Source=/data/stats.db

VOLUME ["/data"]

EXPOSE 8080

ENTRYPOINT ["dotnet", "AiMultiAgent.Stats.Service.dll"]
