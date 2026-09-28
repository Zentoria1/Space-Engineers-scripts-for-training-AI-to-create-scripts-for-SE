// =============================================================================
//  SKP RADAR  -  Space Engineers Programmable Block script
// -----------------------------------------------------------------------------
//  PPI-style scope radar that fuses every detection source on your grid, plus
//  a contact list, a wide-area scope and a WeaponCore fire-control panel.
//
//  QUICK START
//   1. Paste into a Programmable Block (PB), Check Code, Remember & Exit.
//   2. Put "[Radar]" in the NAME of an LCD (or map a cockpit screen - see
//      COCKPIT SCREENS). That is the only tag you need: sensors, cameras,
//      antennas and turrets are found automatically.
//   3. Add at least one detection source (table below).
//   4. Settings are in the PB's Custom Data and apply within a second.
//
//  WHAT CAN DETECT CONTACTS   (read this if the scope stays empty!)
//   Vanilla SE gives scripts no true radar; the scope only shows what these see:
//    Sensor            ~50 m field        whatever its checkboxes allow
//    Camera (enabled)  CameraRange        anything in its ~45 deg cone, incl.
//                                         friendlies and asteroids
//    Vanilla turret /  turret range       only the target it is aiming at
//      Turret Controller
//    WeaponCore mod    weapon range       every hostile the grid's AI sees
//    Antenna           -                  NOTHING by itself. With Network=true
//                                         it shares contacts with other ships
//                                         running this script, which also show
//                                         up themselves.
//   Sensors-only ships get the scope zoomed in to the sensor field; the scope
//   and the PB's info panel warn when there is no long-range source.
//
//  SCREENS   (name contains the tag; every view scales to its screen)
//    [Radar]  scope    [RadarInfo]  contact list
//    [RadarWide]  wide flat scope   [RadarWeapons]  weapons panel
//
//  COCKPIT SCREENS   Custom Data section [RadarScreens]
//    Fighter Cockpit/0 = scope        <block name>/<screen index> = view
//    Helm Seat/1       = info         index 0 = first screen (default)
//    Views: scope, info, wide, weapons. Exact block name first, then partial.
//    Mistakes are listed in the PB's info panel.
//
//  SETTINGS   Custom Data section [Radar]  (default shown)
//    LcdTag=[Radar]  InfoTag=[RadarInfo]  WideTag=[RadarWide]
//    WeaponsTag=[RadarWeapons]            screen name tags
//    Reference=            cockpit/remote for orientation (blank = first found)
//    ProjectionAngle=55    scope tilt in degrees (0 = flat top-down)
//    AutoRange=true        scope range from weapons > cameras > sensors
//    Range=5000            scope radius (m) when AutoRange=false, and fallback
//    WideRange=50000       wide scope radius (m)     WideAngle=0  its tilt
//    HoldSeconds=4         keep a contact this long after last seen
//    ScanCameras=true      raycast through enabled cameras
//    CameraRange=0         camera scan distance (m), 0 = scope range
//    ScanAsteroids=true    show asteroids / planets
//    UseWeaponCore=true    use WeaponCore targets if the mod is loaded
//    Network=false         share contacts over antennas
//    NetworkTag=SKPRadar   radar-net channel (same on every ship)
//    ShowLabels=true       names next to blips
//    FriendlyNames=        comma-separated name parts always shown as yours
//    SortMode=threat       contact list order: threat, distance, name, type
//    ColorEnemy=255,50,50  ColorNeutral=255,210,40  ColorFriendly=60,140,255
//    ColorFaction=60,230,90  ColorVoxel=80,80,80  ColorMeteor=255,140,0
//    ColorBackground=0,0,0  ColorGrid=0,120,160  ColorGridFaint=0,70,95
//    ColorSweep=0,200,120,90  ColorOwnShip=255,255,255  ColorLock=255,255,255
//    ColorText=0,150,200  ColorTextFaint=0,80,110  ColorAlert=255,60,60
//    ColorOk=60,230,90  ColorInactive=120,120,120     (R,G,B or R,G,B,A)
//    WeaponGroup=          block group of WeaponCore weapons (blank = all)
//    FireRange=8000        max engagement range (m)
//    TargetLatch=6         seconds to keep an auto target before re-selecting
//    AimGain=6             gyro responsiveness when Aim is on
//    TickRate=10           update period in game ticks: 1, 10 or 100
//    DrawRate=6            max screen redraws per second
//    RescanSeconds=10      how often to look for added/removed blocks
//   Text after ';' on a line is ignored. Lines the game's settings reader
//   can't handle are turned into "; [ignored]" comments and listed in the
//   PB's info panel; nothing is ever deleted. Clear Custom Data to reset.
//
//  COMMANDS   (PB "Run" argument, button panel or toolbar)
//    reload                re-read Custom Data and re-scan blocks now
//    clean                 remove all comment lines from Custom Data
//    rangeup / rangedown   zoom the scope out / in (manual range)
//    auto                  back to automatic range
//    clear                 wipe all contacts (and any manual target)
//    sort [mode]           cycle the list order, or set it: sort distance
//    next / prev           manually select the next / previous hostile
//    target <name>         select the nearest hostile whose name contains it
//    autotarget            back to automatic target selection
//    arm / disarm          master safety; nothing fires unless ARMED
//    aim                   toggle gyro slew-to-target
//    barrage               toggle distributed fire across all locked targets
//   The selected target gets a diamond on the scopes and ">" in the list. A
//   manual target that stays unseen for a few seconds reverts to automatic.
// =============================================================================

// ----------------------------- CONFIG FIELDS --------------------------------
string _lcdTag = "[Radar]", _infoTag = "[RadarInfo]", _wideTag = "[RadarWide]", _weaponsTag = "[RadarWeapons]";
string _referenceName = "", _networkTag = "SKPRadar", _friendlyNames = "", _sortMode = "threat", _weaponGroup = "";
double _projAngle = 55, _wideRange = 50000, _wideAngle = 0, _rangeSetting = 5000, _holdSeconds = 4;
double _cameraRange = 0, _fireRange = 8000, _targetLatch = 6, _aimGain = 6, _drawRate = 6, _rescanSeconds = 10;
bool _autoRange = true, _scanCameras = true, _scanAsteroids = true, _useWeaponCore = true, _network = false, _showLabels = true;
int _tickRate = 10;

Color _colEnemy = new Color(255, 50, 50), _colNeutral = new Color(255, 210, 40), _colFriendly = new Color(60, 140, 255);
Color _colFaction = new Color(60, 230, 90), _colVoxel = new Color(80, 80, 80), _colMeteor = new Color(255, 140, 0);
Color _colBackground = new Color(0, 0, 0), _colGrid = new Color(0, 120, 160), _colGridFaint = new Color(0, 70, 95);
Color _colSweep = new Color(0, 200, 120, 90), _colOwnShip = new Color(255, 255, 255), _colLock = new Color(255, 255, 255);
Color _colText = new Color(0, 150, 200), _colTextFaint = new Color(0, 80, 110), _colAlert = new Color(255, 60, 60);
Color _colOk = new Color(60, 230, 90), _colInactive = new Color(120, 120, 120);

static readonly string[] SortModes = { "threat", "distance", "name", "type" };
const string ScreensHint = "; <block name>/<screen index> = scope | info | wide | weapons\n";

// Setting name, default. Missing keys are added to Custom Data.
static readonly string[][] Defaults =
{
    new[] { "LcdTag", "[Radar]" }, new[] { "InfoTag", "[RadarInfo]" }, new[] { "WideTag", "[RadarWide]" },
    new[] { "WeaponsTag", "[RadarWeapons]" }, new[] { "Reference", "" }, new[] { "ProjectionAngle", "55" },
    new[] { "AutoRange", "true" }, new[] { "Range", "5000" }, new[] { "WideRange", "50000" }, new[] { "WideAngle", "0" },
    new[] { "HoldSeconds", "4" }, new[] { "ScanCameras", "true" }, new[] { "CameraRange", "0" },
    new[] { "ScanAsteroids", "true" }, new[] { "UseWeaponCore", "true" }, new[] { "Network", "false" },
    new[] { "NetworkTag", "SKPRadar" }, new[] { "ShowLabels", "true" }, new[] { "FriendlyNames", "" },
    new[] { "SortMode", "threat" },
    new[] { "ColorEnemy", "255,50,50" }, new[] { "ColorNeutral", "255,210,40" }, new[] { "ColorFriendly", "60,140,255" },
    new[] { "ColorFaction", "60,230,90" }, new[] { "ColorVoxel", "80,80,80" }, new[] { "ColorMeteor", "255,140,0" },
    new[] { "ColorBackground", "0,0,0" }, new[] { "ColorGrid", "0,120,160" }, new[] { "ColorGridFaint", "0,70,95" },
    new[] { "ColorSweep", "0,200,120,90" }, new[] { "ColorOwnShip", "255,255,255" }, new[] { "ColorLock", "255,255,255" },
    new[] { "ColorText", "0,150,200" }, new[] { "ColorTextFaint", "0,80,110" }, new[] { "ColorAlert", "255,60,60" },
    new[] { "ColorOk", "60,230,90" }, new[] { "ColorInactive", "120,120,120" },
    new[] { "WeaponGroup", "" }, new[] { "FireRange", "8000" }, new[] { "TargetLatch", "6" }, new[] { "AimGain", "6" },
    new[] { "TickRate", "10" }, new[] { "DrawRate", "6" }, new[] { "RescanSeconds", "10" },
};

// ----------------------------- RUNTIME STATE --------------------------------
readonly MyIni _ini = new MyIni();
readonly Dictionary<long, Track> _tracks = new Dictionary<long, Track>();
readonly List<string> _friendlyList = new List<string>();

readonly List<IMySensorBlock> _sensors = new List<IMySensorBlock>();
readonly List<IMyCameraBlock> _cameras = new List<IMyCameraBlock>();
readonly List<int> _camSweep = new List<int>();
readonly List<IMyRadioAntenna> _antennas = new List<IMyRadioAntenna>();
readonly List<IMyLargeTurretBase> _turrets = new List<IMyLargeTurretBase>();
readonly List<IMyTurretControlBlock> _turretCtl = new List<IMyTurretControlBlock>();
readonly List<IMyShipController> _controllers = new List<IMyShipController>();
readonly List<IMyGyro> _gyros = new List<IMyGyro>();
readonly List<IMyTerminalBlock> _allBlocks = new List<IMyTerminalBlock>();
readonly List<IMyTerminalBlock> _wcWeapons = new List<IMyTerminalBlock>();
readonly List<IMyTerminalBlock> _groupWeapons = new List<IMyTerminalBlock>();
readonly List<IMyTextSurface> _surfaces = new List<IMyTextSurface>(), _infoSurfaces = new List<IMyTextSurface>();
readonly List<IMyTextSurface> _wideSurfaces = new List<IMyTextSurface>(), _weaponSurfaces = new List<IMyTextSurface>();
readonly List<Track> _sortList = new List<Track>();
readonly List<long> _deadIds = new List<long>();
readonly List<string> _configWarnings = new List<string>(), _screenWarnings = new List<string>();
readonly List<MyDetectedEntityInfo> _scratch = new List<MyDetectedEntityInfo>();
readonly Dictionary<MyDetectedEntityInfo, float> _wcThreats = new Dictionary<MyDetectedEntityInfo, float>();
readonly HashSet<long> _myGridIds = new HashSet<long>();
readonly StringBuilder _sb = new StringBuilder();
readonly WcPbApi _wc = new WcPbApi();

