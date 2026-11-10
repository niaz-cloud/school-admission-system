# ---- build ------------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY SchoolAdmission.sln ./
COPY src/SchoolAdmission.Api/*.csproj src/SchoolAdmission.Api/
COPY tests/SchoolAdmission.Api.Tests/*.csproj tests/SchoolAdmission.Api.Tests/
RUN dotnet restore
COPY . .
RUN dotnet publish src/SchoolAdmission.Api -c Release -o /app --no-restore
RUN dotnet build tests/SchoolAdmission.Api.Tests -c Release --no-restore

# ---- test (used by `docker compose --profile test run --rm backend-tests`) ---------------
FROM build AS test
ENTRYPOINT ["dotnet", "test", "tests/SchoolAdmission.Api.Tests", "-c", "Release", "--no-build", "--logger", "console;verbosity=normal"]

# ---- runtime ----------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "SchoolAdmission.Api.dll"]
