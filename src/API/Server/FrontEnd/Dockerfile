#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:5.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:5.0 AS build
WORKDIR /src
COPY ["src/API/CodeProject.AI.FrontEnd/CodeProject.AI.FrontEnd.csproj", "src/API/CodeProject.AI.FrontEnd/"]
COPY ["src/API/CodeProject.AI.Backend/CodeProject.AI.Backend.csproj", "src/API/CodeProject.AI.Backend/"]
COPY ["src/API/CodeProject.Ai.Common/CodeProject.Ai.Common.csproj", "src/API/CodeProject.Ai.Common/"]
RUN dotnet restore "src/API/CodeProject.AI.FrontEnd/CodeProject.AI.FrontEnd.csproj"
COPY . .
WORKDIR "/src/src/API/CodeProject.AI.FrontEnd"
RUN dotnet build "CodeProject.AI.FrontEnd.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "CodeProject.AI.FrontEnd.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "CodeProject.AI.FrontEnd.dll"]