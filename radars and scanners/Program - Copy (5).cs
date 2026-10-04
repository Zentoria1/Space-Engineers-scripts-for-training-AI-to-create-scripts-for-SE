/*
 *   R e a d m e
 *   -----------
 * O.I.S. Lidar Proximity Scanner
 * http://steamcommunity.com/sharedfiles/filedetails/?id=825974702
 *
 * update 1.199.020 Removed closed method
 * update 1.192.022 Update to closed
 * update 1.190.009 Update to Text system
 * update 1.187.101 remove temp mp fix
 * update 1.187.088 optimization
 * update 1.187.087 Temp MP Fix
 * update 1.186.300 LCD optimization.
 * update 1.185.100 Changed to MDK deployment, API fixes.
 * update 1.183.017 Change to fix start up. added agrument class
 * update 1.182.103 Change to mono font, WriteLCD class
 * update 1.167.04 Start of code

 */

//To put your code in a PB copy from this comment...

//string cameraName = "[Lidar]";
using Sandbox.Game.GameSystems;
using Sandbox.ModAPI.Ingame;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VRageMath;

string lidarLCDName = "[Proximity]";
float scanAngle = 45;//0-45 angle
double scanRange = 100;//0-222 m, 9 beams, camera builds at 2,000m/sec
// 0-37m, 9 beams, camera builds at 333m/10 tic
// 0-111m, 9 beams, camera builds at 1000m/30 tic
bool laseState = true;//Default program on/off at start


// *** User Customization ***
List<string> reservedNamesStatic = new List<string> { "[LCD]", "[ShipStatus]", "[EmergencyThrust]", "[SunChaser]", "[GravDrive]", "[OrbitThruster]", "" };//Names that are [name] but not airzones
WriteLCD lidarLCD;
List<IMyCameraBlock> cameras = new List<IMyCameraBlock>();
List<IMyCameraBlock> foreCameras = new List<IMyCameraBlock>();
List<IMyCameraBlock> aftCameras = new List<IMyCameraBlock>();
List<IMyCameraBlock> starCameras = new List<IMyCameraBlock>();
List<IMyCameraBlock> portCameras = new List<IMyCameraBlock>();
List<IMyCameraBlock> aboveCameras = new List<IMyCameraBlock>();
List<IMyCameraBlock> belowCameras = new List<IMyCameraBlock>();
StringBuilder tempstring = new StringBuilder();
//int font = 1147350002;//monospace font
string fontString = "Monospace";
//int screensize = 26;
string[] argMessages = new string[10];
string[] pieces = new string[2];
int ticcounter = 0;
//string echoString = "";
List<IMyTextSurface> pbText = new List<IMyTextSurface>();


public Program()
{
    // The constructor, called only once every session and
    // always before any other method is called. Use it to
    // initialize your script.
    //
    // The constructor is optional and can be removed if not
    // needed.
    lidarLCD = new WriteLCD(this, lidarLCDName);
    ArgumentParser(Me.CustomData);
    if (Storage.Length > 0)
    {
        ArgumentParser(Storage);
    }
    BuildLists();
    if (Me.SurfaceCount > 0) pbText.Add(Me.GetSurface(0));
    foreach (IMyTextSurface de in pbText)
    {
        de.ContentType = ContentType.TEXT_AND_IMAGE;
        de.Font = fontString;
    }
    if (laseState) foreach (IMyCameraBlock de in cameras) de.EnableRaycast = true;
    Runtime.UpdateFrequency = UpdateFrequency.Update10;
    //do a first pass
    //empty old lists just in case
}

public void Save()
{
    // Called when the program needs to save its state. Use
    // this method to save your state to the Storage field
    // or some other means.
    //
    // This method is optional and can be removed if not
    // needed.
    StringBuilder writeToStorage = new StringBuilder();

    Storage = writeToStorage.ToString();
}

