FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY NuGet.Config ./
COPY A2G.MemoryFlightRecorder.slnx ./
COPY src/A2G.MemoryFlightRecorder/A2G.MemoryFlightRecorder.csproj src/A2G.MemoryFlightRecorder/
COPY samples/LeakyApi/LeakyApi.csproj samples/LeakyApi/

RUN dotnet restore samples/LeakyApi/LeakyApi.csproj --configfile NuGet.Config

COPY src ./src
COPY samples ./samples

RUN dotnet publish samples/LeakyApi/LeakyApi.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

ENV ASPNETCORE_URLS=http://0.0.0.0:5000
ENV MemoryFlightRecorderSample__Urls=http://0.0.0.0:5000
ENV MemoryFlightRecorderSample__DumpDirectory=/app/memory-dumps

COPY --from=build /app/publish ./

RUN mkdir -p /app/memory-dumps

EXPOSE 5000

ENTRYPOINT ["dotnet", "LeakyApi.dll"]
