#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/runtime:5.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:5.0 AS build
WORKDIR /src
COPY ["src/AnalysisLayer/CodeProject.SenseAI.AnalysisLayer.Yolo/CodeProject.SenseAI.AnalysisLayer.Yolo.csproj", "src/AnalysisLayer/CodeProject.SenseAI.AnalysisLayer.Yolo/"]
RUN dotnet restore "src/AnalysisLayer/CodeProject.SenseAI.AnalysisLayer.Yolo/CodeProject.SenseAI.AnalysisLayer.Yolo.csproj"
COPY . .
WORKDIR "/src/src/AnalysisLayer/CodeProject.SenseAI.AnalysisLayer.Yolo"
RUN dotnet build "CodeProject.SenseAI.AnalysisLayer.Yolo.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "CodeProject.SenseAI.AnalysisLayer.Yolo.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "CodeProject.SenseAI.AnalysisLayer.Yolo.dll"]