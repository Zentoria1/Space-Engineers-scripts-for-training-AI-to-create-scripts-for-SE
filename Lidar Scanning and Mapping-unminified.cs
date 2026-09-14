/// <summary> 
// This Lidar detection script is meant to work in conjunction with a visualization script. It works by radio or by direct insertion of data. 
/// </summary> 
static string visualPBstring = "[LidarVisual]"; // the PB that has this tag will be run to process the scan and target data 
static string timerString = "[CameraTimer]"; // the timer that has this tag will have to be setup to trigger this PB 
IMyProgrammableBlock visualPB;

static bool offGridCameras = false;
// set this to true if your camera's are on a separate grid, for example through rotors 

static int maxBeamPerTick = 1;
// this can be set higher than 1, but it is NOT recommended, except if you are playing SP and do not care. 

static int maxCameraGroups = 6; // this is limited to 6 groups, but it could be set larger 
static int scanBeamMax = 31684; //178 * 178, this is the resolution of the scan panels on the data visualization grid 

StringBuilder messageString = new StringBuilder();
float mapAngle = 45; // this should be kept at 45 degrees preferably 
double mapRange = 2000; // this can be changed through arguments. The higher it is the slower the mapping becomes       
double scanRadius = 100; // this can be set automatically or manually for a larger or smaller (=zoomed in) scan 
long scanEntityId = 0; // this identifies the scan target 
int scanBeamCount = 0; // this keeps track of which beam number we are at 
int scanHits = 0; // this count the number of scanhits. 
Vector3D center = Vector3D.Zero; // this is the center of the grid the cameras are on 
Vector3D scanCenter = Vector3D.Zero; // this is the center of the scan 
ScanPixel[] scanPixels = new ScanPixel[scanBeamMax]; //the pixel information is stored here         
MyTransmitTarget commWho = MyTransmitTarget.Owned; // this can be changed to include allies or...     
IMyTimerBlock timer;
bool transmitting = true;
bool timerTriggered = false;
bool tracking = false; // tracking is off by default 
bool trackEnemy = true;
bool trackFriend = false;
bool trackNeutral = false;
bool trackOwned = false;
bool trackNotOwned = false;
bool scanning = false;
bool mapping = false;
bool autoradius = true;
int trackDelay = 60;
// the tracking method in this script is pretty basic, this delay can be lowered for more reliable tracking 
Vector3D scanX = Vector3D.UnitX; // these are vectors perpendicular to the scan direction 
Vector3D scanY = Vector3D.UnitY; // corresponding to the x and y directions on the panels 
public StringBuilder status = new StringBuilder();

List<IMyCameraBlock> cameras = new List<IMyCameraBlock>();
List<IMyCameraBlock>[] cameraGroups = new List<IMyCameraBlock>[maxCameraGroups];
int[] activeCamera = new int[maxCameraGroups];
int activeGroup = 0;
int totalGroups = 0;
List<IMyRadioAntenna> outRadios = new List<IMyRadioAntenna>();
List<Target> targets = new List<Target>();

string[] mapTargets = new string[maxCameraGroups];
// these are stored strings per cameragroup. They keep track of the mapping pattern 

static Vector2D[] vec = { new Vector2D(-1, -1), new Vector2D(1, 1), new Vector2D(-1, 1), new Vector2D(1, -1) };
// these vector2s are necessary for the mapping pattern 

