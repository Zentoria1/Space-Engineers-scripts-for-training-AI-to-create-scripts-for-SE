//------------------------------------------------------------
//
// GrunkBot 0.22
// The public Weaponcore supported aimbot / target system
// ( No more shall the tyranny of selfish pvpers hold reign )
//
// With thanks to Sogeki, Whiplash and Darkstar
//
//------------------------------------------------------------
//
// GrunkBot Usage Instructions ...
//
// You require :
// One Remote Control block.  It will be used for aiming, align it with your weaponry.
// At least one Gyroscope.
// One control seat that you sit in.
// One weaponcore target provider (A gun or turret)
//
// The GrunkBot starts deactivated.  It will track all targets regardless.
// All target data will be shared with Whips Radar API (Acts as Whips Radar Weaponcore Fix)
// Activate/Deactivate the GrunkBot aim assist with a TOGGLE command in the Programmable Block.
//
// Mousewheel targetting will tell the GrunkBot which target to aim at.
//
// OPTIONAL (DISPLAY_TAG) placed in block name containing a LCD is used to display the GrunkBot and Target status.
// OPTIONAL Place (GYROSCOPE_TAG) on specific gyroscopes to only use those for GrunkBot aim alignment.
// OPTIONAL - Make a timer set to turn all gyroscope overrides off in the event your GrunkBot is destroyed.
// OPTIONAL - Group your main weapon (GUN_GROUP) and the first undamaged/working gun found will calculate target leading.
// If you switch on/off weapons or they get destroyed, the target calculations will change to match.

string DISPLAY_TAG = "GrunkDisplay";
string GYROSCOPE_TAG = "xxx";
string GUN_GROUP = "GrunkGuns";

Color G_GUI = new Color(150, 150, 150, 200); // General Grunk GUI Colour 
Color G_TEXT = new Color(150, 150, 150, 200); // General Grunk Text Display Colour 
Color G_BKG = new Color(0, 0, 0, 255); // Grunk Display Background Colour
Color G_HEADER = new Color(0, 200, 0, 200); // Grunk Header Bar Colour 
Color G_TTEXT = new Color(0, 0, 0, 255); // Grunk Headers Text Colour
Color G_FACE = new Color(0, 0, 0, 255); // Grunk Face Colour

int minimumThreat = 0;  // Minimum threat rating for targets to track ( 5 is equal to your own ship )

//
//   SETTINGS BELOW THIS POINT SHOULD NOT BE ADJUSTED UNLESS YOU KNOW WHAT YOU ARE DOING
//                     IF YOUR GYRO TURNING IS BAD - ADD MORE GYRO
//

//------------------------------ This Section Is For PID Tuning ------------------------------

const double DEF_SMALL_GRID_P = 40; // The default proportional gain of small grid gyroscopes
const double DEF_SMALL_GRID_I = 0; // The default integral gain of small grid gyroscopes
const double DEF_SMALL_GRID_D = 13; // The default derivative gain of small grid gyroscopes

const double DEF_BIG_GRID_P = 15; // The default proportional gain of large grid gyroscopes
const double DEF_BIG_GRID_I = 0; // The default integral gain of large grid gyroscopes
const double DEF_BIG_GRID_D = 7; // The default derivative gain of large grid gyroscopes

double AIM_P = 0; // The proportional gain of the gyroscope turning (Set useDefaultPIDValues to true to use default values)
double AIM_I = 0; // The integral gain of the gyroscope turning (Set useDefaultPIDValues to true to use default values)
double AIM_D = 0; // The derivative gain of the gyroscope turning (Set useDefaultPIDValues to true to use default values)
double AIM_LIMIT = 60; // Limit value of both yaw and pitch combined

double INTEGRAL_WINDUP_LIMIT = 0; // Integral value limit to minimize integral windup. Zero means no limit

//------------------------------ Below Is Main Script Body ------------------------------

IMyRemoteControl aimPointBlock;
List<IMyShipController> cockpits;
IMyShipController controlledCockpit;
IMyTextSurface statusPanel;
List<IMyTerminalBlock> displays;
List<IMyTerminalBlock> grunkGuns;
IMyTerminalBlock grunkLeadGun;

GyroControl gyroControl;
PIDController yawController;
PIDController pitchController;
PIDController rollController;

MatrixD refWorldMatrix;
MatrixD refLookAtMatrix;

Vector3D targetPosition;
Vector3D aimDirection;

bool botactivated = false;
bool init = false;
string IFF = "";
string[] relationships = { "NEUTRAL", "HOSTILE", "FRIENDLY", "ERROR" };
Color IFF_Color = new Color(0, 0, 0, 0);
RectangleF surfaceViewPort;
Vector2 centerPos;

Random rnd = new Random();

const double DIST_FACTOR = Math.PI / 2;
const double RPM_FACTOR = 1800 / Math.PI;
const double ACOS_FACTOR = 180 / Math.PI;
const float GYRO_FACTOR = (float)(Math.PI / 30);
const double RADIAN_FACTOR = Math.PI / 180;

