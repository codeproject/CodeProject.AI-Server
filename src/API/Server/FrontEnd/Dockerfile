#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:5.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:5.0 AS build
WORKDIR /src
COPY ["src/API/Server/FrontEnd/FrontEnd.csproj", "src/API/Server/FrontEnd/"]
COPY ["src/API/Server/Backend/Backend.csproj", "src/API/Server/Backend/"]
COPY ["src/API/Common/Common.csproj", "src/API/Common/"]
RUN dotnet restore "src/API/Server/FrontEnd/FrontEnd.csproj"
COPY . .
WORKDIR "/src/API/Server/FrontEnd"
RUN dotnet build "rontEnd.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "FrontEnd.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Server.dll"]