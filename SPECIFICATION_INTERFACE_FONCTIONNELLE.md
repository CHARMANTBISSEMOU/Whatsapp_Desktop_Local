# P2PChat — Spécification d’interface et de fonctionnalités

Document de passation destiné au développeur qui prendra en charge les pages et les fonctions restantes. Les captures fournies servent de référence de mise en page : rail de navigation étroit à gauche, liste des discussions au centre gauche et panneau principal à droite. L’application doit garder son identité P2PChat et son fonctionnement pair-à-pair local.

## 1. Objectif visuel

Reprendre les repères familiers de WhatsApp Desktop sans reprendre son nom, son logo ou ses ressources de marque. L’interface doit être nette, lisible, cohérente entre les thèmes clair et sombre, et utilisable à la souris comme au clavier.

### Structure persistante

1. **Rail de navigation** (environ 64 px) : icônes Discussions, Appels, Statuts, Communautés, Archives et profil. L’entrée active est visible ; les compteurs non lus apparaissent en badge.
2. **Panneau de liste** (environ 340–420 px, redimensionnable) : titre « Discussions », action nouvelle discussion, menu, champ de recherche, filtres et liste des conversations.
3. **Panneau principal** : accueil lorsque rien n’est sélectionné ; en-tête, fil et zone de saisie lorsqu’une conversation est ouverte.

Le rail et le panneau de liste restent stables pendant la navigation. Les contrôles ne doivent pas chevaucher le texte ni sortir de la fenêtre. Les tailles, marges et icônes sont mises à l’échelle avec le DPI Windows.

### Thèmes

- **Clair** : surfaces principales blanches ou gris chaud très pâle ; texte anthracite ; séparateurs fins ; accent vert réservé aux actions, à l’état actif et aux messages non lus.
- **Sombre** : panneau de liste et rail bleu anthracite, panneau de conversation plus profond, en-têtes et contrôles clairement différenciés, texte clair et contrastes suffisants. Les bulles entrantes et sortantes restent distinctes.
- Les couleurs des textes, icônes, séparateurs, survols, sélections, menus, champs et badges doivent être définies dans un même jeu de tokens. Un changement de thème repeint aussi les contrôles WinForms réels, pas seulement la surface dessinée.
- Éviter un fond illustré dans l’écran d’accueil sans conversation. Réserver le papier peint discret au fil de discussion.

## 2. Pages et exigences fonctionnelles

Les mentions **Déjà présent**, **Partiel** et **À développer** décrivent le code observé dans `src/P2PChat.App`, `src/P2PChat.Core` et `src/P2PChat.Net`.

### Page A — Profil de démarrage

**État : Partiel.** L’écran `LoginScreen` demande un pseudo et une bio/statut ; le profil est mémorisé localement.

- Afficher le pseudo, la bio et un aperçu d’avatar avec initiales ; permettre de modifier le pseudo et la bio.
- Valider le pseudo (3 caractères minimum), expliquer les erreurs sous le champ et conserver les valeurs valides au prochain lancement.
- Proposer une action claire pour entrer dans l’application et une action pour quitter.
- À développer : choisir/importer/supprimer une photo de profil, prévisualiser les changements et contrôler la visibilité du profil.

### Page B — Discussions / boîte de réception

**État : Partiel, fonction de base présente.** `MainForm.Sidebar.cs` dessine recherche, filtres, conversations, aperçus, heures, indicateurs et menus.

- Afficher le titre « Discussions », le profil, le bouton nouvelle discussion et le menu de paramètres.
- Rechercher dans les noms et aperçus ; effacer rapidement la recherche ; afficher un état vide et un état sans résultat distincts.
- Filtres : toutes, non lues, favoris, et menu de filtres additionnels si ces catégories sont ajoutées.
- Trier les conversations épinglées en premier, puis par activité récente. Afficher avatar, nom, aperçu adapté (texte, photo, fichier, vocal), heure, nombre de non-lus, épingle et sourdine.
- Ouvrir une conversation au clic. Au menu contextuel : épingler/désépingler, sourdine, marquer non lue, bloquer/débloquer, supprimer l’historique.
- À développer : vues distinctes « Discussions archivées », gestion des brouillons dans la liste et état de connexion/découverte réseau compréhensible.

### Page C — Accueil sans conversation sélectionnée

**État : Partiel.** Le panneau d’accueil propose des raccourcis ; l’ajout de contact ouvre la connexion par adresse IP et l’invitation copie une consigne.

- Afficher une surface neutre avec un titre court et une explication du fonctionnement local/chiffré.
- Raccourcis : envoyer un document (demander d’abord une conversation), ajouter/connecter un contact, copier une invitation, ouvrir l’assistant P2P quand celui-ci existera.
- Masquer complètement l’en-tête de conversation et la zone de saisie tant qu’aucun échange n’est sélectionné.
- Le clic, le focus clavier et l’état survolé doivent être évidents ; ne jamais présenter un raccourci non implémenté comme opérationnel.

