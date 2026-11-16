# P2PChat — Messagerie Desktop P2P style WhatsApp

Projet scolaire C#/.NET Framework 4.8 (COURS KEYCE — B2, Semestre 1, Programmation C#).
Application Windows de messagerie **pair-à-pair sur réseau local** (LAN/WiFi), avec
l'expérience visuelle de WhatsApp : bulles, coches de réception, messages vocaux,
transfert de fichiers, émojis, chiffrement intégré.

> ⚠️ **Note importante** : ce logiciel est un projet pédagogique indépendant.
> Il n'est ni affilié à WhatsApp™ ni à Meta, ne se connecte à aucun serveur WhatsApp,
> et n'utilise pas ses protocoles. Le logo/l'apparence sont utilisés à des fins
> d'imitation visuelle dans un cadre strictement scolaire.

## Fonctionnalités

- 💬 Messages texte en temps réel, emojis intégrés
- 🎤 Messages vocaux (enregistrement micro → bulle lecture)
- 📎 Transfert de fichiers et d'images (glisser dans la discussion)
- ✓ / ✓✓ / ✓✓ bleues : envoyé, distribué, lu
- ✍️ Indicateur « en train d'écrire… »
- 🟢 Présence en ligne automatique (découverte sans aucune configuration)
- 👥 Groupes P2P (diffusion chiffrée individuelle aux membres)
- 📥 Archivage local, alias de contacts et recherche dans les discussions
- 💾 Historique persistant chiffré par correspondant : éditions, réactions, suppressions et accusés conservés au redémarrage
- 🖼️ Galerie des médias et fichiers de la conversation
- ⚙️ Réglages de thème, taille du texte, notifications, accusés et diagnostics réseau
- 🔔 Notifications sonores + clignotement en barre des tâches
- 🔒 Chiffrement de session ECDH P-256 + AES-256 + HMAC-SHA256 (voir `SECURITY.md`)
- 🎨 Interface aux couleurs et codes visuels de WhatsApp

## Architecture (4 projets)

| Projet | Rôle |
|---|---|
| `P2PChat.Core` | Protocole, modèles, MiniJson (JSON maison), chiffrement |
| `P2PChat.Net` | Découverte UDP automatique + sessions TCP chiffrées + transfert fichiers |
| `P2PChat.App` | Interface WinForms style WhatsApp (.point d'entrée) |
| `P2PChat.Tests` | Console de tests : JSON, crypto, protocole, réseau local |

Ports réseau : **UDP 54545** (découverte), **TCP 54546** (sessions chiffrées).

## Compiler

Prérequis : Windows 10/11 avec .NET Framework 4.8 (inclus par défaut) et Visual Studio 2022
(charge de travail « Développement .NET Desktop »).

Deux options :

1. **Script** : prépare les outils une fois, puis compile :
   ```
   powershell -NoProfile -ExecutionPolicy Bypass -File tools\setup-tools.ps1
   powershell -NoProfile -ExecutionPolicy Bypass -File build\build.ps1
   ```
   Le script télécharge Roslyn et les références .NET 4.8 depuis NuGet, puis
   les extrait localement. Une connexion Internet est nécessaire pour cette
   première préparation.

2. **Visual Studio 2022** : ouvrir `P2PChat.sln` → Build → Générer la solution.
   *(Nécessite la charge de travail « Développement .NET Desktop » installée
   dans Visual Studio.)*

L'application autonome finale : `dist\p2p.exe`. Les composants Core, réseau
et interface sont compilés dans cet unique exécutable ; Windows doit disposer
de .NET Framework 4.8.

## Lancer et tester

1. Lancer `p2p.exe`, choisir un pseudo (≥ 3 caractères), « Discuter ».
2. Pour tester seul sur un PC, ouvrir deux instances avec des profils distincts
   depuis PowerShell, dans le dossier du projet :
   ```powershell
   Start-Process .\dist\p2p.exe -ArgumentList '--profile Alice'
   Start-Process .\dist\p2p.exe -ArgumentList '--profile Bob'
   ```
   Chaque profil choisit son pseudo et possède son identité, ses paramètres et
   son historique séparés. Les deux instances se découvrent sur le réseau local.

Pour relier **plusieurs ordinateurs**, suivre `CONFIGURATION.md` (c'est très simple :
même WiFi + autoriser l'application dans le pare-feu).

## Équipe (projet mené en équipe d'agents)

- **Lead/ChefEquipe** : architecture, contrats, intégration, build, tests, docs
- **dev-core** : chiffrement (ECDH/AES/HMAC) + tests crypto
- **dev-net** : moteur réseau P2P (UDP/TCP chiffré, fichiers)
- **dev-ui** : interface style WhatsApp
- **dev-features** : vocaux, historique, notifications, émojis

## Limites (périmètre v1)

- P2P **local uniquement** (LAN/WiFi) — pas de traversée Internet/NAT ni relais serveur
- Pas de comptes serveur : une instance = une identité locale par machine
- Appels audio/vidéo et statuts éphémères avec médias non implémentés
- Communautés et rôles avancés de groupe non implémentés
- La clé des données locales est stockée dans le dossier P2PChat ; le chiffrement local ne protège pas contre un compte Windows qui peut lire l’ensemble de ce dossier (voir `SECURITY.md`)