public Program()
{
    BuildLists(); // the building of the cameralists and such is done in a separate method 
    targets = new List<Target>();

    if (Storage.Length > 0)
    {
        var parts = Storage.Split(';');
        mapping = bool.Parse(parts[0]);
        mapAngle = float.Parse(parts[1]);
        mapRange = double.Parse(parts[2]);
        scanning = bool.Parse(parts[3]);
        scanCenter = GetVector3D(parts[4]);
        scanRadius = double.Parse(parts[5]);
        autoradius = bool.Parse(parts[6]);
        transmitting = bool.Parse(parts[7]);
        tracking = bool.Parse(parts[8]);
        trackEnemy = bool.Parse(parts[9]);
        trackFriend = bool.Parse(parts[10]);
        trackNeutral = bool.Parse(parts[11]);
        trackOwned = bool.Parse(parts[12]);
        trackNotOwned = bool.Parse(parts[13]);
        trackDelay = int.Parse(parts[14]);
        // keeping track of all the settings that are stored 
        // The storage only stores targets, not scanpixels. Targets number in the hundreds at most, scanpixels number over 30k 
        // The loading of the storage string could perhaps be improved upon by using TryParse 
        for (int i = 0; i < maxCameraGroups; i++)
        {
            mapTargets[i] = parts[i + 15];
        }

        for (int i = maxCameraGroups + 15; i < parts.Length; i++)
        {
            string[] tar = parts[i].Split(',');
            targets.Add(new Target
            {
                time = int.Parse(tar[0]) + 1,
                countdown = int.Parse(tar[1]) - 1,
                type = int.Parse(tar[2]),
                relation = int.Parse(tar[3]),
                entityId = long.Parse(tar[4]),
                position = GetVector3D(tar[5]),
                velocity = GetVector3D(tar[6]),
                size = double.Parse(tar[7]),
                name = tar[8]
            });
        }
    }
}

public void Save()
{
    StringBuilder tempstring = new StringBuilder();
    tempstring.Append(mapping).Append(";");//0        
    tempstring.Append(mapAngle).Append(";");//1        
    tempstring.Append(mapRange).Append(";");//2   
    tempstring.Append(scanning).Append(";");//3       
    tempstring.Append(scanCenter.ToString()).Append(";");//4        
    tempstring.Append(scanRadius).Append(";");//5   
    tempstring.Append(autoradius).Append(";");//6   
    tempstring.Append(transmitting).Append(";");//7 
    tempstring.Append(tracking).Append(";");//8   
    tempstring.Append(trackEnemy).Append(";");//9        
    tempstring.Append(trackFriend).Append(";");//10        
    tempstring.Append(trackNeutral).Append(";");//11   
    tempstring.Append(trackOwned).Append(";");//12        
    tempstring.Append(trackNotOwned).Append(";");//13        
    tempstring.Append(trackDelay).Append(";");//14 

    for (int i = 0; i < maxCameraGroups; i++)
    {
        tempstring.Append(mapTargets[i]); //15 t/m 22   
        if (i < maxCameraGroups - 1) tempstring.Append(";");
    }

    for (int j = 0; j < targets.Count; j++) //25 and further    
    {
        tempstring.Append(";" + targets[j]);
    }
    Storage = tempstring.ToString();
}

void BuildLists()
{
    GridTerminalSystem.GetBlocksOfType(outRadios, b => b.CubeGrid == Me.CubeGrid);

    if (offGridCameras) // if we permit offgrid cameras, we can simply get all of them 
        GridTerminalSystem.GetBlocksOfType(cameras);
    else
        GridTerminalSystem.GetBlocksOfType(cameras, b => b.CubeGrid == Me.CubeGrid);
    // this limits the cameralists to cameras on the PBs own grid 

    GridTerminalSystem.SearchBlocksOfName(timerString, null, b =>
    {
        if (b is IMyTimerBlock) timer = b as IMyTimerBlock;
        return false;
    });

    for (int i = 0; i < maxCameraGroups; i++)
    {
        cameraGroups[i] = new List<IMyCameraBlock>();
        activeCamera[i] = 0;
        mapTargets[i] = "0";
    }

    for (int i = 0; i < cameras.Count; i++)
    {
        // we add every camera to a cameragroup,  
        // if a group already exists with the same orientation we use that one 
        for (int j = 0; j < cameraGroups.Length; j++)
        {
            if (cameraGroups[j].Count == 0)
            {
                cameraGroups[j].Add(cameras[i]);

                break;
            }
            else
            {
                IMyCameraBlock cam = cameraGroups[j][0];
                if (cam.CubeGrid == cameras[i].CubeGrid && cam.Orientation.Forward == cameras[i].Orientation.Forward)
                {
                    cameraGroups[j].Add(cameras[i]);
                    break;
                }
            }
        }
    }

    totalGroups = 0;

    for (int i = 0; i < maxCameraGroups; i++)
        if (cameraGroups[i].Count != 0)
            totalGroups = i + 1;

    for (int i = 0; i < cameras.Count; ++i)
        cameras[i].EnableRaycast = true;

    GridTerminalSystem.SearchBlocksOfName(visualPBstring, null, b =>
    {
        if (b is IMyProgrammableBlock) visualPB = b as IMyProgrammableBlock;
        return false;
    });
}

