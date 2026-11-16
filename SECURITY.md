# SECURITY — Modèle de sécurité de P2PChat

Projet scolaire : les choix ci-dessous visent un **niveau de sécurité réel et
pédagogique**, avec des limites documentées honnêtement (section « Limites »).

## 1. Chiffrement des échanges

Chaque paire de PC qui discute établit une **session chiffrée** :

1. **Handshake ECDH P-256** (Elliptic Curve Diffie-Hellman, courbe NIST P-256)
   - Chaque instance génère une paire de clés **éphémère** à chaque connexion TCP.
   - Les clés publiques sont échangées lors du handshake TCP.
   - Le secret partagé n'est jamais transmis sur le réseau.
2. **Dérivation déterministe** : la clé de session AES et la clé de HMAC sont
   dérivées du secret partagé par SHA-256 de domaine séparé
   (`p2pchat-enc` / `p2pchat-mac`).
3. **Chiffrement AEAD-like « encrypt-then-MAC »** :
   - Trame = `[IV aléatoire 16 o] [AES-256-CBC + padding PKCS7] [HMAC-SHA256 (IV||chiffré)]`
   - Le **MAC est vérifié AVANT tout déchiffrement** : une trame falsifiée ou
     rejouée sur une autre session est rejetée (CryptographicException).
   - Comparaison du MAC **en temps constant** (anti-analyse temporelle).
   - IV **nouveau à chaque message** (jamais réutilisé avec la même clé).

Conséquence : sur le WiFi/le hub/routeur local, un sniffeur (Wireshark, etc.)
ne voit que du trafic chiffré indéchiffrable sans les clés de session.

## 2. Découverte UDP

Les annonces de présence (id, pseudo, statut) partent en **broadcast UDP non
chiffré** — c'est leur but (être vues par tous). Contenu non sensible : pas de
message, pas de fichier, pas de clé privée. Les messages eux-mêmes ne transitent
**jamais** par UDP.

## 3. Garde-fous réseau (anti-DoS/débordement)

- Longueur de trame annoncée plafonnée à **8 Mo** (les longueurs aberrantes sont rejetées).
- Profondeur d'imbrication JSON limitée à **128**.
- Accusés de frappe (« en train d'écrire ») limités à **1 trame/2,5 s** (anti-spam).
- Chaque trame reçue est authentifiée par HMAC avant tout traitement.
- Fichiers reçus écrits uniquement dans le dossier `transfers` de l'application,
  jamais à l'extérieur.

## 4. Données locales

- `%APPDATA%\P2PChat\profile.json`, `settings.json` et `historique\*.jsonl` : chiffrés localement avec AES-256-CBC + HMAC-SHA256 (`LocalCrypto`). Les lignes historiques non chiffrées existantes restent lisibles pour la migration.
- `%APPDATA%\P2PChat\secrets.key` : clé maîtresse aléatoire de 32 octets encodée en Base64.
- `%APPDATA%\P2PChat\transfers\` : fichiers/vocaux reçus.

Limite : `secrets.key` est stockée dans le même dossier utilisateur que les données, sans protection DPAPI. Le chiffrement protège les fichiers isolés, mais pas contre un compte Windows capable de lire tout le dossier et donc la clé. La protection DPAPI et l’effacement de la clé en mémoire restent à développer.

## 5. Limites assumées (projet scolaire)

- Pas de **vérification d'identité à long terme** (type TOFU/empreinte de clé
  permanente) : une machine de l'extérieur qui s'introduit sur le LAN peut ouvrir
  une session — elle sera chiffrée, mais pas authentifiée moralement.
- Les clés réseau sont éphémères par session. Les fichiers locaux sont chiffrés,
  mais la clé maîtresse n’est pas protégée contre l’accès au dossier utilisateur complet.
- Pas de protection contre un correspondant malveillant (captures d'écran,
  recopie de contenu) — impossible par conception.
- Pas de traversée Internet/NAT : le périmètre est le réseau local de confiance.

## 6. Bonnes pratiques retenues dans le code

- Aucune dépendance cryptographique externe : uniquement `System.Security.Cryptography` (Windows CNG).
- Clés privées jamais sérialisées, jamais journalisées.
- Échecs de déchiffrement = trame rejetée silencieusement côté moteur (journalisation.
- Entrées réseau traitées comme non fiables : tailles validées, types vérifiés
  par des `FromDict` défensifs (retour null si invalide).