const float SECOND = 60f;

const string IGC_TAG = "IGC_IFF_MSG";
IMyBroadcastListener broadcastListener;
float minimumOffRat = 1;

public static WcPbApi api;

Program() {
    Runtime.UpdateFrequency = UpdateFrequency.Update1;
    broadcastListener = IGC.RegisterBroadcastListener(IGC_TAG);
    broadcastListener.SetMessageCallback(IGC_TAG);

    api = new WcPbApi();  // initialize weaponcore API
    try
    {
        Echo("Activating Weaponcore API...");
        api.Activate(Me);
    }
    catch
    {
        Echo("WeaponCore Api is failing! \n Make sure WeaponCore is enabled!"); return;
    }
}

void Main(string arguments)
{

    Dictionary<MyDetectedEntityInfo, float> dict = new Dictionary<MyDetectedEntityInfo, float>(); // Collect threat list dictionary
    MyDetectedEntityInfo entityInfo = api._getAiFocus(Me.CubeGrid.EntityId, 0);  // grab current target info
    api.GetSortedThreats(Me, dict);

    if (gyroControl == null || aimPointBlock == null)  // If somethings missing, re-run init
    {
        init = false;
    }

    if (!init)
    {
        Echo("Initializing initial initialization...");

        InitScript();
        botactivated = false;  // GrunkBot starts asleep.
        init = true;
        return;
    }

    if (arguments.Length > 0)  // process command block arguments
    {
        ProcessCommand(arguments);
    }
    if (botactivated == true)       //  Begin primary Grunk Processes if GrunkBot is turned on
    {
        Echo("GrunkBot Activated.");
        if (entityInfo.EntityId > 0)
        {
            refWorldMatrix = aimPointBlock.WorldMatrix;
            refLookAtMatrix = MatrixD.CreateLookAt(Vector3D.Zero, refWorldMatrix.Forward, refWorldMatrix.Up);
            foreach (IMyTerminalBlock block in grunkGuns)  // check Group GrunkGun for one active gun
            {
                if (block.IsWorking && api.HasCoreWeapon(block))
                {
                    Echo("Active Grunk Gun " + block.CustomName + " being used for target leading.");
                    grunkLeadGun = block;
                    break;
                }
                else grunkLeadGun = null;
            }
            if (grunkLeadGun != null)  // if leading gun exists, leading is calculated
            {
                aimDirection = api.GetPredictedTargetPosition(grunkLeadGun, entityInfo.EntityId, 0).Value;
                aimDirection = aimDirection - refWorldMatrix.Translation;
            }
            else
            {
                Echo("Aiming without target leading.");
                targetPosition = entityInfo.BoundingBox.Center;
                aimDirection = targetPosition - refWorldMatrix.Translation;
            }
            aimDirection = Vector3D.Normalize(Vector3D.TransformNormal(aimDirection, refLookAtMatrix));
            AimAtTarget(aimDirection); // Begin Gyro Aiming process
        }
        if (!controlledCockpit.IsUnderControl)       // get cockpit for enabling rolling
        {
            for (int i = 0; i < cockpits.Count; i++)
            {
                if (cockpits[i].IsUnderControl)
                {
                    controlledCockpit = cockpits[i];
                    break;
                }
            }
        }
    }
    else
    {
        //		Echo("GrunkBot Deactivated.");
        gyroControl.ResetGyro();
        gyroControl.SetGyroOverride(false);
    }

    // Weaponcore target transponder sending self to friendly ships
    var myTuple = new MyTuple<byte, long, Vector3D, double>((byte)2, Me.CubeGrid.EntityId, Me.CubeGrid.WorldAABB.Center, 0);
    IGC.SendBroadcastMessage(IGC_TAG, myTuple);

    // Begin threat data processing
    if (api.HasGridAi(Me.CubeGrid.EntityId))
    {
        //        Echo("Sending Threat data to Radar");
        foreach (MyDetectedEntityInfo threats in dict.Keys)
        {
            if (dict[threats] >= minimumOffRat)
            {
                if (!threats.IsEmpty())
                {
                    if (threats.Type == MyDetectedEntityType.SmallGrid || threats.Type == MyDetectedEntityType.LargeGrid)
                    {
                        MyRelationsBetweenPlayerAndBlock relation = threats.Relationship;
                        int relationvalue = 1;
                        switch (relation)
                        {
                            case MyRelationsBetweenPlayerAndBlock.Enemies: { relationvalue = 1; break; };
                            case MyRelationsBetweenPlayerAndBlock.NoOwnership: { relationvalue = 0; break; };
                            case MyRelationsBetweenPlayerAndBlock.Owner: { relationvalue = 2; break; };
                            case MyRelationsBetweenPlayerAndBlock.FactionShare: { relationvalue = 2; break; };
                            case MyRelationsBetweenPlayerAndBlock.Neutral: { relationvalue = 0; break; };
                            case MyRelationsBetweenPlayerAndBlock.Friends: { relationvalue = 2; break; };
                        }
                        myTuple = new MyTuple<byte, long, Vector3D, double>((byte)relationvalue, threats.EntityId, threats.BoundingBox.Center, 0);

                        if (entityInfo.EntityId == threats.EntityId) // Assign IFF tag if current target
                        {
                            IFF = relationships[relationvalue].ToString();
                        }
                    }
                    else
                    {
                        myTuple = new MyTuple<byte, long, Vector3D, double>((byte)1, threats.EntityId, threats.BoundingBox.Center, 0);
                    }
                    IGC.SendBroadcastMessage(IGC_TAG, myTuple);
                }
            }
        }
        //	Echo("Sending Target Data to Whips Radar API.");
    }

    if (displays != null)  // Begin Grunk LCD Drawing
    {
        foreach (IMyTextSurfaceProvider block in displays)
        {
            if (block is IMyTextSurface)
            {
                statusPanel = block.GetSurface(0);
            }
            else if (block is IMyTextSurfaceProvider)
            {
                statusPanel = block.GetSurface(0);
            }
            var frame = statusPanel.DrawFrame();

            //  Collect Strings to write to Grunk Display

            {
                // Calculate the viewport by centering the surface size onto the texture size
                surfaceViewPort = new RectangleF((statusPanel.TextureSize - statusPanel.SurfaceSize) / 2f, statusPanel.SurfaceSize);
                statusPanel.ScriptBackgroundColor = G_BKG;
                statusPanel.ContentType = ContentType.SCRIPT;
                statusPanel.Script = "";
            }

            //	Adjust for LCD size
            float scale = (statusPanel.SurfaceSize.X / 512);
            centerPos = new Vector2((scale * 256), (scale * 256)) + surfaceViewPort.Position;

            // Draw Grunk Sprites
            frame.Add(new MySprite(SpriteType.TEXTURE, "SquareSimple", new Vector2(0f, -215f) * scale + centerPos, new Vector2(512f, 100f) * scale, G_HEADER, null, TextAlignment.CENTER, 0f)); // GrunkBox
            frame.Add(new MySprite(SpriteType.TEXT, "G       B", new Vector2(-229f, -260f) * scale + centerPos, null, G_TTEXT, "DEBUG", TextAlignment.LEFT, 3.2f * scale)); // GRUNK
            frame.Add(new MySprite(SpriteType.TEXT, "RUNK   OT", new Vector2(-177f, -241f) * scale + centerPos, null, G_TTEXT, "DEBUG", TextAlignment.LEFT, 2.4f * scale)); // BoT
            frame.Add(new MySprite(SpriteType.TEXTURE, "SquareHollow", new Vector2(-146f, 143f) * scale + centerPos, new Vector2(200f, 210f) * scale, G_GUI, null, TextAlignment.CENTER, 0f)); // MechanicsBox
            frame.Add(new MySprite(SpriteType.TEXTURE, "SquareSimple", new Vector2(-146f, 60f) * scale + centerPos, new Vector2(190f, 40f) * scale, G_GUI, null, TextAlignment.CENTER, 0f)); // MechanicsTitleBox
            frame.Add(new MySprite(SpriteType.TEXT, "MECHANICS", new Vector2(-235f, 39f) * scale + centerPos, null, G_TTEXT, "DEBUG", TextAlignment.LEFT, 1.2f * scale)); // MechanicsTitle
            frame.Add(new MySprite(SpriteType.TEXTURE, "SquareSimple", new Vector2(-146f, 161f) * scale + centerPos, new Vector2(190f, 10f) * scale, G_GUI, null, TextAlignment.CENTER, 0f)); // MechanicsDivider
            frame.Add(new MySprite(SpriteType.TEXTURE, "SquareHollow", new Vector2(108f, 143f) * scale + centerPos, new Vector2(280f, 210f) * scale, G_GUI, null, TextAlignment.CENTER, 0f)); // ThreatsBox
            frame.Add(new MySprite(SpriteType.TEXTURE, "SquareSimple", new Vector2(108f, 60f) * scale + centerPos, new Vector2(275f, 40f) * scale, G_GUI, null, TextAlignment.CENTER, 0f)); // ThreatTitleBox
            frame.Add(new MySprite(SpriteType.TEXT, "AGGREGATOR", new Vector2(15f, 39f) * scale + centerPos, null, G_TTEXT, "DEBUG", TextAlignment.LEFT, 1.2f * scale)); // ThreatTitle
            if (botactivated == false)
            { // draw mellow grunkbot
                frame.Add(new MySprite(SpriteType.TEXTURE, "SquareSimple", new Vector2(187f, -183f) * scale + centerPos, new Vector2(20f, 10f) * scale, G_FACE, null, TextAlignment.CENTER, 0f)); // FaceMouth2
                frame.Add(new MySprite(SpriteType.TEXTURE, "SquareSimple", new Vector2(205f, -185f) * scale + centerPos, new Vector2(20f, 10f) * scale, G_FACE, null, TextAlignment.CENTER, -0.1745f)); // FaceMouth1
                frame.Add(new MySprite(SpriteType.TEXTURE, "SemiCircle", new Vector2(204f, -229f) * scale + centerPos, new Vector2(20f, 30f) * scale, G_TEXT, null, TextAlignment.CENTER, -3.1416f)); // FaceEyeRight
                frame.Add(new MySprite(SpriteType.TEXTURE, "SemiCircle", new Vector2(157f, -225f) * scale + centerPos, new Vector2(20f, 30f) * scale, G_TEXT, null, TextAlignment.CENTER, -3.1416f)); // FaceEyeLeft
                frame.Add(new MySprite(SpriteType.TEXTURE, "SquareSimple", new Vector2(213f, -240f) * scale + centerPos, new Vector2(25f, 10f) * scale, G_FACE, null, TextAlignment.CENTER, 3.3859f)); // FaceBrowRight
                frame.Add(new MySprite(SpriteType.TEXTURE, "SquareSimple", new Vector2(147f, -235f) * scale + centerPos, new Vector2(25f, 10f) * scale, G_FACE, null, TextAlignment.CENTER, -3.4732f)); // FaceBrowLeft
            }
            else
            { // draw angry grunkbot
                frame.Add(new MySprite(SpriteType.TEXTURE, "SemiCircle", new Vector2(209f, -239f) * scale + centerPos, new Vector2(50f, 50f) * scale, G_FACE, null, TextAlignment.CENTER, 2.618f)); // FaceEyeRightAngryCopy
                frame.Add(new MySprite(SpriteType.TEXTURE, "SquareSimple", new Vector2(201f, -184f) * scale + centerPos, new Vector2(30f, 10f) * scale, G_FACE, null, TextAlignment.CENTER, -0.1745f)); // FaceMouthSmallCopy
                frame.Add(new MySprite(SpriteType.TEXTURE, "SemiCircle", new Vector2(146f, -226f) * scale + centerPos, new Vector2(50f, 50f) * scale, G_FACE, null, TextAlignment.CENTER, -2.9671f)); // FaceEyeLeftAngryCopy
            }
            frame.Add(new MySprite(SpriteType.TEXTURE, "SquareHollow", new Vector2(0f, -20f) * scale + centerPos, new Vector2(250f, 75f) * scale, G_GUI, null, TextAlignment.CENTER, 0f)); // IFFTargetBox

            if (entityInfo.EntityId < 1)
            {
                frame.Add(new MySprite(SpriteType.TEXT, "           NO TARGET", new Vector2(-200f, -127f) * scale + centerPos, null, G_TEXT, "DEBUG", TextAlignment.LEFT, 1f * scale)); // NAME
                frame.Add(new MySprite(SpriteType.TEXTURE, "SquareHollow", new Vector2(0f, -110f) * scale + centerPos, new Vector2(450f, 75f) * scale, G_GUI, null, TextAlignment.CENTER, 0f)); // TargetNameBox
            }
            else
            {
                frame.Add(new MySprite(SpriteType.TEXT, entityInfo.Name, new Vector2(-200f, -127f) * scale + centerPos, null, G_TEXT, "DEBUG", TextAlignment.LEFT, 1f * scale)); // NAME
                frame.Add(new MySprite(SpriteType.TEXTURE, "SquareSimple", new Vector2(261f, -110f) * scale + centerPos, new Vector2(100f, 70f) * scale, G_BKG, null, TextAlignment.CENTER, 0f)); // TargetNameObscurer
                frame.Add(new MySprite(SpriteType.TEXTURE, "SquareHollow", new Vector2(0f, -110f) * scale + centerPos, new Vector2(450f, 75f) * scale, G_GUI, null, TextAlignment.CENTER, 0f)); // TargetNameBox

                switch (IFF)    //  Set colours for IFF Target Indicator
                {
                    case "HOSTILE": { IFF_Color = new Color(255, 0, 0, 255); break; };
                    case "FRIENDLY": { IFF_Color = new Color(0, 50, 150, 200); break; };
                    case "NEUTRAL": { IFF_Color = new Color(150, 150, 0, 200); break; };
                    case "ERROR": { IFF_Color = new Color(0, 0, 0, 0); break; };
                }
                frame.Add(new MySprite(SpriteType.TEXT, IFF, new Vector2(-75f, -43f) * scale + centerPos, null, IFF_Color, "DEBUG", TextAlignment.LEFT, 1.4f * scale)); // IFF 
                frame.Add(new MySprite(SpriteType.TEXTURE, "Triangle", new Vector2(180f, -20f) * scale + centerPos, new Vector2(50f, 75f) * scale, IFF_Color, null, TextAlignment.CENTER, -1.5708f)); // IFFRightTriangle
                frame.Add(new MySprite(SpriteType.TEXTURE, "Triangle", new Vector2(-180f, -20f) * scale + centerPos, new Vector2(50f, 75f) * scale, IFF_Color, null, TextAlignment.CENTER, 1.5708f)); // IFFLeftTriangle

                double targetSpeed;
                targetSpeed = (Math.Pow(entityInfo.Velocity.X, 2) + Math.Pow(entityInfo.Velocity.Y, 2) + Math.Pow(entityInfo.Velocity.Z, 2));

                frame.Add(new MySprite(SpriteType.TEXT, (Math.Round(Vector3D.Distance(entityInfo.Position, aimPointBlock.GetPosition()), 0) / 1000) + " KM", new Vector2(-232f, 204f) * scale + centerPos, null, G_TEXT, "DEBUG", TextAlignment.LEFT, 1f * scale)); // DistanceStat
                frame.Add(new MySprite(SpriteType.TEXT, "VELOCITY", new Vector2(-233f, 83f) * scale + centerPos, null, G_TEXT, "DEBUG", TextAlignment.LEFT, 1f * scale)); // Velocity
                frame.Add(new MySprite(SpriteType.TEXT, Math.Round(Math.Sqrt(targetSpeed), 2).ToString() + " m/s", new Vector2(-232f, 117f) * scale + centerPos, null, G_TEXT, "DEBUG", TextAlignment.LEFT, 1f * scale)); // VelocityStat
                frame.Add(new MySprite(SpriteType.TEXT, "DISTANCE", new Vector2(-232f, 170f) * scale + centerPos, null, G_TEXT, "DEBUG", TextAlignment.LEFT, 1f * scale)); // Distance
            }
            if (dict.Keys != null)
            {
                string allThreats = "";
                foreach (MyDetectedEntityInfo threats in dict.Keys)
                {
                    if (dict[threats] >= minimumOffRat) allThreats = allThreats + threats.Name + "\n";
                }
                frame.Add(new MySprite(SpriteType.TEXT, allThreats, new Vector2(-15f, 83f) * scale + centerPos, null, G_TEXT, "DEBUG", TextAlignment.LEFT, 0.8f * scale)); // Targets
                frame.Add(new MySprite(SpriteType.TEXTURE, "SquareSimple", new Vector2(259f, 180f) * scale + centerPos, new Vector2(40f, 200f) * scale, G_BKG, null, TextAlignment.CENTER, 0f)); // ThreatObscurerRight
                frame.Add(new MySprite(SpriteType.TEXTURE, "SquareSimple", new Vector2(117f, 261f) * scale + centerPos, new Vector2(280f, 40f) * scale, G_BKG, null, TextAlignment.CENTER, 0f)); // ThreatObscurerBelow
                frame.Add(new MySprite(SpriteType.TEXTURE, "SquareHollow", new Vector2(108f, 143f) * scale + centerPos, new Vector2(280f, 210f) * scale, G_GUI, null, TextAlignment.CENTER, 0f)); // ThreatsBox
            }
            frame.Dispose();
        }
    }
}