public bool HandleArguments(string argument)
{
    bool endMain = false;

    if (argument == "map")
    {
        //change run/no run state.        
        mapping = !mapping;
        if (mapping)
            for (int i = 0; i < cameras.Count; ++i) cameras[i].EnableRaycast = true;

        if (!timerTriggered)
        {
            timer.Trigger();
            timerTriggered = true;
        }
    }  // stop and start the mapping 

    else if (argument == "scan")
    {
        if (!scanning)
        {
            for (int i = 0; i < targets.Count; i++)
            {
                if (targets[i].entityId == scanEntityId)
                {
                    scanning = true;
                    StartScan();
                }
            }
        }
        else
            scanning = false;

        if (!timerTriggered)
        {
            timer.Trigger();
            timerTriggered = true;
        }
    }  // stop and start the scanning, only scan if the entityId is known 

    else if (argument.Contains("scanid"))
    {
        long id = 0;
        string[] pieces = argument.Split(' ');
        for (int i = 0; i < pieces.Length; i++)
        {
            if (long.TryParse(pieces[i], out id))
            {
                for (int j = 0; j < targets.Count; j++)
                {
                    if (targets[j].entityId == id)
                    {
                        scanEntityId = id;
                        scanCenter = targets[j].position;
                        if (autoradius)
                            scanRadius = targets[j].size;
                        break;
                    }
                }
            }
        }
    } // change the scanid to use as a target 

    else if (argument.Contains("scanitem"))
    {
        string[] pieces = argument.Split(' ');
        int id = 0;
        for (int i = 0; i < pieces.Length; i++)
        {

            if (int.TryParse(pieces[i], out id))
            {
                if (id < targets.Count)
                {
                    scanEntityId = targets[id].entityId;
                    scanCenter = targets[id].position;
                    if (autoradius)
                        scanRadius = targets[id].size;
                    break;
                }
            }
        }
    } // change the scanitem being scanned usage: scanitem x, where x is the targetnumber, starting at 0 

    else if (argument.Contains("scanradius"))
    {
        if (argument.Contains("auto"))
        {
            autoradius = true;
        }
        else
        {
            double radius = 100;
            string[] pieces = argument.Split(' ');
            for (int i = 0; i < pieces.Length; i++)
            {
                if (double.TryParse(pieces[i], out radius))
                {
                    scanRadius = radius;
                    autoradius = false;
                }
            }
        }
    } // change the size of the scan, if not specified, it is set to auto 

    else if (argument == "rebuild")
    {
        BuildLists();
        endMain = true;
    } // this can be used to rebuild the block information 

    else if (argument == "transmit")
    {
        transmitting = !transmitting;
    } // turn the radio on and off 

    else if (argument == "clean")
    {
        Storage = "";
        targets = new List<Target>();
        activeGroup = 0;
        for (int i = 0; i < maxCameraGroups; i++)
        {
            activeCamera[i] = 0;
            mapTargets[i] = "0";
        }
        endMain = false;
    } //clean the targetlist 

    else if (argument.Contains("range"))
    {
        double range = 0;
        string[] pieces = argument.Split(' ');
        for (int i = 0; i < pieces.Length; ++i)
        {
            if (double.TryParse(pieces[i], out range))
            {
                mapRange = range;
            }
        }
    } // change the range of the mapping. Scanning and tracking are unaffected 

    else if (argument.Contains("angle"))
    {
        float te = 0;
        string[] pieces = argument.Split(' ');
        for (int i = 0; i < pieces.Length; ++i)
        {
            if (float.TryParse(pieces[i], out te))
            {
                if (te <= 45 && te >= 0)
                {
                    mapAngle = te;
                }
            }
        }
    } // changing the map angle, not recommended unless you know what you're doing 

    else if (argument == "track")
    {
        tracking = !tracking;
        if (tracking)
            for (int i = 0; i < cameras.Count; ++i) cameras[i].EnableRaycast = true;

        if (!timerTriggered)
        {
            timer.Trigger();
            timerTriggered = true;
        }
    } // turning tracking on or off 

    else if (argument.Contains("track") && argument.Contains("enemy"))
        trackEnemy = !trackEnemy;

    else if (argument.Contains("track") && argument.Contains("friend"))
        trackFriend = !trackFriend;

    else if (argument.Contains("track") && argument.Contains("neutral"))
        trackNeutral = !trackNeutral;

    else if (argument.Contains("track") && argument.Contains("unowned"))
        trackNotOwned = !trackNotOwned;

    else if (argument.Contains("track") && argument.Contains("owned"))
        trackOwned = !trackOwned;

    else if (argument.Contains("track") && argument.Contains("delay"))
    {
        int t = 0;
        string[] pieces = argument.Split(' ');
        for (int i = 0; i < pieces.Length; ++i)
        {
            if (int.TryParse(pieces[i], out t))
            {
                trackDelay = t;
            }
        }
    } // changing the tracking delay to improve tracking effectiveness 

    return endMain;
} // there are MANY arguments to use 

