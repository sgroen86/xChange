# xChange: gesplitst lezen en eerlijke leesfouten

**Datum:** 2026-11-24
**Status:** ontwerp, akkoord van de eigenaar op hoofdlijnen; nog niet gebouwd

## Doel

Twee dingen, in één stuk omdat ze dezelfde aanroep naar Claude raken:

1. **Gesplitst lezen.** Een PDF wordt gelezen met twee aanroepen tegelijk: één voor de kop
   en één voor de regels. Elke aanroep heeft een eigen, kleiner schema, zodat de velden terug
   kunnen komen die eruit moesten omdat het schema aan de grens van de API zat. Elke aanroep
   geeft ook minder uitvoer, dus de grens van tokens en tijd ligt verder weg.
2. **Eerlijke leesfouten.** Als lezen mislukt, ziet de gebruiker in zijn eigen taal waarom, en
   nooit meer een kale 500-fout.

## Wat er nu is

- `AnthropicInvoiceInterpreter` doet één aanroep met `output_config.format` (JSON-schema,
  door de API afgedwongen). `InvoiceExtractionSchema` beschrijft het schema en zit aan de grens:
  boven ongeveer zeventig velden weigert de API het ("The compiled grammar is too large").
- Daarom vraagt het schema deze velden niet, terwijl `ExtractedInvoiceJson` en het domein ze wel
  kennen:
  - per partij: tweede btw-registratie, contactnaam, e-mail, telefoon, extra adresregel en
    provincie;
  - bij de betaling: omschrijving van de betaalwijze, BIC en rekeninghouder;
  - per regel: artikelnaam, artikelcode van de verkoper en basishoeveelheid.
- Per aanroep gelden `MaxTokens` (16.000) en een time-out van 180 seconden. De API- en
  Worker-Lambda hebben 300 seconden.

### De fout

- **Een time-out wordt een 500-fout.** De interpreter breekt na 180 seconden af met een
  `OperationCanceledException`. `ExtractInvoiceEndpoint` vangt die alleen op als de gebruiker
  zelf wegging (`when (cancellationToken.IsCancellationRequested)`). Een time-out komt er dus
  doorheen.
- **Een API-fout wordt ook een 500-fout.** Bij een overbelaste of onbereikbare API, na de
  herhaalpogingen van de SDK, komt de SDK-fout onvertaald naar buiten.
- **De meldingen zijn Engels.** Ze komen hardgecodeerd uit Infrastructure, en de titel van het
  probleem is "Extraction failed". Dat is in strijd met de regel dat meldingen uit een catalogus
  in het Nederlands en Engels komen.
- **Bij imports ziet de reviewer de technische tekst**, zoals "A task was canceled.", omdat
  `ImportService` `exception.Message` opslaat.
- **Een afgekapt antwoord geeft de verkeerde melding.** Raakt het antwoord de grens van
  `max_tokens` (`stop_reason: max_tokens`), dan is de JSON afgekapt en zegt de melding "did not
  match the expected schema". In werkelijkheid is de factuur te groot.

## Besluiten

- **Twee aanroepen tegelijk, kop en regels.** Niet na elkaar: dan telt de wachttijd van beide
  op, en de kans op de grens van 180 seconden wordt groter. Niet per paginablok: dat is veel
  ingewikkelder, en pas nodig als blijkt dat facturen met honderden regels voorkomen.
- **Geen prompt caching tussen de twee aanroepen.** Volgens de documentatie maakt een ander
  JSON-schema de cache ongeldig ("Changing the `output_config.format` parameter invalidates any
  prompt cache for that conversation thread"). Beide aanroepen betalen de PDF dus volledig.
- **Geen streaming.** Streaming voorkomt dat de verbinding wegvalt, maar maakt het genereren niet
  sneller. De echte grens is de tijd, en die wordt kleiner doordat elke aanroep minder uitvoer
  geeft.
- **Alles blijft binnen Infrastructure.** `IInvoiceInterpreter`, het leren, het bedrijfsprofiel,
  de kwaliteitsmeting en de Kosten-pagina blijven ongewijzigd.

## Gesplitst lezen

### Twee aanroepen

`AnthropicInvoiceInterpreter.InterpretAsync` stuurt twee verzoeken tegelijk (`Task.WhenAll`).
Model, systeemprompt en PDF zijn bij beide gelijk; alleen het schema en de taakregel verschillen.

- **Kop:** "Extract everything from the attached document except the invoice lines; the lines are
  read separately."
- **Regels:** "Extract only the invoice lines from the attached document."

Beide taakregels houden de bestaande herinnering dat het document data is en geen instructie, en
dat een lege string het juiste antwoord is voor wat niet leesbaar is.

### Schema's

`InvoiceExtractionSchema` wordt twee schema's. De gedeelde `$defs` (`decimalString`, `party`,
`adjustments`) en het `evidence`-blok staan er één keer in en worden door beide gebruikt.