IMyShipController _reference;
IMyBroadcastListener _listener;
Vector3D _origin;
bool _wcReady;
long _wcGridId;
double _wcMaxRange, _sensorMaxRange, _range = 5000;
string _rangeSource = "manual";
double _time, _nextRescan, _nextDraw, _nextConfigCheck, _nextBroadcast, _nextWcPoll;
string _lastCustomData, _configError = "", _echo = "";
string _focusDbg = "focus=?", _turretDbg = "tgt=?";
string _message = "";
double _messageUntil;
int _camIndex;

// Fire-control state (persisted in Storage).
bool _armed, _aim, _barrage, _manualTarget, _groupFiring, _gyrosOverridden;
long _wpnTargetId;
double _wpnLatchTime, _manualMissingSince = -1;
int _barrageTargetCount;

public Program()
{
    LoadState();
    LoadConfig();
    Rebuild();
    if (_aim) ReleaseGyros(true); // gyros may still hold old rates from before a recompile
    Runtime.UpdateFrequency = FreqFromTicks(_tickRate);
}

UpdateFrequency FreqFromTicks(int ticks)
{
    return ticks <= 1 ? UpdateFrequency.Update1 : ticks >= 100 ? UpdateFrequency.Update100 : UpdateFrequency.Update10;
}

// "armed;aim;barrage;manual;targetId"
public void Save()
{
    Storage = (_armed ? "1;" : "0;") + (_aim ? "1;" : "0;") + (_barrage ? "1;" : "0;") + (_manualTarget ? "1;" : "0;") + _wpnTargetId;
}

void LoadState()
{
    if (string.IsNullOrEmpty(Storage)) return;
    var f = Storage.Split(';');
    if (f.Length >= 3) { _armed = f[0] == "1"; _aim = f[1] == "1"; _barrage = f[2] == "1"; }
    long id;
    if (f.Length >= 5 && f[3] == "1" && long.TryParse(f[4], out id) && id != 0) { _manualTarget = true; _wpnTargetId = id; }
}

public void Main(string argument, UpdateType updateSource)
{
    _time += Runtime.TimeSinceLastRun.TotalSeconds;
    bool draw = false;

    if (!string.IsNullOrWhiteSpace(argument)) { HandleCommand(argument.Trim()); draw = true; }

    if (_time >= _nextConfigCheck)
    {
        _nextConfigCheck = _time + 1.0;
        if (Me.CustomData != _lastCustomData) { Reload(); draw = true; }
    }
    if (_time >= _nextRescan) Rebuild();

    _origin = _reference != null ? _reference.GetPosition() : Me.GetPosition();

    GatherContacts();
    ReceiveNetwork();
    BroadcastNetwork();
    Prune();
    UpdateAutoRange();
    UpdateWeapons();

    if (draw || _time >= _nextDraw - 0.01)
    {
        _nextDraw = _time + 1.0 / _drawRate;
        DrawAll();
        _echo = BuildEcho();
    }
    Echo(_echo);
}

void Reload()
{
    LoadConfig();
    Rebuild();
    Runtime.UpdateFrequency = FreqFromTicks(_tickRate);
}

void Notify(string msg) { _message = msg; _messageUntil = _time + 10; }

// =============================================================================
//  CONFIG
// =============================================================================
void LoadConfig()
{
    _configError = "";
    _configWarnings.Clear();
    if (!EnsureDefaults()) { _lastCustomData = Me.CustomData; return; }
    _lastCustomData = Me.CustomData;

    MyIniParseResult res;
    if (!_ini.TryParse(Sanitize(_lastCustomData, true, null), out res))
    {
        _configError = "Custom Data line " + res.LineNo + ": " + res.Error;
        return;
    }

    _lcdTag = CfgTag("LcdTag", "[Radar]");
    _infoTag = CfgTag("InfoTag", "[RadarInfo]");
    _wideTag = CfgTag("WideTag", "[RadarWide]");
    _weaponsTag = CfgTag("WeaponsTag", "[RadarWeapons]");
    _networkTag = CfgTag("NetworkTag", "SKPRadar");
    _referenceName = CfgStr("Reference", "");
    _friendlyNames = CfgStr("FriendlyNames", "");
    _weaponGroup = CfgStr("WeaponGroup", "");
    _projAngle = CfgNum("ProjectionAngle", 55);
    _rangeSetting = Math.Max(100, CfgNum("Range", 5000));
    _wideRange = Math.Max(1000, CfgNum("WideRange", 50000));
    _wideAngle = CfgNum("WideAngle", 0);
    _holdSeconds = Math.Max(0.5, CfgNum("HoldSeconds", 4));
    _cameraRange = Math.Max(0, CfgNum("CameraRange", 0));
    _fireRange = Math.Max(100, CfgNum("FireRange", 8000));
    _targetLatch = CfgNum("TargetLatch", 6);
    _aimGain = CfgNum("AimGain", 6);
    _drawRate = MathHelperD.Clamp(CfgNum("DrawRate", 6), 0.2, 60);
    _rescanSeconds = Math.Max(2, CfgNum("RescanSeconds", 10));
    _autoRange = CfgBool("AutoRange", true);
    _scanCameras = CfgBool("ScanCameras", true);
    _scanAsteroids = CfgBool("ScanAsteroids", true);
    _useWeaponCore = CfgBool("UseWeaponCore", true);
    _network = CfgBool("Network", false);
    _showLabels = CfgBool("ShowLabels", true);

    _tickRate = (int)CfgNum("TickRate", 10);
    if (_tickRate != 1 && _tickRate != 10 && _tickRate != 100)
    {
        _configWarnings.Add("TickRate must be 1, 10 or 100");
        _tickRate = _tickRate < 5 ? 1 : _tickRate < 50 ? 10 : 100;
    }
    string sort = CfgStr("SortMode", "threat").ToLowerInvariant();
    if (Array.IndexOf(SortModes, sort) >= 0) _sortMode = sort;
    else { _sortMode = "threat"; _configWarnings.Add("SortMode '" + sort + "' unknown - using threat"); }

    _colEnemy = CfgColor("ColorEnemy", _colEnemy);
    _colNeutral = CfgColor("ColorNeutral", _colNeutral);
    _colFriendly = CfgColor("ColorFriendly", _colFriendly);
    _colFaction = CfgColor("ColorFaction", _colFaction);
    _colVoxel = CfgColor("ColorVoxel", _colVoxel);
    _colMeteor = CfgColor("ColorMeteor", _colMeteor);
    _colBackground = CfgColor("ColorBackground", _colBackground);
    _colGrid = CfgColor("ColorGrid", _colGrid);
    _colGridFaint = CfgColor("ColorGridFaint", _colGridFaint);
    _colSweep = CfgColor("ColorSweep", _colSweep);
    _colOwnShip = CfgColor("ColorOwnShip", _colOwnShip);
    _colLock = CfgColor("ColorLock", _colLock);
    _colText = CfgColor("ColorText", _colText);
    _colTextFaint = CfgColor("ColorTextFaint", _colTextFaint);
    _colAlert = CfgColor("ColorAlert", _colAlert);
    _colOk = CfgColor("ColorOk", _colOk);
    _colInactive = CfgColor("ColorInactive", _colInactive);

    _friendlyList.Clear();
    foreach (var part in _friendlyNames.Split(','))
    {
        var p = part.Trim().ToLowerInvariant();
        if (p.Length > 0) _friendlyList.Add(p);
    }

    if (!_scanAsteroids)
    {
        _deadIds.Clear();
        foreach (var kv in _tracks) if (kv.Value.Voxel) _deadIds.Add(kv.Key);
        foreach (var id in _deadIds) _tracks.Remove(id);
    }
}

// Adds missing sections/keys without touching anything else. Lines MyIni can't
// read are turned into comments first (Sanitize). Returns false if Custom Data
// still can't be parsed; nothing is written in that case.
bool EnsureDefaults()
{
    string raw = Me.CustomData ?? "";
    var problems = new List<string>();
    string clean = Sanitize(raw, false, problems);

    var w = new MyIni();
    MyIniParseResult res;
    if (!w.TryParse(clean, out res))
    {
        _configError = "Custom Data line " + res.LineNo + ": " + res.Error + LineHint(clean, res.LineNo)
                     + " (nothing was changed - fix it, or clear Custom Data to reset)";
        return false;
    }

    bool changed = clean != raw, newScreens = false;
    if (!w.ContainsSection("Radar")) { w.AddSection("Radar"); changed = true; }
    foreach (var d in Defaults)
        if (!w.ContainsKey("Radar", d[0])) { w.Set("Radar", d[0], d[1]); changed = true; }
    if (!w.ContainsSection("RadarScreens")) { w.AddSection("RadarScreens"); newScreens = changed = true; }
    if (!changed) return true;

    string text = w.ToString();
    if (newScreens) text = InsertBeforeSection(text, "RadarScreens", ScreensHint);
    Me.CustomData = text;

    _configWarnings.AddRange(problems);
    if (problems.Count > 0) Notify("Fixed " + problems.Count + " unreadable Custom Data line(s) - see below.");
    return true;
}

// "clean": drop every comment and blank line, keep sections and key=value.
void CleanCustomData()
{
    Me.CustomData = CleanText(Me.CustomData);
    Reload();
    Notify(_configError.Length > 0 ? "Cleaned, but Custom Data still has an error." : "Custom Data cleaned.");
}

static string CleanText(string text)
{
    var sb = new StringBuilder();
    foreach (var raw in (text ?? "").Split('\n'))
    {
        string t = raw.TrimEnd('\r').Trim();
        if (t.Length == 0 || t[0] == ';') continue;
        int c = t.IndexOf(';');
        if (c > 0) t = t.Substring(0, c).TrimEnd();
        if (t[0] == '[')
        {
            if (sb.Length > 0) sb.Append('\n');
            if (t == "[RadarScreens]") sb.Append(ScreensHint);
        }
        sb.Append(t).Append('\n');
    }
    return sb.ToString();
}

static string InsertBeforeSection(string text, string section, string help)
{
    int i = text.IndexOf("[" + section + "]");
    return i < 0 ? text : text.Substring(0, i) + help + text.Substring(i);
}

static string LineHint(string text, int lineNo)
{
    var lines = text.Split('\n');
    return lineNo < 1 || lineNo > lines.Length ? "" : " -> '" + Trunc(lines[lineNo - 1].Trim(), 40) + "'";
}

