# CONFIGURATION — Mettre plusieurs ordinateurs en réseau

**Bonne nouvelle : P2PChat est 100 % automatique.** Aucune adresse IP à saisir,
aucun serveur à installer, aucun réglage réseau manuel. Chaque ordinateur
annonce sa présence en broadcast IPv4 et en multicast de secours. Une reconnexion
au réseau relance automatiquement le scan.

## Ce qu'il vous faut

- 2 PC (ou plus) sous **Windows 10/11**
- Connectés au **même réseau** : même box WiFi / même routeur
  (câble Ethernet sur la même box = OK aussi)
- Le dossier `dist\` recopié sur chaque PC (clé USB, dossier partagé, etc.)
- .NET Framework 4.8 (déjà inclus dans Windows 10/11 — rien à installer)

## Étapes (identiques sur chaque ordinateur)

1. **Copier le dossier `dist\`** quelque part sur le PC (ex. `C:\P2PChat`).
2. **Lancer `p2p.exe`** en double-cliquant.
3. À la demande du pare-feu Windows, **cocher « Réseaux privés »** puis Autoriser
   *(si la fenêtre ne s'affiche pas, voir Dépannage ci-dessous)*.
4. Choisir un **pseudo** (≥ 3 caractères) puis « Discuter ».
5. C'est tout ✅ — les autres ordinateurs ayant fait la même chose apparaissent
   automatiquement dans la colonne de gauche.
6. Cliquer sur le nom d'un ordinateur pour ouvrir la discussion et envoyer un message.

## Ports utilisés (information, aucune action requise)

| Port | Usage | Sens |
|---|---|---|
| UDP 54545 | Découverte automatique des pairs (broadcast) | Sortant + entrant |
| TCP 54546 | Sessions chiffrées (messages, vocaux, fichiers) | Sortant + entrant |

## Utiliser plusieurs instances sur le même PC

Chaque lancement sans argument détecte les autres fenêtres P2PChat déjà
ouvertes et crée automatiquement une identité locale séparée et persistante.
Il suffit donc de lancer `p2p.exe` deux fois : chaque fenêtre doit apparaître
dans la colonne de gauche de l'autre.

Pour choisir vous-même les noms des profils de test, ouvrir PowerShell à la
racine du projet et lancer :

```powershell
Start-Process .\dist\p2p.exe -ArgumentList '--profile Alice'
Start-Process .\dist\p2p.exe -ArgumentList '--profile Bob'
```

Les données des profils se trouvent dans `%APPDATA%\P2PChat\instances\Alice`
et `%APPDATA%\P2PChat\instances\Bob`. Les deux fenêtres doivent apparaître
dans la colonne de gauche ; cliquer sur l'une ouvre la discussion.

Règle d'or : **les deux PC doivent pouvoir se voir sur le réseau local.**
C'est le cas dans quasiment toutes les configurations domestiques/scolaires.

## Cas « Réseau public » (important)

Si Windows qualifie le WiFi de « **Public** », le pare-feu bloque les flux entrants.
Pour vérifier :
**Paramètres → Réseau et Internet →WiFi → cliquer sur le réseau → profil « Réseau privé ».**
Sur un réseau public, les PC ne se découvrent pas.

## Mettre 3 PC, 10 PC… ?

Rien à faire de plus : chaque instance découvre **toutes** les autres instances
présentes sur le réseau local. Les deux ports restent identiques.

Si le réseau bloque les annonces UDP, ouvrir « Ajouter un contact » et saisir
l'adresse IP de l'autre PC (port TCP 54546 par défaut). La discussion s'ouvre
une fois la connexion chiffrée établie.

## Dépannage : « Je ne vois pas l'autre personne »

| Vérification | Comment |
|---|---|
| Même réseau ? | Les deux PC sur la même box (SSID WiFi identique) |
| Pare-feu Windows | Panneau de config → Pare-feu → « Autoriser une application… » → cocher **P2PChat** en privé |
| Profil du réseau | Le réseau doit être « Privé » (voir ci-dessus) |
| Antivirus/pare-feu tiers | Ajouter une exception pour P2PChat.exe |
| Isolation client WiFi | Certaines boxes isolent les clients entre eux — désactiver « AP/Client isolation » sur la box |
| Test réseau brut | Sur PC-A : `ping <adresse-du-PC-B>` (ipconfig pour la connaître) |

## Spécifications sur les fonctionnalités

- Le **pseudo** et le statut (« Disponible »…) se règlent à l'écran de connexion.
  L'identifiant interne (GUID) est créé automatiquement et mémorisé sur la machine.
- L'**historique** de chaque discussion est enregistré sur chaque PC dans
  `%APPDATA%\P2PChat\historique\` — indépendant de l'autre ordinateur.
- Les **fichiers reçus** sont rangés dans `%APPDATA%\P2PChat\transfers\`.
- Un message vocal s'enregistre en maintenant le bouton micro (16 kHz, WAV).
- ✓✓ grises = distribué ; ✓✓ **bleues** = lu (l'interlocuteur a affiché le message).
