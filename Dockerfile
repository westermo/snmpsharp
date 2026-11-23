FROM mcr.microsoft.com/dotnet/sdk:11.0

WORKDIR /workspace

COPY Directory.Build.props Directory.Packages.props ./
COPY src/ src/

CMD ["dotnet", "build", "--configuration", "Release", "src/SnmpSharpNet.csproj"]