// Makes Custom Data safe for MyIni without losing anything: every line MyIni
// would reject becomes a ';' comment (text kept) and is reported. Line count
// never changes. stripInline = also drop "; comments" at the end of lines.
// Handles: lines without '=', settings above the first [Section], repeated
// settings or sections, and leftover lines of broken multi-line comments
// (indented text right under a comment, as older versions could write).
static string Sanitize(string text, bool stripInline, List<string> problems)
{
    text = text ?? "";
    var lines = text.Split('\n');
    var sb = new StringBuilder(text.Length + 64);
    var keys = new HashSet<string>();
    var sections = new HashSet<string>();
    string section = null;
    bool skipSection = false, afterComment = false, multiline = false, ended = false;
    int brokenComment = 0;

    for (int i = 0; i < lines.Length; i++)
    {
        string l = lines[i].TrimEnd('\r');
        string t = l.Trim();
        string bad = null;
        bool continuation = false, isComment = false, keyEmpty = false;

        if (ended || t.Length == 0) { }
        else if (t[0] == ';') isComment = true;
        else if (t == "---") ended = true;
        else if (afterComment && char.IsWhiteSpace(l[0])) { bad = ""; continuation = true; }
        else if (t[0] == '|') { if (!multiline) bad = "starts with '|' but isn't part of a multi-line value"; }
        else if (t[0] == '[')
        {
            string h = t;
            int c = h.IndexOf(';');
            if (c >= 0) h = h.Substring(0, c).TrimEnd();
            if (h.Length < 3 || h[h.Length - 1] != ']') bad = "is not a valid [Section] line";
            else
            {
                string name = h.Substring(1, h.Length - 2).Trim().ToLowerInvariant();
                if (!sections.Add(name)) { bad = "repeats section [" + name + "] - that copy is ignored"; skipSection = true; }
                else { section = name; skipSection = false; if (stripInline) l = h; }
            }
        }
        else
        {
            if (stripInline)
            {
                int c = l.IndexOf(';');
                if (c >= 0) l = l.Substring(0, c).TrimEnd();
            }
            int eq = l.IndexOf('=');
            if (skipSection) bad = "";
            else if (section == null) bad = "is above the first [Section]";
            else if (eq < 0) bad = "is missing '=' (settings are key=value)";
            else
            {
                string key = l.Substring(0, eq).Trim();
                if (key.Length == 0) bad = "has no name before '='";
                else if (!keys.Add(section + "/" + key.ToLowerInvariant())) bad = "repeats a setting already set above - ignored";
                else keyEmpty = l.Substring(eq + 1).Trim().Length == 0;
            }
        }

        if (bad != null)
        {
            if (continuation) brokenComment++;
            else if (bad.Length > 0 && problems != null)
                problems.Add("Line " + (i + 1) + " '" + Trunc(t, 30) + "' " + bad + " (now a comment)");
            l = (continuation ? ";" : "; [ignored] ") + l.TrimEnd();
            isComment = true;
        }

        afterComment = isComment && (continuation || bad == null);
        if (bad == null && !isComment && t.Length > 0) multiline = keyEmpty || (t[0] == '|' && multiline);
        sb.Append(l);
        if (i < lines.Length - 1) sb.Append('\n');
    }

    if (brokenComment > 0 && problems != null)
        problems.Add(brokenComment + " line(s) of a broken comment (from an older version) were turned back into comments");
    return sb.ToString();
}

string CfgStr(string key, string def) { return _ini.Get("Radar", key).ToString(def).Trim(); }

// A blank tag would match every block on the grid.
string CfgTag(string key, string def)
{
    string s = CfgStr(key, def);
    if (s.Length > 0) return s;
    _configWarnings.Add(key + " is blank - using " + def);
    return def;
}

double CfgNum(string key, double def)
{
    var v = _ini.Get("Radar", key);
    double d;
    if (v.IsEmpty) return def;
    if (v.TryGetDouble(out d)) return d;
    _configWarnings.Add(key + "='" + v.ToString() + "' is not a number");
    return def;
}

bool CfgBool(string key, bool def)
{
    var v = _ini.Get("Radar", key);
    bool b;
    if (v.IsEmpty) return def;
    if (v.TryGetBoolean(out b)) return b;
    _configWarnings.Add(key + "='" + v.ToString() + "' is not true/false");
    return def;
}

Color CfgColor(string key, Color def)
{
    string s = CfgStr(key, "");
    if (s.Length == 0) return def;
    var p = s.Split(',');
    int r, g, b, a = 255;
    if (p.Length >= 3 && p.Length <= 4 && int.TryParse(p[0].Trim(), out r) && int.TryParse(p[1].Trim(), out g)
        && int.TryParse(p[2].Trim(), out b) && (p.Length == 3 || int.TryParse(p[3].Trim(), out a)))
        return new Color(MathHelper.Clamp(r, 0, 255), MathHelper.Clamp(g, 0, 255), MathHelper.Clamp(b, 0, 255), MathHelper.Clamp(a, 0, 255));
    _configWarnings.Add(key + "='" + s + "' is not R,G,B");
    return def;
}

bool IsFriendlyName(string name)
{
    if (_friendlyList.Count == 0 || string.IsNullOrEmpty(name)) return false;
    foreach (var f in _friendlyList)
        if (name.IndexOf(f, StringComparison.OrdinalIgnoreCase) >= 0) return true;
    return false;
}

// Writes one [Radar] value back, keeping any "; comment" on that line.
void SaveSetting(string key, string value)
{
    var w = new MyIni();
    if (!w.TryParse(Sanitize(Me.CustomData, false, null))) { Notify("Couldn't save " + key + ": Custom Data has an error."); return; }
    string old = w.Get("Radar", key).ToString("");
    int c = old.IndexOf(';');
    if (c >= 0) value += " " + old.Substring(c);
    w.Set("Radar", key, value);
    Me.CustomData = w.ToString();
    _lastCustomData = Me.CustomData;
}

void HandleCommand(string arg)
{
    string cmd = arg.ToLowerInvariant(), verb = cmd, rest = "";
    int sp = cmd.IndexOf(' ');
    if (sp > 0) { verb = cmd.Substring(0, sp); rest = cmd.Substring(sp + 1).Trim(); }

    switch (verb)
    {
        case "reload":
            Reload();
            Notify(_configError.Length > 0 ? "Reload failed - see config error." : "Settings reloaded.");
            break;
        case "clean":
            CleanCustomData();
            break;
        case "rangeup":
        case "rangedown":
            _autoRange = false;
            _range = _rangeSetting = verb == "rangeup" ? _range * 1.5 : Math.Max(100, _range / 1.5);
            SaveSetting("AutoRange", "false");
            SaveSetting("Range", Math.Round(_rangeSetting).ToString());
            Notify("Scope range " + FormatDist(_range) + " (manual).");
            break;
        case "auto":
            if (rest == "target") { AutoTarget(); break; }
            _autoRange = true;
            SaveSetting("AutoRange", "true");
            UpdateAutoRange();
            Notify("Scope range automatic.");
            break;
        case "autotarget": AutoTarget(); break;
        case "clear":
            _tracks.Clear();
            if (_manualTarget) { _manualTarget = false; _wpnTargetId = 0; Save(); }
            Notify("Contacts cleared.");
            break;
        case "sort": SetSort(rest); break;
        case "next": CycleTarget(1); break;
        case "prev": case "previous": CycleTarget(-1); break;
        case "target":
            if (rest.Length == 0) Notify("Usage: target <name>"); else TargetByName(rest);
            break;
        case "arm": _armed = true; Save(); Notify("ARMED."); break;
        case "disarm":
            _armed = false; SetGroupFire(false); ClearWeaponTargets(); ReleaseGyros(false); Save();
            Notify("Disarmed.");
            break;
        case "aim":
            _aim = !_aim;
            if (!_aim) ReleaseGyros(true);
            Save();
            Notify("Aim " + (_aim ? "ON." : "OFF."));
            break;
        case "barrage":
            _barrage = !_barrage;
            if (!_barrage) { SetGroupFire(false); ClearWeaponTargets(); }
            Save();
            Notify("Barrage " + (_barrage ? "ON." : "OFF."));
            break;
        default: Notify("Unknown command: " + arg); break;
    }
}

void SetSort(string mode)
{
    if (mode.Length == 0) _sortMode = SortModes[(Array.IndexOf(SortModes, _sortMode) + 1) % SortModes.Length];
    else if (Array.IndexOf(SortModes, mode) >= 0) _sortMode = mode;
    else { Notify("Unknown sort '" + mode + "'. Use: threat, distance, name, type."); return; }
    SaveSetting("SortMode", _sortMode);
    Notify("Contact list sorted by " + _sortMode + ".");
}