public void Main(string argument, UpdateType updateSource)
{
    //Initilization of variables
    // Arguments = "", lase, turnon, turnoff, cleangps, rebuild

    //null checker
    for (int i = 0; i < cameras.Count; ++i)
    {
        if (GridTerminalSystem.CanAccess(cameras[i]))
        {
            BuildLists();
            break;
        }
    }

    Echo(argument);
    if (argument.Length > 0)
    {
        ArgumentParser(argument);
        //return;
    }
    if ((updateSource & UpdateType.Update10) == 0) return;
    //Echo(echoString);
    if (ticcounter++ < 3) return;
    ticcounter = 0;
    //Clean LCDs off
    //WriteToLCD(Storage);
    //Actions
    lidarLCD.WriteToLCD("  O.I.S. Lidar Proximity");
    if (cameras.Count == 0) lidarLCD.WriteToLCD("\n No Camera found");
    if (!laseState) lidarLCD.WriteToLCD("\n system turned off");
    if (laseState)
    {
        var foreDistance = ProximityScan(foreCameras, scanAngle, scanAngle, scanRange);
        var aftDistance = ProximityScan(aftCameras, scanAngle, scanAngle, scanRange);
        var starDistance = ProximityScan(starCameras, scanAngle, scanAngle, scanRange);
        var portDistance = ProximityScan(portCameras, scanAngle, scanAngle, scanRange);
        var aboveDistance = ProximityScan(aboveCameras, scanAngle, scanAngle, scanRange);
        var belowDistance = ProximityScan(belowCameras, scanAngle, scanAngle, scanRange);
        //(valueLineParts.Length > 1 ? valueLineParts[1][0] : '.')
        tempstring.Clear();
        tempstring.Append(foreDistance == 99999 ? " - " : foreDistance.ToString("0.0"));
        if (tempstring.Length == 3) tempstring.Append(" ");
        lidarLCD.WriteToLCD($"\n\n           {tempstring}");
        lidarLCD.WriteToLCD("\n           ||||");
        lidarLCD.WriteToLCD("\n          ||||||");
        lidarLCD.WriteToLCD("\n         ||||||||");
        tempstring.Clear();
        tempstring.Append((portDistance == 99999 ? " - " : portDistance.ToString("0.0")));
        if (tempstring.Length == 3) tempstring.Append(" ");
        lidarLCD.WriteToLCD($"\n  {tempstring}   ||||||||   ");
        tempstring.Clear();
        tempstring.Append((starDistance == 99999 ? " - " : starDistance.ToString("0.0")));
        if (tempstring.Length == 3) tempstring.Append(" ");
        lidarLCD.WriteToLCD(tempstring.ToString());
        lidarLCD.WriteToLCD("\n          |||||| ");
        lidarLCD.WriteToLCD("\n          ||  ||");
        tempstring.Clear();
        tempstring.Append((aftDistance == 99999 ? " - " : aftDistance.ToString("0.0")));
        if (tempstring.Length == 3) tempstring.Append(" ");
        lidarLCD.WriteToLCD($"\n           {tempstring}");
        //WriteToLCD("\n Above           Below");
        tempstring.Clear();
        tempstring.Append((aboveDistance == 99999 ? " - " : aboveDistance.ToString("0.0")));
        if (tempstring.Length == 3) tempstring.Append(" ");
        lidarLCD.WriteToLCD($"\n\n            {tempstring}");
        lidarLCD.WriteToLCD("\n          //|||\\\\ ");
        lidarLCD.WriteToLCD("\n         |||||||||");
        lidarLCD.WriteToLCD("\n          \\\\|||// ");
        tempstring.Clear();
        tempstring.Append((belowDistance == 99999 ? " - " : belowDistance.ToString("0.0")));
        if (tempstring.Length == 3) tempstring.Append(" ");
        lidarLCD.WriteToLCD($"\n            {tempstring}");
    }
    lidarLCD.WriteToLCD("\n");
    //echoString = $"Orbital Flight Computer\n{lidarLCD.ToString()}";
    foreach (IMyTextSurface de in pbText)
    {
        de.WriteText($"Orbital Flight Computer\n{lidarLCD.ToString()}");
    }
    lidarLCD.FlushToLCD();


}//main


void ArgumentParser(string argument)
{
    argMessages = argument.Split('\n');
    foreach (string de in argMessages)
    {
        pieces = de.Split(' ');
        if (pieces.Count() < 1) continue;
        //at least 1 pieces below this
        switch (pieces[0])
        {
            case "lase":
                laseState = !laseState;
                if (laseState) foreach (var fr in cameras) fr.EnableRaycast = true;
                if (!laseState) foreach (var fr in cameras) fr.EnableRaycast = false;
                break;
            case "turnon":
                foreach (var fr in cameras) fr.EnableRaycast = true;
                break;
            case "turnoff":
                foreach (var fr in cameras) fr.EnableRaycast = false;
                break;
            case "rebuild":
                BuildLists();
                break;
        }
        if (pieces.Count() < 2) continue;
        //at least 2 pieces below this
        switch (pieces[0])
        {
            case "lidarLCDName":
                lidarLCDName = de.Substring("lidarLCDName ".Length);
                break;
            case "laseState":
                bool test = false;
                if (bool.TryParse(pieces[1], out test)) laseState = true;
                break;
        }
    }
}


