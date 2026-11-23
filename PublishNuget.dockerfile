FROM mcr.microsoft.com/dotnet/sdk:11.0
ARG CONFIGURATION=Release

WORKDIR /workspace

COPY Directory.Build.props Directory.Packages.props ./
COPY src/ src/

RUN dotnet build --configuration "${CONFIGURATION}" src/SnmpSharpNet.csproj && \
    dotnet pack --configuration "${CONFIGURATION}" --no-build -o /output src/SnmpSharpNet.csproj

ENV NUGET_TOKEN=

CMD for nupkg in $(ls /output/*.nupkg); do \
        dotnet nuget push "$nupkg" --api-key "${NUGET_TOKEN}" --source https://nuget.pkg.github.com/westermo/index.json --skip-duplicate; \
    done