void ProcessCommand(string command)  // process the TOGGLE and switch gyro overrides when called for
{
    if (command.Equals("TOGGLE"))
    {
        if (botactivated == false)
        {
            botactivated = true;
        }
        else
        {
            botactivated = false;
            gyroControl.ResetGyro();
            gyroControl.SetGyroOverride(false);
        }
    }
}

// Aiming process

void AimAtTarget(Vector3D targetVector)
{
    //---------- Activate Gyroscopes To Turn Towards Target ----------
    Vector3D yawVector = new Vector3D(targetVector.GetDim(0), 0, targetVector.GetDim(2));
    Vector3D pitchVector = new Vector3D(0, targetVector.GetDim(1), targetVector.GetDim(2));
    yawVector.Normalize();
    pitchVector.Normalize();

    double yawInput = Math.Acos(yawVector.Dot(Vector3D.Forward)) * GetMultiplierSign(targetVector.GetDim(0));
    double pitchInput = Math.Acos(pitchVector.Dot(Vector3D.Forward)) * GetMultiplierSign(targetVector.GetDim(1));

    //---------- PID Controller Adjustment ----------

    yawInput = yawController.Filter(yawInput, 2);
    pitchInput = pitchController.Filter(pitchInput, 2);

    if (Math.Abs(yawInput) + Math.Abs(pitchInput) > AIM_LIMIT)
    {
        double adjust = AIM_LIMIT / (Math.Abs(yawInput) + Math.Abs(pitchInput));
        yawInput *= adjust;
        pitchInput *= adjust;
    }

    //---------- Set Gyroscope Parameters ----------

    gyroControl.SetGyroOverride(true);
    gyroControl.SetGyroRates((float)yawInput, (float)pitchInput, controlledCockpit.RollIndicator * -30);
}

