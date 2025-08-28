# ---------- Build stage ----------
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# copy project file first (cache-friendly)
COPY quizTool.csproj ./
RUN dotnet restore

# copy rest and publish
COPY . ./
RUN dotnet publish -c Release -o /out /p:UseAppHost=false

# ---------- Runtime stage ----------
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

# Render expects apps to listen on port 10000 inside the container
ENV ASPNETCORE_URLS=http://0.0.0.0:10000
ENV ASPNETCORE_FORWARDEDHEADERS_ENABLED=true
EXPOSE 10000

COPY --from=build /out ./
ENTRYPOINT ["dotnet", "quizTool.dll"]
