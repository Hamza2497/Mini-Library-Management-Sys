FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["Library.Api/Library.Api.csproj", "Library.Api/"]
RUN dotnet restore "Library.Api/Library.Api.csproj"

COPY . .
RUN dotnet publish "Library.Api/Library.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Library.Api.dll"]
