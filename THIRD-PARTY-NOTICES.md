# Composants tiers

ShellyBLE est distribué sous licence MIT (voir [LICENSE](LICENSE)). Il intègre ou utilise les composants
tiers ci-dessous, chacun sous sa propre licence. Les textes complets des licences sont accessibles aux
liens indiqués.

## Embarqués dans l'application

### Material Design Icons (police)

- Fichier : `src/ShellyBLE/Resources/Fonts/MaterialDesignIcons.ttf`
  (copie inchangée de `fonts/materialdesignicons-webfont.ttf` du paquet npm `@mdi/font` 7.4.47)
- Auteur : [Pictogrammers](https://pictogrammers.com/) — https://github.com/Templarian/MaterialDesign-Webfont
- Licence : « Pictogrammers Free License » ; les polices sont distribuées sous
  [Apache License 2.0](https://www.apache.org/licenses/LICENSE-2.0)

### Open Sans (polices)

- Fichiers : `src/ShellyBLE/Resources/Fonts/OpenSans-Regular.ttf`, `OpenSans-Semibold.ttf`
  (version 1.10, fournies par le modèle de projet .NET MAUI)
- Copyright : Digitized data copyright © 2010-2011, Google Corporation. Conception : Ascender Corporation.
  « Open Sans » est une marque de Google.
- Licence : [Apache License 2.0](https://www.apache.org/licenses/LICENSE-2.0) (licence déclarée dans les
  métadonnées des fichiers ; les versions plus récentes d'Open Sans sont sous SIL OFL 1.1)

### Bibliothèques NuGet

| Composant | Auteur | Licence |
| --- | --- | --- |
| [.NET MAUI](https://github.com/dotnet/maui) (`Microsoft.Maui.Controls`) et modèle de projet | .NET Foundation et contributeurs | MIT |
| [Microsoft.Extensions.Logging.Debug](https://github.com/dotnet/runtime) | .NET Foundation et contributeurs | MIT |
| [CommunityToolkit.Maui](https://github.com/CommunityToolkit/Maui) | .NET Foundation et contributeurs | MIT |
| [CommunityToolkit.Mvvm](https://github.com/CommunityToolkit/dotnet) | .NET Foundation et contributeurs | MIT |
| [Plugin.BLE](https://github.com/dotnet-bluetooth-le/dotnet-bluetooth-le) | contributeurs dotnet-bluetooth-le | [Apache License 2.0](https://www.apache.org/licenses/LICENSE-2.0) |

## Utilisés uniquement pour les tests (non distribués)

| Composant | Licence |
| --- | --- |
| [xUnit.net v3](https://github.com/xunit/xunit) (`xunit.v3`, `xunit.runner.visualstudio`) | Apache License 2.0 |
| [Shouldly](https://github.com/shouldly/shouldly) | BSD-3-Clause |
| [Moq](https://github.com/devlooped/moq) | BSD-3-Clause |
| [Microsoft.NET.Test.Sdk](https://github.com/microsoft/vstest) | MIT |

## Marques

« Shelly » et « Shelly BLU » sont des marques de Shelly Group (anciennement Allterco Robotics) ou de ses
filiales. ShellyBLE est un projet indépendant et non officiel : il n'est ni affilié à Shelly Group, ni
approuvé, ni soutenu par elle. Les noms de marque ne sont cités que pour désigner les appareils compatibles.

---

# Third-party components (English summary)

ShellyBLE is MIT-licensed. It ships the Material Design Icons font (Pictogrammers, fonts under Apache-2.0,
from `@mdi/font` 7.4.47), the Open Sans 1.10 fonts from the .NET MAUI template (Apache-2.0, per the font
metadata), and uses .NET MAUI, Microsoft.Extensions.Logging.Debug, CommunityToolkit.Maui and
CommunityToolkit.Mvvm (MIT) and Plugin.BLE (Apache-2.0). Tests only: xUnit.net v3 (Apache-2.0), Shouldly and
Moq (BSD-3-Clause), Microsoft.NET.Test.Sdk (MIT). "Shelly" and "Shelly BLU" are trademarks of Shelly Group
(formerly Allterco Robotics); this project is not affiliated with or endorsed by Shelly Group.