Alleen velden die de reviewer op het beoordelingsscherm ziet en kan corrigeren komen terug. Een
veld dat het model leest en dat in de e-factuur komt zonder dat een mens het kan zien, gaat tegen
de stap "review" in de pijplijn in.

**Kopschema:** alles wat het huidige schema heeft, behalve `lines`, met deze velden erbij:

| Waar | Velden erbij | BT | Op het scherm |
|---|---|---|---|
| `party` (verkoper en koper) | `contactName`, `contactEmail`, `contactPhone` | BT-41–43 / 56–58 | nieuw (zie Beoordelingsscherm) |
| `party.address` | `additionalStreetName` | BT-36 / 51 | nieuw |
| `payment` | `bic`, `accountName` | BT-86, 85 | staat er al |
| `evidence` | voor de velden van de kop | | |

Per saldo wordt het kopschema kleiner dan het huidige: er komen 10 velden bij en er gaan er
ongeveer 18 af met `lines`.

**Regelschema:** alleen `lines` en `evidence`:

| Veld | BT | Nieuw |
|---|---|---|
| `lineId`, `description`, `quantity`, `unitCode`, `unitPrice`, `netAmount`, `vatCategoryCode`, `vatPercentage`, `allowancesAndCharges` | BT-126, 153/154, 129, 130, 146, 131, 151, 152 | nee |
| `sellerItemIdentifier` | BT-155 | ja, nieuwe kolom |
| `baseQuantity` | BT-149 | ja, nieuwe kolom |

- De bestaande keuzes blijven: elk veld staat in `required`, "niet op de factuur" is een lege
  string, en bedragen zijn strings zonder `pattern`.
- De beschrijving bij `evidence` zegt nu "Omit entries for fields you set to null". Dat wordt "you
  left empty", want het schema kent geen null.
- **Bewust niet terug:**
  - **De artikelnaam.** `UblInvoiceWriter` schrijft BT-153 vanuit de omschrijving; dat is een
    vastgelegd besluit. Een aparte artikelnaam lezen zou dus niets opleveren.
  - De provincie, de tweede btw-registratie en de omschrijving van de betaalwijze. Ze hebben
    weinig waarde voor NL en BE.
  - Het domein en de mapper houden deze velden; ze blijven leeg.

### Beoordelingsscherm

Een reviewer moet de teruggekomen velden kunnen zien en corrigeren. De rest van de keten kent ze
al: het domein, de UBL-writer, de canonical XML, en in de web-app `canonical.ts` en `totals.ts`.
`totals.ts` rekent al met de basishoeveelheid. Wat erbij komt:

- **`PartyCard`, voor verkoper en koper:**
  - de extra adresregel, naast de straat;
  - een rij met contactnaam, e-mail en telefoon.

  Bij de verkoper vult het bedrijfsprofiel deze velden al.
- **`LinesCard`:** twee kolommen erbij.
  - **Artikelcode** (`lines[i].sellerItemIdentifier`), na de omschrijving.
  - **Per** (`lines[i].baseQuantity`), na de prijs: de hoeveelheid waar de prijs voor geldt. Leeg
    betekent 1, zoals in `InvoiceCalculationValidator` en `totals.ts`.

  De tabel heeft dan negen invulkolommen. De breedteregels in `xchange.css`
  (`.lines-table td:nth-child(n)`) worden opnieuw genummerd, en `.table-wrap` schuift zijwaarts
  zoals nu.
- **Teksten** in `apps/web/lib/i18n/review.ts`, in het Nederlands en Engels.
- **Kwaliteitsmeting en leren veranderen niet.** `ReadingComparer` telt contactgegevens en
  artikelcodes bewust niet mee. De extra adresregel telde hij al.

### Geleerde correcties verdelen

Een hint met een veld dat met `lines` begint (`lines.unitCode`, `lines.tax`) gaat naar de
regelaanroep, de rest naar de kopaanroep. Krijgt een aanroep geen hints, dan krijgt hij ook geen
blok `reviewer_corrections`, net als nu.