// =============================================================================
//  BLOCK DISCOVERY  (one pass over the grid's blocks)
// =============================================================================
void Rebuild()
{
    _nextRescan = _time + _rescanSeconds;
    _sensors.Clear(); _cameras.Clear(); _antennas.Clear(); _turrets.Clear(); _turretCtl.Clear();
    _controllers.Clear(); _gyros.Clear(); _surfaces.Clear(); _infoSurfaces.Clear();
    _wideSurfaces.Clear(); _weaponSurfaces.Clear(); _screenWarnings.Clear(); _myGridIds.Clear();
    _sensorMaxRange = 0;

    _allBlocks.Clear();
    GridTerminalSystem.GetBlocksOfType(_allBlocks, b => b.IsSameConstructAs(Me));
    _myGridIds.Add(Me.CubeGrid.EntityId);

    foreach (var b in _allBlocks)
    {
        _myGridIds.Add(b.CubeGrid.EntityId);

        var sensor = b as IMySensorBlock;
        if (sensor != null)
        {
            _sensors.Add(sensor);
            double e = Math.Max(Math.Max(sensor.LeftExtend, sensor.RightExtend), Math.Max(sensor.TopExtend, sensor.BottomExtend));
            _sensorMaxRange = Math.Max(_sensorMaxRange, Math.Max(e, Math.Max(sensor.FrontExtend, sensor.BackExtend)));
        }
        else if (b is IMyCameraBlock) _cameras.Add((IMyCameraBlock)b);
        else if (b is IMyRadioAntenna) _antennas.Add((IMyRadioAntenna)b);
        else if (b is IMyLargeTurretBase) _turrets.Add((IMyLargeTurretBase)b);
        else if (b is IMyTurretControlBlock) _turretCtl.Add((IMyTurretControlBlock)b);
        else if (b is IMyGyro) _gyros.Add((IMyGyro)b);
        else if (b is IMyShipController) _controllers.Add((IMyShipController)b);

        // Tagged screens. Specific tags first in case a custom tag contains another.
        var p = b as IMyTextSurfaceProvider;
        if (p == null) continue;
        string n = b.CustomName;
        List<IMyTextSurface> target;
        if (n.Contains(_infoTag)) target = _infoSurfaces;
        else if (n.Contains(_wideTag)) target = _wideSurfaces;
        else if (n.Contains(_weaponsTag)) target = _weaponSurfaces;
        else if (n.Contains(_lcdTag)) target = _surfaces;
        else continue;
        if (p.SurfaceCount == 0) { _screenWarnings.Add("'" + n + "' has no screens"); continue; }
        for (int i = 0; i < p.SurfaceCount; i++) target.Add(p.GetSurface(i));
    }
    while (_camSweep.Count < _cameras.Count) _camSweep.Add(_camSweep.Count * 7);

    // Orientation reference: named controller -> any controller.
    _reference = null;
    if (_referenceName.Length > 0)
    {
        _reference = _controllers.Find(c => c.CustomName == _referenceName)
                  ?? _controllers.Find(c => c.CustomName.Contains(_referenceName));
        if (_reference == null) _screenWarnings.Add("Reference '" + _referenceName + "' not found");
    }
    if (_reference == null && _controllers.Count > 0)
        _reference = _controllers.Find(c => c.CanControlShip) ?? _controllers[0];

    AssignConfiguredScreens();
    PrepareSurfaces(_surfaces); PrepareSurfaces(_infoSurfaces);
    PrepareSurfaces(_wideSurfaces); PrepareSurfaces(_weaponSurfaces);

    foreach (var cam in _cameras)
        if (cam.EnableRaycast != _scanCameras) cam.EnableRaycast = _scanCameras;

    // WeaponCore (no-op if the mod isn't loaded). WC turrets aren't
    // IMyLargeTurretBase, so ask WeaponCore which blocks are weapons.
    _wcReady = false;
    _wcWeapons.Clear();
    _wcGridId = 0;
    _wcMaxRange = 0;
    if (_useWeaponCore) { try { _wcReady = _wc.Activate(Me); } catch { _wcReady = false; } }
    if (_wcReady)
    {
        foreach (var b in _allBlocks)
        {
            bool isWeapon = false;
            try { isWeapon = _wc.HasCoreWeapon(b); } catch { }
            if (!isWeapon) continue;
            _wcWeapons.Add(b);
            try { _wcMaxRange = Math.Max(_wcMaxRange, _wc.GetMaxWeaponRange(b, 0)); } catch { }
        }
        foreach (var id in _myGridIds)
        {
            bool has = false;
            try { has = _wc.HasGridAi(id); } catch { }
            if (has) { _wcGridId = id; break; }
        }
    }

    _groupWeapons.Clear();
    if (string.IsNullOrWhiteSpace(_weaponGroup)) _groupWeapons.AddRange(_wcWeapons);
    else
    {
        var grp = GridTerminalSystem.GetBlockGroupWithName(_weaponGroup);
        if (grp == null) _screenWarnings.Add("WeaponGroup '" + _weaponGroup + "' not found");
        else
        {
            var grpBlocks = new List<IMyTerminalBlock>();
            grp.GetBlocks(grpBlocks, b => b.IsSameConstructAs(Me));
            foreach (var b in grpBlocks)
            {
                bool isWeapon = false;
                try { isWeapon = !_wcReady || _wc.HasCoreWeapon(b); } catch { }
                if (isWeapon) _groupWeapons.Add(b);
            }
        }
    }

    if (_network && (_listener == null || _listener.Tag != _networkTag))
        _listener = IGC.RegisterBroadcastListener(_networkTag);

    UpdateAutoRange();
}

void PrepareSurfaces(List<IMyTextSurface> list)
{
    foreach (var s in list)
    {
        if (s.ContentType != ContentType.SCRIPT) s.ContentType = ContentType.SCRIPT;
        if (s.Script != "") s.Script = "";
    }
}

// [RadarScreens]:  <block name>/<index> = scope | info | wide | weapons
void AssignConfiguredScreens()
{
    if (!_ini.ContainsSection("RadarScreens")) return;
    var keys = new List<MyIniKey>();
    _ini.GetKeys("RadarScreens", keys);
    foreach (var key in keys)
    {
        string spec = key.Name.Trim(), blockName = spec;
        string view = _ini.Get(key).ToString("scope").Trim().ToLowerInvariant();
        if (view.Length == 0) view = "scope";

        int idx = 0, slash = spec.LastIndexOf('/');
        if (slash >= 0 && int.TryParse(spec.Substring(slash + 1).Trim(), out idx))
            blockName = spec.Substring(0, slash).Trim();
        if (blockName.Length == 0) continue;

        var target = ViewList(view);
        if (target == null) { _screenWarnings.Add("'" + spec + "': unknown view '" + view + "' (use scope/info/wide/weapons)"); continue; }

        var block = FindSurfaceProvider(blockName);
        var provider = block as IMyTextSurfaceProvider;
        if (provider == null) { _screenWarnings.Add("'" + blockName + "': no block with screens found"); continue; }
        if (provider.SurfaceCount == 0) { _screenWarnings.Add("'" + block.CustomName + "' has no screens"); continue; }
        if (idx < 0 || idx >= provider.SurfaceCount)
        { _screenWarnings.Add("'" + block.CustomName + "' has screens 0-" + (provider.SurfaceCount - 1) + ", not " + idx); continue; }

        var s = provider.GetSurface(idx);
        if (_surfaces.Contains(s) || _infoSurfaces.Contains(s) || _wideSurfaces.Contains(s) || _weaponSurfaces.Contains(s))
        { _screenWarnings.Add("'" + spec + "' is already used by a name tag"); continue; }
        target.Add(s);
    }
}

List<IMyTextSurface> ViewList(string view)
{
    switch (view)
    {
        case "scope": case "tactical": case "radar": return _surfaces;
        case "info": case "contacts": case "list": return _infoSurfaces;
        case "wide": case "strategic": return _wideSurfaces;
        case "weapons": case "weapon": case "fire": return _weaponSurfaces;
        default: return null;
    }
}

IMyTerminalBlock FindSurfaceProvider(string name)
{
    IMyTerminalBlock partial = null;
    foreach (var b in _allBlocks)
    {
        if (!(b is IMyTextSurfaceProvider)) continue;
        if (b.CustomName == name) return b;
        if (partial == null && b.CustomName.Contains(name)) partial = b;
    }
    return partial;
}

// Auto range: weapons > cameras > sensor field > Range setting.
void UpdateAutoRange()
{
    if (!_autoRange) { _range = _rangeSetting; _rangeSource = "manual"; return; }
    double max = _wcMaxRange;
    foreach (var t in _turrets) if (t.IsFunctional && t.Range > max) max = t.Range;
    foreach (var c in _turretCtl) if (c.IsFunctional && c.Range > max) max = c.Range;

    if (max >= 100) { _range = max; _rangeSource = "weapons"; }
    else if (_scanCameras && _cameras.Count > 0) { _range = _cameraRange > 0 ? _cameraRange : _rangeSetting; _rangeSource = "cameras"; }
    else if (_sensors.Count > 0 && !_network) { _range = Math.Max(100, _sensorMaxRange * 1.25); _rangeSource = "sensors"; }
    else { _range = _rangeSetting; _rangeSource = "setting"; }
}

// =============================================================================
//  CONTACT GATHERING
// =============================================================================
void GatherContacts()
{
    foreach (var s in _sensors)
    {
        if (!s.IsWorking) continue;
        _scratch.Clear();
        s.DetectedEntities(_scratch);
        foreach (var info in _scratch) Ingest(info, false);
    }

    if (_wcReady && _wcGridId != 0 && _time >= _nextWcPoll)
    {
        _nextWcPoll = _time + 0.1;
        bool hasAi = false;
        try { hasAi = _wc.HasGridAi(_wcGridId); } catch { }
        if (hasAi)
        {
            _wcThreats.Clear();
            try { _wc.GetSortedThreats(Me, _wcThreats); } catch { }
            foreach (var kv in _wcThreats) Ingest(kv.Key, true);

            _scratch.Clear();
            try { _wc.GetObstructions(Me, _scratch); } catch { }
            foreach (var info in _scratch) Ingest(info, false);

            try
            {
                var focus = _wc.GetAiFocus(_wcGridId, 0);
                if (!focus.IsEmpty()) { Ingest(focus, true); _focusDbg = "focus=" + focus.Name; }
                else _focusDbg = "focus=none";
            }
            catch { _focusDbg = "focus=err"; }
            _turretDbg = "threats=" + _wcThreats.Count;
        }
        else _focusDbg = "focus=no gridAI";
    }
    else if (_wcReady && _wcGridId == 0) _focusDbg = "focus=no WC weapons";
    if (!_wcReady) _turretDbg = "tgt=none";

    foreach (var tur in _turrets)
        if (tur.IsFunctional && tur.HasTarget) Ingest(tur.GetTargetedEntity(), false);
    foreach (var ctl in _turretCtl)
        if (ctl.IsFunctional && ctl.HasTarget) Ingest(ctl.GetTargetedEntity(), false);

    if (_scanCameras && _cameras.Count > 0) ScanCameras();
}

const int MaxCameraScansPerRun = 3;
const int SweepPoints = 97;
const double GoldenAngle = 2.39996323;

// Up to three cameras take a turn per run. Each first tries to re-scan the
// stalest known contact in its view (so camera-only contacts don't time out),
// otherwise it takes the next step of a spiral sweep across its whole cone.
void ScanCameras()
{
    int n = _cameras.Count;
    double maxDist = _cameraRange > 0 ? _cameraRange : _range;
    for (int i = 0; i < Math.Min(n, MaxCameraScansPerRun); i++)
    {
        int ci = _camIndex++ % n;
        var cam = _cameras[ci];
        if (!cam.IsWorking || !cam.EnableRaycast) continue;
        if (RefreshTrackWithCamera(cam, maxDist)) continue;
        SweepCamera(cam, ci, maxDist);
    }
}

bool RefreshTrackWithCamera(IMyCameraBlock cam, double maxDist)
{
    MatrixD wm = cam.WorldMatrix;
    Vector3D camPos = wm.Translation;
    double cosLimit = Math.Cos(Math.Min(cam.RaycastConeLimit, 45f) * 0.95 * Math.PI / 180.0);

    Track best = null;
    Vector3D bestPoint = Vector3D.Zero;
    double bestAge = 0.5;
    foreach (var kv in _tracks)
    {
        var t = kv.Value;
        double age = _time - t.LastSeen;
        if (t.Voxel || age <= bestAge) continue;
        Vector3D dir = t.Position + t.Velocity * age - camPos;
        double dist = dir.Length();
        if (dist < 1 || dist + 50 > maxDist || cam.AvailableScanRange < dist + 50) continue;
        dir /= dist;
        if (Vector3D.Dot(dir, wm.Forward) < cosLimit) continue;
        best = t; bestAge = age;
        bestPoint = camPos + dir * (dist + 50);
    }
    if (best == null) return false;
    Ingest(cam.Raycast(bestPoint), false);
    return true;
}

void SweepCamera(IMyCameraBlock cam, int ci, double dist)
{
    if (dist < 50 || cam.AvailableScanRange < dist) return;
    float cone = Math.Max(1f, Math.Min(cam.RaycastConeLimit, 45f)) * 0.95f;
    int k = _camSweep[ci]++;
    double r = cone * Math.Sqrt(((k % SweepPoints) + 0.5) / SweepPoints), a = k * GoldenAngle;
    Ingest(cam.Raycast(dist, (float)(r * Math.Sin(a)), (float)(r * Math.Cos(a))), false);
}

