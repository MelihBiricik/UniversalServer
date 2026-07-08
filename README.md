# UniversalServer

WPF-Desktop-Anwendung (MVVM) zur Anzeige und Simulation von Raum-Sensordaten (Temperatur, Luftfeuchtigkeit, Luftdruck) mit MySQL-Anbindung.

## Überblick

- **UI**: WPF, MaterialDesign-Theme
- **Architektur**: MVVM (`ViewModels/`, `ViewModelBase/`, `Model/`)
- **Datenbank**: MySQL (`MySql.Data`), Zugriff via ADO.NET in `Model/DBAccess.cs`
- **Simulation**: `Model/ServerMockUp.cs` erzeugt alle 2 Sekunden zufällige Sensordaten (Temperatur, Feuchte, Druck), wenn kein echter Sensor angebunden ist

## Ablauf beim Start

1. Beim Laden des Hauptfensters ruft `MainViewModel` über `DBAccess.GetRooms()` die echten Räume aus der Tabelle `Raum` ab und zeigt sie zur Auswahl an.
   - Ist die DB nicht erreichbar, wird automatisch auf hartkodierte Mock-Räume zurückgefallen (`GetMockRooms()`), damit die UI trotzdem nutzbar bleibt.
2. Über den „Start"-Button wird `ServerMockUp` gestartet. Dessen Timer erzeugt alle 2 Sekunden zufällige Sensordaten (Temperatur, Feuchte, Druck).
3. Jeder simulierte Messwert wird per `DBAccess.InsertData` in die Tabelle `Messwerte` geschrieben und dabei einem zufälligen, tatsächlich existierenden Sensor zugeordnet (`INSERT ... SELECT ... FROM Sensor ORDER BY RAND() LIMIT 1`).
4. Wählt man in der UI einen Raum aus, lädt `DBAccess.GetLatestDataForRoom` den zuletzt gespeicherten Messwert für diesen Raum — über einen Join `Messwerte.SensorID = Sensor.SensorID` und `Sensor.RaumID = @raumId` — und zeigt ihn an.

## Voraussetzungen

- Windows mit .NET Framework (siehe `UniversalServer.csproj`)
- Visual Studio (zum Bauen/Debuggen; `dotnet build` funktioniert bei diesem WPF-Projekttyp nicht zuverlässig, da der XAML-Compiler fehlt — im Zweifel über Visual Studio oder dessen `MSBuild.exe` bauen)
- Beim ersten Öffnen: NuGet-Pakete wiederherstellen (Visual Studio macht das automatisch beim Build; `packages.config` listet u. a. `MySql.Data`, `MaterialDesignThemes`, `MaterialDesignColors`)
- Lokal laufender MySQL-Server

## Lokales Datenbank-Setup

Der Connection-String ist aktuell **hartkodiert** in `UniversalServer/Model/DBAccess.cs`:

```
SERVER=localhost;DATABASE=SmartHomeDB2;UID=root;PASSWORD=;
```

Damit die App bei dir läuft, muss deine lokale MySQL-Installation das exakt erfüllen:

- Server läuft auf `localhost`, Standardport 3306
- Benutzer `root` **ohne Passwort**
- Eine Datenbank namens `SmartHomeDB2`

### Schema

Es gibt kein Migrationsskript im Repo — lege das Schema lokal manuell an:

```sql
CREATE DATABASE IF NOT EXISTS SmartHomeDB2;
USE SmartHomeDB2;

-- 1. Tabelle für die Räume
CREATE TABLE Raum (
    RaumID INT PRIMARY KEY,
    Name VARCHAR(100) NOT NULL
);

-- 2. Tabelle für die Sensoren
-- Ein Sensor gehört zu einem Raum (1:n Beziehung)
CREATE TABLE Sensor (
    SensorID INT PRIMARY KEY,
    Typ VARCHAR(50),
    RaumID INT,
    CONSTRAINT fk_raum
        FOREIGN KEY (RaumID)
        REFERENCES Raum(RaumID)
        ON DELETE SET NULL
);

-- 3. Tabelle für die Messwerte
-- Ein Messwert gehört zu genau einem Sensor (1:n Beziehung)
CREATE TABLE Messwerte (
    MesswerteID INT PRIMARY KEY AUTO_INCREMENT,
    Zeitpunkt TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    Temperatur DECIMAL(5, 2),
    Luftfeuchtigkeit DECIMAL(5, 2),
    Luftdruck DECIMAL(6, 2),
    SensorID INT NOT NULL,
    CONSTRAINT fk_sensor
        FOREIGN KEY (SensorID)
        REFERENCES Sensor(SensorID)
        ON DELETE CASCADE
);
```

