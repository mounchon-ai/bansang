FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Directory.Build.props Bansang.sln ./
COPY src/Bansang.Domain/Bansang.Domain.csproj src/Bansang.Domain/
COPY src/Bansang.Application/Bansang.Application.csproj src/Bansang.Application/
COPY src/Bansang.Infrastructure/Bansang.Infrastructure.csproj src/Bansang.Infrastructure/
COPY src/Bansang.Api/Bansang.Api.csproj src/Bansang.Api/
RUN dotnet restore src/Bansang.Api/Bansang.Api.csproj
COPY src/ src/
RUN dotnet publish src/Bansang.Api/Bansang.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "Bansang.Api.dll"]