// Asteroids often come back as Type=Unknown named "MyVoxelMap".
static bool IsVoxelInfo(MyDetectedEntityType type, string name)
{
    if (type == MyDetectedEntityType.Asteroid || type == MyDetectedEntityType.Planet) return true;
    return !string.IsNullOrEmpty(name) && (name.IndexOf("voxel", StringComparison.OrdinalIgnoreCase) >= 0
                                        || name.IndexOf("asteroid", StringComparison.OrdinalIgnoreCase) >= 0);
}

void Ingest(MyDetectedEntityInfo info, bool threat)
{
    if (info.IsEmpty() || _myGridIds.Contains(info.EntityId)) return;
    bool voxel = IsVoxelInfo(info.Type, info.Name);
    if (voxel && !_scanAsteroids) return;

    Track t;
    if (!_tracks.TryGetValue(info.EntityId, out t)) _tracks[info.EntityId] = t = new Track();
    t.Id = info.EntityId;
    t.Name = string.IsNullOrEmpty(info.Name) ? info.Type.ToString() : info.Name;
    t.Position = info.Position;
    t.Velocity = info.Velocity;
    t.Type = info.Type;
    t.Voxel = voxel;
    t.LastSeen = _time;
    t.Remote = false;
    // Sticky: a WeaponCore threat flag or a known relation isn't downgraded later.
    if (threat) t.Threat = true;
    if (info.Relationship != MyRelationsBetweenPlayerAndBlock.NoOwnership) t.Relation = info.Relationship;
}

void Prune()
{
    _deadIds.Clear();
    foreach (var kv in _tracks) if (_time - kv.Value.LastSeen > _holdSeconds) _deadIds.Add(kv.Key);
    foreach (var id in _deadIds) _tracks.Remove(id);
}

// =============================================================================
//  IGC RADAR NET   "SKP2#<ownerId>#<self entry>#<entry>|<entry>..."
//  Entry: id;x;y;z;relation;type;threat;name. Each ship also sends itself.
//  Plain entry lists from older versions are still understood.
// =============================================================================
bool HasBroadcastingAntenna()
{
    foreach (var a in _antennas) if (a.IsWorking && a.EnableBroadcasting) return true;
    return false;
}

void BroadcastNetwork()
{
    if (!_network || _time < _nextBroadcast) return;
    _nextBroadcast = _time + 0.5;
    if (!HasBroadcastingAntenna()) return;

    var sb = _sb;
    sb.Clear();
    sb.Append("SKP2#").Append(Me.OwnerId).Append('#');
    var grid = Me.CubeGrid;
    AppendEntry(sb, grid.EntityId, grid.WorldAABB.Center, (int)MyRelationsBetweenPlayerAndBlock.Owner,
        grid.GridSize > 1f ? MyDetectedEntityType.LargeGrid : MyDetectedEntityType.SmallGrid, false, grid.CustomName);
    sb.Append('#');
    bool first = true;
    foreach (var kv in _tracks)
    {
        var t = kv.Value;
        if (t.Remote) continue;
        if (!first) sb.Append('|');
        first = false;
        AppendEntry(sb, t.Id, t.Position, (int)t.Relation, t.Type, t.Threat, t.Name);
    }
    IGC.SendBroadcastMessage(_networkTag, sb.ToString());
}

static void AppendEntry(StringBuilder sb, long id, Vector3D p, int rel, MyDetectedEntityType type, bool threat, string name)
{
    sb.Append(id).Append(';').Append((long)Math.Round(p.X)).Append(';').Append((long)Math.Round(p.Y)).Append(';')
      .Append((long)Math.Round(p.Z)).Append(';').Append(rel).Append(';').Append((int)type).Append(';').Append(threat ? 1 : 0).Append(';');
    if (!string.IsNullOrEmpty(name)) sb.Append(name.Replace(';', ' ').Replace('|', ' ').Replace('#', ' '));
}

void ReceiveNetwork()
{
    if (!_network || _listener == null) return;
    while (_listener.HasPendingMessage)
    {
        var data = _listener.AcceptMessage().Data as string;
        if (string.IsNullOrEmpty(data)) continue;
        if (data.StartsWith("SKP2#"))
        {
            var parts = data.Split('#');
            if (parts.Length < 4) continue;
            long owner;
            long.TryParse(parts[1], out owner);
            int selfRel = (int)(owner != 0 && owner == Me.OwnerId ? MyRelationsBetweenPlayerAndBlock.Owner : MyRelationsBetweenPlayerAndBlock.FactionShare);
            IngestNetEntry(parts[2], selfRel);
            if (parts[3].Length > 0) foreach (var e in parts[3].Split('|')) IngestNetEntry(e, -1);
        }
        else foreach (var e in data.Split('|')) IngestNetEntry(e, -1);
    }
}

void IngestNetEntry(string entry, int relOverride)
{
    var f = entry.Split(';');
    long id, x, y, z; int rel, type;
    if (f.Length < 6 || !long.TryParse(f[0], out id) || !long.TryParse(f[1], out x) || !long.TryParse(f[2], out y)
        || !long.TryParse(f[3], out z) || !int.TryParse(f[4], out rel) || !int.TryParse(f[5], out type)) return;
    if (_myGridIds.Contains(id)) return;

    var etype = (MyDetectedEntityType)type;
    string name = f.Length >= 8 && f[7].Length > 0 ? f[7] : etype.ToString();
    bool voxel = IsVoxelInfo(etype, name);
    if (voxel && !_scanAsteroids) return;

    Track t;
    if (!_tracks.TryGetValue(id, out t)) _tracks[id] = t = new Track();
    else if (!t.Remote && _time - t.LastSeen < 1.0) return; // our own sighting is fresher
    t.Id = id;
    t.Position = new Vector3D(x, y, z);
    t.Velocity = Vector3D.Zero;
    t.Relation = (MyRelationsBetweenPlayerAndBlock)(relOverride >= 0 ? relOverride : rel);
    t.Type = etype;
    t.Name = name;
    t.Voxel = voxel;
    t.LastSeen = _time;
    t.Remote = true;
    if (f.Length >= 7 && f[6] == "1") t.Threat = true;
}

// =============================================================================
//  RENDERING
// =============================================================================
// UI scale: 1.0 on a 512 px LCD, smaller on cockpit screens.
static float UiScale(Vector2 view) { return MathHelper.Clamp(Math.Min(view.X, view.Y) / 512f, 0.25f, 4f); }

MySpriteDrawFrame BeginFrame(IMyTextSurface s, out Vector2 pad, out Vector2 view, out float k)
{
    view = s.SurfaceSize;
    pad = (s.TextureSize - view) * 0.5f;
    k = UiScale(view);
    var frame = s.DrawFrame();
    frame.Add(new MySprite(SpriteType.TEXTURE, "SquareSimple", pad + view * 0.5f, view, _colBackground));
    return frame;
}

void DrawAll()
{
    MatrixD m = _reference != null ? _reference.WorldMatrix : MatrixD.Identity;
    foreach (var s in _surfaces)
        if (_reference == null) DrawNotice(s); else DrawScope(s, m, _range, _projAngle, null, false);
    foreach (var s in _wideSurfaces)
        if (_reference == null) DrawNotice(s); else DrawScope(s, m, _wideRange, _wideAngle, "WIDE", true);
    foreach (var s in _infoSurfaces) DrawInfo(s, m);
    foreach (var s in _weaponSurfaces) DrawWeapons(s);
}

void DrawNotice(IMyTextSurface surface)
{
    Vector2 pad, view; float k;
    var frame = BeginFrame(surface, out pad, out view, out k);
    AddText(frame, "NO COCKPIT / REMOTE", pad + new Vector2(view.X * 0.5f, view.Y * 0.4f), 0.8f * k, _colAlert, TextAlignment.CENTER);
    AddText(frame, "Add a cockpit, control seat or remote control", pad + new Vector2(view.X * 0.5f, view.Y * 0.4f + 34 * k), 0.45f * k, _colText, TextAlignment.CENTER);
    frame.Dispose();
}

string DetectionWarning()
{
    if ((_scanCameras && _cameras.Count > 0) || _turrets.Count > 0 || _turretCtl.Count > 0 || _wcGridId != 0 || _network) return null;
    return _sensors.Count > 0 ? "SENSORS ONLY (~" + Math.Round(_sensorMaxRange) + " m)" : "NO DETECTION SOURCES";
}

void DrawScope(IMyTextSurface surface, MatrixD m, double range, double projAngle, string title, bool flat)
{
    Vector2 pad, view; float k;
    var frame = BeginFrame(surface, out pad, out view, out k);
    Vector2 center = pad + view * 0.5f;
    float lw = Math.Max(1f, 1.2f * k);
    float tilt = MathHelper.Clamp((float)Math.Cos(projAngle * Math.PI / 180.0), 0.2f, 1f);
    float scope = Math.Min(view.X * 0.46f, view.Y * 0.42f / tilt);

    DrawEllipse(frame, center, scope, scope * tilt, _colGrid, 48, Math.Max(1f, 1.6f * k));
    DrawEllipse(frame, center, scope * 0.75f, scope * 0.75f * tilt, _colGridFaint, 40, lw);
    DrawEllipse(frame, center, scope * 0.50f, scope * 0.50f * tilt, _colGridFaint, 32, lw);
    DrawEllipse(frame, center, scope * 0.25f, scope * 0.25f * tilt, _colGridFaint, 24, lw);
    DrawLine(frame, new Vector2(center.X - scope, center.Y), new Vector2(center.X + scope, center.Y), _colGridFaint, lw);
    DrawLine(frame, new Vector2(center.X, center.Y - scope * tilt), new Vector2(center.X, center.Y + scope * tilt), _colGridFaint, lw);

    float sweep = (float)(_time % 4.0 / 4.0 * Math.PI * 2.0);
    DrawLine(frame, center, center + new Vector2((float)Math.Sin(sweep) * scope, -(float)Math.Cos(sweep) * scope * tilt), _colSweep, Math.Max(1f, 2f * k));

    float top = pad.Y + 6 * k;
    AddText(frame, "RANGE " + FormatDist(range), new Vector2(center.X, top), 0.55f * k, _colGrid, TextAlignment.CENTER);
    if (title != null) AddText(frame, title, new Vector2(pad.X + 6 * k, top), 0.5f * k, _colGrid, TextAlignment.LEFT);
    string warn = _configError.Length > 0 ? "CONFIG ERROR - SEE PB" : DetectionWarning();
    if (warn != null) AddText(frame, warn, new Vector2(center.X, top + 22 * k), 0.45f * k, _colAlert, TextAlignment.CENTER);

    frame.Add(new MySprite(SpriteType.TEXTURE, "Triangle", center, new Vector2(16, 18) * k, _colOwnShip, null, TextAlignment.CENTER, 0f));

    foreach (var kv in _tracks) DrawBlip(frame, kv.Value, m, center, scope, tilt, range, flat, k);

    float bottom = pad.Y + view.Y - 22 * k;
    Track tgt = CurrentTarget();
    if (tgt != null)
        AddText(frame, (_manualTarget ? "LOCK (MAN) " : "LOCK ") + Trunc(ContactLabel(tgt), 16) + " " + FormatDist(Vector3D.Distance(tgt.Position, _origin)),
            new Vector2(center.X, bottom - 20 * k), 0.42f * k, _colLock, TextAlignment.CENTER);
    AddText(frame, SourceLine(), new Vector2(pad.X + 6 * k, bottom), 0.4f * k, _colGridFaint, TextAlignment.LEFT);
    AddText(frame, "CONTACTS " + _tracks.Count, new Vector2(pad.X + view.X - 6 * k, bottom), 0.45f * k, _colGrid, TextAlignment.RIGHT);
    frame.Dispose();
}