public void StartScan()
{
    center = Me.CubeGrid.GetPosition();
    Vector3D aim = scanCenter - center;
    scanY = scanRadius * 2 * Vector3D.Normalize(GetPerpendicular(aim)) / 178;
    scanX = scanRadius * 2 * Vector3D.Normalize(scanY.Cross(aim)) / 178;
    scanBeamCount = 0;
    scanHits = 0;
} // start the scan 

public bool Tracking()
{
    if (!tracking) return false;
    bool didTrackBeam = false;
    for (int i = 0; i < targets.Count; i++)
    {
        if (targets[i].countdown < 1 && targets[i].type != (int)MyDetectedEntityType.Asteroid && targets[i].type != (int)MyDetectedEntityType.Planet
            && targets[i].type != (int)MyDetectedEntityType.None && targets[i].type != (int)MyDetectedEntityType.Unknown &&
            (trackEnemy && targets[i].relation == (int)MyRelationsBetweenPlayerAndBlock.Enemies || trackFriend && targets[i].relation == (int)MyRelationsBetweenPlayerAndBlock.FactionShare ||
                trackOwned && targets[i].relation == (int)MyRelationsBetweenPlayerAndBlock.Owner || trackNeutral && targets[i].relation == (int)MyRelationsBetweenPlayerAndBlock.Neutral ||
                trackNotOwned && targets[i].relation == (int)MyRelationsBetweenPlayerAndBlock.NoOwnership))
        // this messy business is a filter to check what to track and what not to track 
        {


            Vector3D beamTarget = targets[i].position + targets[i].time * targets[i].velocity / 60;

            double bestDir;
            int bestGroup = BestCameraGroup(beamTarget, out bestDir);
            // finding the best camera group to use for the beam 

            if (bestDir > 0)
            {
                IMyCameraBlock cam = cameraGroups[bestGroup][activeCamera[bestGroup]];

                if (!cam.IsFunctional)
                {
                    IncrementCamera(bestGroup);
                    // we use the next camera next time 
                    return false;
                }

                Vector3D position = targets[i].position + targets[i].time * targets[i].velocity / 60;
                // we project the target ahead in time by using the velocity 

                double range = (position - cam.GetPosition()).Length();

                if (cam.CanScan(range))
                {
                    MyDetectedEntityInfo info = cam.Raycast(position);

                    if (!info.IsEmpty())
                        UpdateTargetlist(info);

                    targets[i].countdown = trackDelay;
                    // the countdown is set to the trackDelay, so we know when to send another tracking beam 
                }
                IncrementCamera(bestGroup);
                didTrackBeam = true;
                break;
            }
        }
    }
    return didTrackBeam;
}