int GetMultiplierSign(double value)
{
    return (value < 0 ? -1 : 1);
}

//------------------------------ Initialization Methods ------------------------------

void InitScript()
{

    // threats of targets to collect.
    if (minimumThreat >= 9)
        minimumOffRat = 5F;
    if (minimumThreat == 8)
        minimumOffRat = 4F;
    if (minimumThreat == 7)
        minimumOffRat = 3F;
    if (minimumThreat == 6)
        minimumOffRat = 2F;
    if (minimumThreat == 5)
        minimumOffRat = 1F;
    if (minimumThreat == 4)
        minimumOffRat = 0.5F;
    if (minimumThreat == 3)
        minimumOffRat = 0.25F;
    if (minimumThreat == 2)
        minimumOffRat = 0.125F;
    if (minimumThreat == 1)
        minimumOffRat = 0.0625F;
    if (minimumThreat <= 0)
        minimumOffRat = 0F;

    aimPointBlock = GetAimPointBlock();
    if (aimPointBlock == null) throw new Exception("--- Failed to get Remote Control for Aiming ---");

    List<IMyGyro> gyros = GetGyroscopes();
    if (gyros == null) throw new Exception("--- Failed to get Gyroscopes ---");

    cockpits = GetCockpits();
    controlledCockpit = aimPointBlock;
    refWorldMatrix = aimPointBlock.WorldMatrix;
    refLookAtMatrix = MatrixD.CreateLookAt(Vector3D.Zero, refWorldMatrix.Forward, refWorldMatrix.Up);

    gyroControl = new GyroControl(gyros, ref refWorldMatrix);
    InitPIDControllers();

    displays = GetScreens();  // Collect LCD Displays
    grunkGuns = GetGrunkGuns();  // Collects Grunk Gun blocks.
}

