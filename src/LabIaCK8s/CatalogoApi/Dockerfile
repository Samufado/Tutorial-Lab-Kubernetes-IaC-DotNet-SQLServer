FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["CatalogoApi/CatalogoApi.csproj", "CatalogoApi/"]
COPY ["CatalogoApi/packages.lock.json", "CatalogoApi/"]
RUN dotnet restore "CatalogoApi/CatalogoApi.csproj" --locked-mode
COPY CatalogoApi/ CatalogoApi/
RUN dotnet publish "CatalogoApi/CatalogoApi.csproj" -c Release -o /out --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=8080
COPY --from=build /out .
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "CatalogoApi.dll"]
