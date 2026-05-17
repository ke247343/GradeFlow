# STAGE 1: Pull SDK image toolsets down from registry to compile source structures
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build-env
WORKDIR /app

# Stage dependencies maps files explicitly to leverage docker caching patterns
COPY *.csproj ./
RUN dotnet restore

# Move remaining components into scope boundaries and compile runtime binaries
COPY . ./
RUN dotnet publish -c Release -o out

# STAGE 2: Drop heavy compilers out and mount optimized light runtime engine layers
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build-env /app/out .

# Expose internal connection ports and set processing execution pipelines
EXPOSE 8080
ENTRYPOINT ["dotnet", "GradeFlow.dll"]
