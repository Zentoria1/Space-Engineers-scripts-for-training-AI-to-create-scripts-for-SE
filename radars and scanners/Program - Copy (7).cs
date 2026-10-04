#region Script Body 

using Sandbox.Game.GameSystems;
using Sandbox.ModAPI.Ingame;
using System;
using System.Collections.Generic;
using VRageMath;

IMyGridTerminalSystem Term;
const string CameraName = "Camera Nose";
const string LCDName = "Radar Scan LCD";
IMyCameraBlock Camera;
IMyTextPanel LCD;
List<IMyRadar> Radars = new List<IMyRadar>();

public Program()
{
    Term = GridTerminalSystem;
    Camera = Term.GetBlockWithName(CameraName) as IMyCameraBlock;
    LCD = Term.GetBlockWithName(LCDName) as IMyTextPanel;
    if (Camera == null) throw new Exception("\nCamera cannot be found");
    if (LCD == null) throw new Exception("\nLCD panel cannot be found");
    List<IMyUpgradeModule> RadarBlocks = new List<IMyUpgradeModule>();
    Term.GetBlocksOfType(RadarBlocks, x => x.BlockDefinition.SubtypeId.Contains("Radar"));
    if (RadarBlocks.Count == 0) throw new Exception("\nNo Radars were found");
    foreach (var RadarBlock in RadarBlocks)
        Radars.Add(new IMyRadar(RadarBlock));
    if (Radars.Count == 0) throw new Exception("\nNo Radars were initialized");
    Camera.EnableRaycast = true;
}

public void Main(string Input)
{
    if (Input == "ScanForward") ScanForward();
}

void ScanForward()
{
    IMyRadar Radar = Radars[0];
    MyDetectedEntityInfo Target = Camera.Raycast(3000);
    if (Target.IsEmpty())
    {
        Clear();
        Print("=== Radar Scan ===");
        Print("");
        Print("Scan failed: Cannot locate target by raycast");
        return;
    }

    bool TargetFound = Radar.RadarData.Any(x => x.EntityId == Target.EntityId);
    if (!TargetFound)
    {
        Clear();
        Print("=== Radar Scan ===");
        Print("");
        Print("Scan failed: Cannot find target by Radar");
        return;
    }

    if (!Radar.CanScan(Target))
    {
        Clear();
        Print("=== Radar Scan ===");
        Print("");
        Print($"Target type/name: {Target.Type.ToString()}/{Target.Name}");
        Print($"Scan failed: Cannot scan target by Radar{(TargetFound ? ", though target is found" : "")}");
        return;
    }

    PrintoutScan(Radar.ScanTarget(Target));
}

void PrintoutScan(List<IMyRadar.TargetBlock> Scan)
{
    Clear();
    Print("=== Radar Scan ===");
    Print("");
    Print($"Jumpdrives: {Scan.Count(x => x.TypeID == "JumpDrive" && x.Functional)}");
    Print($"Turrets (G/M/I/T): {Scan.Count(x => x.TypeID == "LargeGatlingTurret" && x.Functional)}/{Scan.Count(x => x.TypeID == "LargeMissileTurret" && x.Functional)}/{Scan.Count(x => x.TypeID == "InteriorTurret" && x.Functional)}/{Scan.Count(x => x.TypeID.Contains("Turret") && x.Functional)}");
    Print($"Guns (G/M): {Scan.Count(x => x.TypeID == "SmallGatlingGun" && x.Functional)}/{Scan.Count(x => (x.TypeID == "SmallMissileLauncher" && x.Functional) || (x.TypeID == "SmallMissileLauncherReload" && x.Functional))}");
    Print($"Gyros: {Scan.Count(x => x.TypeID == "Gyro" && x.Functional)}");
    Print($"Thrusters: {Scan.Count(x => x.TypeID == "Thrust" && x.Functional)}");
}

void Clear()
{
    LCD.WritePublicText("");
}

void Print(string Output, bool AppendLF = true)
{
    LCD.WritePublicText(Output + (AppendLF ? "\n" : ""), true);
}

sealed class IMyRadar
{
    public IMyUpgradeModule RadarBlock { get; private set; }
    public Vector3D Position { get; private set; }
    public HashSet<MyDetectedEntityInfo> RadarData => RadarBlock.GetValue<HashSet<MyDetectedEntityInfo>>("RadarData");
    public bool Enabled
    {
        get { return RadarBlock.Enabled; }
        set { RadarBlock.Enabled = value; }
    }
    public int RadarPower
    {
        get { return (int)RadarBlock.GetValueFloat("RadarPower"); }
        set { RadarBlock.SetValueFloat("RadarPower", value); }
    }
    public int MaxRadarPower => (int)RadarBlock.GetProperty("RadarPower").AsFloat().GetMaximum(RadarBlock);
    public bool ActiveMode
    {
        get { return RadarBlock.GetValueBool("ActiveMode"); }
        set { RadarBlock.SetValueBool("ActiveMode", value); }
    }

    public IMyRadar(IMyTerminalBlock RadarBlock)
    {
        this.RadarBlock = RadarBlock as IMyUpgradeModule;
        if (this.RadarBlock == null) throw new ArgumentNullException("Radar(): Block is not valid");
        if (this.RadarBlock.GetValue<HashSet<MyDetectedEntityInfo>>("RadarData") == null) throw new ArgumentException("Radar(): supplied block isn't a Radar");
    }

    public bool CanScan(MyDetectedEntityInfo Target)
    {
        var CanScanFunc = RadarBlock.GetValue<Func<MyDetectedEntityInfo, bool>>("CanScan");
        if (CanScanFunc == null) return false;
        return CanScanFunc(Target);
    }

    public List<TargetBlock> ScanTarget(MyDetectedEntityInfo Target)
    {
        var SerializedData = RadarBlock.GetValue<Func<MyDetectedEntityInfo, List<Dictionary<string, string>>>>("ScanTarget")(Target);
        if (SerializedData == null) return null;
        List<TargetBlock> retval = new List<TargetBlock>();
        foreach (var Data in SerializedData) retval.Add(new TargetBlock(Data));
        return retval;
    }

    public class TargetBlock
    {
        public readonly string TypeID;
        public readonly string SubtypeID;
        public readonly long EntityID;
        public readonly long GridID;
        public readonly bool? Enabled;
        public readonly bool Functional;
        public readonly Vector3D WorldPosition;
        public readonly long OwnerID;
        public readonly bool Accessible;

        public TargetBlock(Dictionary<string, string> SerializedData)
        {
            TypeID = SerializedData["Type"];
            SubtypeID = SerializedData["Subtype"];
            EntityID = long.Parse(SerializedData["EntityID"]);
            GridID = long.Parse(SerializedData["Grid"]);
            string enabled = SerializedData["Enabled"];
            if (enabled == "True") Enabled = true;
            if (enabled == "False") Enabled = false;
            if (enabled == "null") Enabled = null;
            Functional = bool.Parse(SerializedData["Functional"]);
            Vector3D.TryParse(SerializedData["WorldPosition"], out WorldPosition);
            OwnerID = long.Parse(SerializedData["OwnerID"]);
            Accessible = bool.Parse(SerializedData["Accessible"]);
        }
    }
}

#endregion