**Beziehung**: `Raum` (1) → `Sensor` (n) → `Messwerte` (n). Ein Raum kann mehrere Sensoren haben, jeder Sensor gehört zu genau einem Raum (oder keinem, per `ON DELETE SET NULL`). `Sensor.Typ` beschreibt den Sensor-Typ (z. B. „DHT22"), nicht den Raumnamen.

Damit die Simulation (Schritt 3 oben) funktioniert, muss mindestens **ein Sensor** in der Tabelle `Sensor` existieren — sonst wirft `InsertData` eine `InvalidOperationException` mit der Meldung „Es sind keine Sensoren in der Datenbank vorhanden."

### Bekannte Fehlerquellen bei der lokalen Einrichtung

| Symptom | Ursache | Was tun |
|---|---|---|
| App startet, zeigt aber nur Mock-Räume (Wohnzimmer, Küche, Bad, Kinderzimmer, Schlafzimmer) | Verbindung zur DB fehlgeschlagen (falscher User/Passwort, DB läuft nicht, falscher Datenbankname) | MySQL-Server prüfen, Connection-String in `DBAccess.cs` mit lokaler Konfiguration abgleichen |
| Exception „Verbindung zur Datenbank fehlgeschlagen. Läuft der DB-Server?" | MySQL-Dienst ist nicht gestartet oder nicht auf `localhost` erreichbar | MySQL-Dienst starten |
| Exception „Table 'SmartHomeDB2.Raum' doesn't exist" o. Ä. beim Laden der Räume | Datenbank existiert, aber Schema fehlt | Schema-SQL oben ausführen |
| Exception „Es sind keine Sensoren in der Datenbank vorhanden." | Tabelle `Sensor` ist leer | Mindestens einen Sensor anlegen (mit gültiger `RaumID`) |
| Werte springen beim Simulieren in unrealistisch hohe Bereiche (z. B. Tausender statt ~20 °C) | Zeigt auf einen Kultur-/Formatierungsfehler zwischen Erzeugung und Parsing der Werte — sollte mit dem aktuellen Code (`InvariantCulture` an beiden Enden, siehe Abschnitt „Simulator" unten) nicht mehr auftreten | Falls doch beobachtet: prüfen, ob eine Stelle im Code `String.Format`/`double.Parse` ohne `CultureInfo.InvariantCulture` aufruft |

## Datenzugriff (`Model/DBAccess.cs`)

| Methode | Zweck | SQL-Kernidee |
|---|---|---|
| `GetRooms()` | Liefert alle Räume für die Auswahl in der UI | `SELECT RaumID, Name FROM Raum` |
| `GetLatestDataForRoom(raumId)` | Liefert den letzten Messwert für einen Raum | `Messwerte JOIN Sensor ... WHERE Sensor.RaumID = @raumId` |
| `InsertData(...)` | Schreibt einen simulierten/echten Messwert und ordnet ihn einem zufälligen Sensor zu | `INSERT INTO Messwerte (...) SELECT ..., SensorID FROM Sensor ORDER BY RAND() LIMIT 1` |

`InsertData` wählt den Sensor bewusst zufällig, weil das Simulationsprotokoll aktuell keine feste Geräte-Identität überträgt — es geht darum, überhaupt plausible Messwerte für irgendeinen echten Sensor zu erzeugen, nicht um eine 1:1-Zuordnung zu einem bestimmten physischen Gerät.

## Simulator (`Model/ServerMockUp.cs`)

- Simuliert eingehende Sensordaten, die im echten Betrieb von einem ESP8266 kämen.
- Ein `System.Threading.Timer` löst `TimerProc` alle 2 Sekunden aus und erzeugt Zufallswerte für Temperatur, Luftfeuchtigkeit und Luftdruck.
- Die Werte werden über `String.Format(CultureInfo.InvariantCulture, ...)` formatiert, damit sie unabhängig von der Systemsprache immer mit Punkt als Dezimaltrennzeichen erzeugt werden — passend zu `double.Parse(..., CultureInfo.InvariantCulture)` in `SensorMessageParser`. **Wichtig**: Ohne `InvariantCulture` an beiden Stellen führt ein Mismatch (z. B. deutsches Komma vs. invariantes Punkt-Format) dazu, dass Werte beim Parsen um Größenordnungen verfälscht werden.
- Ein Reentrancy-Schutz (`Interlocked.Exchange` auf `_isRunningTick`) verhindert, dass mehrere `TimerProc`-Durchläufe gleichzeitig laufen, falls der Timer z. B. durch Debugging-Pausen aufgestaute Ticks nachholt.
- Das Nachrichtenprotokoll (`Temperatur;Luftfeuchte;Luftdruck;IP`, siehe `SensorMessageParser`) enthält weiterhin eine simulierte IP-Adresse (`SensorMessage.IpAddress`). Sie wird nur noch zur Anzeige im Status-Text genutzt, **nicht mehr** zur Sensor-Zuordnung in der Datenbank — dafür wird stattdessen ein zufälliger Sensor gewählt (siehe oben). Die IP ist also aktuell ein reiner Anzeigewert ohne funktionale Bedeutung.

## Kürzlich behobene Probleme

Diese Punkte lohnen sich zu kennen, falls beim Lesen des Codes Fragen aufkommen ("warum steht hier `InvariantCulture`?", "warum kein `FindSensorIdByIp` mehr?"):

- **Schema-Mismatch**: `GetRooms()` las früher fälschlich aus `Sensor` statt aus `Raum`, und die Sensor-Zuordnung erfolgte über eine nicht existierende IP-Spalte. Beides ist behoben (siehe Abschnitt „Datenzugriff").
- **Kultur-Bug**: `ServerMockUp` formatierte Zahlen ohne feste Kultur — auf einem deutschen System entstand z. B. `"21,53"` (Komma). `SensorMessageParser` interpretierte das Komma beim Parsen als Tausendertrennzeichen, wodurch aus `21.53` fälschlich `2153` wurde. Fix: `CultureInfo.InvariantCulture` an beiden Enden.
- **Scheinbare Endlosschleife in `TimerProc`**: Kein Code-Bug, sondern das Verhalten von `System.Threading.Timer`, das beim Debuggen aufgestaute Ticks nachholt/überlappt. Ein Reentrancy-Schutz verhindert nun, dass mehrere Durchläufe gleichzeitig laufen.

## Bekannte offene Punkte

- Connection-String ist nicht konfigurierbar (kein `App.config`/`appsettings.json`) — jeder Entwickler muss lokal exakt passen.
- Kein Migrationsskript für das DB-Schema im Repo.
- `MainWindowSchief.xaml` ist ein ungenutztes/experimentelles Fenster (nicht als Startfenster referenziert).
- `DataClasses1.dbml` (LINQ-to-SQL) liegt im Projekt, wird aber vom aktuellen Code nicht mehr verwendet.
- Der Simulator ordnet Messwerte einem zufälligen Sensor zu, nicht einem bestimmten Gerät — für eine echte Geräte-Anbindung (z. B. per IP oder Seriennummer) müsste das Protokoll und `InsertData` erweitert werden.

## Projektstruktur

```
UniversalServer/
├── App.xaml(.cs)              # Einstiegspunkt, Theme-Setup
├── MainWindow.xaml(.cs)       # Hauptfenster
├── SettingsWindow.xaml(.cs)   # Einstellungen
├── Model/
│   ├── DBAccess.cs            # ADO.NET-Datenzugriff (MySQL)
│   ├── Raum.cs                # Raum-Modell
│   ├── Server.cs              # (echter) Server-Kontrakt
│   ├── ServerMockUp.cs        # Simulator für Testdaten
│   ├── SensorMessage*.cs      # Sensor-Nachrichtenformate
│   ├── TempValue.cs / HumidValue.cs / PressureValue.cs / ...
│   └── I*.cs                  # Interfaces (ISensorRepository, IServerContract, ISettingsRepository)
├── ViewModels/
│   ├── MainViewModel.cs
│   └── SettingsViewModel.cs
├── ViewModelBase/              # MVVM-Basisklassen (RelayCommand, ViewModel)
└── Themes/CustomMaterialStyles.xaml
```