void DrawBlip(MySpriteDrawFrame frame, Track t, MatrixD m, Vector2 center, float scope, float tilt, double range, bool flat, float k)
{
    Vector3D rel = t.Position - _origin;
    double right = Vector3D.Dot(rel, m.Right), fwd = Vector3D.Dot(rel, m.Forward), up = Vector3D.Dot(rel, m.Up);
    double ground = Math.Sqrt(right * right + fwd * fwd);
    Color c = RelationColor(t);
    bool isTarget = _wpnTargetId != 0 && t.Id == _wpnTargetId;

    // Off-scope: marker on the rim in the contact's direction.
    if (ground > range)
    {
        double gx = ground > 0.001 ? right / ground : 0.0, gy = ground > 0.001 ? fwd / ground : 1.0;
        Vector2 edge = center + new Vector2((float)gx * scope, -(float)gy * scope * tilt);
        frame.Add(new MySprite(SpriteType.TEXTURE, "Circle", edge, new Vector2(7, 7) * k, c));
        if (isTarget) DrawEllipse(frame, edge, 11f * k, 11f * k, _colLock, 4, Math.Max(1f, 1.4f * k));
        if (_showLabels)
            AddText(frame, Trunc(ContactLabel(t), 14) + " " + FormatDist(rel.Length()), edge + new Vector2(0, -12 * k), 0.32f * k, c, TextAlignment.CENTER);
        return;
    }

    Vector2 g = new Vector2(center.X + (float)(right / range) * scope, center.Y - (float)(fwd / range) * scope * tilt);
    float alt = flat ? 0f : MathHelper.Clamp((float)(up / range) * scope, -scope * 0.6f, scope * 0.6f);
    Vector2 blip = new Vector2(g.X, g.Y - alt);
    if (!flat)
    {
        DrawLine(frame, g, blip, new Color(c.R, c.G, c.B, 200), Math.Max(1f, 1.6f * k));
        frame.Add(new MySprite(SpriteType.TEXTURE, "Circle", g, new Vector2(4, 4) * k, c));
    }

    // Square = voxel, triangle = grid, circle = anything else.
    string icon = "Circle";
    Vector2 isize = new Vector2(9, 9) * k;
    if (t.Voxel) icon = "SquareSimple";
    else if (t.Type == MyDetectedEntityType.LargeGrid || t.Type == MyDetectedEntityType.SmallGrid) { icon = "Triangle"; isize = new Vector2(11, 12) * k; }
    frame.Add(new MySprite(SpriteType.TEXTURE, icon, blip, isize, c));

    if (t.Threat) frame.Add(new MySprite(SpriteType.TEXTURE, "CircleHollow", blip, new Vector2(22, 22) * k, _colLock));
    if (isTarget) DrawEllipse(frame, blip, 16f * k, 16f * k, _colLock, 4, Math.Max(1f, 1.8f * k)); // diamond
    if (_showLabels)
        AddText(frame, Trunc(ContactLabel(t), 18), new Vector2(blip.X + 8 * k, blip.Y - 9 * k), 0.35f * k, c, TextAlignment.LEFT);
}

// ---- Contact list ([RadarInfo]) --------------------------------------------
void DrawInfo(IMyTextSurface surface, MatrixD m)
{
    Vector2 pad, view; float k;
    var frame = BeginFrame(surface, out pad, out view, out k);
    float w = view.X, h = view.Y;
    AddText(frame, "RADAR CONTACTS", new Vector2(pad.X + w * 0.5f, pad.Y + 6 * k), 0.7f * k, _colText, TextAlignment.CENTER);
    AddText(frame, "SORT:" + _sortMode.ToUpperInvariant(), new Vector2(pad.X + w - 6 * k, pad.Y + 12 * k), 0.38f * k, _colTextFaint, TextAlignment.RIGHT);

    BuildSortedList(false);

    float colSel = pad.X + 6 * k, colName = pad.X + 20 * k, colDist = pad.X + w * 0.56f, colBrg = pad.X + w * 0.74f, colType = pad.X + w * 0.88f;
    float y = pad.Y + 40 * k, hs = 0.42f * k;
    AddText(frame, "CONTACT", new Vector2(colName, y), hs, _sortMode == "name" ? _colText : _colTextFaint, TextAlignment.LEFT);
    AddText(frame, "DIST", new Vector2(colDist, y), hs, _sortMode == "distance" ? _colText : _colTextFaint, TextAlignment.LEFT);
    AddText(frame, "BRG", new Vector2(colBrg, y), hs, _colTextFaint, TextAlignment.LEFT);
    AddText(frame, "TYP", new Vector2(colType, y), hs, _sortMode == "type" ? _colText : _colTextFaint, TextAlignment.LEFT);
    y += 22 * k;
    DrawLine(frame, new Vector2(pad.X + 6 * k, y - 4 * k), new Vector2(pad.X + w - 6 * k, y - 4 * k), _colTextFaint, Math.Max(1f, k));

    float rowH = 20f * k, rs = 0.45f * k;
    int count = Math.Min(_sortList.Count, Math.Max(1, (int)((h - (y - pad.Y) - 24 * k) / rowH)));
    for (int i = 0; i < count; i++)
    {
        var t = _sortList[i];
        Color c = RelationColor(t);
        if (_wpnTargetId != 0 && t.Id == _wpnTargetId) AddText(frame, ">", new Vector2(colSel, y), rs, _colLock, TextAlignment.LEFT);
        AddText(frame, Trunc(ContactLabel(t), 18), new Vector2(colName, y), rs, c, TextAlignment.LEFT);
        AddText(frame, FormatDist(Vector3D.Distance(t.Position, _origin)), new Vector2(colDist, y), rs, c, TextAlignment.LEFT);
        AddText(frame, _reference != null ? Bearing(t, m).ToString("000") : "---", new Vector2(colBrg, y), rs, c, TextAlignment.LEFT);
        AddText(frame, ShortType(t), new Vector2(colType, y), rs, c, TextAlignment.LEFT);
        y += rowH;
    }

    float foot = pad.Y + h - 20 * k;
    AddText(frame, _sortList.Count + " CONTACTS", new Vector2(pad.X + w * 0.5f, foot), 0.4f * k, _colText, TextAlignment.CENTER);
    if (_sortList.Count > count)
        AddText(frame, "+" + (_sortList.Count - count), new Vector2(pad.X + w - 8 * k, foot), 0.4f * k, _colTextFaint, TextAlignment.RIGHT);
    frame.Dispose();
}

// Fills _sortList in the chosen order. hostilesOnly = the list next/prev walks.
void BuildSortedList(bool hostilesOnly)
{
    _sortList.Clear();
    foreach (var kv in _tracks) if (!hostilesOnly || IsHostile(kv.Value)) _sortList.Add(kv.Value);
    _sortList.Sort(CompareForList);
}

int CompareForList(Track a, Track b)
{
    int c = 0;
    switch (_sortMode)
    {
        case "distance": break;
        case "name": c = string.Compare(ContactLabel(a), ContactLabel(b), StringComparison.OrdinalIgnoreCase); break;
        case "type": c = TypeRank(a).CompareTo(TypeRank(b)); break;
        default: c = ContactPriority(a).CompareTo(ContactPriority(b)); break;
    }
    return c != 0 ? c : CompareDistance(a, b);
}

int CompareDistance(Track a, Track b)
{
    int c = Vector3D.DistanceSquared(a.Position, _origin).CompareTo(Vector3D.DistanceSquared(b.Position, _origin));
    return c != 0 ? c : a.Id.CompareTo(b.Id);
}

// Hostiles first, then nearest - used for targeting regardless of SortMode.
int CompareThreat(Track a, Track b)
{
    int c = ContactPriority(a).CompareTo(ContactPriority(b));
    return c != 0 ? c : CompareDistance(a, b);
}

int TypeRank(Track t)
{
    if (t.Voxel) return 5;
    switch (t.Type)
    {
        case MyDetectedEntityType.LargeGrid: return 0;
        case MyDetectedEntityType.SmallGrid: return 1;
        case MyDetectedEntityType.CharacterHuman: case MyDetectedEntityType.CharacterOther: return 2;
        case MyDetectedEntityType.Meteor: return 3;
        default: return 4;
    }
}

// 0 = hostile, 1 = neutral/unidentified, 2 = friendly/own, 3 = asteroid.
int ContactPriority(Track t)
{
    if (t.Voxel) return 3;
    if (IsHostile(t)) return 0;
    return t.Relation == MyRelationsBetweenPlayerAndBlock.Neutral || t.Relation == MyRelationsBetweenPlayerAndBlock.NoOwnership ? 1 : 2;
}

bool IsHostile(Track t)
{
    return t != null && !t.Voxel && (t.Threat || t.Relation == MyRelationsBetweenPlayerAndBlock.Enemies);
}

static string FormatDist(double d)
{
    return d >= 1000 ? (d / 1000.0).ToString("0.0") + " km" : Math.Round(d) + " m";
}

string ShortType(Track t)
{
    if (t.Voxel) return "AST";
    switch (t.Type)
    {
        case MyDetectedEntityType.LargeGrid: return "LG";
        case MyDetectedEntityType.SmallGrid: return "SG";
        case MyDetectedEntityType.CharacterHuman: case MyDetectedEntityType.CharacterOther: return "CHR";
        case MyDetectedEntityType.Meteor: return "MET";
        default: return "?";
    }
}

// Bearing relative to ship forward (0 = ahead, 90 = right).
int Bearing(Track t, MatrixD m)
{
    Vector3D rel = t.Position - _origin;
    double b = Math.Atan2(Vector3D.Dot(rel, m.Right), Vector3D.Dot(rel, m.Forward)) * 180.0 / Math.PI;
    return ((int)Math.Round(b < 0 ? b + 360 : b)) % 360;
}

