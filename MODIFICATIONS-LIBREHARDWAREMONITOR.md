# Adaptations de LibreHardwareMonitor

## Version de reference

Liquid Glass Monitor utilise une version adaptee des sources de
LibreHardwareMonitor 0.9.6.

Les sources sont fournies dans :

`Dependencies/LibreHardwareMonitor-0.9.6/`

La licence MPL-2.0 et les notices originales sont conservees.

## Adaptations identifiees

La comparaison avec les sources officielles de LibreHardwareMonitor
version 0.9.6 a ete realisee a partir du depot :

https://github.com/LibreHardwareMonitor/LibreHardwareMonitor

Tag officiel : `v0.9.6`

Commit : `3d331e3370efb858411f19511373eff65a218701`

Resultats de la comparaison de 420 fichiers :

- 418 fichiers sans difference de contenu apres neutralisation
  des espaces en fin de ligne.
- 2 fichiers modifies.
- Aucun fichier ajoute ou supprime.

Les differences de fins de ligne Windows/Unix expliquent
l'essentiel des differences SHA256 initialement observees.
### 1. LibreHardwareMonitorLib.csproj

Suppression de la reference NuGet :

`Mono.Posix.NETStandard` version `1.0.0`

Cette adaptation concerne la preparation Windows de Liquid Glass Monitor.

### 2. PawnIo.cs

Ajout du chargement experimental de modules PawnIO externes via :

`LGM_PAWNIO_OVERRIDE_DIR`

Les adaptations comprennent :

- Une liste blanche de 12 modules PawnIO.
- La validation des noms de ressources.
- La normalisation et la verification des chemins.
- Le controle de l'existence des fichiers externes.
- Le rejet des fichiers externes vides.
- Le maintien du chargement des ressources embarquees par defaut.
- La liberation explicite du handle Windows en cas d'echec.
- La liberation du handle lorsqu'une exception survient.

Le marqueur UTF-8 BOM a egalement ete retire du fichier.

Le chargement effectif de modules modifies reste soumis
aux restrictions du pilote PawnIO, notamment de signature.

La procedure de reconstruction est decrite dans :

`PAWNIO-REBUILD.md`
## Licences et redistribution

Les fichiers de LibreHardwareMonitor restent soumis aux licences
et notices qui leur sont applicables, notamment MPL-2.0.

Les composants tiers conservent leurs licences respectives.

Consulter egalement :

- `THIRD-PARTY-NOTICES.md`
- `PAWNIO-REBUILD.md`
- `Dependencies/LibreHardwareMonitor-0.9.6/LICENSE`
- `Dependencies/LibreHardwareMonitor-0.9.6/THIRD-PARTY-NOTICES.txt`

## Portee du document

Ce document recense les adaptations identifiees lors de la
preparation de Liquid Glass Monitor pour GitHub.

Il ne constitue pas un diff exhaustif avec les sources officielles
de LibreHardwareMonitor 0.9.6.

## Perimetre des sources conservees

La distribution du code source de Liquid Glass Monitor conserve
uniquement le projet `LibreHardwareMonitorLib` de LibreHardwareMonitor 0.9.6.

Les projets `Aga.Controls` et `LibreHardwareMonitor` (application
graphique autonome) ont ete exclus de cette copie car ils ne sont pas
necessaires a la compilation de Liquid Glass Monitor.

Les sources de `LibreHardwareMonitorLib`, ses ressources PawnIO
integrees, les fichiers de licence et les mentions legales
necessaires sont conserves.

Une compilation de controle en Release, pour Windows x64,
a reussi apres ce retrait, le 15 novembre 2026.

Cette reduction du perimetre des sources distribuees est distincte
des modifications du code de `LibreHardwareMonitorLib` deja
documentees ci-dessus.