public int BestCameraGroup(Vector3D beamTarget, out double bestDir)
{
    bestDir = -1;
    int bestGroup = -1;
    for (int j = 0; j < totalGroups; j++)
    {
        IMyCameraBlock cam = cameraGroups[j][0];
        if (!cam.IsFunctional)
            break;
        Matrix camOr; cam.Orientation.GetMatrix(out camOr);
        Matrix camWorld = cam.WorldMatrix.GetOrientation();
        Vector3D camForward = Vector3D.Transform(camOr.Forward, MatrixD.Transpose(camOr));
        Vector3D camForwardWorld = Vector3D.Transform(camForward, camWorld);

        double dir = camForwardWorld.Dot(beamTarget - cam.GetPosition());
        if (dir > bestDir)
        {
            bestDir = dir;
            bestGroup = j;
        }
    }
    return bestGroup;
}

public bool Scanning()
{
    if (!scanning) return false;
    bool didScanBeam = false;
    int y = scanBeamCount / 178;
    int x = scanBeamCount % 178;
    Vector3D dx = (x - 89) * scanX;
    Vector3D dy = (y - 89) * scanY;
    Vector3D beamTarget = scanCenter + dx + dy;
    // the above math is calculating the location the scanbeams are aimed at 

    double bestDir;
    int bestGroup = BestCameraGroup(beamTarget, out bestDir);
    // finding the best camera group to use for the beam 

    if (bestDir > 0)
    {
        IMyCameraBlock cam = cameraGroups[bestGroup][activeCamera[bestGroup]];

        if (!cam.IsFunctional)
        {
            IncrementCamera(bestGroup);
            return false;
        }

        Vector3D camPos = cam.GetPosition();
        double range = (beamTarget - camPos).Length() + scanRadius;

        if (cam.CanScan(range + mapRange)) // keep a reserve of 1 scan at range to do tracking with    
        {
            MyDetectedEntityInfo info = cam.Raycast(camPos + range * Vector3D.Normalize(beamTarget - camPos));

            if (!info.IsEmpty())
            {
                UpdateTargetlist(info);
                Vector3D hitPos = (Vector3D)info.HitPosition;
                int depth = (int)(512 * ((hitPos - camPos).Length() - (camPos - scanCenter).Length() + scanRadius) / (2 * scanRadius));
                // there are 512 colors to choose from, so we have a range of 512 depth values 
                ScanPixel newPixel = new ScanPixel
                {
                    entityId = info.EntityId,
                    type = (int)info.Type,
                    relation = (int)info.Relationship,
                    hitPosition = (Vector3D)info.HitPosition,
                    pixelNumber = scanBeamCount,
                    pixelDepth = depth
                };
                messageString.Append(";" + newPixel);
                scanPixels[scanBeamCount] = newPixel;

                scanHits++;
            }
            else
            {
                messageString.Append(";scanMiss," + scanBeamCount);
                // misses need to be sent as well (they are represented by black pixels 
            }

            didScanBeam = true;
            scanBeamCount++;
        }
        IncrementCamera(bestGroup);
    }

    if (scanBeamCount > scanBeamMax - 1)
        scanning = false;

    return didScanBeam;
}

void Mapping()
{
    if (!mapping) return;
    IMyCameraBlock currentCam = cameraGroups[activeGroup][activeCamera[activeGroup]];

    if (!currentCam.IsFunctional)
    {
        IncrementCamera();
        return;
    } // skip the camera if it doesn't work 

    if (currentCam.CanScan(mapRange * 2)) // keep a reserve of 1 scan at range to do tracking with    
    {
        Vector2D target = GetScanTargetAndIncrement(activeGroup);

        float x = (float)target.X * mapAngle;
        float y = (float)target.Y * mapAngle;

        MyDetectedEntityInfo info = currentCam.Raycast(mapRange, x, y);

        if (!info.IsEmpty())
            UpdateTargetlist(info);
    }
    IncrementCamera();
}

