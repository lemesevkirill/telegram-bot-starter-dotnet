FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["src/BotTemplate.Api/BotTemplate.Api.csproj", "src/BotTemplate.Api/"]
COPY ["src/BotTemplate.Core/BotTemplate.Core.csproj", "src/BotTemplate.Core/"]
RUN dotnet restore "src/BotTemplate.Api/BotTemplate.Api.csproj"

COPY . .
WORKDIR "/src/src/BotTemplate.Api"
RUN dotnet publish "BotTemplate.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "BotTemplate.Api.dll"]
