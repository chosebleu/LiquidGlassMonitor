# Liquid Glass Monitor

Application de surveillance materielle pour Windows.

**Auteur :** chosebleu

## Presentation

Liquid Glass Monitor est une application Windows developpee en C# avec WPF et .NET 10.
Elle propose une interface inspiree du verre translucide et plusieurs modules de surveillance du systeme.

## Technologies

- C#
- WPF
- .NET 10
- LibreHardwareMonitorLib

## Compilation

Installer le SDK .NET 10, puis executer depuis le dossier du projet :

    dotnet restore LiquidGlassMonitor.csproj
    dotnet build LiquidGlassMonitor.csproj -c Release

## Compatibilite

Application destinee a Windows. La disponibilite des mesures depend du materiel et des pilotes.

## Licence

Les informations de licence et les mentions des composants tiers seront ajoutees avant la publication publique.