### Page D — Conversation individuelle

**État : Déjà présent pour le socle.** `MainForm.Chat.cs`, `MainForm.ChatExtra.cs`, `MainForm.Composer.cs` et `MainForm.Net.cs` couvrent le fil et son raccordement réseau.

- En-tête : avatar, pseudo, présence/en train d’écrire, recherche dans l’échange et menu d’actions.
- Fil chronologique : séparateurs de date, bulles entrantes/sortantes, heures, états envoyé/distribué/lu/échec, réactions et citations.
- Composeur : texte multiligne, emoji, pièce jointe, envoi et enregistrement vocal ; aperçu de réponse ; retour à la ligne et envoi clavier clairement définis.
- Pièces jointes : images avec aperçu, fichiers avec nom/taille, progression de transfert, résultat du transfert et ouverture locale sûre.
- Actions de message : répondre, copier, réagir, supprimer localement. Les changements de statut doivent être persistés.
- Présent : modifier un message, supprimer pour tous et réactions, avec diffusion réseau sur les conversations individuelles et les groupes.
- À développer : transférer, sélectionner plusieurs messages, rechercher les liens, afficher une progression de remise détaillée et reprendre un transfert interrompu.

### Page E — Recherche

**État : Partiel.** Recherche globale de la liste et recherche locale dans la conversation existent.

- Distinguer recherche de contacts/conversations de recherche dans le fil.
- Afficher les résultats avec contexte et date ; permettre d’ouvrir le résultat à sa position dans le fil.
- Prévoir états chargement, aucun résultat et erreur ; conserver la saisie pendant la navigation.
- À développer : index de recherche persistant sur messages et pièces jointes, avec limites et suppression cohérentes avec l’historique.

### Page F — Nouveau contact / nouvelle conversation

**État : Partiel.** La découverte automatique UDP identifie les pairs du réseau local ; une connexion manuelle IP:port et l’alias local d’un contact sont disponibles.

- Liste des pairs détectés avec pseudo, statut en ligne et action « Discuter » ; recherche et actualisation.
- Formulaire manuel avec adresse/port, validation et erreur de connexion explicite.
- Prévenir les doublons et permettre d’ouvrir directement une conversation existante.
- À développer : favoris de contacts, suppression et fiche contact ; invitations plus simples que la saisie d’IP.

### Page G — Appels

**État : À développer.** L’application sait enregistrer/lire des messages vocaux ; cela ne constitue pas un appel.

- Historique des appels avec contact, date/heure, durée, appel entrant/sortant/manqué et action de rappel.
- Écran d’appel entrant avec accepter/refuser ; écran d’appel actif avec raccrocher, couper/rétablir micro et état de connexion.
- Permissions micro, périphériques audio, gestion de l’absence de microphone et erreurs réseau.
- Audio temps réel chiffré et transport média sont nécessaires avant d’afficher ces commandes comme fonctionnelles. La vidéo est une phase séparée avec aperçu caméra et contrôle caméra.

### Page H — Statuts

**État : Partiel.** La page affiche le profil local et les bios/statuts de profil des pairs ; ce ne sont pas des publications éphémères.

- À développer : liste des statuts récents, vus et muets ; avatar avec anneau de progression et heure de publication.
- Création d’un statut texte/photo/vidéo, aperçu avant publication, durée d’expiration, audience et suppression.
- Visionneuse avec navigation, progression, réponse et liste des vues selon les règles de confidentialité.
- Définir stockage, transport, taille maximale, expiration et confidentialité avant implémentation.

### Page I — Groupes et communautés

**État : Partiel.** Création de groupes, métadonnées, membres et messages de groupe sont présents. Les messages et fichiers sont envoyés individuellement à chaque membre ; la livraison n’est pas atomique et le chiffrement de groupe dédié reste à concevoir. Les communautés ne sont pas implémentées.

- Création de groupe, nom, image, participants et lien/code d’invitation.
- Fil de groupe, auteur par message, réponses/réactions, notifications et compteur de non-lus.
- Fiche du groupe : membres, rôles, ajout/retrait, quitter, paramètres et permissions d’envoi.
- Communautés : regrouper plusieurs groupes, afficher annonces et gérer les rôles administrateurs.
- À développer : image de groupe, lien/code d’invitation, rôles, ajout/retrait, quitter, paramètres et permissions d’envoi ; concevoir une gestion des clés adaptée.

### Page J — Archives

**État : Partiel.** L’archivage et le désarchivage sont persistés et accessibles depuis la navigation et le menu de conversation.