string ContactLabel(Track t)
{
    if (t.Voxel) return t.Type == MyDetectedEntityType.Planet ? "Planet" : "Asteroid";
    bool nameless = string.IsNullOrEmpty(t.Name) || t.Name.Equals("Unknown", StringComparison.OrdinalIgnoreCase);
    if (t.Threat && nameless) return "Hostile";
    if (nameless && (t.Type == MyDetectedEntityType.CharacterHuman || t.Relation == MyRelationsBetweenPlayerAndBlock.Owner)) return "Player";
    return t.Name;
}

Color RelationColor(Track t)
{
    if (t.Voxel) return _colVoxel;
    if (t.Type == MyDetectedEntityType.Meteor) return _colMeteor;
    if (IsFriendlyName(t.Name)) return _colFriendly;
    if (t.Threat) return _colEnemy;
    switch (t.Relation)
    {
        case MyRelationsBetweenPlayerAndBlock.Enemies: return _colEnemy;
        case MyRelationsBetweenPlayerAndBlock.Neutral: case MyRelationsBetweenPlayerAndBlock.NoOwnership: return _colNeutral;
        case MyRelationsBetweenPlayerAndBlock.Owner: return _colFriendly;
        default: return _colFaction;
    }
}

string SourceLine()
{
    return "SEN:" + _sensors.Count + " CAM:" + (_scanCameras ? _cameras.Count.ToString() : "off") + " WC:" + (_wcGridId != 0 ? "ON" : "--")
         + (_network ? (HasBroadcastingAntenna() ? " NET" : " NET(no ant)") : "");
}

// PB info panel text; rebuilt at DrawRate, echoed every run.
string BuildEcho()
{
    var sb = _sb;
    sb.Clear();
    sb.AppendLine("SKP RADAR");
    if (_time < _messageUntil && _message.Length > 0) sb.Append("> ").AppendLine(_message);
    if (_configError.Length > 0) sb.Append("\n!! CONFIG ERROR: ").AppendLine(_configError).AppendLine();
    foreach (var wmsg in _configWarnings) sb.Append("! Setting ").AppendLine(wmsg);
    foreach (var wmsg in _screenWarnings) sb.Append("! ").AppendLine(wmsg);

    sb.Append("Tracks: ").Append(_tracks.Count).Append("   Sort: ").AppendLine(_sortMode);
    sb.Append("Screens - scope: ").Append(_surfaces.Count).Append("  info: ").Append(_infoSurfaces.Count)
      .Append("  wide: ").Append(_wideSurfaces.Count).Append("  weapons: ").AppendLine(_weaponSurfaces.Count.ToString());
    sb.Append("Sensors: ").Append(_sensors.Count).Append("  Cameras: ").Append(_cameras.Count).Append(_scanCameras ? "" : " (scan off)")
      .Append("  Antennas: ").AppendLine(_antennas.Count.ToString());
    sb.Append("Turrets: ").Append(_turrets.Count).Append("  CTC: ").Append(_turretCtl.Count).Append("  ").AppendLine(_turretDbg);
    sb.Append("WeaponCore: ").Append(_wcReady ? "active" : "not found").Append("  weapons: ").Append(_wcWeapons.Count)
      .Append("  group: ").Append(_groupWeapons.Count).Append("  ").AppendLine(_focusDbg);
    sb.Append("Network: ").AppendLine(_network ? (HasBroadcastingAntenna() ? "on" : "on - NO BROADCASTING ANTENNA") : "off");
    sb.Append("Range: ").Append(FormatDist(_range)).Append(_autoRange ? " (auto: " + _rangeSource + ")" : " (manual)").AppendLine();

    Track tgt = CurrentTarget();
    sb.Append("Target: ").Append(tgt != null ? ContactLabel(tgt) : (_manualTarget ? "(waiting for contact)" : "none"))
      .AppendLine(_manualTarget ? " (manual)" : " (auto)");
    sb.Append("Weapons: ").Append(_armed ? "ARMED" : "disarmed").Append("  Aim:").Append(_aim ? "on" : "off")
      .Append("  Barrage:").Append(_barrage ? "on" : "off").AppendLine(_groupFiring ? "  FIRING" : "");
    sb.Append("Gyros: ").Append(_gyros.Count).Append("  Reference: ").AppendLine(_reference != null ? _reference.CustomName : "NONE - add a cockpit!");

    if (_surfaces.Count + _infoSurfaces.Count + _wideSurfaces.Count + _weaponSurfaces.Count == 0)
        sb.Append("\n! No screen found. Put \"").Append(_lcdTag).AppendLine("\" in an LCD's name, or use [RadarScreens].");
    string warn = DetectionWarning();
    if (warn != null)
        sb.Append("\n! ").AppendLine(warn)
          .AppendLine("  Sensors only reach ~50 m and antennas don't detect")
          .AppendLine("  anything on their own. Add cameras, turrets or")
          .AppendLine("  WeaponCore weapons, or set Network=true so")
          .AppendLine("  friendly ships share what they see.");
    sb.Append("\nRuntime: ").Append(Runtime.LastRunTimeMs.ToString("0.00")).Append(" ms");
    return sb.ToString();
}

// ----------------------------- DRAW HELPERS ---------------------------------
void DrawLine(MySpriteDrawFrame frame, Vector2 a, Vector2 b, Color color, float width)
{
    Vector2 d = b - a;
    float len = d.Length();
    if (len < 0.5f) return;
    frame.Add(new MySprite(SpriteType.TEXTURE, "SquareSimple", (a + b) * 0.5f, new Vector2(len, width), color, null, TextAlignment.CENTER, (float)Math.Atan2(d.Y, d.X)));
}

void DrawEllipse(MySpriteDrawFrame frame, Vector2 c, float rx, float ry, Color color, int segments, float width)
{
    Vector2 prev = c + new Vector2(rx, 0);
    for (int i = 1; i <= segments; i++)
    {
        float a = (float)(i / (double)segments * Math.PI * 2.0);
        Vector2 p = c + new Vector2((float)Math.Cos(a) * rx, (float)Math.Sin(a) * ry);
        DrawLine(frame, prev, p, color, width);
        prev = p;
    }
}

void AddText(MySpriteDrawFrame frame, string text, Vector2 pos, float scale, Color color, TextAlignment align)
{
    var s = MySprite.CreateText(text, "White", color, scale, align);
    s.Position = pos;
    frame.Add(s);
}

static string Trunc(string s, int n) { return !string.IsNullOrEmpty(s) && s.Length > n ? s.Substring(0, n) : (s ?? ""); }

// =============================================================================
//  WEAPONS FIRE CONTROL
//  Picks a target (automatic, or chosen with next/prev/target), designates it
//  to WeaponCore, optionally slews the ship onto it, and in Barrage spreads
//  every weapon across all locked targets. Nothing actuates unless ARMED.
// =============================================================================
void UpdateWeapons()
{
    UpdateWeaponTarget();
    Track current = CurrentTarget();
    bool aiming = false, firing = false;
    if (_armed && _reference != null)
    {
        if (current != null && _wcReady) { try { _wc.SetAiFocus(Me, current.Id, 0); } catch { } }
        if (_aim && current != null) { AimAt(current.Position); aiming = true; }
        if (_barrage) firing = DistributeBarrage();
    }
    if (!aiming) ReleaseGyros(false);
    if (!firing) { SetGroupFire(false); _barrageTargetCount = 0; }
}

// Every target gets a share of the weapon group; leftovers go to the top
// threats. A manually chosen target is served first.
bool DistributeBarrage()
{
    if (!_wcReady || _groupWeapons.Count == 0) return false;
    var targets = _sortList;
    targets.Clear();
    foreach (var kv in _tracks) if (IsEngageable(kv.Value)) targets.Add(kv.Value);
    if (targets.Count == 0) { _barrageTargetCount = 0; return false; }
    targets.Sort(CompareThreat);
    if (_manualTarget)
    {
        int mi = targets.FindIndex(x => x.Id == _wpnTargetId);
        if (mi > 0) { var mt = targets[mi]; targets.RemoveAt(mi); targets.Insert(0, mt); }
    }
    _barrageTargetCount = targets.Count;

    int weapons = _groupWeapons.Count, baseN = weapons / targets.Count, rem = weapons % targets.Count, wi = 0;
    for (int ti = 0; ti < targets.Count && wi < weapons; ti++)
        for (int j = 0, n = baseN + (ti < rem ? 1 : 0); j < n && wi < weapons; j++)
        { try { _wc.SetWeaponTarget(_groupWeapons[wi++], targets[ti].Id, 0); } catch { } }
    SetGroupFire(true);
    return true;
}

void ClearWeaponTargets()
{
    if (!_wcReady) return;
    foreach (var w in _groupWeapons) { try { _wc.SetWeaponTarget(w, 0, 0); } catch { } }
}

Track CurrentTarget()
{
    Track t;
    return _wpnTargetId != 0 && _tracks.TryGetValue(_wpnTargetId, out t) ? t : null;
}

void UpdateWeaponTarget()
{
    if (_manualTarget)
    {
        Track mt = CurrentTarget();
        if (mt != null && IsHostile(mt)) { _manualMissingSince = -1; return; }
        if (_manualMissingSince < 0) _manualMissingSince = _time;
        if (_time - _manualMissingSince < Math.Max(_holdSeconds, 5)) return; // give it a moment to reappear
        _manualTarget = false;
        _manualMissingSince = -1;
        _wpnTargetId = 0;
        Notify("Manual target lost - back to automatic targeting.");
        Save();
    }
    if (_reference == null) { _wpnTargetId = 0; return; }

    bool currentValid = IsEngageable(CurrentTarget());
    if (currentValid && _time - _wpnLatchTime < _targetLatch) return;

    Track best = null;
    foreach (var kv in _tracks)
        if (IsEngageable(kv.Value) && (best == null || CompareThreat(kv.Value, best) < 0)) best = kv.Value;
    if (best != null) { if (best.Id != _wpnTargetId) { _wpnTargetId = best.Id; _wpnLatchTime = _time; } }
    else if (!currentValid) _wpnTargetId = 0;
}

bool IsEngageable(Track t)
{
    return IsHostile(t) && _time - t.LastSeen <= _holdSeconds && Vector3D.Distance(t.Position, _origin) <= _fireRange;
}

void CycleTarget(int dir)
{
    BuildSortedList(true);
    if (_sortList.Count == 0) { Notify("No hostile contacts to target."); return; }
    int idx = _sortList.FindIndex(x => x.Id == _wpnTargetId);
    idx = idx < 0 ? (dir > 0 ? 0 : _sortList.Count - 1) : (idx + dir + _sortList.Count) % _sortList.Count;
    SetManualTarget(_sortList[idx]);
}

void TargetByName(string name)
{
    Track best = null;
    double bestDist = double.MaxValue;
    foreach (var kv in _tracks)
    {
        var t = kv.Value;
        if (!IsHostile(t) || !(ContactLabel(t).ToLowerInvariant().Contains(name) || t.Id.ToString() == name)) continue;
        double d = Vector3D.DistanceSquared(t.Position, _origin);
        if (d < bestDist) { best = t; bestDist = d; }
    }
    if (best == null) Notify("No hostile contact matching '" + name + "'."); else SetManualTarget(best);
}

