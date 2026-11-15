# Composants tiers - Liquid Glass Monitor

Liquid Glass Monitor est distribue sous Mozilla Public License 2.0
(MPL-2.0). Les composants tiers conservent leurs licences respectives.

## Dependances identifiees

| Composant | Version | Licence |
| --- | --- | --- |
| LibreHardwareMonitorLib | 0.9.6 | MPL-2.0 |
| BlackSharp.Core | 1.0.7 | MPL-2.0 |
| DiskInfoToolkit | 1.1.2 | MPL-2.0 |
| RAMSPDToolkit-NDD | 1.4.2 | MPL-2.0 |
| HidSharp | 2.6.4 | Apache-2.0 |
| System.IO.Ports | 10.0.3 | MIT |
| System.Management | 10.0.2 | MIT |
| System.IO.FileSystem.AccessControl | 5.0.0 | MIT |
| Microsoft.Win32.Registry | 5.0.0 | MIT |
| System.Threading.AccessControl | 10.0.3 | MIT |
| PawnIO.Modules | 0.2.2 | LGPL-2.1-or-later |

Les fichiers d'inclusion PawnIO portant l'identifiant SPDX 0BSD
conservent leur licence propre.

## Mono.Posix

Les distributions Windows preparees ne contiennent plus :

- Mono.Posix.NETStandard.dll
- MonoPosixHelper.dll
- libMonoPosixHelper.dll

Le retrait a ete teste sur Windows. La reference au paquet
Mono.Posix.NETStandard a egalement ete retiree du projet
LibreHardwareMonitorLib de la preparation GitHub.

## PawnIO.Modules

Les 12 modules PawnIO embarques correspondent aux binaires
officiels de PawnIO.Modules 0.2.2 verifies par SHA256.

Les sources correspondantes sont conservees dans le depot GitHub :

ThirdParty/PawnIO.Modules-0.2.2/

Une archive separee des sources a egalement ete preparee.

La procedure de reconstruction et les limites de signature sont
documentees dans PAWNIO-REBUILD.md.

La disponibilite des sources et de la documentation ne constitue
pas, a elle seule, une validation definitive de conformite LGPL.

## Runtime Microsoft .NET

La distribution autonome embarque des composants Microsoft
.NET Runtime et Windows Desktop Runtime 10.0.14.

Cinq bibliotheques representatives ont ete comparees aux paquets
NuGet officiels 10.0.14 par SHA256 avec correspondance exacte.

La distribution legere requiert une installation compatible
de .NET 10 Desktop Runtime sur le poste utilisateur.

Les documents officiels de licence .NET 10.0.14 sont fournis
dans les dossiers Licences des deux distributions.

## Documents de licence fournis
- DotNet-Runtime-10.0.14-LICENSE.txt
- DotNet-Runtime-10.0.14-THIRD-PARTY-NOTICES.txt
- DotNet-WindowsDesktop-10.0.14-LICENSE.txt
- HidSharp-LICENSE.txt
- LibreHardwareMonitor-LICENSE.txt
- LibreHardwareMonitor-THIRD-PARTY-NOTICES.txt
- LICENSE-MPL-2.0.txt
- Microsoft-NuGet-MIT-LICENSE.txt
- Microsoft.Win32.Registry-LICENSE.txt
- Microsoft.Win32.Registry-THIRD-PARTY-NOTICES.txt
- PawnIO-Embedded-COPYING.txt
- PawnIO-Modules-COPYING.txt
- System.IO.FileSystem.AccessControl-LICENSE.txt
- System.IO.FileSystem.AccessControl-THIRD-PARTY-NOTICES.txt
- System.IO.Ports-THIRD-PARTY-NOTICES.txt
- System.Management-THIRD-PARTY-NOTICES.txt
- System.Threading.AccessControl-THIRD-PARTY-NOTICES.txt

## Points restant a verifier avant publication

- Verifier les ressources graphiques integrees aux binaires.
- Verifier les composants effectivement redistribues.
- Confirmer les obligations LGPL applicables aux modules PawnIO,
  notamment les conditions pratiques de modification et remplacement.
- Verifier les mentions de copyright et les notices applicables.

Ce document est un inventaire de conformite en cours de finalisation.
Il ne remplace pas les textes de licence originaux.

### System.Memory 4.6.3

- Licence : MIT
- Projet : https://github.com/dotnet/maintenance-packages
- Licence : https://licenses.nuget.org/MIT
- Composant present dans la distribution Windows autonome.
- Texte de licence : `Licences/System.Memory-MIT-LICENSE.txt`.

### Microsoft.Windows.CsWin32 0.3.269

- Licence declaree par NuGet : MIT.
- Projet : https://github.com/Microsoft/CsWin32
- NOTICE original conserve integralement :
  `ThirdParty/Notices/Microsoft.Windows.CsWin32-0.3.269-NOTICE.txt`.
- Ce composant est utilise comme outil de generation de code
  pendant la compilation de LibreHardwareMonitorLib.

### Pawn compiler 4.1.7152

- Copyright : CompuPhase, 1997-2023.
- Licence declaree : Apache License 2.0.
- Exception specifique autorisant certaines formes de liaison statique et dynamique.
- Attribution et informations sur les contributeurs conservees dans le document officiel `ThirdParty/Notices/Pawn-4.1.7152/NOTICE`.
- Texte integral de la licence : `ThirdParty/Notices/Pawn-4.1.7152/LICENSE`.
- Version examinee : 4.1.7152, release 1.el9.
- Source officielle : https://www.compuphase.com/pawn/pawn.htm

Le document NOTICE reference un fichier NOTICES supplementaire.
Ce fichier n'etait pas present dans l'archive officielle 4.1.7152 examinee.
Aucune copie de ce document n'a ete creee artificiellement.