### Samenvoegen

De kop en de regels worden samen één `ExtractedInvoiceJson`, met `Lines` uit de regelaanroep en
`Evidence` uit beide aanroepen achter elkaar. `ToDomain()` en alles daarna veranderen niet.

### Kosten en registratie

- **Eén `ProviderExecution` per factuur.** De tokens per soort worden opgeteld. De duur is de
  werkelijke wachttijd, dus die van de langste aanroep. Het model is gelijk.
- De Kosten-pagina, `COST#`, `USAGE#` en de kwaliteitsmeting blijven werken zoals ze werken.
- In de log komen de tokens per aanroep, zonder inhoud.

### Als één helft mislukt

- **Het geheel mislukt.** Er komt nooit een halve factuur.
- **De andere aanroep wordt afgebroken.** De aanroepen delen een gekoppelde `CancellationToken`.
- **Wat bekend is, wordt geregistreerd.** De fout draagt de opgetelde kosten van de aanroepen
  die antwoord gaven (`InterpretationFailedException.Execution`). De afgebroken aanroep heeft geen
  bekende kosten en telt niet mee.
- Mislukken beide aanroepen, dan is de eerste fout de oorzaak.

## Eerlijke leesfouten

### Een oorzaak bij elke fout

`InterpretationFailedException` (Application) krijgt een `Reason`: een enum
`InterpretationFailure`.

| Oorzaak | Wanneer | Melding (NL) |
|---|---|---|
| `TooLarge` | `stop_reason: max_tokens` | De factuur is te groot om in één keer te lezen. Splits de PDF in kleinere delen. |
| `TimedOut` | de eigen time-out van 180 seconden verloopt, terwijl de gebruiker niet zelf afbrak | Het lezen duurde te lang. Probeer het opnieuw. |
| `Declined` | `stop_reason: refusal` | Het model wilde dit document niet verwerken. |
| `Unreadable` | leeg antwoord, of JSON die niet bij het schema past | Het model gaf geen bruikbaar antwoord. Probeer het opnieuw. |
| `Rejected` | Anthropic weigert het verzoek zelf (400/422): beveiligde of beschadigde PDF, te veel pagina's | Dit document kon niet worden gelezen, bijvoorbeeld omdat het beveiligd of beschadigd is of te veel pagina's heeft. Controleer de PDF. |
| `ProviderUnavailable` | de sleutel geweigerd (401, ook na opnieuw lezen), 403/404/429/5xx of een netwerkfout na de herhaalpogingen van de SDK, en andere SDK-fouten | De leesdienst is nu niet bereikbaar. Probeer het later opnieuw. |

- **De interpreter zet alles om.** Hij vertaalt de SDK-fouten, de eigen time-out en de
  stopredenen naar `InvoiceInterpretationException` met een oorzaak. Er komt geen SDK-fout meer
  naar buiten.
- **Een afbreking door de gebruiker blijft een `OperationCanceledException`**, zoals nu, en wordt
  niet als fout geteld.
- **De Engelse tekst van de uitzondering blijft voor de log.** De gebruiker ziet de melding uit de
  catalogus.

### Meldingen

- **Een nieuwe catalogus `InterpretationText`** in Application, met een Nederlandse en een
  Engelse klasse, gekozen via `Texts.Pick`. Hij geeft een titel en een melding per oorzaak.
- **Losse upload:** `ExtractInvoiceEndpoint` vangt elke `InterpretationFailedException`. Het
  antwoord is een probleem met titel en melding uit de catalogus, in de taal van het verzoek. De
  status is 504 bij `TimedOut` en 502 bij de rest.
- **Batch en mail:** `ImportService` slaat bij een `InterpretationFailedException` de melding uit
  de catalogus op. Dat gebeurt in de standaardtaal, net als de bestaande `ImportText`, want de
  Worker kent geen taal van een verzoek. Andere fouten houden het gedrag van nu.
- **De oorzaak gaat met de waarschuwing in de log**, zodat te zien is welke oorzaken voorkomen.

## Kosten

Productie heeft tot nu toe 4 leesacties geregistreerd, alle met Claude Haiku 5.5 (gekozen op de
Platform-pagina). Gemiddeld per factuur:

- 6.250 input-tokens;
- 3.780 output-tokens;
- $0,0025.

