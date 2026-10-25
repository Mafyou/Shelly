# ShellyBLE

**Une application Android pour piloter un bouton Bluetooth Shelly BLU Button1 sans l'application officielle, ni compte, ni passerelle.**

> Projet personnel et **non officiel**, issu de rétro-ingénierie. Sans lien avec Shelly / Allterco.

---

## Le projet

Le [Shelly BLU Button1](https://www.amazon.fr/dp/B0BVW7C863) est un petit bouton connecté sur pile. À chaque appui, il diffuse un message Bluetooth ; d'habitude, il faut une passerelle Shelly ou un relais compatible pour en faire quelque chose.

ShellyBLE fait du téléphone ce récepteur :

- **voir les appuis** du bouton en direct (simple, double, triple, long) ;
- **programmer une action** par type d'appui : basculer un relais Shelly, ouvrir un lien (comme un QR code scanné) ou envoyer une requête HTTP ;
- **régler le bouton** directement par Bluetooth : buzzer, mode balise, heure, chiffrement, réinitialisation ;
- **afficher les appuis en très grand** (mode affichage), sans aucun bip.

Tout fonctionne en local : pas de cloud, pas de compte, pas d'Internet.

---

## Ce que fait l'application

| Écran | Usage |
|---|---|
| **Boutons** | Boutons à portée, avec batterie, signal, état (chiffré, mode appairage) et dernier appui. |
| **Fiche › Activité** | Chronologie des appuis et du résultat de chaque action. |
| **Fiche › Programmation** | Le trajet d'un appui (bouton → téléphone → action) et une action par type d'appui. |
| **Fiche › Appareil** | Connexion au bouton pour régler le buzzer et le mode balise, le faire sonner, mettre son horloge à l'heure, activer le chiffrement (code PIN), lire sa clé, le réinitialiser. |
| **Éditeur d'action** | Choix du type d'action, aperçu exact de ce qui sera envoyé, validation en direct et bouton « Tester ». |
| **Mode affichage** | Écran noir plein écran : **CLICK**, **DOUBLE CLICK**, **TRIPLE CLICK** ou **LONG** en énorme, avec un flash à chaque appui ; l'écran reste allumé. Un bouton coupe le buzzer du Shelly. |

Trois types d'actions, du plus simple au plus libre :

| Action | Ce qui est envoyé |
|---|---|
| **Relais Shelly** | Adresse du relais, canal et commande (basculer, allumer, éteindre) : l'application construit l'appel, par exemple `http://192.168.1.50/rpc/Switch.Toggle?id=0` (Gen2+) ou `http://192.168.1.50/relay/0?turn=toggle` (Gen1). |
| **Ouvrir un lien** | Le téléphone ouvre le lien comme s'il venait de scanner un QR code : page web, `tel:`, `mailto:`, `geo:`… |
| **Requête HTTP** | GET ou POST vers n'importe quelle URL, avec un corps (JSON par défaut) pour Home Assistant, Node-RED, un webhook… |

---

## Installation et prise en main

1. Télécharger l'APK depuis les [Releases](../../releases) et l'installer sur un téléphone Android 7 ou plus récent (autoriser l'installation d'applications de sources inconnues).
2. Ouvrir l'application et accepter l'autorisation Bluetooth (« Appareils à proximité ») et les notifications.
3. Appuyer sur le bouton Shelly : il apparaît dans la liste.
4. Pour le régler : le maintenir **plus de 10 secondes** (la LED clignote en bleu), ouvrir sa fiche, « Se connecter », puis accepter la demande d'association d'Android lors du premier réglage.
5. Pour les actions : onglet « Programmation » de la fiche, choisir un type d'appui, puis l'action ; « Tester » l'exécute tout de suite.

Une notification « Écoute des boutons Shelly BLU » reste affichée pendant l'écoute : elle permet à Android de continuer à écouter quand l'écran s'éteint.

---

## État du projet

Testé sur un **OnePlus 7 Pro (Android 17)** avec un BLU Button1 neuf, livré en **firmware 1.0.2** (février 2023).

| Fonctionnalité | État |
|---|---|
| Détection des appuis (simple, double, triple, long) | ✅ |
| Actions à chaque appui (relais, lien, HTTP) | ✅ (~30 ms entre l'appui et l'appel HTTP) |
| Connexion, association, lecture des réglages | ✅ |
| Buzzer, mode balise, faire sonner, horloge | ✅ |
| Chiffrement : activation par PIN, clé, déchiffrement des appuis | ✅ |
| Mode affichage, coupure du buzzer | ✅ |
| Réinitialisation usine | ⚠️ non testée |
| Écran éteint | ⚠️ le téléphone continue d'écouter, mais des appuis peuvent être manqués |
| iPhone, Mac, Windows | ⚠️ prévus mais jamais testés |

---

## Ce que la rétro-ingénierie a révélé

Le bouton n'a pas de mode d'emploi technique complet. Tout ce qui suit a été établi en observant un vrai bouton (captures Bluetooth, lecture de ses caractéristiques) et en recoupant la documentation publique de Shelly et du standard BTHome.

### Ce que le bouton diffuse

- Chaque appui produit un court message au format ouvert **[BTHome v2](https://bthome.io/)** : numéro de paquet, niveau de batterie et type d'appui.
- Le message porte aussi le modèle, des drapeaux d'état (mode appairage, buzzer) et l'adresse du bouton (écrite à l'envers).
- **Batterie** : en mode appairage, le bouton annonce 0 % ; les messages d'appui donnent la vraie valeur.
- **Maintien** : en 1.0.2, un appui maintenu est annoncé comme un appui long ; l'événement « maintien » n'existe qu'à partir de versions plus récentes.
- **Un second émetteur caché** : en parallèle, le bouton diffuse sous une autre adresse un signal nommé « OTA ». C'est la porte d'entrée des mises à jour de firmware ; s'y connecter n'apporte rien et peut bloquer le bouton en mode mise à jour. L'application l'ignore.

### Ce que l'on peut régler

Par connexion Bluetooth, le bouton expose : faire sonner, activer/couper le buzzer, mode balise, horloge, code PIN (qui active le chiffrement), lecture de la clé de chiffrement, réinitialisation usine, version du firmware et un jeton d'identité signé par Shelly (posé en usine, utilisé pour l'enregistrement dans le cloud). Le réglage de la vitesse multi-clic n'existe pas encore en 1.0.2.

### Chiffrement

Une fois un code PIN défini, les appuis sont chiffrés (standard BTHome, AES). L'application lit la clé sur le bouton et déchiffre les appuis. Curiosité : le compteur anti-rejeu des messages chiffrés est simplement l'horloge du bouton.

Ce choix a deux conséquences, observées en 1.0.2 :

- **L'horloge repart de zéro à chaque redémarrage** (changement de code PIN, pile retirée). Les messages sont alors datés de 1970 tant que l'horloge n'a pas été remise à l'heure ; l'application le fait à chaque connexion.
- **Le compteur se fige** : en mode appairage, il vaut toujours 0 ; en mode balise, le message répété toutes les 8 secondes garde le compteur de sa création. Avec la même clé, le même compteur produit le même flux de chiffrement : il a suffi de connaître le contenu, prévisible, d'un message d'appairage pour déchiffrer un double appui émis en mode appairage, **sans la clé**. Le chiffrement de ce firmware protège donc mal les appuis faits en mode appairage ; une raison de plus de mettre le bouton à jour.

### Les caprices du firmware 1.0.2

- Un téléphone non associé qui tente un réglage ne reçoit pas d'erreur : le bouton coupe la connexion. Il faut donc s'associer avant.
- Après un changement de code PIN, le bouton redémarre sans prévenir.
- Sans activité, il coupe la connexion au bout d'une trentaine de secondes.
- Sur ce téléphone (Android 17), le bouton perdait la connexion dès qu'Android ajustait les paramètres de liaison, ce qu'Android fait systématiquement juste après la connexion. Il a fallu trouver un réglage qu'il supporte.

Shelly a corrigé une bonne partie de ces comportements dans les versions suivantes (dernière connue : 1.0.23). **Mettre à jour le bouton avec l'application officielle Shelly reste recommandé** ; cette mise à jour ne peut pas être faite depuis ShellyBLE.

### Les pièges côté Android

- **Localisation coupée** : par défaut, Android cache tous les appareils Bluetooth aux applications. L'application déclare qu'elle n'utilise pas le Bluetooth pour localiser, ce qui lève ce blocage.
- **Écran éteint** : Android n'écoute plus que par intermittence et seulement pour des appareils précis. D'où le filtrage sur les boutons Shelly et la notification permanente ; même ainsi, un appui très bref peut passer entre deux écoutes. Mesuré : environ 3 messages d'appairage sur 4 arrivent écran éteint.
- **Le tri fait par la puce Bluetooth** : le filtre « messages BTHome » est appliqué par la puce elle-même, et sur ce téléphone il laisse passer les messages de balise et d'appairage mais pas ceux des appuis, écran allumé comme éteint. Seul le filtre par adresse les laisse passer : l'application ajoute donc l'adresse de chaque bouton dès qu'elle l'a aperçu une fois (balise ou mode appairage).
- **« Ça marche branché, pas débranché »** : avec l'option développeur « Rester activé », l'écran ne s'éteint jamais quand le téléphone charge. Le problème d'écran éteint n'apparaissait donc qu'une fois débranché.

---

## Limites connues

- Pour une détection sans faille, garder l'écran allumé (mode affichage).
- Les actions ne partent que si l'application tourne sur le téléphone. Un lien ne s'ouvre directement que si l'application est au premier plan ; sinon Android impose de passer par une notification.
- Les actions sont définies par type d'appui pour le premier bouton (les modèles Shelly à plusieurs touches sont détectés, mais leurs touches suivantes n'ont pas d'action).
- Revenir d'un bouton chiffré à un bouton non chiffré demande une réinitialisation usine, puis d'oublier le bouton dans les réglages Bluetooth du téléphone.
- L'APK est signé avec une clé de développement : il s'installe directement, mais n'est pas prêt pour le Play Store.

---

## Pistes

- Mode affichage économe (écran assombri).
- Mise à jour du firmware, si le protocole OTA de Shelly devient documenté.

---

## Pour les développeurs

Le code source (C# 15, .NET 11, .NET MAUI) se compile avec le SDK épinglé par `global.json` ; les tests se lancent avec `dotnet test --project tests/ShellyBLE.Core.Tests`. Tout le protocole (BTHome, chiffrement, configuration Bluetooth, actions) vit dans la bibliothèque `ShellyBLE.Core`, indépendante de l'interface et testée sur des trames réelles anonymisées. Le dossier `tools/` contient un décodeur de captures Bluetooth (btsnoop) et un petit serveur HTTP de test, utilisés pendant la rétro-ingénierie.

---

## Licence

[MIT](LICENSE). Les composants tiers (polices, icônes, bibliothèques) sont listés dans [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).

« Shelly » est une marque de Shelly Group (ex-Allterco Robotics) ; elle n'est citée ici que pour désigner le matériel compatible.

---

## Sources

- Documentation Shelly BLU : [fonctions communes](https://shelly-api-docs.shelly.cloud/docs-ble/common/), [chiffrement](https://shelly-api-docs.shelly.cloud/docs-ble/encryption/), [bouton](https://shelly-api-docs.shelly.cloud/docs-ble/Devices/BLU/button), [historique des firmwares](https://shelly-api-docs.shelly.cloud/docs-ble/changelog)
- Standard BTHome : [format](https://bthome.io/format/), [chiffrement](https://bthome.io/encryption/)
