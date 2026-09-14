/// <summary> 
// this script has to be used in conjunction with a data collection script (LIDAR) and is only used for visualization 
/// </summary> 
static string depthPanelString = "[Depth]";
// the name of the depthscan LCD has to have this tag 
static string scanPanelString = "[Scan]";
// the name of the scan LCD has to have this tag 
static string targetPanelString = "[Target]";
// the name of the LCD that displays your target info has to have this tag 
static string mapPanelString = "[Map]";
// the name of the map LCD has to have this tag 
static string timerBlockString = "[MapTimer]";
// the timer to trigger this program has to have this tag 
static string mapTurretString = "[MapTurret]";
// the turret used to target stuff on the map and scan panels has to have this tag 
static string guiString = "[GUI]";
// All LCDs that you will be targeting with the turret have to have this tag 
static string firePBString = "[Fire]";
// the name of your PB used to fire stuff has to have this tag 
static string mapControllerString = "[MapController]";
// the name of the flight chair controlling the map has to have this tag 
static int mapResY = 60;

static char whitePixel = rgbSquare(7, 7, 7);
static char redPixel = rgbSquare(7, 0, 0);
static char greenPixel = rgbSquare(0, 7, 0);
static char bluePixel = rgbSquare(0, 0, 7);
static char purplePixel = rgbSquare(7, 0, 7);
static char yellowPixel = rgbSquare(7, 7, 0);
static char cyanPixel = rgbSquare(0, 0, 7);
static char blackPixel = rgbSquare(0, 0, 0);

bool timerTriggered = false;
bool targetLocked = false;
// you can lock a target to make sure it can't be changed and then fire stuff at it with the firing PB 

List<Target> targets = new List<Target>();
// the targetlist, this is saved between sessions 

List<LCD> guiLCDs = new List<LCD>(); // LCDs with the [GUI] tag. 
static Dictionary<IMyTextPanel, LCD> lcdDict = new Dictionary<IMyTextPanel, LCD>();
ScanPixel[] scanPixels = new ScanPixel[178 * 178];

StringBuilder scanPixelString = new StringBuilder(179 * 178);
StringBuilder depthPixelString = new StringBuilder(179 * 178);
// these stringbuilders represent the 178x178 resolution scans. The 1 extra is the "next line" character 

StringBuilder mapPixelString = new StringBuilder((2 * mapResY + 1) * mapResY);
//The map resolution is configurable. You have to use a large panel for this,  
//because the map represents a 360 by 180 degrees view. 

IMyTextPanel scanPanel, depthPanel, targetPanel, mapPanel, targetedPanel;
// the targetedPanel is the GUI panel that the turret is aiming at 

IMyProgrammableBlock FiringPB;
//the firing programmable block, used to run a program to fire... anything.  

int targetX = 0; // the x-coordinate of the pixel the turret is aiming at 
int targetY = 0; // the x-coordinate of the pixel the turret is aiming at 
int mapTargetX = 0; // the x-coordinate of the Mappixel the turret is/was aiming at 
int mapTargetY = 0; // the y-coordinate of the Mappixel the turret is/was aiming at 
Target mapTarget; // the Target-instance that comes with the map target the turret is aiming at 
int mapTargetNr = 0;
bool ADold = false;
ScanPixel pixelTarget; // the ScanPixel-instance that is being targeted with the turret 

Vector3D targetPosition = Vector3D.Zero;
// this is the position that is fed to the firing PB if there is one. It uses Vector3Ds turned into strings. 

IMyTimerBlock timer;
IMyLargeInteriorTurret turret;
IMyShipController controller;

public void RunFiringPB(Vector3D target)
{
    FiringPB.TryRun(target.ToString());
    // This method can be modified to suit the input required by the firing PB. 
}

static char rgbSquare(byte r, byte g, byte b)
{
    return (char)(0xe100 + (r << 6) + (g << 3) + b);
    // This selects the right color monospace-font character. 
}

