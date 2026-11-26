# Stage 1: Restore and publish ASP.NET Core 9.0 MVC application
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build

WORKDIR /src

COPY FrontendNet.csproj ./
RUN dotnet restore FrontendNet.csproj

COPY . ./
RUN dotnet publish FrontendNet.csproj -c Release -o /app/publish /p:UseAppHost=false

# Stage 2: Hardened non-root runtime image
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final

WORKDIR /app

COPY --from=build /app/publish ./

ENV ASPNETCORE_URLS=http://0.0.0.0:5000
EXPOSE 5000

RUN mkdir -p /home/app/.aspnet/DataProtection-Keys \
    && chown -R "$APP_UID:$APP_UID" /home/app/.aspnet

USER $APP_UID

ENTRYPOINT ["dotnet", "FrontendNet.dll"]