public void Main(string argument)
{
    messageString.Clear();

    for (int i = 0; i < targets.Count; i++)
    {
        if (targets[i].countdown > 0) targets[i].countdown--;
        targets[i].time++;
    }

    if (HandleArguments(argument))
        return;

    if (cameras.Count > 0)
    {
        for (int i = 0; i < maxBeamPerTick; i++)
        {
            if (!Tracking())
                if (!Scanning())
                    Mapping();
            // tracking is first priority, scanning second, mapping last.  
            // Mapping happens only when scanning and tracking do not 
        }
    }

    if (transmitting)
        LowPriority(outRadios, "anyone", Me.CubeGrid.DisplayName, "", messageString.ToString(), commWho, true);
    if (visualPB != null)
        visualPB.TryRun(";" + ";" + ";" + ";" + messageString);

    status.Append("Lidar Status");

    int beamCount = activeGroup + totalGroups * GetBeamCountFromMapCode(mapTargets[activeGroup]) - totalGroups;
    double scanDetail = Math.Round(Math.Sqrt(mapRange * mapRange * Math.PI * 4 / beamCount), 2);

    status.Append("\n" + "Settings" +
                    "\n" + "Mapping Range: " + mapRange + " m" +
                    "\n" + "Beam Count: " + beamCount.ToString() +
                    "\n" + "Map Detail: " + scanDetail.ToString() + " m" +
                    "\n" + "Transmitting: " + transmitting +
                    "\n" + "Tracking: " + tracking +
                    "\n" + "Track Enemy: " + trackEnemy +
                    "\n" + "Track Friend: " + trackFriend +
                    "\n" + "Track Neutral: " + trackNeutral +
                    "\n" + "Track Owned by me: " + trackOwned +
                    "\n" + "Track Unowned: " + trackNotOwned +
                    "\n" + "Total targets: " + targets.Count +
                    "\n" + "Track Delay: " + trackDelay +
                    "\n" + "Autoradius: " + autoradius +
                    "\n" + "Scan Radius: " + scanRadius.ToString() + " m" +
                    "\n" + "Scan Id: " + scanEntityId.ToString() +
                    "\n" + "Scan Pixels Found: " + scanHits);


    if (cameras.Count == 0) status.Append("\n No Camera found");
    if (!mapping && !scanning && !tracking) status.Append("\nsystem turned off");
    if (mapping && !scanning)
    {
        status.Append("\nLidar mapping in progress");
    }
    else if (scanning)
    {
        status.Append("\nLidar scanning in progress: " + 100 * Math.Round(scanBeamCount / (double)scanBeamMax, 2) + "%");
    }

    SendMessages();

    status.Append("\n");

    Echo(status.ToString());

    status.Clear();

    if (!timerTriggered)
    {
        timer.Trigger();
        timerTriggered = true;
    }
}

void IncrementCamera()
{
    if (activeCamera[activeGroup] < cameraGroups[activeGroup].Count - 1)
        activeCamera[activeGroup]++;
    else
        activeCamera[activeGroup] = 0;


    if (activeGroup < totalGroups - 1)
        activeGroup++;
    else
        activeGroup = 0;
}

void IncrementCamera(int group)
{
    if (activeCamera[group] < cameraGroups[group].Count - 1)
        activeCamera[group]++;
    else
        activeCamera[group] = 0;
}

int GetBeamCountFromMapCode(string mapcode)
{
    int beamCount = 0;
    int[] targetNrs = mapcode.Select(x => int.Parse(x.ToString())).ToArray();

    for (int i = 0; i < targetNrs.Length; i++)
    {
        int x = targetNrs[i];
        beamCount += (x + 1) * (int)Math.Pow(4, i);
    }

    return beamCount;
} // this calculates the number of beams that have to have been done to arrive at the current mapcode 