void SetManualTarget(Track t)
{
    _manualTarget = true;
    _manualMissingSince = -1;
    _wpnTargetId = t.Id;
    _wpnLatchTime = _time;
    Notify("Target: " + ContactLabel(t) + " (" + FormatDist(Vector3D.Distance(t.Position, _origin)) + ")");
    Save();
}

void AutoTarget()
{
    _manualTarget = false;
    _manualMissingSince = -1;
    _wpnLatchTime = -1e9;
    Notify("Automatic targeting.");
    Save();
}

// --- Gyro aiming (Whip's rotation-angle method) -----------------------------
void AimAt(Vector3D targetPos)
{
    if (_gyros.Count == 0) return;
    MatrixD wm = _reference.WorldMatrix;
    Vector3D dir = targetPos - wm.Translation;
    if (dir.LengthSquared() < 1) return;
    Vector3D local = Vector3D.TransformNormal(Vector3D.Normalize(dir), MatrixD.Transpose(wm));
    Vector3D flat = new Vector3D(local.X, 0, local.Z);
    double flatLen = flat.Length(), yaw, pitch;
    if (flatLen < 1e-9) { yaw = 0; pitch = local.Y > 0 ? Math.PI / 2 : -Math.PI / 2; }
    else
    {
        yaw = Math.Acos(MathHelperD.Clamp(-flat.Z / flatLen, -1, 1)) * Math.Sign(local.X);
        pitch = Math.Acos(MathHelperD.Clamp(flatLen / local.Length(), -1, 1)) * Math.Sign(local.Y);
    }
    // If aiming turns the ship the WRONG way for your build, flip the sign on -pitch.
    var world = Vector3D.TransformNormal(new Vector3D(-pitch * _aimGain, yaw * _aimGain, 0), wm);
    foreach (var g in _gyros)
    {
        if (!g.IsFunctional) continue;
        var gl = Vector3D.TransformNormal(world, MatrixD.Transpose(g.WorldMatrix));
        g.Pitch = (float)gl.X; g.Yaw = (float)gl.Y; g.Roll = (float)gl.Z;
        g.GyroOverride = true;
    }
    _gyrosOverridden = true;
}

// force = release even if this run didn't set the override (startup, aim off).
void ReleaseGyros(bool force)
{
    if (!_gyrosOverridden && !force) return;
    _gyrosOverridden = false;
    foreach (var g in _gyros) { g.Pitch = 0; g.Yaw = 0; g.Roll = 0; g.GyroOverride = false; }
}

void SetGroupFire(bool on)
{
    if (on == _groupFiring) return;
    _groupFiring = on;
    if (!_wcReady) return;
    foreach (var w in _groupWeapons) { try { _wc.ToggleWeaponFire(w, on, true); } catch { } }
}

// --- Weapons panel ([RadarWeapons]) -----------------------------------------
void DrawWeapons(IMyTextSurface surface)
{
    Vector2 pad, view; float k;
    var frame = BeginFrame(surface, out pad, out view, out k);
    k = MathHelper.Clamp(Math.Min(view.X / 512f, view.Y / 580f), 0.2f, 4f); // 16 rows need ~580 px at full size
    float w = view.X, left = pad.X + 10 * k;
    Color lbl = _colText, off = _colInactive;

    AddText(frame, "WEAPONS CONTROL", new Vector2(pad.X + w * 0.5f, pad.Y + 6 * k), 0.7f * k, _colText, TextAlignment.CENTER);
    float y = pad.Y + 40 * k;
    DrawLine(frame, new Vector2(pad.X + 6 * k, y - 6 * k), new Vector2(pad.X + w - 6 * k, y - 6 * k), _colTextFaint, Math.Max(1f, k));

    Track tgt = CurrentTarget();
    double dist = tgt != null ? Vector3D.Distance(tgt.Position, _origin) : 0;
    bool inRange = tgt != null && dist <= _fireRange;
    Color tc = tgt != null ? RelationColor(tgt) : off;

    y = WpnLine(frame, "WC API:", _wcReady ? "READY" : "NOT FOUND", left, w, y, k, lbl, _wcReady ? _colOk : off);
    y = WpnLine(frame, "State:", _armed ? "ARMED" : "DISARMED", left, w, y, k, lbl, _armed ? _colAlert : off);
    y = WpnLine(frame, "Group:", string.IsNullOrWhiteSpace(_weaponGroup) ? "(all WC)" : Trunc(_weaponGroup, 16), left, w, y, k, lbl, lbl);
    y = WpnLine(frame, "Weapons:", _groupWeapons.Count.ToString(), left, w, y, k, lbl, lbl);
    y = WpnLine(frame, "FireRange:", FormatDist(_fireRange), left, w, y, k, lbl, lbl);
    y = WpnLine(frame, "Latch:", _targetLatch.ToString("0.0") + " s", left, w, y, k, lbl, lbl) + 10 * k;
    y = WpnLine(frame, "Target:", tgt != null ? Trunc(ContactLabel(tgt), 16) : (_manualTarget ? "(searching)" : "None"), left, w, y, k, lbl, tc);
    y = WpnLine(frame, "Select:", _manualTarget ? "MANUAL" : "AUTO", left, w, y, k, lbl, _manualTarget ? _colLock : off);
    y = WpnLine(frame, "Seen:", tgt != null ? (_time - tgt.LastSeen).ToString("0.0") + " s" : "-", left, w, y, k, lbl, tc);
    y = WpnLine(frame, "Range:", tgt != null ? FormatDist(dist) : "-", left, w, y, k, lbl, tc);
    y = WpnLine(frame, "InRange:", tgt != null ? (inRange ? "YES" : "NO") : "-", left, w, y, k, lbl, inRange ? _colOk : off);
    y = WpnLine(frame, "TargetId:", tgt != null ? tgt.Id.ToString() : "-", left, w, y, k, lbl, tc) + 10 * k;
    y = WpnLine(frame, "Aim:", _aim ? "ON" : "OFF", left, w, y, k, lbl, _aim ? _colOk : off);
    y = WpnLine(frame, "Barrage:", _barrage ? "ON" : "OFF", left, w, y, k, lbl, _barrage ? _colOk : off);
    y = WpnLine(frame, "Spread:", _barrageTargetCount > 0 ? _barrageTargetCount + " tgt / " + _groupWeapons.Count + " wpn" : "-", left, w, y, k, lbl, _barrageTargetCount > 0 ? _colOk : off);
    WpnLine(frame, "Firing:", _groupFiring ? "FIRING" : "hold", left, w, y, k, lbl, _groupFiring ? _colAlert : off);
    frame.Dispose();
}

float WpnLine(MySpriteDrawFrame frame, string label, string value, float left, float w, float y, float k, Color lblC, Color valC)
{
    AddText(frame, label, new Vector2(left, y), 0.62f * k, lblC, TextAlignment.LEFT);
    AddText(frame, value, new Vector2(left + w * 0.42f, y), 0.62f * k, valC, TextAlignment.LEFT);
    return y + 32 * k;
}

// =============================================================================
//  TRACK MODEL
// =============================================================================
class Track
{
    public long Id;
    public string Name;
    public Vector3D Position, Velocity;
    public MyDetectedEntityType Type;
    public MyRelationsBetweenPlayerAndBlock Relation;
    public double LastSeen;
    public bool Remote, Threat, Voxel;
}

// =============================================================================
//  WEAPONCORE  (WcPbApi) - only the methods this script uses. Activate()
//  returns false without the mod; a mismatched delegate silently does nothing.
// =============================================================================
public class WcPbApi
{
    Action<IMyTerminalBlock, IDictionary<MyDetectedEntityInfo, float>> _getSortedThreats;
    Action<IMyTerminalBlock, ICollection<MyDetectedEntityInfo>> _getObstructions;
    Func<long, int, MyDetectedEntityInfo> _getAiFocus;
    Func<long, bool> _hasGridAi;
    Func<IMyTerminalBlock, bool> _hasCoreWeapon;
    Func<IMyTerminalBlock, long, int, bool> _setAiFocus;
    Action<IMyTerminalBlock, bool, bool, int> _toggleWeaponFire;
    Action<IMyTerminalBlock, long, int> _setWeaponTarget;
    Func<IMyTerminalBlock, int, float> _getMaxWeaponRange;

    public bool Activate(IMyTerminalBlock pb)
    {
        var prop = pb.GetProperty("WcPbAPI");
        if (prop == null) return false;
        var d = prop.As<IReadOnlyDictionary<string, Delegate>>().GetValue(pb);
        if (d == null) return false;
        Assign(d, "GetSortedThreats", ref _getSortedThreats);
        Assign(d, "GetObstructions", ref _getObstructions);
        Assign(d, "GetAiFocus", ref _getAiFocus);
        Assign(d, "HasGridAi", ref _hasGridAi);
        Assign(d, "HasCoreWeapon", ref _hasCoreWeapon);
        Assign(d, "SetAiFocus", ref _setAiFocus);
        Assign(d, "ToggleWeaponFire", ref _toggleWeaponFire);
        Assign(d, "SetWeaponTarget", ref _setWeaponTarget);
        Assign(d, "GetMaxWeaponRange", ref _getMaxWeaponRange);
        return true;
    }

    void Assign<T>(IReadOnlyDictionary<string, Delegate> d, string name, ref T field) where T : class
    {
        Delegate del;
        if (d.TryGetValue(name, out del)) field = del as T;
    }

    public void GetSortedThreats(IMyTerminalBlock pb, IDictionary<MyDetectedEntityInfo, float> col) { if (_getSortedThreats != null) _getSortedThreats(pb, col); }
    public void GetObstructions(IMyTerminalBlock pb, ICollection<MyDetectedEntityInfo> col) { if (_getObstructions != null) _getObstructions(pb, col); }
    public MyDetectedEntityInfo GetAiFocus(long gridId, int priority) { return _getAiFocus != null ? _getAiFocus(gridId, priority) : new MyDetectedEntityInfo(); }
    public bool HasGridAi(long gridId) { return _hasGridAi != null && _hasGridAi(gridId); }
    public bool HasCoreWeapon(IMyTerminalBlock block) { return _hasCoreWeapon != null && _hasCoreWeapon(block); }
    public bool SetAiFocus(IMyTerminalBlock pb, long target, int priority) { return _setAiFocus != null && _setAiFocus(pb, target, priority); }
    public void ToggleWeaponFire(IMyTerminalBlock weapon, bool on, bool allWeapons) { if (_toggleWeaponFire != null) _toggleWeaponFire(weapon, on, allWeapons, 0); }
    public void SetWeaponTarget(IMyTerminalBlock weapon, long target, int weaponId) { if (_setWeaponTarget != null) _setWeaponTarget(weapon, target, weaponId); }
    public float GetMaxWeaponRange(IMyTerminalBlock weapon, int weaponId) { return _getMaxWeaponRange != null ? _getMaxWeaponRange(weapon, weaponId) : 0f; }
}
