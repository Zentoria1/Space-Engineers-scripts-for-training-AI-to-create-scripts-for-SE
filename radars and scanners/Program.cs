// Graphical Radar Script v2 rev 5 
// Infinite Camera and Monospace support! 

// Original script by alex-thatradarguy 
// Large chunks of fixed backend logic by sysigy 
// Monospace colours and running symbol by Whiplash141 
// Compiled by dagriefaa 

// -- BASIC SETTINGS -- // 

using Sandbox.ModAPI.Ingame;
using System;
using System.Collections.Generic;
using VRageMath;

int cameraRange = 200;                           // Camera raycast range 
int sensorRange = 50;                              // Sensor forward detection range 
int sensorTopExtent = 10;                         // Sensor upward detection range 
int sensorBottomExtent = 15;                   // Sensor downward detection range 
double sweepLineRPM = 12.5;                 // Rotation speed of the rotor 
bool upsideDown = false;                        // Is rotor upside down relative to panel? 

string radarBlocksName = "Radar - ";        // Names of all radar blocks must contain this 

string radarRotorName = "Rotor";                // Radar - Rotor 
string radarScreenName = "Display";         // Radar - Display 
string radarMultiName = "Multi";                 // Radar - Multi [NW/NE/SW/SE] 
string radarStatsName = "Status";             // Radar - Status 

string sensorName = "Sensor";
string cameraName = "Camera";
List<string> detectorDirections = new List<String>(new String[]{
        " Front",            // Radar - [Sensor/Camera] Front 
        " Back",            // Radar - [Sensor/Camera] Back 
        " Left",              // Radar - [Sensor/Camera] Left 
        " Right"            // Radar - [Sensor/Camera] Right 
        });

// -- ADVANCED SETTINGS -- // 

int refreshRate = 10;                            // Script runs/second. 60 is realtime. 
int stepRange = 100;                           // Increase/decrease camera range by this much with inc c/dec c 
float stepRPM = 2.5f;                         // Increase/decrease rotor RPM by this much with inc r/dec r 
int screenResolution = 59;                 // 40x lowest, 160x highest; may cause freezes at high resolutions. 
bool multiScreenMode = false;           // Enables spanning over 4 screens [NW/NE/SW/SE]. 4x normal resolution; may cause freezes at high resolutions. 
int rangeMultiplier = 2;                       // metres/millisecond delay of camera. Change for modded cameras. 

bool useCameras = true;
bool useSensors = true;

// -- COLOURS -- // 
// CreateCustomColor(red, green, blue), from (0, 0, 0) [black] to (7, 7, 7) [white] 
// Alternatively: green, blue, red, yellow, white, lightGray, mediumGray, darkGray 

char colorTargets = CreateCustomColor(0, 7, 0);
char colorHostileTargets = CreateCustomColor(7, 0, 0);
char colorInnerBackground = CreateCustomColor(1, 1, 1);
char colorOuterBackground = CreateCustomColor(1, 1, 1);
char colorRings = CreateCustomColor(5, 5, 5);
char colorText = CreateCustomColor(7, 7, 0);
char colorFrontLine = CreateCustomColor(7, 7, 0);
char colorRightLine = CreateCustomColor(7, 7, 5);
char colorLeftLine = CreateCustomColor(7, 7, 5);
char colorBackLine = CreateCustomColor(7, 7, 5);

//////////////////////////////////////////////////////////////////////////////////////////////////////////////////////// 
// -- SCRIPT BODY -- ALTER AT YOUR OWN RISK -- // 

// Blocks 
List<IMyTextPanel> radarScreens = new List<IMyTextPanel>();
List<IMyTextPanel> statScreens = new List<IMyTextPanel>();
List<IMyTextPanel> spanDisplays = new List<IMyTextPanel>();
List<IMySensorBlock> sensorBlocks = new List<IMySensorBlock>();
IMyMotorStator rotor;

// Status 
bool initialized = false;
int sensors = 0;
int cameras = 0;
int ticks = 0;

string[] matrix;

// Detectors 
class Detector
{
    public bool exists;
    public int direction;
    public double angle;
    public IMySensorBlock sensor;
    public List<IMyCameraBlock> camera;
    public int cameraCount;
    public int currentCamera;
    public int bufferTime;
    public int timeUntilPing;
};

Detector detectorFront = new Detector();
Detector detectorBack = new Detector();
Detector detectorLeft = new Detector();
Detector detectorRight = new Detector();

