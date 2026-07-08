# UniversalServer

WPF-Desktop-Anwendung (MVVM) zur Anzeige und Simulation von Raum-Sensordaten (Temperatur, Luftfeuchtigkeit, Luftdruck) mit MySQL-Anbindung.

## Überblick

- **UI**: WPF, MaterialDesign-Theme
- **Architektur**: MVVM (`ViewModels/`, `ViewModelBase/`, `Model/`)
- **Datenbank**: MySQL (`MySql.Data`), Zugriff via ADO.NET in `Model/DBAccess.cs`
- **Simulation**: `Model/ServerMockUp.cs` erzeugt alle 2 Sekunden zufällige Sensordaten (Temperatur, Feuchte, Druck, IP), wenn kein echter Sensor angebunden ist

## Ablauf beim Start

1. Beim Laden des Hauptfensters ruft `MainViewModel` über `DBAccess.GetRooms()` Daten ab — aktuell fälschlich aus der Tabelle `Sensor` statt aus `Raum` (siehe Schema-Mismatch unten).
   - Ist die DB nicht erreichbar, wird automatisch auf hartkodierte Mock-Räume zurückgefallen (`GetMockRooms()`).
2. Über den „Start"-Button wird `ServerMockUp` gestartet, der simulierte Sensor-Messages (Temperatur, Feuchte, Druck, zufällige IP) erzeugt.
3. Jede simulierte Message soll per IP dem passenden Sensor zugeordnet (`FindSensorIdByIp`) und in die Tabelle `Messwerte` geschrieben werden — auch das passt nicht zum echten Schema, da dort keine IP-Spalte existiert (siehe unten).
4. Wählt man in der UI einen Raum aus, werden dessen letzte gespeicherte Werte aus der DB geladen.

## Voraussetzungen

- Windows mit .NET Framework (siehe `UniversalServer.csproj`)
- Visual Studio (zum Bauen/Debuggen)
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

Dies ist das **offizielle, aktuelle Schema** der Datenbank (von Melih bereitgestellt). Es gibt kein Migrationsskript im Repo — lege es lokal manuell an:

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

**Beziehung**: `Raum` (1) → `Sensor` (n) → `Messwerte` (n). Ein Raum kann mehrere Sensoren haben, jeder Sensor gehört zu genau einem Raum (oder keinem, per `ON DELETE SET NULL`). `Sensor.Typ` beschreibt den Sensor-Typ (z. B. „DHT22"), nicht den Raumnamen und nicht die IP.

⚠️ **Schema-Mismatch: Code passt nicht zum echten Schema.**

`DBAccess.GetRooms()` (`Model/DBAccess.cs`) führt aktuell aus:

```sql
SELECT SensorID, Typ FROM Sensor ORDER BY Typ
```

Das ist mit dem echten Schema aus mehreren Gründen falsch:
- Es liest aus `Sensor` statt aus `Raum` — die eigentliche Raumliste (`RaumID`, `Name`) wird nie abgefragt. Was aktuell als "Raum" in der UI erscheint, ist in Wirklichkeit die Liste der **Sensoren**, benannt nach ihrem `Typ`.
- Da mehrere Sensoren zum selben Raum gehören können, würde bei korrekter Befüllung der Tabellen jeder Sensor einzeln in der Raumliste auftauchen — nicht jeder Raum einmal.
- `FindSensorIdByIp()` sucht ebenfalls fälschlich über `Sensor.Typ`, um einen Sensor anhand einer IP zu identifizieren — dafür gibt es im Schema gar keine passende Spalte (weder in `Sensor` noch in `Raum` existiert eine IP-Spalte).

**Richtig wäre** vermutlich: `GetRooms()` sollte gegen `Raum` selektieren (`SELECT RaumID, Name FROM Raum`), und die Zuordnung „welcher Sensor/Messwert gehört zu welchem Raum" müsste über den JOIN `Sensor.RaumID = Raum.RaumID` erfolgen. Das ist aber eine Code-Änderung — aktuell nur dokumentiert, noch nicht umgesetzt.

## Bekannte Fehlerquellen

| Symptom | Ursache | Was tun |
|---|---|---|
| App startet, zeigt aber nur Mock-Räume (Wohnzimmer, Küche, Bad, Kinderzimmer, Schlafzimmer) | Verbindung zur DB fehlgeschlagen (falscher User/Passwort, DB läuft nicht, falscher Datenbankname) | MySQL-Server prüfen, Connection-String in `DBAccess.cs` mit lokaler Konfiguration abgleichen |
| Exception „Verbindung zur Datenbank fehlgeschlagen. Läuft der DB-Server?" | MySQL-Dienst ist nicht gestartet oder nicht auf `localhost` erreichbar | MySQL-Dienst starten |
| Exception „Table 'SmartHomeDB2.Sensor' doesn't exist" o. Ä. beim Laden der Räume | Datenbank existiert, aber Schema fehlt | Schema-SQL oben ausführen |
| Exception „Sensor '&lt;ip&gt;' wurde nicht in der Datenbank gefunden." | `FindSensorIdByIp` sucht die IP in `Sensor.Typ` — im echten Schema gibt es dafür keine passende Spalte | Bekannter Bug, siehe Schema-Mismatch oben; Code muss angepasst werden |
| "Räume" in der UI sind eigentlich Sensoren, mehrfach pro echtem Raum | `GetRooms()` liest aus `Sensor` statt aus `Raum` | Bekannter Bug, siehe Schema-Mismatch oben; Code muss angepasst werden |

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

## Bekannte offene Punkte

- **Schema-Mismatch (bestätigt)**: `GetRooms()` liest aus `Sensor` statt aus `Raum`; `FindSensorIdByIp()` sucht eine IP-Spalte, die im Schema nicht existiert. Code muss auf das echte Schema (`Raum` ↔ `Sensor` via `RaumID`) angepasst werden — siehe Abschnitt „Schema" oben.
- Connection-String ist nicht konfigurierbar (kein `App.config`/`appsettings.json`) — jeder Entwickler muss lokal exakt passen.
- Kein Migrationsskript für das DB-Schema im Repo.
- `MainWindowSchief.xaml` ist ein ungenutztes/experimentelles Fenster (nicht als Startfenster referenziert).
- `DataClasses1.dbml` (LINQ-to-SQL) liegt im Projekt, wird aber vom aktuellen Code nicht mehr verwendet.