List<IMyTerminalBlock> GetGrunkGuns()  // grab all screens tagged or PB if none found
{
    IMyBlockGroup gunGroup = GridTerminalSystem.GetBlockGroupWithName(GUN_GROUP);
    if (gunGroup == null)
    {
        Echo("No Grunk Gun group found. Target leading disabled.");
        return null;
    }
    List<IMyTerminalBlock> blocks = new List<IMyTerminalBlock>();
    gunGroup.GetBlocks(blocks);
    if (blocks.Count > 0)
    {
        Echo(blocks.Count.ToString() + " Grunk Gun group blocks found. Target leading enabled.");
        return blocks;
    }
    else
    {
        Echo("No blocks found in Gun group. Target leading disabled.");
        return null;
    }
}

List<IMyTerminalBlock> GetScreens()  // grab all screens tagged or PB if none found
{
    List<IMyTerminalBlock> blocks = new List<IMyTerminalBlock>();
    GridTerminalSystem.GetBlocksOfType<IMyTerminalBlock>(blocks, b => b.CustomName.Contains(DISPLAY_TAG));
    if (blocks.Count > 0)
    {
        foreach (IMyTerminalBlock block in blocks)
        {
            if (block is IMyTextSurface) // If the screen is an LCD block
            {
                Echo("Single LCD found.");
            }
            else if (block is IMyTextSurfaceProvider) // If the screen is a cockpit / programmable block
            {
                //		statusPanel = ((IMyTextSurfaceProvider)statusProvider).GetSurface(SCREEN_NUMBER);
                Echo("Multi display LCD found.");
            }
        }
        return blocks;
    }
    else
    {
        Echo("no LCDs found.");
        return null;
    }
}