- Lister les échanges archivés, leur activité et leurs non-lus ; ouvrir, désarchiver et archiver depuis le menu.
- Définir si un nouveau message désarchive automatiquement un échange et rendre ce choix cohérent avec les paramètres.
- Persister l’état d’archivage sans supprimer l’historique.

### Page K — Profil et paramètres

**État : Partiel.** L’édition du pseudo/de la bio et les paramètres de thème, taille de police, notifications et accusés de lecture sont disponibles. Les blocages, émojis récents et géométrie de fenêtre sont persistés.

- Profil : avatar, pseudo, bio/statut et aperçu de l’identité diffusée sur le LAN.
- Apparence : système/clair/sombre, taille du texte, fond du fil et aperçu des changements.
- Notifications : son, aperçu, clignotement, réglages par conversation.
- Confidentialité : accusés de lecture, blocages, visibilité du statut ; état clair des limites de sécurité.
- Stockage : emplacement et taille des médias, suppression contrôlée de l’historique, export/sauvegarde.
- À développer : avatar, visibilité du profil, réglages par conversation, sauvegarde/export, confirmations aux opérations irréversibles et affichage des erreurs de sauvegarde.

### Page L — Médias et transferts

**État : Partiel.** Les transferts de fichiers/images et l’enregistrement des vocaux existent ; une galerie de médias d’une conversation est disponible et les reçus sont placés dans `%APPDATA%\P2PChat\transfers`.

- Vue globale des médias par type : photos, vidéos, documents, vocaux ; filtres, tri et ouverture du dossier.
- Afficher taille, date, conversation d’origine, progression, échec et action de réessai.
- À développer : galerie globale, gestion de quota et nettoyage, téléchargement/annulation explicites et reprise après interruption.

## 3. Matrice d’état résumée

| Domaine | État actuel | Priorité suivante |
|---|---|---|
| Rail, liste et accueil | Discussions, archives, profil et navigation vers les pages disponibles | Finir les états et ajuster les tailles/DPI |
| Conversation 1-à-1 | Socle fonctionnel | Fiabiliser transferts, recherche média et actions avancées |
| Appels | Absent (vocaux seulement) | Audio temps réel avant vidéo |
| Statuts | Absent | Modèle, confidentialité, expiration, puis UI |
| Groupes/communautés | Groupes de base ; communautés absentes | Gestion des membres/roles, clés de groupe et fiabilité d’envoi |
| Archives/contacts | Archivage et alias présents | Favoris, suppression et fiche contact |
| Paramètres | Profil et préférences de base | Pages dédiées, accessibilité et gestion des erreurs |
| Sécurité locale/identité | Historique/configuration chiffrés ; clé locale non protégée par DPAPI | Protéger la clé et vérifier durablement les identités |

## 4. Critères d’acceptation visuelle

- À l’ouverture, aucune barre de saisie vide ne flotte au bas du panneau d’accueil.
- Rail, liste et panneau de conversation sont alignés sans chevauchement ; la liste a un titre clairement visible.
- Les rangées de conversations ont des avatars circulaires lisibles, deux lignes de texte et une séparation discrète.
- Recherche et filtres sont alignés, suffisamment espacés et utilisables ; l’état actif ne repose pas uniquement sur la couleur.
- Les états vide, sans résultat, hors ligne, chargement, erreur et aucune conversation sélectionnée sont dessinés séparément.
- Le thème sombre garde une hiérarchie nette entre rail, liste, en-tête, fond du fil, composeur, bulles, menus et texte.
- Redimensionnement, fenêtre étroite et DPI Windows élevé ne coupent ni les libellés ni les actions.
- Toute entrée de navigation non implémentée est marquée comme indisponible/à venir ; elle ne doit pas ouvrir une page factice laissant croire que les données existent.

## 5. Fichiers principaux à connaître

- `src/P2PChat.App/UI/MainForm.cs` : composition de la fenêtre et interactions globales.
- `src/P2PChat.App/UI/MainForm.Layout.cs` : géométrie des panneaux.
- `src/P2PChat.App/UI/MainForm.Navigation.cs` : rail visuel et entrées de navigation.
- `src/P2PChat.App/UI/MainForm.Sidebar.cs` : recherche, filtres et liste des discussions.
- `src/P2PChat.App/UI/MainForm.Chat.cs`, `MainForm.ChatExtra.cs`, `MainForm.Composer.cs` : fil et saisie.
- `src/P2PChat.App/Theme/WaTheme.cs` et `WaWallpaper.cs` : couleurs, typographie et fond du fil.
- `src/P2PChat.App/Settings/AppSettings.cs` : préférences locales.
- `src/P2PChat.Core/Protocol` et `src/P2PChat.Net` : modèles et échanges réseau ; les nouvelles pages réseau doivent étendre ces contrats, pas contourner le moteur.