// Display cache 
float fontSize = 1f;
int center;
int[] lineXFront;
int[] lineYFront;
int[] oldLineXFront;
int[] oldLineYFront;
int[] lineXBack;
int[] lineYBack;
int[] oldLineXBack;
int[] oldLineYBack;
int[] lineXRight;
int[] lineYRight;
int[] oldLineXRight;
int[] oldLineYRight;
int[] lineXLeft;
int[] lineYLeft;
int[] oldLineXLeft;
int[] oldLineYLeft;
SortedList<string, Target> scanData = new SortedList<string, Target>();
int rotorOffset = -90;
List<Vector3D> vecDims = new List<Vector3D>();
double iAngle = 0;
double iAngle2 = 0;

// Round pixels 
const char green = '\uE001';
const char blue = '\uE002';
const char red = '\uE003';
const char yellow = '\uE004';
const char white = '\uE006';
const char lightGray = '\uE00E';
const char mediumGray = '\uE00D';
const char darkGray = '\uE00F';

public struct Target
{
    public long BirthDay;
    public Vector3D Position;
    public double Distance;
    public bool hostile;
    public string Debug;
};

static char CreateCustomColor(int r, int g, int b)
{
    return (char)(0xE100 + (MathHelper.Clamp(r, 0, 7) << 6) + (MathHelper.Clamp(g, 0, 7) << 3) + MathHelper.Clamp(b, 0, 7));
}

public Program() { Runtime.UpdateFrequency = UpdateFrequency.Update1; }

void Init()
{
    int halfRes = (int)screenResolution / 2;
    center = halfRes;
    lineXFront = new int[halfRes];
    lineYFront = new int[halfRes];
    oldLineXFront = new int[halfRes];
    oldLineYFront = new int[halfRes];
    lineXBack = new int[halfRes];
    lineYBack = new int[halfRes];
    oldLineXBack = new int[halfRes];
    oldLineYBack = new int[halfRes];
    lineXRight = new int[halfRes];
    lineYRight = new int[halfRes];
    oldLineXRight = new int[halfRes];
    oldLineYRight = new int[halfRes];
    lineXLeft = new int[halfRes];
    lineYLeft = new int[halfRes];
    oldLineXLeft = new int[halfRes];
    oldLineYLeft = new int[halfRes];
    fontSize = 0.4f;

    if (screenResolution < 41) fontSize = 0.4f;
    if (screenResolution >= 41 && screenResolution <= 59) fontSize = 0.3f;
    if (screenResolution >= 60 && screenResolution <= 85) fontSize = 0.2f;
    if (screenResolution >= 86 && screenResolution <= 160) fontSize = 0.1f;

    detectorFront.direction = 0;
    detectorBack.direction = 1;
    detectorLeft.direction = 2;
    detectorRight.direction = 3;

    try
    {
        // all single screens 
        radarScreens = this.GetBlocks<IMyTextPanel>(radarBlocksName + radarScreenName);
        if (!(radarScreens == null || radarScreens.Count == 0))
        {
            //foreach(IMyTextPanel lcd in radarScreens){ // doesn't work in .NET Framework 4.0 
            for (int i = 0; i < radarScreens.Count; i++)
            {
                IMyTextPanel lcd = radarScreens[i];
                lcd.SetValue<float>("FontSize", fontSize);
                lcd.SetValue<Color>("FontColor", new Color(255, 255, 255));
                lcd.SetValue<long>("Font", 1147350002);
                lcd.ShowTextureOnScreen();
                lcd.ShowPublicTextOnScreen();
            }
        }

        // span screens 
        if (multiScreenMode)
        {
            spanDisplays = this.GetBlocks<IMyTextPanel>(radarBlocksName + radarMultiName);
            if (!(spanDisplays == null || spanDisplays.Count == 0))
            {
                //foreach(IMyTextPanel lcd in spanDisplays){ // doesn't work in .NET Framework 4.0 
                for (int i = 0; i < spanDisplays.Count; i++)
                {
                    IMyTextPanel lcd = spanDisplays[i];
                    lcd.SetValue<float>("FontSize", 0.6f);
                    lcd.SetValue<Color>("FontColor", new Color(128, 128, 128));
                    lcd.SetValue<long>("Font", 1147350002);
                    lcd.ShowTextureOnScreen();
                    lcd.ShowPublicTextOnScreen();
                }
            }
        }

        // status screens 
        statScreens = this.GetBlocks<IMyTextPanel>(radarBlocksName + radarStatsName);

        // rotors 
        rotor = this.GetBlock<IMyMotorStator>(radarBlocksName + radarRotorName);
        if (rotor == default(IMyMotorStator)) throw new Exception(string.Format("No rotor found!"));
        rotor.SetValue<float>("Torque", 30000f);
        rotor.SetValue<float>("BrakingTorque", 30000f);
        rotor.SetValue<float>("Velocity", (float)sweepLineRPM);

        // sensors 
        this.InitialiseDetector(detectorFront);
        this.InitialiseDetector(detectorBack);
        this.InitialiseDetector(detectorLeft);
        this.InitialiseDetector(detectorRight);

        if (cameras == 0 && sensors == 0) { throw new Exception("No named cameras or sensors exist."); }

        // generate LCD matrix 
        matrix = new string[screenResolution];
        for (int a = 0; a < screenResolution; a++)
        {
            matrix[a] = "";
            for (int b = 0; b < screenResolution; b++)
            {
                matrix[a] += colorOuterBackground;
            }
            matrix[a] += "\n";
        }

        Refresh();

        if (cameras > 0) { PlotBigNumber(4, 0, cameraRange); }
        if (sensors > 0) { PlotBigNumber(4, 6, sensorRange); }

        initialized = true;

    }
    catch (Exception ex)
    {
        // throw error to PB output 
        Echo(string.Format("Init Error: {0}", ex.Message));
    }
}