IMyRemoteControl GetAimPointBlock()
{
    List<IMyRemoteControl> blocks = new List<IMyRemoteControl>();
    GridTerminalSystem.GetBlocksOfType<IMyRemoteControl>(blocks, b => b.CubeGrid == Me.CubeGrid);
    if (blocks.Count > 1)
    {
        Echo("Error: More than one Aim Point Remote Control block found. Please remove one of them.");
        return null;
    }
    if (blocks.Count == 1)
    {
        Echo("Aiming Remote Control found.");
        return (IMyRemoteControl)blocks[0];
    }
    return null;
}

List<IMyShipController> GetCockpits()   // collect cockpits and controllers
{
    List<IMyShipController> blocks = new List<IMyShipController>();
    GridTerminalSystem.GetBlocksOfType<IMyShipController>(blocks);
    if (blocks.Count > 0)
    {
        Echo(blocks.Count.ToString() + " Ship Controllers found.");
        return blocks;
    }
    else
    {
        Echo("Error: No Cockpits or Controllers detected. Please add at least one.");
        return null;
    }
}

List<IMyGyro> GetGyroscopes()
{
    List<IMyGyro> blocks = new List<IMyGyro>();
    //  List < IMyTerminalBlock > blocks = GridTerminalSystem.GetBlocksWithName < IMyGyro > (GYROSCOPE_TAG);
    GridTerminalSystem.GetBlocksOfType<IMyGyro>(blocks, b => b.CustomName.Contains(GYROSCOPE_TAG));
    if (blocks.Count > 0)
    {
        Echo(blocks.Count.ToString() + " name tagged Gyroscope found.");
        return blocks;
    }

    //  GridTerminalSystem.GetBlocksOfType<IMyGyro>(blocks,b => b.CubeGrid == Me.CubeGrid);
    GridTerminalSystem.GetBlocksOfType<IMyGyro>(blocks, b => b.CubeGrid == Me.CubeGrid);
    if (blocks.Count > 0)
    {
        Echo(blocks.Count.ToString() + " unnamed Gyroscope found.");
        return blocks;
    }
    else
    {
        Echo("Error: No Gyroscopes detected. Please add at least one.");
        return null;
    }
}