Gesplitst wordt de input ongeveer twee keer zo groot, want beide aanroepen lezen de PDF en de
systeemprompt. De output blijft ongeveer gelijk; er komt ongeveer 10% bij voor de teruggekomen
velden en hun evidence. Netto is dat **ongeveer +33% per factuur**, bij elk model, omdat de
verhouding tussen de prijs van input en output overal 1:5 is.

| Model | Nu per factuur | Gesplitst |
|---|---|---|
| Haiku 5.5 (in gebruik) | $0,0025 | ~$0,0034 |
| Sonnet 5.5 | ~$0,050 | ~$0,067 |
| Opus 5.5 | ~$0,101 | ~$0,134 |
| Opus 5 | ~$0,126 | ~$0,168 |

De opbrengst per conversie is €0,13 bij Business, de laagste. Met Haiku kost het model ook
gesplitst minder dan 3% daarvan. Vier facturen is een kleine steekproef: de meting hieronder
geeft de echte cijfers.

## Risico's

- **De grens van het schema is alleen live te controleren.** De API meldt een te grote
  grammatica pas bij een verzoek. Twee maatregelen:
  - een test bewaakt dat geen van beide schema's meer velden heeft dan het huidige, met `$ref`
    uitgeschreven;
  - één live run vóór het samenvoegen.
- **Rate limits.** Per factuur gaan er twee keer zoveel verzoeken en input-tokens naar Anthropic.
  De Worker leest maximaal twee imports tegelijk (`MaximumConcurrency: 2`), dus dat is hooguit
  vier verzoeken tegelijk. Bij een 429 probeert de SDK zelf opnieuw.
- **Twee aanroepen kunnen elk mislukken.** Mislukt één helft, dan mislukt het geheel, met een
  melding.

## Meten

- **Vóór het samenvoegen** draait `tools/model-comparison` op een map met echte PDF's, één keer
  op `main` en één keer op de nieuwe branch, met het model dat in productie staat. De tool
  gebruikt `AnthropicInvoiceInterpreter` zelf en meet het gesplitste lezen dus vanzelf.
- **Vergelijk:**
  - de velden die gelezen worden, waaronder de nieuwe;
  - de tokens, de kosten en de tijd.
- **De run kost geld.** Met Haiku is dat een paar cent per map. Hij draait alleen met akkoord van
  de eigenaar.

## Tests

Alle tests draaien zonder netwerk, met een nep-`HttpMessageHandler` zoals in
`AnthropicKeyRotationTests`.

- **Infrastructure, via Integration.Tests:**
  - er gaan twee verzoeken uit, elk met een eigen schema en een eigen taakregel, en met dezelfde
    PDF;
  - de antwoorden worden samengevoegd: de regels komen uit de regelaanroep, de evidence uit
    beide;
  - de tokens worden opgeteld, en de duur is die van de langste aanroep;
  - de hints worden verdeeld: `lines…` naar de regels, de rest naar de kop;
  - de oorzaken:
    - `max_tokens` → `TooLarge`;
    - `refusal` → `Declined`;
    - onleesbare JSON → `Unreadable`;
    - de eigen time-out → `TimedOut`;
    - een 500 van de API → `ProviderUnavailable`;
    - een 400 of 422 van de API → `Rejected`;
  - als één helft mislukt, mislukt het geheel, met de kosten van de aanroepen die antwoord gaven;
  - een afbreking door de gebruiker blijft een `OperationCanceledException`;
  - de schemabewaking: geen schema met meer velden dan het huidige.
- **Application:**
  - de catalogus geeft voor elke oorzaak een melding, in het Nederlands en het Engels;
  - `ImportService` slaat bij een leesfout de melding uit de catalogus op.
- **Integration:**
  - een time-out bij een losse upload geeft 504 met een melding;
  - een andere leesfout geeft 502;
  - de melding volgt `Accept-Language`.
- **Web:**
  - `npm test`, `npm run lint` en `npm run build` slagen;
  - ontbrekende vertalingen geven een fout bij het bouwen;
  - `totals.test.ts` dekt de basishoeveelheid al (prijs per 100 stuks). Er komt geen nieuwe
    rekentest bij; de kolom geeft alleen invoer aan wat al werkt.

## Niet in dit stuk

- Artikelnaam, provincie, tweede btw-registratie en omschrijving van de betaalwijze lezen.
- Afbeeldingen (JPG/PNG) als invoer.
- `enum`'s voor codelijsten in het schema.
- Een ander leesmodel kiezen.
- Facturen met honderden regels in blokken lezen.
- De oorzaak van een leesfout op de Kosten-pagina tonen.