void BuildLists()
{
    cameras.Clear();
    foreCameras.Clear();
    aftCameras.Clear();
    starCameras.Clear();
    portCameras.Clear();
    aboveCameras.Clear();
    belowCameras.Clear();
    GridTerminalSystem.GetBlocksOfType(cameras);
    var controlStations = new List<IMyShipController>();
    GridTerminalSystem.GetBlocksOfType(controlStations, b => b.CubeGrid == Me.CubeGrid);
    if (controlStations.Count > 0)
    {
        var cs = controlStations[0] as IMyShipController;
        foreach (var de in cameras)
        {
            Base6Directions.Direction dir = cs.WorldMatrix.GetClosestDirection(de.WorldMatrix.Forward);
            switch (dir)
            {
                case Base6Directions.Direction.Forward:
                    foreCameras.Add(de);
                    break;
                case Base6Directions.Direction.Backward:
                    aftCameras.Add(de);
                    break;
                case Base6Directions.Direction.Right:
                    starCameras.Add(de);
                    break;
                case Base6Directions.Direction.Left:
                    portCameras.Add(de);
                    break;
                case Base6Directions.Direction.Up:
                    aboveCameras.Add(de);
                    break;
                case Base6Directions.Direction.Down:
                    belowCameras.Add(de);
                    break;
            }
        }
    }
    lidarLCD.LCDBuild(lidarLCDName);
    foreach (var de in lidarLCD.Lcds)
    {
        //lcds[i].SetValue<long>("Font", font);
        de.Font = fontString;
    }
}

MyShipVelocities FindVelocity()
{
    var shipControl = new List<IMyShipController>();
    GridTerminalSystem.GetBlocksOfType(shipControl);
    if (shipControl.Count < 1) { return new MyShipVelocities(); }
    return shipControl[0].GetShipVelocities();
}

double LidarRange(List<IMyCameraBlock> cameras, float pitch, float yaw, double scanRange)
{
    double range = 9999999;
    double range2 = 9999999;
    MyDetectedEntityInfo info;
    foreach (var de in cameras)
    {
        if (de.EnableRaycast)
        {
            info = de.Raycast(scanRange, pitch, yaw);
            if (info.HitPosition.HasValue && !info.EntityId.Equals(Me.CubeGrid.EntityId))
            {
                range2 = Vector3D.Distance(de.GetPosition(), info.HitPosition.Value);
            }
            if (range2 < range) range = range2;
        }
    }
    return range;
}

double ProximityScan(List<IMyCameraBlock> cameras, float pitch, float yaw, double scanRange)
{
    double range = 99999;
    List<double> range2 = new List<double> {
        99999,
        LidarRange(cameras, -pitch, yaw, scanRange),
        LidarRange(cameras, 0, yaw, scanRange),
        LidarRange(cameras, pitch, yaw, scanRange),
        LidarRange(cameras, -pitch, 0, scanRange),
        LidarRange(cameras, 0, 0, scanRange),
        LidarRange(cameras, pitch, 0, scanRange),
        LidarRange(cameras, -pitch, -yaw, scanRange),
        LidarRange(cameras, 0, -yaw, scanRange),
        LidarRange(cameras, pitch, -yaw, scanRange)
    };
    foreach (var de in range2)
    {
        if (de < range) range = de;
    }
    return range;
}

double VelocityEta(MyShipVelocities shipV, Vector3D toObject)
{
    Vector3D rate2 = VectorProjection(shipV.LinearVelocity, toObject);
    int sign = Math.Sign(toObject.Dot(shipV.LinearVelocity));
    return sign * rate2.Length();
}

Vector3D VectorProjection(Vector3D a, Vector3D b)
{//project a onto b
    Vector3D projection = a.Dot(b) / b.LengthSquared() * b;
    return projection;
}

//Whip's Profiler Graph Code
int count = 1;
int maxSeconds = 60;
StringBuilder profile = new StringBuilder();
void ProfilerGraph()
{
    if (count <= maxSeconds * 1)
    {
        double timeToRunCode = Runtime.LastRunTimeMs;

        profile.Append(timeToRunCode.ToString()).Append("\n");
        count++;
    }
    else
    {
        var screen = GridTerminalSystem.GetBlockWithName("DEBUG") as IMyTextSurface;
        screen?.WriteText(profile.ToString());
        //screen?.ContentType = ContentType.TEXT_AND_IMAGE;
    }
}