void InitPIDControllers()
{    //---------- Setup PID Controller ----------

    if (AIM_P + AIM_I + AIM_D < 0.001)
    {
        if (Me.CubeGrid.ToString().Contains("Large"))
        {
            AIM_P = DEF_BIG_GRID_P;
            AIM_I = DEF_BIG_GRID_I;
            AIM_D = DEF_BIG_GRID_D;
        }
        else
        {
            AIM_P = DEF_SMALL_GRID_P;
            AIM_I = DEF_SMALL_GRID_I;
            AIM_D = DEF_SMALL_GRID_D;
            AIM_LIMIT *= 2;
        }
    }
    yawController = new PIDController(AIM_P, AIM_I, AIM_D, INTEGRAL_WINDUP_LIMIT, -INTEGRAL_WINDUP_LIMIT, SECOND);
    pitchController = new PIDController(AIM_P, AIM_I, AIM_D, INTEGRAL_WINDUP_LIMIT, -INTEGRAL_WINDUP_LIMIT, SECOND);
    rollController = new PIDController(AIM_P, AIM_I, AIM_D, INTEGRAL_WINDUP_LIMIT, -INTEGRAL_WINDUP_LIMIT, SECOND);
}

//------------------------------ Gyro Control API ------------------------------
public class GyroControl
{
    string[] profiles = {
    "Yaw",
    "Yaw",
    "Pitch",
    "Pitch",
    "Roll",
    "Roll"
  };

    List<IMyGyro> gyros;

    private byte[] gyroYaw;
    private byte[] gyroPitch;
    private byte[] gyroRoll;

    public GyroControl(List<IMyGyro> newGyros, ref MatrixD refWorldMatrix)
    {
        gyros = new List<IMyGyro>(newGyros.Count);

        gyroYaw = new byte[newGyros.Count];
        gyroPitch = new byte[newGyros.Count];
        gyroRoll = new byte[newGyros.Count];

        int index = 0;
        foreach (IMyGyro block in newGyros)
        {
            IMyGyro gyro = block as IMyGyro;
            if (gyro != null)
            {
                gyroYaw[index] = SetRelativeDirection(gyro.WorldMatrix.GetClosestDirection(refWorldMatrix.Up));
                gyroPitch[index] = SetRelativeDirection(gyro.WorldMatrix.GetClosestDirection(refWorldMatrix.Left));
                gyroRoll[index] = SetRelativeDirection(gyro.WorldMatrix.GetClosestDirection(refWorldMatrix.Forward));

                gyros.Add(gyro);

                index++;
            }
        }
    }

    public byte SetRelativeDirection(Base6Directions.Direction dir)
    {
        switch (dir)
        {
            case Base6Directions.Direction.Up:
                return 0;
            case Base6Directions.Direction.Down:
                return 1;
            case Base6Directions.Direction.Left:
                return 2;
            case Base6Directions.Direction.Right:
                return 3;
            case Base6Directions.Direction.Forward:
                return 5;
            case Base6Directions.Direction.Backward:
                return 4;
        }
        return 0;
    }
    public void SetGyroRates(float yawRate, float pitchRate, float rollRate)    // Applies turn rates to each gyro here
    {
        for (int i = 0; i < gyros.Count; i++)
        {
            byte Yindex = gyroYaw[i];
            gyros[i].SetValue(profiles[Yindex], (Yindex % 2 == 0 ? yawRate : -yawRate) * MathHelper.RadiansPerSecondToRPM);
            byte Pindex = gyroPitch[i];
            gyros[i].SetValue(profiles[Pindex], (Pindex % 2 == 0 ? pitchRate : -pitchRate) * MathHelper.RadiansPerSecondToRPM);
            byte Rindex = gyroRoll[i];
            gyros[i].SetValue(profiles[Rindex], (Rindex % 2 == 0 ? rollRate : -rollRate) * MathHelper.RadiansPerSecondToRPM);
            //		gyros[i].(profiles[Yindex]) = ((Yindex % 2 == 0 ? yawRate : -yawRate) * MathHelper.RadiansPerSecondToRPM);
            //		gyros[i].(profiles[Pindex]) = ((Pindex % 2 == 0 ? pitchRate : -pitchRate) * MathHelper.RadiansPerSecondToRPM);
            //		gyros[i].(profiles[Rindex]) = ((Rindex % 2 == 0 ? rollRate : -rollRate) * MathHelper.RadiansPerSecondToRPM);

            /* use this with state machine to add pauses to this gyro shit. every 50 maybe.
               int a = 10; int b = 5;

               // is a a multiple of b 
               if ( a % b == 0 )  ....
            */
        }
    }

