
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["Wanderlust.csproj", "./"]
RUN dotnet restore "Wanderlust.csproj"

COPY . .
RUN dotnet publish "Wanderlust.csproj" -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["sh", "-c", "dotnet Wanderlust.dll --urls http://0.0.0.0:${PORT:-10000}"]