Vector2D GetScanTargetAndIncrement(int listNr)
{
    int[] targetNrs = mapTargets[listNr].Select(x => int.Parse(x.ToString())).ToArray();
    double scale = 0.5;
    Vector2D target = new Vector2D(0, 0);
    for (int i = 0; i < targetNrs.Length; i++)
    {
        int nr = targetNrs[i];
        target += vec[nr] * scale;
        scale *= 0.5;
    }

    bool append0 = false;
    for (int i = 0; i < targetNrs.Length; i++)
    {
        if (targetNrs[i] < 3)
        {
            targetNrs[i]++;
            break;
        }
        else
        {
            targetNrs[i] = 0;
            if (i == targetNrs.Length - 1)
            {
                append0 = true;
            }
        }
    } // this is the mapping pattern based on a 4 quadrant approach, watch the video to understand it 

    string targetString = "";

    for (int i = 0; i < targetNrs.Length; i++)
    {
        targetString += targetNrs[i];
    }

    if (append0)
        targetString += "0";

    mapTargets[listNr] = targetString;

    return target;
}

public class Target
{ // this class describes a target with all the information that goes with it.      
    public int time;    // time since last scan (ticks)      
    public string name;
    public long entityId;
    public int type;
    public int relation;
    public int countdown; // countdown to next scan      
    public Vector3D velocity; // velocity of the target       
    public Vector3D position;  // the coordinates of the target      
    public double size; // the diagonal size of the bounding box 

    public override string ToString()
    {
        return time + "," + countdown + "," + type + "," + relation + "," + entityId + "," + position + "," + velocity + "," + size + "," + name;
    }
}

public class ScanPixel
{
    public long entityId;
    public int type;
    public int relation;
    public Vector3D hitPosition; // the point on the target where it was hit  
    public int pixelNumber;
    public int pixelDepth;

    public override string ToString()
    {
        return type + "," + relation + "," + entityId + "," + hitPosition + "," + pixelNumber + ',' + pixelDepth;
    }
}

Vector3D GetVector3D(string rString)
{  // convert String to Vector3D, there is also a Parse option, I know, but this works                    
    if (rString == "0") rString = "(X:0 Y:0 Z:0)";
    string[] temp = rString.Substring(1, rString.Length - 2).Split(' ');
    double x = double.Parse(temp[0].Substring(2, temp[0].Length - 2));
    double y = double.Parse(temp[1].Substring(2, temp[1].Length - 2));
    double z = double.Parse(temp[2].Substring(2, temp[2].Length - 2));
    Vector3D rValue = new Vector3D(x, y, z);
    return rValue;
}

private Vector3D GetPerpendicular(Vector3D originalVector)
{
    Vector3D x = new Vector3D(1, 0, 0);
    Vector3D y = new Vector3D(0, 1, 0);
    Vector3D z = new Vector3D(0, 0, 1);
    Vector3D crossX = originalVector.Cross(x);
    Vector3D crossY = originalVector.Cross(y);
    Vector3D crossZ = originalVector.Cross(z);

    if (crossX != Vector3D.Zero) return Vector3D.Normalize(crossX);
    else if (crossY != Vector3D.Zero) return Vector3D.Normalize(crossY);
    else return Vector3D.Normalize(crossZ);

} // this method gives you any perpendicular vector given a vector as input 

void UpdateTargetlist(MyDetectedEntityInfo info)
{
    bool newTarget = true;
    for (int i = 0; i < targets.Count; i++)
    {
        if (targets[i].entityId == info.EntityId) // we check if the entityId is known 
        {
            newTarget = false;
            targets[i].velocity = info.Velocity;
            targets[i].time = 0;
            targets[i].countdown = trackDelay;
            targets[i].position = info.Position;
            targets[i].relation = (int)info.Relationship;
            targets[i].size = info.BoundingBox.Size.Length() / 2;
            messageString.Append(";" + targets[i]);
            break;
        }
    }

    if (newTarget)
    {
        string name = info.Name;

        string[] charsToRemove = new string[] { ",", ";", "\n" };
        foreach (var c in charsToRemove)
        {
            name = name.Replace(c, string.Empty);
        }

        if (name == "")
            name = "Nameless";

        // we remove all unwanted characters and make sure the target has a name 

        targets.Add(new Target
        {
            time = 0,
            countdown = trackDelay,
            position = info.Position,
            velocity = info.Velocity,
            type = (int)info.Type,
            relation = (int)info.Relationship,
            entityId = info.EntityId,
            size = info.BoundingBox.Size.Length() / 2,
            name = name
        });

        messageString.Append(";" + targets.Last());
    }
}