void InitialiseDetector(Detector detector)
{
    detector.exists = false;
    if (useSensors)
    {
        detector.sensor = this.GetBlock<IMySensorBlock>(radarBlocksName + sensorName + detectorDirections[detector.direction]);
        if (detector.sensor != default(IMySensorBlock))
        {
            detector.sensor.SetValue<float>("Left", 1f);
            detector.sensor.SetValue<float>("Right", 1f);
            detector.sensor.SetValue<float>("Top", (float)sensorTopExtent);
            detector.sensor.SetValue<float>("Bottom", (float)sensorBottomExtent);
            detector.sensor.SetValue<float>("Back", 1f);
            detector.sensor.SetValue<float>("Front", (float)sensorRange);
            detector.exists = true;
            sensors++;
        }
    }
    if (useCameras)
    {
        detector.camera = this.GetBlocks<IMyCameraBlock>(radarBlocksName + cameraName + detectorDirections[detector.direction]);
        if (detector.camera.Count > 0)
        {
            detector.cameraCount = detector.camera.Count;
            cameras += detector.camera.Count;
            for (int i = 0; i < detector.camera.Count; i++)
            {
                detector.camera[i].EnableRaycast = true;
                detector.timeUntilPing = detector.bufferTime;
            }
            detector.exists = true;
            detector.bufferTime = (int)Math.Floor(((cameraRange / rangeMultiplier) / detector.cameraCount) / 60f);
        }
    }

}

//Thank you Skleroz 
Vector3D GetVector(Vector3D vecFront, Vector3D vecPoM, double dFrontAng, double dPoMAng)
{
    Vector3D vecRes = vecFront;
    if (vecFront.Dot(vecPoM) < 0.001 && Math.Abs(Math.Cos(dFrontAng)) + Math.Abs(Math.Cos(dPoMAng)) > 0.995)
    {
        vecFront = vecFront * Math.Cos(dFrontAng);
        vecPoM = vecPoM * Math.Cos(dPoMAng);
        vecRes = Vector3D.Add(vecFront, vecPoM);
        vecRes.Normalize();
    }
    return vecRes;
}

void Render(List<IMyTextPanel> screens, string str)
{
    //foreach(IMyTextPanel lcd in screens){ // doesn't work in .NET Framework 4.0 
    for (int i = 0; i < screens.Count; i++)
    {
        IMyTextPanel lcd = screens[i];
        lcd.WritePublicText(str);
    }
}

