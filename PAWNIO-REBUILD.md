# Reconstruction des modules PawnIO

## Contexte

Liquid Glass Monitor utilise LibreHardwareMonitor pour lire certaines
informations materielles.

LibreHardwareMonitor integre des modules PawnIO sous forme de ressources
binaires dans LibreHardwareMonitorLib.dll.

Les sources des modules officiels PawnIO.Modules 0.2.2 sont fournies dans :

ThirdParty/PawnIO.Modules-0.2.2/

Les modules embarques sont situes dans :

Dependencies/LibreHardwareMonitor-0.9.6/LibreHardwareMonitorLib/Resources/PawnIo/

## Sources et licences

Les fichiers .p constituent les sources des modules PawnIO.

Les fichiers .inc fournissent des declarations et utilitaires utilises
pendant leur compilation.

Consulter les fichiers COPYING et les notices de licences fournis avec
les sources pour les conditions applicables.

## Compilation des modules

Le workflow officiel de PawnIO.Modules utilise Oracle Linux 9 et le
compilateur pawncc.

La commande de compilation est :

```bash
for f in ./*.p; do
    [ -f "$f" ] && pawncc "$f" '-iinclude' '-C64' '-;+' '-(+' '-p'
done
```

Cette commande produit des fichiers .amx.

Le workflow officiel est disponible dans :

ThirdParty/PawnIO.Modules-0.2.2/.github/workflows/ci.yml

La compilation suppose qu'une version compatible du compilateur Pawn
et les fichiers d'inclusion requis sont disponibles.

## Preparation des fichiers BIN

Les modules embarques dans LibreHardwareMonitor sont des fichiers .bin,
et non des fichiers .amx directement renommes.

L'utilitaire PawnIOUtil fournit une commande de preparation/signature
de la forme :

```powershell
PawnIOUtil.exe sign module.amx module.bin
```

Verifier la version et la documentation de PawnIOUtil avant utilisation.

La creation d'un fichier .bin ne garantit pas son acceptation par le
pilote PawnIO installe. Des restrictions de signature peuvent s'appliquer.

Ne pas desactiver les protections du pilote pour effectuer ces essais.

## Chargement externe dans Liquid Glass Monitor

La version de LibreHardwareMonitor incluse dans ce projet dispose d'un
mecanisme de remplacement experimental des ressources PawnIO.

La variable d'environnement suivante permet de choisir un dossier
contenant des fichiers .bin externes :

LGM_PAWNIO_OVERRIDE_DIR

Exemple PowerShell :

```powershell
$env:LGM_PAWNIO_OVERRIDE_DIR = "C:\PawnIO-Test"
```

L'application recherche alors les modules autorises dans ce dossier
plutot que dans ses ressources integrees.

Attention :

- Le dossier doit contenir les modules demandes par l'application.
- Le chargement est limite aux noms autorises par le code.
- Les fichiers sont transmis au pilote PawnIO.
- Le pilote peut refuser un module dont la signature n'est pas acceptee.
- Les essais doivent etre effectues sur une installation de test.

Pour revenir au comportement normal dans la session PowerShell :

```powershell
Remove-Item Env:LGM_PAWNIO_OVERRIDE_DIR -ErrorAction SilentlyContinue
```

## Reconstruction de LibreHardwareMonitor

Pour remplacer definitivement un module embarque :

1. Produire un fichier .bin compatible et accepte par PawnIO.
2. Sauvegarder la ressource .bin d'origine.
3. Remplacer la ressource correspondante dans Resources/PawnIo/.
4. Recompiler LibreHardwareMonitor et Liquid Glass Monitor.
5. Tester les mesures materielles sur Windows.

Le projet utilise des ressources integrees a la compilation.
Remplacer un fichier a cote de l'executable ne modifie donc pas les
ressources de LibreHardwareMonitorLib.dll.

## Limites connues

Cette documentation decrit la chaine de reconstruction identifiee
dans les sources officielles et dans le projet.

Elle ne garantit pas qu'un module PawnIO modifie pourra etre charge
par un pilote PawnIO standard.

La signature, la compatibilite du format binaire et les exigences du
pilote doivent etre verifiees pour chaque module personnalise.

Les distributions officielles de Liquid Glass Monitor conservent
les modules PawnIO d'origine, deja testes.

## Licence et attribution du compilateur Pawn

Le compilateur utilise pour la reconstruction des modules PawnIO
est distribue sous Apache License 2.0, avec une exception
specifique concernant la liaison statique et dynamique.

Les documents originaux de Pawn 4.1.7152 sont conserves dans :

`ThirdParty/Notices/Pawn-4.1.7152/`

Fichiers conserves :
- `LICENSE` : licence et exception officielle.
- `NOTICE` : attribution et informations sur les contributeurs.

L'archive officielle examinee ne contenait pas le fichier
NOTICES mentionne dans NOTICE.

Les conditions de redistribution doivent etre examinees en tenant
compte du texte integral de la licence et de son exception.
