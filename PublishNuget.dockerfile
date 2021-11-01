FROM mcr.microsoft.com/dotnet/sdk:6.0
ARG CONFIGURATION=Release

WORKDIR /src

COPY src /src/

RUN mkdir /output && \
    dotnet build --configuration "${CONFIGURATION}" ./SnmpSharpNet.csproj && \
    dotnet pack --configuration "${CONFIGURATION}" -o /output ./SnmpSharpNet.csproj

ENV NUGET_TOKEN=

CMD for nupkg in $(ls /output/*.nupkg); do \
        dotnet nuget push "$nupkg" --api-key "${NUGET_TOKEN}" --source https://nuget.pkg.github.com/westermo/index.json --skip-duplicate; \
    done