void RenderMulti(List<IMyTextPanel> screens, string str)
{
    string topLeft = "";
    string topRight = "";
    string bottomLeft = "";
    string bottomRight = "";

    List<string> txtArray = new List<string>(str.Split('\n'));
    int totalEnd = txtArray.Count;

    for (int i = 0; i < totalEnd; i++)
    {
        if (i <= totalEnd / 2)
        {
            topLeft += txtArray[i].Substring(0, txtArray[i].Length / 2) + "\n";
            topRight += txtArray[i].Substring((txtArray[i].Length / 2)) + "\n";
        }
        else if (i > totalEnd / 2)
        {
            bottomLeft += txtArray[i].Substring(0, txtArray[i].Length / 2) + "\n";
            bottomRight += txtArray[i].Substring(txtArray[i].Length / 2) + "\n";
        }
    }

    //foreach(IMyTextPanel lcd in screens){ // doesn't work in .NET Framework 4.0 
    for (int i = 0; i < screens.Count; i++)
    {
        IMyTextPanel lcd = screens[i] as IMyTextPanel;
        if (lcd.CustomName.Contains(radarBlocksName + radarMultiName + " NW")) lcd.WritePublicText(topLeft);
        if (lcd.CustomName.Contains(radarBlocksName + radarMultiName + " NE")) lcd.WritePublicText(topRight);
        if (lcd.CustomName.Contains(radarBlocksName + radarMultiName + " SW")) lcd.WritePublicText(bottomLeft);
        if (lcd.CustomName.Contains(radarBlocksName + radarMultiName + " SE")) lcd.WritePublicText(bottomRight);
        lcd.ShowTextureOnScreen();
        lcd.ShowPublicTextOnScreen();
    }

}

void Plot(int x, int y, char c)
{
    if (x >= 0 && y >= 0 && x < screenResolution && y < screenResolution)
    {
        char[] chars = matrix[y].ToCharArray();
        chars[x] = c;
        matrix[y] = new string(chars);
    }
}

