# See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

# This stage is used when running from VS in fast mode (Default for Debug configuration)
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER $APP_UID
WORKDIR /app

# Switch to root user to install packages
USER root

# Install Ghostscript and Tesseract OCR dependencies with german and english language packs
RUN apt-get update && apt-get install -y \
    ghostscript \
    tesseract-ocr \
    tesseract-ocr-eng \
    tesseract-ocr-deu \
    libleptonica-dev \
    libtesseract-dev \
    libc6-dev \
    && rm -rf /var/lib/apt/lists/*

# Find the tessdata directory and create a symlink at /usr/share/tessdata for compatibility
# Also set TESSDATA_PREFIX environment variable
RUN TESSDATA_DIR=$(find /usr/share/tesseract-ocr -name tessdata -type d | head -n 1) && \
    ln -sf $TESSDATA_DIR /usr/share/tessdata && \
    echo "Tessdata directory: $TESSDATA_DIR"

ENV TESSDATA_PREFIX=/usr/share/tessdata

# Hack to allow Tesseract NuGet package to work
# Create symlink for libdl.so in system directory
RUN ln -s /usr/lib/x86_64-linux-gnu/libdl.so.2 /usr/lib/x86_64-linux-gnu/libdl.so

# Create x64 directory and symlinks for Tesseract libraries
# The Tesseract NuGet package looks for native libraries in /app/x64/
WORKDIR /app/x64
RUN ln -s /usr/lib/x86_64-linux-gnu/liblept.so.5 /app/x64/libleptonica-1.82.0.so && \
    ln -s /usr/lib/x86_64-linux-gnu/libtesseract.so.5 /app/x64/libtesseract50.so

# Switch back to the non-root user
USER $APP_UID
WORKDIR /app

# This stage is used to build the service project
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["SmartArchivist.Ocr/SmartArchivist.Ocr.csproj", "SmartArchivist.Ocr/"]
COPY ["SmartArchivist.Contract/SmartArchivist.Contract.csproj", "SmartArchivist.Contract/"]
COPY ["SmartArchivist.Dal/SmartArchivist.Dal.csproj", "SmartArchivist.Dal/"]
COPY ["SmartArchivist.Infrastructure/SmartArchivist.Infrastructure.csproj", "SmartArchivist.Infrastructure/"]
RUN dotnet restore "./SmartArchivist.Ocr/SmartArchivist.Ocr.csproj"
COPY . .
WORKDIR "/src/SmartArchivist.Ocr"
RUN dotnet build "./SmartArchivist.Ocr.csproj" -c $BUILD_CONFIGURATION -o /app/build

# This stage is used to publish the service project to be copied to the final stage
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./SmartArchivist.Ocr.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# This stage is used in production or when running from VS in regular mode (Default when not using the Debug configuration)
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "SmartArchivist.Ocr.dll"]