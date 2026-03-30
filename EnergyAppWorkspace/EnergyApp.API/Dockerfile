FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY EnergyApp.API/EnergyApp.API.csproj EnergyApp.API/
RUN dotnet restore EnergyApp.API/EnergyApp.API.csproj

COPY . .
RUN dotnet publish EnergyApp.API/EnergyApp.API.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

EXPOSE 5008
ENV ASPNETCORE_URLS=http://+:5008

ENTRYPOINT ["dotnet", "EnergyApp.API.dll"]
