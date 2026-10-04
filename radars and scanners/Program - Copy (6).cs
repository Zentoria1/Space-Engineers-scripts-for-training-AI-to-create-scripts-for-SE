// Camera Scan script
// BY DerLaCroix  
// Version 0.1 (17.12.2016)   
// Script will scan for targets at given range (20000m default)
//If different range is wanted (faster scanning, less power), set the Run parameter to distance,
//and trigger Timer block with settings "Run with default parameter" - it will then always use the
//range indicated in the Run textbox.
//
// Scan speed is about 3000m/tick, so longer range scans take more time.
//
//Can alternatively print to a text panel or into the cockpit lcds (any LCD needs to be set to "Text and Images" to display the text)
//
//
//NEEDS 
//"SCAN_CAMERA" Camera to scan ;  
//"SCAN_INFO" Lcd Text Panel OR a COCKPIT_NAME and LCD INDEX (0= Top center, 1= left ,2= right, etc...)
//Timer block to run continously with default settings or trigger it once via toolbar (prefferred)

//TO EDIT BY USER
using Sandbox.Game.Entities;
using Sandbox.Game.GameSystems;
using Sandbox.ModAPI.Ingame;
using System;
using System.Collections.Generic;
using System.Text;
using VRageMath;
using static System.Net.Mime.MediaTypeNames;

private String cameraName = "SCAN_CAMERA";
private String displayname = "SCAN_INFO";
private String cockpitName = "";
private int lcdIndex = 0;


//NO EDIT

double RANGE = 20000;
float PITCH = 0;
float YAW = 0;
private IMyCameraBlock camera;
private IMyTextPanel text;
private IMyTextSurface lcd;
private IMyCockpit cockpit;
private bool firstrun = true;
private MyDetectedEntityInfo info;
private StringBuilder sb = new StringBuilder();


void Hud(String text)
{
    List<IMyTerminalBlock> blocks = new List<IMyTerminalBlock>();
    GridTerminalSystem.GetBlocksOfType<IMyRadioAntenna>(blocks);

    blocks[0].CustomName = text;
}


void Main(String setRange)
{

    if (setRange != "")
    {
        RANGE = Convert.ToDouble(setRange.Trim());
    }
    if (firstrun)
    {
        firstrun = false;
        camera = GridTerminalSystem.GetBlockWithName(cameraName) as IMyCameraBlock;
        Echo("\nCamera ID to be used: " + camera.Name);
        if (string.IsNullOrWhiteSpace(cockpitName))
        {
            Echo("\nNo Cockpit defined.");
            text = GridTerminalSystem.GetBlockWithName(displayname) as IMyTextPanel;
            Echo("\nPanel found: ");
            lcd = (IMyTextSurface)text;
            Echo("\n" + lcd.Name);
        }
        else
        {
            Echo("\nCockpit found: ");
            cockpit = GridTerminalSystem.GetBlockWithName(cockpitName) as IMyCockpit;
            Echo(cockpit.CustomName);
            lcd = cockpit.GetSurface(lcdIndex);
            Echo("\n" + lcd.Name);
        }
        camera.EnableRaycast = true;
    }

    Echo("\n\nInit done ");
    sb.Clear();
    if (camera.CanScan(RANGE))
    {

        info = camera.Raycast(RANGE, PITCH, YAW);
        sb.Append("Last scan at range: " + RANGE);
    }
    else
    {


        sb.Append("Powering up for scan... Current range: " + camera.AvailableScanRange);

    }


    if (info.EntityId == 0)
    {
        //nothing found
        sb.AppendLine();
        sb.Append("No target found within  " + RANGE + " m");
        lcd.WriteText(sb, false);
        return;
    }
    if (info.HitPosition.HasValue)
    {
        sb.AppendLine();
        sb.Append("Target found at " + Vector3D.Distance(camera.GetPosition(),
            info.HitPosition.Value).ToString("0.00") + "m range");
    }


    String size = info.BoundingBox.Size.ToString("0.000");
    String printSize = "";
    double volume = 1;
    String unitName = "m3";
    if (size != null || size != "")
    {
        size = size.Replace("{", "").Replace("}", "").Replace("X", "").Replace("Y", "").Replace("Z", "").Replace(" ", "");
        size = size.Trim();
        String[] sizes = size.Split(':');

        double factor = 1;

        if (info.Type.ToString() == "SmallGrid")
        {
            factor = 4;
            unitName = "blocks";
        }
        else if (info.Type.ToString() == "LargeGrid")
        {
            factor = 16;
            unitName = "blocks";
        }
        foreach (String measure in sizes)
        {
            if (measure == "")
            {
                //first value
                continue;
            }

            volume = volume * Math.Round(Convert.ToDouble(measure), 0);

            printSize += (Convert.ToDouble(measure)).ToString("0.00") + " X ";
        }
        volume = volume / factor;
    }
    printSize = printSize.Remove(printSize.Length - 2);
    printSize += "m";

    //X:5.302 Y:3.072 Z:4.252

    sb.AppendLine();
    sb.Append("Found grid ID: " + info.EntityId);
    sb.AppendLine();
    sb.Append("Name of object: " + info.Name);
    sb.AppendLine();
    sb.Append("Type of object: " + info.Type);
    sb.AppendLine();
    sb.Append("Current velocity: " + info.Velocity.ToString("0.000"));
    sb.AppendLine();
    sb.Append("Relationship: " + info.Relationship);
    sb.AppendLine();
    sb.Append("Size: " + printSize);
    sb.AppendLine();
    sb.Append("Estimated volume: " + volume + " " + unitName);
    sb.AppendLine();
    sb.Append("Position: " + info.Position.ToString("0.000"));
    sb.AppendLine();
    sb.Append("GPS:object" + info.Position.ToString("0.000").Replace("{", "").Replace("}", "").Replace("X", "")
          .Replace("Y", "").Replace("Z", "").Replace(" ", "") + ":");

    if (info.HitPosition.HasValue)
    {
        sb.AppendLine();
        sb.Append("Hit point: " + info.HitPosition.Value.ToString("0.000"));
        sb.AppendLine();
        sb.Append("GPS:objectHit" + info.HitPosition.Value.ToString("0.000").Replace("{", "").Replace("}", "").Replace("X", "")
              .Replace("Y", "").Replace("Z", "").Replace(" ", "") + ":");
    }

    lcd.WriteText(sb, false);
}

void Hunt()
{
    sb.Clear();
    var list = new List<IMyTerminalBlock>();
    GridTerminalSystem.GetBlocksOfType<IMyRemoteControl>(list);
    if (list.Count > 0)
    {
        sb.AppendLine();
        sb.Append("Hunting for Player");
        var remote = list[0] as IMyRemoteControl;
        remote.ClearWaypoints();
        Vector3D player = new Vector3D(0, 0, 0);
        Vector3D mindistance = new Vector3D(0, 0, 5000);
        Vector3D currentposition = new Vector3D(0, 0, 0);
        bool success = remote.GetNearestPlayer(out player);
        if (success)
        {
            sb.Append("Player found");
            currentposition = remote.GetPosition();
            if (Vector3D.DistanceSquared(player, currentposition) < 4000 * 4000)
            {
                return;
            }
            else
            {
                player = player - mindistance;
                remote.AddWaypoint(player, "Player");
                //remote.SetAutoPilotEnabled(true); 
            }
        }
    }
    lcd.WriteText(sb);

}




