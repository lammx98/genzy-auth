# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy csproj files and restore dependencies
COPY ["genzy-auth/genzy-auth.csproj", "genzy-auth/"]
COPY ["genzy-base/genzy-base.csproj", "genzy-base/"]
RUN dotnet restore "genzy-auth/genzy-auth.csproj"

# Copy source code and build
COPY genzy-auth/ genzy-auth/
COPY genzy-base/ genzy-base/
WORKDIR /src/genzy-auth
RUN dotnet build "genzy-auth.csproj" -c Release -o /app/build

# Publish stage
FROM build AS publish
RUN dotnet publish "genzy-auth.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Install curl for health checks
RUN apt-get update && apt-get install -y curl && rm -rf /var/lib/apt/lists/*

# Copy published files
COPY --from=publish /app/publish .

# Set environment variables
ENV ASPNETCORE_URLS=http://+:80
ENV ASPNETCORE_ENVIRONMENT=Production

# Expose port
EXPOSE 80

# Health check
HEALTHCHECK --interval=30s --timeout=3s --start-period=10s --retries=3 \
  CMD curl -f http://localhost/health || exit 1

ENTRYPOINT ["dotnet", "genzy-auth.dll"]
