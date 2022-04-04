#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM codeproject/senseai-base:focal AS base

WORKDIR /app
EXPOSE 5000
ENV ASPNETCORE_URLS=http://+:5000

FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /src
COPY ["src/API/Server/FrontEnd/Frontend.csproj", "src/API/Server/FrontEnd/"]
COPY ["src/API/Server/Backend/Backend.csproj", "src/API/Server/Backend/"]
COPY ["src/API/Common/Common.csproj", "src/API/Common/"]
COPY ["src/AnalysisLayer/CodeProject.SenseAI.AnalysisLayer.Yolo/CodeProject.SenseAI.AnalysisLayer.Yolo.csproj", "src/AnalysisLayer/CodeProject.SenseAI.AnalysisLayer.Yolo/"]
COPY ["src/AnalysisLayer/SDK/ModelRunners/Yolov5Net.Scorer/Yolov5Net.Scorer.csproj", "src/AnalysisLayer/SDK/ModelRunners/Yolov5Net.Scorer/"]
RUN dotnet restore "src/API/Server/FrontEnd/Frontend.csproj"
COPY . .
WORKDIR "/src/src/API/Server/FrontEnd"
RUN dotnet build "Frontend.csproj" -c Release -o /app/build/server
WORKDIR "/src/src/AnalysisLayer/CodeProject.SenseAI.AnalysisLayer.Yolo"
RUN dotnet build "CodeProject.SenseAI.AnalysisLayer.Yolo.csproj" -c Release -o /app/build/AnalysisLayer/CodeProject.SenseAI.AnalysisLayer.Yolo

FROM build AS publish
WORKDIR "/src/src/API/Server/FrontEnd"
RUN dotnet publish "Frontend.csproj" -c Release -o /app/publish/server
WORKDIR "/src/src/AnalysisLayer/CodeProject.SenseAI.AnalysisLayer.Yolo"
RUN dotnet publish "CodeProject.SenseAI.AnalysisLayer.Yolo.csproj" -c Release -o /app/publish/AnalysisLayer/CodeProject.SenseAI.AnalysisLayer.Yolo

# zipping up the test images and moving to the server wwwroot directory so it can be downloaded from server.
#RUN apt-get update -y && apt-get upgrade -y
#RUN apt-get install -y zip
#WORKDIR "/src/demos/TestData"
#RUN zip -r testdata.zip .
#RUN mv testdata.zip /app/publish/server/wwwroot
#
WORKDIR /src
COPY ["src/AnalysisLayer/TextSummary/", "/app/publish/AnalysisLayer/TextSummary"]
COPY ["src/AnalysisLayer/DeepStack/intelligencelayer", "/app/publish/AnalysisLayer/DeepStack/intelligencelayer"]
COPY ["src/AnalysisLayer/DeepStack/assets", "/app/publish/AnalysisLayer/DeepStack/assets"]
COPY ["demos/TestData", "/app/publish/demos/TestData"]

FROM base AS final
#These libraries are needed for System.Drawing to work on Linux in NET 6.

#libfontconfig1 is required for SkiaSharp
RUN apt-get install -y libfontconfig1

#libgdplus is required for System.Drawing
RUN apt-get install -y libgdiplus

EXPOSE 5000
ENV ASPNETCORE_URLS=http://+:5000

# the simple log format is easier on my brain than json
ENV LOGGING__CONSOLE__FORMATTERNAME=simple

WORKDIR /app
COPY --from=publish /app/publish .
RUN pip3 install -r /app/AnalysisLayer/TextSummary/requirements.txt
RUN pip3 install -r /app/AnalysisLayer/DeepStack/intelligencelayer/requirements.txt

WORKDIR /app/server
ENTRYPOINT ["dotnet", "CodeProject.SenseAI.Server.dll"]