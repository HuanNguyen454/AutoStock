FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ASM.sln ./
COPY src/ASM.Domain/ASM.Domain.csproj src/ASM.Domain/
COPY src/ASM.Application/ASM.Application.csproj src/ASM.Application/
COPY src/ASM.WebPortal/ASM.WebPortal.csproj src/ASM.WebPortal/

RUN dotnet restore src/ASM.WebPortal/ASM.WebPortal.csproj

COPY . .
RUN dotnet publish src/ASM.WebPortal/ASM.WebPortal.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "ASM.WebPortal.dll"]
