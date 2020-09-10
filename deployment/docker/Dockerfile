ARG PACKAGE_VERSION=${PACKAGE_VERSION:-1.0.0}
ARG DOTNET_SDK_VERSION=${DOTNET_SDK_VERSION:-3.1}
ARG DOTNET_ASP_NETCORE_RUNTIME_VERSION=${DOTNET_ASP_NETCORE_RUNTIME_VERSION:-3.1}
ARG DOCKER_REPOSITORY_PROXY=${DOCKER_REPOSITORY_PROXY:-mcr.microsoft.com}

FROM $DOCKER_REPOSITORY_PROXY/dotnet/core/sdk:$DOTNET_SDK_VERSION AS build

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
RUN dotnet publish -c Release -o ../../../output/Release/YourShipping.Monitor

WORKDIR /build

FROM $DOCKER_REPOSITORY_PROXY/dotnet/core/aspnet:$DOTNET_ASP_NETCORE_RUNTIME_VERSION
ENV ASPNETCORE_URLS=http://0.0.0.0:80

VOLUME  /app/data
VOLUME  /app/logs

WORKDIR /app
COPY --from=build /build/output/Release/YourShipping.Monitor .

RUN rm appsettings.json
RUN rm appsettings.Development.json

# COPY deployment/docker/appsettings.json .
ENTRYPOINT ["dotnet", "YourShipping.Monitor.Server.dll"]