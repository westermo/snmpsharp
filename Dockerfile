FROM mcr.microsoft.com/dotnet/sdk:6.0

WORKDIR /src

COPY src /src/

CMD ["dotnet", "build", "./SnmpSharpNet.csproj"]