    public void SetGyroOverride(bool bOverride)
    {
        foreach (IMyGyro gyro in gyros)
        {
            if (gyro.GyroOverride != bOverride)
            {
                gyro.GyroOverride = bOverride;
            }
        }
    }

    public void ResetGyro()
    {
        foreach (IMyGyro gyro in gyros)
        {
            gyro.Yaw = 0f;
            gyro.Pitch = 0f;
            gyro.Roll = 0f;
        }
    }
}

public class PIDController
{
    double integral;
    double lastInput;

    double gain_p;
    double gain_i;
    double gain_d;
    double upperLimit_i;
    double lowerLimit_i;
    double second;

    public PIDController(double pGain, double iGain, double dGain, double iUpperLimit = 0, double iLowerLimit = 0, float stepsPerSecond = 60f)
    {
        gain_p = pGain;
        gain_i = iGain;
        gain_d = dGain;
        upperLimit_i = iUpperLimit;
        lowerLimit_i = iLowerLimit;
        second = stepsPerSecond;
    }

    public double Filter(double input, int round_d_digits)
    {
        double roundedInput = Math.Round(input, round_d_digits);

        integral = integral + (input / second);
        integral = (upperLimit_i > 0 && integral > upperLimit_i ? upperLimit_i : integral);
        integral = (lowerLimit_i < 0 && integral < lowerLimit_i ? lowerLimit_i : integral);

        double derivative = (roundedInput - lastInput) * second;
        lastInput = roundedInput;

        return (gain_p * input) + (gain_i * integral) + (gain_d * derivative);
    }

    public void Reset()
    {
        integral = lastInput = 0;
    }
}

public class WcPbApi
{
    public Func<long, int, MyDetectedEntityInfo> _getAiFocus;
    private Action<IMyTerminalBlock, IDictionary<MyDetectedEntityInfo, float>> _getSortedThreats;
    private Func<long, bool> _hasGridAi;
    private Func<IMyTerminalBlock, bool> _hasCoreWeapon;
    private Func<IMyTerminalBlock, long, int, Vector3D?> _getPredictedTargetPos;

    /*
     *  Tries to setup the Api if WC is loaded
     */
    public bool Activate(IMyTerminalBlock pbBlock)
    {
        var dict = pbBlock.GetProperty("WcPbAPI")?.As<IReadOnlyDictionary<string, Delegate>>().GetValue(pbBlock);
        if (dict == null) throw new Exception($"WcPbAPI failed to activate");
        return ApiAssign(dict);
    }

    /*
     *  Tries to assign the Api delegates to fields of this class
     */
    public bool ApiAssign(IReadOnlyDictionary<string, Delegate> delegates)
    {
        if (delegates == null)
            return false;

        AssignMethod(delegates, "GetAiFocus", ref _getAiFocus);
        AssignMethod(delegates, "GetSortedThreats", ref _getSortedThreats);
        AssignMethod(delegates, "HasGridAi", ref _hasGridAi);
        AssignMethod(delegates, "HasCoreWeapon", ref _hasCoreWeapon);
        AssignMethod(delegates, "GetPredictedTargetPosition", ref _getPredictedTargetPos);

        return true;
    }

    /*
     *  Tries to assign delegate methods to fields while checking for identical types.
     */
    private void AssignMethod<T>(IReadOnlyDictionary<string, Delegate> delegates, string name, ref T field) where T : class
    {
        if (delegates == null)
        {
            field = null;
            return;
        }
        Delegate del;
        if (!delegates.TryGetValue(name, out del))
            throw new Exception($"{GetType().Name} :: Couldn't find {name} delegate of type {typeof(T)}");
        field = del as T;
        if (field == null)
            throw new Exception(
                $"{GetType().Name} :: Delegate {name} is not type {typeof(T)}, instead it's: {del.GetType()}");
    }
    public void GetSortedThreats(IMyTerminalBlock pbBlock, IDictionary<MyDetectedEntityInfo, float> collection) =>
        _getSortedThreats?.Invoke(pbBlock, collection);
    public bool HasGridAi(long entity) => _hasGridAi?.Invoke(entity) ?? false;
    public bool HasCoreWeapon(IMyTerminalBlock weapon) => _hasCoreWeapon?.Invoke(weapon) ?? false;
    public Vector3D? GetPredictedTargetPosition(IMyTerminalBlock weapon, long targetEnt, int weaponId) =>
        _getPredictedTargetPos?.Invoke(weapon, targetEnt, weaponId) ?? null;
}