public class WriteLCD
{
    public WriteLCD(Program script)
    {
        _script = script;
        LcdStringBuilder = new StringBuilder();
        Lcds = new List<IMyTextSurface>();
        surfacesProviders = new List<IMyTextSurfaceProvider>();
    }
    public WriteLCD(Program script, string lcdNameIn)
    {
        _script = script;
        LcdStringBuilder = new StringBuilder();
        Lcds = new List<IMyTextSurface>();
        surfacesProviders = new List<IMyTextSurfaceProvider>();
        lcdName = lcdNameIn;
        LCDBuild();
    }

    Program _script;
    public StringBuilder LcdStringBuilder { get; }
    public List<IMyTextSurface> Lcds { get; }
    List<IMyTextSurfaceProvider> surfacesProviders;
    private const int MAX_NUMBER_CHARACTERS = 100000;
    string lcdName = "";

    public void CleanLCD()
    {
        for (int i = 0; i < Lcds.Count; ++i)
        {
            if (!_script.GridTerminalSystem.CanAccess((IMyTerminalBlock)Lcds[i]))
            {
                LCDBuild();
                break;
            }
        }
        foreach (IMyTextSurface de in Lcds)
        {
            de.WriteText("", false);
        }
    }

    public void FlushToLCD(bool append = false)
    {
        if (LcdStringBuilder.Length >= MAX_NUMBER_CHARACTERS)
        {
            LcdStringBuilder.Clear();
            LcdStringBuilder.Append(" ERROR/n Attempted to write/n too many characters");
        }
        foreach (IMyTextSurface de in Lcds)
        {
            de.WriteText(LcdStringBuilder, append);
        }
        LcdStringBuilder.Clear();
    }

    public void FlushToLCD(Color color, bool append = false)
    {
        for (int i = 0; i < Lcds.Count; ++i)
        {
            if (!_script.GridTerminalSystem.CanAccess((IMyTerminalBlock)Lcds[i]))
            {
                LCDBuild();
                break;
            }
        }
        foreach (IMyTextSurface de in Lcds)
        {
            de.FontColor = color;
        }
        FlushToLCD(append);
    }

    public void LCDBuild(string lcdNameIn)
    {
        lcdName = lcdNameIn;
        LCDBuild();
    }

    public void LCDBuild()
    {
        Lcds.Clear();
        surfacesProviders.Clear();
        _script.GridTerminalSystem.GetBlocksOfType(surfacesProviders, b =>
        {
            if (((IMyTerminalBlock)b).CubeGrid == _script.Me.CubeGrid && b is IMyTextSurface && ((IMyTerminalBlock)b).CustomName.Contains(lcdName) && ((IMyTerminalBlock)b).IsFunctional)
            {
                Lcds.Add((IMyTextSurface)b);
                return false;
            }
            if (((IMyTerminalBlock)b).CubeGrid == _script.Me.CubeGrid && ((IMyTerminalBlock)b).CustomData.Contains(lcdName) && ((IMyTerminalBlock)b).IsFunctional) return true;
            return false;
        });
        foreach (IMyTextSurfaceProvider de in surfacesProviders)
        {
            string[] argMessages = ((IMyTerminalBlock)de).CustomData.Split('\n');
            string[] pieces;
            int tempInt = 0;
            foreach (string fr in argMessages)
            {
                pieces = fr.Split(' ');
                if (pieces.Count() < 2) continue;
                //at least 2 pieces below this
                if (pieces[0].Contains(lcdName))
                {
                    if (int.TryParse(pieces[1], out tempInt))
                    {
                        if (de.SurfaceCount > tempInt && tempInt >= 0)
                        {
                            if (de is IMyTextSurface && !Lcds.Contains((IMyTextSurface)de))
                            {
                                Lcds.Add((IMyTextSurface)de);
                            }
                            else
                            {
                                if (!Lcds.Contains(de.GetSurface(tempInt)))
                                {
                                    Lcds.Add(de.GetSurface(tempInt));
                                }
                            }
                        }
                    }
                }
            }
        }
        ;
        foreach (IMyTextSurface de in Lcds) de.ContentType = ContentType.TEXT_AND_IMAGE;
    }

    public override string ToString()
    {
        return LcdStringBuilder.ToString();
    }

    public void WriteToLCD(string textToWrite)
    {
        LcdStringBuilder.Append(textToWrite);
    }
}