void PlotNumber(int X, int Y, int number)
{

    char iiiii = colorOuterBackground;
    char ooooo = colorText;

    if (number == 1)
    {
        Plot(X, Y + 0, iiiii); Plot(X + 1, Y + 0, ooooo); Plot(X + 2, Y + 0, iiiii);
        Plot(X, Y + 1, iiiii); Plot(X + 1, Y + 1, ooooo); Plot(X + 2, Y + 1, iiiii);
        Plot(X, Y + 2, iiiii); Plot(X + 1, Y + 2, ooooo); Plot(X + 2, Y + 2, iiiii);
        Plot(X, Y + 3, iiiii); Plot(X + 1, Y + 3, ooooo); Plot(X + 2, Y + 3, iiiii);
        Plot(X, Y + 4, iiiii); Plot(X + 1, Y + 4, ooooo); Plot(X + 2, Y + 4, iiiii);
    }
    if (number == 2)
    {
        Plot(X, Y + 0, ooooo); Plot(X + 1, Y + 0, ooooo); Plot(X + 2, Y + 0, ooooo);
        Plot(X, Y + 1, iiiii); Plot(X + 1, Y + 1, iiiii); Plot(X + 2, Y + 1, ooooo);
        Plot(X, Y + 2, ooooo); Plot(X + 1, Y + 2, ooooo); Plot(X + 2, Y + 2, ooooo);
        Plot(X, Y + 3, ooooo); Plot(X + 1, Y + 3, iiiii); Plot(X + 2, Y + 3, iiiii);
        Plot(X, Y + 4, ooooo); Plot(X + 1, Y + 4, ooooo); Plot(X + 2, Y + 4, ooooo);
    }
    if (number == 3)
    {
        Plot(X, Y + 0, ooooo); Plot(X + 1, Y + 0, ooooo); Plot(X + 2, Y + 0, ooooo);
        Plot(X, Y + 1, iiiii); Plot(X + 1, Y + 1, iiiii); Plot(X + 2, Y + 1, ooooo);
        Plot(X, Y + 2, iiiii); Plot(X + 1, Y + 2, ooooo); Plot(X + 2, Y + 2, ooooo);
        Plot(X, Y + 3, iiiii); Plot(X + 1, Y + 3, iiiii); Plot(X + 2, Y + 3, ooooo);
        Plot(X, Y + 4, ooooo); Plot(X + 1, Y + 4, ooooo); Plot(X + 2, Y + 4, ooooo);
    }
    if (number == 4)
    {
        Plot(X, Y + 0, ooooo); Plot(X + 1, Y + 0, iiiii); Plot(X + 2, Y + 0, ooooo);
        Plot(X, Y + 1, ooooo); Plot(X + 1, Y + 1, iiiii); Plot(X + 2, Y + 1, ooooo);
        Plot(X, Y + 2, ooooo); Plot(X + 1, Y + 2, ooooo); Plot(X + 2, Y + 2, ooooo);
        Plot(X, Y + 3, iiiii); Plot(X + 1, Y + 3, iiiii); Plot(X + 2, Y + 3, ooooo);
        Plot(X, Y + 4, iiiii); Plot(X + 1, Y + 4, iiiii); Plot(X + 2, Y + 4, ooooo);
    }
    if (number == 5)
    {
        Plot(X, Y + 0, ooooo); Plot(X + 1, Y + 0, ooooo); Plot(X + 2, Y + 0, ooooo);
        Plot(X, Y + 1, ooooo); Plot(X + 1, Y + 1, iiiii); Plot(X + 2, Y + 1, iiiii);
        Plot(X, Y + 2, ooooo); Plot(X + 1, Y + 2, ooooo); Plot(X + 2, Y + 2, ooooo);
        Plot(X, Y + 3, iiiii); Plot(X + 1, Y + 3, iiiii); Plot(X + 2, Y + 3, ooooo);
        Plot(X, Y + 4, ooooo); Plot(X + 1, Y + 4, ooooo); Plot(X + 2, Y + 4, ooooo);
    }
    if (number == 6)
    {
        Plot(X, Y + 0, ooooo); Plot(X + 1, Y + 0, ooooo); Plot(X + 2, Y + 0, ooooo);
        Plot(X, Y + 1, ooooo); Plot(X + 1, Y + 1, iiiii); Plot(X + 2, Y + 1, iiiii);
        Plot(X, Y + 2, ooooo); Plot(X + 1, Y + 2, ooooo); Plot(X + 2, Y + 2, ooooo);
        Plot(X, Y + 3, ooooo); Plot(X + 1, Y + 3, iiiii); Plot(X + 2, Y + 3, ooooo);
        Plot(X, Y + 4, ooooo); Plot(X + 1, Y + 4, ooooo); Plot(X + 2, Y + 4, ooooo);
    }
    if (number == 7)
    {
        Plot(X, Y + 0, ooooo); Plot(X + 1, Y + 0, ooooo); Plot(X + 2, Y + 0, ooooo);
        Plot(X, Y + 1, iiiii); Plot(X + 1, Y + 1, iiiii); Plot(X + 2, Y + 1, ooooo);
        Plot(X, Y + 2, iiiii); Plot(X + 1, Y + 2, ooooo); Plot(X + 2, Y + 2, iiiii);
        Plot(X, Y + 3, iiiii); Plot(X + 1, Y + 3, ooooo); Plot(X + 2, Y + 3, iiiii);
        Plot(X, Y + 4, iiiii); Plot(X + 1, Y + 4, ooooo); Plot(X + 2, Y + 4, iiiii);
    }
    if (number == 8)
    {
        Plot(X, Y + 0, ooooo); Plot(X + 1, Y + 0, ooooo); Plot(X + 2, Y + 0, ooooo);
        Plot(X, Y + 1, ooooo); Plot(X + 1, Y + 1, iiiii); Plot(X + 2, Y + 1, ooooo);
        Plot(X, Y + 2, ooooo); Plot(X + 1, Y + 2, ooooo); Plot(X + 2, Y + 2, ooooo);
        Plot(X, Y + 3, ooooo); Plot(X + 1, Y + 3, iiiii); Plot(X + 2, Y + 3, ooooo);
        Plot(X, Y + 4, ooooo); Plot(X + 1, Y + 4, ooooo); Plot(X + 2, Y + 4, ooooo);
    }
    if (number == 9)
    {
        Plot(X, Y + 0, ooooo); Plot(X + 1, Y + 0, ooooo); Plot(X + 2, Y + 0, ooooo);
        Plot(X, Y + 1, ooooo); Plot(X + 1, Y + 1, iiiii); Plot(X + 2, Y + 1, ooooo);
        Plot(X, Y + 2, ooooo); Plot(X + 1, Y + 2, ooooo); Plot(X + 2, Y + 2, ooooo);
        Plot(X, Y + 3, iiiii); Plot(X + 1, Y + 3, iiiii); Plot(X + 2, Y + 3, ooooo);
        Plot(X, Y + 4, ooooo); Plot(X + 1, Y + 4, ooooo); Plot(X + 2, Y + 4, ooooo);

    }
    if (number == 0)
    {
        Plot(X, Y + 0, ooooo); Plot(X + 1, Y + 0, ooooo); Plot(X + 2, Y + 0, ooooo);
        Plot(X, Y + 1, ooooo); Plot(X + 1, Y + 1, iiiii); Plot(X + 2, Y + 1, ooooo);
        Plot(X, Y + 2, ooooo); Plot(X + 1, Y + 2, iiiii); Plot(X + 2, Y + 2, ooooo);
        Plot(X, Y + 3, ooooo); Plot(X + 1, Y + 3, iiiii); Plot(X + 2, Y + 3, ooooo);
        Plot(X, Y + 4, ooooo); Plot(X + 1, Y + 4, ooooo); Plot(X + 2, Y + 4, ooooo);
    }
}