//OIS Comm system. This is not my work        
List<RadioMessage> highPriority = new List<RadioMessage>();
List<RadioMessage> mediumPriority = new List<RadioMessage>();
List<RadioMessage> lowPriority = new List<RadioMessage>();

class RadioMessage
{
    public IMyRadioAntenna Radio { get; set; }
    public string To { get; set; }
    public string From { get; set; }
    public string Options { get; set; }
    public string Message { get; set; }
    public MyTransmitTarget TransmitTarget { get; set; }
    public RadioMessage(IMyRadioAntenna radio, string to, string from, string options, string message, MyTransmitTarget transmitTarget = MyTransmitTarget.Default)
    {
        Radio = radio;
        To = to;
        From = from;
        Options = options;
        Message = message;
        TransmitTarget = transmitTarget;
    }
}

void HighPriority(List<IMyRadioAntenna> radios, string to, string from, string options, string message,
    MyTransmitTarget transmitTarget = MyTransmitTarget.Default, bool append = false)
{
    OutGoingMessage(ref highPriority, radios, to, from, options, message, transmitTarget, append);
}

void MediumPriority(List<IMyRadioAntenna> radios, string to, string from, string options, string message,
MyTransmitTarget transmitTarget = MyTransmitTarget.Default, bool append = false)
{
    OutGoingMessage(ref mediumPriority, radios, to, from, options, message, transmitTarget, append);
}

void LowPriority(List<IMyRadioAntenna> radios, string to, string from, string options, string message,
MyTransmitTarget transmitTarget = MyTransmitTarget.Default, bool append = false)
{
    OutGoingMessage(ref lowPriority, radios, to, from, options, message, transmitTarget, append);
}

void OutGoingMessage(ref List<RadioMessage> mlist, List<IMyRadioAntenna> radios, string to, string from, string options, string message,
MyTransmitTarget transmitTarget = MyTransmitTarget.Default, bool append = false)
{
    if (append)
    {
        bool added = false;
        for (int i = 0; i < mlist.Count; ++i)
        {
            for (int j = 0; j < radios.Count; ++j)
            {
                if (mlist[i].Radio == radios[j] && mlist[i].To == to &&
                    mlist[i].From == from && mlist[i].Options == options &&
                    mlist[i].TransmitTarget == transmitTarget)
                {
                    mlist[i].Message += "\n" + message;
                    added = true;
                }
            }
        }
        if (!added)
        {
            for (int i = 0; i < radios.Count; ++i)
            {
                mlist.Add(new RadioMessage(radios[i], to, from, options, message, transmitTarget));
            }
        }
    }
    else
    {
        for (int i = 0; i < radios.Count; ++i)
        {
            mlist.Add(new RadioMessage(radios[i], to, from, options, message, transmitTarget));
        }
    }
}

void SendMessages()
{
    bool sent = false;
    for (int i = 0; i < highPriority.Count; ++i)
    {
        sent = highPriority[i].Radio.TransmitMessage($"MSG;{highPriority[i].To};{highPriority[i].From};{highPriority[i].Options};{highPriority[i].Message}", highPriority[i].TransmitTarget);
        if (sent)
        {
            if (highPriority.Remove(highPriority[i])) --i;
        }
    }
    for (int i = 0; i < mediumPriority.Count; ++i)
    {
        sent = mediumPriority[i].Radio.TransmitMessage($"MSG;{mediumPriority[i].To};{mediumPriority[i].From};{mediumPriority[i].Options};{mediumPriority[i].Message}", mediumPriority[i].TransmitTarget);
        if (sent)
        {
            if (mediumPriority.Remove(mediumPriority[i])) --i;
        }
    }
    for (int i = 0; i < lowPriority.Count; ++i)
    {
        sent = lowPriority[i].Radio.TransmitMessage($"MSG;{lowPriority[i].To};{lowPriority[i].From};{lowPriority[i].Options};{lowPriority[i].Message}", lowPriority[i].TransmitTarget);
        if (sent)
        {
            if (lowPriority.Remove(lowPriority[i])) --i;
        }
    }
}