public Program()
{
    turret = (IMyLargeInteriorTurret)GridTerminalSystem.GetBlockWithName(mapTurretString);

    GridTerminalSystem.SearchBlocksOfName(scanPanelString, null, b =>
    {
        if (b is IMyTextPanel)
        {
            scanPanel = b as IMyTextPanel;
        }
        return false;
    });

    GridTerminalSystem.SearchBlocksOfName(depthPanelString, null, b =>
    {
        if (b is IMyTextPanel)
        {
            depthPanel = b as IMyTextPanel;
        }
        return false;
    });

    GridTerminalSystem.SearchBlocksOfName(targetPanelString, null, b =>
    {
        if (b is IMyTextPanel)
        {
            targetPanel = b as IMyTextPanel;
        }
        return false;
    });

    GridTerminalSystem.SearchBlocksOfName(mapPanelString, null, b =>
    {
        if (b is IMyTextPanel)
        {
            mapPanel = b as IMyTextPanel;
        }
        return false;
    });

    GridTerminalSystem.SearchBlocksOfName(timerBlockString, null, b =>
    {
        if (b is IMyTimerBlock)
        {
            timer = b as IMyTimerBlock;
        }
        return false;
    });

    GridTerminalSystem.SearchBlocksOfName(guiString, null, b =>
    {
        if (b is IMyTextPanel)
        {
            LCD lcd = new LCD();
            // the LCD class is a class that keeps track of some properties of the panel being used. 
            lcd.panel = (IMyTextPanel)b;
            lcd.large = b.BlockDefinition.SubtypeName.Contains("LCDPanelWide");
            // large or small panel? 

            lcd.right = Base6Directions.GetIntVector(Base6Directions.GetFlippedDirection(b.Orientation.Left));
            // this is the vector pointing to the right of the panel, used in calculating the x-coordinate pixel. 
            lcd.up = Base6Directions.GetIntVector(b.Orientation.Up);
            // this is the vector pointing upwards from the panel, used in calculating the y-coordinate pixel. 
            lcd.forward = Base6Directions.GetIntVector(b.Orientation.Forward);
            // this is the vector pointing to the back of panel (when looking at it) 
            lcd.position = (Vector3)b.Position + 0.415 * lcd.forward;
            // the panel position is to the back of the block it occupies, this is why we add the forward vector with some constant 
            guiLCDs.Add(lcd);
            // we add the LCD to a list of GUI LCDs that are targetable by the map turret. 
            lcdDict.Add(lcd.panel, lcd);
        }
        return false;
    });

    if (mapPanel != null)
    {
        LCD mapLCD = lcdDict[mapPanel];
        mapLCD.resY = mapResY;
        mapLCD.MakeBlackString();
        ResetMapPixels();
    }
    // the map panel starts with a full string of black pixels, the other two GUI panels are not refreshed, only appended to.  

    GridTerminalSystem.SearchBlocksOfName(timerBlockString, null, b =>
    {
        if (b is IMyTimerBlock)
        {
            timer = b as IMyTimerBlock;
        }
        return false;
    });

    GridTerminalSystem.SearchBlocksOfName(mapControllerString, null, b =>
    {
        if (b is IMyShipController)
        {
            controller = b as IMyShipController;
        }
        return false;
    });

    if (Storage.Length > 0)
    {
        var parts = Storage.Split(';');

        for (int i = 0; i < parts.Length; i++)
        {
            string[] tar = parts[i].Split(',');
            if (tar.Length < 9) continue;
            // The storage only stores targets, not scanpixels. Targets number in the hundreds at most, scanpixels number over 30k 
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
    // The loading of the storage string could perhaps be improved upon by using TryParse 

}

public Vector3I ToVector3I(Vector3 v)
{
    return new Vector3I((int)Math.Round(v.X), (int)Math.Round(v.Y), (int)Math.Round(v.Z));
}

public void Save()
{
    StringBuilder tempstring = new StringBuilder();

    for (int j = 0; j < targets.Count; j++)
    {
        tempstring.Append(";" + targets[j]);
    }
    Storage = tempstring.ToString();
}

public void ResetScanAndDepthPixels()
{
    if (scanPanel != null)
    {
        scanPixelString.Clear();
    }

    if (depthPanel != null)
    {
        depthPixelString.Clear();
    }
}

public void ResetMapPixels()
{
    if (mapPanel != null)
    {
        mapPixelString.Clear();
        mapPixelString.Append(lcdDict[mapPanel].blackString);
    }
}

public void ReceiveData(string argument)
{
    var parts = argument.Split(';');
    for (int i = 4; i < parts.Length; i++)
    {
        // the first 4 parts of the message contains no information used in this script.  
        string message = parts[i];
        string[] tar = message.Split(',');
        // the length of 9 is the only selection criterion, this could be made safer 
        if (tar.Length == 9)
        {
            Target target = new Target
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
            };

            string name = target.name;

            string[] charsToRemove = new string[] { ",", ";", "\n" };
            foreach (var c in charsToRemove)
            {
                name = name.Replace(c, string.Empty);
            }

            target.name = name;

            // just to be safe, we remove all unwanted characters from the targets' name 

            if (Me.CubeGrid.EntityId != target.entityId)
                UpdateTargetlist(target);

            // we don't include ourselves in the targetlist, that would be confusing 
        }

        else if (tar.Length == 6)
        {
            // scanpixels contain somewhat less information. The main difference is the hitPosition and pixelDepth 
            ScanPixel pixel = new ScanPixel
            {
                type = int.Parse(tar[0]),
                relation = int.Parse(tar[1]),
                entityId = long.Parse(tar[2]),
                hitPosition = GetVector3D(tar[3]),
                pixelNumber = int.Parse(tar[4]),
                pixelDepth = int.Parse(tar[5])
            };

            int pixelNr = pixel.pixelNumber;

            if (pixelNr == 0) // this is a new scan so the scanpanel and depthpanel have to be reset 
                ResetScanAndDepthPixels();

            scanPixels[pixel.pixelNumber] = pixel;

            char pixelChar = PickColorChar(pixel);
            // PickColorChar is a method that chooses the color based on type and relations with the player 

            if (pixelNr % 178 == 0)
            {
                depthPixelString.Append("\n");
                scanPixelString.Append("\n");
            }
            //divide the pixelnumber by 178 to see if the end of the line has been reached 

            scanPixelString.Append(pixelChar);

            int depth = pixel.pixelDepth;
            // the depthpixels are colored by depth. The pixels number through red, blue and green to show depth as much as possible 
            pixelChar = (char)(depth < 0 ? 0 : depth > 511 ? 511 : depth);
            depthPixelString.Append((char)(0xe100 + 511 - pixelChar)); //57600 - pixelchar  
        }
        else if (parts[i].Contains("scanMiss"))
        {
            int pixelNr = int.Parse(tar[1]);

            // misses also have to be counted because they are represented by black pixels that are appended 

            if (pixelNr == 0)
                ResetScanAndDepthPixels();

            if (pixelNr % 178 == 0)
            {
                depthPixelString.Append("\n");
                scanPixelString.Append("\n");
            }
            scanPixelString.Append(blackPixel);
            depthPixelString.Append(blackPixel);
            scanPixels[pixelNr] = null;
        }
    }
    if (scanPanel != null)
        scanPanel.WritePublicText(scanPixelString);
    if (depthPanel != null)
        depthPanel.WritePublicText(depthPixelString);
}

public void UpdateTicks()
{
    for (int i = 0; i < targets.Count; i++)
    {
        targets[i].time++;
    }
    // this keeps track of how long it has been since the target was last detected 
}

public void PixelTargeting()
{
    if (targetedPanel != null && turret != null && (targetedPanel == scanPanel || targetedPanel == depthPanel) && !targetLocked)
    {
        // this enables you to get information about a pixel on the screen after it has been selected 
        // by turret targeting 
        pixelTarget = scanPixels[targetX + targetY * 178];
        StringBuilder targetString = new StringBuilder();
        if (pixelTarget != null && mapTarget != null)
        {
            targetPosition = pixelTarget.hitPosition;
            targetString.Append("\n" + "Target Info" + "\n" +
                "\n" + "Time since update: " + mapTarget.time.ToString() + " ticks" +
                "\n" + "Name: " + mapTarget.name +
                "\n" + "Id: " + pixelTarget.entityId +
                "\n" + "Type: " + (MyDetectedEntityType)mapTarget.type +
                "\n" + "Relation: " + (MyRelationsBetweenPlayerAndBlock)mapTarget.relation +
                "\n" + "Velocity: " + Math.Round(mapTarget.velocity.Length(), 1) + " m/s" +
                "\n" + "Hit Position: " + "X: " + Math.Round(pixelTarget.hitPosition.X) + " Y: " + Math.Round(pixelTarget.hitPosition.Y)
                    + " Z: " + Math.Round(pixelTarget.hitPosition.Z) +
                "\n" + "Size: " + Math.Round(mapTarget.size, 1) + " m");
        }
        else
        {
            targetString.Append("\n" + "Target Info" + "\n" +
                "\n" + "No Target");
        }
        targetPanel.WritePublicText(targetString);
    }
}

public void Main(string argument)
{
    ReceiveData(argument);

    if (argument == "")
    {
        UpdateTicks();
        TurretTargeting();
        UpdateMap();
        PixelTargeting();
    }

    if (argument.Contains("clean"))
    {
        // you may clean the targetlist with or without a specified time limit 
        // clean 500 will clean all targets that have not been detected for 500 ticks 
        string[] pieces = argument.Split(' ');
        int limit = 0;

        for (int i = 0; i < pieces.Length; i++)
        {
            int.TryParse(pieces[i], out limit);
        }

        if (limit > 0)
        {
            for (int i = targets.Count - 1; i >= 0; i--)
            {
                if (targets[i].time > limit)
                    targets.RemoveAt(i);
            }
        }
        else
            targets.Clear();
    }

    if (argument == "lock")
    {
        targetLocked = !targetLocked;
    } // lock the target to prevent it from being changed, you need to do this before firing 

    if (argument == "fire")
    {
        GridTerminalSystem.SearchBlocksOfName(firePBString, null, b =>
        {
            if (b is IMyProgrammableBlock) FiringPB = b as IMyProgrammableBlock;
            return false;
        });

        if (FiringPB != null)
        {
            RunFiringPB(targetPosition);
        }
    } // if a firing PB is configured, feed information to it and fire it 

    if (!timerTriggered)
    {
        timer.Trigger();
        timerTriggered = true;
    }

}

public Vector3D PerpendicularTurretVector(double azimuth, double elevation)
{
    double newElevation = 0.5 * Math.PI + elevation;
    double newAzimuth = azimuth;

    if (newElevation > 0.5 * Math.PI)
    {
        newElevation = Math.PI - newElevation;
        newAzimuth += Math.PI;
    }


    Vector3D direction;
    Vector3D.CreateFromAzimuthAndElevation(newAzimuth, newElevation, out direction);
    return direction;
}
// this generates a 1-length vector normal and upwards to the aim direction of the turret 

public void TurretTargeting()
{
    if (!turret.IsUnderControl)
        return;
    double azimuth = turret.Azimuth;
    double elevation = turret.Elevation;
    Vector3D turretPos = turret.Position;

    Vector3D direction;
    Vector3D.CreateFromAzimuthAndElevation(azimuth, elevation, out direction);
    Matrix or; turret.Orientation.GetMatrix(out or);
    direction = Vector3D.Transform(direction, MatrixD.Transpose(or));

    // the above 4 lines are necessary to calculate the vector pointing from the turret to the panels 

    Vector3D offset = PerpendicularTurretVector(azimuth, elevation);
    offset = Vector3D.Transform(offset, MatrixD.Transpose(or));
    turretPos += 0.19 * offset;

    // the offset has to be calculated and added because the turret sights are almost 0.5m above the actual turret hinge 

    targetedPanel = null;

    for (int i = 0; i < guiLCDs.Count; i++)
    {
        LCD lcd = guiLCDs[i];
        double dot = direction.Dot((Vector3D)lcd.forward);
        if (dot <= 0)
            continue;
        // if the dotproduct is below zero, the turret is looking at the back of the panel 

        double d = (lcd.position - turretPos).Dot((Vector3D)lcd.forward) / dot;
        Vector3D intersection = turretPos + d * direction;
        Vector3D dpos = intersection - lcd.position;

        // the above 3 lines consist of math to calculate the intersection of a line and a plane (the plane is the LCD) 

        double x = Vector3D.Dot(dpos, (Vector3D)lcd.right);
        double y = Vector3D.Dot(dpos, (Vector3D)lcd.up);

        double fac = 1.01; // this factor takes into account the fact that the edges of the LCD have no pixels,  
                           // that means the view of the LCD is compressed slightly 

        targetY = (int)Math.Round(0.5 * (lcd.resY - 1) - fac * y * lcd.resY);
        targetX = (int)Math.Round(0.5 * (lcd.resY - 1) + fac * x * lcd.resY);

        // the x and y start at 0 in the topleft corner of the LCD, instead of the center of the LCD,  
        // the above 2 lines take that into account 

        int resY = lcd.resY;

        if (targetY > resY - 1 || targetY < 0 || targetX < 0 || (targetX > resY - 1 && !lcd.large) || (targetX > 2 * resY - 1) && lcd.large)
            continue;
        // the above if checks if the turret aimpoint falls on the LCD. We need to differentiate between small and large LCDs 
        else
        {
            targetedPanel = lcd.panel;
            break;
        }
    }
}

public void UpdateMap()
{
    if (mapPanel == null)
        return;
    ResetMapPixels();

    Vector3D lcdPos = mapPanel.GetPosition();
    Matrix mapOr; mapPanel.Orientation.GetMatrix(out mapOr);
    Matrix mapWorld = mapPanel.WorldMatrix.GetOrientation();
    Vector3D mapForward = Vector3D.Transform(mapOr.Forward, MatrixD.Transpose(mapOr));
    Vector3D mapUp = Vector3D.Transform(mapOr.Up, MatrixD.Transpose(mapOr));
    Vector3D mapRight = Vector3D.Transform(mapOr.Right, MatrixD.Transpose(mapOr));
    Vector3D mapForwardWorld = Vector3D.Transform(mapForward, mapWorld);
    Vector3D mapUpWorld = Vector3D.Transform(mapUp, mapWorld);
    Vector3D mapRightWorld = Vector3D.Transform(mapRight, mapWorld);

    // The above lines transform the maps position and orientation to the world position and orientation 
    // This is necessary because we want to display targets that are straight ahead, in the middle of the map 
    // the top and bottom parts correspond to things above and below you 
    // the far left and the far right parts are behind you (the horizontal axis is a 360 degree axis) 

    double shortestDistSq = mapResY * mapResY;

    // this is used to target the closest pixel to the turret aim reticule 

    for (int i = 0; i < targets.Count; i++)
    {
        Vector3D aimVector = Vector3D.Normalize(targets[i].position - lcdPos);
        double dotForward = mapForwardWorld.Dot(aimVector);
        double dotUp = mapUpWorld.Dot(aimVector);
        double dotRight = mapRightWorld.Dot(aimVector);
        int x = mapResY + (int)Math.Round(mapResY * Math.Atan2(dotRight, dotForward) / Math.PI);
        int y = (int)Math.Round(mapResY * Math.Acos(dotUp) / Math.PI);
        // the above 5 lines consist of math to translate the world position to map angles. 

        x = x < 0 ? 0 : x > mapResY * 2 - 1 ? mapResY * 2 - 1 : x;
        y = y < 0 ? 0 : y > mapResY - 1 ? mapResY - 1 : y;
        // clamping the mappixels to the map resolution 

        char pixel = PickColorChar(targets[i]);
        mapPixelString[(mapResY * 2 + 1) * y + x] = pixel;

        if (targetedPanel == mapPanel && turret != null && !targetLocked && turret.IsUnderControl)
        {
            // this part is doing the calculation to obtain the closest target to the reticule 
            double distX = Math.Abs(targetX - x);
            double distY = Math.Abs(targetY - y);

            if (distX > mapResY)
                distX = 2 * mapResY - distX;
            if (distY > 0.5 * mapResY)
                distY = mapResY - 0.5 * mapResY;

            double dist = distX * distX + distY * distY;
            if (dist < shortestDistSq)
            {
                shortestDistSq = dist;
                mapTarget = targets[i];
                mapTargetX = x;
                mapTargetY = y;
            }
        }
        else
        {
            if (mapTarget == targets[i])
            {
                mapTargetX = x;
                mapTargetY = y;
            }
        }

        if (targetedPanel != null && (targetedPanel == depthPanel || targetedPanel == scanPanel) && !targetLocked)
        {
            pixelTarget = scanPixels[targetX + targetY * 178];
            if (pixelTarget != null && pixelTarget.entityId == targets[i].entityId)
            {
                mapTarget = targets[i];
                mapTargetX = x;
                mapTargetY = y;
            }
            // this part adds information to the targeted pixels since ScanPixels lack some things 
        }
    }

    if (controller != null && targets.Count > 0) // ADold checks if the button was pressed last tick 
    {
        if (controller.MoveIndicator.X != 0)
        {
            if (!ADold)
            {
                if (controller.MoveIndicator.X > 0)
                {
                    mapTargetNr += 1;
                    if (mapTargetNr > targets.Count - 1)
                        mapTargetNr = 0;
                }
                else if (controller.MoveIndicator.X < 0)
                {
                    mapTargetNr -= 1;
                    if (mapTargetNr < 0)
                        mapTargetNr = targets.Count - 1;
                }
                mapTarget = targets[mapTargetNr];
                targetPosition = mapTarget.position;
                targetLocked = true;
                ADold = true;
            }
        }
        else
        {
            ADold = false;
        }

    } // using a chair to select targets with WASD (AD actually) 



    if (mapTarget != null)
    {
        if (mapTargetX > 0)
            mapPixelString[(mapResY * 2 + 1) * mapTargetY + mapTargetX - 1] = whitePixel;
        if (mapTargetX < 2 * mapResY)
            mapPixelString[(mapResY * 2 + 1) * mapTargetY + mapTargetX + 1] = whitePixel;
        if (mapTargetY > 0)
            mapPixelString[(mapResY * 2 + 1) * (mapTargetY - 1) + mapTargetX] = whitePixel;
        if (mapTargetY < mapResY)
            mapPixelString[(mapResY * 2 + 1) * (mapTargetY + 1) + mapTargetX] = whitePixel;
        // this part makes the white cross around the target on the map 
    }

    mapPanel.WritePublicText(mapPixelString);

    if (targetPanel != null && mapTarget != null && (targetedPanel == mapPanel || controller.MoveIndicator.X != 0))
    {
        targetPosition = mapTarget.position;
        StringBuilder targetString = new StringBuilder();

        targetString.Append("\n" + "Target Info" + "\n" +
            "\n" + "Time since update: " + mapTarget.time.ToString() + " ticks" +
            "\n" + "Name: " + mapTarget.name +
            "\n" + "Id: " + mapTarget.entityId +
            "\n" + "Type: " + (MyDetectedEntityType)mapTarget.type +
            "\n" + "Relation: " + (MyRelationsBetweenPlayerAndBlock)mapTarget.relation +
            "\n" + "Velocity: " + Math.Round(mapTarget.velocity.Length(), 1) + " m/s" +
            "\n" + "Position: " + "X: " + Math.Round(mapTarget.position.X) + " Y: " + Math.Round(mapTarget.position.Y) + " Z: " + Math.Round(mapTarget.position.Z) +
            "\n" + "Size: " + Math.Round(mapTarget.size, 1) + " m");
        targetPanel.WritePublicText(targetString);
    }
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

void UpdateTargetlist(Target targetUpdate)
{
    bool newTarget = true;    // if this is true we have a new target, it may be made false below.    
    for (int i = 0; i < targets.Count; i++)
    {
        if (targets[i].entityId == targetUpdate.entityId) // check if the target is known 
        {
            newTarget = false;
            targets[i].velocity = targetUpdate.velocity;
            targets[i].time = targetUpdate.time;
            targets[i].countdown = targetUpdate.countdown;
            targets[i].position = targetUpdate.position;
            targets[i].relation = targetUpdate.relation;
            break;
        }
    }

    if (newTarget) // this is a new target 
    {
        targets.Add(new Target
        {
            time = targetUpdate.time,
            countdown = targetUpdate.countdown,
            position = targetUpdate.position,
            velocity = targetUpdate.velocity,
            type = targetUpdate.type,
            relation = targetUpdate.relation,
            entityId = targetUpdate.entityId,
            name = targetUpdate.name,
            size = targetUpdate.size
        });
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

char PickColorChar(ScanPixel pixel)
{
    char pixelChar;

    if (pixel.type == (int)MyDetectedEntityType.Asteroid || pixel.type == (int)MyDetectedEntityType.Planet)
        pixelChar = yellowPixel;
    else if (pixel.type == (int)MyDetectedEntityType.LargeGrid || pixel.type == (int)MyDetectedEntityType.SmallGrid)
    {
        if (pixel.relation == (int)MyRelationsBetweenPlayerAndBlock.Enemies)
            pixelChar = redPixel;
        else if (pixel.relation == (int)MyRelationsBetweenPlayerAndBlock.Owner || pixel.relation == (int)MyRelationsBetweenPlayerAndBlock.FactionShare)
            pixelChar = greenPixel;
        else
            pixelChar = bluePixel;
    }
    else if (pixel.type == (int)MyDetectedEntityType.CharacterHuman || pixel.type == (int)MyDetectedEntityType.CharacterOther)
        pixelChar = cyanPixel;
    else
        pixelChar = purplePixel;

    return pixelChar;
}

char PickColorChar(Target target)
{
    char pixelChar;

    if (target.type == (int)MyDetectedEntityType.Asteroid || target.type == (int)MyDetectedEntityType.Planet)
        pixelChar = yellowPixel;
    else if (target.type == (int)MyDetectedEntityType.LargeGrid || target.type == (int)MyDetectedEntityType.SmallGrid)
    {
        if (target.relation == (int)MyRelationsBetweenPlayerAndBlock.Enemies)
            pixelChar = redPixel;
        else if (target.relation == (int)MyRelationsBetweenPlayerAndBlock.Owner || target.relation == (int)MyRelationsBetweenPlayerAndBlock.FactionShare)
            pixelChar = greenPixel;
        else
            pixelChar = bluePixel;
    }
    else if (target.type == (int)MyDetectedEntityType.CharacterHuman || target.type == (int)MyDetectedEntityType.CharacterOther)
        pixelChar = cyanPixel;
    else
        pixelChar = purplePixel;

    return pixelChar;
}

public class LCD

{ // this class describes an LCD panel 
    public IMyTextPanel panel;
    public Vector3D position;
    public Vector3I up;
    public Vector3I right;
    public Vector3I forward;
    public int resY = 178;
    public bool large = false;
    public string blackString = "";

    public void MakeBlackString()
    {
        int resX = large ? 2 * resY : resY;
        StringBuilder blackBuilder = new StringBuilder(resY * (resX + 1));

        for (int i = 0; i < resY; i++)
        {
            blackBuilder.Append(blackPixel, resX);
            blackBuilder.Append('\n');
        }

        blackString = blackBuilder.ToString();
    }
}