void PlotBigNumber(int startX, int startY, int num)
{
    startX = startX - 3;
    List<int> listOfInts = new List<int>();
    while (num > 0)
    {
        listOfInts.Add(num % 10);
        num = num / 10;
    }
    listOfInts.Reverse();

    for (int i = 0; i < listOfInts.Count; i++)
    {
        PlotNumber(startX + (i * 4), startY, listOfInts[i]);
    }

}

void ArgumentHandler(string args)
{
    args = args.ToLower();

    if (args == "inc c")
    {
        cameraRange = cameraRange + stepRange;
        if (cameras > 0) { PlotBigNumber(4, 0, cameraRange); }
    }
    if (args == "dec c")
    {
        if (cameraRange - stepRange > 0)
            cameraRange = cameraRange - stepRange;
        if (cameras > 0) { PlotBigNumber(4, 0, cameraRange); }
    }
    if (args == "inc r")
    {
        if (sweepLineRPM + stepRPM > 30)
            sweepLineRPM = sweepLineRPM + stepRPM;
    }
    if (args == "dec r")
    {
        if (sweepLineRPM - stepRPM > 0)
            sweepLineRPM = sweepLineRPM - stepRPM;
    }

}

void Refresh()
{
    if (radarScreens.Count > 0 || spanDisplays.Count >= 4)
    {
        string output = "\n\n";
        if (detectorFront.exists || detectorBack.exists || detectorLeft.exists)
        {
            for (int i = 0; i < scanData.Count; i++)
            {
                string key = scanData.Keys[i];
                int x = Int32.Parse(key.Split(':')[0]);
                int y = Int32.Parse(key.Split(':')[1]);
                Target target = scanData.Values[i];
                TimeSpan age = new TimeSpan(DateTime.Now.Ticks - target.BirthDay);
                if (age.TotalSeconds <= 1)
                {
                    if (target.hostile) { Plot(x, y, colorHostileTargets); }
                    else { Plot(x, y, colorTargets); }
                }
            }
        }

        for (int i = 0; i < screenResolution; i++)
        {
            output += matrix[i];
        }

        Render(radarScreens, output);
        if (multiScreenMode) RenderMulti(spanDisplays, output);

    }

    // status screen handling 
    if (statScreens.Count > 0)
    {
        string statsOutput = "[Sensors " + sensors.ToString()
            + "] [Cameras " + cameras.ToString()
            + "] [Rotor RPM: " + sweepLineRPM.ToString()
            + "]\n\n";

        if (detectorFront.exists || detectorBack.exists || detectorLeft.exists)
        {
            statsOutput += "Scan Data:\n";

            for (int i = 0; i < scanData.Count; i++)
            {
                TimeSpan age = new TimeSpan(DateTime.Now.Ticks - scanData.Values[i].BirthDay);
                if (age.TotalSeconds < 10)
                {
                    Vector3D detectedpos = scanData.Values[i].Position;
                    string gps = "GPS:Sensor Target " + i.ToString() + ":" + Math.Round(detectedpos.GetDim(0), 2).ToString() + ":"
                        + Math.Round(detectedpos.GetDim(1), 2).ToString() + ":" + Math.Round(detectedpos.GetDim(2), 2).ToString() + ":";
                    string dist = ((int)scanData.Values[i].Distance).ToString();
                    statsOutput += "[" + gps + "] [Distance: " + dist + "m] [Last seen: " + ((int)age.TotalSeconds).ToString() + "s]\n";
                }
            }
        }
        Render(statScreens, statsOutput);
    }
}

void RunDetector(Detector detector)
{
    if (detector.sensor != default(IMySensorBlock))
    {
        GetDetectorData(detector.sensor.LastDetectedEntity, detector.sensor.GetPosition(), detector.angle);
    }
    if (detector.cameraCount != 0 && detector.camera[detector.currentCamera].CanScan(cameraRange))
    {
        if (detector.timeUntilPing > 0) { detector.timeUntilPing--; return; }
        MyDetectedEntityInfo entityInfo = detector.camera[detector.currentCamera].Raycast(cameraRange, pitch: 0f, yaw: 0f);
        GetDetectorData(entityInfo, detector.camera[detector.currentCamera].GetPosition(), detector.angle);
        if (detector.currentCamera < (detector.cameraCount - 1))
        {
            detector.currentCamera++;
        }
        else
        {
            detector.currentCamera = 0;
        }
        detector.timeUntilPing = detector.bufferTime;
    }

}

