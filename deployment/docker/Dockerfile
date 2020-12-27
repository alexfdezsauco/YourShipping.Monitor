ARG PACKAGE_VERSION=${PACKAGE_VERSION:-1.0.0}
ARG DOTNET_SDK_VERSION=${DOTNET_SDK_VERSION:-3.1}
ARG DOTNET_ASP_NETCORE_RUNTIME_VERSION=${DOTNET_ASP_NETCORE_RUNTIME_VERSION:-3.1}
ARG DOCKER_REPOSITORY_PROXY=${DOCKER_REPOSITORY_PROXY:-mcr.microsoft.com}

# FROM $DOCKER_REPOSITORY_PROXY/dotnet/core/sdk:$DOTNET_SDK_VERSION AS build

FROM mcr.microsoft.com/dotnet/core/sdk@sha256:4805a0ec2e1acae2be9554679acca0ec368aaf50cabdd028c0aa3cb8ff34a751 AS build

ARG NUGET_REPOSITORY_PROXY=${NUGET_REPOSITORY_PROXY:-https://www.nuget.org/api/v2/}
# ENV NUGET_REPOSITORY_PROXY
ENV DOTNET_SYSTEM_NET_HTTP_USESOCKETSHTTPHANDLER=0

# COPY deployment/nexus/nexus.crt /usr/local/share/ca-certificates/
# RUN update-ca-certificates
		
WORKDIR /build/src
# COPY dotnet.csproj.tar.gz .
# RUN tar -xf dotnet.csproj.tar.gz
ADD dotnet.csproj.tar.gz .
RUN dotnet restore --source $NUGET_REPOSITORY_PROXY
COPY src/. .

WORKDIR /build/src/YourShipping.Monitor/Server
RUN dotnet publish -c Release -o ../../../output/Release/YourShipping.Monitor /p:ServerGarbageCollection=false

WORKDIR /build

# FROM $DOCKER_REPOSITORY_PROXY/dotnet/core/aspnet:$DOTNET_ASP_NETCORE_RUNTIME_VERSION
FROM mcr.microsoft.com/dotnet/core/aspnet@sha256:07d9cfc7442360e2827b638e78d15e24f6b1c108ffa68d9ba29f3ab699b41edf

ENV ASPNETCORE_URLS=http://0.0.0.0:80

WORKDIR /app
RUN echo "deb [trusted=yes] http://packages.ubuntu.com/xenial/libtesseract3 ubuntu-xenial main" | tee /etc/apt/sources.list.d/libtesseract.list
RUN apt -y update & apt -y upgrade
RUN apt install -y gnupg

# RUN apt -y update && apt -y install libleptonica-dev
# RUN mkdir -p /app/x64
# RUN cp /usr/lib/x86_64-linux-gnu/liblept.so /app/x64/liblept1753.so
# RUN apt install -y libc6-dev  libgdiplus  libx11-dev
# RUN apt install -y libgif7 libjpeg62 libopenjp2-7 libpng16-16 libtiff5 libwebp6 libc6-dev libgdiplus


RUN apt-key adv --keyserver keyserver.ubuntu.com --recv-keys 3B4FE6ACC0B21F32
RUN apt clean
RUN apt -y update && apt -y install libleptonica-dev libgif7 libjpeg62 libopenjp2-7 libpng16-16 libtiff5 libwebp6 libc6-dev libgdiplus
ADD deployment/lib/x64 /app/x64

# RUN apt install -y libtesseract-dev
# RUN cp /usr/lib/x86_64-linux-gnu/libtesseract.so /app/x64/libtesseract3052.so

COPY --from=build /build/output/Release/YourShipping.Monitor .

RUN rm appsettings.json
RUN rm appsettings.Development.json

VOLUME  /app/captchas
VOLUME  /app/re-captchas
VOLUME  /app/data
VOLUME  /app/logs

ENTRYPOINT ["dotnet", "YourShipping.Monitor.Server.dll"]