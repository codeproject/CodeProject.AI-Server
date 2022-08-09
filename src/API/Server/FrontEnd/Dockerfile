#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM codeproject/ai-base:focal AS base

WORKDIR /app
EXPOSE 5000
ENV ASPNETCORE_URLS=http://+:5000

FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /src
COPY ["src/API/Server/FrontEnd/Frontend.csproj", "src/API/Server/FrontEnd/"]
COPY ["src/API/Server/Backend/Backend.csproj", "src/API/Server/Backend/"]
COPY ["src/API/Common/Common.csproj", "src/API/Common/"]
COPY ["src/AnalysisLayer/ObjectDetectionNet/ObjectDetectionNet.csproj", "src/AnalysisLayer/ObjectDetectionNet/"]
COPY ["src/AnalysisLayer/PortraitFilter/PortraitFilter.csproj", "src/AnalysisLayer/PortraitFilter/"]
#COPY ["src/AnalysisLayer/SentimentAnalysis/SentimentAnalysis.csproj", "src/AnalysisLayer/SentimentAnalysis/"]
COPY ["src/AnalysisLayer/SDK/ModelRunners/Yolov5Net.Scorer/Yolov5Net.Scorer.csproj", "src/AnalysisLayer/SDK/ModelRunners/Yolov5Net.Scorer/"]
Copy ["src/AnalysisLayer/SDK/NET/CodeProject.AI.AnalysisLayer.SDK/CodeProject.AI.AnalysisLayer.SDK.csproj", "src/AnalysisLayer/SDK/NET/CodeProject.AI.AnalysisLayer.SDK/" ]

RUN dotnet restore "src/API/Server/FrontEnd/Frontend.csproj"
COPY . .

WORKDIR "/src/src/API/Server/FrontEnd"
RUN dotnet build "Frontend.csproj" -c Release --no-self-contained -o /app/build/server

WORKDIR "/src/src/AnalysisLayer/ObjectDetectionNet"
RUN dotnet build "ObjectDetectionNet.csproj" -c Release --no-self-contained -o /app/build/AnalysisLayer/ObjectDetectionNet

WORKDIR "/src/src/AnalysisLayer/PortraitFilter"
RUN dotnet build "PortraitFilter.csproj" -c Release --no-self-contained -o /app/build/AnalysisLayer/PortraitFilter

#WORKDIR "/src/src/AnalysisLayer/SentimentAnalysis"
#RUN dotnet build "SentimentAnalysis.csproj" -c Release --no-self-contained -o /app/build/AnalysisLayer/SentimentAnalysis

FROM build AS publish

WORKDIR "/src/src/API/Server/FrontEnd"
RUN dotnet publish "Frontend.csproj" -c Release --no-self-contained -o /app/publish/server

WORKDIR "/src/src/AnalysisLayer/ObjectDetectionNet"
RUN dotnet publish "ObjectDetectionNet.csproj" -c Release --no-self-contained -o /app/publish/AnalysisLayer/ObjectDetectionNet

WORKDIR "/src/src/AnalysisLayer/PortraitFilter"
RUN dotnet publish "PortraitFilter.csproj" -c Release --no-self-contained -o /app/publish/AnalysisLayer/PortraitFilter

#WORKDIR "/src/src/AnalysisLayer/SentimentAnalysis"
#RUN dotnet publish "SentimentAnalysis.csproj" -c Release --no-self-contained -o /app/publish/AnalysisLayer/SentimentAnalysis

# zipping up the test images and moving to the server wwwroot directory so it can be downloaded from server.
RUN apt-get update -y && apt-get upgrade -y
RUN apt-get install -y zip

WORKDIR "/src/demos/TestData"
RUN zip -r testdata.zip .
RUN mv testdata.zip /app/publish/server/wwwroot

WORKDIR /src
COPY ["src/AnalysisLayer/BackgroundRemover/", "/app/publish/AnalysisLayer/BackgroundRemover"]
#COPY ["src/AnalysisLayer/TextSummary/", "/app/publish/AnalysisLayer/TextSummary"]
COPY ["src/AnalysisLayer/Vision/", "/app/publish/AnalysisLayer/Vision"]
COPY ["src/AnalysisLayer/CustomDetection/", "/app/publish/AnalysisLayer/CustomDetection"]
COPY ["src/AnalysisLayer/SDK/Python", "/app/publish/AnalysisLayer/SDK/Python"]
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
RUN add-apt-repository ppa:deadsnakes/ppa -y
RUN apt update -y
RUN apt-get install python3.9 -y
RUN python3.8 -m pip install --upgrade pip
RUN python3.9 -m pip install --upgrade pip

# CM: Added --no-cache-dir here to reduce the size of the image
RUN python3.8 -m pip --no-cache-dir install -r /app/AnalysisLayer/Vision/intelligencelayer/requirements.txt
#RUN python3.8 -m pip --no-cache-dir install -r /app/AnalysisLayer/TextSummary/requirements.txt
RUN python3.9 -m pip --no-cache-dir install -r /app/AnalysisLayer/BackgroundRemover/requirements.txt

WORKDIR /app/server
ENTRYPOINT ["dotnet", "CodeProject.AI.Server.dll"]