void GetDetectorData(MyDetectedEntityInfo entityInfo, Vector3D positionSelf, double angle)
{
    if (entityInfo.IsEmpty()) { return; }
    // GetPosition, GetDistance 
    Vector3D position = entityInfo.HitPosition ?? entityInfo.Position;
    float distance = (float)Vector3D.Distance(entityInfo.HitPosition ?? entityInfo.Position, positionSelf);

    // Main 
    double scaleMath = 0;
    if (cameras == 0)
    {
        scaleMath = (double)sensorRange / ((double)screenResolution / 2); // to format it to screen correctly 
    }
    else
    {
        scaleMath = (double)cameraRange / ((double)screenResolution / 2); // to format it to screen correctly 
    }

    int rawX = (int)(Math.Cos(angle) * distance);
    int rawY = (int)(Math.Sin(angle) * distance);
    int x = center + (int)(rawX / scaleMath);
    int y = center + (int)(rawY / scaleMath);
    string sCoordinates = x.ToString() + ":" + y.ToString();
    Target t;
    t.BirthDay = DateTime.Now.Ticks;
    t.Distance = distance;
    t.Position = position;
    t.hostile = (entityInfo.Relationship.ToString() == "Enemies");
    t.Debug = "";
    if (scanData.ContainsKey(sCoordinates) == false)
    {
        scanData.Add(sCoordinates, t);
    }
    else
    {
        scanData[sCoordinates] = t;
    }
}

// I stole this from Pennywise 
Vector3D CircleBy3Points(Vector3D p1, Vector3D p2, Vector3D p3)
{
    Vector3D Center = new Vector3D(0, 0, 0);
    Vector3D t = p2 - p1;
    Vector3D u = p3 - p1;
    Vector3D v = p3 - p2;
    Vector3D w = Vector3D.Cross(t, u);
    double wsl = Math.Pow(w.Length(), 2);
    if (wsl < 10e-14)
    {
        return Center;
    }
    else
    {
        double iwsl2 = 1.0 / (2.0 * wsl);
        double tt = Vector3D.Dot(t, t);
        double uu = Vector3D.Dot(u, u);
        Center = p1 + (u * tt * Vector3D.Dot(u, v) - t * uu * Vector3D.Dot(t, v)) * iwsl2;
        return Center;
    }
}

void DrawSweepingLine()
{
    for (int i = 0; i < screenResolution / 2; i++)
    {
        lineXFront[i] = (int)(center + (Math.Cos(iAngle) * i));
        lineYFront[i] = (int)(center + (Math.Sin(iAngle) * i));

        if (detectorBack.exists)
        {
            lineXBack[i] = (int)(center + (Math.Cos(detectorBack.angle) * i));
            lineYBack[i] = (int)(center + (Math.Sin(detectorBack.angle) * i));

            Plot(oldLineXBack[i], oldLineYBack[i], colorInnerBackground);
            Plot(lineXBack[i], lineYBack[i], colorBackLine); //visible line 
        }
        if (detectorLeft.exists)
        {
            lineXRight[i] = (int)(center + (Math.Cos(detectorRight.angle) * i));
            lineYRight[i] = (int)(center + (Math.Sin(detectorRight.angle) * i));
            lineXLeft[i] = (int)(center + (Math.Cos(detectorLeft.angle) * i));
            lineYLeft[i] = (int)(center + (Math.Sin(detectorLeft.angle) * i));

            Plot(oldLineXRight[i], oldLineYRight[i], colorInnerBackground);
            Plot(oldLineXLeft[i], oldLineYLeft[i], colorInnerBackground);
            Plot(lineXRight[i], lineYRight[i], colorRightLine); //visible line 
            Plot(lineXLeft[i], lineYLeft[i], colorLeftLine); //visible line 
        }

        // range circles 
        int ring1 = (int)((screenResolution * 0.25) / 2);
        int ring2 = (int)((screenResolution * 0.50) / 2);
        int ring3 = (int)((screenResolution * 0.75) / 2);
        int ring4 = (int)((screenResolution / 2) - 1);

        if (i == ring1 || i == ring2 || i == ring3 || i == ring4)
        {
            Plot(oldLineXFront[i], oldLineYFront[i], colorRings);
            if (detectorBack.exists) Plot(oldLineXBack[i], oldLineYBack[i], colorRings);
            if (detectorLeft.exists)
            {
                Plot(oldLineXRight[i], oldLineYRight[i], colorRings);
                Plot(oldLineXLeft[i], oldLineYLeft[i], colorRings);
            }
        }
        else Plot(oldLineXFront[i], oldLineYFront[i], colorInnerBackground);

        Plot(lineXFront[i], lineYFront[i], colorFrontLine); //visible line rc block 

        oldLineXFront[i] = lineXFront[i];
        oldLineYFront[i] = lineYFront[i];

        if (detectorBack.exists)
        {
            oldLineXBack[i] = lineXBack[i];
            oldLineYBack[i] = lineYBack[i];
        }
        if (detectorLeft.exists)
        {
            oldLineXRight[i] = lineXRight[i];
            oldLineYRight[i] = lineYRight[i];
            oldLineXLeft[i] = lineXLeft[i];
            oldLineYLeft[i] = lineYLeft[i];
        }
    }
}

