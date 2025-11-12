FROM mcr.microsoft.com/dotnet/sdk:10.0

WORKDIR /src

COPY src /src/

CMD ["dotnet", "build", "--configuration", "Release", "./SnmpSharpNet.csproj"]