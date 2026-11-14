# Composants tiers - Liquid Glass Monitor

Liquid Glass Monitor est distribue sous licence Mozilla Public License 2.0
(MPL-2.0). Les composants tiers conservent leurs propres licences.

## Dependances identifiees

| Composant | Version | Licence | Sources |
| --- | --- | --- | --- |
| LibreHardwareMonitorLib | 0.9.6 | MPL-2.0 | https://github.com/LibreHardwareMonitor/LibreHardwareMonitor |
| BlackSharp.Core | 1.0.7 | MPL-2.0 | https://github.com/Blacktempel/BlackSharp |
| DiskInfoToolkit | 1.1.2 | MPL-2.0 | https://github.com/Blacktempel/DiskInfoToolkit |
| RAMSPDToolkit-NDD | 1.4.2 | MPL-2.0 | https://github.com/Blacktempel/RAMSPDToolkit |
| HidSharp | 2.6.4 | Apache-2.0 | https://github.com/IntergatedCircuits/HidSharp |
| Mono.Posix.NETStandard | 1.0.0 | A confirmer | https://github.com/mono/mono |
| System.IO.Ports | 10.0.3 | MIT | https://github.com/dotnet/runtime |
| System.Management | 10.0.2 | MIT | https://github.com/dotnet/runtime |
| System.IO.FileSystem.AccessControl | 5.0.0 | MIT | https://github.com/dotnet/runtime |

## Modules PawnIO

LibreHardwareMonitorLib 0.9.6 integre des modules PawnIO.

Le depot PawnIO.Modules est publie sous licence GNU LGPL-2.1 :

https://github.com/namazso/PawnIO.Modules

Sources de la version 0.1.6 :

https://github.com/namazso/PawnIO.Modules/releases/tag/0.1.6

Onze modules integres possedent une correspondance de nom dans les
sources PawnIO.Modules 0.1.6.

Le module IsaBridgeEC.bin est present dans les sources archivees
de LibreHardwareMonitorLib 0.9.6.

Un fichier source IsaBridgeEC.p a ete retrouve dans le depot officiel
PawnIO.Modules. Son historique Git indique une introduction le
2 novembre 2025, apres la publication de PawnIO.Modules 0.1.6
du 13 septembre 2025.

La correspondance exacte entre ce fichier source et le binaire
IsaBridgeEC.bin integre a LibreHardwareMonitorLib 0.9.6
n'est pas encore demontree.

Les correspondances de noms ne garantissent pas l'identite exacte
des binaires compiles.

## Textes des licences

- MPL-2.0 : https://www.mozilla.org/en-US/MPL/2.0/
- LGPL-2.1 : https://www.gnu.org/licenses/old-licenses/lgpl-2.1.html
- Apache-2.0 : https://www.apache.org/licenses/LICENSE-2.0
- MIT : https://opensource.org/license/mit

## Points restant a verifier avant distribution

- Confirmer la licence exacte de Mono.Posix.NETStandard 1.0.0.
- Verifier les mentions de copyright et fichiers NOTICE applicables.
- Inclure les textes de licence requis dans la distribution.
- Verifier les obligations LGPL-2.1 relatives aux modules PawnIO,
  notamment leur modification, remplacement et redistribution.
- Confirmer les droits de redistribution des ressources graphiques.
- Verifier les autres composants effectivement inclus dans le
  paquet Windows final.

Ce document est un inventaire de conformite en cours de finalisation.
Il ne remplace pas les textes de licence des composants concernes.