void Main(string arguments)
{
    try
    {

        if (arguments != "") ArgumentHandler(arguments);

        if (initialized == false) Init();

        iAngle = MathHelper.ToRadians(MathHelper.ToDegrees(rotor.Angle) + rotorOffset % 360);
        iAngle2 = rotor.Angle;
        if (upsideDown) iAngle = MathHelper.ToRadians(180) - iAngle;
        int dAngle = (int)MathHelper.ToDegrees((float)iAngle);
        detectorFront.angle = iAngle;
        detectorRight.angle = MathHelper.ToRadians(dAngle + 90 % 360);
        detectorBack.angle = MathHelper.ToRadians(dAngle + 180 % 360);
        detectorLeft.angle = MathHelper.ToRadians(dAngle + 270 % 360);

        rotor.SetValue<float>("Velocity", (float)sweepLineRPM);

        // -- ticks -- 
        ticks++;
        if (ticks == 60 / refreshRate)
        {
            ticks = 0;
            Echo("]" + RunningSymbol());
            Echo("Sensors Active: " + sensors.ToString());
            Echo("Cameras Active: " + cameras.ToString());
            Echo("");
            Echo("Camera Scan Range: " + cameraRange.ToString());
            Echo("Use 'inc c' to increase camera range.");
            Echo("Use 'dec c' to decrease camera range.");
            Echo("");
            Echo("Rotor RPM: " + sweepLineRPM.ToString());
            Echo("Use 'inc r' to increase rotor RPM.");
            Echo("Use 'dec r' to decrease rotor RPM.");
            Echo("");
            Echo("Camera ID in use: " + detectorFront.currentCamera.ToString());
            Echo("Ticks Until Next: " + detectorFront.timeUntilPing);
            Echo("Tick Buffer: " + detectorFront.bufferTime);
            if (detectorFront.exists)
            {
                this.RunDetector(detectorFront);
            }
            if (detectorBack.exists)
            {
                this.RunDetector(detectorBack);
            }
            if (detectorLeft.exists)
            {
                this.RunDetector(detectorLeft);
                this.RunDetector(detectorRight);
            }

        } // -- ticks -- 

        DrawSweepingLine();
        Refresh();

    }
    catch (Exception ex)
    {
        Echo(string.Format("Main Error: {0}", ex.Message));
    }
}

T GetBlock<T>(string tag)
    where T : IMyTerminalBlock
{
    List<T> blocks = this.GetBlocks<T>(tag);
    return (blocks.Count > 0) ? blocks[0] : default(T);
}

List<T> GetBlocks<T>(string tag)
    where T : IMyTerminalBlock
{
    List<IMyTerminalBlock> blocks = new List<IMyTerminalBlock>();
    this.GridTerminalSystem.SearchBlocksOfName(tag, blocks);
    return blocks.Cast<T>().ToList();
}

// Whip's Running Symbol Method v6 
int runningSymbolVariant = 0;
string RunningSymbol()
{
    runningSymbolVariant++;
    string strRunningSymbol = "";

    if (runningSymbolVariant == 0)
        strRunningSymbol = " | ";
    else if (runningSymbolVariant == 1)
        strRunningSymbol = " / ";
    else if (runningSymbolVariant == 2)
        strRunningSymbol = "-- ";
    else if (runningSymbolVariant == 3)
    {
        strRunningSymbol = " \\ ";
        runningSymbolVariant = -1;
    }
    return strRunningSymbol;
}