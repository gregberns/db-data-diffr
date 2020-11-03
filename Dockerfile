FROM mcr.microsoft.com/dotnet/core/sdk:3.1 AS build

WORKDIR /source

COPY src/*.csproj src/
RUN dotnet restore src

COPY src src
RUN dotnet build -c release src

RUN dotnet publish src -c Release -o /app --no-restore -r linux-x64 -p:PublishSingleFile=true --self-contained true

RUN ls /app
