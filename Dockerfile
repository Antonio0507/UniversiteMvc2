FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY UniversiteMvc2/ UniversiteMvc2/
RUN dotnet publish UniversiteMvc2/UniversiteMvc2.csproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0
# Polices et bibliothèque requises par QuestPDF pour générer les PDF sous Linux
RUN apt-get update && apt-get install -y --no-install-recommends libfontconfig1 fonts-dejavu-core && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /app .
# Clés de chiffrement des cookies, conservées dans un volume (voir compose.yaml)
RUN mkdir -p /donnees/cles && chown -R app:app /donnees
USER app
ENV ASPNETCORE_URLS=http://+:8080
ENV DataProtection__Dossier=/donnees/cles
EXPOSE 8080
ENTRYPOINT ["dotnet", "UniversiteMvc2.dll"]
